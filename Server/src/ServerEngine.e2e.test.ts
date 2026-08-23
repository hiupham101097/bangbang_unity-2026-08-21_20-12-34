import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import WebSocket, { WebSocketServer } from 'ws';
import { ServerEngine } from './ServerEngine';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

type Message = { type: string; reqId?: string; data: any };

class Peer {
    snapshot: any;
    private messages: Message[] = [];
    private waiters: Array<() => void> = [];

    constructor(public socket: WebSocket) {
        socket.on('message', raw => {
            const envelope = JSON.parse(raw.toString());
            const message = { ...envelope, data: typeof envelope.data === 'string' ? JSON.parse(envelope.data) : envelope.data };
            if (message.type === 'room.snapshot') this.snapshot = message.data;
            this.messages.push(message);
            this.waiters.splice(0).forEach(resolve => resolve());
        });
    }

    send(type: string, data: any = {}, reqId?: string) {
        this.socket.send(JSON.stringify({ type, data, reqId }));
    }

    async waitFor(predicate: (message: Message) => boolean, timeoutMs = 5000): Promise<Message> {
        const deadline = Date.now() + timeoutMs;
        while (Date.now() < deadline) {
            const index = this.messages.findIndex(predicate);
            if (index >= 0) return this.messages.splice(index, 1)[0];
            await new Promise<void>(resolve => {
                const timeout = setTimeout(resolve, Math.min(200, Math.max(1, deadline - Date.now())));
                this.waiters.push(() => { clearTimeout(timeout); resolve(); });
            });
        }
        throw new Error('Timed out waiting for server message');
    }

    waitForState(state: string, timeoutMs = 8000) {
        if (this.snapshot?.state === state) return Promise.resolve({ type: 'room.snapshot', data: this.snapshot } as Message);
        return this.waitFor(message => message.type === 'room.snapshot' && message.data.state === state, timeoutMs);
    }

    async waitForSnapshot(predicate: (snapshot: any) => boolean, timeoutMs = 5000): Promise<any> {
        if (this.snapshot && predicate(this.snapshot)) return this.snapshot;
        return (await this.waitFor(message => message.type === 'room.snapshot' && predicate(message.data), timeoutMs)).data;
    }
}

test('real WebSocket flow supports a full 8-player table without leaking hidden roles', { timeout: 25000 }, async () => {
    const stateFile = path.join(os.tmpdir(), `bang-e2e-${process.pid}-${Date.now()}.json`);
    process.env.BANG_STATE_FILE = stateFile;
    const server = http.createServer();
    const wss = new WebSocketServer({ server });
    const engine = new ServerEngine(wss);
    await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));
    const address = server.address();
    assert.ok(address && typeof address !== 'string');
    const peers: Peer[] = [];
    const accessTokens: string[] = [];

    try {
        for (let i = 0; i < 8; i++) {
            const socket = new WebSocket(`ws://127.0.0.1:${address.port}`);
            await new Promise<void>((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
            const peer = new Peer(socket);
            peers.push(peer);
            peer.send('session.resume', { deviceId: `e2e_player_${i}`, clientVersion: 'test' });
            const session = await peer.waitFor(message => message.type === 'session.ready');
            accessTokens.push(session.data.accessToken);
        }

        peers[0].send('room.create', { playerName: 'Host', maxPlayers: 8, turnTimeSec: 10, roleDraftSec: 2, characterDraftSec: 4 }, 'create');
        const created = await peers[0].waitFor(message => message.type === 'room.created');
        const roomId = created.data.roomId;
        for (let i = 1; i < peers.length; i++) {
            peers[i].send('room.join', { roomId, playerName: `Player ${i}` }, `join_${i}`);
            await peers[i].waitFor(message => message.type === 'room.joined');
            peers[i].send('room.ready', { isReady: true });
        }

        await peers[0].waitFor(message => message.type === 'room.snapshot' && message.data.players.length === 8);
        peers[0].send('game.start');
        await Promise.all(peers.map(peer => peer.waitForState('ROLE_DRAFT')));

        for (let i = 0; i < peers.length; i++) {
            const peer = peers[i];
            const snap = peer.snapshot;
            const slot = Array.from({ length: snap.draftSlotCount }, (_, index) => index).find(index => !snap.lockedDraftSlots.includes(index));
            peer.send('draft.role.pick', { slotId: slot, actionId: `role_${i}`, stateRevision: snap.revision });
            await peer.waitForSnapshot(snapshot => !!snapshot.privateState?.roleId);
        }

        await Promise.all(peers.map(peer => peer.waitForState('CHARACTER_DRAFT', 7000)));
        for (let i = 0; i < peers.length; i++) {
            const peer = peers[i];
            for (let pick = 0; pick < 2; pick++) {
                const snap = peer.snapshot;
                const slot = Array.from({ length: snap.draftSlotCount }, (_, index) => index).find(index => !snap.lockedDraftSlots.includes(index));
                peer.send('draft.character.pick', { slotId: slot, actionId: `character_${i}_${pick}`, stateRevision: snap.revision });
                await peer.waitForSnapshot(snapshot => snapshot.privateState.draftCharacterSlots.length >= pick + 1);
            }
            const option = peer.snapshot.privateState.draftCharacterOptions[0];
            peer.send('draft.character.confirm', { characterId: option, actionId: `confirm_${i}`, stateRevision: peer.snapshot.revision });
        }

        const playable = await peers[0].waitFor(message => message.type === 'room.snapshot' && ['TURN_START', 'JUDGEMENT', 'DRAW', 'PLAY'].includes(message.data.state), 9000);
        assert.equal(playable.data.players.length, 8);
        assert.ok(playable.data.privateState.hand.length > 0);
        playable.data.players.forEach((player: any) => {
            if (!player.isRoleRevealed) assert.equal(player.publicRoleId, undefined);
        });

        const reconnectIndex = 7;
        const previousHand = peers[reconnectIndex].snapshot.privateState.hand.slice();
        peers[reconnectIndex].socket.close();
        await new Promise(resolve => setTimeout(resolve, 100));
        const replacementSocket = new WebSocket(`ws://127.0.0.1:${address.port}`);
        await new Promise<void>((resolve, reject) => { replacementSocket.once('open', resolve); replacementSocket.once('error', reject); });
        const replacement = new Peer(replacementSocket);
        peers[reconnectIndex] = replacement;
        replacement.send('session.resume', { deviceId: `e2e_player_${reconnectIndex}`, accessToken: accessTokens[reconnectIndex], clientVersion: 'test' });
        const resumed = await replacement.waitFor(message => message.type === 'session.ready');
        assert.equal(resumed.data.resumed, true);
        const restoredSnapshot = await replacement.waitFor(message => message.type === 'room.snapshot');
        assert.deepEqual(restoredSnapshot.data.privateState.hand, previousHand);

        const attackerSocket = new WebSocket(`ws://127.0.0.1:${address.port}`);
        await new Promise<void>((resolve, reject) => { attackerSocket.once('open', resolve); attackerSocket.once('error', reject); });
        const attacker = new Peer(attackerSocket);
        attacker.send('session.resume', { deviceId: `e2e_player_${reconnectIndex}`, accessToken: 'invalid-token', clientVersion: 'test' });
        const rejected = await attacker.waitFor(message => message.type === 'session.reject');
        assert.equal(rejected.data.code, 'INVALID_SESSION');
        attackerSocket.close();

        replacement.send('chat.send', { message: 'hello table' });
        const chat = await peers[0].waitFor(message => message.type === 'chat.message' && message.data.message === 'hello table');
        assert.equal(chat.data.playerId, `e2e_player_${reconnectIndex}`);
    } finally {
        peers.forEach(peer => peer.socket.close());
        await new Promise(resolve => setTimeout(resolve, 100));
        engine.dispose();
        await new Promise<void>(resolve => wss.close(() => resolve()));
        await new Promise<void>(resolve => server.close(() => resolve()));
        fs.rmSync(stateFile, { force: true });
        fs.rmSync(`${stateFile}.tmp`, { force: true });
    }
});

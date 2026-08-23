"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_test_1 = __importDefault(require("node:test"));
const strict_1 = __importDefault(require("node:assert/strict"));
const node_http_1 = __importDefault(require("node:http"));
const ws_1 = __importStar(require("ws"));
const ServerEngine_1 = require("./ServerEngine");
const node_fs_1 = __importDefault(require("node:fs"));
const node_os_1 = __importDefault(require("node:os"));
const node_path_1 = __importDefault(require("node:path"));
class Peer {
    socket;
    snapshot;
    messages = [];
    waiters = [];
    constructor(socket) {
        this.socket = socket;
        socket.on('message', raw => {
            const envelope = JSON.parse(raw.toString());
            const message = { ...envelope, data: typeof envelope.data === 'string' ? JSON.parse(envelope.data) : envelope.data };
            if (message.type === 'room.snapshot')
                this.snapshot = message.data;
            this.messages.push(message);
            this.waiters.splice(0).forEach(resolve => resolve());
        });
    }
    send(type, data = {}, reqId) {
        this.socket.send(JSON.stringify({ type, data, reqId }));
    }
    async waitFor(predicate, timeoutMs = 5000) {
        const deadline = Date.now() + timeoutMs;
        while (Date.now() < deadline) {
            const index = this.messages.findIndex(predicate);
            if (index >= 0)
                return this.messages.splice(index, 1)[0];
            await new Promise(resolve => {
                const timeout = setTimeout(resolve, Math.min(200, Math.max(1, deadline - Date.now())));
                this.waiters.push(() => { clearTimeout(timeout); resolve(); });
            });
        }
        throw new Error('Timed out waiting for server message');
    }
    waitForState(state, timeoutMs = 8000) {
        if (this.snapshot?.state === state)
            return Promise.resolve({ type: 'room.snapshot', data: this.snapshot });
        return this.waitFor(message => message.type === 'room.snapshot' && message.data.state === state, timeoutMs);
    }
    async waitForSnapshot(predicate, timeoutMs = 5000) {
        if (this.snapshot && predicate(this.snapshot))
            return this.snapshot;
        return (await this.waitFor(message => message.type === 'room.snapshot' && predicate(message.data), timeoutMs)).data;
    }
}
(0, node_test_1.default)('real WebSocket flow supports a full 8-player table without leaking hidden roles', { timeout: 25000 }, async () => {
    const stateFile = node_path_1.default.join(node_os_1.default.tmpdir(), `bang-e2e-${process.pid}-${Date.now()}.json`);
    process.env.BANG_STATE_FILE = stateFile;
    const server = node_http_1.default.createServer();
    const wss = new ws_1.WebSocketServer({ server });
    const engine = new ServerEngine_1.ServerEngine(wss);
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const address = server.address();
    strict_1.default.ok(address && typeof address !== 'string');
    const peers = [];
    const accessTokens = [];
    try {
        for (let i = 0; i < 8; i++) {
            const socket = new ws_1.default(`ws://127.0.0.1:${address.port}`);
            await new Promise((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
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
        strict_1.default.equal(playable.data.players.length, 8);
        strict_1.default.ok(playable.data.privateState.hand.length > 0);
        playable.data.players.forEach((player) => {
            if (!player.isRoleRevealed)
                strict_1.default.equal(player.publicRoleId, undefined);
        });
        const reconnectIndex = 7;
        const previousHand = peers[reconnectIndex].snapshot.privateState.hand.slice();
        peers[reconnectIndex].socket.close();
        await new Promise(resolve => setTimeout(resolve, 100));
        const replacementSocket = new ws_1.default(`ws://127.0.0.1:${address.port}`);
        await new Promise((resolve, reject) => { replacementSocket.once('open', resolve); replacementSocket.once('error', reject); });
        const replacement = new Peer(replacementSocket);
        peers[reconnectIndex] = replacement;
        replacement.send('session.resume', { deviceId: `e2e_player_${reconnectIndex}`, accessToken: accessTokens[reconnectIndex], clientVersion: 'test' });
        const resumed = await replacement.waitFor(message => message.type === 'session.ready');
        strict_1.default.equal(resumed.data.resumed, true);
        const restoredSnapshot = await replacement.waitFor(message => message.type === 'room.snapshot');
        strict_1.default.deepEqual(restoredSnapshot.data.privateState.hand, previousHand);
        const attackerSocket = new ws_1.default(`ws://127.0.0.1:${address.port}`);
        await new Promise((resolve, reject) => { attackerSocket.once('open', resolve); attackerSocket.once('error', reject); });
        const attacker = new Peer(attackerSocket);
        attacker.send('session.resume', { deviceId: `e2e_player_${reconnectIndex}`, accessToken: 'invalid-token', clientVersion: 'test' });
        const rejected = await attacker.waitFor(message => message.type === 'session.reject');
        strict_1.default.equal(rejected.data.code, 'INVALID_SESSION');
        attackerSocket.close();
        replacement.send('chat.send', { message: 'hello table' });
        const chat = await peers[0].waitFor(message => message.type === 'chat.message' && message.data.message === 'hello table');
        strict_1.default.equal(chat.data.playerId, `e2e_player_${reconnectIndex}`);
    }
    finally {
        peers.forEach(peer => peer.socket.close());
        await new Promise(resolve => setTimeout(resolve, 100));
        engine.dispose();
        await new Promise(resolve => wss.close(() => resolve()));
        await new Promise(resolve => server.close(() => resolve()));
        node_fs_1.default.rmSync(stateFile, { force: true });
        node_fs_1.default.rmSync(`${stateFile}.tmp`, { force: true });
    }
});

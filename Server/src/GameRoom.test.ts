import test from 'node:test';
import assert from 'node:assert/strict';
import { WebSocket } from 'ws';
import { GameRoom, ServerPlayerState } from './GameRoom';
import { ServerGameState } from './models/GameState';

class FakeSocket {
    public readyState = WebSocket.OPEN;
    public sent: any[] = [];
    public id: string;

    constructor(id: string) { this.id = id; }
    send(payload: string) { this.sent.push(JSON.parse(payload)); }
}

function createRoom(playerCount = 4) {
    const room = new GameRoom('TEST01', {} as any, {
        maxPlayers: playerCount,
        startingHandMode: 'BY_HP',
        roleDraftSec: 20,
        characterDraftSec: 30,
        responseTimeSec: 10
    });
    const sockets: FakeSocket[] = [];
    for (let i = 0; i < playerCount; i++) {
        const socket = new FakeSocket(`p${i}`);
        sockets.push(socket);
        assert.equal(room.joinPlayer(socket as any, `Player ${i}`, i === 0), true);
    }
    return { room, sockets, players: room.getPlayers() };
}

test('snapshot projection never leaks another player role or hand', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[0].isRoleRevealed = true;
    players[0].hand = ['bang_1'];
    players[1].roleId = 'outlaw';
    players[1].hand = ['missed_1', 'beer_1'];

    const sheriffView = room.getSnapshotFor(players[0].id);
    assert.equal(sheriffView.privateState?.roleId, 'sheriff');
    assert.deepEqual(sheriffView.privateState?.hand, ['bang_1']);
    assert.equal(sheriffView.players.find(p => p.id === players[1].id)?.publicRoleId, undefined);
    assert.equal(sheriffView.players.find(p => p.id === players[1].id)?.handCount, 2);
});

test('all recipients of one broadcast receive the same revision and sequence', () => {
    const { room, sockets } = createRoom();
    (room as any).broadcastSnapshot();
    const snapshots = sockets.map(socket => {
        const envelope = socket.sent.filter(x => x.type === 'room.snapshot').at(-1);
        return JSON.parse(envelope.data);
    });
    assert.equal(new Set(snapshots.map(s => s.revision)).size, 1);
    assert.equal(new Set(snapshots.map(s => s.sequence)).size, 1);
});

test('stale room mutation can resync and retry without losing its action id', () => {
    const room = new GameRoom('TEST02', {} as any, { maxPlayers: 8 });
    const host = new FakeSocket('host');
    assert.equal(room.joinPlayer(host as any, 'Host', true), true);

    const staleRevision = room.getSnapshotFor('host').revision - 1;
    room.handleMessage(host as any, 'room.addBot', {
        actionId: 'add_bot_retry',
        stateRevision: staleRevision
    });
    assert.equal(room.getPlayers().length, 1);
    assert.equal(host.sent.at(-2)?.type, 'game.action.rejected');
    assert.equal(JSON.parse(host.sent.at(-2).data).reason, 'STALE_STATE');

    room.handleMessage(host as any, 'room.addBot', {
        actionId: 'add_bot_retry',
        stateRevision: room.getSnapshotFor('host').revision
    });
    assert.equal(room.getPlayers().length, 2);
    assert.equal(room.getPlayers().filter(player => player.isBot).length, 1);
});

test('dead seats are removed from distance calculation', () => {
    const { room, players } = createRoom();
    for (const p of players) p.isAlive = true;
    players[1].isAlive = false;
    assert.equal((room as any).getSeatDistance(players[0], players[2]), 1);
});

test('sheriff team wins immediately after final outlaw and renegade are eliminated', () => {
    const { room, players } = createRoom();
    const roles = ['sheriff', 'deputy', 'outlaw', 'renegade'];
    players.forEach((p: ServerPlayerState, i: number) => {
        p.roleId = roles[i];
        p.currentHealth = 1;
    });
    (room as any).state = ServerGameState.PLAY;
    (room as any).applyDamage(players[2], 1, players[0].id);
    assert.equal(room.getState(), ServerGameState.PLAY);
    (room as any).applyDamage(players[3], 1, players[0].id);
    assert.equal(room.getState(), ServerGameState.GAME_OVER);
    assert.equal(room.getSnapshotFor(players[0].id).winnerTeam, 'SHERIFF_DEPUTIES');
});

for (const count of [4, 5, 6, 7, 8]) {
    test(`authoritative pre-game flow supports ${count} players`, () => {
        const { room, players } = createRoom(count);
        players.forEach(p => p.isReady = true);
        (room as any).startGame();
        clearTimeout((room as any).timerHandle);
        assert.equal(room.getState(), ServerGameState.ROLE_DRAFT);
        assert.equal((room as any).deadlineAt > Date.now() + 15_000, true);

        (room as any).handleRoleDraftTimeout();
        clearTimeout((room as any).timerHandle);
        assert.equal(players.filter(p => p.roleId === 'sheriff').length, 1);
        assert.equal(new Set(players.map(p => p.draftRoleSlot)).size, count);

        (room as any).startCharacterDraft();
        clearTimeout((room as any).timerHandle);
        (room as any).handleCharacterDraftTimeout();
        clearTimeout((room as any).timerHandle);
        assert.equal(players.every(p => Boolean(p.characterId)), true);
        assert.equal(new Set(players.flatMap(p => [p.draftCharacterSlot1, p.draftCharacterSlot2])).size, count * 2);

        (room as any).startInitialDeal();
        clearTimeout((room as any).timerHandle);
        assert.equal(players.every(p => p.hand.length === p.maxHealth), true);
        assert.equal(room.getSnapshotFor(players[0].id).drawPileCount, 80 - players.reduce((sum, p) => sum + p.maxHealth, 0));
    });
}

test('lethal damage opens a private Beer save instead of auto-consuming cards', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[1].roleId = 'outlaw';
    players[1].currentHealth = 1;
    players[1].hand = ['beer__1__hearts__6'];
    (room as any).state = ServerGameState.PLAY;
    (room as any).applyDamage(players[1], 1, players[0].id);
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getState(), ServerGameState.RESPONSE);
    assert.equal(room.getSnapshotFor(players[0].id).activeInteraction, undefined);
    assert.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'beer');
    (room as any).handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout((room as any).timerHandle);
    assert.equal(players[1].isAlive, true);
    assert.equal(players[1].currentHealth, 1);
});

test('Dynamite spades 2-9 deals three damage and is discarded', () => {
    const { room, players } = createRoom();
    const player = players[0];
    player.roleId = 'sheriff';
    player.currentHealth = 5;
    player.maxHealth = 5;
    player.equipment = ['dynamite__1__clubs__A'];
    (room as any).currentTurnPlayerId = player.id;
    (room as any).deck = ['bang__9__spades__5'];
    (room as any).startJudgementPhase();
    clearTimeout((room as any).timerHandle);
    assert.equal(player.currentHealth, 2);
    assert.equal(player.equipment.some((c: string) => c.startsWith('dynamite')), false);
});

test('Duel alternates BANG responses and damages the first player who passes', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => { p.roleId = ['sheriff','outlaw','deputy','renegade'][i]; p.currentHealth = 4; });
    players[0].hand = ['duello__1__clubs__Q'];
    players[1].hand = ['bang__2__hearts__7'];
    (room as any).state = ServerGameState.PLAY;
    (room as any).currentPhase = 'PLAY';
    (room as any).currentTurnPlayerId = players[0].id;
    (room as any).handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [players[1].id] });
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'bang');
    (room as any).handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getSnapshotFor(players[0].id).activeInteraction?.requiredCardType, 'bang');
    (room as any).handleRespond(players[0].id, { action: 'PASS' });
    clearTimeout((room as any).timerHandle);
    assert.equal(players[0].currentHealth, 3);
});

test('Indians resolves targets in deterministic seat order', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => { p.roleId = ['sheriff','outlaw','deputy','renegade'][i]; p.currentHealth = 4; });
    players[0].hand = ['indiani__1__diamonds__K'];
    players[1].hand = ['bang__2__clubs__4'];
    (room as any).state = ServerGameState.PLAY;
    (room as any).currentPhase = 'PLAY';
    (room as any).currentTurnPlayerId = players[0].id;
    (room as any).handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [] });
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'bang');
    (room as any).handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getSnapshotFor(players[2].id).activeInteraction?.requiredCardType, 'bang');
});

test('General Store exposes public cards but only current picker can take one', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => p.roleId = ['sheriff','outlaw','deputy','renegade'][i]);
    players[0].hand = ['general_store__1__diamonds__9'];
    (room as any).deck = (room as any).createDeck();
    (room as any).state = ServerGameState.PLAY;
    (room as any).currentPhase = 'PLAY';
    (room as any).currentTurnPlayerId = players[0].id;
    (room as any).handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [] });
    clearTimeout((room as any).timerHandle);
    const offered = (room as any).generalStoreCards.slice();
    (room as any).handleGeneralStorePick(players[1].id, offered[0]);
    assert.equal(players[1].hand.length, 0);
    (room as any).handleGeneralStorePick(players[0].id, offered[0]);
    clearTimeout((room as any).timerHandle);
    assert.equal(players[0].hand.includes(offered[0]), true);
});

test('room state restores private hands and hidden roles without exposing them', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[1].roleId = 'outlaw';
    players[1].hand = ['bang__restore__clubs__7'];
    (room as any).state = ServerGameState.PLAY;
    const restored = GameRoom.restore(room.exportState(), {} as any);
    const ownerView = restored.getSnapshotFor(players[1].id);
    const otherView = restored.getSnapshotFor(players[0].id);
    clearTimeout((restored as any).timerHandle);
    assert.deepEqual(ownerView.privateState?.hand, ['bang__restore__clubs__7']);
    assert.equal(otherView.players.find(player => player.id === players[1].id)?.publicRoleId, undefined);
});

test('all connected humans voting rematch starts a fresh role draft', () => {
    const { room, players } = createRoom();
    (room as any).state = ServerGameState.GAME_OVER;
    for (const player of players) (room as any).handleRematchVote(player.id);
    clearTimeout((room as any).timerHandle);
    assert.equal(room.getState(), ServerGameState.ROLE_DRAFT);
    assert.equal(players.every(player => player.hand.length === 0 && player.isAlive), true);
});

test('Kit Carlson receives a private choose-two draw interaction', () => {
    const { room, players } = createRoom();
    const kit = players[0];
    kit.characterId = 'kit_carlson';
    (room as any).deck = (room as any).createDeck();
    (room as any).currentTurnPlayerId = kit.id;
    (room as any).startDrawPhase();
    clearTimeout((room as any).timerHandle);
    const prompt = room.getSnapshotFor(kit.id).activeInteraction;
    assert.equal(prompt?.type, 'SELECT_CARDS');
    assert.equal(prompt?.validCardIds.length, 3);
    (room as any).handleRespond(kit.id, { action: 'SUBMIT', selectedCardIds: prompt?.validCardIds.slice(0, 2) });
    clearTimeout((room as any).timerHandle);
    assert.equal(kit.hand.length, 2);
});

test('Lucky Duke chooses one of two judgement cards privately', () => {
    const { room, players } = createRoom();
    const lucky = players[0];
    lucky.characterId = 'lucky_duke';
    lucky.equipment = ['jail__lucky__clubs__J'];
    (room as any).currentTurnPlayerId = lucky.id;
    (room as any).deck = ['bang__safe__hearts__A', 'bang__fail__spades__7'];
    (room as any).startJudgementPhase();
    clearTimeout((room as any).timerHandle);
    const prompt = room.getSnapshotFor(lucky.id).activeInteraction;
    assert.equal(prompt?.validCardIds.length, 2);
    const heart = prompt?.validCardIds.find(card => card.includes('__hearts__'));
    (room as any).handleRespond(lucky.id, { action: 'SUBMIT', selectedCardIds: [heart] });
    clearTimeout((room as any).timerHandle);
    assert.equal((room as any).judgementResult, 'THOÁT TÙ');
});

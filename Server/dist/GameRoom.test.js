"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_test_1 = __importDefault(require("node:test"));
const strict_1 = __importDefault(require("node:assert/strict"));
const ws_1 = require("ws");
const GameRoom_1 = require("./GameRoom");
const GameState_1 = require("./models/GameState");
class FakeSocket {
    readyState = ws_1.WebSocket.OPEN;
    sent = [];
    id;
    constructor(id) { this.id = id; }
    send(payload) { this.sent.push(JSON.parse(payload)); }
}
function createRoom(playerCount = 4) {
    const room = new GameRoom_1.GameRoom('TEST01', {}, {
        maxPlayers: playerCount,
        startingHandMode: 'BY_HP',
        roleDraftSec: 20,
        characterDraftSec: 30,
        responseTimeSec: 10
    });
    const sockets = [];
    for (let i = 0; i < playerCount; i++) {
        const socket = new FakeSocket(`p${i}`);
        sockets.push(socket);
        strict_1.default.equal(room.joinPlayer(socket, `Player ${i}`, i === 0), true);
    }
    return { room, sockets, players: room.getPlayers() };
}
(0, node_test_1.default)('snapshot projection never leaks another player role or hand', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[0].isRoleRevealed = true;
    players[0].hand = ['bang_1'];
    players[1].roleId = 'outlaw';
    players[1].hand = ['missed_1', 'beer_1'];
    const sheriffView = room.getSnapshotFor(players[0].id);
    strict_1.default.equal(sheriffView.privateState?.roleId, 'sheriff');
    strict_1.default.deepEqual(sheriffView.privateState?.hand, ['bang_1']);
    strict_1.default.equal(sheriffView.players.find(p => p.id === players[1].id)?.publicRoleId, undefined);
    strict_1.default.equal(sheriffView.players.find(p => p.id === players[1].id)?.handCount, 2);
});
(0, node_test_1.default)('all recipients of one broadcast receive the same revision and sequence', () => {
    const { room, sockets } = createRoom();
    room.broadcastSnapshot();
    const snapshots = sockets.map(socket => {
        const envelope = socket.sent.filter(x => x.type === 'room.snapshot').at(-1);
        return JSON.parse(envelope.data);
    });
    strict_1.default.equal(new Set(snapshots.map(s => s.revision)).size, 1);
    strict_1.default.equal(new Set(snapshots.map(s => s.sequence)).size, 1);
});
(0, node_test_1.default)('dead seats are removed from distance calculation', () => {
    const { room, players } = createRoom();
    for (const p of players)
        p.isAlive = true;
    players[1].isAlive = false;
    strict_1.default.equal(room.getSeatDistance(players[0], players[2]), 1);
});
(0, node_test_1.default)('sheriff team wins immediately after final outlaw and renegade are eliminated', () => {
    const { room, players } = createRoom();
    const roles = ['sheriff', 'deputy', 'outlaw', 'renegade'];
    players.forEach((p, i) => {
        p.roleId = roles[i];
        p.currentHealth = 1;
    });
    room.state = GameState_1.ServerGameState.PLAY;
    room.applyDamage(players[2], 1, players[0].id);
    strict_1.default.equal(room.getState(), GameState_1.ServerGameState.PLAY);
    room.applyDamage(players[3], 1, players[0].id);
    strict_1.default.equal(room.getState(), GameState_1.ServerGameState.GAME_OVER);
    strict_1.default.equal(room.getSnapshotFor(players[0].id).winnerTeam, 'SHERIFF_DEPUTIES');
});
for (const count of [4, 5, 6, 7, 8]) {
    (0, node_test_1.default)(`authoritative pre-game flow supports ${count} players`, () => {
        const { room, players } = createRoom(count);
        players.forEach(p => p.isReady = true);
        room.startGame();
        clearTimeout(room.timerHandle);
        strict_1.default.equal(room.getState(), GameState_1.ServerGameState.ROLE_DRAFT);
        strict_1.default.equal(room.deadlineAt > Date.now() + 15_000, true);
        room.handleRoleDraftTimeout();
        clearTimeout(room.timerHandle);
        strict_1.default.equal(players.filter(p => p.roleId === 'sheriff').length, 1);
        strict_1.default.equal(new Set(players.map(p => p.draftRoleSlot)).size, count);
        room.startCharacterDraft();
        clearTimeout(room.timerHandle);
        room.handleCharacterDraftTimeout();
        clearTimeout(room.timerHandle);
        strict_1.default.equal(players.every(p => Boolean(p.characterId)), true);
        strict_1.default.equal(new Set(players.flatMap(p => [p.draftCharacterSlot1, p.draftCharacterSlot2])).size, count * 2);
        room.startInitialDeal();
        clearTimeout(room.timerHandle);
        strict_1.default.equal(players.every(p => p.hand.length === p.maxHealth), true);
        strict_1.default.equal(room.getSnapshotFor(players[0].id).drawPileCount, 80 - players.reduce((sum, p) => sum + p.maxHealth, 0));
    });
}
(0, node_test_1.default)('lethal damage opens a private Beer save instead of auto-consuming cards', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[1].roleId = 'outlaw';
    players[1].currentHealth = 1;
    players[1].hand = ['beer__1__hearts__6'];
    room.state = GameState_1.ServerGameState.PLAY;
    room.applyDamage(players[1], 1, players[0].id);
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getState(), GameState_1.ServerGameState.RESPONSE);
    strict_1.default.equal(room.getSnapshotFor(players[0].id).activeInteraction, undefined);
    strict_1.default.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'beer');
    room.handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(players[1].isAlive, true);
    strict_1.default.equal(players[1].currentHealth, 1);
});
(0, node_test_1.default)('Dynamite spades 2-9 deals three damage and is discarded', () => {
    const { room, players } = createRoom();
    const player = players[0];
    player.roleId = 'sheriff';
    player.currentHealth = 5;
    player.maxHealth = 5;
    player.equipment = ['dynamite__1__clubs__A'];
    room.currentTurnPlayerId = player.id;
    room.deck = ['bang__9__spades__5'];
    room.startJudgementPhase();
    clearTimeout(room.timerHandle);
    strict_1.default.equal(player.currentHealth, 2);
    strict_1.default.equal(player.equipment.some((c) => c.startsWith('dynamite')), false);
});
(0, node_test_1.default)('Duel alternates BANG responses and damages the first player who passes', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => { p.roleId = ['sheriff', 'outlaw', 'deputy', 'renegade'][i]; p.currentHealth = 4; });
    players[0].hand = ['duello__1__clubs__Q'];
    players[1].hand = ['bang__2__hearts__7'];
    room.state = GameState_1.ServerGameState.PLAY;
    room.currentPhase = 'PLAY';
    room.currentTurnPlayerId = players[0].id;
    room.handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [players[1].id] });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'bang');
    room.handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getSnapshotFor(players[0].id).activeInteraction?.requiredCardType, 'bang');
    room.handleRespond(players[0].id, { action: 'PASS' });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(players[0].currentHealth, 3);
});
(0, node_test_1.default)('Indians resolves targets in deterministic seat order', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => { p.roleId = ['sheriff', 'outlaw', 'deputy', 'renegade'][i]; p.currentHealth = 4; });
    players[0].hand = ['indiani__1__diamonds__K'];
    players[1].hand = ['bang__2__clubs__4'];
    room.state = GameState_1.ServerGameState.PLAY;
    room.currentPhase = 'PLAY';
    room.currentTurnPlayerId = players[0].id;
    room.handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [] });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getSnapshotFor(players[1].id).activeInteraction?.requiredCardType, 'bang');
    room.handleRespond(players[1].id, { action: 'USE_CARDS', selectedCardIds: players[1].hand.slice() });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getSnapshotFor(players[2].id).activeInteraction?.requiredCardType, 'bang');
});
(0, node_test_1.default)('General Store exposes public cards but only current picker can take one', () => {
    const { room, players } = createRoom();
    players.forEach((p, i) => p.roleId = ['sheriff', 'outlaw', 'deputy', 'renegade'][i]);
    players[0].hand = ['general_store__1__diamonds__9'];
    room.deck = room.createDeck();
    room.state = GameState_1.ServerGameState.PLAY;
    room.currentPhase = 'PLAY';
    room.currentTurnPlayerId = players[0].id;
    room.handlePlayCard(players[0].id, { cardId: players[0].hand[0], targetPlayerIds: [] });
    clearTimeout(room.timerHandle);
    const offered = room.generalStoreCards.slice();
    room.handleGeneralStorePick(players[1].id, offered[0]);
    strict_1.default.equal(players[1].hand.length, 0);
    room.handleGeneralStorePick(players[0].id, offered[0]);
    clearTimeout(room.timerHandle);
    strict_1.default.equal(players[0].hand.includes(offered[0]), true);
});
(0, node_test_1.default)('room state restores private hands and hidden roles without exposing them', () => {
    const { room, players } = createRoom();
    players[0].roleId = 'sheriff';
    players[1].roleId = 'outlaw';
    players[1].hand = ['bang__restore__clubs__7'];
    room.state = GameState_1.ServerGameState.PLAY;
    const restored = GameRoom_1.GameRoom.restore(room.exportState(), {});
    const ownerView = restored.getSnapshotFor(players[1].id);
    const otherView = restored.getSnapshotFor(players[0].id);
    clearTimeout(restored.timerHandle);
    strict_1.default.deepEqual(ownerView.privateState?.hand, ['bang__restore__clubs__7']);
    strict_1.default.equal(otherView.players.find(player => player.id === players[1].id)?.publicRoleId, undefined);
});
(0, node_test_1.default)('all connected humans voting rematch starts a fresh role draft', () => {
    const { room, players } = createRoom();
    room.state = GameState_1.ServerGameState.GAME_OVER;
    for (const player of players)
        room.handleRematchVote(player.id);
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.getState(), GameState_1.ServerGameState.ROLE_DRAFT);
    strict_1.default.equal(players.every(player => player.hand.length === 0 && player.isAlive), true);
});
(0, node_test_1.default)('Kit Carlson receives a private choose-two draw interaction', () => {
    const { room, players } = createRoom();
    const kit = players[0];
    kit.characterId = 'kit_carlson';
    room.deck = room.createDeck();
    room.currentTurnPlayerId = kit.id;
    room.startDrawPhase();
    clearTimeout(room.timerHandle);
    const prompt = room.getSnapshotFor(kit.id).activeInteraction;
    strict_1.default.equal(prompt?.type, 'SELECT_CARDS');
    strict_1.default.equal(prompt?.validCardIds.length, 3);
    room.handleRespond(kit.id, { action: 'SUBMIT', selectedCardIds: prompt?.validCardIds.slice(0, 2) });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(kit.hand.length, 2);
});
(0, node_test_1.default)('Lucky Duke chooses one of two judgement cards privately', () => {
    const { room, players } = createRoom();
    const lucky = players[0];
    lucky.characterId = 'lucky_duke';
    lucky.equipment = ['jail__lucky__clubs__J'];
    room.currentTurnPlayerId = lucky.id;
    room.deck = ['bang__safe__hearts__A', 'bang__fail__spades__7'];
    room.startJudgementPhase();
    clearTimeout(room.timerHandle);
    const prompt = room.getSnapshotFor(lucky.id).activeInteraction;
    strict_1.default.equal(prompt?.validCardIds.length, 2);
    const heart = prompt?.validCardIds.find(card => card.includes('__hearts__'));
    room.handleRespond(lucky.id, { action: 'SUBMIT', selectedCardIds: [heart] });
    clearTimeout(room.timerHandle);
    strict_1.default.equal(room.judgementResult, 'THOÁT TÙ');
});

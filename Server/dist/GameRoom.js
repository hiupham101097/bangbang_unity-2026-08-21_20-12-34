"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.GameRoom = void 0;
const ws_1 = require("ws");
const GameState_1 = require("./models/GameState");
class GameRoom {
    roomId;
    maxPlayers;
    wss;
    state = GameState_1.ServerGameState.WAITING;
    players = new Map();
    sockets = new Map();
    hostId = '';
    turnNumber = 0;
    sequence = 0;
    constructor(roomId, wss, config) {
        this.roomId = roomId;
        this.wss = wss;
        this.maxPlayers = config.maxPlayers || 5;
    }
    joinPlayer(ws, name, isHost = false) {
        if (this.players.size >= this.maxPlayers)
            return false;
        if (this.state !== GameState_1.ServerGameState.WAITING)
            return false;
        const socketId = ws.id;
        const player = {
            id: socketId,
            name: name,
            seat: this.players.size,
            isHost: isHost,
            isReady: isHost,
            isConnected: true,
            isAlive: true,
            currentHealth: 4,
            maxHealth: 4,
            isRoleRevealed: false,
            handCount: 0,
            hand: [],
            equipment: [],
            effectiveDistanceToLocal: 1,
            isTargetable: false
        };
        if (isHost)
            this.hostId = socketId;
        this.players.set(socketId, player);
        this.sockets.set(socketId, ws);
        this.broadcastSnapshot();
        return true;
    }
    handleMessage(ws, type, data) {
        const socketId = ws.id;
        if (type === 'room.ready') {
            const p = this.players.get(socketId);
            if (p && this.state === GameState_1.ServerGameState.WAITING) {
                p.isReady = data.isReady;
                this.broadcastSnapshot();
            }
        }
        else if (type === 'game.start') {
            if (socketId === this.hostId && this.state === GameState_1.ServerGameState.WAITING) {
                const allReady = Array.from(this.players.values()).every(p => p.isReady);
                if (allReady && this.players.size >= 4) {
                    this.startGame();
                }
                else {
                    ws.send(JSON.stringify({ type: 'game.error', data: JSON.stringify('Not all players ready or not enough players (min 4).') }));
                }
            }
        }
        else if (type === 'draft.character.pick') {
            const p = this.players.get(socketId);
            if (p && this.state === GameState_1.ServerGameState.SELECTING_CHARACTER) {
                p.characterId = data.characterId;
                // If all players have picked their characters, we can start INITIALIZING
                const allPicked = Array.from(this.players.values()).every(player => player.characterId != null);
                if (allPicked) {
                    this.state = GameState_1.ServerGameState.INITIALIZING;
                    console.log(`[ROOM ${this.roomId}] All players selected characters. Transitioning to INITIALIZING`);
                    // Actually initialize health and cards
                    this.initializeGame();
                }
                this.broadcastSnapshot();
            }
        }
    }
    handleDisconnect(socketId) {
        const p = this.players.get(socketId);
        if (p) {
            p.isConnected = false;
            this.sockets.delete(socketId);
            if (this.state === GameState_1.ServerGameState.WAITING) {
                this.players.delete(socketId);
                // Re-assign host if host left
                if (this.hostId === socketId && this.players.size > 0) {
                    const nextHost = Array.from(this.players.values())[0];
                    nextHost.isHost = true;
                    nextHost.isReady = true;
                    this.hostId = nextHost.id;
                }
            }
            this.broadcastSnapshot();
        }
    }
    hasPlayer(socketId) {
        return this.players.has(socketId);
    }
    isEmpty() {
        return this.players.size === 0 || Array.from(this.players.values()).every(p => !p.isConnected);
    }
    getPlayers() {
        return Array.from(this.players.values());
    }
    getState() {
        return this.state;
    }
    getSnapshot() {
        return {
            roomId: this.roomId,
            roomCode: this.roomId,
            hostPlayerId: this.hostId,
            state: this.state,
            turnNumber: this.turnNumber,
            players: Array.from(this.players.values()).map(p => ({ ...p, hand: [] })),
            drawPileCount: 80,
            discardPileCount: 0,
            combatLogs: [],
            serverTime: Date.now(),
            sequence: this.sequence++
        };
    }
    broadcastSnapshot() {
        const snap = this.getSnapshot();
        const payload = JSON.stringify({ type: 'room.snapshot', data: JSON.stringify(snap) });
        for (const [id, socket] of this.sockets.entries()) {
            if (socket.readyState === ws_1.WebSocket.OPEN) {
                socket.send(payload);
            }
        }
    }
    // --- GAME LOGIC ---
    startGame() {
        this.state = GameState_1.ServerGameState.DEALING_ROLES;
        console.log(`[ROOM ${this.roomId}] Starting game, transitioning to DEALING_ROLES`);
        this.distributeRoles();
        this.broadcastSnapshot();
        // Start timer for role reveal (20s) -> CHARACTER_DRAFT
        setTimeout(() => {
            this.startCharacterDraft();
        }, 20000);
    }
    distributeRoles() {
        const numPlayers = this.players.size;
        let roles = [];
        // standard BANG! distributions
        if (numPlayers === 4)
            roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW'];
        else if (numPlayers === 5)
            roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'DEPUTY'];
        else if (numPlayers === 6)
            roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY'];
        else if (numPlayers === 7)
            roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY', 'DEPUTY'];
        else if (numPlayers >= 8)
            roles = ['SHERIFF', 'RENEGADE', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY', 'DEPUTY']; // up to 8
        else {
            // fallback (e.g. testing with < 4 players)
            roles = ['SHERIFF', 'OUTLAW', 'RENEGADE', 'DEPUTY'].slice(0, numPlayers);
        }
        // Shuffle roles
        for (let i = roles.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [roles[i], roles[j]] = [roles[j], roles[i]];
        }
        // Assign roles
        let i = 0;
        for (const p of this.players.values()) {
            p.role = roles[i];
            // Only sheriff is revealed to others
            p.isRoleRevealed = (p.role === 'SHERIFF');
            i++;
        }
    }
    startCharacterDraft() {
        this.state = GameState_1.ServerGameState.SELECTING_CHARACTER;
        console.log(`[ROOM ${this.roomId}] Transitioning to SELECTING_CHARACTER`);
        // In a real app we draw 2 random characters from a pool and store them 
        // in a transient state or send them via a specific interaction prompt.
        // For now, we will send an interaction prompt for character selection.
        const charPool = ['bart_cassidy', 'black_jack', 'calamity_janet', 'el_gringo', 'jesse_jones', 'jourdonnais', 'kit_carlson', 'lucky_duke', 'paul_regret', 'pedro_ramirez', 'rose_doolan', 'sid_ketchum', 'slab_the_killer', 'suzy_lafayette', 'vulture_sam', 'willy_the_kid'];
        // Shuffle char pool
        for (let i = charPool.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [charPool[i], charPool[j]] = [charPool[j], charPool[i]];
        }
        let cIdx = 0;
        for (const p of this.players.values()) {
            const options = [charPool[cIdx++], charPool[cIdx++]];
            // We could use activeInteraction here per player, but the architecture 
            // has only one activeInteraction per room.
            // Since this is simultaneous, we will just send it as a custom payload or 
            // let the client display a static list of characters for testing.
        }
        this.broadcastSnapshot();
    }
    initializeGame() {
        // Set health based on character (for now default 4, +1 if Sheriff)
        for (const p of this.players.values()) {
            p.maxHealth = p.role === 'SHERIFF' ? 5 : 4;
            p.currentHealth = p.maxHealth;
            p.handCount = p.maxHealth;
            // deal cards (mock)
            for (let i = 0; i < p.handCount; i++)
                p.hand.push(`card_${Math.random()}`);
        }
        this.state = GameState_1.ServerGameState.PLAYING;
        // Find Sheriff to start
        const sheriff = Array.from(this.players.values()).find(p => p.role === 'SHERIFF');
        if (sheriff)
            this.turnNumber = 1;
        this.broadcastSnapshot();
    }
}
exports.GameRoom = GameRoom;

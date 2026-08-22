import { WebSocketServer, WebSocket } from 'ws';
import { MatchStateSnapshotDTO, PlayerSnapshotDTO, ServerGameState } from './models/GameState';

export class GameRoom {
    public roomId: string;
    public maxPlayers: number;
    
    private wss: WebSocketServer;
    private state: ServerGameState = ServerGameState.WAITING;
    private players: Map<string, PlayerSnapshotDTO> = new Map();
    private sockets: Map<string, WebSocket> = new Map();
    
    private hostId: string = '';
    private turnNumber: number = 0;
    private sequence: number = 0;
    
    // Turn State
    private currentTurnPlayerId: string = "";
    private currentPhase: string = "";
    private activeInteraction: any = null; // InteractionPromptDTO
    private deck: string[] = [];
    private discardPile: string[] = [];

    constructor(roomId: string, wss: WebSocketServer, config: any) {
        this.roomId = roomId;
        this.wss = wss;
        this.maxPlayers = config.maxPlayers || 5;
    }

    public joinPlayer(ws: WebSocket, name: string, isHost: boolean = false): boolean {
        if (this.players.size >= this.maxPlayers) return false;
        if (this.state !== ServerGameState.WAITING) return false;

        const socketId = (ws as any).id;
        const player: PlayerSnapshotDTO = {
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

        if (isHost) this.hostId = socketId;

        this.players.set(socketId, player);
        this.sockets.set(socketId, ws);

        this.broadcastSnapshot();

        return true;
    }

    public handleMessage(ws: WebSocket, type: string, data: any) {
        const socketId = (ws as any).id;
        
        if (type === 'room.ready') {
            const p = this.players.get(socketId);
            if (p && this.state === ServerGameState.WAITING) {
                p.isReady = data.isReady;
                this.broadcastSnapshot();
            }
        }
        else if (type === 'game.start') {
            if (socketId === this.hostId && this.state === ServerGameState.WAITING) {
                const allReady = Array.from(this.players.values()).every(p => p.isReady);
                if (allReady && this.players.size >= 4) { 
                    this.startGame();
                } else {
                    ws.send(JSON.stringify({ type: 'game.error', data: JSON.stringify('Not all players ready or not enough players (min 4).') }));
                }
            }
        }
        else if (type === 'draft.character.pick') {
            const p = this.players.get(socketId);
            if (p && this.state === ServerGameState.SELECTING_CHARACTER) {
                p.characterId = data.characterId;
                
                // If all players have picked their characters, we can start INITIALIZING
                const allPicked = Array.from(this.players.values()).every(player => player.characterId != null);
                if (allPicked) {
                    this.state = ServerGameState.INITIALIZING;
                    console.log(`[ROOM ${this.roomId}] All players selected characters. Transitioning to INITIALIZING`);
                    
                    // Actually initialize health and cards
                    this.initializeGame();
                }
                this.broadcastSnapshot();
            }
        }
        else if (type === 'game.action.play') {
            const p = this.players.get(socketId);
            if (p && this.state === ServerGameState.PLAYING && this.currentTurnPlayerId === socketId && this.currentPhase === 'PLAY') {
                const { cardId, targetPlayerIds } = data;
                
                // Ensure player has the card
                const idx = p.hand.findIndex(c => c === cardId);
                if (idx >= 0) {
                    p.hand.splice(idx, 1);
                    p.handCount = p.hand.length;
                    this.discardPile.push(cardId);
                    
                    if (cardId.startsWith('bang')) {
                        // Requires Missed! from target
                        if (targetPlayerIds && targetPlayerIds.length > 0) {
                            const target = targetPlayerIds[0];
                            this.state = ServerGameState.WAITING_RESPONSE;
                            this.activeInteraction = {
                                interactionId: 'bang_' + Date.now(),
                                type: 'RESPOND',
                                actorPlayerId: target,
                                title: 'BANG!',
                                message: `${p.name} đã bắn bạn! Dùng Missed! hoặc mất 1 máu.`,
                                validCardIds: ['missed_.*'], 
                                canCancel: true,
                                defaultAction: 'take_damage'
                            };
                            console.log(`[ROOM] BANG! played on ${target}`);
                        }
                    }
                    this.broadcastSnapshot();
                }
            }
        }
        else if (type === 'game.action.respond') {
            if (this.state === ServerGameState.WAITING_RESPONSE && this.activeInteraction && this.activeInteraction.actorPlayerId === socketId) {
                const { action, selectedCardIds } = data;
                const p = this.players.get(socketId);
                
                if ((action === 'USE_CARDS' || action === 'SUBMIT') && selectedCardIds && selectedCardIds.length > 0) {
                    // E.g. used Missed!
                    const cardId = selectedCardIds[0];
                    const idx = p!.hand.findIndex(c => c === cardId);
                    if (idx >= 0) {
                        p!.hand.splice(idx, 1);
                        p!.handCount = p!.hand.length;
                        this.discardPile.push(cardId);
                        console.log(`[ROOM] ${p!.name} used Missed!`);
                    }
                } else if (action === 'CANCEL' || action === 'take_damage') {
                    p!.currentHealth -= 1;
                    console.log(`[ROOM] ${p!.name} took 1 damage!`);
                    if (p!.currentHealth <= 0) p!.isAlive = false;
                }
                
                this.activeInteraction = null;
                this.state = ServerGameState.PLAYING;
                this.broadcastSnapshot();
            }
        }
        else if (type === 'game.action.endTurn') {
            if (this.state === ServerGameState.PLAYING && this.currentTurnPlayerId === socketId && this.currentPhase === 'PLAY') {
                const p = this.players.get(socketId);
                if (p!.handCount > p!.currentHealth) {
                    // Need to discard (skipped for now, force discard would be an interaction)
                }
                
                // Next player
                this.nextTurn();
            }
        }
    }
    public handleDisconnect(socketId: string) {
        const p = this.players.get(socketId);
        if (p) {
            p.isConnected = false;
            this.sockets.delete(socketId);
            
            if (this.state === ServerGameState.WAITING) {
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

    public hasPlayer(socketId: string): boolean {
        return this.players.has(socketId);
    }

    public isEmpty(): boolean {
        return this.players.size === 0 || Array.from(this.players.values()).every(p => !p.isConnected);
    }

    public getPlayers(): PlayerSnapshotDTO[] {
        return Array.from(this.players.values());
    }

    public getState(): ServerGameState {
        return this.state;
    }

    public getSnapshot(): MatchStateSnapshotDTO {
        return {
            roomId: this.roomId,
            roomCode: this.roomId,
            hostPlayerId: this.hostId,
            state: this.state,
            turnNumber: this.turnNumber,
            currentTurnPlayerId: this.currentTurnPlayerId,
            currentPhase: this.currentPhase,
            activeInteraction: this.activeInteraction,
            players: Array.from(this.players.values()).map(p => ({ ...p, hand: [] })), 
            drawPileCount: this.deck.length,
            discardPileCount: this.discardPile.length,
            combatLogs: [],
            serverTime: Date.now(),
            sequence: this.sequence++
        };
    }

    private broadcastSnapshot() {
        const snap = this.getSnapshot();
        const payload = JSON.stringify({ type: 'room.snapshot', data: JSON.stringify(snap) });
        
        for (const [id, socket] of this.sockets.entries()) {
            if (socket.readyState === WebSocket.OPEN) {
                socket.send(payload);
            }
        }
    }

    // --- GAME LOGIC ---
    private startGame() {
        this.state = ServerGameState.DEALING_ROLES;
        console.log(`[ROOM ${this.roomId}] Starting game, transitioning to DEALING_ROLES`);
        
        this.distributeRoles();
        
        this.broadcastSnapshot();
        
        // Start timer for role reveal (20s) -> CHARACTER_DRAFT
        setTimeout(() => {
            this.startCharacterDraft();
        }, 20000);
    }
    
    private distributeRoles() {
        const numPlayers = this.players.size;
        let roles: string[] = [];
        
        // standard BANG! distributions
        if (numPlayers === 4) roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW'];
        else if (numPlayers === 5) roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'DEPUTY'];
        else if (numPlayers === 6) roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY'];
        else if (numPlayers === 7) roles = ['SHERIFF', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY', 'DEPUTY'];
        else if (numPlayers >= 8) roles = ['SHERIFF', 'RENEGADE', 'RENEGADE', 'OUTLAW', 'OUTLAW', 'OUTLAW', 'DEPUTY', 'DEPUTY']; // up to 8
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
    
    private startCharacterDraft() {
        this.state = ServerGameState.SELECTING_CHARACTER;
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
            p.characterOptions = [charPool[cIdx++], charPool[cIdx++]];
        }
        
        this.broadcastSnapshot();
    }
    
    private initializeGame() {
        // Initialize deck
        for (let i = 0; i < 80; i++) {
            const types = ['bang', 'missed', 'beer', 'stagecoach'];
            this.deck.push(types[Math.floor(Math.random() * types.length)] + '_' + i);
        }
        
        // Set health based on character (for now default 4, +1 if Sheriff)
        for (const p of this.players.values()) {
            p.maxHealth = p.role === 'SHERIFF' ? 5 : 4;
            p.currentHealth = p.maxHealth;
            p.hand = [];
            // deal initial hand
            for (let i = 0; i < p.maxHealth; i++) {
                p.hand.push(this.deck.pop()!);
            }
            p.handCount = p.hand.length;
        }
        this.state = ServerGameState.PLAYING;
        
        // Find Sheriff to start
        const sheriff = Array.from(this.players.values()).find(p => p.role === 'SHERIFF');
        if (sheriff) {
            this.turnNumber = 1;
            this.startTurn(sheriff.id);
        } else {
            this.broadcastSnapshot();
        }
    }

    private startTurn(playerId: string) {
        this.currentTurnPlayerId = playerId;
        this.currentPhase = "DRAW";
        
        // Draw 2 cards
        const p = this.players.get(playerId);
        if (p) {
            if (this.deck.length < 2) {
                // shuffle discard pile into deck... (skipping for now)
            }
            p.hand.push(this.deck.pop()!);
            p.hand.push(this.deck.pop()!);
            p.handCount = p.hand.length;
        }
        
        this.currentPhase = "PLAY";
        this.broadcastSnapshot();
    }
    
    private nextTurn() {
        this.currentPhase = "DISCARD"; // Skip actual discard phase for prototype simplicity
        
        const playerIds = Array.from(this.players.keys());
        const currentIndex = playerIds.indexOf(this.currentTurnPlayerId);
        
        let nextIndex = (currentIndex + 1) % playerIds.length;
        while (!this.players.get(playerIds[nextIndex])!.isAlive && nextIndex !== currentIndex) {
            nextIndex = (nextIndex + 1) % playerIds.length;
        }
        
        this.turnNumber++;
        this.startTurn(playerIds[nextIndex]);
    }
}

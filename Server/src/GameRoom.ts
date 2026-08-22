import { WebSocketServer, WebSocket } from 'ws';
import { MatchStateSnapshotDTO, PlayerSnapshotDTO, PrivatePlayerState, ServerGameState, RuleConfig } from './models/GameState';
import { v4 as uuidv4 } from 'uuid';

export interface ServerPlayerState {
    id: string;
    name: string;
    seat: number;
    isBot: boolean;
    isHost: boolean;
    isReady: boolean;
    isConnected: boolean;
    isAlive: boolean;
    currentHealth: number;
    maxHealth: number;
    characterId?: string;
    roleId?: string;
    isRoleRevealed: boolean;
    hand: string[];
    equipment: string[];
    effectiveDistanceToLocal: number;
    isTargetable: boolean;
    draftCharacterOptions?: string[];
    draftCharacterSlot1?: number;
    draftCharacterSlot2?: number;
    draftRoleSlot?: number;
}

export class GameRoom {
    public roomId: string;
    
    private wss: WebSocketServer;
    private state: ServerGameState = ServerGameState.WAITING;
    private players: Map<string, ServerPlayerState> = new Map();
    private sockets: Map<string, WebSocket> = new Map();
    
    private hostId: string = '';
    private turnNumber: number = 0;
    private sequence: number = 0;
    
    // Turn State
    private currentTurnPlayerId: string = "";
    private currentPhase: string = "";
    private activeInteraction: any = null; 
    private deck: string[] = [];
    private discardPile: string[] = [];

    // Draft State
    private phaseId: string = "";
    private deadlineAt: number = 0;
    private timerHandle: NodeJS.Timeout | null = null;
    
    private roleSlotLocks: Map<number, string> = new Map(); // slotIndex -> playerId
    private characterSlotLocks: Map<number, string> = new Map(); // slotIndex -> playerId
    private rolePool: string[] = [];
    private characterPool: string[] = [];

    private rules: RuleConfig;

    constructor(roomId: string, wss: WebSocketServer, config: any) {
        this.roomId = roomId;
        this.wss = wss;
        this.rules = {
            maxPlayers: config.maxPlayers || 5,
            botCount: config.botCount || 0,
            turnTimeSec: config.turnTimeSec || 30,
            startingHandMode: config.startingHandMode || 'FIXED_7'
        };
    }

    public joinPlayer(ws: WebSocket, name: string, isHost: boolean = false): boolean {
        if (this.players.size >= this.rules.maxPlayers) return false;
        if (this.state !== ServerGameState.WAITING) return false;

        const socketId = (ws as any).id;
        const player: ServerPlayerState = {
            id: socketId,
            name: name,
            seat: this.players.size,
            isBot: false,
            isHost: isHost,
            isReady: isHost,
            isConnected: true,
            isAlive: true,
            currentHealth: 4,
            maxHealth: 4,
            isRoleRevealed: false,
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
        else if (type === 'draft.role.pick') {
            this.handleRolePick(socketId, data.slotId);
        }
        else if (type === 'draft.character.pick') {
            this.handleCharacterPick(socketId, data.slotId);
        }
        else if (type === 'draft.character.confirm') {
            this.handleCharacterConfirm(socketId, data.characterId);
        }
        else if (type === 'game.action.play') {
            this.handlePlayCard(socketId, data);
        }
        else if (type === 'game.action.respond') {
            this.handleRespond(socketId, data);
        }
        else if (type === 'game.action.endTurn') {
            this.handleEndTurn(socketId);
        }
        else if (type === 'game.resync') {
            this.sendSnapshotTo(socketId);
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

    // --- SNAPSHOT GENERATION ---
    public getSnapshotFor(targetSocketId: string): MatchStateSnapshotDTO {
        const publicPlayers: PlayerSnapshotDTO[] = Array.from(this.players.values()).map(p => {
            return {
                id: p.id,
                name: p.name,
                seat: p.seat,
                isBot: p.isBot,
                isHost: p.isHost,
                isReady: p.isReady,
                isConnected: p.isConnected,
                isAlive: p.isAlive,
                currentHealth: p.currentHealth,
                maxHealth: p.maxHealth,
                characterId: (this.state === ServerGameState.CHARACTER_REVEAL || this.state === ServerGameState.INITIAL_DEAL || this.state === ServerGameState.PLAY || this.state === ServerGameState.TURN_START) ? p.characterId : undefined,
                publicRoleId: p.isRoleRevealed ? p.roleId : undefined,
                isRoleRevealed: p.isRoleRevealed,
                handCount: p.hand.length,
                equipment: p.equipment,
                effectiveDistanceToLocal: this.calculateDistance(targetPlayer, p), 
                isTargetable: this.isTargetable(targetPlayer, p)
            };
        });

        let privateState: PrivatePlayerState | undefined = undefined;
        const targetPlayer = this.players.get(targetSocketId);
        if (targetPlayer) {
            privateState = {
                roleId: targetPlayer.roleId,
                hand: targetPlayer.hand,
                draftCharacterOptions: targetPlayer.draftCharacterOptions
            };
        }

        return {
            roomId: this.roomId,
            roomCode: this.roomId,
            hostPlayerId: this.hostId,
            state: this.state,
            phaseId: this.phaseId,
            deadlineAt: this.deadlineAt,
            turnNumber: this.turnNumber,
            currentTurnPlayerId: this.currentTurnPlayerId,
            currentPhase: this.currentPhase,
            activeInteraction: this.activeInteraction,
            players: publicPlayers,
            privateState: privateState,
            drawPileCount: this.deck.length,
            topDiscardCardId: this.discardPile.length > 0 ? this.discardPile[this.discardPile.length - 1] : undefined,
            discardPileCount: this.discardPile.length,
            combatLogs: [],
            serverTime: Date.now(),
            sequence: this.sequence++,
            rules: this.rules
        };
    }

    private sendSnapshotTo(socketId: string) {
        const ws = this.sockets.get(socketId);
        if (ws && ws.readyState === WebSocket.OPEN) {
            const snap = this.getSnapshotFor(socketId);
            ws.send(JSON.stringify({ type: 'room.snapshot', data: JSON.stringify(snap) }));
        }
    }

    private broadcastSnapshot() {
        for (const [id, socket] of this.sockets.entries()) {
            if (socket.readyState === WebSocket.OPEN) {
                const snap = this.getSnapshotFor(id);
                socket.send(JSON.stringify({ type: 'room.snapshot', data: JSON.stringify(snap) }));
            }
        }
    }

    private broadcastMessage(type: string, data: any) {
        const payload = JSON.stringify({ type, data: JSON.stringify(data) });
        for (const socket of this.sockets.values()) {
            if (socket.readyState === WebSocket.OPEN) {
                socket.send(payload);
            }
        }
    }
    
    private sendPrivateMessage(socketId: string, type: string, data: any) {
        const ws = this.sockets.get(socketId);
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type, data: JSON.stringify(data) }));
        }
    }

    // --- GAME LOGIC STATE MACHINE ---

    private getSeatDistance(p1: ServerPlayerState, p2: ServerPlayerState): number {
        const alivePlayers = Array.from(this.players.values()).filter(p => p.isAlive).sort((a, b) => a.seat - b.seat);
        if (!p1.isAlive || !p2.isAlive) return 999;
        const i1 = alivePlayers.findIndex(p => p.id === p1.id);
        const i2 = alivePlayers.findIndex(p => p.id === p2.id);
        const n = alivePlayers.length;
        const dist = Math.abs(i1 - i2);
        return Math.min(dist, n - dist);
    }

    private calculateDistance(viewer: ServerPlayerState | undefined, target: ServerPlayerState): number {
        if (!viewer || viewer.id === target.id) return 0;
        let dist = this.getSeatDistance(viewer, target);
        
        // Target mods (Mustang/Hideout)
        if (target.equipment.some(e => e.startsWith('mustang') || e.startsWith('hideout'))) dist += 1;
        if (target.characterId === 'paul_regret') dist += 1;
        
        // Viewer mods (Scope)
        if (viewer.equipment.some(e => e.startsWith('scope'))) dist -= 1;
        if (viewer.characterId === 'rose_doolan') dist -= 1;
        
        return Math.max(1, dist);
    }

    private isTargetable(viewer: ServerPlayerState | undefined, target: ServerPlayerState): boolean {
        if (!viewer || viewer.id === target.id || !target.isAlive) return false;
        if (this.state !== ServerGameState.PLAY || this.currentTurnPlayerId !== viewer.id) return false;
        
        const dist = this.calculateDistance(viewer, target);
        let weaponRange = 1; // Colt .45
        if (viewer.equipment.some(e => e.startsWith('volcanic'))) weaponRange = 1;
        else if (viewer.equipment.some(e => e.startsWith('schofield'))) weaponRange = 2;
        else if (viewer.equipment.some(e => e.startsWith('remington'))) weaponRange = 3;
        else if (viewer.equipment.some(e => e.startsWith('rev_carabine'))) weaponRange = 4;
        else if (viewer.equipment.some(e => e.startsWith('winchester'))) weaponRange = 5;

        return dist <= weaponRange;
    }

    private startGame() {
        this.phaseId = uuidv4();
        this.state = ServerGameState.ROLE_DRAFT;
        console.log(`[ROOM ${this.roomId}] Starting game, ROLE_DRAFT`);
        
        // Setup Role Pool
        const numPlayers = this.players.size;
        this.rolePool = [];
        if (numPlayers === 4) this.rolePool = ['sheriff', 'renegade', 'outlaw', 'outlaw'];
        else if (numPlayers === 5) this.rolePool = ['sheriff', 'renegade', 'outlaw', 'outlaw', 'deputy'];
        else if (numPlayers === 6) this.rolePool = ['sheriff', 'renegade', 'outlaw', 'outlaw', 'outlaw', 'deputy'];
        else if (numPlayers === 7) this.rolePool = ['sheriff', 'renegade', 'outlaw', 'outlaw', 'outlaw', 'deputy', 'deputy'];
        else if (numPlayers >= 8) this.rolePool = ['sheriff', 'renegade', 'renegade', 'outlaw', 'outlaw', 'outlaw', 'deputy', 'deputy'];
        else this.rolePool = ['sheriff', 'outlaw', 'renegade', 'deputy'].slice(0, numPlayers);
        
        // Shuffle role pool
        for (let i = this.rolePool.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.rolePool[i], this.rolePool[j]] = [this.rolePool[j], this.rolePool[i]];
        }

        this.roleSlotLocks.clear();
        // Instant auto pick for testing
        this.deadlineAt = Date.now() + 1000;
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.handleRoleDraftTimeout();
        }, 1000);
    }

    private handleRolePick(socketId: string, slotId: number) {
        if (this.state !== ServerGameState.ROLE_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p) return;

        if (p.draftRoleSlot !== undefined) return; // already picked

        if (this.roleSlotLocks.has(slotId)) {
            // Taken
            this.sendPrivateMessage(socketId, 'draft.role.reject', { reason: 'SLOT_TAKEN' });
            return;
        }

        // Lock it
        this.roleSlotLocks.set(slotId, socketId);
        p.draftRoleSlot = slotId;
        p.roleId = this.rolePool[slotId];
        
        this.broadcastMessage('draft.role.slotLocked', { slotId, playerId: socketId });
        this.sendPrivateMessage(socketId, 'draft.role.assigned', { roleId: p.roleId });

        // Check if all picked
        const allPicked = Array.from(this.players.values()).every(player => player.draftRoleSlot !== undefined);
        if (allPicked) {
            this.transitionToRoleLockWait();
        }
    }

    private handleRoleDraftTimeout() {
        if (this.state !== ServerGameState.ROLE_DRAFT) return;

        // Auto assign remaining slots
        const availableSlots = [];
        for (let i = 0; i < this.rolePool.length; i++) {
            if (!this.roleSlotLocks.has(i)) availableSlots.push(i);
        }

        for (const p of this.players.values()) {
            if (p.draftRoleSlot === undefined) {
                const slot = availableSlots.pop()!;
                this.roleSlotLocks.set(slot, p.id);
                p.draftRoleSlot = slot;
                p.roleId = this.rolePool[slot];
                this.sendPrivateMessage(p.id, 'draft.role.assigned', { roleId: p.roleId });
            }
        }

        this.transitionToRoleLockWait();
    }

    private transitionToRoleLockWait() {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.state = ServerGameState.ROLE_LOCK_WAIT;
        this.deadlineAt = Date.now() + 3000;

        // Reveal Sheriff
        let sheriffId = "";
        for (const p of this.players.values()) {
            if (p.roleId === 'sheriff') {
                p.isRoleRevealed = true;
                sheriffId = p.id;
            }
        }
        
        this.broadcastMessage('draft.role.complete', { sheriffPlayerId: sheriffId, transitionAt: this.deadlineAt });
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.startCharacterDraft();
        }, 3000);
    }
    
    private startCharacterDraft() {
        this.phaseId = uuidv4();
        this.state = ServerGameState.CHARACTER_DRAFT;
        console.log(`[ROOM ${this.roomId}] Transitioning to CHARACTER_DRAFT`);
        
        const numPlayers = this.players.size;
        const totalCards = numPlayers * 2;
        const charPoolRaw = ['bart_cassidy', 'black_jack', 'calamity_janet', 'el_gringo', 'jesse_jones', 'jourdonnais', 'kit_carlson', 'lucky_duke', 'paul_regret', 'pedro_ramirez', 'rose_doolan', 'sid_ketchum', 'slab_the_killer', 'suzy_lafayette', 'vulture_sam', 'willy_the_kid'];
        
        // Shuffle char pool
        for (let i = charPoolRaw.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [charPoolRaw[i], charPoolRaw[j]] = [charPoolRaw[j], charPoolRaw[i]];
        }

        this.characterPool = charPoolRaw.slice(0, totalCards);
        this.characterSlotLocks.clear();

        this.deadlineAt = Date.now() + 1000; 
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.handleCharacterDraftTimeout();
        }, 1000);
    }

    private handleCharacterPick(socketId: string, slotId: number) {
        if (this.state !== ServerGameState.CHARACTER_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p) return;

        if (p.draftCharacterSlot1 !== undefined && p.draftCharacterSlot2 !== undefined) return;

        if (this.characterSlotLocks.has(slotId)) {
            this.sendPrivateMessage(socketId, 'draft.character.reject', { reason: 'SLOT_TAKEN' });
            return;
        }

        this.characterSlotLocks.set(slotId, socketId);
        
        if (p.draftCharacterSlot1 === undefined) {
            p.draftCharacterSlot1 = slotId;
        } else {
            p.draftCharacterSlot2 = slotId;
            // Provide options
            p.draftCharacterOptions = [this.characterPool[p.draftCharacterSlot1], this.characterPool[p.draftCharacterSlot2]];
            this.sendPrivateMessage(socketId, 'draft.character.options', { options: p.draftCharacterOptions, deadlineAt: this.deadlineAt });
        }
        
        this.broadcastMessage('draft.character.slotLocked', { slotId, playerId: socketId });
    }

    private handleCharacterConfirm(socketId: string, characterId: string) {
        if (this.state !== ServerGameState.CHARACTER_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p || !p.draftCharacterOptions || !p.draftCharacterOptions.includes(characterId)) return;

        p.characterId = characterId; 

        this.sendPrivateMessage(socketId, 'draft.character.assigned', { characterId: characterId });

        // Check if all confirmed
        const allConfirmed = Array.from(this.players.values()).every(player => player.characterId != null);
        if (allConfirmed) {
            this.transitionToCharacterReveal();
        }
    }

    private handleCharacterDraftTimeout() {
        if (this.state !== ServerGameState.CHARACTER_DRAFT) return;

        // Auto fill and pick
        const availableSlots = [];
        for (let i = 0; i < this.characterPool.length; i++) {
            if (!this.characterSlotLocks.has(i)) availableSlots.push(i);
        }

        for (const p of this.players.values()) {
            if (p.draftCharacterSlot1 === undefined) p.draftCharacterSlot1 = availableSlots.pop()!;
            if (p.draftCharacterSlot2 === undefined) p.draftCharacterSlot2 = availableSlots.pop()!;
            
            if (p.characterId == null) {
                p.draftCharacterOptions = [this.characterPool[p.draftCharacterSlot1], this.characterPool[p.draftCharacterSlot2]];
                p.characterId = p.draftCharacterOptions[Math.floor(Math.random() * 2)];
                this.sendPrivateMessage(p.id, 'draft.character.assigned', { characterId: p.characterId });
            }
        }

        this.transitionToCharacterReveal();
    }

    private transitionToCharacterReveal() {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.state = ServerGameState.CHARACTER_REVEAL;
        this.deadlineAt = Date.now() + 3000;

        // Setup base HP
        for (const p of this.players.values()) {
            p.maxHealth = 4; // Should get from catalog
            if (p.characterId === 'el_gringo' || p.characterId === 'paul_regret') p.maxHealth = 3;
            if (p.roleId === 'sheriff') p.maxHealth += 1;
            p.currentHealth = p.maxHealth;
        }

        this.broadcastMessage('draft.character.complete', { transitionAt: this.deadlineAt });
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.startInitialDeal();
        }, 3000);
    }
    
    private startInitialDeal() {
        this.state = ServerGameState.INITIAL_DEAL;
        console.log(`[ROOM ${this.roomId}] Transitioning to INITIAL_DEAL`);
        
        // Initialize deck
        this.deck = [];
        for (let i = 0; i < 80; i++) {
            const types = ['bang', 'missed', 'beer', 'stagecoach'];
            this.deck.push(types[Math.floor(Math.random() * types.length)] + '_' + i);
        }

        // Shuffle
        for (let i = this.deck.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
        }
        
        for (const p of this.players.values()) {
            p.hand = [];
            const drawCount = this.rules.startingHandMode === 'FIXED_7' ? 7 : p.maxHealth;
            for (let i = 0; i < drawCount; i++) {
                p.hand.push(this.deck.pop()!);
            }
        }
        
        this.broadcastSnapshot();

        // Deal animation time
        setTimeout(() => {
            // Find Sheriff to start
            const sheriff = Array.from(this.players.values()).find(p => p.roleId === 'sheriff');
            if (sheriff) {
                this.turnNumber = 1;
                this.state = ServerGameState.PLAY;
                this.startTurn(sheriff.id);
            }
        }, 2500);
    }

    private startTurn(playerId: string) {
        this.currentTurnPlayerId = playerId;
        this.currentPhase = "DRAW"; // Skip Judgement for now
        this.state = ServerGameState.PLAY; // PLAY state encompasses the turn loop
        
        // Draw 2 cards
        const p = this.players.get(playerId);
        if (p) {
            if (this.deck.length < 2) {
                // simple reshuffle
                this.deck = this.discardPile;
                this.discardPile = [];
                for (let i = this.deck.length - 1; i > 0; i--) {
                    const j = Math.floor(Math.random() * (i + 1));
                    [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
                }
            }
            if (this.deck.length > 0) p.hand.push(this.deck.pop()!);
            if (this.deck.length > 0) p.hand.push(this.deck.pop()!);
        }
        
        this.currentPhase = "PLAY";
        this.deadlineAt = Date.now() + (this.rules.turnTimeSec * 1000);
        this.broadcastSnapshot();
    }

    private nextTurn() {
        const playerIds = Array.from(this.players.keys());
        const currentIndex = playerIds.indexOf(this.currentTurnPlayerId);
        
        let nextIndex = (currentIndex + 1) % playerIds.length;
        while (!this.players.get(playerIds[nextIndex])!.isAlive && nextIndex !== currentIndex) {
            nextIndex = (nextIndex + 1) % playerIds.length;
        }
        
        this.turnNumber++;
        this.startTurn(playerIds[nextIndex]);
    }

    private handlePlayCard(socketId: string, data: any) {
        const p = this.players.get(socketId);
        if (p && this.state === ServerGameState.PLAY && this.currentTurnPlayerId === socketId && this.currentPhase === 'PLAY') {
            const { cardId, targetPlayerIds } = data;
            
            const idx = p.hand.findIndex(c => c === cardId);
            if (idx >= 0) {
                p.hand.splice(idx, 1);
                this.discardPile.push(cardId);
                
                if (cardId.startsWith('bang')) {
                    if (targetPlayerIds && targetPlayerIds.length > 0) {
                        const target = targetPlayerIds[0];
                        this.state = ServerGameState.RESPONSE;
                        this.activeInteraction = {
                            interactionId: 'bang_' + Date.now(),
                            type: 'RESPOND',
                            actorPlayerId: target,
                            title: 'BANG!',
                            message: `${p.name} đã bắn bạn! Dùng Missed! hoặc mất 1 máu.`,
                            validCardIds: ['missed_.*'], 
                            canCancel: true,
                            defaultAction: 'take_damage',
                            expiresAt: Date.now() + 10000
                        };
                    }
                }
                this.broadcastSnapshot();
            }
        }
    }

    private handleRespond(socketId: string, data: any) {
        if (this.state === ServerGameState.RESPONSE && this.activeInteraction && this.activeInteraction.actorPlayerId === socketId) {
            const { action, selectedCardIds } = data;
            const p = this.players.get(socketId);
            
            if ((action === 'USE_CARDS' || action === 'SUBMIT') && selectedCardIds && selectedCardIds.length > 0) {
                const cardId = selectedCardIds[0];
                const idx = p!.hand.findIndex(c => c === cardId);
                if (idx >= 0) {
                    p!.hand.splice(idx, 1);
                    this.discardPile.push(cardId);
                }
            } else if (action === 'CANCEL' || action === 'take_damage') {
                p!.currentHealth -= 1;
                if (p!.currentHealth <= 0) {
                    p!.isAlive = false;
                    this.broadcastMessage('player.eliminated', { playerId: p!.id, roleId: p!.roleId });
                }
            }
            
            this.activeInteraction = null;
            this.state = ServerGameState.PLAY; // Return to play phase of the current player
            this.broadcastSnapshot();
        }
    }

    private handleEndTurn(socketId: string) {
        if (this.state === ServerGameState.PLAY && this.currentTurnPlayerId === socketId && this.currentPhase === 'PLAY') {
            const p = this.players.get(socketId);
            if (p!.hand.length > p!.currentHealth) {
                // Enforce discard phase
                this.currentPhase = "DISCARD";
                this.deadlineAt = Date.now() + 15000;
                this.broadcastSnapshot();
                // To keep simple for prototype: just auto-discard right now if we want, or wait for explicit discard.
                // In full implementation, wait for `discard.submit` action.
            } else {
                this.nextTurn();
            }
        }
    }
}

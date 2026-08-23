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
    bangCardsPlayedThisTurn?: number;
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
    private revision: number = 0;
    private processedActionIds: Set<string> = new Set();
    private processedActionOrder: string[] = [];
    private bangCardsPlayedThisTurn: number = 0;
    
    // Turn State
    private currentTurnPlayerId: string = "";
    private currentPhase: string = "";
    private activeInteraction: any = null; 
    private winnerRole?: string;
    private winnerTeam?: string;
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
    private pendingMultiTargets: string[] = [];
    private pendingEffectType: string = '';
    private pendingEffectActorId: string = '';
    private generalStoreCards: string[] = [];
    private generalStoreOrder: string[] = [];
    private generalStoreIndex: number = 0;
    private duelParticipants: string[] = [];
    private duelResponderIndex: number = 0;
    private effectBeforeLethal: string = '';
    private actorBeforeLethal: string = '';
    private judgementCard: string = '';
    private judgementEffect: string = '';
    private judgementResult: string = '';

    private rules: RuleConfig;

    constructor(roomId: string, wss: WebSocketServer, config: any) {
        this.roomId = roomId;
        this.wss = wss;
        const clampInt = (value: unknown, fallback: number, min: number, max: number) =>
            Math.max(min, Math.min(max, Math.trunc(Number(value) || fallback)));
        this.rules = {
            maxPlayers: clampInt(config.maxPlayers, 5, 4, 8),
            botCount: clampInt(config.botCount, 0, 0, 7),
            turnTimeSec: clampInt(config.turnTimeSec, 30, 10, 120),
            startingHandMode: config.startingHandMode === 'BY_HP' ? 'BY_HP' : 'FIXED_7',
            roleDraftSec: clampInt(config.roleDraftSec, 20, 2, 60),
            characterDraftSec: clampInt(config.characterDraftSec, 30, 4, 90),
            responseTimeSec: clampInt(config.responseTimeSec, 10, 3, 30)
        };
    }

    public get maxPlayers(): number { return this.rules.maxPlayers; }
    public getPlayers(): ServerPlayerState[] { return Array.from(this.players.values()); }
    public getState(): ServerGameState { return this.state; }

    public dispose() {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.timerHandle = null;
        this.sockets.clear();
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

    public addBot(name?: string): boolean {
        if (this.state !== ServerGameState.WAITING || this.players.size >= this.rules.maxPlayers) return false;
        const index = this.players.size;
        const id = `bot_${this.roomId}_${index}_${Math.random().toString(36).slice(2, 7)}`;
        this.players.set(id, {
            id,
            name: name || `Bot Cao Bồi ${index + 1}`,
            seat: index,
            isBot: true,
            isHost: false,
            isReady: true,
            isConnected: true,
            isAlive: true,
            currentHealth: 4,
            maxHealth: 4,
            isRoleRevealed: false,
            hand: [],
            equipment: [],
            effectiveDistanceToLocal: 1,
            isTargetable: false
        });
        this.broadcastSnapshot();
        return true;
    }

    public reconnectPlayer(playerId: string, ws: WebSocket): boolean {
        const player = this.players.get(playerId);
        if (!player || player.isBot) return false;
        player.isConnected = true;
        this.sockets.set(playerId, ws);
        (ws as any).id = playerId;
        this.sendSnapshotTo(playerId);
        return true;
    }

    public handleMessage(ws: WebSocket, type: string, data: any) {
        const socketId = (ws as any).id;
        data = data || {};

        if (data.actionId) {
            if (this.processedActionIds.has(data.actionId)) {
                this.sendPrivateMessage(socketId, 'game.action.rejected', { reason: 'DUPLICATE_ACTION', revision: this.revision });
                return;
            }
            this.processedActionIds.add(data.actionId);
            this.processedActionOrder.push(data.actionId);
            if (this.processedActionOrder.length > 4096) {
                const oldest = this.processedActionOrder.shift();
                if (oldest) this.processedActionIds.delete(oldest);
            }
        }

        if (data.stateRevision !== undefined && data.stateRevision !== this.revision) {
            this.sendPrivateMessage(socketId, 'game.action.rejected', { reason: 'STALE_STATE', revision: this.revision });
            this.sendSnapshotTo(socketId);
            return;
        }
        
        if (type === 'room.ready') {
            const p = this.players.get(socketId);
            if (p && this.state === ServerGameState.WAITING) {
                p.isReady = data.isReady;
                this.broadcastSnapshot();
            }
        }
        else if (type === 'room.leave') {
            this.handleDisconnect(socketId);
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
        else if (type === 'room.addBot') {
            if (socketId === this.hostId) this.addBot();
        }
        else if (type === 'room.removeBot') {
            if (socketId === this.hostId && this.state === ServerGameState.WAITING) {
                const bot = Array.from(this.players.values()).filter(p => p.isBot).sort((a, b) => b.seat - a.seat)[0];
                if (bot) this.players.delete(bot.id);
                this.reseatPlayers();
                this.broadcastSnapshot();
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
        else if (type === 'discard.submit') {
            this.handleDiscardSubmit(socketId, data.cardIds || []);
        }
        else if (type === 'effect.generalStorePick') {
            this.handleGeneralStorePick(socketId, data.cardInstanceId);
        }
        else if (type === 'game.action.activateAbility') {
            this.handleActivateAbility(socketId, data);
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

    private reseatPlayers() {
        Array.from(this.players.values()).sort((a, b) => a.seat - b.seat).forEach((p, i) => p.seat = i);
    }

    // --- SNAPSHOT GENERATION ---
    public getSnapshotFor(targetSocketId: string): MatchStateSnapshotDTO {
        const targetPlayer = this.players.get(targetSocketId);
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
                characterId: this.isCharacterPublic() ? p.characterId : undefined,
                publicRoleId: p.isRoleRevealed ? p.roleId : undefined,
                isRoleRevealed: p.isRoleRevealed,
                handCount: p.hand.length,
                equipment: p.equipment,
                effectiveDistanceToLocal: this.calculateDistance(targetPlayer, p), 
                isTargetable: this.isTargetable(targetPlayer, p)
            };
        });

        let privateState: PrivatePlayerState | undefined = undefined;
        if (targetPlayer) {
            privateState = {
                roleId: targetPlayer.roleId,
                hand: targetPlayer.hand,
                draftCharacterOptions: targetPlayer.draftCharacterOptions,
                draftRoleSlot: targetPlayer.draftRoleSlot,
                draftCharacterSlots: [targetPlayer.draftCharacterSlot1, targetPlayer.draftCharacterSlot2]
                    .filter((slot): slot is number => slot !== undefined),
                selectedCharacterId: targetPlayer.characterId
            };
        }

        return {
            roomId: this.roomId,
            roomCode: this.roomId,
            hostPlayerId: this.hostId,
            state: this.state,
            phaseId: this.phaseId,
            deadlineAt: this.deadlineAt,
            draftSlotCount: this.state === ServerGameState.ROLE_DRAFT || this.state === ServerGameState.ROLE_LOCK_WAIT
                ? this.rolePool.length
                : this.state === ServerGameState.CHARACTER_DRAFT ? this.characterPool.length : 0,
            lockedDraftSlots: this.state === ServerGameState.ROLE_DRAFT || this.state === ServerGameState.ROLE_LOCK_WAIT
                ? Array.from(this.roleSlotLocks.keys())
                : this.state === ServerGameState.CHARACTER_DRAFT ? Array.from(this.characterSlotLocks.keys()) : [],
            judgementCard: this.judgementCard || undefined,
            judgementEffect: this.judgementEffect || undefined,
            judgementResult: this.judgementResult || undefined,
            turnNumber: this.turnNumber,
            currentTurnPlayerId: this.currentTurnPlayerId,
            currentPhase: this.currentPhase,
            activeInteraction: this.activeInteraction?.actorPlayerId === targetSocketId ? this.activeInteraction : undefined,
            winnerRole: this.winnerRole,
            winnerTeam: this.winnerTeam,
            players: publicPlayers,
            privateState: privateState,
            drawPileCount: this.deck.length,
            topDiscardCardId: this.discardPile.length > 0 ? this.discardPile[this.discardPile.length - 1] : undefined,
            discardPileCount: this.discardPile.length,
            combatLogs: [],
            serverTime: Date.now(),
            sequence: this.sequence,
            revision: this.revision,
            rules: this.rules
        };
    }

    private isCharacterPublic(): boolean {
        return ![ServerGameState.WAITING, ServerGameState.ROLE_DRAFT, ServerGameState.ROLE_LOCK_WAIT, ServerGameState.CHARACTER_DRAFT].includes(this.state);
    }

    private sendSnapshotTo(socketId: string) {
        const ws = this.sockets.get(socketId);
        if (ws && ws.readyState === WebSocket.OPEN) {
            const snap = this.getSnapshotFor(socketId);
            ws.send(JSON.stringify({ type: 'room.snapshot', data: JSON.stringify(snap) }));
        }
    }

    private broadcastSnapshot() {
        this.revision++;
        this.sequence++;
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
        if (viewer.equipment.some(e => this.cardType(e) === 'scope' || this.cardType(e) === 'appaloosa')) dist -= 1;
        if (viewer.characterId === 'rose_oolan' || viewer.characterId === 'rose_doolan') dist -= 1;
        
        return Math.max(1, dist);
    }

    private isTargetable(viewer: ServerPlayerState | undefined, target: ServerPlayerState): boolean {
        if (!viewer || viewer.id === target.id || !target.isAlive) return false;
        if (this.state !== ServerGameState.PLAY || this.currentTurnPlayerId !== viewer.id) return false;
        
        const dist = this.calculateDistance(viewer, target);
        let weaponRange = 1; // Colt .45
        const weaponType = viewer.equipment.map(e => this.cardType(e)).find(t => ['volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5'].includes(t));
        if (weaponType === 'gun_range_2') weaponRange = 2;
        else if (weaponType === 'gun_range_3') weaponRange = 3;
        else if (weaponType === 'gun_range_4') weaponRange = 4;
        else if (weaponType === 'gun_range_5') weaponRange = 5;

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
        for (const player of this.players.values()) {
            player.draftRoleSlot = undefined;
            player.draftCharacterSlot1 = undefined;
            player.draftCharacterSlot2 = undefined;
            player.draftCharacterOptions = undefined;
            player.roleId = undefined;
            player.characterId = undefined;
            player.isRoleRevealed = false;
        }
        const roleSlots = this.rolePool.map((_, index) => index);
        for (const bot of Array.from(this.players.values()).filter(player => player.isBot)) {
            const slot = roleSlots.splice(Math.floor(Math.random() * roleSlots.length), 1)[0];
            this.roleSlotLocks.set(slot, bot.id);
            bot.draftRoleSlot = slot;
            bot.roleId = this.rolePool[slot];
        }
        this.deadlineAt = Date.now() + this.rules.roleDraftSec * 1000;
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.handleRoleDraftTimeout();
        }, this.rules.roleDraftSec * 1000);
    }

    private handleRolePick(socketId: string, slotId: number) {
        if (this.state !== ServerGameState.ROLE_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p) return;

        if (!Number.isInteger(slotId) || slotId < 0 || slotId >= this.rolePool.length) {
            this.sendPrivateMessage(socketId, 'draft.role.reject', { reason: 'INVALID_SLOT' });
            return;
        }

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
        
        this.broadcastMessage('draft.role.slotLocked', { slotId });
        this.sendPrivateMessage(socketId, 'draft.role.assigned', { roleId: p.roleId });
        this.broadcastSnapshot();

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

        const characterSlots = this.characterPool.map((_, index) => index);
        for (const bot of Array.from(this.players.values()).filter(player => player.isBot)) {
            const first = characterSlots.splice(Math.floor(Math.random() * characterSlots.length), 1)[0];
            const second = characterSlots.splice(Math.floor(Math.random() * characterSlots.length), 1)[0];
            bot.draftCharacterSlot1 = first;
            bot.draftCharacterSlot2 = second;
            bot.draftCharacterOptions = [this.characterPool[first], this.characterPool[second]];
            bot.characterId = bot.draftCharacterOptions[Math.floor(Math.random() * 2)];
            this.characterSlotLocks.set(first, bot.id);
            this.characterSlotLocks.set(second, bot.id);
        }

        this.deadlineAt = Date.now() + this.rules.characterDraftSec * 1000;
        this.broadcastSnapshot();

        this.timerHandle = setTimeout(() => {
            this.handleCharacterDraftTimeout();
        }, this.rules.characterDraftSec * 1000);
    }

    private handleCharacterPick(socketId: string, slotId: number) {
        if (this.state !== ServerGameState.CHARACTER_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p) return;

        if (!Number.isInteger(slotId) || slotId < 0 || slotId >= this.characterPool.length) {
            this.sendPrivateMessage(socketId, 'draft.character.reject', { reason: 'INVALID_SLOT' });
            return;
        }

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
        
        this.broadcastMessage('draft.character.slotLocked', { slotId });
        this.broadcastSnapshot();
    }

    private handleCharacterConfirm(socketId: string, characterId: string) {
        if (this.state !== ServerGameState.CHARACTER_DRAFT) return;
        const p = this.players.get(socketId);
        if (!p || !p.draftCharacterOptions || !p.draftCharacterOptions.includes(characterId)) return;

        p.characterId = characterId; 

        this.sendPrivateMessage(socketId, 'draft.character.assigned', { characterId: characterId });
        this.broadcastSnapshot();

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
            if (p.draftCharacterSlot1 === undefined) {
                p.draftCharacterSlot1 = availableSlots.pop()!;
                this.characterSlotLocks.set(p.draftCharacterSlot1, p.id);
            }
            if (p.draftCharacterSlot2 === undefined) {
                p.draftCharacterSlot2 = availableSlots.pop()!;
                this.characterSlotLocks.set(p.draftCharacterSlot2, p.id);
            }
            
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
        
        this.deck = this.createDeck();

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
        this.timerHandle = setTimeout(() => {
            // Find Sheriff to start
            const sheriff = Array.from(this.players.values()).find(p => p.roleId === 'sheriff');
            if (sheriff) {
                this.turnNumber = 1;
                this.state = ServerGameState.PLAY;
                this.startTurn(sheriff.id);
            }
        }, 2500);
    }

    private createDeck(): string[] {
        // Data-driven type cycle from the catalog currently shipped with Unity.
        // Suit/rank are part of each instance so all judgement results are reproducible.
        const weightedTypes = [
            'bang','bang','bang','bang','dodge','dodge','dodge','beer','beer',
            'cat_balou','panico','dilizenza','wells_fargo','saloon','general_store',
            'duello','indiani','gatling','mustang','appaloosa','barrel','jail','dynamite',
            'volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5'
        ];
        const suits = ['spades', 'hearts', 'diamonds', 'clubs'];
        const ranks = ['A','2','3','4','5','6','7','8','9','10','J','Q','K'];
        const cards: string[] = [];
        for (let i = 0; i < 80; i++) {
            const type = weightedTypes[i % weightedTypes.length];
            cards.push(`${type}__${i}__${suits[i % suits.length]}__${ranks[i % ranks.length]}`);
        }
        return cards;
    }

    private cardType(instanceId: string): string {
        return (instanceId || '').split('__')[0];
    }

    private cardSuit(instanceId: string): string {
        return (instanceId || '').split('__')[2] || '';
    }

    private cardRank(instanceId: string): string {
        return (instanceId || '').split('__')[3] || '';
    }

    private startTurn(playerId: string) {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.currentTurnPlayerId = playerId;
        this.currentPhase = "START";
        this.state = ServerGameState.TURN_START;
        this.bangCardsPlayedThisTurn = 0;
        this.judgementCard = '';
        this.judgementEffect = '';
        this.judgementResult = '';
        this.deadlineAt = Date.now() + 600;
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.startJudgementPhase(), 600);
    }

    private startJudgementPhase() {
        this.state = ServerGameState.JUDGEMENT;
        this.currentPhase = "JUDGEMENT";
        this.deadlineAt = Date.now() + 900;
        this.broadcastSnapshot();
        const player = this.players.get(this.currentTurnPlayerId);
        if (!player) { this.timerHandle = setTimeout(() => this.startDrawPhase(), 600); return; }

        const dynamite = player.equipment.find(c => this.cardType(c) === 'dynamite');
        if (dynamite) {
            const judgement = this.drawJudgement(player, card => !(this.cardSuit(card) === 'spades' && this.isRankBetween(card, 2, 9)));
            this.judgementCard = judgement.card;
            this.judgementEffect = 'DYNAMITE';
            this.judgementResult = judgement.matched ? 'CHUYỂN TIẾP' : 'PHÁT NỔ: -3 HP';
            this.broadcastMessage('judgement.cardRevealed', { playerId: player.id, effect: 'dynamite', card: judgement.card });
            if (!judgement.matched) {
                player.equipment.splice(player.equipment.indexOf(dynamite), 1);
                this.discardPile.push(dynamite);
                this.applyDamage(player, 3);
                this.broadcastSnapshot();
                if (this.getState() === ServerGameState.GAME_OVER) return;
                if (this.getState() === ServerGameState.RESPONSE) return;
            } else {
                this.passDynamite(player, dynamite);
                this.broadcastSnapshot();
            }
        }

        const jail = player.equipment.find(c => this.cardType(c) === 'jail');
        if (jail && player.isAlive) {
            const judgement = this.drawJudgement(player, card => this.cardSuit(card) === 'hearts');
            this.judgementCard = judgement.card;
            this.judgementEffect = 'JAIL';
            this.judgementResult = judgement.matched ? 'THOÁT TÙ' : 'MẤT LƯỢT';
            player.equipment.splice(player.equipment.indexOf(jail), 1);
            this.discardPile.push(jail);
            this.broadcastMessage('judgement.cardRevealed', { playerId: player.id, effect: 'jail', card: judgement.card });
            if (!judgement.matched) {
                this.broadcastMessage('judgement.resolved', { playerId: player.id, effect: 'jail', result: 'SKIP_TURN' });
                this.timerHandle = setTimeout(() => this.nextTurn(), 900);
                this.broadcastSnapshot();
                return;
            }
            this.broadcastSnapshot();
        }

        this.timerHandle = setTimeout(() => this.startDrawPhase(), 900);
    }

    private drawJudgement(player: ServerPlayerState, predicate: (card: string) => boolean): { card: string, matched: boolean } {
        const count = player.characterId === 'lucky_duke' ? 2 : 1;
        const cards: string[] = [];
        this.ensureDeck(count);
        for (let i = 0; i < count; i++) {
            const card = this.deck.pop();
            if (card) cards.push(card);
        }
        const chosen = cards.find(predicate) || cards[0] || '';
        for (const card of cards) this.discardPile.push(card);
        return { card: chosen, matched: predicate(chosen) };
    }

    private isRankBetween(card: string, min: number, max: number): boolean {
        const value = Number(this.cardRank(card));
        return Number.isFinite(value) && value >= min && value <= max;
    }

    private passDynamite(owner: ServerPlayerState, dynamite: string) {
        const alive = Array.from(this.players.values()).filter(p => p.isAlive).sort((a, b) => a.seat - b.seat);
        const start = alive.findIndex(p => p.id === owner.id);
        for (let offset = 1; offset < alive.length; offset++) {
            const target = alive[(start + offset) % alive.length];
            if (!target.equipment.some(c => this.cardType(c) === 'dynamite')) {
                owner.equipment.splice(owner.equipment.indexOf(dynamite), 1);
                target.equipment.push(dynamite);
                return;
            }
        }
    }

    private ensureDeck(count: number) {
        if (this.deck.length >= count) return;
        if (this.discardPile.length === 0) return;
        this.deck.push(...this.discardPile.splice(0));
        for (let i = this.deck.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
        }
    }

    private startDrawPhase() {
        this.state = ServerGameState.DRAW;
        this.currentPhase = "DRAW";
        const p = this.players.get(this.currentTurnPlayerId);
        if (p) {
            if (p.isBot && p.characterId === 'pedro_ramirez' && this.discardPile.length > 0) p.hand.push(this.discardPile.pop()!);
            else if (p.isBot && p.characterId === 'jesse_jones') {
                const victim = this.alivePlayers().find(other => other.id !== p.id && other.hand.length > 0);
                if (victim) this.stealRandomCard(p, victim); else this.drawCards(p, 1);
            } else if (p.characterId === 'kit_carlson') {
                this.ensureDeck(3);
                const top = [this.deck.pop(), this.deck.pop(), this.deck.pop()].filter(Boolean) as string[];
                p.hand.push(...top.slice(0, 2));
                if (top[2]) this.deck.push(top[2]);
            } else {
                const before = p.hand.length;
                this.drawCards(p, 2);
                if (p.characterId === 'black_jack') {
                    const second = p.hand[before + 1];
                    if (second && ['hearts','diamonds'].includes(this.cardSuit(second))) this.drawCards(p, 1);
                }
            }
        }
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.startPlayPhase(), 450);
    }

    private startPlayPhase() {
        this.state = ServerGameState.PLAY;
        this.currentPhase = "PLAY";
        this.deadlineAt = Date.now() + (this.rules.turnTimeSec * 1000);
        this.broadcastSnapshot();
        const player = this.players.get(this.currentTurnPlayerId);
        this.timerHandle = setTimeout(() => player?.isBot ? this.runBotTurn(player) : this.finishOrDiscardCurrentTurn(), player?.isBot ? 450 : this.rules.turnTimeSec * 1000);
    }

    private runBotTurn(bot: ServerPlayerState) {
        if (this.state !== ServerGameState.PLAY || this.currentTurnPlayerId !== bot.id) return;

        if (bot.characterId === 'sid_ketchum' && bot.currentHealth < bot.maxHealth && bot.hand.length >= 2) {
            this.handleActivateAbility(bot.id, { cardIds: bot.hand.slice(0, 2) });
        }
        if (this.state !== ServerGameState.PLAY) return;

        const beer = bot.hand.find(c => this.cardType(c) === 'beer');
        if (beer && bot.currentHealth < bot.maxHealth && this.alivePlayers().length > 2) this.handlePlayCard(bot.id, { cardId: beer, targetPlayerIds: [] });
        if (this.state !== ServerGameState.PLAY) return;

        const equipmentTypes = new Set(['volcanic', 'schofield', 'remington', 'rev_carabine', 'winchester', 'scope', 'mustang', 'barrel', 'dynamite']);
        const equipment = bot.hand.find(c => equipmentTypes.has(this.cardType(c)));
        if (equipment) this.handlePlayCard(bot.id, { cardId: equipment, targetPlayerIds: [] });
        if (this.state !== ServerGameState.PLAY) return;

        const target = this.alivePlayers().find(p => p.id !== bot.id && this.isTargetable(bot, p));
        const bang = bot.hand.find(c => this.cardType(c) === 'bang');
        if (bang && target) this.handlePlayCard(bot.id, { cardId: bang, targetPlayerIds: [target.id] });
        if (this.state !== ServerGameState.PLAY) return;

        const globalAction = bot.hand.find(c => ['general_store', 'indiani', 'gatling', 'saloon', 'dilizenza', 'wells_fargo'].includes(this.cardType(c)));
        if (globalAction) this.handlePlayCard(bot.id, { cardId: globalAction, targetPlayerIds: [] });
        if (this.state === ServerGameState.PLAY) this.finishOrDiscardCurrentTurn();
    }

    private drawCards(player: ServerPlayerState, count: number) {
        for (let n = 0; n < count; n++) {
            if (this.deck.length === 0 && this.discardPile.length > 0) {
                this.deck = this.discardPile.splice(0);
                for (let i = this.deck.length - 1; i > 0; i--) {
                    const j = Math.floor(Math.random() * (i + 1));
                    [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
                }
            }
            const card = this.deck.pop();
            if (card) player.hand.push(card);
        }
    }

    private nextTurn() {
        const playerIds = Array.from(this.players.values()).sort((a, b) => a.seat - b.seat).map(p => p.id);
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
        if (!p || this.state !== ServerGameState.PLAY || this.currentTurnPlayerId !== socketId || this.currentPhase !== 'PLAY') return;
        const cardId = data.cardId;
        const rawType = this.cardType(cardId);
        const type = rawType === 'dodge' && p.characterId === 'calamity_janet' ? 'bang' : rawType;
        const target = this.players.get(data.targetPlayerIds?.[0]);
        const idx = p.hand.indexOf(cardId);
        if (idx < 0) { this.reject(socketId, 'CARD_NOT_IN_HAND'); return; }

        if (['bang','panico','cat_balou','duello','jail'].includes(type) && (!target || !target.isAlive || target.id === p.id)) {
            this.reject(socketId, 'INVALID_TARGET'); return;
        }
        if (type === 'bang' && (!target || !this.isTargetable(p, target))) { this.reject(socketId, 'INVALID_TARGET'); return; }
        if (type === 'panico' && (!target || this.calculateDistance(p, target) > 1)) { this.reject(socketId, 'OUT_OF_RANGE'); return; }
        if (type === 'jail' && (!target || target.roleId === 'sheriff' || target.equipment.some(c => this.cardType(c) === 'jail'))) {
            this.reject(socketId, 'INVALID_JAIL_TARGET'); return;
        }
        if (type === 'beer' && (p.currentHealth >= p.maxHealth || this.alivePlayers().length <= 2)) { this.reject(socketId, 'BEER_NOT_ALLOWED'); return; }
        if (type === 'dynamite' && p.equipment.some(c => this.cardType(c) === 'dynamite')) { this.reject(socketId, 'DUPLICATE_EQUIPMENT'); return; }
        if (type === 'bang') {
            const unlimited = p.equipment.some(e => this.cardType(e) === 'volcanic') || p.characterId === 'willy_the_kid';
            if (!unlimited && this.bangCardsPlayedThisTurn >= 1) { this.reject(socketId, 'BANG_LIMIT'); return; }
            this.bangCardsPlayedThisTurn++;
        }

        p.hand.splice(idx, 1);
        if (this.isEquipment(type)) this.equipCard(p, target, cardId, type);
        else this.discardPile.push(cardId);
        this.triggerEmptyHandAbility(p);

        switch (type) {
            case 'bang': this.openBangResponse(p, target!); return;
            case 'beer': this.heal(p, 1); break;
            case 'saloon': for (const player of this.alivePlayers()) this.heal(player, 1); break;
            case 'dilizenza': this.drawCards(p, 2); break;
            case 'wells_fargo': this.drawCards(p, 3); break;
            case 'panico': this.stealRandomCard(p, target!); break;
            case 'cat_balou': this.discardRandomCard(target!); break;
            case 'general_store': this.startGeneralStore(p); return;
            case 'duello': this.startDuel(p, target!); return;
            case 'indiani': this.startMultiTargetEffect('indiani', p); return;
            case 'gatling': this.startMultiTargetEffect('gatling', p); return;
        }
        this.broadcastSnapshot();
    }

    private reject(playerId: string, reason: string) {
        this.sendPrivateMessage(playerId, 'game.action.rejected', { reason, revision: this.revision });
    }

    private alivePlayers(): ServerPlayerState[] {
        return Array.from(this.players.values()).filter(p => p.isAlive).sort((a, b) => a.seat - b.seat);
    }

    private isEquipment(type: string): boolean {
        return ['mustang','appaloosa','barrel','jail','dynamite','volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5'].includes(type);
    }

    private equipCard(actor: ServerPlayerState, target: ServerPlayerState | undefined, card: string, type: string) {
        const owner = type === 'jail' ? target! : actor;
        const weapon = ['volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5'].includes(type);
        if (weapon) {
            const previous = owner.equipment.find(c => ['volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5'].includes(this.cardType(c)));
            if (previous) { owner.equipment.splice(owner.equipment.indexOf(previous), 1); this.discardPile.push(previous); }
        } else {
            const previous = owner.equipment.find(c => this.cardType(c) === type);
            if (previous) { owner.equipment.splice(owner.equipment.indexOf(previous), 1); this.discardPile.push(previous); }
        }
        owner.equipment.push(card);
    }

    private heal(player: ServerPlayerState, amount: number) {
        player.currentHealth = Math.min(player.maxHealth, player.currentHealth + amount);
    }

    private stealRandomCard(actor: ServerPlayerState, target: ServerPlayerState) {
        const pool = [...target.hand, ...target.equipment];
        if (pool.length === 0) return;
        const card = pool[Math.floor(Math.random() * pool.length)];
        const handIndex = target.hand.indexOf(card);
        if (handIndex >= 0) target.hand.splice(handIndex, 1); else target.equipment.splice(target.equipment.indexOf(card), 1);
        actor.hand.push(card);
        this.triggerEmptyHandAbility(target);
    }

    private discardRandomCard(target: ServerPlayerState) {
        const pool = [...target.hand, ...target.equipment];
        if (pool.length === 0) return;
        const card = pool[Math.floor(Math.random() * pool.length)];
        const handIndex = target.hand.indexOf(card);
        if (handIndex >= 0) target.hand.splice(handIndex, 1); else target.equipment.splice(target.equipment.indexOf(card), 1);
        this.discardPile.push(card);
        this.triggerEmptyHandAbility(target);
    }

    private openBangResponse(actor: ServerPlayerState, target: ServerPlayerState) {
        const hasBarrel = target.equipment.some(c => this.cardType(c) === 'barrel') || target.characterId === 'jourdonnais';
        if (hasBarrel) {
            const judgement = this.drawJudgement(target, card => this.cardSuit(card) === 'hearts');
            this.broadcastMessage('judgement.cardRevealed', { playerId: target.id, effect: 'barrel', card: judgement.card });
            if (judgement.matched) { this.broadcastSnapshot(); return; }
        }
        const required = actor.characterId === 'slab_the_killer' ? 2 : 1;
        this.openCardResponse('bang', actor.id, target.id, 'dodge', required);
    }

    private openCardResponse(effect: string, actorId: string, targetId: string, requiredType: string, requiredCount: number) {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.state = ServerGameState.RESPONSE;
        this.currentPhase = 'RESPONSE';
        this.pendingEffectType = effect;
        this.pendingEffectActorId = actorId;
        const target = this.players.get(targetId);
        this.activeInteraction = {
            interactionId: `${effect}_${Date.now()}_${targetId}`,
            type: effect === 'duello' ? 'DUEL' : 'RESPOND',
            actorPlayerId: targetId,
            title: effect.toUpperCase(),
            message: `Dùng ${requiredCount} lá ${requiredType.toUpperCase()} hoặc PASS.`,
            minSelections: 0,
            maxSelections: requiredCount,
            requiredCount,
            requiredCardType: requiredType,
            validPlayerIds: [],
            validCardIds: target ? target.hand.filter(card => this.responseCardMatches(target, card, requiredType)) : [],
            options: ['PASS'],
            canCancel: true,
            defaultAction: 'PASS',
            expiresAt: Date.now() + this.rules.responseTimeSec * 1000
        };
        this.deadlineAt = this.activeInteraction.expiresAt;
        this.broadcastSnapshot();
        if (target?.isBot) this.timerHandle = setTimeout(() => this.resolveBotResponse(target), 350);
        else this.timerHandle = setTimeout(() => this.resolveResponseTimeout(), this.rules.responseTimeSec * 1000);
    }

    private responseCardMatches(player: ServerPlayerState, card: string, requiredType: string): boolean {
        const type = this.cardType(card);
        if (type === requiredType) return true;
        return player.characterId === 'calamity_janet' && ((requiredType === 'dodge' && type === 'bang') || (requiredType === 'bang' && type === 'dodge'));
    }

    private resolveBotResponse(bot: ServerPlayerState) {
        if (!this.activeInteraction || this.activeInteraction.actorPlayerId !== bot.id) return;
        const matching = bot.hand.filter(c => this.responseCardMatches(bot, c, this.activeInteraction.requiredCardType));
        const count = this.activeInteraction.requiredCount || 1;
        this.handleRespond(bot.id, matching.length >= count ? { action: 'USE_CARDS', selectedCardIds: matching.slice(0, count) } : { action: 'PASS' });
    }

    private startMultiTargetEffect(type: 'indiani' | 'gatling', actor: ServerPlayerState) {
        this.pendingMultiTargets = this.alivePlayers().filter(p => p.id !== actor.id).map(p => p.id);
        this.pendingEffectType = type;
        this.pendingEffectActorId = actor.id;
        this.continueMultiTargetEffect();
    }

    private continueMultiTargetEffect() {
        if (this.getState() === ServerGameState.GAME_OVER) return;
        const targetId = this.pendingMultiTargets.shift();
        if (!targetId) { this.returnToPlay(); return; }
        this.broadcastMessage('effect.multiTargetProgress', { effect: this.pendingEffectType, remaining: this.pendingMultiTargets.length + 1 });
        this.openCardResponse(this.pendingEffectType, this.pendingEffectActorId, targetId, this.pendingEffectType === 'indiani' ? 'bang' : 'dodge', 1);
    }

    private startDuel(actor: ServerPlayerState, target: ServerPlayerState) {
        this.duelParticipants = [actor.id, target.id];
        this.duelResponderIndex = 1;
        this.openCardResponse('duello', actor.id, target.id, 'bang', 1);
    }

    private continueDuel(responded: boolean) {
        if (!responded) return;
        this.duelResponderIndex = this.duelResponderIndex === 0 ? 1 : 0;
        const responder = this.duelParticipants[this.duelResponderIndex];
        const other = this.duelParticipants[this.duelResponderIndex === 0 ? 1 : 0];
        this.openCardResponse('duello', other, responder, 'bang', 1);
    }

    private startGeneralStore(actor: ServerPlayerState) {
        const alive = this.alivePlayers();
        this.ensureDeck(alive.length);
        this.generalStoreCards = [];
        for (let i = 0; i < alive.length; i++) {
            const card = this.deck.pop();
            if (card) this.generalStoreCards.push(card);
        }
        const start = alive.findIndex(p => p.id === actor.id);
        this.generalStoreOrder = alive.map((_, i) => alive[(start + i) % alive.length].id);
        this.generalStoreIndex = 0;
        this.promptGeneralStorePicker();
    }

    private promptGeneralStorePicker() {
        if (this.generalStoreCards.length === 0 || this.generalStoreIndex >= this.generalStoreOrder.length) { this.returnToPlay(); return; }
        const pickerId = this.generalStoreOrder[this.generalStoreIndex];
        this.state = ServerGameState.RESPONSE;
        this.currentPhase = 'GENERAL_STORE';
        this.deadlineAt = Date.now() + this.rules.responseTimeSec * 1000;
        this.activeInteraction = {
            interactionId: `general_store_${Date.now()}_${pickerId}`,
            type: 'CHOOSE_CARD',
            actorPlayerId: pickerId,
            title: 'GENERAL STORE',
            message: 'Chọn một lá bài công khai.',
            minSelections: 1,
            maxSelections: 1,
            validPlayerIds: [],
            validCardIds: [...this.generalStoreCards],
            options: [],
            canCancel: false,
            defaultAction: 'AUTO',
            expiresAt: this.deadlineAt
        };
        this.broadcastMessage('effect.generalStoreUpdated', { cards: this.generalStoreCards, currentPickerId: pickerId, deadlineAt: this.deadlineAt });
        const picker = this.players.get(pickerId);
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.timerHandle = setTimeout(() => {
            const card = this.generalStoreCards[Math.floor(Math.random() * this.generalStoreCards.length)];
            this.handleGeneralStorePick(pickerId, card);
        }, picker?.isBot ? 350 : this.rules.responseTimeSec * 1000);
    }

    private handleGeneralStorePick(playerId: string, card: string) {
        if (this.currentPhase !== 'GENERAL_STORE' || this.generalStoreOrder[this.generalStoreIndex] !== playerId || !this.generalStoreCards.includes(card)) return;
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.players.get(playerId)?.hand.push(card);
        this.generalStoreCards.splice(this.generalStoreCards.indexOf(card), 1);
        this.generalStoreIndex++;
        this.promptGeneralStorePicker();
    }

    private handleRespond(socketId: string, data: any) {
        if (this.state === ServerGameState.RESPONSE && this.currentPhase === 'GENERAL_STORE' && this.activeInteraction?.actorPlayerId === socketId) {
            const card = (data.selectedCardIds || [])[0];
            if (card) this.handleGeneralStorePick(socketId, card);
            return;
        }
        if (this.state === ServerGameState.RESPONSE && this.activeInteraction && this.activeInteraction.actorPlayerId === socketId) {
            const { action, selectedCardIds } = data;
            const p = this.players.get(socketId);
            if (!p) return;
            const requiredType = this.activeInteraction.requiredCardType || 'dodge';
            const requiredCount = this.activeInteraction.requiredCount || 1;
            const resolvingEffect = this.pendingEffectType;
            const cards: string[] = selectedCardIds || [];
            const responded = (action === 'USE_CARDS' || action === 'SUBMIT') && cards.length === requiredCount;
            if (responded && (new Set(cards).size !== cards.length || cards.some(card => !p.hand.includes(card) || !this.responseCardMatches(p, card, requiredType)))) {
                this.reject(socketId, 'INVALID_RESPONSE_CARD');
                return;
            }
            if (responded) {
                for (const card of cards) {
                    p.hand.splice(p.hand.indexOf(card), 1);
                    this.discardPile.push(card);
                }
                this.triggerEmptyHandAbility(p);
            }
            if (this.timerHandle) clearTimeout(this.timerHandle);
            this.activeInteraction = null;

            if (resolvingEffect === 'lethal_save') {
                if (responded) p.currentHealth = 1;
                else this.finalizeElimination(p, this.players.get(this.pendingEffectActorId));
                if (this.getState() === ServerGameState.GAME_OVER) return;
                this.pendingEffectType = this.effectBeforeLethal;
                this.pendingEffectActorId = this.actorBeforeLethal;
                this.effectBeforeLethal = '';
                this.actorBeforeLethal = '';
                if (this.pendingEffectType === 'indiani' || this.pendingEffectType === 'gatling') this.continueMultiTargetEffect();
                else this.returnToPlay();
                return;
            }

            if (!responded) {
                this.applyDamage(p, 1, this.pendingEffectActorId || this.currentTurnPlayerId);
                if (this.pendingEffectType === 'lethal_save') return;
            }
            if (this.getState() === ServerGameState.GAME_OVER) return;
            if (this.pendingEffectType === 'duello') {
                if (responded) this.continueDuel(true); else this.returnToPlay();
            } else if (this.pendingEffectType === 'indiani' || this.pendingEffectType === 'gatling') {
                this.continueMultiTargetEffect();
            } else {
                this.returnToPlay();
            }
        }
    }

    private resolveResponseTimeout() {
        if (this.state !== ServerGameState.RESPONSE || !this.activeInteraction) return;
        const target = this.players.get(this.activeInteraction.actorPlayerId);
        const resolvingEffect = this.pendingEffectType;
        if (target) {
            if (resolvingEffect === 'lethal_save') this.finalizeElimination(target, this.players.get(this.pendingEffectActorId));
            else this.applyDamage(target, 1, this.pendingEffectActorId || this.currentTurnPlayerId);
        }
        this.activeInteraction = null;
        if (this.getState() === ServerGameState.GAME_OVER) return;
        if (resolvingEffect === 'lethal_save') {
            this.pendingEffectType = this.effectBeforeLethal;
            this.pendingEffectActorId = this.actorBeforeLethal;
            this.effectBeforeLethal = '';
            this.actorBeforeLethal = '';
            if (this.pendingEffectType === 'indiani' || this.pendingEffectType === 'gatling') this.continueMultiTargetEffect();
            else this.returnToPlay();
            return;
        }
        if (this.pendingEffectType === 'indiani' || this.pendingEffectType === 'gatling') this.continueMultiTargetEffect();
        else this.returnToPlay();
    }

    private returnToPlay() {
        this.activeInteraction = null;
        this.pendingEffectType = '';
        this.pendingEffectActorId = '';
        this.state = ServerGameState.PLAY;
        this.currentPhase = 'PLAY';
        this.deadlineAt = Date.now() + this.rules.turnTimeSec * 1000;
        this.broadcastSnapshot();
        if (this.timerHandle) clearTimeout(this.timerHandle);
        const actor = this.players.get(this.currentTurnPlayerId);
        this.timerHandle = setTimeout(() => actor?.isBot ? this.runBotTurn(actor) : this.finishOrDiscardCurrentTurn(), actor?.isBot ? 350 : this.rules.turnTimeSec * 1000);
    }

    private handleEndTurn(socketId: string) {
        if (this.state === ServerGameState.PLAY && this.currentTurnPlayerId === socketId && this.currentPhase === 'PLAY') {
            this.finishOrDiscardCurrentTurn();
        }
    }

    private finishOrDiscardCurrentTurn() {
        if (this.timerHandle) clearTimeout(this.timerHandle);
        const p = this.players.get(this.currentTurnPlayerId);
        if (!p || !p.isAlive) { this.nextTurn(); return; }
        const excess = Math.max(0, p.hand.length - p.currentHealth);
        if (excess === 0) { this.nextTurn(); return; }
        this.state = ServerGameState.DISCARD;
        this.currentPhase = 'DISCARD';
        this.deadlineAt = Date.now() + 15000;
        this.activeInteraction = {
            interactionId: `discard_${Date.now()}_${p.id}`,
            type: 'DISCARD',
            actorPlayerId: p.id,
            title: 'BỎ BÀI DƯ',
            message: `Chọn đúng ${excess} lá để bỏ.`,
            minSelections: excess,
            maxSelections: excess,
            validPlayerIds: [],
            validCardIds: [...p.hand],
            options: [],
            canCancel: false,
            defaultAction: 'AUTO',
            expiresAt: this.deadlineAt
        };
        this.sendPrivateMessage(p.id, 'discard.required', { count: excess, deadlineAt: this.deadlineAt });
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.autoDiscardAndAdvance(), 15000);
    }

    private handleDiscardSubmit(socketId: string, cardIds: string[]) {
        if (this.state !== ServerGameState.DISCARD || socketId !== this.currentTurnPlayerId) return;
        const p = this.players.get(socketId);
        if (!p) return;
        const required = Math.max(0, p.hand.length - p.currentHealth);
        if (cardIds.length !== required || new Set(cardIds).size !== cardIds.length || cardIds.some(id => !p.hand.includes(id))) {
            this.sendPrivateMessage(socketId, 'game.action.rejected', { reason: 'INVALID_DISCARD', required, revision: this.revision });
            return;
        }
        for (const id of cardIds) {
            p.hand.splice(p.hand.indexOf(id), 1);
            this.discardPile.push(id);
        }
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.activeInteraction = null;
        this.broadcastMessage('discard.completed', { playerId: socketId, count: cardIds.length });
        this.nextTurn();
    }

    private autoDiscardAndAdvance() {
        const p = this.players.get(this.currentTurnPlayerId);
        if (p) {
            const count = Math.max(0, p.hand.length - p.currentHealth);
            for (let i = 0; i < count; i++) {
                const index = Math.floor(Math.random() * p.hand.length);
                this.discardPile.push(p.hand.splice(index, 1)[0]);
            }
        }
        this.activeInteraction = null;
        this.nextTurn();
    }

    private applyDamage(target: ServerPlayerState, amount: number, killerId?: string) {
        if (!target.isAlive || amount <= 0) return;
        target.currentHealth -= amount;
        if (target.characterId === 'bart_cassidy') this.drawCards(target, amount);
        const killer = killerId ? this.players.get(killerId) : undefined;
        if (target.characterId === 'el_gringo' && killer && killer.hand.length > 0) this.stealRandomCard(target, killer);
        if (target.currentHealth > 0) return;

        const beers = target.hand.filter(c => this.cardType(c) === 'beer');
        const needed = 1 - target.currentHealth;
        if (this.alivePlayers().length > 2 && beers.length >= needed) {
            this.effectBeforeLethal = this.pendingEffectType;
            this.actorBeforeLethal = this.pendingEffectActorId || killerId || '';
            this.openCardResponse('lethal_save', killerId || '', target.id, 'beer', needed);
            return;
        }
        this.finalizeElimination(target, killer);
    }

    private finalizeElimination(target: ServerPlayerState, killer?: ServerPlayerState) {
        const loot = [...target.hand, ...target.equipment];

        target.currentHealth = 0;
        target.isAlive = false;
        target.isRoleRevealed = true;
        const vulture = this.alivePlayers().find(p => p.characterId === 'vulture_sam' && p.id !== target.id);
        if (vulture) vulture.hand.push(...loot); else this.discardPile.push(...loot);
        target.hand = [];
        target.equipment = [];

        if (killer && target.roleId === 'outlaw') this.drawCards(killer, 3);
        if (killer && killer.roleId === 'sheriff' && target.roleId === 'deputy') {
            this.discardPile.push(...killer.hand, ...killer.equipment);
            killer.hand = [];
            killer.equipment = [];
        }

        this.broadcastMessage('player.eliminated', {
            playerId: target.id,
            roleId: target.roleId,
            killerId: killer?.id
        });
        this.checkWinCondition();
    }

    private triggerEmptyHandAbility(player: ServerPlayerState) {
        if (player.isAlive && player.characterId === 'suzy_lafayette' && player.hand.length === 0) this.drawCards(player, 1);
    }

    private handleActivateAbility(playerId: string, data: any) {
        const player = this.players.get(playerId);
        if (!player || !player.isAlive || player.characterId !== 'sid_ketchum') return;
        const cards: string[] = data.cardIds || [];
        if (cards.length !== 2 || new Set(cards).size !== 2 || cards.some(c => !player.hand.includes(c)) || player.currentHealth >= player.maxHealth) {
            this.reject(playerId, 'INVALID_ABILITY_ACTION');
            return;
        }
        for (const card of cards) {
            player.hand.splice(player.hand.indexOf(card), 1);
            this.discardPile.push(card);
        }
        this.heal(player, 1);
        this.triggerEmptyHandAbility(player);
        this.broadcastSnapshot();
    }

    private checkWinCondition(): boolean {
        const alive = Array.from(this.players.values()).filter(p => p.isAlive);
        const sheriffAlive = alive.some(p => p.roleId === 'sheriff');
        let winnerTeam: string | undefined;
        let winnerRole: string | undefined;

        if (!sheriffAlive) {
            if (alive.length === 1 && alive[0].roleId === 'renegade') {
                winnerRole = 'renegade';
                winnerTeam = 'RENEGADE';
            } else {
                winnerRole = 'outlaw';
                winnerTeam = 'OUTLAWS';
            }
        } else if (!alive.some(p => p.roleId === 'outlaw' || p.roleId === 'renegade')) {
            winnerRole = 'sheriff';
            winnerTeam = 'SHERIFF_DEPUTIES';
        }

        if (!winnerTeam) return false;
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.state = ServerGameState.GAME_OVER;
        this.winnerRole = winnerRole;
        this.winnerTeam = winnerTeam;
        this.currentPhase = 'GAME_OVER';
        this.deadlineAt = 0;
        for (const p of this.players.values()) p.isRoleRevealed = true;
        this.broadcastMessage('game.ended', { winnerType: winnerTeam, winnerRole, reason: 'WIN_CONDITION' });
        this.broadcastSnapshot();
        return true;
    }
}

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
    private disconnectedAt: Map<string, number> = new Map();
    private combatLogs: string[] = [];
    private rematchVotes: Set<string> = new Set();
    private static readonly RECONNECT_GRACE_MS = 90_000;
    private bangCardsPlayedThisTurn: number = 0;
    // Public-behaviour memory used by bots. Positive values mean a player has
    // behaved aggressively toward the Sheriff; negative values mean support.
    // Bots deliberately do not read unrevealed roleId values when targeting.
    private botSuspicion: Map<string, number> = new Map();
    private voiceRate: Map<string, { windowAt: number; count: number }> = new Map();
    private chatRate: Map<string, { windowAt: number; count: number }> = new Map();
    
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
    private pendingDrawCards: string[] = [];
    private pendingJudgementCards: string[] = [];
    private pendingJudgementKind: 'dynamite' | 'jail' | '' = '';

    private rules: RuleConfig;
    private onChanged?: () => void;
    private onGameEnded?: (players: ServerPlayerState[], winnerTeam: string) => void;

    constructor(roomId: string, wss: WebSocketServer, config: any, onChanged?: () => void, onGameEnded?: (players: ServerPlayerState[], winnerTeam: string) => void) {
        this.roomId = roomId;
        this.wss = wss;
        this.onChanged = onChanged;
        this.onGameEnded = onGameEnded;
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

    public exportState(): any {
        return {
            roomId: this.roomId, state: this.state, players: Array.from(this.players.entries()), hostId: this.hostId,
            turnNumber: this.turnNumber, sequence: this.sequence, revision: this.revision,
            currentTurnPlayerId: this.currentTurnPlayerId, currentPhase: this.currentPhase,
            activeInteraction: this.activeInteraction, winnerRole: this.winnerRole, winnerTeam: this.winnerTeam,
            deck: this.deck, discardPile: this.discardPile, phaseId: this.phaseId, deadlineAt: this.deadlineAt,
            roleSlotLocks: Array.from(this.roleSlotLocks.entries()), characterSlotLocks: Array.from(this.characterSlotLocks.entries()),
            rolePool: this.rolePool, characterPool: this.characterPool, pendingMultiTargets: this.pendingMultiTargets,
            pendingEffectType: this.pendingEffectType, pendingEffectActorId: this.pendingEffectActorId,
            generalStoreCards: this.generalStoreCards, generalStoreOrder: this.generalStoreOrder, generalStoreIndex: this.generalStoreIndex,
            duelParticipants: this.duelParticipants, duelResponderIndex: this.duelResponderIndex,
            effectBeforeLethal: this.effectBeforeLethal, actorBeforeLethal: this.actorBeforeLethal,
            judgementCard: this.judgementCard, judgementEffect: this.judgementEffect, judgementResult: this.judgementResult,
            pendingDrawCards: this.pendingDrawCards, pendingJudgementCards: this.pendingJudgementCards, pendingJudgementKind: this.pendingJudgementKind,
            rules: this.rules, combatLogs: this.combatLogs, rematchVotes: Array.from(this.rematchVotes),
            disconnectedAt: Array.from(this.disconnectedAt.entries()), bangCardsPlayedThisTurn: this.bangCardsPlayedThisTurn,
            botSuspicion: Array.from(this.botSuspicion.entries())
        };
    }

    public static restore(data: any, wss: WebSocketServer, onChanged?: () => void, onGameEnded?: (players: ServerPlayerState[], winnerTeam: string) => void): GameRoom {
        const room = new GameRoom(data.roomId, wss, data.rules || {}, onChanged, onGameEnded);
        room.state = data.state || ServerGameState.WAITING;
        room.players = new Map(data.players || []);
        room.hostId = data.hostId || '';
        room.turnNumber = data.turnNumber || 0; room.sequence = data.sequence || 0; room.revision = data.revision || 0;
        room.currentTurnPlayerId = data.currentTurnPlayerId || ''; room.currentPhase = data.currentPhase || '';
        room.activeInteraction = data.activeInteraction || null; room.winnerRole = data.winnerRole; room.winnerTeam = data.winnerTeam;
        room.deck = data.deck || []; room.discardPile = data.discardPile || []; room.phaseId = data.phaseId || ''; room.deadlineAt = data.deadlineAt || 0;
        room.roleSlotLocks = new Map(data.roleSlotLocks || []); room.characterSlotLocks = new Map(data.characterSlotLocks || []);
        room.rolePool = data.rolePool || []; room.characterPool = data.characterPool || []; room.pendingMultiTargets = data.pendingMultiTargets || [];
        room.pendingEffectType = data.pendingEffectType || ''; room.pendingEffectActorId = data.pendingEffectActorId || '';
        room.generalStoreCards = data.generalStoreCards || []; room.generalStoreOrder = data.generalStoreOrder || []; room.generalStoreIndex = data.generalStoreIndex || 0;
        room.duelParticipants = data.duelParticipants || []; room.duelResponderIndex = data.duelResponderIndex || 0;
        room.effectBeforeLethal = data.effectBeforeLethal || ''; room.actorBeforeLethal = data.actorBeforeLethal || '';
        room.judgementCard = data.judgementCard || ''; room.judgementEffect = data.judgementEffect || ''; room.judgementResult = data.judgementResult || '';
        room.pendingDrawCards = data.pendingDrawCards || []; room.pendingJudgementCards = data.pendingJudgementCards || []; room.pendingJudgementKind = data.pendingJudgementKind || '';
        room.combatLogs = data.combatLogs || []; room.rematchVotes = new Set(data.rematchVotes || []);
        room.disconnectedAt = new Map(data.disconnectedAt || []); room.bangCardsPlayedThisTurn = data.bangCardsPlayedThisTurn || 0;
        room.botSuspicion = new Map(data.botSuspicion || []);
        for (const player of room.players.values()) player.isConnected = false;
        room.rearmRestoredTimer();
        return room;
    }

    private rearmRestoredTimer() {
        if ([ServerGameState.WAITING, ServerGameState.GAME_OVER].includes(this.state)) return;
        const delay = Math.max(50, this.deadlineAt - Date.now());
        if (this.state === ServerGameState.ROLE_DRAFT) this.timerHandle = setTimeout(() => this.handleRoleDraftTimeout(), delay);
        else if (this.state === ServerGameState.ROLE_LOCK_WAIT) this.timerHandle = setTimeout(() => this.startCharacterDraft(), delay);
        else if (this.state === ServerGameState.CHARACTER_DRAFT) this.timerHandle = setTimeout(() => this.handleCharacterDraftTimeout(), delay);
        else if (this.state === ServerGameState.RESPONSE && this.currentPhase === 'ABILITY_DRAW')
            this.timerHandle = setTimeout(() => this.resolveDrawAbility(this.currentTurnPlayerId, { action: 'AUTO' }), delay);
        else if (this.state === ServerGameState.RESPONSE && this.currentPhase === 'JUDGEMENT_CHOICE')
            this.timerHandle = setTimeout(() => this.resolveLuckyJudgement(this.currentTurnPlayerId, []), delay);
        else if (this.state === ServerGameState.RESPONSE) this.timerHandle = setTimeout(() => this.resolveResponseTimeout(), delay);
        else if (this.state === ServerGameState.DISCARD) this.timerHandle = setTimeout(() => this.autoDiscardAndAdvance(), delay);
        else this.timerHandle = setTimeout(() => this.startTurn(this.currentTurnPlayerId || this.hostId), delay);
    }

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
        this.disconnectedAt.delete(playerId);
        this.sockets.set(playerId, ws);
        (ws as any).id = playerId;
        this.sendSnapshotTo(playerId);
        return true;
    }

    public handleMessage(ws: WebSocket, type: string, data: any) {
        const socketId = (ws as any).id;
        data = data || {};

        if (data.stateRevision !== undefined && data.stateRevision !== this.revision) {
            this.sendPrivateMessage(socketId, 'game.action.rejected', { reason: 'STALE_STATE', revision: this.revision });
            this.sendSnapshotTo(socketId);
            return;
        }

        // Only consume an action id after its revision has been accepted. A stale
        // client must be able to resync and retry instead of being rejected as a
        // duplicate on the second attempt.
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
                const count = this.players.size;
                const allReady = Array.from(this.players.values()).every(p => p.isReady);
                if (!allReady) {
                    ws.send(JSON.stringify({ type: 'game.error', data: JSON.stringify('Tất cả người chơi phải sẵn sàng trước khi bắt đầu.') }));
                } else if (count < 4) {
                    ws.send(JSON.stringify({ type: 'game.error', data: JSON.stringify('Cần ít nhất 4 người chơi để bắt đầu trận.') }));
                } else if (count > 8) {
                    ws.send(JSON.stringify({ type: 'game.error', data: JSON.stringify('Tối đa 8 người chơi mỗi trận.') }));
                } else {
                    this.startGame();
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
        else if (type === 'game.action.draw') {
            this.handleRequestDraw(socketId);
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
        else if (type === 'room.rematch') {
            this.handleRematchVote(socketId);
        }
        else if (type === 'chat.send') {
            const player = this.players.get(socketId);
            const message = String(data.message || '').trim().slice(0, 240);
            const now = Date.now();
            const rate = this.chatRate.get(socketId) || { windowAt: now, count: 0 };
            if (now - rate.windowAt >= 5000) { rate.windowAt = now; rate.count = 0; }
            rate.count++;
            this.chatRate.set(socketId, rate);
            if (player && message && rate.count <= 5) this.broadcastMessage('chat.message', { playerId: player.id, playerName: player.name, message, sentAt: now });
        }
        else if (type === 'voice.signal') {
            const targetId = String(data.targetPlayerId || '');
            if (this.players.has(targetId)) this.sendPrivateMessage(targetId, 'voice.signal', { fromPlayerId: socketId, signal: data.signal });
        }
        else if (type === 'voice.frame') {
            const player = this.players.get(socketId);
            const payload = String(data.payload || '');
            const level = Math.max(0, Math.min(1, Number(data.level) || 0));
            const now = Date.now();
            const rate = this.voiceRate.get(socketId) || { windowAt: now, count: 0 };
            if (now - rate.windowAt >= 1000) { rate.windowAt = now; rate.count = 0; }
            rate.count++;
            this.voiceRate.set(socketId, rate);
            // 20 ms mono PCM16 packets are ~856 base64 chars. Keep a hard cap
            // so voice cannot be abused to inflate websocket traffic.
            if (rate.count <= 55 && player && player.isConnected && payload.length > 0 && payload.length <= 2048) {
                for (const peer of this.alivePlayers()) {
                    if (peer.id !== socketId && !peer.isBot && peer.isConnected)
                        this.sendPrivateMessage(peer.id, 'voice.frame', { fromPlayerId: socketId, payload, level });
                }
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
            } else {
                this.disconnectedAt.set(socketId, Date.now());
            }
            this.broadcastSnapshot();
        }
    }

    public hasPlayer(socketId: string): boolean {
        return this.players.has(socketId);
    }

    public isEmpty(): boolean {
        return this.players.size === 0;
    }

    public pruneExpiredDisconnected(now = Date.now()) {
        for (const [playerId, disconnectedAt] of this.disconnectedAt.entries()) {
            if (now - disconnectedAt < GameRoom.RECONNECT_GRACE_MS) continue;
            this.disconnectedAt.delete(playerId);
            const player = this.players.get(playerId);
            if (!player || player.isConnected) continue;
            if (this.state === ServerGameState.GAME_OVER) this.players.delete(playerId);
        }
    }

    private reseatPlayers() {
        Array.from(this.players.values()).sort((a, b) => a.seat - b.seat).forEach((p, i) => p.seat = i);
    }

    // --- SNAPSHOT GENERATION ---
    public getSnapshotFor(targetSocketId: string): MatchStateSnapshotDTO {
        const targetPlayer = this.players.get(targetSocketId);
        const publicPlayers: PlayerSnapshotDTO[] = Array.from(this.players.values()).map(p => {
            // Role sanitization: a player can only see:
            //   - their own role (via privateState, not here)
            //   - SHERIFF (always revealed)
            //   - any role that was explicitly revealed (e.g. on death)
            // All others must be hidden from the snapshot to prevent client-side cheating.
            const showRole = p.isRoleRevealed;
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
                publicRoleId: showRole ? p.roleId : undefined,
                hiddenRole: !showRole ? 'hidden' : undefined,  // tells client to show 'VAI TRÒ BÍ MẬT'
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
                roleId: targetPlayer.roleId,    // only the owner receives their real role here
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
            combatLogs: [...this.combatLogs],
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
        this.onChanged?.();
    }

    private broadcastMessage(type: string, data: any) {
        const payload = JSON.stringify({ type, data: JSON.stringify(data) });
        for (const socket of this.sockets.values()) {
            if (socket.readyState === WebSocket.OPEN) {
                socket.send(payload);
            }
        }
    }

    private addCombatLog(message: string) {
        this.combatLogs.push(message);
        if (this.combatLogs.length > 40) this.combatLogs.splice(0, this.combatLogs.length - 40);
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
        this.rematchVotes.clear();
        this.combatLogs = [];
        this.deck = [];
        this.discardPile = [];
        this.activeInteraction = null;
        this.winnerRole = undefined;
        this.winnerTeam = undefined;
        this.turnNumber = 0;
        this.currentTurnPlayerId = '';
        this.currentPhase = '';
        this.phaseId = uuidv4();
        console.log(`[ROOM ${this.roomId}] Starting game — ${this.players.size} players`);
        this.addCombatLog('Trận đấu mới bắt đầu. Đang phân vai trò.');

        // ── ROLE POOL by player count ────────────────────────────────────
        const numPlayers = this.players.size;
        const clampedCount = Math.max(4, Math.min(8, numPlayers));
        const rolePools: Record<number, string[]> = {
            4: ['sheriff', 'outlaw', 'outlaw', 'renegade'],
            5: ['sheriff', 'deputy', 'outlaw', 'outlaw', 'renegade'],
            6: ['sheriff', 'deputy', 'outlaw', 'outlaw', 'outlaw', 'renegade'],
            7: ['sheriff', 'deputy', 'deputy', 'outlaw', 'outlaw', 'outlaw', 'renegade'],
            8: ['sheriff', 'deputy', 'deputy', 'outlaw', 'outlaw', 'outlaw', 'renegade', 'renegade']
        };
        this.rolePool = [...rolePools[clampedCount]];

        // ── SHUFFLE role pool (Fisher-Yates) ─────────────────────────────
        for (let i = this.rolePool.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.rolePool[i], this.rolePool[j]] = [this.rolePool[j], this.rolePool[i]];
        }

        // ── ASSIGN roles — server decides, players do NOT choose ─────────
        // Reset all players then distribute one role per player by seat order.
        this.roleSlotLocks.clear();
        const playersBySeat = Array.from(this.players.values()).sort((a, b) => a.seat - b.seat);
        for (const player of playersBySeat) {
            player.draftRoleSlot = undefined;
            player.draftCharacterSlot1 = undefined;
            player.draftCharacterSlot2 = undefined;
            player.draftCharacterOptions = undefined;
            player.characterId = undefined;
            player.isRoleRevealed = false;
        }
        for (let i = 0; i < playersBySeat.length; i++) {
            const player = playersBySeat[i];
            player.roleId = this.rolePool[i];
            player.draftRoleSlot = i;
            this.roleSlotLocks.set(i, player.id);
            // Notify each player privately of their own role
            if (!player.isBot) {
                this.sendPrivateMessage(player.id, 'draft.role.assigned', { roleId: player.roleId });
            }
        }

        // ── REVEAL SHERIFF publicly ───────────────────────────────────────
        let sheriffId = '';
        for (const player of this.players.values()) {
            if (player.roleId === 'sheriff') {
                player.isRoleRevealed = true;
                sheriffId = player.id;
            }
        }

        this.broadcastMessage('draft.role.complete', { sheriffPlayerId: sheriffId, transitionAt: Date.now() + 3000 });
        this.state = ServerGameState.ROLE_LOCK_WAIT;
        this.deadlineAt = Date.now() + 3000;
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.startCharacterDraft(), 3000);
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
            bot.characterId = this.chooseBotCharacter(bot, bot.draftCharacterOptions!);
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
        const player = this.players.get(playerId);
        if (player) this.addCombatLog(`Lượt ${this.turnNumber + 1}: ${player.name}.`);
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
            if (this.beginLuckyJudgement(player, 'dynamite')) return;
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
            if (this.beginLuckyJudgement(player, 'jail')) return;
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
        const count = player.characterId === 'lucky_duke' && player.isBot ? 2 : 1;
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

    private beginLuckyJudgement(player: ServerPlayerState, kind: 'dynamite' | 'jail'): boolean {
        if (player.characterId !== 'lucky_duke' || player.isBot) return false;
        this.ensureDeck(2);
        this.pendingJudgementCards = [this.deck.pop(), this.deck.pop()].filter(Boolean) as string[];
        this.pendingJudgementKind = kind;
        this.state = ServerGameState.RESPONSE;
        this.currentPhase = 'JUDGEMENT_CHOICE';
        this.deadlineAt = Date.now() + this.rules.responseTimeSec * 1000;
        this.activeInteraction = {
            interactionId: `judgement_${kind}_${Date.now()}`, type: 'SELECT_CARDS', actorPlayerId: player.id,
            title: 'LUCKY DUKE', message: 'Chọn 1 trong 2 lá phán xét.', minSelections: 1, maxSelections: 1,
            validPlayerIds: [], validCardIds: [...this.pendingJudgementCards], options: [], canCancel: false,
            defaultAction: 'AUTO', expiresAt: this.deadlineAt
        };
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.resolveLuckyJudgement(player.id, []), this.rules.responseTimeSec * 1000);
        return true;
    }

    private resolveLuckyJudgement(playerId: string, selected: string[]) {
        const player = this.players.get(playerId);
        if (!player || this.currentPhase !== 'JUDGEMENT_CHOICE') return;
        if (this.timerHandle) clearTimeout(this.timerHandle);
        const chosen = selected.length === 1 && this.pendingJudgementCards.includes(selected[0]) ? selected[0] : this.pendingJudgementCards[0];
        this.discardPile.push(...this.pendingJudgementCards);
        const kind = this.pendingJudgementKind;
        this.pendingJudgementCards = []; this.pendingJudgementKind = ''; this.activeInteraction = null;
        this.state = ServerGameState.JUDGEMENT; this.currentPhase = 'JUDGEMENT';
        if (kind === 'dynamite') {
            const dynamite = player.equipment.find(c => this.cardType(c) === 'dynamite');
            if (dynamite) this.applyChosenDynamite(player, dynamite, chosen);
            if (this.getState() === ServerGameState.GAME_OVER || this.getState() === ServerGameState.RESPONSE) return;
            const jail = player.equipment.find(c => this.cardType(c) === 'jail');
            if (jail) {
                if (this.beginLuckyJudgement(player, 'jail')) return;
            }
            this.timerHandle = setTimeout(() => this.startDrawPhase(), 900);
        } else {
            const jail = player.equipment.find(c => this.cardType(c) === 'jail');
            if (!jail || this.applyChosenJail(player, jail, chosen)) this.timerHandle = setTimeout(() => this.startDrawPhase(), 900);
        }
    }

    private applyChosenDynamite(player: ServerPlayerState, dynamite: string, card: string) {
        const safe = !(this.cardSuit(card) === 'spades' && this.isRankBetween(card, 2, 9));
        this.judgementCard = card; this.judgementEffect = 'DYNAMITE'; this.judgementResult = safe ? 'CHUYỂN TIẾP' : 'PHÁT NỔ: -3 HP';
        this.broadcastMessage('judgement.cardRevealed', { playerId: player.id, effect: 'dynamite', card });
        if (safe) this.passDynamite(player, dynamite);
        else { player.equipment.splice(player.equipment.indexOf(dynamite), 1); this.discardPile.push(dynamite); this.applyDamage(player, 3); }
        this.broadcastSnapshot();
    }

    private applyChosenJail(player: ServerPlayerState, jail: string, card: string): boolean {
        const escaped = this.cardSuit(card) === 'hearts';
        this.judgementCard = card; this.judgementEffect = 'JAIL'; this.judgementResult = escaped ? 'THOÁT TÙ' : 'MẤT LƯỢT';
        player.equipment.splice(player.equipment.indexOf(jail), 1); this.discardPile.push(jail);
        this.broadcastMessage('judgement.cardRevealed', { playerId: player.id, effect: 'jail', card });
        this.broadcastSnapshot();
        if (!escaped) { this.timerHandle = setTimeout(() => this.nextTurn(), 900); return false; }
        return true;
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
            if (!p.isBot && ['pedro_ramirez', 'jesse_jones', 'kit_carlson'].includes(p.characterId || '')) {
                this.openDrawAbilityChoice(p);
                return;
            }
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
                if (p.isBot) {
                    const before = p.hand.length;
                    this.drawCards(p, 2);
                    if (p.characterId === 'black_jack') {
                        const second = p.hand[before + 1];
                        if (second && ['hearts','diamonds'].includes(this.cardSuit(second))) this.drawCards(p, 1);
                    }
                }
            }
        }
        this.broadcastSnapshot();
        
        if (p?.isBot) {
            this.timerHandle = setTimeout(() => this.startPlayPhase(), 5000);
        } else if (p && !['pedro_ramirez', 'jesse_jones', 'kit_carlson'].includes(p.characterId || '')) {
            this.deadlineAt = Date.now() + this.rules.turnTimeSec * 1000;
            this.timerHandle = setTimeout(() => this.handleEndTurn(this.currentTurnPlayerId), this.rules.turnTimeSec * 1000);
        }
    }

    private handleRequestDraw(socketId: string) {
        if (this.state !== ServerGameState.DRAW || this.currentTurnPlayerId !== socketId || this.currentPhase !== 'DRAW') return;
        const p = this.players.get(socketId);
        if (!p) return;
        
        if (this.timerHandle) clearTimeout(this.timerHandle);
        
        const before = p.hand.length;
        this.drawCards(p, 2);
        if (p.characterId === 'black_jack') {
            const second = p.hand[before + 1];
            if (second && ['hearts','diamonds'].includes(this.cardSuit(second))) this.drawCards(p, 1);
        }
        
        this.startPlayPhase();
    }

    private openDrawAbilityChoice(player: ServerPlayerState) {
        this.state = ServerGameState.RESPONSE;
        this.currentPhase = 'ABILITY_DRAW';
        this.deadlineAt = Date.now() + this.rules.responseTimeSec * 1000;
        if (player.characterId === 'kit_carlson') {
            this.ensureDeck(3);
            this.pendingDrawCards = [this.deck.pop(), this.deck.pop(), this.deck.pop()].filter(Boolean) as string[];
            this.activeInteraction = {
                interactionId: `draw_kit_${Date.now()}`, type: 'SELECT_CARDS', actorPlayerId: player.id,
                title: 'KIT CARLSON', message: 'Chọn 2 trong 3 lá. Lá còn lại được đặt lại lên bộ bài.',
                minSelections: Math.min(2, this.pendingDrawCards.length), maxSelections: Math.min(2, this.pendingDrawCards.length),
                validPlayerIds: [], validCardIds: [...this.pendingDrawCards], options: [], canCancel: false,
                defaultAction: 'AUTO', expiresAt: this.deadlineAt
            };
        } else if (player.characterId === 'jesse_jones') {
            this.activeInteraction = {
                interactionId: `draw_jesse_${Date.now()}`, type: 'SELECT_TARGET', actorPlayerId: player.id,
                title: 'JESSE JONES', message: 'Chọn người để lấy ngẫu nhiên 1 lá, hoặc rút 2 lá từ bộ bài.',
                minSelections: 0, maxSelections: 1,
                validPlayerIds: this.alivePlayers().filter(p => p.id !== player.id && p.hand.length > 0).map(p => p.id),
                validCardIds: [], options: ['DRAW_DECK'], canCancel: true, defaultAction: 'AUTO', expiresAt: this.deadlineAt
            };
        } else {
            this.activeInteraction = {
                interactionId: `draw_pedro_${Date.now()}`, type: 'CHOOSE_OPTION', actorPlayerId: player.id,
                title: 'PEDRO RAMIREZ', message: 'Chọn nguồn cho lá rút đầu tiên.', minSelections: 0, maxSelections: 0,
                validPlayerIds: [], validCardIds: [], options: this.discardPile.length ? ['DRAW_DECK', 'TAKE_DISCARD'] : ['DRAW_DECK'],
                canCancel: false, defaultAction: 'AUTO', expiresAt: this.deadlineAt
            };
        }
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.resolveDrawAbility(player.id, { action: 'AUTO' }), this.rules.responseTimeSec * 1000);
    }

    private resolveDrawAbility(playerId: string, data: any) {
        const player = this.players.get(playerId);
        if (!player || this.currentPhase !== 'ABILITY_DRAW' || this.activeInteraction?.actorPlayerId !== playerId) return;
        if (this.timerHandle) clearTimeout(this.timerHandle);
        if (player.characterId === 'kit_carlson') {
            let selected: string[] = data.selectedCardIds || [];
            if (selected.length !== Math.min(2, this.pendingDrawCards.length) || selected.some(card => !this.pendingDrawCards.includes(card))) {
                selected = this.pendingDrawCards.slice(0, 2);
            }
            player.hand.push(...selected);
            for (const card of this.pendingDrawCards.filter(card => !selected.includes(card))) this.deck.push(card);
            this.pendingDrawCards = [];
        } else if (player.characterId === 'jesse_jones') {
            const target = this.players.get((data.targetPlayerIds || [])[0]);
            if (target && target.id !== player.id && target.hand.length > 0) {
                this.stealRandomCard(player, target);
                this.drawCards(player, 1);
            } else this.drawCards(player, 2);
        } else {
            if (data.optionIndex === 1 && this.discardPile.length > 0) player.hand.push(this.discardPile.pop()!);
            else this.drawCards(player, 1);
            this.drawCards(player, 1);
        }
        this.activeInteraction = null;
        this.state = ServerGameState.DRAW;
        this.currentPhase = 'DRAW';
        this.broadcastSnapshot();
        this.timerHandle = setTimeout(() => this.startPlayPhase(), 450);
    }

    private startPlayPhase() {
        this.state = ServerGameState.PLAY;
        this.currentPhase = "PLAY";
        this.deadlineAt = Date.now() + (this.rules.turnTimeSec * 1000);
        this.broadcastSnapshot();
        const player = this.players.get(this.currentTurnPlayerId);
        this.timerHandle = setTimeout(() => player?.isBot ? this.runBotTurn(player) : this.finishOrDiscardCurrentTurn(), player?.isBot ? 5000 : this.rules.turnTimeSec * 1000);
    }

    private runBotTurn(bot: ServerPlayerState) {
        if (this.state !== ServerGameState.PLAY || this.currentTurnPlayerId !== bot.id) return;

        if (bot.characterId === 'sid_ketchum' && bot.currentHealth <= Math.max(2, bot.maxHealth - 1) && bot.hand.length >= 3) {
            const expendable = [...bot.hand]
                .sort((a, b) => this.botCardKeepValue(bot, a) - this.botCardKeepValue(bot, b))
                .slice(0, 2);
            this.handleActivateAbility(bot.id, { cardIds: expendable });
            if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
            return;
        }

        const beer = bot.hand.find(c => this.cardType(c) === 'beer');
        if (beer && bot.currentHealth < bot.maxHealth && this.alivePlayers().length > 2 && (bot.currentHealth <= 2 || bot.hand.length > bot.currentHealth)) {
            const before = bot.hand.length;
            this.handlePlayCard(bot.id, { cardId: beer, targetPlayerIds: [] });
            if (bot.hand.length < before) {
                if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                return;
            }
        }

        const equipmentTypes = new Set(['volcanic', 'gun_range_2', 'gun_range_3', 'gun_range_4', 'gun_range_5', 'appaloosa', 'mustang', 'barrel', 'dynamite']);
        let equipment = bot.hand
            .filter(c => equipmentTypes.has(this.cardType(c)))
            .sort((a, b) => this.botEquipmentValue(bot, b) - this.botEquipmentValue(bot, a))[0];
        if (equipment) {
            const before = bot.hand.length;
            this.handlePlayCard(bot.id, { cardId: equipment, targetPlayerIds: [] });
            if (bot.hand.length < before) {
                if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                return;
            }
        }

        const target = this.chooseBotTarget(bot);
        const disrupt = bot.hand.find(c => ['jail', 'panico', 'cat_balou', 'duello'].includes(this.cardType(c)));
        if (disrupt && target) {
            const type = this.cardType(disrupt);
            const valid = type !== 'jail' || (target.roleId !== 'sheriff' && !target.equipment.some(c => this.cardType(c) === 'jail'));
            const inRange = type !== 'panico' || this.calculateDistance(bot, target) <= 1;
            if (valid && inRange) {
                const before = bot.hand.length;
                this.handlePlayCard(bot.id, { cardId: disrupt, targetPlayerIds: [target.id] });
                if (bot.hand.length < before) {
                    if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                    return;
                }
            }
        }

        const drawAction = bot.hand.find(c => ['dilizenza', 'wells_fargo'].includes(this.cardType(c)));
        if (drawAction) {
            const before = bot.hand.length;
            this.handlePlayCard(bot.id, { cardId: drawAction, targetPlayerIds: [] });
            if (bot.hand.length < before) {
                if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                return;
            }
        }

        const bang = bot.hand.find(c => this.cardType(c) === 'bang');
        if (bang && target) {
            const unlimited = bot.equipment.some(e => this.cardType(e) === 'volcanic') || bot.characterId === 'willy_the_kid';
            if (unlimited || this.bangCardsPlayedThisTurn === 0) {
                const before = bot.hand.length;
                this.handlePlayCard(bot.id, { cardId: bang, targetPlayerIds: [target.id] });
                if (bot.hand.length < before) {
                    if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                    return;
                }
            }
        }

        const globalAction = bot.hand.find(c => this.shouldBotPlayGlobal(bot, this.cardType(c)));
        if (globalAction) {
            const before = bot.hand.length;
            this.handlePlayCard(bot.id, { cardId: globalAction, targetPlayerIds: [] });
            if (bot.hand.length < before) {
                if (this.state === ServerGameState.PLAY) this.timerHandle = setTimeout(() => this.runBotTurn(bot), 5000);
                return;
            }
        }

        if (this.state === ServerGameState.PLAY) this.finishOrDiscardCurrentTurn();
    }

    private chooseBotTarget(bot: ServerPlayerState): ServerPlayerState | undefined {
        const candidates = this.alivePlayers().filter(player => player.id !== bot.id && this.isTargetable(bot, player));
        const score = (target: ServerPlayerState) => {
            let value = (target.maxHealth - target.currentHealth) * 3 + (target.hand.length * 0.35);
            const suspicion = this.botSuspicion.get(target.id) || 0;
            if (bot.roleId === 'outlaw') value += target.roleId === 'sheriff' ? 100 : 0;
            else if (bot.roleId === 'sheriff' || bot.roleId === 'deputy') {
                value += suspicion * 8;
                if (target.isRoleRevealed && target.roleId === 'outlaw') value += 100;
                if (target.isRoleRevealed && target.roleId === 'deputy') value -= 100;
                if (target.roleId === 'sheriff') value -= 1000;
            } else if (bot.roleId === 'renegade') {
                const alive = this.alivePlayers();
                if (alive.length === 2 && target.roleId === 'sheriff') value += 100;
                else if (target.roleId === 'sheriff') value -= 60 + Math.max(0, 3 - target.currentHealth) * 20;
                else value += 20 + suspicion * 2;
            }
            if (target.currentHealth === 1) value += 12;
            value += Math.random() * 2.5;
            return value;
        };
        return candidates.sort((a, b) => score(b) - score(a))[0];
    }

    private chooseBotCharacter(bot: ServerPlayerState, options: string[]): string {
        const score = (id: string) => {
            let value = 0;
            if (['jourdonnais', 'paul_regret', 'bart_cassidy'].includes(id)) value += bot.roleId === 'sheriff' ? 9 : 5;
            if (['willy_the_kid', 'slab_the_killer', 'calamity_janet'].includes(id)) value += bot.roleId === 'outlaw' ? 9 : 4;
            if (['kit_carlson', 'black_jack', 'jesse_jones'].includes(id)) value += 6;
            if (['sid_ketchum', 'suzy_lafayette'].includes(id)) value += bot.roleId === 'renegade' ? 8 : 5;
            return value + Math.random() * 2;
        };
        return [...options].sort((a, b) => score(b) - score(a))[0];
    }

    private botCardKeepValue(bot: ServerPlayerState, card: string): number {
        const type = this.cardType(card);
        if (type === 'beer') return bot.currentHealth <= 2 ? 100 : 35;
        if (type === 'dodge') return 80;
        if (type === 'bang') return bot.roleId === 'outlaw' ? 65 : 45;
        if (['barrel', 'mustang'].includes(type)) return 60;
        if (['wells_fargo', 'dilizenza'].includes(type)) return 55;
        return 20;
    }

    private botEquipmentValue(bot: ServerPlayerState, card: string): number {
        const type = this.cardType(card);
        if (type === 'barrel') return bot.currentHealth <= 2 ? 100 : 75;
        if (type === 'mustang') return bot.roleId === 'sheriff' ? 90 : 65;
        if (type === 'volcanic') return bot.hand.filter(c => this.cardType(c) === 'bang').length * 25 + 45;
        if (type === 'dynamite') return bot.currentHealth <= 2 ? 5 : 30;
        const ranges: Record<string, number> = { gun_range_2: 52, gun_range_3: 58, gun_range_4: 64, gun_range_5: 70, appaloosa: 62 };
        return ranges[type] || 25;
    }

    private shouldBotPlayGlobal(bot: ServerPlayerState, type: string): boolean {
        if (type === 'general_store') return true;
        if (type === 'saloon') return bot.currentHealth < bot.maxHealth && this.alivePlayers().filter(p => p.currentHealth < p.maxHealth).length <= 2;
        if (!['indiani', 'gatling'].includes(type)) return false;
        const sheriff = this.alivePlayers().find(p => p.roleId === 'sheriff');
        if (bot.roleId === 'outlaw') return !!sheriff && sheriff.currentHealth <= 2;
        if (bot.roleId === 'sheriff' || bot.roleId === 'deputy') return this.alivePlayers().some(p => p.id !== bot.id && (this.botSuspicion.get(p.id) || 0) >= 2);
        return this.alivePlayers().length <= 3;
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
        if ((type === 'panico' || type === 'cat_balou') && target && target.hand.length === 0 && target.equipment.length === 0) {
            this.reject(socketId, 'TARGET_HAS_NO_CARDS'); return;
        }
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

        this.observePublicAction(p, target, type);

        p.hand.splice(idx, 1);
        this.addCombatLog(`${p.name} dùng ${type}${target ? ` lên ${target.name}` : ''}.`);
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

    private observePublicAction(actor: ServerPlayerState, target: ServerPlayerState | undefined, type: string) {
        if (target && ['bang', 'duello', 'jail', 'cat_balou', 'panico'].includes(type)) {
            let delta = 0;
            if (target.roleId === 'sheriff') delta = type === 'bang' || type === 'duello' ? 2 : 1;
            else if (target.isRoleRevealed && target.roleId === 'outlaw') delta = -1.25;
            if (delta !== 0) this.botSuspicion.set(actor.id, Math.max(-5, Math.min(8, (this.botSuspicion.get(actor.id) || 0) + delta)));
        }
        if (type === 'saloon') {
            const sheriff = this.alivePlayers().find(player => player.roleId === 'sheriff');
            if (sheriff && sheriff.currentHealth < sheriff.maxHealth) {
                this.botSuspicion.set(actor.id, Math.max(-5, (this.botSuspicion.get(actor.id) || 0) - 0.5));
            }
        }
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
        const required = actor.characterId === 'slab_the_killer' ? 2 : 1;
        const barrelAttempts = (target.equipment.some(c => this.cardType(c) === 'barrel') ? 1 : 0) +
            (target.characterId === 'jourdonnais' ? 1 : 0);
        let automaticDodges = 0;
        for (let attempt = 0; attempt < barrelAttempts && automaticDodges < required; attempt++) {
            const judgement = this.drawJudgement(target, card => this.cardSuit(card) === 'hearts');
            this.broadcastMessage('judgement.cardRevealed', { playerId: target.id, effect: 'barrel', card: judgement.card });
            if (judgement.matched) automaticDodges++;
        }
        if (automaticDodges >= required) {
            this.broadcastSnapshot();
            return;
        }
        this.openCardResponse('bang', actor.id, target.id, 'dodge', required - automaticDodges);
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
        if (target?.isBot) this.timerHandle = setTimeout(() => this.resolveBotResponse(target), 5000);
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
        }, picker?.isBot ? 5000 : this.rules.responseTimeSec * 1000);
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
        if (this.currentPhase === 'JUDGEMENT_CHOICE' && this.activeInteraction?.actorPlayerId === socketId) {
            this.resolveLuckyJudgement(socketId, data.selectedCardIds || []);
            return;
        }
        if (this.currentPhase === 'ABILITY_DRAW' && this.activeInteraction?.actorPlayerId === socketId) {
            this.resolveDrawAbility(socketId, data);
            return;
        }
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
        this.timerHandle = setTimeout(() => actor?.isBot ? this.runBotTurn(actor) : this.finishOrDiscardCurrentTurn(), actor?.isBot ? 5000 : this.rules.turnTimeSec * 1000);
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
        this.addCombatLog(`${target.name} bị loại${killer ? ` bởi ${killer.name}` : ''}.`);

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
        if (this.state !== ServerGameState.PLAY || this.currentPhase !== 'PLAY' || this.currentTurnPlayerId !== playerId) {
            this.reject(playerId, 'ABILITY_NOT_AVAILABLE');
            return;
        }
        const cards: string[] = data.cardIds || data.selectedCardIds || [];
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
        const outlawAlive = alive.some(p => p.roleId === 'outlaw');
        const renegadeAlive = alive.some(p => p.roleId === 'renegade');

        let winnerTeam: string | undefined;
        let winnerRole: string | undefined;
        let winnerPlayerId: string | undefined;

        // CASE 1: Solo Renegade victory — must be the only surviving player.
        // Works for both 1-renegade AND 2-renegade (8-player) games.
        // In 8-player games the two Renegades compete individually;
        // only the last one standing can claim victory — NOT as a shared team win.
        if (alive.length === 1 && alive[0].roleId === 'renegade') {
            winnerRole = 'renegade';
            winnerTeam = 'RENEGADE';
            winnerPlayerId = alive[0].id;
        }
        // CASE 2: Sheriff + Deputies win — Sheriff alive and all threats eliminated.
        else if (sheriffAlive && !outlawAlive && !renegadeAlive) {
            winnerRole = 'sheriff';
            winnerTeam = 'SHERIFF_DEPUTIES';
        }
        // CASE 3: Outlaw victory — Sheriff is dead and it is NOT a solo Renegade win.
        else if (!sheriffAlive) {
            winnerRole = 'outlaw';
            winnerTeam = 'OUTLAWS';
        }

        if (!winnerTeam) return false;
        if (this.timerHandle) clearTimeout(this.timerHandle);
        this.state = ServerGameState.GAME_OVER;
        this.winnerRole = winnerRole;
        this.winnerTeam = winnerTeam;
        this.addCombatLog(`Kết thúc trận: ${winnerTeam} chiến thắng.`);
        this.currentPhase = 'GAME_OVER';
        this.deadlineAt = 0;
        for (const p of this.players.values()) p.isRoleRevealed = true;
        this.broadcastMessage('game.ended', { winnerType: winnerTeam, winnerRole, winnerPlayerId, reason: 'WIN_CONDITION' });
        this.onGameEnded?.(Array.from(this.players.values()), winnerTeam);
        this.broadcastSnapshot();
        return true;
    }

    private handleRematchVote(playerId: string) {
        if (this.state !== ServerGameState.GAME_OVER) return;
        const player = this.players.get(playerId);
        if (!player || player.isBot) return;
        this.rematchVotes.add(playerId);
        const connectedHumans = Array.from(this.players.values()).filter(p => !p.isBot && p.isConnected);
        this.broadcastMessage('room.rematchVoteUpdated', { votes: this.rematchVotes.size, required: connectedHumans.length });
        if (connectedHumans.length > 0 && connectedHumans.every(p => this.rematchVotes.has(p.id))) {
            for (const p of this.players.values()) {
                p.isAlive = true;
                p.currentHealth = 4;
                p.maxHealth = 4;
                p.hand = [];
                p.equipment = [];
                p.isReady = true;
                p.bangCardsPlayedThisTurn = 0;
            }
            this.startGame();
        }
    }
}

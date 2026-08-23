export enum ServerGameState {
    LOBBY = 'LOBBY',
    WAITING = 'WAITING',
    ROLE_DRAFT = 'ROLE_DRAFT',
    ROLE_LOCK_WAIT = 'ROLE_LOCK_WAIT',
    CHARACTER_DRAFT = 'CHARACTER_DRAFT',
    CHARACTER_REVEAL = 'CHARACTER_REVEAL',
    INITIAL_DEAL = 'INITIAL_DEAL',
    TURN_START = 'TURN_START',
    JUDGEMENT = 'JUDGEMENT',
    DRAW = 'DRAW',
    PLAY = 'PLAY',
    RESPONSE = 'RESPONSE',
    DISCARD = 'DISCARD',
    GAME_OVER = 'GAME_OVER'
}

export interface PlayerSnapshotDTO {
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
    characterId?: string; // public after reveal
    publicRoleId?: string; // only Sheriff until death/reveal
    isRoleRevealed: boolean;
    handCount: number;
    equipment: string[];
    effectiveDistanceToLocal: number;
    isTargetable: boolean;
}

export interface PrivatePlayerState {
    roleId?: string;
    hand: string[];
    draftCharacterOptions?: string[];
}

export interface InteractionPromptDTO {
    interactionId: string;
    type: string;
    actorPlayerId: string;
    title: string;
    message: string;
    minSelections: number;
    maxSelections: number;
    requiredCount?: number;
    requiredCardType?: string;
    validPlayerIds: string[];
    validCardIds: string[];
    options: string[];
    expiresAt: number;
    canCancel: boolean;
    defaultAction?: string;
}

export interface RuleConfig {
    maxPlayers: number;
    botCount: number;
    turnTimeSec: number;
    startingHandMode: 'FIXED_7' | 'BY_HP';
    roleDraftSec: number;
    characterDraftSec: number;
    responseTimeSec: number;
}

export interface MatchStateSnapshotDTO {
    roomId: string;
    roomCode: string;
    hostPlayerId: string;
    state: ServerGameState;
    phaseId?: string;
    deadlineAt?: number;
    currentTurnPlayerId?: string;
    currentPhase?: string; 
    turnNumber: number;
    players: PlayerSnapshotDTO[];
    privateState?: PrivatePlayerState; // Sent only to the owner
    drawPileCount: number;
    topDiscardCardId?: string;
    discardPileCount: number;
    activeInteraction?: InteractionPromptDTO;
    winnerRole?: string;
    winnerTeam?: string;
    combatLogs: string[];
    serverTime: number;
    sequence: number;
    revision: number;
    rules: RuleConfig;
}

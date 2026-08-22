export enum ServerGameState {
    LOBBY = 'LOBBY',
    WAITING = 'WAITING',
    DEALING_ROLES = 'DEALING_ROLES',
    ROLE_LOCK_WAIT = 'ROLE_LOCK_WAIT', // from spec frame 09
    SELECTING_CHARACTER = 'SELECTING_CHARACTER',
    INITIALIZING = 'INITIALIZING',
    PLAYING = 'PLAYING',
    WAITING_RESPONSE = 'WAITING_RESPONSE',
    TURN_ENDING = 'TURN_ENDING',
    FINISHED = 'FINISHED'
}

export interface PlayerSnapshotDTO {
    id: string;
    name: string;
    seat: number;
    isHost: boolean;
    isReady: boolean;
    isConnected: boolean;
    isAlive: boolean;
    currentHealth: number;
    maxHealth: number;
    characterId?: string;
    role?: string; 
    isRoleRevealed: boolean;
    handCount: number;
    hand: string[]; 
    equipment: string[];
    effectiveDistanceToLocal: number;
    isTargetable: boolean;
}

export interface InteractionPromptDTO {
    interactionId: string;
    type: string;
    actorPlayerId: string;
    title: string;
    message: string;
    minSelections: number;
    maxSelections: number;
    validPlayerIds: string[];
    validCardIds: string[];
    options: string[];
    expiresAt: number;
    canCancel: boolean;
    defaultAction?: string;
}

export interface MatchStateSnapshotDTO {
    roomId: string;
    roomCode: string;
    hostPlayerId: string;
    state: ServerGameState;
    currentTurnPlayerId?: string;
    currentPhase?: string; 
    turnNumber: number;
    players: PlayerSnapshotDTO[];
    drawPileCount: number;
    topDiscardCardId?: string;
    discardPileCount: number;
    activeInteraction?: InteractionPromptDTO;
    winnerRole?: string;
    winnerTeam?: string;
    combatLogs: string[];
    serverTime: number;
    sequence: number;
}

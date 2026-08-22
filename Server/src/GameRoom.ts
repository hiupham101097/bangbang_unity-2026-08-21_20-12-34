import { WebSocketServer, WebSocket } from 'ws';
import { MatchStateSnapshotDTO, PlayerSnapshotDTO, ServerGameState } from './models/GameState';

export class GameRoom {
    public roomId: string;
    public maxPlayers: number;
    
    private wss: WebSocketServer;
    private state: ServerGameState = ServerGameState.LOBBY;
    private players: Map<string, PlayerSnapshotDTO> = new Map();
    private sockets: Map<string, WebSocket> = new Map();
    
    private hostId: string = '';
    private turnNumber: number = 0;
    private sequence: number = 0;

    constructor(roomId: string, wss: WebSocketServer, config: any) {
        this.roomId = roomId;
        this.wss = wss;
        this.maxPlayers = config.maxPlayers || 5;
    }

    public joinPlayer(ws: WebSocket, name: string, isHost: boolean = false): boolean {
        if (this.players.size >= this.maxPlayers) return false;
        if (this.state !== ServerGameState.LOBBY) return false;

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
            if (p && this.state === ServerGameState.LOBBY) {
                p.isReady = data.isReady;
                this.broadcastSnapshot();
            }
        }
        else if (type === 'game.start') {
            if (socketId === this.hostId && this.state === ServerGameState.LOBBY) {
                const allReady = Array.from(this.players.values()).every(p => p.isReady);
                if (allReady && this.players.size >= 4) { 
                    this.startGame();
                } else {
                    ws.send(JSON.stringify({ type: 'game.error', data: 'Not all players ready or not enough players (min 4).' }));
                }
            }
        }
    }

    public handleDisconnect(socketId: string) {
        const p = this.players.get(socketId);
        if (p) {
            p.isConnected = false;
            this.sockets.delete(socketId);
            
            if (this.state === ServerGameState.LOBBY) {
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
            players: Array.from(this.players.values()).map(p => ({ ...p, hand: [] })), 
            drawPileCount: 80,
            discardPileCount: 0,
            combatLogs: [],
            serverTime: Date.now(),
            sequence: this.sequence++
        };
    }

    private broadcastSnapshot() {
        const snap = this.getSnapshot();
        const payload = JSON.stringify({ type: 'room.snapshot', data: snap });
        
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
        this.broadcastSnapshot();
    }
}

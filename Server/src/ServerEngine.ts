import { WebSocketServer, WebSocket } from 'ws';
import { GameRoom } from './GameRoom';
import { v4 as uuidv4 } from 'uuid';

export class ServerEngine {
    private wss: WebSocketServer;
    private rooms: Map<string, GameRoom> = new Map();

    constructor(wss: WebSocketServer) {
        this.wss = wss;
        this.setupHandlers();
    }

    private setupHandlers() {
        this.wss.on('connection', (ws: WebSocket) => {
            // Assign a connection ID
            (ws as any).id = uuidv4();
            console.log(`[CONNECT] User connected: ${(ws as any).id}`);

            ws.on('message', (message: string) => {
                try {
                    const parsed = JSON.parse(message);
                    this.handleMessage(ws, parsed);
                } catch (e) {
                    console.error('Invalid message format', message);
                }
            });

            ws.on('close', () => {
                console.log(`[DISCONNECT] User disconnected: ${(ws as any).id}`);
                for (const [roomId, room] of this.rooms.entries()) {
                    if (room.hasPlayer((ws as any).id)) {
                        room.handleDisconnect((ws as any).id);
                        if (room.isEmpty()) {
                            console.log(`[ROOM] Closing empty room ${roomId}`);
                            this.rooms.delete(roomId);
                        }
                    }
                }
            });
        });
    }

    private handleMessage(ws: WebSocket, payload: any) {
        const type = payload.type;
        const data = payload.data;
        const reqId = payload.reqId; // for callbacks

        if (type === 'session.resume') {
            const stableId = String(data?.deviceId || (ws as any).id).replace(/[^a-zA-Z0-9_-]/g, '').slice(0, 64);
            let resumed = false;
            for (const room of this.rooms.values()) {
                if (room.reconnectPlayer(stableId, ws)) { resumed = true; break; }
            }
            (ws as any).id = stableId;
            ws.send(JSON.stringify({ type: 'session.ready', data: JSON.stringify({ playerId: stableId, resumed, serverTime: Date.now() }) }));
        }
        else if (type === 'room.create') {
            const roomId = Math.random().toString(36).substring(2, 8).toUpperCase();
            const room = new GameRoom(roomId, this.wss, data || {});
            this.rooms.set(roomId, room);
            
            room.joinPlayer(ws, data?.playerName || 'Player', true);
            for (let i = 0; i < Math.min(Number(data?.botCount || 0), room.maxPlayers - 1); i++) room.addBot();
            
            if (reqId) {
                ws.send(JSON.stringify({ reqId, type: 'room.created', data: JSON.stringify({ roomId, playerId: (ws as any).id }) }));
            }
        } 
        else if (type === 'room.join') {
            const { roomId, playerName } = data;
            const room = this.rooms.get(roomId);
            
            if (!room) {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'error', data: 'Room not found' }));
                return;
            }

            const joined = room.joinPlayer(ws, playerName || 'Player');
            if (joined) {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'room.joined', data: JSON.stringify({ success: true, playerId: (ws as any).id }) }));
            } else {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'error', data: JSON.stringify('Room is full or game already started') }));
            }
        }
        else if (type === 'room.list') {
            const list = Array.from(this.rooms.values()).map(r => ({
                roomId: r.roomId,
                playerCount: r.getPlayers().length,
                maxPlayers: r.maxPlayers,
                state: r.getState()
            }));
            if (reqId) ws.send(JSON.stringify({ reqId, type: 'room.listResponse', data: JSON.stringify(list) }));
        }
        else {
            // Route to room if player is in one
            const socketId = (ws as any).id;
            for (const room of this.rooms.values()) {
                if (room.hasPlayer(socketId)) {
                    room.handleMessage(ws, type, data);
                    break;
                }
            }
        }
    }
}

"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServerEngine = void 0;
const GameRoom_1 = require("./GameRoom");
const uuid_1 = require("uuid");
class ServerEngine {
    wss;
    rooms = new Map();
    constructor(wss) {
        this.wss = wss;
        this.setupHandlers();
    }
    setupHandlers() {
        this.wss.on('connection', (ws) => {
            // Assign a connection ID
            ws.id = (0, uuid_1.v4)();
            console.log(`[CONNECT] User connected: ${ws.id}`);
            ws.on('message', (message) => {
                try {
                    const parsed = JSON.parse(message);
                    this.handleMessage(ws, parsed);
                }
                catch (e) {
                    console.error('Invalid message format', message);
                }
            });
            ws.on('close', () => {
                console.log(`[DISCONNECT] User disconnected: ${ws.id}`);
                for (const [roomId, room] of this.rooms.entries()) {
                    if (room.hasPlayer(ws.id)) {
                        room.handleDisconnect(ws.id);
                        if (room.isEmpty()) {
                            console.log(`[ROOM] Closing empty room ${roomId}`);
                            this.rooms.delete(roomId);
                        }
                    }
                }
            });
        });
    }
    handleMessage(ws, payload) {
        const type = payload.type;
        const data = payload.data;
        const reqId = payload.reqId; // for callbacks
        if (type === 'room.create') {
            const roomId = Math.random().toString(36).substring(2, 8).toUpperCase();
            const room = new GameRoom_1.GameRoom(roomId, this.wss, data || {});
            this.rooms.set(roomId, room);
            room.joinPlayer(ws, data?.playerName || 'Player', true);
            if (reqId) {
                ws.send(JSON.stringify({ reqId, type: 'room.created', data: JSON.stringify({ roomId: roomId }) }));
            }
        }
        else if (type === 'room.join') {
            const { roomId, playerName } = data;
            const room = this.rooms.get(roomId);
            if (!room) {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: 'Room not found' }));
                return;
            }
            const joined = room.joinPlayer(ws, playerName || 'Player');
            if (joined) {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'room.joined', data: JSON.stringify({ success: true }) }));
            }
            else {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: JSON.stringify('Room is full or game already started') }));
            }
        }
        else if (type === 'room.list') {
            const list = Array.from(this.rooms.values()).map(r => ({
                roomId: r.roomId,
                playerCount: r.getPlayers().length,
                maxPlayers: r.maxPlayers,
                state: r.getState()
            }));
            if (reqId)
                ws.send(JSON.stringify({ reqId, type: 'room.listResponse', data: JSON.stringify(list) }));
        }
        else {
            // Route to room if player is in one
            const socketId = ws.id;
            for (const room of this.rooms.values()) {
                if (room.hasPlayer(socketId)) {
                    room.handleMessage(ws, type, data);
                    break;
                }
            }
        }
    }
}
exports.ServerEngine = ServerEngine;

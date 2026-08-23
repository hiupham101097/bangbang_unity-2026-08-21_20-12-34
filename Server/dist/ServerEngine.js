"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServerEngine = void 0;
const GameRoom_1 = require("./GameRoom");
const uuid_1 = require("uuid");
class ServerEngine {
    wss;
    rooms = new Map();
    heartbeatHandle;
    static MAX_ROOMS = 500;
    constructor(wss) {
        this.wss = wss;
        this.setupHandlers();
        this.heartbeatHandle = setInterval(() => {
            for (const client of this.wss.clients) {
                const socket = client;
                if (socket.isAlive === false) {
                    socket.terminate();
                    continue;
                }
                socket.isAlive = false;
                socket.ping();
            }
        }, 30_000);
        this.heartbeatHandle.unref();
        this.wss.once('close', () => clearInterval(this.heartbeatHandle));
    }
    setupHandlers() {
        this.wss.on('connection', (ws) => {
            // Assign a connection ID
            ws.id = (0, uuid_1.v4)();
            ws.isAlive = true;
            ws.on('pong', () => { ws.isAlive = true; });
            console.log(`[CONNECT] User connected: ${ws.id}`);
            ws.on('message', (message) => {
                try {
                    const raw = message.toString();
                    if (raw.length > 64 * 1024)
                        return ws.close(1009, 'Message too large');
                    const parsed = JSON.parse(raw);
                    if (!parsed || typeof parsed.type !== 'string' || parsed.type.length > 80)
                        return;
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
                            room.dispose();
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
        if (type === 'session.resume') {
            const stableId = String(data?.deviceId || ws.id).replace(/[^a-zA-Z0-9_-]/g, '').slice(0, 64);
            let resumed = false;
            for (const room of this.rooms.values()) {
                if (room.reconnectPlayer(stableId, ws)) {
                    resumed = true;
                    break;
                }
            }
            ws.id = stableId;
            ws.send(JSON.stringify({ type: 'session.ready', data: JSON.stringify({ playerId: stableId, resumed, serverTime: Date.now() }) }));
        }
        else if (type === 'room.create') {
            if (this.rooms.size >= ServerEngine.MAX_ROOMS) {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: 'Server room capacity reached' }));
                return;
            }
            const roomId = Math.random().toString(36).substring(2, 8).toUpperCase();
            const room = new GameRoom_1.GameRoom(roomId, this.wss, data || {});
            this.rooms.set(roomId, room);
            room.joinPlayer(ws, data?.playerName || 'Player', true);
            for (let i = 0; i < Math.min(Number(data?.botCount || 0), room.maxPlayers - 1); i++)
                room.addBot();
            if (reqId) {
                ws.send(JSON.stringify({ reqId, type: 'room.created', data: JSON.stringify({ roomId, playerId: ws.id }) }));
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
                    ws.send(JSON.stringify({ reqId, type: 'room.joined', data: JSON.stringify({ success: true, playerId: ws.id }) }));
            }
            else {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: JSON.stringify('Room is full or game already started') }));
            }
        }
        else if (type === 'room.list') {
            const list = Array.from(this.rooms.values()).slice(0, 200).map(r => ({
                roomId: r.roomId,
                roomCode: r.roomId,
                roomName: `Saloon ${r.roomId}`,
                currentPlayers: r.getPlayers().length,
                maxPlayers: r.maxPlayers,
                state: r.getState(),
                turnTimeSeconds: 30,
                pingMs: 0,
                isPrivate: false
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

"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServerEngine = void 0;
const GameRoom_1 = require("./GameRoom");
const uuid_1 = require("uuid");
const node_fs_1 = __importDefault(require("node:fs"));
const node_path_1 = __importDefault(require("node:path"));
const node_crypto_1 = __importDefault(require("node:crypto"));
class ServerEngine {
    wss;
    rooms = new Map();
    heartbeatHandle;
    cleanupHandle;
    static MAX_ROOMS = 500;
    persistencePath = process.env.BANG_STATE_FILE || node_path_1.default.join(process.cwd(), 'data', 'rooms.json');
    persistHandle = null;
    messageWindows = new Map();
    profiles = new Map();
    authSecret = process.env.BANG_AUTH_SECRET || 'bang-development-secret-change-in-production';
    constructor(wss) {
        if (process.env.NODE_ENV === 'production' && !process.env.BANG_AUTH_SECRET) {
            throw new Error('BANG_AUTH_SECRET is required in production.');
        }
        this.wss = wss;
        this.loadPersistedRooms();
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
        this.cleanupHandle = setInterval(() => {
            for (const [roomId, room] of this.rooms.entries()) {
                room.pruneExpiredDisconnected();
                if (room.isEmpty()) {
                    room.dispose();
                    this.rooms.delete(roomId);
                }
            }
        }, 30_000);
        this.cleanupHandle.unref();
        this.wss.once('close', () => {
            clearInterval(this.heartbeatHandle);
            clearInterval(this.cleanupHandle);
        });
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
                    const now = Date.now();
                    const recent = (this.messageWindows.get(ws) || []).filter(time => now - time < 10_000);
                    if (recent.length >= 80)
                        return ws.close(1008, 'Rate limit exceeded');
                    recent.push(now);
                    this.messageWindows.set(ws, recent);
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
                this.messageWindows.delete(ws);
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
            if (this.profiles.has(stableId) && !this.verifyResumeToken(stableId, String(data?.accessToken || ''))) {
                ws.send(JSON.stringify({ type: 'session.reject', data: JSON.stringify({ code: 'INVALID_SESSION', message: 'Resume token is invalid.' }) }));
                return;
            }
            let resumed = false;
            for (const room of this.rooms.values()) {
                if (room.reconnectPlayer(stableId, ws)) {
                    resumed = true;
                    break;
                }
            }
            ws.id = stableId;
            if (!this.profiles.has(stableId))
                this.profiles.set(stableId, { level: 1, currency: 0, games: 0, wins: 0, claimed: [] });
            this.schedulePersist();
            ws.send(JSON.stringify({ type: 'session.ready', data: JSON.stringify({ playerId: stableId, resumed, serverTime: Date.now(), accessToken: this.createResumeToken(stableId), user: this.profiles.get(stableId), catalogVersion: 'base-1' }) }));
        }
        else if (type === 'profile.getSummary') {
            ws.send(JSON.stringify({ type: 'profile.summary', data: JSON.stringify(this.profiles.get(ws.id) || {}) }));
        }
        else if (type === 'mission.list') {
            const profile = this.profiles.get(ws.id) || { games: 0, wins: 0, claimed: [] };
            const missions = [
                { id: 'play_3', title: 'Chơi 3 trận', progress: profile.games, target: 3, reward: 100, claimed: profile.claimed.includes('play_3') },
                { id: 'win_1', title: 'Thắng 1 trận', progress: profile.wins, target: 1, reward: 150, claimed: profile.claimed.includes('win_1') }
            ];
            ws.send(JSON.stringify({ type: 'mission.snapshot', data: JSON.stringify({ missions, resetAt: this.nextUtcDay() }) }));
        }
        else if (type === 'mission.claim') {
            const profile = this.profiles.get(ws.id);
            const missionId = String(data?.missionId || '');
            const eligible = profile && ((missionId === 'play_3' && profile.games >= 3) || (missionId === 'win_1' && profile.wins >= 1));
            if (profile && eligible && !profile.claimed.includes(missionId)) {
                profile.claimed.push(missionId);
                profile.currency += missionId === 'win_1' ? 150 : 100;
                this.schedulePersist();
                ws.send(JSON.stringify({ type: 'mission.claimed', data: JSON.stringify({ missionId, currency: profile.currency }) }));
            }
            else
                ws.send(JSON.stringify({ type: 'error', data: JSON.stringify('MISSION_NOT_CLAIMABLE') }));
        }
        else if (type === 'catalog.get') {
            ws.send(JSON.stringify({ type: 'catalog.snapshot', data: JSON.stringify({ version: 'base-1', cards: this.catalogCards(), roles: ['sheriff', 'deputy', 'outlaw', 'renegade'] }) }));
        }
        else if (type === 'room.create') {
            if (this.rooms.size >= ServerEngine.MAX_ROOMS) {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: 'Server room capacity reached' }));
                return;
            }
            const roomId = Math.random().toString(36).substring(2, 8).toUpperCase();
            const room = new GameRoom_1.GameRoom(roomId, this.wss, data || {}, () => this.schedulePersist(), (players, winner) => this.recordGameResult(players, winner));
            this.rooms.set(roomId, room);
            room.joinPlayer(ws, this.sanitizePlayerName(data?.playerName), true);
            for (let i = 0; i < Math.min(Number(data?.botCount || 0), room.maxPlayers - 1); i++)
                room.addBot();
            if (reqId) {
                ws.send(JSON.stringify({ reqId, type: 'room.created', data: JSON.stringify({ roomId, playerId: ws.id }) }));
            }
        }
        else if (type === 'room.join') {
            const { roomId, playerName } = data;
            const room = this.rooms.get(String(roomId || '').toUpperCase());
            if (!room) {
                if (reqId)
                    ws.send(JSON.stringify({ reqId, type: 'error', data: 'Room not found' }));
                return;
            }
            const joined = room.joinPlayer(ws, this.sanitizePlayerName(playerName));
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
            const requestedRoomId = String(data?.roomId || '').toUpperCase();
            const requestedRoom = requestedRoomId ? this.rooms.get(requestedRoomId) : undefined;
            if (requestedRoom?.hasPlayer(socketId)) {
                requestedRoom.handleMessage(ws, type, data);
                return;
            }
            for (const room of this.rooms.values()) {
                if (!room.hasPlayer(socketId))
                    continue;
                room.handleMessage(ws, type, data);
                return;
            }
        }
    }
    dispose() {
        clearInterval(this.heartbeatHandle);
        clearInterval(this.cleanupHandle);
        if (this.persistHandle)
            clearTimeout(this.persistHandle);
        this.persistRooms();
        for (const room of this.rooms.values())
            room.dispose();
        this.rooms.clear();
    }
    nextUtcDay() {
        const date = new Date();
        return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate() + 1);
    }
    sanitizePlayerName(value) {
        const clean = String(value || 'Player').replace(/[\u0000-\u001f<>]/g, '').trim().slice(0, 24);
        return clean || 'Player';
    }
    catalogCards() {
        return ['bang', 'dodge', 'beer', 'saloon', 'dilizenza', 'wells_fargo', 'panico', 'cat_balou', 'general_store', 'duello', 'indiani', 'gatling', 'jail', 'dynamite', 'barrel', 'mustang', 'appaloosa', 'volcanic', 'gun_range_2', 'gun_range_3', 'gun_range_4', 'gun_range_5']
            .map(id => ({ id, effectKey: id, serverAuthoritative: true }));
    }
    recordGameResult(players, winnerTeam) {
        for (const player of players.filter(player => !player.isBot)) {
            const profile = this.profiles.get(player.id);
            if (!profile)
                continue;
            profile.games++;
            const won = winnerTeam === 'OUTLAWS' ? player.roleId === 'outlaw'
                : winnerTeam === 'RENEGADE' ? player.roleId === 'renegade'
                    : player.roleId === 'sheriff' || player.roleId === 'deputy';
            if (won)
                profile.wins++;
            profile.level = 1 + Math.floor(profile.games / 5);
        }
        this.schedulePersist();
    }
    createResumeToken(playerId) {
        return node_crypto_1.default.createHmac('sha256', this.authSecret).update(playerId).digest('base64url');
    }
    verifyResumeToken(playerId, token) {
        const expected = this.createResumeToken(playerId);
        if (token.length !== expected.length)
            return false;
        return node_crypto_1.default.timingSafeEqual(Buffer.from(token), Buffer.from(expected));
    }
    schedulePersist() {
        if (this.persistHandle)
            clearTimeout(this.persistHandle);
        this.persistHandle = setTimeout(() => this.persistRooms(), 100);
        this.persistHandle.unref();
    }
    persistRooms() {
        this.persistHandle = null;
        try {
            node_fs_1.default.mkdirSync(node_path_1.default.dirname(this.persistencePath), { recursive: true });
            const temporary = `${this.persistencePath}.tmp`;
            node_fs_1.default.writeFileSync(temporary, JSON.stringify({ rooms: Array.from(this.rooms.values()).map(room => room.exportState()), profiles: Array.from(this.profiles.entries()) }));
            node_fs_1.default.renameSync(temporary, this.persistencePath);
        }
        catch (error) {
            console.error('[PERSISTENCE] Unable to save rooms', error);
        }
    }
    loadPersistedRooms() {
        try {
            if (!node_fs_1.default.existsSync(this.persistencePath))
                return;
            const stored = JSON.parse(node_fs_1.default.readFileSync(this.persistencePath, 'utf8'));
            const states = Array.isArray(stored) ? stored : stored.rooms;
            if (!Array.isArray(states))
                return;
            if (Array.isArray(stored.profiles))
                this.profiles = new Map(stored.profiles);
            for (const state of states.slice(0, ServerEngine.MAX_ROOMS)) {
                const room = GameRoom_1.GameRoom.restore(state, this.wss, () => this.schedulePersist(), (players, winner) => this.recordGameResult(players, winner));
                this.rooms.set(room.roomId, room);
            }
            console.log(`[PERSISTENCE] Restored ${this.rooms.size} room(s)`);
        }
        catch (error) {
            console.error('[PERSISTENCE] Ignoring invalid room state', error);
        }
    }
}
exports.ServerEngine = ServerEngine;

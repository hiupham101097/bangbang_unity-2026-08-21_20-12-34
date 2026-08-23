import { WebSocketServer, WebSocket } from 'ws';
import { GameRoom } from './GameRoom';
import { v4 as uuidv4 } from 'uuid';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

export class ServerEngine {
    private wss: WebSocketServer;
    private rooms: Map<string, GameRoom> = new Map();
    private heartbeatHandle: NodeJS.Timeout;
    private cleanupHandle: NodeJS.Timeout;
    private static readonly MAX_ROOMS = 500;
    private readonly persistencePath = process.env.BANG_STATE_FILE || path.join(process.cwd(), 'data', 'rooms.json');
    private persistHandle: NodeJS.Timeout | null = null;
    private messageWindows: Map<WebSocket, number[]> = new Map();
    private profiles: Map<string, { level: number, currency: number, games: number, wins: number, claimed: string[] }> = new Map();
    private readonly authSecret = process.env.BANG_AUTH_SECRET || 'bang-development-secret-change-in-production';

    constructor(wss: WebSocketServer) {
        if (process.env.NODE_ENV === 'production' && !process.env.BANG_AUTH_SECRET) {
            throw new Error('BANG_AUTH_SECRET is required in production.');
        }
        this.wss = wss;
        this.loadPersistedRooms();
        this.setupHandlers();
        this.heartbeatHandle = setInterval(() => {
            for (const client of this.wss.clients) {
                const socket = client as WebSocket & { isAlive?: boolean };
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

    private setupHandlers() {
        this.wss.on('connection', (ws: WebSocket) => {
            // Assign a connection ID
            (ws as any).id = uuidv4();
            (ws as any).isAlive = true;
            ws.on('pong', () => { (ws as any).isAlive = true; });
            console.log(`[CONNECT] User connected: ${(ws as any).id}`);

            ws.on('message', (message) => {
                try {
                    const now = Date.now();
                    const recent = (this.messageWindows.get(ws) || []).filter(time => now - time < 10_000);
                    if (recent.length >= 80) return ws.close(1008, 'Rate limit exceeded');
                    recent.push(now);
                    this.messageWindows.set(ws, recent);
                    const raw = message.toString();
                    if (raw.length > 64 * 1024) return ws.close(1009, 'Message too large');
                    const parsed = JSON.parse(raw);
                    if (!parsed || typeof parsed.type !== 'string' || parsed.type.length > 80) return;
                    this.handleMessage(ws, parsed);
                } catch (e) {
                    console.error('Invalid message format', message);
                }
            });

            ws.on('close', () => {
                this.messageWindows.delete(ws);
                console.log(`[DISCONNECT] User disconnected: ${(ws as any).id}`);
                for (const [roomId, room] of this.rooms.entries()) {
                    if (room.hasPlayer((ws as any).id)) {
                        room.handleDisconnect((ws as any).id);
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

    private handleMessage(ws: WebSocket, payload: any) {
        const type = payload.type;
        const data = payload.data;
        const reqId = payload.reqId; // for callbacks

        if (type === 'session.resume') {
            const stableId = String(data?.deviceId || (ws as any).id).replace(/[^a-zA-Z0-9_-]/g, '').slice(0, 64);
            if (this.profiles.has(stableId) && !this.verifyResumeToken(stableId, String(data?.accessToken || ''))) {
                ws.send(JSON.stringify({ type: 'session.reject', data: JSON.stringify({ code: 'INVALID_SESSION', message: 'Resume token is invalid.' }) }));
                return;
            }
            let resumed = false;
            for (const room of this.rooms.values()) {
                if (room.reconnectPlayer(stableId, ws)) { resumed = true; break; }
            }
            (ws as any).id = stableId;
            if (!this.profiles.has(stableId)) this.profiles.set(stableId, { level: 1, currency: 0, games: 0, wins: 0, claimed: [] });
            this.schedulePersist();
            ws.send(JSON.stringify({ type: 'session.ready', data: JSON.stringify({ playerId: stableId, resumed, serverTime: Date.now(), accessToken: this.createResumeToken(stableId), user: this.profiles.get(stableId), catalogVersion: 'base-1' }) }));
        }
        else if (type === 'profile.getSummary') {
            ws.send(JSON.stringify({ type: 'profile.summary', data: JSON.stringify(this.profiles.get((ws as any).id) || {}) }));
        }
        else if (type === 'mission.list') {
            const profile: { games: number, wins: number, claimed: string[] } = this.profiles.get((ws as any).id) || { games: 0, wins: 0, claimed: [] };
            const missions = [
                { id: 'play_3', title: 'Chơi 3 trận', progress: profile.games, target: 3, reward: 100, claimed: profile.claimed.includes('play_3') },
                { id: 'win_1', title: 'Thắng 1 trận', progress: profile.wins, target: 1, reward: 150, claimed: profile.claimed.includes('win_1') }
            ];
            ws.send(JSON.stringify({ type: 'mission.snapshot', data: JSON.stringify({ missions, resetAt: this.nextUtcDay() }) }));
        }
        else if (type === 'mission.claim') {
            const profile = this.profiles.get((ws as any).id);
            const missionId = String(data?.missionId || '');
            const eligible = profile && ((missionId === 'play_3' && profile.games >= 3) || (missionId === 'win_1' && profile.wins >= 1));
            if (profile && eligible && !profile.claimed.includes(missionId)) {
                profile.claimed.push(missionId);
                profile.currency += missionId === 'win_1' ? 150 : 100;
                this.schedulePersist();
                ws.send(JSON.stringify({ type: 'mission.claimed', data: JSON.stringify({ missionId, currency: profile.currency }) }));
            } else ws.send(JSON.stringify({ type: 'error', data: JSON.stringify('MISSION_NOT_CLAIMABLE') }));
        }
        else if (type === 'catalog.get') {
            ws.send(JSON.stringify({ type: 'catalog.snapshot', data: JSON.stringify({ version: 'base-1', cards: this.catalogCards(), roles: ['sheriff','deputy','outlaw','renegade'] }) }));
        }
        else if (type === 'room.create') {
            if (this.rooms.size >= ServerEngine.MAX_ROOMS) {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'error', data: 'Server room capacity reached' }));
                return;
            }
            const roomId = Math.random().toString(36).substring(2, 8).toUpperCase();
            const room = new GameRoom(roomId, this.wss, data || {}, () => this.schedulePersist(), (players, winner) => this.recordGameResult(players, winner));
            this.rooms.set(roomId, room);
            
            room.joinPlayer(ws, this.sanitizePlayerName(data?.playerName), true);
            for (let i = 0; i < Math.min(Number(data?.botCount || 0), room.maxPlayers - 1); i++) room.addBot();
            
            if (reqId) {
                ws.send(JSON.stringify({ reqId, type: 'room.created', data: JSON.stringify({ roomId, playerId: (ws as any).id }) }));
            }
        } 
        else if (type === 'room.join') {
            const { roomId, playerName } = data;
            const room = this.rooms.get(String(roomId || '').toUpperCase());
            
            if (!room) {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'error', data: 'Room not found' }));
                return;
            }

            const joined = room.joinPlayer(ws, this.sanitizePlayerName(playerName));
            if (joined) {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'room.joined', data: JSON.stringify({ success: true, playerId: (ws as any).id }) }));
            } else {
                if (reqId) ws.send(JSON.stringify({ reqId, type: 'error', data: JSON.stringify('Room is full or game already started') }));
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
            if (reqId) ws.send(JSON.stringify({ reqId, type: 'room.listResponse', data: JSON.stringify(list) }));
        }
        else {
            // Route to room if player is in one
            const socketId = (ws as any).id;
            const requestedRoomId = String(data?.roomId || '').toUpperCase();
            const requestedRoom = requestedRoomId ? this.rooms.get(requestedRoomId) : undefined;
            if (requestedRoom?.hasPlayer(socketId)) {
                requestedRoom.handleMessage(ws, type, data);
                return;
            }
            for (const room of this.rooms.values()) {
                if (!room.hasPlayer(socketId)) continue;
                room.handleMessage(ws, type, data);
                return;
            }
        }
    }

    public dispose() {
        clearInterval(this.heartbeatHandle);
        clearInterval(this.cleanupHandle);
        if (this.persistHandle) clearTimeout(this.persistHandle);
        this.persistRooms();
        for (const room of this.rooms.values()) room.dispose();
        this.rooms.clear();
    }

    private nextUtcDay(): number {
        const date = new Date();
        return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate() + 1);
    }

    private sanitizePlayerName(value: unknown): string {
        const clean = String(value || 'Player').replace(/[\u0000-\u001f<>]/g, '').trim().slice(0, 24);
        return clean || 'Player';
    }

    private catalogCards() {
        return ['bang','dodge','beer','saloon','dilizenza','wells_fargo','panico','cat_balou','general_store','duello','indiani','gatling','jail','dynamite','barrel','mustang','appaloosa','volcanic','gun_range_2','gun_range_3','gun_range_4','gun_range_5']
            .map(id => ({ id, effectKey: id, serverAuthoritative: true }));
    }

    private recordGameResult(players: any[], winnerTeam: string) {
        for (const player of players.filter(player => !player.isBot)) {
            const profile = this.profiles.get(player.id);
            if (!profile) continue;
            profile.games++;
            const won = winnerTeam === 'OUTLAWS' ? player.roleId === 'outlaw'
                : winnerTeam === 'RENEGADE' ? player.roleId === 'renegade'
                : player.roleId === 'sheriff' || player.roleId === 'deputy';
            if (won) profile.wins++;
            profile.level = 1 + Math.floor(profile.games / 5);
        }
        this.schedulePersist();
    }

    private createResumeToken(playerId: string): string {
        return crypto.createHmac('sha256', this.authSecret).update(playerId).digest('base64url');
    }

    private verifyResumeToken(playerId: string, token: string): boolean {
        const expected = this.createResumeToken(playerId);
        if (token.length !== expected.length) return false;
        return crypto.timingSafeEqual(Buffer.from(token), Buffer.from(expected));
    }

    private schedulePersist() {
        if (this.persistHandle) clearTimeout(this.persistHandle);
        this.persistHandle = setTimeout(() => this.persistRooms(), 100);
        this.persistHandle.unref();
    }

    private persistRooms() {
        this.persistHandle = null;
        try {
            fs.mkdirSync(path.dirname(this.persistencePath), { recursive: true });
            const temporary = `${this.persistencePath}.tmp`;
            fs.writeFileSync(temporary, JSON.stringify({ rooms: Array.from(this.rooms.values()).map(room => room.exportState()), profiles: Array.from(this.profiles.entries()) }));
            fs.renameSync(temporary, this.persistencePath);
        } catch (error) {
            console.error('[PERSISTENCE] Unable to save rooms', error);
        }
    }

    private loadPersistedRooms() {
        try {
            if (!fs.existsSync(this.persistencePath)) return;
            const stored = JSON.parse(fs.readFileSync(this.persistencePath, 'utf8'));
            const states = Array.isArray(stored) ? stored : stored.rooms;
            if (!Array.isArray(states)) return;
            if (Array.isArray(stored.profiles)) this.profiles = new Map(stored.profiles);
            for (const state of states.slice(0, ServerEngine.MAX_ROOMS)) {
                const room = GameRoom.restore(state, this.wss, () => this.schedulePersist(), (players, winner) => this.recordGameResult(players, winner));
                this.rooms.set(room.roomId, room);
            }
            console.log(`[PERSISTENCE] Restored ${this.rooms.size} room(s)`);
        } catch (error) {
            console.error('[PERSISTENCE] Ignoring invalid room state', error);
        }
    }
}

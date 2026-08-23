"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_http_1 = __importDefault(require("node:http"));
const ws_1 = __importStar(require("ws"));
const ServerEngine_1 = require("./ServerEngine");
const roomCount = Math.max(1, Math.min(50, Number(process.env.SOAK_ROOMS || 5)));
const durationMs = Math.max(10_000, Number(process.env.SOAK_SECONDS || 60) * 1000);
process.env.BANG_STATE_FILE = process.env.BANG_STATE_FILE || `${process.cwd()}/data/soak-rooms.json`;
async function main() {
    const server = node_http_1.default.createServer();
    const wss = new ws_1.WebSocketServer({ server, maxPayload: 64 * 1024, perMessageDeflate: false });
    const engine = new ServerEngine_1.ServerEngine(wss);
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const address = server.address();
    if (!address || typeof address === 'string')
        throw new Error('Unable to bind soak server');
    const sockets = [];
    let snapshots = 0;
    try {
        for (let index = 0; index < roomCount; index++) {
            const socket = new ws_1.default(`ws://127.0.0.1:${address.port}`);
            sockets.push(socket);
            await new Promise((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
            socket.on('message', raw => {
                const message = JSON.parse(raw.toString());
                if (message.type === 'room.snapshot')
                    snapshots++;
            });
            socket.send(JSON.stringify({ type: 'session.resume', data: { deviceId: `soak_${Date.now()}_${index}` } }));
            socket.send(JSON.stringify({ type: 'room.create', data: { playerName: `Soak ${index}`, maxPlayers: 8, botCount: 7, roleDraftSec: 2, characterDraftSec: 4, turnTimeSec: 10 } }));
            await new Promise(resolve => setTimeout(resolve, 25));
            socket.send(JSON.stringify({ type: 'game.start', data: {} }));
        }
        const started = Date.now();
        await new Promise(resolve => setTimeout(resolve, durationMs));
        const heapMb = Math.round(process.memoryUsage().heapUsed / 1024 / 1024);
        if (snapshots < roomCount * 3)
            throw new Error(`Insufficient activity: ${snapshots} snapshots`);
        console.log(`[SOAK] ${roomCount} rooms, ${snapshots} snapshots, ${Date.now() - started}ms, heap ${heapMb} MiB`);
    }
    finally {
        sockets.forEach(socket => socket.close());
        engine.dispose();
        await new Promise(resolve => wss.close(() => resolve()));
        await new Promise(resolve => server.close(() => resolve()));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });

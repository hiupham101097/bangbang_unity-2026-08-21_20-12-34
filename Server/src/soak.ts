import http from 'node:http';
import WebSocket, { WebSocketServer } from 'ws';
import { ServerEngine } from './ServerEngine';

const roomCount = Math.max(1, Math.min(50, Number(process.env.SOAK_ROOMS || 5)));
const durationMs = Math.max(10_000, Number(process.env.SOAK_SECONDS || 60) * 1000);
process.env.BANG_STATE_FILE = process.env.BANG_STATE_FILE || `${process.cwd()}/data/soak-rooms.json`;

async function main() {
    const server = http.createServer();
    const wss = new WebSocketServer({ server, maxPayload: 64 * 1024, perMessageDeflate: false });
    const engine = new ServerEngine(wss);
    await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));
    const address = server.address();
    if (!address || typeof address === 'string') throw new Error('Unable to bind soak server');
    const sockets: WebSocket[] = [];
    let snapshots = 0;

    try {
        for (let index = 0; index < roomCount; index++) {
            const socket = new WebSocket(`ws://127.0.0.1:${address.port}`);
            sockets.push(socket);
            await new Promise<void>((resolve, reject) => { socket.once('open', resolve); socket.once('error', reject); });
            socket.on('message', raw => {
                const message = JSON.parse(raw.toString());
                if (message.type === 'room.snapshot') snapshots++;
            });
            socket.send(JSON.stringify({ type: 'session.resume', data: { deviceId: `soak_${Date.now()}_${index}` } }));
            socket.send(JSON.stringify({ type: 'room.create', data: { playerName: `Soak ${index}`, maxPlayers: 8, botCount: 7, roleDraftSec: 2, characterDraftSec: 4, turnTimeSec: 10 } }));
            await new Promise(resolve => setTimeout(resolve, 25));
            socket.send(JSON.stringify({ type: 'game.start', data: {} }));
        }
        const started = Date.now();
        await new Promise(resolve => setTimeout(resolve, durationMs));
        const heapMb = Math.round(process.memoryUsage().heapUsed / 1024 / 1024);
        if (snapshots < roomCount * 3) throw new Error(`Insufficient activity: ${snapshots} snapshots`);
        console.log(`[SOAK] ${roomCount} rooms, ${snapshots} snapshots, ${Date.now() - started}ms, heap ${heapMb} MiB`);
    } finally {
        sockets.forEach(socket => socket.close());
        engine.dispose();
        await new Promise<void>(resolve => wss.close(() => resolve()));
        await new Promise<void>(resolve => server.close(() => resolve()));
    }
}

main().catch(error => { console.error(error); process.exitCode = 1; });

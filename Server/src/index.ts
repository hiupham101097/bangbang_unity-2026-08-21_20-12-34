import express from 'express';
import http from 'http';
import { WebSocketServer } from 'ws';
import cors from 'cors';
import { ServerEngine } from './ServerEngine';

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
const allowedOrigins = new Set((process.env.BANG_ALLOWED_ORIGINS || '').split(',').map(value => value.trim()).filter(Boolean));
const wss = new WebSocketServer({
    server,
    maxPayload: 64 * 1024,
    perMessageDeflate: false,
    verifyClient: ({ origin }, done) => done(!origin || allowedOrigins.size === 0 || allowedOrigins.has(origin), 403, 'Origin not allowed')
});

const PORT = process.env.PORT || 3000;

app.get('/health', (req, res) => {
    res.send({ status: 'ok', version: '1.0.0' });
});

const engine = new ServerEngine(wss);

server.listen(PORT, () => {
    console.log(`[BANG SERVER] Listening on port ${PORT}`);
});

const shutdown = () => {
    engine.dispose();
    wss.close();
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(1), 5000).unref();
};
process.once('SIGINT', shutdown);
process.once('SIGTERM', shutdown);

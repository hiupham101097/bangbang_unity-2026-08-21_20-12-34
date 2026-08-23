import express from 'express';
import http from 'http';
import { WebSocketServer } from 'ws';
import cors from 'cors';
import { ServerEngine } from './ServerEngine';

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
const wss = new WebSocketServer({
    server,
    maxPayload: 64 * 1024,
    perMessageDeflate: false
});

const PORT = process.env.PORT || 3000;

app.get('/health', (req, res) => {
    res.send({ status: 'ok', version: '1.0.0' });
});

const engine = new ServerEngine(wss);

server.listen(PORT, () => {
    console.log(`[BANG SERVER] Listening on port ${PORT}`);
});

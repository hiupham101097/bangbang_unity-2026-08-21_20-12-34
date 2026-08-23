"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const express_1 = __importDefault(require("express"));
const http_1 = __importDefault(require("http"));
const ws_1 = require("ws");
const cors_1 = __importDefault(require("cors"));
const ServerEngine_1 = require("./ServerEngine");
const app = (0, express_1.default)();
app.use((0, cors_1.default)());
app.use(express_1.default.json());
const server = http_1.default.createServer(app);
const wss = new ws_1.WebSocketServer({
    server,
    maxPayload: 64 * 1024,
    perMessageDeflate: false
});
const PORT = process.env.PORT || 3000;
app.get('/health', (req, res) => {
    res.send({ status: 'ok', version: '1.0.0' });
});
const engine = new ServerEngine_1.ServerEngine(wss);
server.listen(PORT, () => {
    console.log(`[BANG SERVER] Listening on port ${PORT}`);
});

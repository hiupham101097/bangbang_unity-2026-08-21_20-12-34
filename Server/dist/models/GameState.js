"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServerGameState = void 0;
var ServerGameState;
(function (ServerGameState) {
    ServerGameState["LOBBY"] = "LOBBY";
    ServerGameState["WAITING"] = "WAITING";
    ServerGameState["DEALING_ROLES"] = "DEALING_ROLES";
    ServerGameState["ROLE_LOCK_WAIT"] = "ROLE_LOCK_WAIT";
    ServerGameState["SELECTING_CHARACTER"] = "SELECTING_CHARACTER";
    ServerGameState["INITIALIZING"] = "INITIALIZING";
    ServerGameState["PLAYING"] = "PLAYING";
    ServerGameState["WAITING_RESPONSE"] = "WAITING_RESPONSE";
    ServerGameState["TURN_ENDING"] = "TURN_ENDING";
    ServerGameState["FINISHED"] = "FINISHED";
})(ServerGameState || (exports.ServerGameState = ServerGameState = {}));

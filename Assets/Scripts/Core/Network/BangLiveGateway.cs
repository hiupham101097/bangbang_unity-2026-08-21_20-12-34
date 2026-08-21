using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BangBang.Core.Network
{
    public class BangLiveGateway : MonoBehaviour, IGameGateway
    {
        public ConnectionState CurrentConnectionState { get; private set; } = ConnectionState.Disconnected;
        public string LocalPlayerId { get; private set; } = "p_local";

        public event Action<MatchStateSnapshotDTO> OnSnapshotReceived;
        public event Action<InteractionPromptDTO> OnInteractionReceived;
        public event Action<string, string> OnActionRejected;
        public event Action<List<RoomSummaryDTO>> OnRoomListUpdated;
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<string> OnErrorMessage;

        [Header("Cloudflare Server URL")]
        public string serverBaseUrl = "https://blue-frog-fec8.hieupham101097.workers.dev";
        public string AuthToken { get; private set; }
        public string CurrentRoomId { get; private set; }

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;

        public async Task<bool> InitializeSessionAsync(string deviceId, string displayName)
        {
            LocalPlayerId = string.IsNullOrEmpty(deviceId) ? Guid.NewGuid().ToString("N").Substring(0, 16) : deviceId;
            CurrentConnectionState = ConnectionState.Connecting;
            OnConnectionStateChanged?.Invoke(CurrentConnectionState);

            string url = serverBaseUrl + "/v1/session";
            string bodyJson = "{\"deviceId\":\"" + LocalPlayerId + "\",\"displayName\":\"" + (displayName ?? "Cao bồi viễn tây") + "\"}";

            using (var req = UnityWebRequest.Post(url, bodyJson, "application/json"))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);
                    AuthToken = res.token;
                    CurrentConnectionState = ConnectionState.Connected;
                    OnConnectionStateChanged?.Invoke(CurrentConnectionState);
                    return true;
                }
                else
                {
                    OnErrorMessage?.Invoke("Lỗi kết nối phiên đăng nhập: " + req.error);
                    CurrentConnectionState = ConnectionState.Disconnected;
                    OnConnectionStateChanged?.Invoke(CurrentConnectionState);
                    return false;
                }
            }
        }

        public async Task<bool> RefreshRoomListAsync()
        {
            string url = serverBaseUrl + "/v1/rooms";
            using (var req = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(AuthToken)) req.SetRequestHeader("Authorization", "Bearer " + AuthToken);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // If server returns room list JSON array
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool isPrivate, string password, int turnSeconds)
        {
            string url = serverBaseUrl + "/v1/rooms";
            string body = "{\"name\":\"" + roomName + "\",\"maxPlayers\":" + maxPlayers + "}";

            using (var req = UnityWebRequest.Post(url, body, "application/json"))
            {
                if (!string.IsNullOrEmpty(AuthToken)) req.SetRequestHeader("Authorization", "Bearer " + AuthToken);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<RoomResponse>(req.downloadHandler.text);
                    CurrentRoomId = res.id;
                    await ConnectWebSocketAsync(res.id);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> JoinRoomAsync(string roomCodeOrId, string password = "")
        {
            CurrentRoomId = roomCodeOrId;
            return await ConnectWebSocketAsync(roomCodeOrId);
        }

        public Task<bool> LeaveRoomAsync()
        {
            DisconnectWebSocket();
            return Task.FromResult(true);
        }

        public Task<bool> ToggleReadyAsync(bool isReady)
        {
            return SendWebSocketActionAsync("READY", "{\"isReady\":" + (isReady ? "true" : "false") + "}");
        }

        public Task<bool> StartGameAsync()
        {
            return SendWebSocketActionAsync("START_GAME");
        }

        public Task<bool> SelectCharacterAsync(string characterId)
        {
            return SendWebSocketActionAsync("SELECT_CHARACTER", "{\"characterId\":\"" + characterId + "\"}");
        }

        public Task<bool> RequestDrawAsync()
        {
            return SendWebSocketActionAsync("DRAW");
        }

        public Task<bool> PlayCardAsync(string cardId, List<string> targetPlayerIds = null, List<string> selectedCardIds = null)
        {
            string targetJson = targetPlayerIds != null && targetPlayerIds.Count > 0 ? ",\"targetPlayerId\":\"" + targetPlayerIds[0] + "\"" : "";
            return SendWebSocketActionAsync("PLAY_CARD", "{\"cardId\":\"" + cardId + "\"" + targetJson + "}");
        }

        public Task<bool> SubmitInteractionAsync(string interactionId, string action, List<string> selectedPlayers = null, List<string> selectedCards = null, int optionIndex = 0)
        {
            return SendWebSocketActionAsync("INTERACTION_RESPONSE", "{\"interactionId\":\"" + interactionId + "\",\"action\":\"" + action + "\"}");
        }

        public Task<bool> EndTurnAsync(List<string> discardCardIds = null)
        {
            return SendWebSocketActionAsync("END_TURN");
        }

        public Task<bool> RequestRematchAsync()
        {
            return SendWebSocketActionAsync("REMATCH");
        }

        private async Task<bool> ConnectWebSocketAsync(string roomId)
        {
            DisconnectWebSocket();
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            string wsUrl = serverBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/rooms/" + roomId + "/ws?token=" + AuthToken;

            try
            {
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);
                _ = ReceiveWebSocketLoopAsync();
                return true;
            }
            catch (Exception ex)
            {
                OnErrorMessage?.Invoke("Lỗi kết nối WebSocket: " + ex.Message);
                return false;
            }
        }

        private async Task ReceiveWebSocketLoopAsync()
        {
            var buffer = new byte[8192];
            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleServerWebSocketMessage(json);
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private void HandleServerWebSocketMessage(string json)
        {
            try
            {
                var snapshot = JsonUtility.FromJson<MatchStateSnapshotDTO>(json);
                if (snapshot != null)
                {
                    OnSnapshotReceived?.Invoke(snapshot);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BangLiveGateway] Parse snapshot error: " + ex.Message);
            }
        }

        private async Task<bool> SendWebSocketActionAsync(string action, string payloadJson = "{}")
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return false;

            try
            {
                string msg = "{\"type\":\"" + action + "\",\"payload\":" + payloadJson + "}";
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorMessage?.Invoke("Lỗi gửi request: " + ex.Message);
                return false;
            }
        }

        private void DisconnectWebSocket()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_webSocket != null)
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }
    }
}

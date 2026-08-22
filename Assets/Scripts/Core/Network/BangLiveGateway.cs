using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BangBang.Core.Network
{
    [Serializable]
    public class WsMessage
    {
        public string type;
        public string reqId;
        public string data; // Contains stringified JSON data from server or to server
    }

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

        [Header("Node.js Server URL")]
        public string serverWsUrl = "ws://localhost:3000";
        public string CurrentRoomId { get; private set; }

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private string _displayName;
        
        // Dictionary to track request callbacks
        private Dictionary<string, Action<string>> _pendingRequests = new Dictionary<string, Action<string>>();

        public async Task<bool> InitializeSessionAsync(string deviceId, string displayName)
        {
            LocalPlayerId = string.IsNullOrEmpty(deviceId) ? Guid.NewGuid().ToString("N").Substring(0, 16) : deviceId;
            _displayName = displayName ?? "Player";
            
            CurrentConnectionState = ConnectionState.Connecting;
            OnConnectionStateChanged?.Invoke(CurrentConnectionState);

            DisconnectWebSocket();
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(serverWsUrl), _cts.Token);
                _ = ReceiveWebSocketLoopAsync();
                
                CurrentConnectionState = ConnectionState.Connected;
                OnConnectionStateChanged?.Invoke(CurrentConnectionState);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorMessage?.Invoke("Lỗi kết nối WebSocket: " + ex.Message);
                CurrentConnectionState = ConnectionState.Disconnected;
                OnConnectionStateChanged?.Invoke(CurrentConnectionState);
                return false;
            }
        }

        private async Task ReceiveWebSocketLoopAsync()
        {
            var buffer = new byte[8192 * 4];
            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleServerMessage(json);
                }
                catch (Exception)
                {
                    break;
                }
            }
            
            CurrentConnectionState = ConnectionState.Disconnected;
            OnConnectionStateChanged?.Invoke(CurrentConnectionState);
        }

        private void HandleServerMessage(string jsonRaw)
        {
            try
            {
                var msg = JsonUtility.FromJson<WsMessage>(jsonRaw);
                if (msg == null) return;

                // Handle callbacks
                if (!string.IsNullOrEmpty(msg.reqId) && _pendingRequests.ContainsKey(msg.reqId))
                {
                    _pendingRequests[msg.reqId]?.Invoke(msg.data);
                    _pendingRequests.Remove(msg.reqId);
                }

                // Global events
                if (msg.type == "room.snapshot")
                {
                    var snapshot = JsonUtility.FromJson<MatchStateSnapshotDTO>(msg.data);
                    OnSnapshotReceived?.Invoke(snapshot);
                }
                else if (msg.type == "game.error")
                {
                    OnErrorMessage?.Invoke(msg.data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BangLiveGateway] Parse error: " + ex.Message + "\nJSON: " + jsonRaw);
            }
        }

        private async Task<string> SendRequestAsync(string type, string dataJson = "{}")
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return null;

            string reqId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _pendingRequests[reqId] = (response) => tcs.TrySetResult(response);

            string msgStr = "{\"type\":\"" + type + "\",\"reqId\":\"" + reqId + "\",\"data\":" + dataJson + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(msgStr);

            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                
                // Timeout logic
                var resultTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                if (resultTask == tcs.Task) return await tcs.Task;
                
                _pendingRequests.Remove(reqId);
                return null; // timeout
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> SendEventAsync(string type, string dataJson = "{}")
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return false;

            string msgStr = "{\"type\":\"" + type + "\",\"data\":" + dataJson + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(msgStr);

            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // --- IGameGateway Implementation ---

        public async Task<bool> RefreshRoomListAsync()
        {
            var res = await SendRequestAsync("room.list");
            if (res != null)
            {
                // In a real app we'd parse this list. For now we assume success if response isn't null.
                return true;
            }
            return false;
        }

        public async Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool isPrivate, string password, int turnSeconds)
        {
            string payload = "{\"playerName\":\"" + _displayName + "\",\"maxPlayers\":" + maxPlayers + "}";
            var res = await SendRequestAsync("room.create", payload);
            if (res != null)
            {
                try {
                    var dict = JsonUtility.FromJson<RoomCreatedResponseDTO>(res);
                    if (dict != null && !string.IsNullOrEmpty(dict.roomId)) CurrentRoomId = dict.roomId;
                } catch { CurrentRoomId = "WAITING..."; }
                return true;
            }
            return false;
        }

        public async Task<bool> JoinRoomAsync(string roomCodeOrId, string password = "")
        {
            string payload = "{\"roomId\":\"" + roomCodeOrId + "\",\"playerName\":\"" + _displayName + "\"}";
            var res = await SendRequestAsync("room.join", payload);
            if (res != null && !res.Contains("error"))
            {
                CurrentRoomId = roomCodeOrId;
                return true;
            }
            return false;
        }

        public Task<bool> LeaveRoomAsync()
        {
            DisconnectWebSocket();
            return Task.FromResult(true);
        }

        public Task<bool> ToggleReadyAsync(bool isReady)
        {
            return SendEventAsync("room.ready", "{\"isReady\":" + (isReady ? "true" : "false") + "}");
        }

        public Task<bool> StartGameAsync()
        {
            return SendEventAsync("game.start");
        }

        public Task<bool> SelectCharacterAsync(string characterId)
        {
            return SendEventAsync("draft.character.pick", "{\"characterId\":\"" + characterId + "\"}");
        }

        public Task<bool> RequestDrawAsync()
        {
            return SendEventAsync("game.action.draw");
        }

        public Task<bool> PlayCardAsync(string cardId, List<string> targetPlayerIds = null, List<string> selectedCardIds = null)
        {
            return SendEventAsync("game.action.playCard", "{\"cardId\":\"" + cardId + "\"}");
        }

        public Task<bool> SubmitInteractionAsync(string interactionId, string action, List<string> selectedPlayers = null, List<string> selectedCards = null, int optionIndex = 0)
        {
            return SendEventAsync("effect.respond", "{\"interactionId\":\"" + interactionId + "\"}");
        }

        public Task<bool> EndTurnAsync(List<string> discardCardIds = null)
        {
            return SendEventAsync("turn.endPlay");
        }

        public Task<bool> RequestRematchAsync()
        {
            return SendEventAsync("room.rematch");
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
        
        private void OnDestroy()
        {
            DisconnectWebSocket();
        }
    }
}

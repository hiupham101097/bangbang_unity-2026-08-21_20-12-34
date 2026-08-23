using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BangBang.Core.Network
{
    public sealed class BangCloudflareGateway : MonoBehaviour, IGameGateway
    {
        [Serializable] private class WorkerUser { public string id; public string name; public string avatarId; }
        [Serializable] private class SessionEnvelope { public string token; public WorkerUser user; public string error; }
        [Serializable] private class WorkerCard { public string id; public string value; public string pickedBy; }
        [Serializable] private class WorkerPlayer
        {
            public string id; public string name; public string avatarId; public int seat; public bool bot; public bool ready;
            public bool alive = true; public int health; public int maxHealth; public int cardCount;
            public string characterId; public string revealedRole; public string role;
            public List<string> hand = new List<string>(); public List<string> equipment = new List<string>();
            public List<string> characterOptions = new List<string>(); public int attackRange = 1;
        }
        [Serializable] private class WorkerPending
        {
            public string id; public string actorId; public string targetId; public long deadline;
            public int requiredDodges; public string actionType; public string requiredCardType;
            public int requiredHealth; public string currentPickerId;
            public List<string> openedCardIds = new List<string>(); public List<string> choices = new List<string>();
        }
        [Serializable] private class WorkerRoom
        {
            public string id; public string code; public string hostId; public int maxPlayers; public int turnDurationSeconds;
            public string status; public string phase; public List<WorkerPlayer> players = new List<WorkerPlayer>();
            public List<string> discard = new List<string>(); public string currentTurnPlayerId; public int turnNumber;
            public long turnDeadline; public long characterSelectionDeadline;
            public List<WorkerCard> roleDeck = new List<WorkerCard>(); public List<WorkerCard> characterDeck = new List<WorkerCard>();
            public WorkerPending pendingBang; public string winner; public List<string> publicLog = new List<string>();
            public List<string> hand = new List<string>();
        }
        [Serializable] private class RoomEnvelope { public WorkerRoom room; public string error; }
        [Serializable] private class WsEnvelope { public string type; public WorkerRoom room; public string error; public string fromPlayerId; public string payload; public float level; public string playerId; public string playerName; public string message; public long sentAt; }
        [Serializable] private class WorkerRoomSummary
        {
            public string id; public string code; public int maxPlayers; public int turnDurationSeconds;
            public int totalCount; public int botCount; public string status;
        }
        [Serializable] private class RoomListEnvelope { public List<WorkerRoomSummary> rooms = new List<WorkerRoomSummary>(); public string error; }

        public string serverBaseUrl = "https://blue-frog-fec8.hieupham101097.workers.dev";
        public ConnectionState CurrentConnectionState { get; private set; } = ConnectionState.Disconnected;
        public string LocalPlayerId { get; private set; } = "p_local";
        public string CurrentRoomId { get; private set; }

        public event Action<MatchStateSnapshotDTO> OnSnapshotReceived;
        public event Action<InteractionPromptDTO> OnInteractionReceived;
        public event Action<string, string> OnActionRejected;
        public event Action<List<RoomSummaryDTO>> OnRoomListUpdated;
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<string> OnErrorMessage;
        public event Action<ChatMessageDTO> OnChatMessage;
        public event Action<string, string, float> OnVoiceFrame;

        private string _token;
        private ClientWebSocket _socket;
        private CancellationTokenSource _socketCts;
        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        private WorkerRoom _room;
        private int _revision;

        private void Update()
        {
            while (_incoming.TryDequeue(out var raw))
            {
                try
                {
                    var envelope = JsonUtility.FromJson<WsEnvelope>(raw);
                    if (envelope != null && envelope.type == "voice_frame" && !string.IsNullOrEmpty(envelope.payload))
                    {
                        OnVoiceFrame?.Invoke(envelope.fromPlayerId, envelope.payload, envelope.level);
                    }
                    else if (envelope != null && envelope.type == "chat_message" && !string.IsNullOrEmpty(envelope.message))
                    {
                        OnChatMessage?.Invoke(new ChatMessageDTO { playerId = envelope.playerId, playerName = envelope.playerName, message = envelope.message, sentAt = envelope.sentAt });
                    }
                    else if (envelope != null && envelope.room != null)
                    {
                        bool belongsToCurrentRoom = !string.IsNullOrEmpty(CurrentRoomId) &&
                            (envelope.room.id == CurrentRoomId || envelope.room.code == CurrentRoomId);
                        if (belongsToCurrentRoom) Publish(envelope.room);
                    }
                    else if (envelope != null && !string.IsNullOrEmpty(envelope.error) && !string.IsNullOrEmpty(CurrentRoomId))
                        OnErrorMessage?.Invoke(envelope.error);
                }
                catch (Exception exception) { Debug.LogWarning("[CloudflareGateway] Invalid WebSocket message: " + exception.Message); }
            }
        }

        public async Task<bool> InitializeSessionAsync(string deviceId, string displayName)
        {
            LocalPlayerId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString("N") : deviceId;
            SetConnection(ConnectionState.Connecting);
            string body = "{\"deviceId\":\"" + Escape(LocalPlayerId) + "\",\"displayName\":\"" + Escape(displayName) + "\",\"avatarId\":\"" + Escape(BangBang.Core.Data.AvatarCatalog.SelectedId) + "\"}";
            var response = await HttpAsync("POST", "/v1/session", body, false);
            if (!response.ok) { SetConnection(ConnectionState.Disconnected); return Fail("Không thể kết nối Cloudflare: " + response.error); }
            var session = JsonUtility.FromJson<SessionEnvelope>(response.body);
            if (session == null || string.IsNullOrEmpty(session.token)) { SetConnection(ConnectionState.Disconnected); return Fail("Cloudflare không trả token phiên hợp lệ."); }
            _token = session.token.Trim();
            if (session.user != null && !string.IsNullOrEmpty(session.user.id)) LocalPlayerId = session.user.id;
            SetConnection(ConnectionState.Connected);
            return true;
        }

        public async Task<bool> RefreshRoomListAsync()
        {
            if (!await WaitForAuthenticatedSessionAsync()) return false;
            var response = await HttpAsync("GET", "/v1/rooms", null, true);
            if (!response.ok) return Fail(response.error);
            var source = JsonUtility.FromJson<RoomListEnvelope>(response.body);
            OnRoomListUpdated?.Invoke((source?.rooms ?? new List<WorkerRoomSummary>()).Select(room => new RoomSummaryDTO
            {
                roomId = room.id, roomCode = room.code, roomName = "Saloon " + room.code,
                currentPlayers = room.totalCount, maxPlayers = room.maxPlayers,
                turnTimeSeconds = room.turnDurationSeconds, isPrivate = false, pingMs = 0
            }).ToList());
            return true;
        }

        private async Task<bool> WaitForAuthenticatedSessionAsync()
        {
            // Unity can enable a view in the same frame that bootstrap starts the
            // session. Give that in-flight request a short chance to publish its
            // token instead of sending an unauthenticated request to the Worker.
            for (int attempt = 0;
                 attempt < 50 && CurrentConnectionState == ConnectionState.Connecting;
                 attempt++)
            {
                await Task.Delay(100);
            }

            if (CurrentConnectionState == ConnectionState.Connected && !string.IsNullOrWhiteSpace(_token))
                return true;

            Debug.LogWarning("[CloudflareGateway] Room refresh skipped: session is not authenticated yet.");
            return false;
        }

        public async Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool isPrivate, string password, int turnSeconds)
        {
            string body = "{\"maxPlayers\":" + Mathf.Clamp(maxPlayers, 4, 8) + ",\"turnDurationSeconds\":" + Mathf.Clamp(turnSeconds, 20, 120) + "}";
            var response = await HttpAsync("POST", "/v1/rooms", body, true);
            if (!response.ok) return Fail(response.error);
            var envelope = JsonUtility.FromJson<RoomEnvelope>(response.body);
            if (envelope?.room == null) return Fail("Server không trả dữ liệu phòng.");
            CurrentRoomId = envelope.room.id;
            Publish(envelope.room);
            await ConnectRoomSocketAsync();
            return true;
        }

        public async Task<bool> JoinRoomAsync(string roomCodeOrId, string password = "")
        {
            CurrentRoomId = (roomCodeOrId ?? string.Empty).Trim().ToUpperInvariant();
            bool ok = await CommandAsync("join", "{}");
            if (ok) await ConnectRoomSocketAsync(); else CurrentRoomId = null;
            return ok;
        }

        public async Task<bool> LeaveRoomAsync()
        {
            bool ok = await CommandAsync("leave", "{}");
            CurrentRoomId = null; CloseRoomSocket(); return ok;
        }

        public Task<bool> ToggleReadyAsync(bool isReady) => CommandAsync("ready", "{\"ready\":" + (isReady ? "true" : "false") + "}");
        public Task<bool> AddBotAsync() => CommandAsync("add_bot", "{}");
        public Task<bool> RemoveBotAsync()
        {
            string botId = _room?.players?.LastOrDefault(player => player.bot)?.id ?? string.Empty;
            return CommandAsync("remove_bot", "{\"botId\":\"" + Escape(botId) + "\"}");
        }
        public Task<bool> StartGameAsync() => CommandAsync("start", "{}");
        public Task<bool> PickRoleAsync(int slotId) => CommandAsync("choose_role", "{\"cardId\":\"" + Escape(SlotCard(_room?.roleDeck, slotId)) + "\"}");
        public Task<bool> PickCharacterSlotAsync(int slotId) => CommandAsync("take_character_card", "{\"cardId\":\"" + Escape(SlotCard(_room?.characterDeck, slotId)) + "\"}");
        public Task<bool> SelectCharacterAsync(string characterId) => CommandAsync("choose_character", "{\"characterId\":\"" + Escape(characterId) + "\"}");
        public Task<bool> RequestDrawAsync() => CommandAsync("draw", "{}");

        public Task<bool> PlayCardAsync(string cardId, List<string> targetPlayerIds = null, List<string> selectedCardIds = null)
        {
            string target = targetPlayerIds != null && targetPlayerIds.Count > 0 ? targetPlayerIds[0] : string.Empty;
            return CommandAsync("play", "{\"cardId\":\"" + Escape(cardId) + "\",\"targetPlayerId\":\"" + Escape(target) + "\"}");
        }

        public Task<bool> SubmitInteractionAsync(string interactionId, string action, List<string> selectedPlayers = null, List<string> selectedCards = null, int optionIndex = 0)
        {
            var cards = selectedCards ?? new List<string>();
            var pending = _room?.pendingBang;
            if (pending == null || pending.id != interactionId) return Task.FromResult(false);
            string expectedPlayerId = pending.actionType == "general_store" ? pending.currentPickerId :
                (pending.actionType == "kit_carlson" || pending.actionType == "lucky_duke_judgment" ? pending.actorId : pending.targetId);
            if (!string.IsNullOrEmpty(expectedPlayerId) && expectedPlayerId != LocalPlayerId) return Task.FromResult(false);
            string actionType = pending.actionType ?? string.Empty;
            if (actionType == "general_store") return CommandAsync("choose_general_store", "{\"cardId\":\"" + Escape(cards.FirstOrDefault()) + "\"}");
            if (actionType == "rescue") return CommandAsync("rescue", "{\"cardIds\":" + JsonArray(cards) + "}");
            if (actionType == "kit_carlson") return CommandAsync("choose_kit_carlson", "{\"cardIds\":" + JsonArray(cards) + "}");
            if (actionType == "lucky_duke_judgment") return CommandAsync("choose_lucky_duke", "{\"resultCardId\":\"" + Escape(cards.FirstOrDefault()) + "\"}");
            bool pass = string.Equals(action, "PASS", StringComparison.OrdinalIgnoreCase) || string.Equals(action, "CANCEL", StringComparison.OrdinalIgnoreCase) || cards.Count == 0;
            return CommandAsync("respond_bang", "{\"response\":\"" + (pass ? "damage" : "card") + "\",\"cardId\":\"" + Escape(cards.FirstOrDefault()) + "\",\"cardIds\":" + JsonArray(cards) + "}");
        }

        public async Task<bool> UpdateAvatarAsync(string avatarId)
        {
            BangBang.Core.Data.AvatarCatalog.SelectedId = avatarId;
            return await InitializeSessionAsync(LocalPlayerId, "Cao bồi viễn tây");
        }

        public Task<bool> EndTurnAsync(List<string> discardCardIds = null)
        {
            if (discardCardIds != null && discardCardIds.Count > 0) return CommandAsync("discard", "{\"cardIds\":" + JsonArray(discardCardIds) + "}");
            return CommandAsync("end_turn", "{}");
        }
        public Task<bool> RequestRematchAsync() { OnErrorMessage?.Invoke("Cloudflare chưa hỗ trợ rematch."); return Task.FromResult(false); }
        public async Task<bool> SendChatAsync(string message)
        {
            string clean = (message ?? string.Empty).Trim();
            if (_socket == null || _socket.State != WebSocketState.Open || clean.Length == 0) return false;
            if (clean.Length > 240) clean = clean.Substring(0, 240);
            string json = "{\"action\":\"chat_send\",\"message\":\"" + Escape(clean) + "\"}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _socketCts.Token);
                return true;
            }
            catch { return false; }
        }

        private async Task<bool> CommandAsync(string action, string payload)
        {
            if (string.IsNullOrEmpty(CurrentRoomId)) return Fail("Chưa vào phòng.");
            string body = "{\"action\":\"" + Escape(action) + "\",\"payload\":" + (string.IsNullOrEmpty(payload) ? "{}" : payload) + "}";
            var response = await HttpAsync("POST", "/v1/rooms/" + CurrentRoomId, body, true);
            if (!response.ok)
            {
                bool expiredInteraction = (action == "respond_bang" || action == "rescue" || action.StartsWith("choose_")) &&
                    !string.IsNullOrEmpty(response.error) &&
                    (response.error.Contains("Không có phản ứng hợp lệ") || response.error.Contains("hợp lệ"));
                if (expiredInteraction)
                {
                    await RefreshCurrentRoomSnapshotAsync();
                    return false;
                }
                return Fail(response.error);
            }
            var envelope = JsonUtility.FromJson<RoomEnvelope>(response.body);
            if (envelope?.room == null) return Fail("Phản hồi phòng không hợp lệ.");
            Publish(envelope.room); return true;
        }

        private async Task RefreshCurrentRoomSnapshotAsync()
        {
            if (string.IsNullOrEmpty(CurrentRoomId)) return;
            var latest = await HttpAsync("GET", "/v1/rooms/" + CurrentRoomId, null, true);
            if (!latest.ok) return;
            var envelope = JsonUtility.FromJson<RoomEnvelope>(latest.body);
            if (envelope?.room != null) Publish(envelope.room);
        }

        private void Publish(WorkerRoom room)
        {
            _room = room;
            var snapshot = Convert(room);
            OnSnapshotReceived?.Invoke(snapshot);
        }

        private MatchStateSnapshotDTO Convert(WorkerRoom room)
        {
            _revision++;
            var me = room.players?.FirstOrDefault(player => player.id == LocalPlayerId);
            var snapshot = new MatchStateSnapshotDTO
            {
                roomId = room.id, roomCode = room.code, hostPlayerId = room.hostId,
                state = MapState(room), currentPhase = room.phase, currentTurnPlayerId = room.currentTurnPlayerId,
                turnNumber = room.turnNumber,
                deadlineAt = room.pendingBang?.deadline > 0 ? room.pendingBang.deadline : (room.characterSelectionDeadline > 0 ? room.characterSelectionDeadline : room.turnDeadline),
                draftSlotCount = room.phase == "role_selection" ? room.roleDeck?.Count ?? 0 : room.characterDeck?.Count ?? 0,
                lockedDraftSlots = LockedSlots(room.phase == "role_selection" ? room.roleDeck : room.characterDeck),
                privateState = new PrivatePlayerState
                {
                    roleId = me?.role, hand = room.hand ?? me?.hand ?? new List<string>(),
                    draftRoleSlot = FindPickedSlot(room.roleDeck, LocalPlayerId),
                    draftCharacterOptions = me?.characterOptions ?? new List<string>(), selectedCharacterId = me?.characterId
                },
                discardPileCount = room.discard?.Count ?? 0,
                topDiscardCardId = room.discard != null && room.discard.Count > 0 ? room.discard[room.discard.Count - 1] : null,
                winnerTeam = room.winner, combatLogs = room.publicLog ?? new List<string>(),
                serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence = _revision, revision = _revision,
                rules = new RuleConfig { maxPlayers = room.maxPlayers, turnTimeSec = room.turnDurationSeconds, startingHandMode = "FIXED_7", roleDraftSec = 20, characterDraftSec = 30, responseTimeSec = 10 }
            };
            snapshot.players = (room.players ?? new List<WorkerPlayer>()).Select(player => new PlayerSnapshotDTO
            {
                id = player.id, name = player.name, avatarId = player.avatarId, seat = player.seat, isBot = player.bot,
                isHost = player.id == room.hostId, isReady = player.ready || player.bot || player.id == room.hostId,
                isConnected = true, isAlive = player.alive, currentHealth = player.health, maxHealth = player.maxHealth,
                characterId = player.characterId, publicRoleId = player.revealedRole,
                isRoleRevealed = !string.IsNullOrEmpty(player.revealedRole), handCount = player.cardCount,
                equipment = player.equipment ?? new List<string>(), effectiveDistanceToLocal = 1
            }).ToList();
            snapshot.activeInteraction = room.phase == "waiting_response" ? ConvertInteraction(room.pendingBang) : null;
            return snapshot;
        }

        private static ServerGameState MapState(WorkerRoom room)
        {
            switch (room.phase)
            {
                case "role_selection": return ServerGameState.ROLE_DRAFT;
                case "role_reveal": return ServerGameState.ROLE_LOCK_WAIT;
                case "character_selection": case "choosing_character": return ServerGameState.CHARACTER_DRAFT;
                case "match_intro": return ServerGameState.INITIAL_DEAL;
                case "turn_start": return ServerGameState.TURN_START;
                case "play_phase": return ServerGameState.PLAY;
                case "waiting_response": return ServerGameState.RESPONSE;
                case "discard_phase": return ServerGameState.DISCARD;
                case "game_over": return ServerGameState.GAME_OVER;
                default: return ServerGameState.WAITING;
            }
        }

        private static InteractionPromptDTO ConvertInteraction(WorkerPending pending)
        {
            if (pending == null) return null;
            string actorId = pending.actionType == "general_store" ? pending.currentPickerId :
                (pending.actionType == "kit_carlson" || pending.actionType == "lucky_duke_judgment" ? pending.actorId : pending.targetId);
            return new InteractionPromptDTO
            {
                interactionId = pending.id, type = "RESPOND", actorPlayerId = actorId,
                title = pending.actionType ?? "Phản ứng", message = "Chọn phản ứng hợp lệ hoặc Bỏ qua",
                requiredCount = Math.Max(1, pending.requiredDodges), requiredCardType = pending.requiredCardType,
                validCardIds = pending.choices ?? pending.openedCardIds ?? new List<string>(), expiresAt = pending.deadline,
                canCancel = true, defaultAction = "PASS"
            };
        }

        private async Task ConnectRoomSocketAsync()
        {
            CloseRoomSocket();
            if (string.IsNullOrEmpty(CurrentRoomId) || string.IsNullOrEmpty(_token)) return;
            try
            {
                _socketCts = new CancellationTokenSource(); _socket = new ClientWebSocket();
                string wsBase = serverBaseUrl.TrimEnd('/').Replace("https://", "wss://").Replace("http://", "ws://");
                string url = wsBase + "/v1/rooms/" + CurrentRoomId + "/ws?token=" + UnityWebRequest.EscapeURL(_token);
                await _socket.ConnectAsync(new Uri(url), _socketCts.Token); _ = ReceiveLoopAsync();
            }
            catch (Exception exception) { Debug.LogWarning("[CloudflareGateway] Realtime unavailable, HTTP commands remain active: " + exception.Message); }
        }

        public async Task<bool> SendVoiceFrameAsync(string base64Pcm16, float level)
        {
            if (_socket == null || _socket.State != WebSocketState.Open || string.IsNullOrEmpty(base64Pcm16)) return false;
            string json = "{\"action\":\"voice_frame\",\"payload\":\"" + base64Pcm16 + "\",\"level\":" + level.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _socketCts.Token);
                return true;
            }
            catch { return false; }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (_socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _socketCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    _incoming.Enqueue(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
            }
            catch when (_socketCts == null || _socketCts.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogWarning("[CloudflareGateway] Realtime disconnected: " + exception.Message); }
        }

        private void CloseRoomSocket()
        {
            try { _socketCts?.Cancel(); _socket?.Dispose(); } catch { }
            _socketCts = null; _socket = null;
            while (_incoming.TryDequeue(out _)) { }
            _room = null;
        }

        private struct HttpResult { public bool ok; public string body; public string error; }
        private Task<HttpResult> HttpAsync(string method, string path, string body, bool authenticated)
        {
            var completion = new TaskCompletionSource<HttpResult>();
            StartCoroutine(HttpCoroutine(method, path, body, authenticated, completion)); return completion.Task;
        }
        private IEnumerator HttpCoroutine(string method, string path, string body, bool authenticated, TaskCompletionSource<HttpResult> completion)
        {
            string requestUrl = serverBaseUrl.TrimEnd('/') + path;
            if (authenticated && !string.IsNullOrEmpty(_token))
            {
                requestUrl += (requestUrl.Contains("?") ? "&" : "?") + "token=" + UnityWebRequest.EscapeURL(_token);
            }
            using (var request = new UnityWebRequest(requestUrl, method))
            {
                if (body != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated) request.SetRequestHeader("Authorization", "Bearer " + _token);
                yield return request.SendWebRequest();
                string text = request.downloadHandler?.text ?? string.Empty;
                completion.TrySetResult(new HttpResult { ok = request.result == UnityWebRequest.Result.Success, body = text, error = request.result == UnityWebRequest.Result.Success ? null : ExtractError(text, request.error) });
            }
        }

        private bool Fail(string error) { OnErrorMessage?.Invoke(error); return false; }
        private void SetConnection(ConnectionState state) { CurrentConnectionState = state; OnConnectionStateChanged?.Invoke(state); }
        private static string SlotCard(List<WorkerCard> cards, int index) => cards != null && index >= 0 && index < cards.Count ? cards[index].id : string.Empty;
        private static List<int> LockedSlots(List<WorkerCard> cards) { var result = new List<int>(); if (cards != null) for (int i = 0; i < cards.Count; i++) if (!string.IsNullOrEmpty(cards[i].pickedBy)) result.Add(i); return result; }
        private static int FindPickedSlot(List<WorkerCard> cards, string playerId)
        {
            if (cards == null || string.IsNullOrEmpty(playerId)) return -1;
            return cards.FindIndex(card => card != null && card.pickedBy == playerId);
        }
        private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string JsonArray(IEnumerable<string> values) => "[" + string.Join(",", (values ?? Array.Empty<string>()).Select(value => "\"" + Escape(value) + "\"")) + "]";
        private static string ExtractError(string json, string fallback) { try { var value = JsonUtility.FromJson<RoomEnvelope>(json); if (!string.IsNullOrEmpty(value?.error)) return value.error; } catch { } return string.IsNullOrEmpty(fallback) ? "Yêu cầu Cloudflare thất bại." : fallback; }
        private void OnDestroy() { CloseRoomSocket(); }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BangBang.Core.Network
{
    public class BangMockGateway : MonoBehaviour, IGameGateway
    {
        public ConnectionState CurrentConnectionState { get; private set; } = ConnectionState.Connected;
        public string LocalPlayerId { get; private set; } = "p_local";

        public event Action<MatchStateSnapshotDTO> OnSnapshotReceived;
        public event Action<InteractionPromptDTO> OnInteractionReceived;
        public event Action<string, string> OnActionRejected;
        public event Action<List<RoomSummaryDTO>> OnRoomListUpdated;
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<string> OnErrorMessage;

        private MatchStateSnapshotDTO _currentSnapshot;
        private Coroutine _mockGameLoopCoroutine;

        public Task<bool> InitializeSessionAsync(string deviceId, string displayName)
        {
            LocalPlayerId = string.IsNullOrEmpty(deviceId) ? "p_local" : deviceId;
            CurrentConnectionState = ConnectionState.Connected;
            OnConnectionStateChanged?.Invoke(CurrentConnectionState);
            return Task.FromResult(true);
        }

        public Task<bool> RefreshRoomListAsync()
        {
            var mockRooms = new List<RoomSummaryDTO>
            {
                new RoomSummaryDTO { roomId = "r_saloon_1", roomName = "🤠 Quán Rượu Saloon #01", roomCode = "SALOON", currentPlayers = 4, maxPlayers = 7, isPrivate = false, turnTimeSeconds = 30, pingMs = 20 },
                new RoomSummaryDTO { roomId = "r_desert_2", roomName = "🌵 Đấu Trường Cát Cháy", roomCode = "DESERT", currentPlayers = 5, maxPlayers = 7, isPrivate = false, turnTimeSeconds = 25, pingMs = 35 },
                new RoomSummaryDTO { roomId = "r_sheriff_3", roomName = "⭐ Trụ Sở Cảnh Sát Trưởng", roomCode = "SHERIFF", currentPlayers = 6, maxPlayers = 7, isPrivate = true, turnTimeSeconds = 30, pingMs = 15 }
            };
            OnRoomListUpdated?.Invoke(mockRooms);
            return Task.FromResult(true);
        }

        public Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool isPrivate, string password, int turnSeconds)
        {
            _currentSnapshot = new MatchStateSnapshotDTO
            {
                roomId = "r_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                roomCode = "BANG" + UnityEngine.Random.Range(100, 999),
                hostPlayerId = LocalPlayerId,
                state = ServerGameState.WAITING,
                players = new List<PlayerSnapshotDTO>
                {
                    new PlayerSnapshotDTO { id = LocalPlayerId, name = "Cao bồi của bạn", seat = 0, isHost = true, isReady = true, currentHealth = 4, maxHealth = 4 }
                },
                serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = 1
            };

            // Add 4 Mock Bots
            string[] botNames = { "Bill Độc Nhãn", "Apache Jack", "Django Nhanh Nhẹn", "Doc Holliday" };
            for (int i = 0; i < botNames.Length; i++)
            {
                _currentSnapshot.players.Add(new PlayerSnapshotDTO
                {
                    id = "bot_" + (i + 1),
                    name = botNames[i],
                    seat = i + 1,
                    isHost = false,
                    isReady = true,
                    currentHealth = 4,
                    maxHealth = 4
                });
            }

            BroadcastSnapshot();
            return Task.FromResult(true);
        }

        public Task<bool> JoinRoomAsync(string roomCodeOrId, string password = "")
        {
            return CreateRoomAsync("Phòng Saloon #" + roomCodeOrId, 7, false, "", 30);
        }

        public Task<bool> LeaveRoomAsync()
        {
            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _currentSnapshot = null;
            return Task.FromResult(true);
        }

        public Task<bool> ToggleReadyAsync(bool isReady)
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local != null)
            {
                local.isReady = isReady;
                BroadcastSnapshot();
            }
            return Task.FromResult(true);
        }

        public Task<bool> StartGameAsync()
        {
            if (_currentSnapshot == null) return Task.FromResult(false);
            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _mockGameLoopCoroutine = StartCoroutine(MockGameLifecycleCoroutine());
            return Task.FromResult(true);
        }

        private IEnumerator MockGameLifecycleCoroutine()
        {
            // 1. DEALING_ROLES Phase
            _currentSnapshot.state = ServerGameState.DEALING_ROLES;
            string[] roles = { "sheriff", "outlaw", "outlaw", "deputy", "renegade" };
            for (int i = 0; i < _currentSnapshot.players.Count; i++)
            {
                var p = _currentSnapshot.players[i];
                string r = roles[i % roles.Length];
                if (p.id == LocalPlayerId)
                {
                    p.role = r;
                }
                if (r == "sheriff")
                {
                    p.isRoleRevealed = true;
                    p.role = "sheriff";
                    p.maxHealth = 5;
                    p.currentHealth = 5;
                }
            }
            BroadcastSnapshot();
            yield return new WaitForSeconds(3.5f);

            // 2. SELECTING_CHARACTER Phase
            _currentSnapshot.state = ServerGameState.SELECTING_CHARACTER;
            _currentSnapshot.activeInteraction = new InteractionPromptDTO
            {
                interactionId = Guid.NewGuid().ToString(),
                type = "CHOOSE_OPTION",
                actorPlayerId = LocalPlayerId,
                title = "CHỌN TƯỚNG BẮT ĐẦU",
                message = "Chọn 1 trong 2 thẻ bài tướng ngẫu nhiên:",
                options = new List<string> { "willy_the_kid", "calamity_janet" },
                expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15000,
                canCancel = false,
                defaultAction = "willy_the_kid"
            };
            BroadcastSnapshot();
        }

        public Task<bool> SelectCharacterAsync(string characterId)
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local != null)
            {
                local.characterId = characterId;
                _currentSnapshot.activeInteraction = null;
            }

            // Assign Bot characters
            string[] botChars = { "bart_cassidy", "black_jack", "el_gringo", "kit_carlson", "rose_oolan" };
            for (int i = 0; i < _currentSnapshot.players.Count; i++)
            {
                if (_currentSnapshot.players[i].id != LocalPlayerId)
                {
                    _currentSnapshot.players[i].characterId = botChars[i % botChars.Length];
                }
            }

            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _mockGameLoopCoroutine = StartCoroutine(StartBattleAfterSelectionCoroutine());
            return Task.FromResult(true);
        }

        private IEnumerator StartBattleAfterSelectionCoroutine()
        {
            // 3. INITIALIZING Phase (Dealing cards & health)
            _currentSnapshot.state = ServerGameState.INITIALIZING;
            _currentSnapshot.drawPileCount = 65;
            _currentSnapshot.discardPileCount = 1;
            _currentSnapshot.topDiscardCardId = "duello_spade_7";

            var local = _currentSnapshot.players.Find(p => p.id == LocalPlayerId);
            if (local != null)
            {
                local.hand = new List<string> { "bang_heart_a", "bang_diamond_k", "beer_heart_6", "mustang_heart_8", "gun_range_2_club_j" };
                local.handCount = local.hand.Count;
            }

            for (int i = 0; i < _currentSnapshot.players.Count; i++)
            {
                if (_currentSnapshot.players[i].id != LocalPlayerId)
                {
                    _currentSnapshot.players[i].handCount = _currentSnapshot.players[i].currentHealth;
                    _currentSnapshot.players[i].equipment = new List<string>();
                }
            }

            BroadcastSnapshot();
            yield return new WaitForSeconds(2.0f);

            // 4. PLAYING Phase (Start with Sheriff or Local)
            var sheriff = _currentSnapshot.players.Find(p => p.role == "sheriff");
            _currentSnapshot.state = ServerGameState.PLAYING;
            _currentSnapshot.currentTurnPlayerId = sheriff != null ? sheriff.id : LocalPlayerId;
            _currentSnapshot.currentPhase = "play";
            _currentSnapshot.turnNumber = 1;
            _currentSnapshot.combatLogs.Add("Trận đấu bắt đầu! Cảnh Trưởng đi đầu.");

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();
        }

        public Task<bool> RequestDrawAsync()
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local != null)
            {
                local.hand.Add("bang_spade_8");
                local.hand.Add("ne_diamond_10");
                local.handCount = local.hand.Count;
                _currentSnapshot.drawPileCount -= 2;
                _currentSnapshot.currentPhase = "play";
                _currentSnapshot.combatLogs.Add("Bạn đã rút 2 lá bài mới.");
                BroadcastSnapshot();
            }
            return Task.FromResult(true);
        }

        public Task<bool> PlayCardAsync(string cardId, List<string> targetPlayerIds = null, List<string> selectedCardIds = null)
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local == null) return Task.FromResult(false);

            local.hand.Remove(cardId);
            local.handCount = local.hand.Count;
            _currentSnapshot.topDiscardCardId = cardId;
            _currentSnapshot.discardPileCount++;

            string type = cardId.Split('_')[0].ToLower();

            if (type == "beer")
            {
                local.currentHealth = Mathf.Min(local.currentHealth + 1, local.maxHealth);
                _currentSnapshot.combatLogs.Add("Bạn đã uống Bia và hồi 1 Máu.");
                BroadcastSnapshot();
            }
            else if (type == "mustang" || type == "gun" || type == "barrel" || type == "volcanic")
            {
                local.equipment.Add(cardId);
                _currentSnapshot.combatLogs.Add("Bạn đã trang bị " + cardId.ToUpper() + ".");
                UpdateDistancesAndTargetables();
                BroadcastSnapshot();
            }
            else if (type == "bang" && targetPlayerIds != null && targetPlayerIds.Count > 0)
            {
                string targetId = targetPlayerIds[0];
                var target = _currentSnapshot.players.Find(p => p.id == targetId);
                if (target != null)
                {
                    _currentSnapshot.combatLogs.Add("Bạn đánh BANG! nhắm vào " + target.name + "!");
                    target.currentHealth = Mathf.Max(0, target.currentHealth - 1);
                    if (target.currentHealth == 0)
                    {
                        target.isAlive = false;
                        target.isRoleRevealed = true;
                        target.role = "outlaw";
                        _currentSnapshot.combatLogs.Add(target.name + " đã bị hạ gục!");
                    }
                    BroadcastSnapshot();
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> SubmitInteractionAsync(string interactionId, string action, List<string> selectedPlayers = null, List<string> selectedCards = null, int optionIndex = 0)
        {
            _currentSnapshot.activeInteraction = null;
            BroadcastSnapshot();
            return Task.FromResult(true);
        }

        public Task<bool> EndTurnAsync(List<string> discardCardIds = null)
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local != null && discardCardIds != null)
            {
                foreach (var c in discardCardIds) local.hand.Remove(c);
                local.handCount = local.hand.Count;
            }

            // Move to next live bot
            int myIdx = _currentSnapshot.players.FindIndex(p => p.id == LocalPlayerId);
            int nextIdx = (myIdx + 1) % _currentSnapshot.players.Count;
            _currentSnapshot.currentTurnPlayerId = _currentSnapshot.players[nextIdx].id;
            _currentSnapshot.turnNumber++;
            _currentSnapshot.combatLogs.Add("Chuyển lượt cho " + _currentSnapshot.players[nextIdx].name + ".");
            BroadcastSnapshot();

            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _mockGameLoopCoroutine = StartCoroutine(BotTurnCycleCoroutine());
            return Task.FromResult(true);
        }

        private IEnumerator BotTurnCycleCoroutine()
        {
            yield return new WaitForSeconds(3.0f);
            // Bot plays turn then passes back to local
            _currentSnapshot.currentTurnPlayerId = LocalPlayerId;
            _currentSnapshot.currentPhase = "play";
            _currentSnapshot.combatLogs.Add("Đến lượt của bạn!");
            BroadcastSnapshot();
        }

        public Task<bool> RequestRematchAsync()
        {
            return StartGameAsync();
        }

        private void UpdateDistancesAndTargetables()
        {
            if (_currentSnapshot == null) return;
            var local = _currentSnapshot.players.Find(p => p.id == LocalPlayerId);
            if (local == null) return;

            var alive = _currentSnapshot.players.Where(p => p.isAlive).ToList();
            int localAliveIdx = alive.FindIndex(p => p.id == LocalPlayerId);

            for (int i = 0; i < alive.Count; i++)
            {
                var p = alive[i];
                if (p.id == LocalPlayerId)
                {
                    p.effectiveDistanceToLocal = 0;
                    p.isTargetable = false;
                }
                else
                {
                    int cw = Mathf.Abs(i - localAliveIdx);
                    int ccw = alive.Count - cw;
                    int baseDist = Mathf.Min(cw, ccw);
                    p.effectiveDistanceToLocal = baseDist;
                    p.isTargetable = baseDist <= 2;
                }
            }
        }

        private void BroadcastSnapshot()
        {
            if (_currentSnapshot != null)
            {
                _currentSnapshot.serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _currentSnapshot.sequence++;
                OnSnapshotReceived?.Invoke(_currentSnapshot);
            }
        }
    }
}

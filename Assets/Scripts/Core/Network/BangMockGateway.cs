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

        private static readonly string[] _cardPool = {
            "bang_heart_a", "bang_heart_2", "bang_heart_3", "bang_diamond_k", "bang_diamond_q",
            "bang_spade_8", "bang_spade_9", "bang_club_j", "bang_club_10",
            "ne_diamond_10", "ne_diamond_j", "ne_heart_9", "ne_spade_q",
            "beer_heart_6", "beer_heart_7", "beer_heart_8",
            "saloon_heart_q", "gatling_heart_10", "duello_spade_7",
            "mustang_heart_8", "mustang_heart_9",
            "gun_range_2_club_j", "gun_range_3_spade_9",
            "barrel_spade_q"
        };

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
                    new PlayerSnapshotDTO { id = LocalPlayerId, name = "Cao bồi của bạn", seat = 0, isHost = true, isReady = true, isAlive = true, currentHealth = 4, maxHealth = 4 }
                },
                serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = 1
            };

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
                    isAlive = true,
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
            // ── Phase 1: DEALING ROLES ──────────────────────────────
            _currentSnapshot.state = ServerGameState.DEALING_ROLES;
            _currentSnapshot.activeInteraction = null;

            // Shuffle roles properly using Fisher-Yates
            string[] rolePool = { "sheriff", "outlaw", "outlaw", "deputy", "renegade" };
            System.Random rng = new System.Random();
            for (int i = rolePool.Length - 1; i > 0; i--)
            {
                int k = rng.Next(i + 1);
                string temp = rolePool[i];
                rolePool[i] = rolePool[k];
                rolePool[k] = temp;
            }
            var shuffled = rolePool;

            for (int i = 0; i < _currentSnapshot.players.Count; i++)
            {
                var p = _currentSnapshot.players[i];
                string r = shuffled[i % shuffled.Length];
                p.role = r;
                p.isAlive = true;
                if (r == "sheriff")
                {
                    p.isRoleRevealed = true;
                    p.maxHealth = 5;
                    p.currentHealth = 5;
                }
                else
                {
                    p.isRoleRevealed = false;
                    p.maxHealth = 4;
                    p.currentHealth = 4;
                }
            }
            // Always reveal local player's own role
            var localP = _currentSnapshot.players.Find(p => p.id == LocalPlayerId);
            if (localP != null) localP.isRoleRevealed = true;

            BroadcastSnapshot();
            yield return new WaitForSeconds(4.5f); // Let role reveal animation play

            // ── Phase 2: SELECTING CHARACTER ────────────────────────
            _currentSnapshot.state = ServerGameState.SELECTING_CHARACTER;
            _currentSnapshot.activeInteraction = new InteractionPromptDTO
            {
                interactionId = Guid.NewGuid().ToString(),
                type = "CHOOSE_OPTION",
                actorPlayerId = LocalPlayerId,
                title = "CHỌN NHÂN VẬT BẮT ĐẦU TRẬN",
                message = "Chọn 1 trong 2 thẻ nhân vật bốc ngẫu nhiên:",
                options = new List<string> { "willy_the_kid", "calamity_janet" },
                expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15000,
                canCancel = false,
                defaultAction = "willy_the_kid"
            };
            BroadcastSnapshot();

            // Wait up to 15s for player selection; then auto-select default
            float elapsed = 0f;
            while (_currentSnapshot.state == ServerGameState.SELECTING_CHARACTER && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_currentSnapshot.state == ServerGameState.SELECTING_CHARACTER)
            {
                // Auto-select default character
                ApplyCharacterSelectionInternal("willy_the_kid");
                if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
                _mockGameLoopCoroutine = StartCoroutine(StartBattleAfterSelectionCoroutine());
            }
        }

        private void ApplyCharacterSelectionInternal(string characterId)
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local != null)
            {
                local.characterId = characterId;
                _currentSnapshot.activeInteraction = null;
            }
            string[] botChars = { "bart_cassidy", "black_jack", "el_gringo", "kit_carlson", "rose_oolan" };
            int bi = 0;
            foreach (var p in _currentSnapshot.players)
            {
                if (p.id != LocalPlayerId)
                {
                    p.characterId = botChars[bi % botChars.Length];
                    bi++;
                }
            }
        }

        public Task<bool> SelectCharacterAsync(string characterId)
        {
            ApplyCharacterSelectionInternal(characterId);
            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _mockGameLoopCoroutine = StartCoroutine(StartBattleAfterSelectionCoroutine());
            return Task.FromResult(true);
        }

        private IEnumerator StartBattleAfterSelectionCoroutine()
        {
            // ── Phase 3: INITIALIZING ───────────────────────────────
            _currentSnapshot.state = ServerGameState.INITIALIZING;
            _currentSnapshot.activeInteraction = null;

            var deckList = _cardPool.OrderBy(_ => UnityEngine.Random.value).ToList();
            while (deckList.Count < 80) deckList.AddRange(_cardPool.OrderBy(_ => UnityEngine.Random.value));

            int deckIdx = 0;
            foreach (var p in _currentSnapshot.players)
            {
                p.isAlive = true;
                p.hand = new List<string>();
                p.equipment = new List<string>();
                for (int c = 0; c < p.maxHealth && deckIdx < deckList.Count; c++, deckIdx++)
                    p.hand.Add(deckList[deckIdx]);
                p.handCount = p.hand.Count;
            }

            _currentSnapshot.drawPileCount = deckList.Count - deckIdx;
            _currentSnapshot.discardPileCount = 0;
            _currentSnapshot.topDiscardCardId = "";
            _currentSnapshot.combatLogs = new List<string> { "⚙️ Đang chia bài và thiết lập trận..." };

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();
            yield return new WaitForSeconds(1.5f);

            // ── Phase 4: PLAYING (Sheriff first, DRAW phase) ────────
            var sheriff = _currentSnapshot.players.Find(p => p.role == "sheriff");
            string firstId = sheriff != null ? sheriff.id : LocalPlayerId;

            _currentSnapshot.state = ServerGameState.PLAYING;
            _currentSnapshot.currentTurnPlayerId = firstId;
            _currentSnapshot.currentPhase = "draw";   // ← DRAW first!
            _currentSnapshot.turnNumber = 1;
            _currentSnapshot.combatLogs.Add("🔥 Trận đấu bắt đầu! " + (sheriff?.name ?? "Người đầu tiên") + " đi lượt đầu tiên.");
            _currentSnapshot.combatLogs.Add(firstId == LocalPlayerId
                ? "👉 Đến lượt của bạn! Bấm RÚT BÀI để bắt đầu."
                : "⏳ " + (sheriff?.name ?? "Bot") + " đang đi...");

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();

            if (firstId != LocalPlayerId)
                yield return StartCoroutine(RunAllBotTurnsUntilLocal());
        }

        public Task<bool> RequestDrawAsync()
        {
            var local = _currentSnapshot?.players.Find(p => p.id == LocalPlayerId);
            if (local == null || _currentSnapshot.currentTurnPlayerId != LocalPlayerId)
                return Task.FromResult(false);

            // Draw 2 random cards from pool
            local.hand.Add(_cardPool[UnityEngine.Random.Range(0, _cardPool.Length)]);
            local.hand.Add(_cardPool[UnityEngine.Random.Range(0, _cardPool.Length)]);
            local.handCount = local.hand.Count;
            _currentSnapshot.drawPileCount = Mathf.Max(0, _currentSnapshot.drawPileCount - 2);
            _currentSnapshot.currentPhase = "play";
            _currentSnapshot.combatLogs.Add("🃏 Bạn rút 2 lá bài. Hãy đánh bài hoặc kết thúc lượt.");

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();
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
                _currentSnapshot.combatLogs.Add("🍺 Bạn uống Bia và hồi 1 Máu (" + local.currentHealth + "/" + local.maxHealth + ").");
            }
            else if (type == "saloon")
            {
                foreach (var p in _currentSnapshot.players.Where(p2 => p2.isAlive))
                    p.currentHealth = Mathf.Min(p.currentHealth + 1, p.maxHealth);
                _currentSnapshot.combatLogs.Add("🍸 Saloon! Tất cả mọi người hồi 1 Máu.");
            }
            else if (type == "gatling")
            {
                _currentSnapshot.combatLogs.Add("💥 GATLING! Bạn bắn vào tất cả đối thủ!");
                foreach (var p in _currentSnapshot.players.Where(p2 => p2.id != LocalPlayerId && p2.isAlive).ToList())
                {
                    bool dodged = UnityEngine.Random.value > 0.65f;
                    if (!dodged) { p.currentHealth = Mathf.Max(0, p.currentHealth - 1); _currentSnapshot.combatLogs.Add("  💢 " + p.name + " còn " + p.currentHealth + " máu."); if (p.currentHealth <= 0) KillPlayer(p, local); }
                    else _currentSnapshot.combatLogs.Add("  🛡️ " + p.name + " né được!");
                }
            }
            else if (type == "mustang" || type == "gun" || type == "barrel" || type == "volcanic")
            {
                local.equipment.Add(cardId);
                _currentSnapshot.combatLogs.Add("🔧 Bạn trang bị " + cardId.Replace("_", " ").ToUpper() + ".");
                UpdateDistancesAndTargetables();
            }
            else if (type == "bang" && targetPlayerIds != null && targetPlayerIds.Count > 0)
            {
                string targetId = targetPlayerIds[0];
                var target = _currentSnapshot.players.Find(p => p.id == targetId);
                if (target != null && target.isAlive)
                {
                    _currentSnapshot.combatLogs.Add("🔫 Bạn BANG! vào " + target.name + "!");
                    bool dodged = UnityEngine.Random.value > 0.55f;
                    if (!dodged) { target.currentHealth = Mathf.Max(0, target.currentHealth - 1); _currentSnapshot.combatLogs.Add("💢 " + target.name + " trúng đạn! Còn " + target.currentHealth + " máu."); if (target.currentHealth <= 0) KillPlayer(target, local); }
                    else _currentSnapshot.combatLogs.Add("🛡️ " + target.name + " né tránh được!");
                }
            }
            else if (type == "duello" && targetPlayerIds != null && targetPlayerIds.Count > 0)
            {
                var target = _currentSnapshot.players.Find(p => p.id == targetPlayerIds[0]);
                if (target != null && target.isAlive)
                {
                    _currentSnapshot.combatLogs.Add("⚔️ DUELLO! Bạn đấu tay đôi với " + target.name + "!");
                    if (UnityEngine.Random.value > 0.4f) { target.currentHealth = Mathf.Max(0, target.currentHealth - 1); _currentSnapshot.combatLogs.Add("💢 " + target.name + " thua! Còn " + target.currentHealth + " máu."); if (target.currentHealth <= 0) KillPlayer(target, local); }
                    else { local.currentHealth = Mathf.Max(0, local.currentHealth - 1); _currentSnapshot.combatLogs.Add("💢 Bạn thua cuộc! Còn " + local.currentHealth + " máu."); if (local.currentHealth <= 0) { local.isAlive = false; local.isRoleRevealed = true; _currentSnapshot.combatLogs.Add("💀 Bạn bị hạ gục!"); CheckGameOver(); } }
                }
            }
            else
            {
                _currentSnapshot.combatLogs.Add("🃏 Bạn đánh lá bài " + cardId.ToUpper() + ".");
            }

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();
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
                foreach (var c in discardCardIds) { local.hand.Remove(c); _currentSnapshot.discardPileCount++; }
                local.handCount = local.hand.Count;
            }

            _currentSnapshot.combatLogs.Add("✅ Bạn kết thúc lượt.");

            // Advance to next alive player
            int myIdx = _currentSnapshot.players.FindIndex(p => p.id == LocalPlayerId);
            int total = _currentSnapshot.players.Count;
            int nextIdx = (myIdx + 1) % total;
            int safety = 0;
            while (!_currentSnapshot.players[nextIdx].isAlive && safety < total) { nextIdx = (nextIdx + 1) % total; safety++; }

            _currentSnapshot.currentTurnPlayerId = _currentSnapshot.players[nextIdx].id;
            _currentSnapshot.currentPhase = "draw";
            _currentSnapshot.turnNumber++;
            _currentSnapshot.combatLogs.Add("▶️ Đến lượt: " + _currentSnapshot.players[nextIdx].name + ".");

            UpdateDistancesAndTargetables();
            BroadcastSnapshot();

            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            _mockGameLoopCoroutine = StartCoroutine(RunAllBotTurnsUntilLocal());
            return Task.FromResult(true);
        }

        // ── BOT AI LOOP ─────────────────────────────────────────────
        private IEnumerator RunAllBotTurnsUntilLocal()
        {
            int safety = 0;
            while (_currentSnapshot.currentTurnPlayerId != LocalPlayerId && safety < 20)
            {
                safety++;
                var bot = _currentSnapshot.players.Find(p => p.id == _currentSnapshot.currentTurnPlayerId);
                if (bot == null || !bot.isAlive) { AdvanceToNextPlayer(); continue; }

                yield return StartCoroutine(RunOneBotTurn(bot));
                AdvanceToNextPlayer();
                yield return new WaitForSeconds(0.4f);
            }

            if (_currentSnapshot.currentTurnPlayerId == LocalPlayerId)
            {
                _currentSnapshot.currentPhase = "draw";
                _currentSnapshot.combatLogs.Add("👉 Đến lượt của bạn! Bấm RÚT BÀI để bắt đầu.");
                UpdateDistancesAndTargetables();
                BroadcastSnapshot();
            }
        }

        private IEnumerator RunOneBotTurn(PlayerSnapshotDTO bot)
        {
            // Draw 2
            _currentSnapshot.currentPhase = "draw";
            BroadcastSnapshot();
            yield return new WaitForSeconds(1.2f);

            bot.handCount = Mathf.Min(bot.handCount + 2, 7);
            _currentSnapshot.drawPileCount = Mathf.Max(0, _currentSnapshot.drawPileCount - 2);
            _currentSnapshot.combatLogs.Add("🃏 " + bot.name + " rút 2 lá bài.");
            BroadcastSnapshot();
            yield return new WaitForSeconds(0.7f);

            // Play
            _currentSnapshot.currentPhase = "play";
            BroadcastSnapshot();
            yield return new WaitForSeconds(0.8f);

            // Attack local player 70%
            var lp = _currentSnapshot.players.Find(p => p.id == LocalPlayerId);
            if (lp != null && lp.isAlive && UnityEngine.Random.value < 0.70f && bot.handCount > 0)
            {
                _currentSnapshot.combatLogs.Add("🔫 " + bot.name + " BANG! nhắm vào " + lp.name + "!");
                bot.handCount = Mathf.Max(0, bot.handCount - 1);
                _currentSnapshot.discardPileCount++;
                BroadcastSnapshot();
                yield return new WaitForSeconds(0.9f);

                bool playerDodges = UnityEngine.Random.value < 0.40f;
                if (playerDodges)
                {
                    _currentSnapshot.combatLogs.Add("🛡️ " + lp.name + " né tránh được!");
                }
                else
                {
                    lp.currentHealth = Mathf.Max(0, lp.currentHealth - 1);
                    _currentSnapshot.combatLogs.Add("💢 " + lp.name + " trúng đạn! Còn " + lp.currentHealth + " máu.");
                    if (lp.currentHealth <= 0)
                    {
                        lp.isAlive = false;
                        lp.isRoleRevealed = true;
                        _currentSnapshot.combatLogs.Add("💀 " + lp.name + " đã bị hạ gục bởi " + bot.name + "!");
                        UpdateDistancesAndTargetables();
                        BroadcastSnapshot();
                        CheckGameOver();
                        yield break;
                    }
                }
                UpdateDistancesAndTargetables();
                BroadcastSnapshot();
                yield return new WaitForSeconds(0.7f);
            }

            // Beer if low HP 30%
            if (bot.currentHealth < bot.maxHealth && UnityEngine.Random.value < 0.30f && bot.handCount > 0)
            {
                bot.currentHealth = Mathf.Min(bot.maxHealth, bot.currentHealth + 1);
                bot.handCount = Mathf.Max(0, bot.handCount - 1);
                _currentSnapshot.discardPileCount++;
                _currentSnapshot.combatLogs.Add("🍺 " + bot.name + " uống Bia, hồi 1 Máu (" + bot.currentHealth + "/" + bot.maxHealth + ").");
                BroadcastSnapshot();
                yield return new WaitForSeconds(0.6f);
            }

            _currentSnapshot.combatLogs.Add("✅ " + bot.name + " kết thúc lượt.");
        }

        private void AdvanceToNextPlayer()
        {
            int currentIdx = _currentSnapshot.players.FindIndex(p => p.id == _currentSnapshot.currentTurnPlayerId);
            int total = _currentSnapshot.players.Count;
            if (total == 0) return;
            int nextIdx = (currentIdx + 1) % total;
            int safety = 0;
            while (!_currentSnapshot.players[nextIdx].isAlive && safety < total) { nextIdx = (nextIdx + 1) % total; safety++; }
            _currentSnapshot.currentTurnPlayerId = _currentSnapshot.players[nextIdx].id;
            _currentSnapshot.turnNumber++;
        }

        public Task<bool> RequestRematchAsync()
        {
            return StartGameAsync();
        }

        private void KillPlayer(PlayerSnapshotDTO player, PlayerSnapshotDTO killer)
        {
            player.isAlive = false;
            player.isRoleRevealed = true;
            player.currentHealth = 0;
            _currentSnapshot.combatLogs.Add("💀 " + player.name + " [" + player.role.ToUpper() + "] bị " + (killer?.name ?? "?") + " hạ gục!");
            CheckGameOver();
        }

        private void CheckGameOver()
        {
            var alive = _currentSnapshot.players.Where(p => p.isAlive).ToList();
            bool sheriffAlive = alive.Any(p => p.role == "sheriff");
            bool outlawAlive = alive.Any(p => p.role == "outlaw");
            bool renegadeAlive = alive.Any(p => p.role == "renegade");

            if (!sheriffAlive)
                EndGame(alive.Count == 1 && renegadeAlive ? "renegade" : "outlaw");
            else if (!outlawAlive && !renegadeAlive)
                EndGame("sheriff");
        }

        private void EndGame(string winnerRole)
        {
            if (_mockGameLoopCoroutine != null) StopCoroutine(_mockGameLoopCoroutine);
            string emoji = winnerRole == "sheriff" ? "⭐" : winnerRole == "outlaw" ? "💀" : "🗡️";
            string team = winnerRole == "sheriff" ? "PHE CẢNH SÁT TRƯỞNG" : winnerRole == "outlaw" ? "PHE CƯỚP" : "KẺ PHẢN BỘI";
            _currentSnapshot.state = ServerGameState.FINISHED;
            _currentSnapshot.winnerRole = winnerRole;
            _currentSnapshot.winnerTeam = team;
            _currentSnapshot.combatLogs.Add(emoji + " TRẬN KẾT THÚC! " + team + " THẮNG!");
            foreach (var p in _currentSnapshot.players) p.isRoleRevealed = true;
            BroadcastSnapshot();
        }

        private void UpdateDistancesAndTargetables()
        {
            if (_currentSnapshot == null) return;
            var local = _currentSnapshot.players.Find(p => p.id == LocalPlayerId);
            if (local == null) return;

            int gunRange = 1;
            foreach (var eq in local.equipment)
            {
                if (eq.StartsWith("gun_range_3")) gunRange = 3;
                else if (eq.StartsWith("gun_range_2")) gunRange = 2;
            }

            var alive = _currentSnapshot.players.Where(p => p.isAlive).ToList();
            int localAliveIdx = alive.FindIndex(p => p.id == LocalPlayerId);
            if (localAliveIdx < 0) return;

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
                    // Mustang adds +1 distance
                    if (p.equipment.Any(e => e.StartsWith("mustang"))) baseDist++;
                    p.effectiveDistanceToLocal = baseDist;
                    p.isTargetable = baseDist <= gunRange;
                }
            }
            foreach (var p in _currentSnapshot.players.Where(p => !p.isAlive))
            {
                p.effectiveDistanceToLocal = 999;
                p.isTargetable = false;
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

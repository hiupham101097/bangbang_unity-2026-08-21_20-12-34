using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using BangBang.UI;
using UnityEngine;

namespace BangBang.Core.Logic
{
    /// <summary>
    /// Autonomous agent that plays matches continuously in Offline Mock mode,
    /// driving the UI & Gateway, verifying game rules, and catching any logic bugs,
    /// deadlocks, or illegal states in real time.
    /// </summary>
    public class AutoPlayBotRunner : MonoBehaviour
    {
        public static AutoPlayBotRunner Instance { get; private set; }

        [Header("Settings")]
        public bool isAutoPlayActive = true;
        public float stepDelaySeconds = 1.0f;
        public bool loopNextMatchOnGameOver = true;
        public int totalMatchesPlayed = 0;
        public int totalMatchesCompleted = 0;

        private Coroutine _runnerCoroutine;
        private float _lastStateChangeTime;
        private string _lastObservedTurnId = "";
        private string _lastObservedPhase = "";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _lastStateChangeTime = Time.time;
            if (GameStateStore.Instance != null && GameStateStore.Instance.Gateway != null)
            {
                HookGatewayEvents(GameStateStore.Instance.Gateway);
            }

            if (isAutoPlayActive)
            {
                StartRunner();
            }
        }

        public void StartRunner()
        {
            if (_runnerCoroutine != null) StopCoroutine(_runnerCoroutine);
            _runnerCoroutine = StartCoroutine(AutoPlayLoopCoroutine());
            Debug.Log("<color=cyan><b>[AutoPlayBotRunner]</b> Đã kích hoạt Auto-Play Bot. Bot sẽ tự động chơi và giám sát logic!</color>");
        }

        public void StopRunner()
        {
            if (_runnerCoroutine != null)
            {
                StopCoroutine(_runnerCoroutine);
                _runnerCoroutine = null;
            }
            Debug.Log("<color=yellow><b>[AutoPlayBotRunner]</b> Đã tạm dừng Auto-Play Bot.</color>");
        }

        private void HookGatewayEvents(IGameGateway gateway)
        {
            gateway.OnActionRejected -= HandleActionRejected;
            gateway.OnActionRejected += HandleActionRejected;
            gateway.OnErrorMessage -= HandleErrorMessage;
            gateway.OnErrorMessage += HandleErrorMessage;
        }

        private void HandleActionRejected(string action, string reason)
        {
            Debug.LogError($"<color=red><b>[AutoPlay LOGIC BUG]</b> Action [{action}] bị từ chối: {reason}</color>");
        }

        private void HandleErrorMessage(string message)
        {
            Debug.LogError($"<color=orange><b>[AutoPlay GATEWAY ERROR]</b> {message}</color>");
        }

        private IEnumerator AutoPlayLoopCoroutine()
        {
            while (isAutoPlayActive)
            {
                yield return new WaitForSeconds(stepDelaySeconds);

                var store = GameStateStore.Instance;
                if (store == null || store.Gateway == null)
                {
                    continue;
                }

                HookGatewayEvents(store.Gateway);
                var gateway = store.Gateway;
                var snapshot = store.CurrentSnapshot;

                // 0. Nếu đang ở màn hình Home -> Tự động chuyển vào Sảnh
                if (GameBootstrap.Instance != null && GameBootstrap.Instance.homeScreen != null && GameBootstrap.Instance.homeScreen.gameObject.activeSelf)
                {
                    Debug.Log("<color=cyan><b>[AutoPlay]</b> Đang ở Màn hình chính (HomeScreen). Tự động BẮT ĐẦU vào Sảnh...</color>");
                    GameBootstrap.Instance.homeScreen.gameObject.SetActive(false);
                    GameFlowController.Instance?.TransitionToState(ServerGameState.LOBBY);
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // 1. Sảnh chính (LOBBY) -> Tự động Tạo Phòng với 4 Bot
                if (snapshot == null || snapshot.state == ServerGameState.LOBBY)
                {
                    Debug.Log("<color=cyan><b>[AutoPlay]</b> Đang ở Sảnh. Tự động khởi tạo phòng đấu 5 người...</color>");
                    totalMatchesPlayed++;
                    _lastStateChangeTime = Time.time;
                    _ = gateway.CreateRoomAsync("Phòng Auto-Play #" + totalMatchesPlayed, 5, false, "", 30);
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                // 2. Phòng chờ (WAITING) -> Bắt đầu ván
                if (snapshot.state == ServerGameState.WAITING)
                {
                    Debug.Log("<color=cyan><b>[AutoPlay]</b> Đang ở Phòng chờ. Tự động BẮT ĐẦU TRẬN ĐẤU...</color>");
                    _lastStateChangeTime = Time.time;
                    _ = gateway.StartGameAsync();
                    yield return new WaitForSeconds(2.0f);
                    continue;
                }

                // 3. Chọn vai trò (ROLE_DRAFT) -> Chờ reveal
                if (snapshot.state == ServerGameState.ROLE_DRAFT || snapshot.state == ServerGameState.ROLE_LOCK_WAIT)
                {
                    _lastStateChangeTime = Time.time;
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // 4. Chọn nhân vật (CHARACTER_DRAFT)
                if (snapshot.state == ServerGameState.CHARACTER_DRAFT)
                {
                    _lastStateChangeTime = Time.time;
                    string pickChar = "willy_the_kid";
                    if (store.LocalPrivateState?.draftCharacterOptions != null && store.LocalPrivateState.draftCharacterOptions.Count > 0)
                    {
                        pickChar = store.LocalPrivateState.draftCharacterOptions[0];
                    }
                    Debug.Log($"<color=cyan><b>[AutoPlay]</b> Tự động chọn nhân vật: {pickChar}</color>");
                    _ = gateway.SelectCharacterAsync(pickChar);
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                // 5. Trong trận đấu (PLAY)
                if (snapshot.state == ServerGameState.PLAY)
                {
                    // Deadlock check: nếu cùng 1 lượt và pha kéo dài quá 35s mà không ai đi
                    if (snapshot.currentTurnPlayerId == _lastObservedTurnId &&
                        string.Equals(snapshot.currentPhase, _lastObservedPhase, StringComparison.OrdinalIgnoreCase))
                    {
                        if (Time.time - _lastStateChangeTime > 35f)
                        {
                            Debug.LogError($"<color=red><b>[AutoPlay DEADLOCK BUG]</b> Trận đấu bị kẹt ở Lượt của [{snapshot.currentTurnPlayerId}], Pha [{snapshot.currentPhase}] quá 35s không chuyển!</color>");
                            _lastStateChangeTime = Time.time;
                        }
                    }
                    else
                    {
                        _lastObservedTurnId = snapshot.currentTurnPlayerId;
                        _lastObservedPhase = snapshot.currentPhase;
                        _lastStateChangeTime = Time.time;
                    }

                    // 5a. Xử lý Interaction Prompt nếu có yêu cầu phản hồi (VD: né đòn, bốc bài bách hóa)
                    if (snapshot.activeInteraction != null && snapshot.activeInteraction.actorPlayerId == store.LocalPlayerId)
                    {
                        yield return StartCoroutine(HandleInteractionAsync(snapshot.activeInteraction, gateway, store));
                        continue;
                    }

                    // 5b. Nếu là lượt của người chơi cục bộ (Local Player) -> Bot chơi hộ
                    if (snapshot.currentTurnPlayerId == store.LocalPlayerId)
                    {
                        yield return StartCoroutine(PlayLocalTurnAsync(snapshot, gateway, store));
                    }
                }

                // 6. Kết thúc ván (GAME_OVER)
                if (snapshot.state == ServerGameState.GAME_OVER)
                {
                    totalMatchesCompleted++;
                    Debug.Log($"<color=green><b>[AutoPlay]</b> Ván đấu kết thúc thành công! Tổng số ván đã hoàn thành: {totalMatchesCompleted}</color>");
                    if (loopNextMatchOnGameOver)
                    {
                        Debug.Log("<color=cyan><b>[AutoPlay]</b> Chuẩn bị vào ván tiếp theo sau 3 giây...</color>");
                        yield return new WaitForSeconds(3.0f);
                        _ = gateway.LeaveRoomAsync();
                    }
                    else
                    {
                        isAutoPlayActive = false;
                    }
                }
            }
        }

        private IEnumerator PlayLocalTurnAsync(MatchStateSnapshotDTO snapshot, IGameGateway gateway, GameStateStore store)
        {
            string phase = snapshot.currentPhase != null ? snapshot.currentPhase.ToLowerInvariant() : "";

            // Phase: DRAW
            if (phase == "draw")
            {
                Debug.Log("<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Bước RÚT BÀI -> Rút 2 lá...</color>");
                yield return new WaitForSeconds(0.5f);
                _ = gateway.RequestDrawAsync();
                yield return new WaitForSeconds(1.0f);
                yield break;
            }

            // Phase: PLAY
            if (phase == "play")
            {
                yield return new WaitForSeconds(0.8f);

                var hand = store.LocalPrivateState?.hand;
                var localP = store.LocalPlayer;

                if (hand != null && hand.Count > 0 && localP != null && localP.isAlive)
                {
                    // 1. Ưu tiên uống Bia nếu mất máu
                    if (localP.currentHealth < localP.maxHealth)
                    {
                        string beerCard = hand.Find(c => CardCatalogDatabase.GetTypeOf(c) == "beer");
                        if (!string.IsNullOrEmpty(beerCard) && MatchActionRules.CanSelectCard(snapshot, store.LocalPlayerId, beerCard, out _))
                        {
                            Debug.Log($"<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Máu hiện tại {localP.currentHealth}/{localP.maxHealth} -> Dùng thẻ BIA ({beerCard}) để hồi máu!</color>");
                            _ = gateway.PlayCardAsync(beerCard, null);
                            yield return new WaitForSeconds(1.0f);
                            yield break;
                        }
                    }

                    // 2. Ưu tiên trang bị vũ khí / ngựa / thùng gỗ (equipment)
                    string equipCard = hand.Find(c => {
                        string t = CardCatalogDatabase.GetTypeOf(c);
                        return t == "gun" || t == "mustang" || t == "barrel" || t == "scope";
                    });
                    if (!string.IsNullOrEmpty(equipCard) && MatchActionRules.CanSelectCard(snapshot, store.LocalPlayerId, equipCard, out _))
                    {
                        Debug.Log($"<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Trang bị trang bị mới ({equipCard})...</color>");
                        _ = gateway.PlayCardAsync(equipCard, null);
                        yield return new WaitForSeconds(1.0f);
                        yield break;
                    }

                    // 3. Đánh thẻ diện rộng (Gatling / Saloon)
                    string aoeCard = hand.Find(c => {
                        string t = CardCatalogDatabase.GetTypeOf(c);
                        return t == "gatling" || t == "saloon";
                    });
                    if (!string.IsNullOrEmpty(aoeCard) && MatchActionRules.CanSelectCard(snapshot, store.LocalPlayerId, aoeCard, out _))
                    {
                        Debug.Log($"<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Đánh thẻ diện rộng ({aoeCard})!</color>");
                        _ = gateway.PlayCardAsync(aoeCard, null);
                        yield return new WaitForSeconds(1.2f);
                        yield break;
                    }

                    // 4. Bắn Bang! vào mục tiêu hợp lệ trong tầm bắn
                    string bangCard = hand.Find(c => CardCatalogDatabase.GetTypeOf(c) == "bang");
                    if (!string.IsNullOrEmpty(bangCard) && MatchActionRules.CanSelectCard(snapshot, store.LocalPlayerId, bangCard, out _))
                    {
                        var targets = snapshot.players.Where(p =>
                            p.id != store.LocalPlayerId &&
                            p.isAlive &&
                            MatchActionRules.IsValidTarget(snapshot, store.LocalPlayerId, p.id, bangCard)
                        ).ToList();

                        if (targets.Count > 0)
                        {
                            // Ưu tiên bắn mục tiêu ít máu nhất
                            var target = targets.OrderBy(t => t.currentHealth).First();
                            Debug.Log($"<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Đánh BANG! ({bangCard}) vào [{target.name}] (Máu: {target.currentHealth})</color>");
                            _ = gateway.PlayCardAsync(bangCard, new List<string> { target.id });
                            yield return new WaitForSeconds(1.2f);
                            yield break;
                        }
                    }
                }

                // Không còn bài nào đánh được hoặc đã đánh xong -> Kết thúc lượt
                Debug.Log("<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Không còn bài cần đánh -> KẾT THÚC LƯỢT.</color>");
                _ = gateway.EndTurnAsync();
                yield return new WaitForSeconds(1.0f);
                yield break;
            }

            // Phase: DISCARD
            if (phase == "discard")
            {
                var hand = store.LocalPrivateState?.hand;
                var localP = store.LocalPlayer;
                if (hand != null && localP != null && hand.Count > localP.currentHealth)
                {
                    string discardCard = hand.Last();
                    Debug.Log($"<color=yellow><b>[AutoPlay]</b> [Lượt Tôi] Hủy bài thừa ({discardCard}) để cân bằng số Máu...</color>");
                    _ = gateway.EndTurnAsync(new List<string> { discardCard });
                    yield return new WaitForSeconds(0.8f);
                    yield break;
                }

                _ = gateway.EndTurnAsync();
                yield return new WaitForSeconds(0.8f);
            }
        }

        private IEnumerator HandleInteractionAsync(InteractionPromptDTO prompt, IGameGateway gateway, GameStateStore store)
        {
            Debug.Log($"<color=orange><b>[AutoPlay]</b> Nhận Interaction Prompt: [{prompt.type}] - {prompt.title}</color>");
            yield return new WaitForSeconds(0.8f);

            var hand = store.LocalPrivateState?.hand ?? new List<string>();

            // Nếu là bị tấn công và cần dùng Né! (dodge) hoặc Thùng gỗ (barrel)
            if (prompt.type == "RESPOND_DEFENSE" || prompt.type == "CHOOSE_CARD")
            {
                string dodgeCard = hand.Find(c => CardCatalogDatabase.GetTypeOf(c) == "dodge");
                if (!string.IsNullOrEmpty(dodgeCard))
                {
                    Debug.Log($"<color=orange><b>[AutoPlay]</b> Phản hồi phòng thủ: Sử dụng NÉ ({dodgeCard})</color>");
                    _ = gateway.SubmitInteractionAsync(prompt.interactionId, "RESPOND", selectedCards: new List<string> { dodgeCard });
                    yield return new WaitForSeconds(1.0f);
                    yield break;
                }
            }

            // Mặc định chọn phương án đầu tiên hoặc defaultAction
            string chosen = !string.IsNullOrEmpty(prompt.defaultAction)
                ? prompt.defaultAction
                : (prompt.options != null && prompt.options.Count > 0 ? prompt.options[0] : "CONFIRM");

            Debug.Log($"<color=orange><b>[AutoPlay]</b> Phản hồi prompt với lựa chọn mặc định: '{chosen}'</color>");
            _ = gateway.SubmitInteractionAsync(prompt.interactionId, chosen);
            yield return new WaitForSeconds(1.0f);
        }
    }
}

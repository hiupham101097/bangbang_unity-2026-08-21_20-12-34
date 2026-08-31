using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using BangBang.Core.Network;
using BangBang.Core.State;
using BangBang.UI.Interaction;
using BangBang.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class GameTableView : MonoBehaviour
    {
        [Header("Table Visuals")]
        public Image tableBackgroundImage;
        public Transform opponentSeatsContainer;
        public GameObject playerSeatPrefab;

        [Header("Center Decks & Combat Zone")]
        public Image drawPileImage;
        public Text drawPileCountText;
        public Image discardPileImage;
        public Text discardPileCountText;
        public Text combatLogText;
        public Text turnPhaseStatusText;
        public Text turnNumberText;
        public Text turnInstructionText;
        public Image drawStepImage;
        public Image playStepImage;
        public Image endStepImage;

        [Header("Local Player Dashboard (Bottom)")]
        public Image localAvatarImage;
        public Text localNameText;
        public Text localRoleText;
        public Transform localBulletHealthContainer;
        public Transform localEquipmentTray;
        public HandCardFanLayout handCardLayout;
        public Button drawCardButton;
        public Button playCardButton;
        public Text playCardButtonText;
        public Button endTurnButton;
        public Button abilityButton;
        public Button cancelTargetButton;
        public GameObject targetBannerObj;
        public Text targetBannerText;
        public GameObject cardPreviewTooltipObj;
        public Text cardPreviewTooltipText;
        public Text timerText;

        private readonly List<PlayerSeatUI> _seatUIs = new List<PlayerSeatUI>();
        private readonly List<GameObject> _localEquipCards = new List<GameObject>();
        private readonly List<GameObject> _bulletTokens = new List<GameObject>();
        private int _renderedHealth = -1;
        private int _renderedMaxHealth = -1;
        private string _renderedEquipmentKey = null;
        private int _renderedTableCapacity = -1;

        private CardUI _selectedCardUI;
        private string _selectedTargetId;
        private MatchStateSnapshotDTO _lastSnapshot;

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            if (tableBackgroundImage != null)
            {
                var tableSprite = CardCatalogDatabase.LoadSprite("UI/LandscapeV2/match_table_8");
                if (tableSprite != null)
                {
                    tableBackgroundImage.sprite = tableSprite;
                    tableBackgroundImage.color = Color.white;
                }
            }

            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += RenderTableSnapshot;
                GameStateStore.Instance.OnCombatLogAdded += HandleCombatLogAdded;
                GameStateStore.Instance.OnRequestPendingChanged += HandleRequestPendingChanged;
                GameStateStore.Instance.OnGatewayErrorMessage += HandleGatewayError;
            }

            if (handCardLayout != null)
            {
                handCardLayout.OnCardClicked += HandleCardSelected;
                handCardLayout.OnCardDropped += HandleHandCardPlayed;
            }

            if (targetBannerObj != null) targetBannerObj.SetActive(false);
            if (cardPreviewTooltipObj != null) cardPreviewTooltipObj.SetActive(false);
            if (playCardButton != null) playCardButton.gameObject.SetActive(false);
            if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(false);
            if (combatLogText != null) combatLogText.text = "";
            if (drawPileImage != null) drawPileImage.sprite = CardCatalogDatabase.LoadCardBackSprite();
            RenderTableSnapshot(GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null);
        }

        public void BindListeners()
        {
            if (drawCardButton != null)
            {
                drawCardButton.onClick.RemoveAllListeners();
                drawCardButton.onClick.AddListener(HandleDrawCardClicked);
            }

            if (playCardButton != null)
            {
                playCardButton.onClick.RemoveAllListeners();
                playCardButton.onClick.AddListener(HandlePlayCardButtonClicked);
            }

            if (cancelTargetButton != null)
            {
                cancelTargetButton.onClick.RemoveAllListeners();
                cancelTargetButton.onClick.AddListener(CancelCardSelection);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(HandleEndTurnClicked);
            }

            if (abilityButton != null)
            {
                abilityButton.onClick.RemoveAllListeners();
                abilityButton.onClick.AddListener(HandleAbilityClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= RenderTableSnapshot;
                GameStateStore.Instance.OnCombatLogAdded -= HandleCombatLogAdded;
                GameStateStore.Instance.OnRequestPendingChanged -= HandleRequestPendingChanged;
                GameStateStore.Instance.OnGatewayErrorMessage -= HandleGatewayError;
            }
        }

        private void HandleCombatLogAdded(string message)
        {
            if (combatLogText != null) combatLogText.text = message;
        }

        private void HandleRequestPendingChanged(bool pending)
        {
            if (_lastSnapshot != null) RenderTableSnapshot(_lastSnapshot);
        }

        private void HandleGatewayError(string message)
        {
            ShowActionMessage(string.IsNullOrWhiteSpace(message) ? "Hành động không được chấp nhận. Hãy thử lại." : message, true);
        }

        public void RenderTableSnapshot(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null) return;
            _lastSnapshot = snapshot;

            int tableCapacity = Mathf.Clamp(snapshot.rules != null && snapshot.rules.maxPlayers > 0
                ? snapshot.rules.maxPlayers
                : snapshot.players.Count, 4, 8);
            if (_renderedTableCapacity != tableCapacity && tableBackgroundImage != null)
            {
                var tableSprite = CardCatalogDatabase.LoadSprite("UI/LandscapeV2/match_table_" + tableCapacity);
                if (tableSprite != null) tableBackgroundImage.sprite = tableSprite;
                _renderedTableCapacity = tableCapacity;
            }

            string localId = GameStateStore.Instance != null ? GameStateStore.Instance.LocalPlayerId : "";
            var local = snapshot.players.Find(p => p.id == localId);
            var localPrivate = GameStateStore.Instance != null ? GameStateStore.Instance.LocalPrivateState : null;

            // 1. Center Decks & Status
            if (drawPileCountText != null) drawPileCountText.text = snapshot.drawPileCount.ToString();
            if (discardPileCountText != null) discardPileCountText.text = snapshot.discardPileCount.ToString();
            if (discardPileImage != null && !string.IsNullOrEmpty(snapshot.topDiscardCardId))
            {
                var info = CardCatalogDatabase.GetCardInfo(snapshot.topDiscardCardId);
                discardPileImage.sprite = CardCatalogDatabase.LoadSprite(info.resourcePath);
            }

            bool isMyTurn = snapshot.currentTurnPlayerId == localId;
            string phase = (snapshot.currentPhase ?? string.Empty).ToUpperInvariant();
            if (turnPhaseStatusText != null)
            {
                turnPhaseStatusText.text = snapshot.state == ServerGameState.JUDGEMENT && !string.IsNullOrEmpty(snapshot.judgementCard)
                    ? "PHÁN XÉT " + snapshot.judgementEffect + ": " + snapshot.judgementCard.Replace("__", " ") + " — " + snapshot.judgementResult
                    : isMyTurn ? "LƯỢT CỦA BẠN" : "ĐANG CHỜ " + GetCurrentPlayerName(snapshot).ToUpperInvariant();
                turnPhaseStatusText.color = isMyTurn ? BangUITheme.Success : BangUITheme.Ivory;
            }
            if (turnNumberText != null) turnNumberText.text = "LƯỢT " + Mathf.Max(1, snapshot.turnNumber);
            UpdateTurnGuidance(snapshot, isMyTurn, phase);

            // 2. Buttons State
            if (drawCardButton != null)
            {
                bool drawing = isMyTurn && phase == "DRAW";
                drawCardButton.gameObject.SetActive(drawing);
                drawCardButton.interactable = drawing && GameStateStore.Instance != null && !GameStateStore.Instance.IsRequestPending;
                var drawLabel = drawCardButton.GetComponentInChildren<Text>();
                if (drawLabel != null) drawLabel.text = GameStateStore.Instance != null && GameStateStore.Instance.IsRequestPending ? "ĐANG RÚT BÀI…" : "🃏 RÚT BÀI";
            }

            if (endTurnButton != null)
            {
                bool canEnd = isMyTurn && phase == "PLAY" && GameStateStore.Instance != null && !GameStateStore.Instance.IsRequestPending;
                endTurnButton.gameObject.SetActive(isMyTurn && phase == "PLAY");
                endTurnButton.interactable = canEnd;
                var endLabel = endTurnButton.GetComponentInChildren<Text>();
                if (endLabel != null) endLabel.text = canEnd ? "🛑 KẾT THÚC" : "ĐANG XỬ LÝ…";
            }

            // 3. Local Dashboard
            if (local != null)
            {
                bool spectating = !local.isAlive;
                if (localNameText != null) localNameText.text = local.name;
                if (localRoleText != null)
                {
                    string r = localPrivate != null ? localPrivate.roleId : "";
                    localRoleText.text = r == "sheriff" ? "⭐ CẢNH SÁT TRƯỞNG" :
                                         r == "deputy" ? "🛡️ PHÓ CẢNH SÁT" :
                                         r == "outlaw" ? "💀 CƯỚP (OUTLAW)" : "🗡️ PHẢN BỘI (RENEGADE)";
                }

                if (localAvatarImage != null)
                {
                    localAvatarImage.sprite = AvatarCatalog.Load(local.avatarId, local.id);
                }

                RenderBulletHealth(local.currentHealth, local.maxHealth);
                RenderLocalEquipment(local.equipment);

                if (handCardLayout != null && localPrivate != null)
                {
                    handCardLayout.UpdateHand(localPrivate.hand);
                    handCardLayout.gameObject.SetActive(!spectating);
                    RefreshHandInteractionState(snapshot);
                }

                if (abilityButton != null)
                {
                    bool isSid = local.characterId == "sid_ketchum";
                    abilityButton.gameObject.SetActive(isSid);
                    abilityButton.interactable = isSid && isMyTurn && phase == "PLAY" &&
                                                 local.currentHealth < local.maxHealth &&
                                                 localPrivate != null && localPrivate.hand.Count >= 2 &&
                                                 GameStateStore.Instance != null && !GameStateStore.Instance.IsRequestPending;
                }

                if (spectating)
                {
                    if (drawCardButton != null) drawCardButton.gameObject.SetActive(false);
                    if (playCardButton != null) playCardButton.gameObject.SetActive(false);
                    if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
                    if (abilityButton != null) abilityButton.gameObject.SetActive(false);
                    if (targetBannerObj != null) targetBannerObj.SetActive(true);
                    if (targetBannerText != null) targetBannerText.text = "BẠN ĐÃ BỊ LOẠI — ĐANG XEM TRẬN";
                }
                else if (_selectedCardUI == null && targetBannerObj != null)
                {
                    targetBannerObj.SetActive(false);
                }
            }

            // 4. Opponents Seats
            RenderOpponentSeats(snapshot, localId);
        }

        private static string GetCurrentPlayerName(MatchStateSnapshotDTO snapshot)
        {
            var current = snapshot.players?.Find(player => player.id == snapshot.currentTurnPlayerId);
            return current != null && !string.IsNullOrWhiteSpace(current.name) ? current.name : "ĐỐI THỦ";
        }

        private void UpdateTurnGuidance(MatchStateSnapshotDTO snapshot, bool isMyTurn, string phase)
        {
            bool requestPending = GameStateStore.Instance != null && GameStateStore.Instance.IsRequestPending;
            if (turnInstructionText != null)
            {
                if (!isMyTurn)
                    turnInstructionText.text = "Quan sát bàn đấu. Bạn sẽ được báo ngay khi đến lượt hoặc cần phản ứng.";
                else if (requestPending)
                    turnInstructionText.text = "Đang gửi hành động và chờ bàn đấu xác nhận…";
                else if (phase == "DRAW")
                    turnInstructionText.text = "Bước 1/3 — Rút 2 lá bài để bắt đầu lượt.";
                else if (phase == "PLAY" && _selectedCardUI == null)
                    turnInstructionText.text = "Bước 2/3 — Chọn một lá sáng trên tay, sau đó chọn mục tiêu nếu cần.";
                else if (phase == "PLAY")
                    turnInstructionText.text = "Bước 2/3 — Kiểm tra lựa chọn rồi nhấn ĐÁNH BÀI.";
                else if (phase == "DISCARD")
                    turnInstructionText.text = "Bước 3/3 — Bỏ bài dư theo giới hạn Máu để kết thúc lượt.";
                else
                    turnInstructionText.text = "Đang xử lý trạng thái lượt chơi…";
            }

            StyleStep(drawStepImage, isMyTurn && phase == "DRAW", !isMyTurn || phase != "DRAW");
            StyleStep(playStepImage, isMyTurn && phase == "PLAY", !isMyTurn || phase == "DRAW");
            StyleStep(endStepImage, isMyTurn && phase == "DISCARD", !isMyTurn || phase == "DRAW");
        }

        private static void StyleStep(Image image, bool active, bool muted)
        {
            if (image == null) return;
            image.color = active ? BangUITheme.Brass : muted ? new Color(0.15f, 0.12f, 0.1f, 0.92f) : BangUITheme.SurfaceRaised;
        }

        private void RefreshHandInteractionState(MatchStateSnapshotDTO snapshot)
        {
            if (handCardLayout == null || GameStateStore.Instance == null) return;
            foreach (var card in handCardLayout.Cards)
            {
                bool playable = MatchActionRules.CanSelectCard(snapshot, GameStateStore.Instance.LocalPlayerId, card.cardId, out _);
                card.SetPlayable(playable);
                card.SetSelected(card == _selectedCardUI);
            }
        }

        private void HandleAbilityClicked()
        {
            var store = GameStateStore.Instance;
            var hand = store != null ? store.LocalPrivateState?.hand : null;
            if (store == null || hand == null || hand.Count < 2 || InteractionController.Instance == null) return;
            InteractionController.Instance.ShowModal(new InteractionPromptDTO
            {
                interactionId = "sid_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = "SELECT_CARDS",
                actorPlayerId = store.LocalPlayerId,
                title = "SID KETCHUM",
                message = "Chọn đúng 2 lá để bỏ và hồi 1 máu.",
                minSelections = 2,
                maxSelections = 2,
                validCardIds = new List<string>(hand),
                validPlayerIds = new List<string>(),
                options = new List<string>(),
                canCancel = true,
                expiresAt = 0
            }, isLocalOnly: true);
        }

        private void RenderBulletHealth(int current, int max)
        {
            if (localBulletHealthContainer == null) return;
            if (_renderedHealth == current && _renderedMaxHealth == max) return;
            _renderedHealth = current;
            _renderedMaxHealth = max;
            foreach (var b in _bulletTokens) Destroy(b);
            _bulletTokens.Clear();

            for (int i = 0; i < max; i++)
            {
                var bulletObj = new GameObject("Bullet_" + i, typeof(RectTransform), typeof(Image));
                bulletObj.transform.SetParent(localBulletHealthContainer, false);
                var rt = bulletObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(12, 22);

                var img = bulletObj.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite("health_bullet");
                img.color = i < current ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.5f);

                _bulletTokens.Add(bulletObj);
            }
        }

        private void RenderLocalEquipment(List<string> equipment)
        {
            if (localEquipmentTray == null) return;
            string equipmentKey = equipment == null ? string.Empty : string.Join("\u001f", equipment);
            if (_renderedEquipmentKey == equipmentKey) return;
            _renderedEquipmentKey = equipmentKey;
            foreach (var eq in _localEquipCards) Destroy(eq);
            _localEquipCards.Clear();

            if (equipment == null) return;
            
            // Check if we already created the local equipment text
            Text eqNameText = localEquipmentTray.GetComponentInChildren<Text>();
            if (eqNameText == null)
            {
                var txtObj = new GameObject("LocalEquipmentText", typeof(RectTransform), typeof(Text));
                txtObj.transform.SetParent(localEquipmentTray.parent, false); // put it near the tray
                var rt = txtObj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 45f); // just above the tray
                rt.sizeDelta = new Vector2(400, 24);
                eqNameText = txtObj.GetComponent<Text>();
                eqNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                eqNameText.fontSize = 14;
                eqNameText.alignment = TextAnchor.MiddleCenter;
                eqNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                eqNameText.verticalOverflow = VerticalWrapMode.Overflow;
                eqNameText.color = new Color(0.9f, 0.9f, 0.9f);
                eqNameText.raycastTarget = false;
            }

            List<string> eqNames = new List<string>();
            foreach (var eqId in equipment)
            {
                var cardInfo = CardCatalogDatabase.GetCardInfo(eqId);
                var eqCard = new GameObject("Eq_" + cardInfo.id, typeof(RectTransform), typeof(Image));
                eqCard.transform.SetParent(localEquipmentTray, false);
                var rt = eqCard.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(48, 68);

                var img = eqCard.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite(cardInfo.resourcePath);
                img.color = Color.white;
                img.preserveAspect = true;

                _localEquipCards.Add(eqCard);
                if (cardInfo != null && !string.IsNullOrEmpty(cardInfo.vietnameseName))
                {
                    eqNames.Add(cardInfo.vietnameseName);
                }
            }
            
            if (eqNameText != null)
            {
                eqNameText.text = eqNames.Count > 0 ? "Trang bị: " + string.Join(", ", eqNames) : "";
            }
        }

        private void RenderOpponentSeats(MatchStateSnapshotDTO snapshot, string localPlayerId)
        {
            var localPlayer = snapshot.players.Find(p => p.id == localPlayerId);
            int tableCapacity = Mathf.Max(4, snapshot.rules != null ? snapshot.rules.maxPlayers : snapshot.players.Count);
            int localSeat = localPlayer != null ? localPlayer.seat : 0;
            var opponents = snapshot.players
                .Where(p => p.id != localPlayerId)
                .OrderBy(p => (p.seat - localSeat + tableCapacity) % tableCapacity)
                .ToList();

            // Remove excess seats
            while (_seatUIs.Count > opponents.Count)
            {
                var last = _seatUIs[_seatUIs.Count - 1];
                _seatUIs.RemoveAt(_seatUIs.Count - 1);
                Destroy(last.gameObject);
            }

            // Add new seats with proper child UI objects
            while (_seatUIs.Count < opponents.Count)
            {
                var seatUI = CreateProperSeatObject();
                _seatUIs.Add(seatUI);
            }

            for (int i = 0; i < opponents.Count; i++)
            {
                var p = opponents[i];
                var model = new PlayerModel
                {
                    id = p.id,
                    name = p.name,
                    avatarId = p.avatarId,
                    seat = p.seat,
                    isAlive = p.isAlive,
                    health = p.currentHealth,
                    maxHealth = p.maxHealth,
                    characterId = p.characterId,
                    character = string.IsNullOrEmpty(p.characterId) ? null : CardCatalogDatabase.GetCharacterInfo(p.characterId),
                    role = p.hiddenRole == "hidden"
                        ? RoleType.Unknown
                        : (p.publicRoleId == "sheriff" ? RoleType.Sheriff
                            : p.publicRoleId == "deputy" ? RoleType.Deputy
                            : p.publicRoleId == "outlaw" ? RoleType.Outlaw
                            : p.publicRoleId == "renegade" ? RoleType.Renegade
                            : RoleType.Unknown),
                    isRoleRevealed = p.isRoleRevealed || p.hiddenRole != "hidden",
                    cardCount = p.handCount,
                    equipment = p.equipment ?? new List<string>(),
                    hand = new List<string>()
                };

                _seatUIs[i].SetupSeat(model, p.effectiveDistanceToLocal, false);
                _seatUIs[i].SetTurnActive(snapshot.currentTurnPlayerId == p.id);
                bool isChoosingTarget = _selectedCardUI != null &&
                                        MatchActionRules.IsValidTarget(snapshot, localPlayerId, p.id, _selectedCardUI.cardId);
                _seatUIs[i].SetTargetHighlight(isChoosingTarget, isChoosingTarget && _selectedTargetId == p.id);

                _seatUIs[i].OnSeatClicked -= HandleOpponentSeatClicked;
                _seatUIs[i].OnSeatClicked += HandleOpponentSeatClicked;

                var rt = _seatUIs[i].GetComponent<RectTransform>();
                rt.anchoredPosition = GetOpponentSeatPosition(tableCapacity, i);
            }
        }

        private Vector2 GetOpponentSeatPosition(int totalPlayers, int index)
        {
            // These normalized centers match the actual painted frames shared by
            // match_table_4 ... match_table_8 (x/y are relative to half the table).
            var topLeft = new Vector2(-0.45f, 0.63f);
            var topMiddle = new Vector2(0f, 0.63f);
            var topRight = new Vector2(0.45f, 0.63f);
            var topLeftWide = new Vector2(-0.40f, 0.63f);
            var topRightWide = new Vector2(0.40f, 0.63f);
            var middleLeft = new Vector2(-0.70f, 0.13f);
            var middleRight = new Vector2(0.70f, 0.13f);
            var bottomLeft = new Vector2(-0.70f, -0.30f);
            var bottomRight = new Vector2(0.70f, -0.30f);

            Vector2[][] layouts =
            {
                // Clockwise from the local seat: nearest left -> far side -> nearest right.
                new[] { topLeft, topMiddle, topRight },
                new[] { middleLeft, topLeftWide, topRightWide, middleRight },
                new[] { middleLeft, topLeft, topMiddle, topRight, middleRight },
                new[] { bottomLeft, middleLeft, topLeftWide, topRightWide, middleRight, bottomRight },
                new[] { bottomLeft, middleLeft, topLeft, topMiddle, topRight, middleRight, bottomRight }
            };

            int capacityIndex = Mathf.Clamp(totalPlayers, 4, 8) - 4;
            var layout = layouts[capacityIndex];
            Vector2 norm = layout[Mathf.Clamp(index, 0, layout.Length - 1)];

            // Use the container rect if already laid out, otherwise fall back to Screen dimensions
            // so positions are correct on the very first frame before Unity finishes layout.
            var container = opponentSeatsContainer != null
                ? opponentSeatsContainer.GetComponent<RectTransform>()
                : (RectTransform)transform;

            float refW = container.rect.width > 10f ? container.rect.width : 1280f;
            float refH = container.rect.height > 10f ? container.rect.height : 720f;

            return new Vector2(
                Mathf.Round(norm.x * refW * 0.5f),
                Mathf.Round(norm.y * refH * 0.5f));
        }

        /// <summary>Creates a fully-built PlayerSeatUI with all child components wired up.</summary>
        private PlayerSeatUI CreateProperSeatObject()
        {
            var parent = opponentSeatsContainer != null ? opponentSeatsContainer : (RectTransform)transform;
            var seatObj = new GameObject("OpponentSeatUI", typeof(RectTransform), typeof(PlayerSeatUI));
            seatObj.transform.SetParent(parent, false);

            var rt = seatObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(184, 86);

            // ── Wooden Avatar Frame ──
            var frameObj = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(seatObj.transform, false);
            var frameRt = frameObj.GetComponent<RectTransform>();
            frameRt.sizeDelta = new Vector2(68, 68);
            frameRt.anchoredPosition = new Vector2(-52f, 0f);
            var frameImg = frameObj.GetComponent<Image>();
            frameImg.color = new Color(0.4f, 0.25f, 0.12f, 0.78f);

            // ── Avatar Image (inside frame) ──
            var avatarObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarObj.transform.SetParent(frameObj.transform, false);
            var avatarRt = avatarObj.GetComponent<RectTransform>();
            avatarRt.sizeDelta = new Vector2(62, 62);
            var avatarImg = avatarObj.GetComponent<Image>();
            avatarImg.preserveAspect = true;
            avatarImg.color = Color.white;

            // ── Sheriff Star ──
            var starObj = new GameObject("SheriffStar", typeof(RectTransform), typeof(Image));
            starObj.transform.SetParent(frameObj.transform, false);
            var starRt = starObj.GetComponent<RectTransform>();
            starRt.anchoredPosition = new Vector2(-24f, -22f);
            starRt.sizeDelta = new Vector2(22, 22);
            var starImg = starObj.GetComponent<Image>();
            starImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
            starObj.SetActive(false);

            // ── HP Heart Badge ──
            var heartObj = new GameObject("HeartHp", typeof(RectTransform), typeof(Image));
            heartObj.transform.SetParent(frameObj.transform, false);
            var heartRt = heartObj.GetComponent<RectTransform>();
            heartRt.anchoredPosition = new Vector2(24f, -22f);
            heartRt.sizeDelta = new Vector2(24, 24);
            heartObj.GetComponent<Image>().color = new Color(0.85f, 0.15f, 0.15f);

            var hpTxtObj = new GameObject("HpText", typeof(RectTransform), typeof(Text));
            hpTxtObj.transform.SetParent(heartObj.transform, false);
            var hpTxtRt = hpTxtObj.GetComponent<RectTransform>();
            hpTxtRt.anchorMin = Vector2.zero;
            hpTxtRt.anchorMax = Vector2.one;
            hpTxtRt.sizeDelta = Vector2.zero;
            var hpTxt = hpTxtObj.GetComponent<Text>();
            hpTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpTxt.fontSize = 14;
            hpTxt.fontStyle = FontStyle.Bold;
            hpTxt.alignment = TextAnchor.MiddleCenter;
            hpTxt.color = Color.white;
            hpTxt.text = "4";

            // ── Name Label ──
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(seatObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(28f, 24f);
            nameRt.sizeDelta = new Vector2(102, 20);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 14;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            // ── Role Label ──
            var roleObj = new GameObject("Role", typeof(RectTransform), typeof(Text));
            roleObj.transform.SetParent(seatObj.transform, false);
            var roleRt = roleObj.GetComponent<RectTransform>();
            roleRt.anchoredPosition = new Vector2(28f, 3f);
            roleRt.sizeDelta = new Vector2(102, 18);
            var roleTxt = roleObj.GetComponent<Text>();
            roleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roleTxt.fontSize = 12;
            roleTxt.alignment = TextAnchor.MiddleCenter;
            roleTxt.color = new Color(0.9f, 0.8f, 0.4f);

            // ── Equipment Shelf ──
            var eqObj = new GameObject("EquipmentShelf", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            eqObj.transform.SetParent(seatObj.transform, false);
            var eqRt = eqObj.GetComponent<RectTransform>();
            eqRt.anchoredPosition = new Vector2(28f, -24f);
            eqRt.sizeDelta = new Vector2(102, 34);
            
            var eqImg = eqObj.GetComponent<Image>();
            eqImg.color = new Color(0.055f, 0.035f, 0.022f, 0.5f);
            
            var hlg = eqObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;
            hlg.padding = new RectOffset(4, 4, 2, 2);

            // ── Distance Text ──
            var distObj = new GameObject("DistanceText", typeof(RectTransform), typeof(Text));
            distObj.transform.SetParent(seatObj.transform, false);
            var distRt = distObj.GetComponent<RectTransform>();
            distRt.anchoredPosition = new Vector2(-28f, -24f);
            distRt.sizeDelta = new Vector2(50, 20);
            var distTxt = distObj.GetComponent<Text>();
            distTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            distTxt.fontSize = 12;
            distTxt.alignment = TextAnchor.MiddleCenter;
            distTxt.color = new Color(0.7f, 0.7f, 0.7f);
            distTxt.raycastTarget = false;

            // ── Equipment Details Text ──
            var eqTxtObj = new GameObject("EquipmentText", typeof(RectTransform), typeof(Text));
            eqTxtObj.transform.SetParent(seatObj.transform, false);
            var eqTxtRt = eqTxtObj.GetComponent<RectTransform>();
            eqTxtRt.anchoredPosition = new Vector2(0f, -44f);
            eqTxtRt.sizeDelta = new Vector2(120, 18);
            var eqTxt = eqTxtObj.GetComponent<Text>();
            eqTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            eqTxt.fontSize = 11;
            eqTxt.alignment = TextAnchor.UpperCenter;
            eqTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            eqTxt.verticalOverflow = VerticalWrapMode.Overflow;
            eqTxt.color = new Color(0.9f, 0.9f, 0.9f);
            eqTxt.raycastTarget = false;

            // ── Crosshair (tap-to-target) ──
            var crossObj = new GameObject("Crosshair", typeof(RectTransform), typeof(Image), typeof(Button));
            crossObj.transform.SetParent(seatObj.transform, false);
            var crossRt = crossObj.GetComponent<RectTransform>();
            crossRt.sizeDelta = new Vector2(180, 82);
            crossRt.anchoredPosition = Vector2.zero;
            var crossHitArea = crossObj.GetComponent<Image>();
            crossHitArea.color = new Color(0f, 0f, 0f, 0.01f);
            crossHitArea.raycastTarget = true;

            var targetIconObj = new GameObject("TargetIcon", typeof(RectTransform), typeof(Text));
            targetIconObj.transform.SetParent(crossObj.transform, false);
            var targetIconRt = targetIconObj.GetComponent<RectTransform>();
            targetIconRt.anchoredPosition = new Vector2(-52f, 0f);
            targetIconRt.sizeDelta = new Vector2(52f, 52f);
            var targetIconText = targetIconObj.GetComponent<Text>();
            targetIconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            targetIconText.fontSize = 38;
            targetIconText.fontStyle = FontStyle.Bold;
            targetIconText.alignment = TextAnchor.MiddleCenter;
            targetIconText.color = BangUITheme.Brass;
            targetIconText.text = "⊕";
            targetIconText.raycastTarget = false;
            crossObj.SetActive(false);

            // ── Turn Glow ──
            var glowObj = new GameObject("TurnGlow", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(seatObj.transform, false);
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(72, 72);
            glowRt.anchoredPosition = new Vector2(-52f, 0f);
            var glowImg = glowObj.GetComponent<Image>();
            glowImg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            glowObj.SetActive(false);
            glowObj.transform.SetAsFirstSibling(); // Behind everything

            // ── Wire up PlayerSeatUI ──
            var seatUI = seatObj.GetComponent<PlayerSeatUI>();
            seatUI.avatarImage = avatarImg;
            seatUI.avatarFrameImage = frameImg;
            seatUI.roleBadgeStar = starImg;
            seatUI.heartHpBadge = heartObj.GetComponent<Image>();
            seatUI.hpText = hpTxt;
            seatUI.nameText = nameTxt;
            seatUI.roleText = roleTxt;
            seatUI.equipmentRowTransform = eqObj.transform;
            seatUI.crosshairTargetObj = crossObj;
            seatUI.seatSelectButton = crossObj.GetComponent<Button>();
            seatUI.targetIconText = targetIconText;
            seatUI.turnActiveGlow = glowImg;
            seatUI.distanceText = distTxt;
            seatUI.equipmentText = eqTxt;

            return seatUI;
        }

        private void HandleCardSelected(CardUI card)
        {
            if (card == null || GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;

            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (_selectedCardUI == card)
            {
                CancelCardSelection();
                return;
            }

            // There is only one selected card at a time. Always lower the old
            // card first, even when the newly tapped card is not playable.
            if (_selectedCardUI != null) CancelCardSelection();

            if (!MatchActionRules.CanSelectCard(snapshot, GameStateStore.Instance.LocalPlayerId, card.cardId, out string blockedReason))
            {
                card.SetSelected(false);
                ShowActionMessage(blockedReason, true);
                return;
            }

            AudioManager.Instance?.PlaySFX("button_tap");
            _selectedCardUI = card;
            _selectedTargetId = null;
            card.SetSelected(true);

            // Show Card Tooltip Preview
            if (cardPreviewTooltipObj != null)
            {
                cardPreviewTooltipObj.SetActive(true);
                if (cardPreviewTooltipText != null)
                {
                    cardPreviewTooltipText.text = "<b>" + card.info.vietnameseName + "</b>: " + card.info.description;
                }
            }

            bool requiresTarget = MatchActionRules.RequiresTarget(snapshot, GameStateStore.Instance.LocalPlayerId, card.cardId);
            if (requiresTarget)
            {
                // Enter targeting mode
                if (targetBannerObj != null)
                {
                    targetBannerObj.SetActive(true);
                    var bannerImage = targetBannerObj.GetComponent<Image>();
                    if (bannerImage != null) bannerImage.color = new Color(0.36f, 0.16f, 0.08f, 0.96f);
                    if (targetBannerText != null)
                    {
                        targetBannerText.color = BangUITheme.Ivory;
                        var local = snapshot.players.Find(player => player.id == GameStateStore.Instance.LocalPlayerId);
                        targetBannerText.text = "⊕ CHỌN MỤC TIÊU TRONG TẦM " + MatchActionRules.GetWeaponRange(local);
                    }
                }

                if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(true);
                if (playCardButton != null)
                {
                    playCardButton.gameObject.SetActive(true);
                    playCardButton.interactable = false;
                    if (playCardButtonText != null) playCardButtonText.text = "2  •  CHỌN MỤC TIÊU";
                }
            }
            else
            {
                // Instant Action Card (No target required: Beer, Saloon, Gatling, Equipment...)
                if (targetBannerObj != null) targetBannerObj.SetActive(false);
                if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(true);

                if (playCardButton != null)
                {
                    playCardButton.gameObject.SetActive(true);
                    playCardButton.interactable = true;
                    if (playCardButtonText != null)
                    {
                        playCardButtonText.text = "2  •  ĐÁNH " + card.info.vietnameseName;
                    }
                }
            }

            RenderOpponentSeats(snapshot, GameStateStore.Instance.LocalPlayerId);
            UpdateTurnGuidance(snapshot, true, "PLAY");
        }

        private void HandleOpponentSeatClicked(string targetPlayerId)
        {
            if (_selectedCardUI == null || GameStateStore.Instance == null ||
                !MatchActionRules.RequiresTarget(GameStateStore.Instance.CurrentSnapshot, GameStateStore.Instance.LocalPlayerId, _selectedCardUI.cardId)) return;

            var snapshot = GameStateStore.Instance?.CurrentSnapshot;
            if (snapshot == null) return;

            var targetPlayer = snapshot.players.Find(p => p.id == targetPlayerId);
            if (targetPlayer == null || !MatchActionRules.IsValidTarget(snapshot, GameStateStore.Instance.LocalPlayerId, targetPlayerId, _selectedCardUI.cardId))
            {
                ShowActionMessage("Mục tiêu này không hợp lệ với lá bài đã chọn.", true);
                return;
            }

            AudioManager.Instance?.PlaySFX("button_tap");
            _selectedTargetId = targetPlayerId;
            foreach (var seat in _seatUIs)
            {
                bool available = MatchActionRules.IsValidTarget(snapshot, GameStateStore.Instance.LocalPlayerId, seat.playerId, _selectedCardUI.cardId);
                seat.SetTargetHighlight(available, seat.playerId == targetPlayerId);
            }

            // Show targeting tracer line
            if (FXManager.Instance != null)
            {
                var targetSeat = _seatUIs.Find(s => s.playerId == targetPlayerId);
                if (targetSeat != null)
                {
                    Vector2 myPos = new Vector2(Screen.width * 0.5f, 100f);
                    FXManager.Instance.DrawTargetingLine(myPos, targetSeat.GetScreenCenterPosition());
                }
            }

            if (targetBannerText != null)
            {
                targetBannerText.text = "⊕ " + targetPlayer.name.ToUpperInvariant() +
                                        "  •  KHOẢNG CÁCH " + targetPlayer.effectiveDistanceToLocal;
            }

            if (playCardButton != null)
            {
                playCardButton.gameObject.SetActive(false);
            }

            // Fire immediately — no second confirm button needed.
            HandlePlayCardButtonClicked();
        }

        private async void HandlePlayCardButtonClicked()
        {
            if (_selectedCardUI == null || GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;

            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (!MatchActionRules.CanSelectCard(snapshot, GameStateStore.Instance.LocalPlayerId, _selectedCardUI.cardId, out string blockedReason))
            {
                CancelCardSelection();
                ShowActionMessage(blockedReason, true);
                return;
            }

            if (MatchActionRules.RequiresTarget(snapshot, GameStateStore.Instance.LocalPlayerId, _selectedCardUI.cardId) && string.IsNullOrEmpty(_selectedTargetId))
            {
                ShowActionMessage("Hãy chọn một mục tiêu hợp lệ trước khi đánh bài.", true);
                return;
            }

            string cardId = _selectedCardUI.cardId;
            string targetId = _selectedTargetId;
            var info = _selectedCardUI.info;
            var cardRect = _selectedCardUI.GetComponent<RectTransform>();

            // Determine center canvas position for throw target
            Canvas canvas = GetComponentInParent<Canvas>();
            Vector2 centerScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Play audio immediately
            string sfxKey = (cardId != null && cardId.StartsWith("bang")) ? "bang_shot" : "card_play";
            AudioManager.Instance?.PlaySFX(sfxKey);

            // Play card throw animation (non-blocking — runs parallel to below)
            if (cardRect != null && canvas != null)
            {
                // Convert center screen to canvas-local for throw target
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform, centerScreen, null, out Vector2 centerLocal);
                UIAnimator.Instance.PlayCardThrowAnimation(cardRect, centerLocal, canvas, null);
            }

            // Determine target seat screen position for directional FX
            Vector2 targetSeatScreen = centerScreen;
            if (!string.IsNullOrEmpty(targetId))
            {
                var tSeat = _seatUIs.Find(s => s.playerId == targetId);
                if (tSeat != null) targetSeatScreen = tSeat.GetScreenCenterPosition();
            }

            // Play card-specific animation
            bool isBang = cardId != null && (cardId.StartsWith("bang") || cardId.StartsWith("gatling") || cardId.StartsWith("duel"));
            bool isNe = cardId != null && cardId.StartsWith("missed");
            if (isBang && canvas != null && !string.IsNullOrEmpty(targetId))
            {
                UIAnimator.Instance.PlayBangAnimation(canvas,
                    new Vector2(Screen.width * 0.15f, Screen.height * 0.2f),
                    targetSeatScreen, null);
            }
            else if (!isBang && !isNe && canvas != null)
            {
                UIAnimator.Instance.PlayGenericCardAnimation(canvas, centerScreen,
                    info?.vietnameseName ?? info?.name ?? cardId, null);
            }

            CancelCardSelection();

            GameStateStore.Instance.SetRequestPending(true);

            if (GameStateStore.Instance?.Gateway != null)
            {
                var targetList = !string.IsNullOrEmpty(targetId) ? new List<string> { targetId } : null;
                bool ok = await GameStateStore.Instance.Gateway.PlayCardAsync(cardId, targetList);
                if (!ok) HandleRejectedAction("Không thể đánh lá bài này trong trạng thái hiện tại.");
            }
        }

        public void CancelCardSelection()
        {
            if (_selectedCardUI != null) _selectedCardUI.SetSelected(false);
            _selectedCardUI = null;
            _selectedTargetId = null;

            if (FXManager.Instance != null) FXManager.Instance.HideTargetingLine();
            if (targetBannerObj != null) targetBannerObj.SetActive(false);
            if (cardPreviewTooltipObj != null) cardPreviewTooltipObj.SetActive(false);
            if (playCardButton != null) playCardButton.gameObject.SetActive(false);
            if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(false);
            if (_lastSnapshot != null)
            {
                RefreshHandInteractionState(_lastSnapshot);
                string localId = GameStateStore.Instance != null ? GameStateStore.Instance.LocalPlayerId : string.Empty;
                RenderOpponentSeats(_lastSnapshot, localId);
                UpdateTurnGuidance(_lastSnapshot, _lastSnapshot.currentTurnPlayerId == localId, (_lastSnapshot.currentPhase ?? string.Empty).ToUpperInvariant());
            }
        }

        private async void HandleDrawCardClicked()
        {
            if (GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;
            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (snapshot == null || snapshot.currentTurnPlayerId != GameStateStore.Instance.LocalPlayerId ||
                !string.Equals(snapshot.currentPhase, "DRAW", StringComparison.OrdinalIgnoreCase))
            {
                ShowActionMessage("Chỉ có thể rút bài ở bước RÚT trong lượt của bạn.", true);
                return;
            }
            CancelCardSelection();
            AudioManager.Instance?.PlaySFX("card_draw");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                bool ok = await GameStateStore.Instance.Gateway.RequestDrawAsync();
                if (!ok) HandleRejectedAction("Không thể rút bài lúc này.");
            }
        }

        private async void HandleHandCardPlayed(CardUI card, Vector2 screenPos)
        {
            if (GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;
            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (!MatchActionRules.IsLocalPlayPhase(snapshot, GameStateStore.Instance.LocalPlayerId)) return;

            // If card was dragged to the upper 65% of the screen AND doesn't need a target,
            // play it immediately without requiring a separate confirm tap.
            bool draggedHigh = screenPos.y > Screen.height * 0.35f;
            bool needsTarget = MatchActionRules.RequiresTarget(snapshot, GameStateStore.Instance.LocalPlayerId, card.cardId);
            bool canPlay = MatchActionRules.CanSelectCard(snapshot, GameStateStore.Instance.LocalPlayerId, card.cardId, out _);

            if (draggedHigh && !needsTarget && canPlay)
            {
                // Select and immediately play
                if (_selectedCardUI != null && _selectedCardUI != card) CancelCardSelection();
                _selectedCardUI = card;
                _selectedTargetId = null;
                card.SetSelected(true);
                await System.Threading.Tasks.Task.Yield();
                HandlePlayCardButtonClicked();
                return;
            }

            // Default: select the card (player still needs to confirm)
            HandleCardSelected(card);
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async void HandleEndTurnClicked()
        {
            if (GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;
            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (!MatchActionRules.IsLocalPlayPhase(snapshot, GameStateStore.Instance.LocalPlayerId))
            {
                ShowActionMessage("Bạn chỉ có thể kết thúc ở bước ĐÁNH BÀI của lượt mình.", true);
                return;
            }
            CancelCardSelection();
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                bool ok = await GameStateStore.Instance.Gateway.EndTurnAsync();
                if (!ok) HandleRejectedAction("Chưa thể kết thúc lượt. Hãy xử lý yêu cầu đang chờ trước.");
            }
        }

        private void HandleRejectedAction(string message)
        {
            GameStateStore.Instance?.SetRequestPending(false);
            ShowActionMessage(message, true);
            if (_lastSnapshot != null) RenderTableSnapshot(_lastSnapshot);
        }

        private void ShowActionMessage(string message, bool isError)
        {
            if (targetBannerObj != null) UIAnimator.Instance.ShowModal(targetBannerObj, 0.25f);
            if (targetBannerText != null)
            {
                targetBannerText.text = message;
                targetBannerText.color = isError ? new Color(1f, 0.78f, 0.66f) : BangUITheme.Ivory;
            }
            var bannerImage = targetBannerObj != null ? targetBannerObj.GetComponent<Image>() : null;
            if (bannerImage != null) bannerImage.color = isError ? new Color(0.5f, 0.1f, 0.08f, 0.96f) : BangUITheme.SurfaceRaised;
        }
    }
}

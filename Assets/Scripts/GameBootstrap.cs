using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using BangBang.Core.Network;
using BangBang.UI;
using BangBang.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Managers")]
        public TableManager tableManager;
        public HandCardFanLayout handCardLayout;
        public PromptResponseModal promptModal;
        public GameOverUI gameOverUI;
        public LobbyUI lobbyUI;

        [Header("Top Pill Bar Elements")]
        public Text turnCountPillText;
        public Text turnOwnerPillText;
        public Text timerPillText;
        public Button endTurnButton;
        public Text combatLogText;
        public Transform localEquipmentTray;

        private CardUI _selectedCard;
        private List<string> _validTargetIds = new List<string>();
        private readonly List<GameObject> _localEquippedObjs = new List<GameObject>();

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM("western_theme");
            }

            EnsureUIHierarchy();

            if (handCardLayout != null)
            {
                handCardLayout.OnCardDragging += HandleCardDragging;
                handCardLayout.OnCardDropped += HandleCardDropped;
            }

            if (tableManager != null)
            {
                tableManager.OnPlayerSeatSelected += HandleSeatClicked;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(HandleEndTurnClicked);
            }

            if (gameOverUI != null)
            {
                gameOverUI.OnRestartRequested += () =>
                {
                    gameOverUI.Hide();
                    StartMatch(7, "Cao bồi bạn");
                };
            }

            if (lobbyUI != null)
            {
                lobbyUI.OnStartOfflineMatch += (count, pName) => StartMatch(count, pName);
            }

            if (OfflineBotEngine.Instance != null)
            {
                OfflineBotEngine.Instance.OnStateChanged += OnMatchStateChanged;
                OfflineBotEngine.Instance.OnCombatLog += OnCombatLogReceived;
            }

            // Start instant 7-player match matching the digital screenshot
            StartMatch(7, "Cao bồi bạn");
        }

        private void StartMatch(int count, string pName)
        {
            if (OfflineBotEngine.Instance != null)
            {
                OfflineBotEngine.Instance.StartNewMatch(count, pName);
            }
        }

        private void OnMatchStateChanged(MatchStateModel state)
        {
            if (state == null) return;

            var local = state.players.Find(p => p.id == "player_local");

            // Update Table & Opponents
            if (tableManager != null)
            {
                tableManager.SetupTable(state, local != null ? local.id : "player_local");
            }

            // Update Hand Cards
            if (handCardLayout != null && local != null)
            {
                handCardLayout.UpdateHand(local.hand);
            }

            // Update Local Equipment Tray (bottom left next to portrait)
            UpdateLocalEquipmentTray(local);

            // Update Top Pill Bar
            if (turnCountPillText != null)
            {
                turnCountPillText.text = "Turn " + state.turnNumber + " / ∞";
            }

            if (turnOwnerPillText != null)
            {
                var cur = state.players.Find(p => p.id == state.currentTurnPlayerId);
                bool isMyTurn = state.currentTurnPlayerId == (local != null ? local.id : "");
                turnOwnerPillText.text = isMyTurn ? "🟢 Your Turn" : "⏳ " + (cur != null ? cur.name : "Đối thủ");
                turnOwnerPillText.color = isMyTurn ? new Color(0.3f, 1f, 0.4f) : Color.white;
            }

            if (timerPillText != null)
            {
                timerPillText.text = "⌛ 23s";
            }

            if (endTurnButton != null && local != null)
            {
                endTurnButton.interactable = state.currentTurnPlayerId == local.id && state.phase == GamePhase.PlayPhase;
            }

            // Handle Response Modal
            if (state.phase == GamePhase.WaitingResponse && state.pendingBang != null && local != null)
            {
                if (state.pendingBang.targetPlayerId == local.id)
                {
                    string reqType = state.pendingBang.requiredCardType ?? "dodge";
                    bool hasCard = local.hand.Any(c => CardCatalogDatabase.GetTypeOf(c) == reqType || (local.characterId == "calamity_janet"));
                    var shooter = state.players.Find(p => p.id == state.pendingBang.actorPlayerId);

                    promptModal?.ShowPrompt(
                        "BẠN ĐANG BỊ TẤN CÔNG!",
                        (shooter != null ? shooter.name : "Kẻ địch") + " đang nhắm vào bạn! Cần nộp " + (reqType == "dodge" ? "NÉ" : "BANG") + ".",
                        hasCard ? "DÙNG " + (reqType == "dodge" ? "NÉ" : "BANG") : "CHỊU ĐÒN (-1 HP)",
                        hasCard,
                        (useCard) => OfflineBotEngine.Instance?.ResolvePendingResponse(useCard)
                    );
                }
            }

            // Game Over
            if (state.phase == GamePhase.GameOver && !string.IsNullOrEmpty(state.winner))
            {
                gameOverUI?.ShowGameOver(state.winner, state.players);
            }
        }

        private void UpdateLocalEquipmentTray(PlayerModel local)
        {
            if (localEquipmentTray == null || local == null) return;

            foreach (var obj in _localEquippedObjs) Destroy(obj);
            _localEquippedObjs.Clear();

            foreach (var eq in local.equipment)
            {
                var cardInfo = CardCatalogDatabase.GetCardInfo(eq);
                var eqCard = new GameObject("Eq_" + cardInfo.id, typeof(RectTransform), typeof(Image));
                eqCard.transform.SetParent(localEquipmentTray, false);
                var rt = eqCard.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(75, 110);

                var img = eqCard.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite(cardInfo.resourcePath);
                img.color = Color.white;

                _localEquippedObjs.Add(eqCard);
            }
        }

        private void HandleCardDragging(CardUI card, Vector2 screenPos)
        {
            _selectedCard = card;
            var state = OfflineBotEngine.Instance?.State;
            if (state == null) return;

            var local = state.players.Find(p => p.id == "player_local");
            if (local == null) return;

            if (card.info.requiresTarget || card.info.id == "bang")
            {
                var validTargets = BangGameRules.GetValidTargets(state, local.id, card.cardId);
                _validTargetIds = validTargets.Select(t => t.id).ToList();
                tableManager?.HighlightValidTargets(_validTargetIds);

                if (_validTargetIds.Count > 0 && FXManager.Instance != null)
                {
                    var closestSeat = tableManager?.GetSeatByPlayerId(_validTargetIds[0]);
                    if (closestSeat != null)
                    {
                        FXManager.Instance.DrawTargetingLine(screenPos, closestSeat.GetScreenCenterPosition());
                        tableManager?.SetCombatActionDisplay(local.name, closestSeat.playerModel.name, card.cardId);
                    }
                }
            }
        }

        private void HandleCardDropped(CardUI card, Vector2 screenPos)
        {
            tableManager?.ClearTargetHighlights();
            FXManager.Instance?.HideTargetingLine();

            var state = OfflineBotEngine.Instance?.State;
            if (state == null) return;

            var local = state.players.Find(p => p.id == "player_local");
            if (local == null || state.currentTurnPlayerId != local.id) return;

            var seatDroppedOn = tableManager?.GetSeatUnderScreenPosition(screenPos);

            if (screenPos.y > Screen.height * 0.25f)
            {
                if (!card.info.requiresTarget && card.info.id != "bang")
                {
                    OfflineBotEngine.Instance?.LocalPlayerPlayCard(card.cardId);
                    AudioManager.Instance?.PlaySFX("card_play");
                }
                else
                {
                    if (seatDroppedOn != null && _validTargetIds.Contains(seatDroppedOn.playerId))
                    {
                        OfflineBotEngine.Instance?.LocalPlayerPlayCard(card.cardId, seatDroppedOn.playerId);
                        AudioManager.Instance?.PlaySFX("bang_shot");
                        FXManager.Instance?.TriggerScreenShake(6f, 0.2f);
                        FXManager.Instance?.SpawnFloatingText(seatDroppedOn.GetScreenCenterPosition(), "BANG!", Color.red);
                    }
                    else if (_validTargetIds.Count > 0)
                    {
                        string targetId = _validTargetIds[0];
                        var targetSeat = tableManager?.GetSeatByPlayerId(targetId);
                        OfflineBotEngine.Instance?.LocalPlayerPlayCard(card.cardId, targetId);
                        AudioManager.Instance?.PlaySFX("bang_shot");
                        FXManager.Instance?.TriggerScreenShake(6f, 0.2f);
                        if (targetSeat != null) FXManager.Instance?.SpawnFloatingText(targetSeat.GetScreenCenterPosition(), "BANG!", Color.red);
                    }
                }
            }

            _selectedCard = null;
            _validTargetIds.Clear();
        }

        private void HandleSeatClicked(string clickedPlayerId)
        {
            if (_selectedCard != null && _validTargetIds.Contains(clickedPlayerId))
            {
                OfflineBotEngine.Instance?.LocalPlayerPlayCard(_selectedCard.cardId, clickedPlayerId);
                AudioManager.Instance?.PlaySFX("bang_shot");
                FXManager.Instance?.TriggerScreenShake(6f, 0.2f);
                _selectedCard = null;
                tableManager?.ClearTargetHighlights();
                FXManager.Instance?.HideTargetingLine();
            }
        }

        private void HandleEndTurnClicked()
        {
            OfflineBotEngine.Instance?.LocalPlayerEndTurn();
            AudioManager.Instance?.PlaySFX("button_tap");
        }

        private void OnCombatLogReceived(string message, string actionType)
        {
            if (combatLogText != null)
            {
                combatLogText.text = message;
            }

            if (actionType == "bang") AudioManager.Instance?.PlaySFX("bang_shot");
            else if (actionType == "dodge") AudioManager.Instance?.PlaySFX("dodge");
            else if (actionType == "damage") AudioManager.Instance?.PlaySFX("damage");
            else if (actionType == "beer") AudioManager.Instance?.PlaySFX("button_tap");
            else if (actionType == "draw") AudioManager.Instance?.PlaySFX("card_draw");
        }

        private void EnsureUIHierarchy()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                if (FindFirstObjectByType<Camera>() == null)
                {
                    var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    camObj.tag = "MainCamera";
                }

                if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                }
            }

            if (FindFirstObjectByType<AudioManager>() == null)
            {
                new GameObject("AudioManager", typeof(AudioManager));
            }
            if (FindFirstObjectByType<FXManager>() == null)
            {
                new GameObject("FXManager", typeof(FXManager));
            }
            if (FindFirstObjectByType<OfflineBotEngine>() == null)
            {
                new GameObject("OfflineBotEngine", typeof(OfflineBotEngine));
            }
            if (FindFirstObjectByType<BangNetworkClient>() == null)
            {
                new GameObject("BangNetworkClient", typeof(BangNetworkClient));
            }

            // Table Manager & Center Elements
            if (tableManager == null)
            {
                var tableObj = new GameObject("TableManager", typeof(RectTransform), typeof(TableManager));
                tableObj.transform.SetParent(canvas.transform, false);
                tableManager = tableObj.GetComponent<TableManager>();

                var bgObj = new GameObject("TableBackground", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(tableObj.transform, false);
                var bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                tableManager.tableBackground = bgObj.GetComponent<Image>();
                tableManager.tableBackground.color = new Color(0.13f, 0.28f, 0.16f); // Green felt poker table
                tableManager.tableContainer = tableObj.GetComponent<RectTransform>();

                // Center Combat Zone Container
                var centerZoneObj = new GameObject("CenterZone", typeof(RectTransform));
                centerZoneObj.transform.SetParent(tableObj.transform, false);
                var centerZoneRt = centerZoneObj.GetComponent<RectTransform>();
                centerZoneRt.anchoredPosition = new Vector2(0, 30f);
                centerZoneRt.sizeDelta = new Vector2(500, 200);

                // Draw Pile (Left of center)
                var drawObj = new GameObject("DrawPile", typeof(RectTransform), typeof(Image));
                drawObj.transform.SetParent(centerZoneObj.transform, false);
                var drawRt = drawObj.GetComponent<RectTransform>();
                drawRt.anchoredPosition = new Vector2(-150f, 0);
                drawRt.sizeDelta = new Vector2(85, 125);
                var drawImg = drawObj.GetComponent<Image>();
                drawImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                tableManager.drawPileImage = drawImg;

                var drawCountObj = new GameObject("CountBadge", typeof(RectTransform), typeof(Text));
                drawCountObj.transform.SetParent(drawObj.transform, false);
                var drawCountRt = drawCountObj.GetComponent<RectTransform>();
                drawCountRt.anchoredPosition = new Vector2(0, -75f);
                drawCountRt.sizeDelta = new Vector2(80, 24);
                var drawCountTxt = drawCountObj.GetComponent<Text>();
                drawCountTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                drawCountTxt.fontSize = 15;
                drawCountTxt.fontStyle = FontStyle.Bold;
                drawCountTxt.alignment = TextAnchor.MiddleCenter;
                drawCountTxt.color = Color.white;
                drawCountTxt.text = "23";
                tableManager.drawPileCountText = drawCountTxt;

                // Discard Pile (Right of center)
                var discObj = new GameObject("DiscardPile", typeof(RectTransform), typeof(Image));
                discObj.transform.SetParent(centerZoneObj.transform, false);
                var discRt = discObj.GetComponent<RectTransform>();
                discRt.anchoredPosition = new Vector2(150f, 0);
                discRt.sizeDelta = new Vector2(85, 125);
                var discImg = discObj.GetComponent<Image>();
                tableManager.discardPileImage = discImg;

                var discCountObj = new GameObject("CountBadge", typeof(RectTransform), typeof(Text));
                discCountObj.transform.SetParent(discObj.transform, false);
                var discCountRt = discCountObj.GetComponent<RectTransform>();
                discCountRt.anchoredPosition = new Vector2(0, -75f);
                discCountRt.sizeDelta = new Vector2(80, 24);
                var discCountTxt = discCountObj.GetComponent<Text>();
                discCountTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                discCountTxt.fontSize = 15;
                discCountTxt.fontStyle = FontStyle.Bold;
                discCountTxt.alignment = TextAnchor.MiddleCenter;
                discCountTxt.color = Color.white;
                discCountTxt.text = "7";
                tableManager.discardPileCountText = discCountTxt;
            }

            // Hand Cards
            if (handCardLayout == null)
            {
                var handObj = new GameObject("HandCardLayout", typeof(RectTransform), typeof(HandCardFanLayout));
                handObj.transform.SetParent(canvas.transform, false);
                handCardLayout = handObj.GetComponent<HandCardFanLayout>();
                var handRt = handObj.GetComponent<RectTransform>();
                handRt.anchorMin = new Vector2(0.5f, 0f);
                handRt.anchorMax = new Vector2(0.5f, 0f);
                handRt.sizeDelta = new Vector2(650, 180);
            }

            // Local Player Equipment Shelf (Bottom-left from portrait)
            if (localEquipmentTray == null)
            {
                var eqTrayObj = new GameObject("LocalEquipmentTray", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                eqTrayObj.transform.SetParent(canvas.transform, false);
                var eqTrayRt = eqTrayObj.GetComponent<RectTransform>();
                eqTrayRt.anchoredPosition = new Vector2(-540f, -370f);
                eqTrayRt.sizeDelta = new Vector2(250, 120);
                var hlg = eqTrayObj.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.spacing = 8f;
                localEquipmentTray = eqTrayObj.transform;
            }

            // Top Pill Bar
            var topBarObj = new GameObject("TopPillBar", typeof(RectTransform), typeof(Image));
            topBarObj.transform.SetParent(canvas.transform, false);
            var topBarRt = topBarObj.GetComponent<RectTransform>();
            topBarRt.anchorMin = new Vector2(0.5f, 1f);
            topBarRt.anchorMax = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = new Vector2(0, -35f);
            topBarRt.sizeDelta = new Vector2(460, 48);
            var topBarImg = topBarObj.GetComponent<Image>();
            topBarImg.color = new Color(0.1f, 0.15f, 0.1f, 0.95f);

            var turnTxtObj = new GameObject("TurnCountText", typeof(RectTransform), typeof(Text));
            turnTxtObj.transform.SetParent(topBarObj.transform, false);
            var turnTxtRt = turnTxtObj.GetComponent<RectTransform>();
            turnTxtRt.anchoredPosition = new Vector2(-140f, 0);
            turnTxtRt.sizeDelta = new Vector2(120, 36);
            turnCountPillText = turnTxtObj.GetComponent<Text>();
            turnCountPillText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            turnCountPillText.fontSize = 15;
            turnCountPillText.alignment = TextAnchor.MiddleCenter;
            turnCountPillText.color = new Color(0.9f, 0.9f, 0.9f);
            turnCountPillText.text = "Turn 1 / ∞";

            var ownerTxtObj = new GameObject("TurnOwnerText", typeof(RectTransform), typeof(Text));
            ownerTxtObj.transform.SetParent(topBarObj.transform, false);
            var ownerTxtRt = ownerTxtObj.GetComponent<RectTransform>();
            ownerTxtRt.anchoredPosition = new Vector2(0, 0);
            ownerTxtRt.sizeDelta = new Vector2(150, 36);
            turnOwnerPillText = ownerTxtObj.GetComponent<Text>();
            turnOwnerPillText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            turnOwnerPillText.fontSize = 17;
            turnOwnerPillText.fontStyle = FontStyle.Bold;
            turnOwnerPillText.alignment = TextAnchor.MiddleCenter;
            turnOwnerPillText.color = new Color(0.3f, 1f, 0.4f);
            turnOwnerPillText.text = "🟢 Your Turn";

            var timerTxtObj = new GameObject("TimerText", typeof(RectTransform), typeof(Text));
            timerTxtObj.transform.SetParent(topBarObj.transform, false);
            var timerTxtRt = timerTxtObj.GetComponent<RectTransform>();
            timerTxtRt.anchoredPosition = new Vector2(140f, 0);
            timerTxtRt.sizeDelta = new Vector2(100, 36);
            timerPillText = timerTxtObj.GetComponent<Text>();
            timerPillText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerPillText.fontSize = 15;
            timerPillText.alignment = TextAnchor.MiddleCenter;
            timerPillText.color = new Color(1f, 0.85f, 0.3f);
            timerPillText.text = "⌛ 23s";

            // End Turn Button (Bottom Right)
            if (endTurnButton == null)
            {
                var btnObj = new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(canvas.transform, false);
                var btnRt = btnObj.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0f);
                btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.anchoredPosition = new Vector2(720f, 80f);
                btnRt.sizeDelta = new Vector2(180, 65);

                var btnImg = btnObj.GetComponent<Image>();
                btnImg.color = new Color(0.15f, 0.12f, 0.1f, 0.95f); // Sleek dark wood

                var txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                txtObj.transform.SetParent(btnObj.transform, false);
                var txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                var txt = txtObj.GetComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 18;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.95f, 0.9f, 0.8f);
                txt.text = "End Turn";

                endTurnButton = btnObj.GetComponent<Button>();
            }

            // Emote & Chat Buttons (Bottom Left)
            var emoteObj = new GameObject("EmoteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            emoteObj.transform.SetParent(canvas.transform, false);
            var emoteRt = emoteObj.GetComponent<RectTransform>();
            emoteRt.anchoredPosition = new Vector2(-880f, -480f);
            emoteRt.sizeDelta = new Vector2(110, 42);
            var emoteImg = emoteObj.GetComponent<Image>();
            emoteImg.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);
            var emoteTxt = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            emoteTxt.transform.SetParent(emoteObj.transform, false);
            var eTxt = emoteTxt.GetComponent<Text>();
            eTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            eTxt.fontSize = 13;
            eTxt.alignment = TextAnchor.MiddleCenter;
            eTxt.color = Color.white;
            eTxt.text = "😊 Emote";

            var chatObj = new GameObject("ChatButton", typeof(RectTransform), typeof(Image), typeof(Button));
            chatObj.transform.SetParent(canvas.transform, false);
            var chatRt = chatObj.GetComponent<RectTransform>();
            chatRt.anchoredPosition = new Vector2(-760f, -480f);
            chatRt.sizeDelta = new Vector2(110, 42);
            var chatImg = chatObj.GetComponent<Image>();
            chatImg.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);
            var chatTxt = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            chatTxt.transform.SetParent(chatObj.transform, false);
            var cTxt = chatTxt.GetComponent<Text>();
            cTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cTxt.fontSize = 13;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.color = Color.white;
            cTxt.text = "💬 Chat";

            // Action History HUD Ticker
            if (combatLogText == null)
            {
                var logObj = new GameObject("CombatLogText", typeof(RectTransform), typeof(Text));
                logObj.transform.SetParent(canvas.transform, false);
                var lRt = logObj.GetComponent<RectTransform>();
                lRt.anchoredPosition = new Vector2(0, -180f);
                lRt.sizeDelta = new Vector2(600, 30);
                combatLogText = logObj.GetComponent<Text>();
                combatLogText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                combatLogText.fontSize = 14;
                combatLogText.alignment = TextAnchor.MiddleCenter;
                combatLogText.color = new Color(0.95f, 0.85f, 0.6f);
            }
        }
    }
}

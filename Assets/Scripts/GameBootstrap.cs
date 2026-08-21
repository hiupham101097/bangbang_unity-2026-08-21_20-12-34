using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using BangBang.Core.Network;
using BangBang.UI.Screens;
using BangBang.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Screen Manager & Screens")]
        public ScreenManager screenManager;
        public SplashScreenUI splashScreen;
        public HomeScreenUI homeScreen;
        public RoomListScreenUI roomListScreen;
        public RoomLobbyScreenUI roomLobbyScreen;

        [Header("Battle Screen Managers")]
        public TableManager tableManager;
        public HandCardFanLayout handCardLayout;
        public PromptResponseModal promptModal;
        public GameOverUI gameOverUI;

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

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        private void Start()
        {
            EnsureUIHierarchy();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM("western_theme");
            }

            // Bind Screen Transitions
            if (homeScreen != null)
            {
                homeScreen.OnQuickPlayClicked += () => ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
                homeScreen.OnOnlineRoomsClicked += () => ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomList);
            }

            if (roomLobbyScreen != null)
            {
                roomLobbyScreen.OnStartGameRequested += () => StartMatch(7, "Cao bồi của bạn");
            }

            // Bind Battle UI Events
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
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
                };
            }

            if (OfflineBotEngine.Instance != null)
            {
                OfflineBotEngine.Instance.OnStateChanged += OnMatchStateChanged;
                OfflineBotEngine.Instance.OnCombatLog += OnCombatLogReceived;
            }

            // Start with Splash Loading screen
            ScreenManager.Instance?.SwitchToScreen(AppScreenState.Splash, useFade: false);
        }

        public void StartMatch(int count, string pName)
        {
            ScreenManager.Instance?.SwitchToScreen(AppScreenState.Battle);
            if (OfflineBotEngine.Instance != null)
            {
                OfflineBotEngine.Instance.StartNewMatch(count, pName);
            }
        }

        private void OnMatchStateChanged(MatchStateModel state)
        {
            if (state == null) return;

            var local = state.players.Find(p => p.id == "player_local");

            // Update Table
            if (tableManager != null)
            {
                tableManager.SetupTable(state, local != null ? local.id : "player_local");
            }

            // Update Hand
            if (handCardLayout != null && local != null)
            {
                handCardLayout.UpdateHand(local.hand);
            }

            // Update Local Equipment Shelf
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

            // Response Modal
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

                // Unity 6 Input System Support
                EnsureEventSystem();
            }

            if (FindFirstObjectByType<AudioManager>() == null) new GameObject("AudioManager", typeof(AudioManager));
            if (FindFirstObjectByType<FXManager>() == null) new GameObject("FXManager", typeof(FXManager));
            if (FindFirstObjectByType<OfflineBotEngine>() == null) new GameObject("OfflineBotEngine", typeof(OfflineBotEngine));
            if (FindFirstObjectByType<BangNetworkClient>() == null) new GameObject("BangNetworkClient", typeof(BangNetworkClient));

            // Screen Manager
            if (screenManager == null)
            {
                var smObj = new GameObject("ScreenManager", typeof(ScreenManager));
                smObj.transform.SetParent(canvas.transform, false);
                screenManager = smObj.GetComponent<ScreenManager>();
            }

            // 1. Splash Screen Panel
            if (splashScreen == null)
            {
                var splashObj = new GameObject("SplashScreenPanel", typeof(RectTransform), typeof(Image), typeof(SplashScreenUI));
                splashObj.transform.SetParent(canvas.transform, false);
                var sRt = splashObj.GetComponent<RectTransform>();
                sRt.anchorMin = Vector2.zero;
                sRt.anchorMax = Vector2.one;
                sRt.sizeDelta = Vector2.zero;

                splashScreen = splashObj.GetComponent<SplashScreenUI>();
                splashScreen.backgroundImage = splashObj.GetComponent<Image>();

                // Logo
                var logoObj = new GameObject("Logo", typeof(RectTransform), typeof(Image));
                logoObj.transform.SetParent(splashObj.transform, false);
                var logoRt = logoObj.GetComponent<RectTransform>();
                logoRt.anchoredPosition = new Vector2(0, 80f);
                logoRt.sizeDelta = new Vector2(500, 250);
                splashScreen.logoImage = logoObj.GetComponent<Image>();
                splashScreen.logoImage.preserveAspect = true;

                // Loading Bar BG
                var barBgObj = new GameObject("LoadingBarBg", typeof(RectTransform), typeof(Image));
                barBgObj.transform.SetParent(splashObj.transform, false);
                var barBgRt = barBgObj.GetComponent<RectTransform>();
                barBgRt.anchoredPosition = new Vector2(0, -180f);
                barBgRt.sizeDelta = new Vector2(500, 24);
                var barBgImg = barBgObj.GetComponent<Image>();
                barBgImg.color = new Color(0.15f, 0.1f, 0.08f, 0.9f);

                // Loading Bar Fill
                var barFillObj = new GameObject("LoadingBarFill", typeof(RectTransform), typeof(Image));
                barFillObj.transform.SetParent(barBgObj.transform, false);
                var barFillRt = barFillObj.GetComponent<RectTransform>();
                barFillRt.anchorMin = Vector2.zero;
                barFillRt.anchorMax = Vector2.one;
                var barFillImg = barFillObj.GetComponent<Image>();
                barFillImg.type = Image.Type.Filled;
                barFillImg.fillMethod = Image.FillMethod.Horizontal;
                barFillImg.color = new Color(0.95f, 0.65f, 0.2f);
                splashScreen.loadingProgressBar = barFillImg;

                // Status Text
                var statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
                statusObj.transform.SetParent(splashObj.transform, false);
                var statusRt = statusObj.GetComponent<RectTransform>();
                statusRt.anchoredPosition = new Vector2(0, -140f);
                statusRt.sizeDelta = new Vector2(600, 30);
                var statusTxt = statusObj.GetComponent<Text>();
                statusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                statusTxt.fontSize = 16;
                statusTxt.fontStyle = FontStyle.Bold;
                statusTxt.alignment = TextAnchor.MiddleCenter;
                statusTxt.color = Color.white;
                splashScreen.loadingStatusText = statusTxt;

                screenManager.splashPanel = splashObj;
            }

            // 2. Home Screen Panel
            if (homeScreen == null)
            {
                var homeObj = new GameObject("HomeScreenPanel", typeof(RectTransform), typeof(Image), typeof(HomeScreenUI));
                homeObj.transform.SetParent(canvas.transform, false);
                var hRt = homeObj.GetComponent<RectTransform>();
                hRt.anchorMin = Vector2.zero;
                hRt.anchorMax = Vector2.one;
                hRt.sizeDelta = Vector2.zero;

                homeScreen = homeObj.GetComponent<HomeScreenUI>();
                homeScreen.backgroundImage = homeObj.GetComponent<Image>();

                // Title Banner
                var titleObj = new GameObject("TitleBanner", typeof(RectTransform), typeof(Image));
                titleObj.transform.SetParent(homeObj.transform, false);
                var titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchoredPosition = new Vector2(0, 320f);
                titleRt.sizeDelta = new Vector2(480, 180);
                var titleImg = titleObj.GetComponent<Image>();
                titleImg.sprite = CardCatalogDatabase.LoadSprite("bang_bang_logo");
                titleImg.preserveAspect = true;

                // Quick Play Button
                var qpBtnObj = CreateHomeMenuButton("QuickPlayButton", "🤠 CHƠI NHANH (OFFLINE BOT)", new Vector2(0, 80f), new Color(0.85f, 0.45f, 0.15f));
                qpBtnObj.transform.SetParent(homeObj.transform, false);
                homeScreen.quickPlayButton = qpBtnObj.GetComponent<Button>();

                // Online Rooms Button
                var onlBtnObj = CreateHomeMenuButton("OnlineRoomsButton", "🌐 PHÒNG MULTIPLAYER", new Vector2(0, -10f), new Color(0.2f, 0.55f, 0.85f));
                onlBtnObj.transform.SetParent(homeObj.transform, false);
                homeScreen.onlineRoomsButton = onlBtnObj.GetComponent<Button>();

                screenManager.homePanel = homeObj;
            }

            // 3. Room List Screen Panel
            if (roomListScreen == null)
            {
                var rlObj = new GameObject("RoomListScreenPanel", typeof(RectTransform), typeof(Image), typeof(RoomListScreenUI));
                rlObj.transform.SetParent(canvas.transform, false);
                var rlRt = rlObj.GetComponent<RectTransform>();
                rlRt.anchorMin = Vector2.zero;
                rlRt.anchorMax = Vector2.one;
                rlRt.sizeDelta = Vector2.zero;

                roomListScreen = rlObj.GetComponent<RoomListScreenUI>();
                roomListScreen.backgroundImage = rlObj.GetComponent<Image>();

                // Header
                var headObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(Text));
                headObj.transform.SetParent(rlObj.transform, false);
                var headRt = headObj.GetComponent<RectTransform>();
                headRt.anchoredPosition = new Vector2(0, 420f);
                headRt.sizeDelta = new Vector2(600, 50);
                var headTxt = headObj.GetComponent<Text>();
                headTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                headTxt.fontSize = 28;
                headTxt.fontStyle = FontStyle.Bold;
                headTxt.alignment = TextAnchor.MiddleCenter;
                headTxt.color = new Color(1f, 0.85f, 0.3f);
                headTxt.text = "DANH SÁCH BÀN CHƠI ONLINE";

                // Content Scroll Container
                var listContainer = new GameObject("RoomListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
                listContainer.transform.SetParent(rlObj.transform, false);
                var listRt = listContainer.GetComponent<RectTransform>();
                listRt.anchoredPosition = new Vector2(0, 60f);
                listRt.sizeDelta = new Vector2(850, 500);
                var vlg = listContainer.GetComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 15f;
                roomListScreen.roomListContentTransform = listContainer.transform;

                // Back Button
                var backBtnObj = CreateHomeMenuButton("BackBtn", "⬅ QUAY LẠI", new Vector2(-650f, 430f), new Color(0.4f, 0.25f, 0.15f), new Vector2(160, 50));
                backBtnObj.transform.SetParent(rlObj.transform, false);
                roomListScreen.backToHomeButton = backBtnObj.GetComponent<Button>();

                screenManager.roomListPanel = rlObj;
            }

            // 4. Room Lobby Screen Panel
            if (roomLobbyScreen == null)
            {
                var lobbyObj = new GameObject("RoomLobbyScreenPanel", typeof(RectTransform), typeof(Image), typeof(RoomLobbyScreenUI));
                lobbyObj.transform.SetParent(canvas.transform, false);
                var lobbyRt = lobbyObj.GetComponent<RectTransform>();
                lobbyRt.anchorMin = Vector2.zero;
                lobbyRt.anchorMax = Vector2.one;
                lobbyRt.sizeDelta = Vector2.zero;

                roomLobbyScreen = lobbyObj.GetComponent<RoomLobbyScreenUI>();
                roomLobbyScreen.backgroundImage = lobbyObj.GetComponent<Image>();

                // Header
                var titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
                titleObj.transform.SetParent(lobbyObj.transform, false);
                var titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchoredPosition = new Vector2(0, 420f);
                titleRt.sizeDelta = new Vector2(800, 50);
                var titleTxt = titleObj.GetComponent<Text>();
                titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleTxt.fontSize = 26;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.color = new Color(1f, 0.85f, 0.3f);
                roomLobbyScreen.roomTitleText = titleTxt;

                // 7 Slots Container
                var slotsObj = new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                slotsObj.transform.SetParent(lobbyObj.transform, false);
                var slotsRt = slotsObj.GetComponent<RectTransform>();
                slotsRt.anchoredPosition = new Vector2(0, 60f);
                slotsRt.sizeDelta = new Vector2(1300, 260);
                var hlg = slotsObj.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 15f;
                roomLobbyScreen.playerSlotsContainer = slotsObj.transform;

                // Controls Bottom
                var startBtnObj = CreateHomeMenuButton("StartBtn", "🚀 BẮT ĐẦU TRẬN ĐẤU", new Vector2(300f, -380f), new Color(0.2f, 0.7f, 0.25f), new Vector2(280, 65));
                startBtnObj.transform.SetParent(lobbyObj.transform, false);
                roomLobbyScreen.startGameButton = startBtnObj.GetComponent<Button>();

                var addBotBtnObj = CreateHomeMenuButton("AddBotBtn", "➕ THÊM BOT", new Vector2(0f, -380f), new Color(0.85f, 0.5f, 0.15f), new Vector2(220, 65));
                addBotBtnObj.transform.SetParent(lobbyObj.transform, false);
                roomLobbyScreen.addBotButton = addBotBtnObj.GetComponent<Button>();

                var leaveBtnObj = CreateHomeMenuButton("LeaveBtn", "⬅ RỜI PHÒNG", new Vector2(-300f, -380f), new Color(0.45f, 0.25f, 0.15f), new Vector2(220, 65));
                leaveBtnObj.transform.SetParent(lobbyObj.transform, false);
                roomLobbyScreen.leaveRoomButton = leaveBtnObj.GetComponent<Button>();

                screenManager.roomLobbyPanel = lobbyObj;
            }

            // 5. Battle Screen Panel (Saloon Table matching digital screenshot)
            if (tableManager == null)
            {
                var battleObj = new GameObject("BattleScreenPanel", typeof(RectTransform));
                battleObj.transform.SetParent(canvas.transform, false);
                var battleRt = battleObj.GetComponent<RectTransform>();
                battleRt.anchorMin = Vector2.zero;
                battleRt.anchorMax = Vector2.one;
                battleRt.sizeDelta = Vector2.zero;

                var tableObj = new GameObject("TableManager", typeof(RectTransform), typeof(TableManager));
                tableObj.transform.SetParent(battleObj.transform, false);
                tableManager = tableObj.GetComponent<TableManager>();

                var bgObj = new GameObject("TableBackground", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(tableObj.transform, false);
                var bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;
                tableManager.tableBackground = bgObj.GetComponent<Image>();
                tableManager.tableBackground.color = new Color(0.13f, 0.28f, 0.16f); // Green felt poker
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

                // Hand Cards Fan Layout
                var handObj = new GameObject("HandCardLayout", typeof(RectTransform), typeof(HandCardFanLayout));
                handObj.transform.SetParent(battleObj.transform, false);
                handCardLayout = handObj.GetComponent<HandCardFanLayout>();
                var handRt = handObj.GetComponent<RectTransform>();
                handRt.anchorMin = new Vector2(0.5f, 0f);
                handRt.anchorMax = new Vector2(0.5f, 0f);
                handRt.sizeDelta = new Vector2(650, 180);

                // Local Equipment Shelf
                var eqTrayObj = new GameObject("LocalEquipmentTray", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                eqTrayObj.transform.SetParent(battleObj.transform, false);
                var eqTrayRt = eqTrayObj.GetComponent<RectTransform>();
                eqTrayRt.anchoredPosition = new Vector2(-540f, -370f);
                eqTrayRt.sizeDelta = new Vector2(250, 120);
                var ehlg = eqTrayObj.GetComponent<HorizontalLayoutGroup>();
                ehlg.childAlignment = TextAnchor.MiddleLeft;
                ehlg.spacing = 8f;
                localEquipmentTray = eqTrayObj.transform;

                // Top Pill Bar
                var topBarObj = new GameObject("TopPillBar", typeof(RectTransform), typeof(Image));
                topBarObj.transform.SetParent(battleObj.transform, false);
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

                // End Turn Button
                var btnObj = new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(battleObj.transform, false);
                var btnRt = btnObj.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0f);
                btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.anchoredPosition = new Vector2(720f, 80f);
                btnRt.sizeDelta = new Vector2(180, 65);

                var btnImg = btnObj.GetComponent<Image>();
                btnImg.color = new Color(0.15f, 0.12f, 0.1f, 0.95f);

                var btxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                btxtObj.transform.SetParent(btnObj.transform, false);
                var btxtRt = btxtObj.GetComponent<RectTransform>();
                btxtRt.anchorMin = Vector2.zero;
                btxtRt.anchorMax = Vector2.one;
                var btxt = btxtObj.GetComponent<Text>();
                btxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btxt.fontSize = 18;
                btxt.fontStyle = FontStyle.Bold;
                btxt.alignment = TextAnchor.MiddleCenter;
                btxt.color = new Color(0.95f, 0.9f, 0.8f);
                btxt.text = "End Turn";
                endTurnButton = btnObj.GetComponent<Button>();

                // Action History
                var logObj = new GameObject("CombatLogText", typeof(RectTransform), typeof(Text));
                logObj.transform.SetParent(battleObj.transform, false);
                var lRt = logObj.GetComponent<RectTransform>();
                lRt.anchoredPosition = new Vector2(0, -180f);
                lRt.sizeDelta = new Vector2(600, 30);
                combatLogText = logObj.GetComponent<Text>();
                combatLogText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                combatLogText.fontSize = 14;
                combatLogText.alignment = TextAnchor.MiddleCenter;
                combatLogText.color = new Color(0.95f, 0.85f, 0.6f);

                screenManager.battlePanel = battleObj;
            }
        }

        private GameObject CreateHomeMenuButton(string name, string text, Vector2 anchoredPos, Color color, Vector2? size = null)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size ?? new Vector2(360, 65);

            var img = btnObj.GetComponent<Image>();
            img.color = color;

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
            txt.color = Color.white;
            txt.text = text;

            return btnObj;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                // Try add InputSystemUIInputModule first, fallback to StandaloneInputModule
                var inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModuleType != null)
                {
                    esObj.AddComponent(inputModuleType);
                }
                else
                {
                    esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
            }
        }
    }
}

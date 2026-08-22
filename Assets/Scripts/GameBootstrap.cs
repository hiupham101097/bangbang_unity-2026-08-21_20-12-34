using System;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using BangBang.UI.Interaction;
using BangBang.UI.Views;
using BangBang.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [Header("Networking Mode")]
        public bool useLiveCloudflareServer = false;
        public string cloudflareWorkerUrl = "https://blue-frog-fec8.hieupham101097.workers.dev";

        [Header("Core Architecture Controllers")]
        public GameStateStore gameStateStore;
        public GameFlowController flowController;
        public InteractionController interactionController;
        public BangLiveGateway liveGateway;

        [Header("Views")]
        public HomeScreenUI homeScreen;
        public CardGalleryView cardGalleryView;
        public LobbyView lobbyView;
        public WaitingRoomView waitingRoomView;
        public RoleRevealView roleRevealView;
        public CharacterSelectionView characterSelectionView;
        public GameTableView gameTableView;
        public ResultView resultView;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        private async void Start()
        {
            EnsureUIHierarchy();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM("western_theme");
            }

            // Initialize Gateways & State Store
            IGameGateway activeGateway = liveGateway;
            if (gameStateStore != null)
            {
                gameStateStore.BindGateway(activeGateway);
            }

            // Start User Session
            string deviceId = PlayerPrefs.GetString("bang_device_id", Guid.NewGuid().ToString("N").Substring(0, 16));
            PlayerPrefs.SetString("bang_device_id", deviceId);
            await activeGateway.InitializeSessionAsync(deviceId, "Cao bồi viễn tây");

            // Show Home Screen initially
            ShowHomeScreen();
        }

        public void ShowHomeScreen()
        {
            if (homeScreen != null) homeScreen.gameObject.SetActive(true);
            if (cardGalleryView != null) cardGalleryView.gameObject.SetActive(false);
            if (lobbyView != null) lobbyView.gameObject.SetActive(false);
            if (waitingRoomView != null) waitingRoomView.gameObject.SetActive(false);
            if (roleRevealView != null) roleRevealView.gameObject.SetActive(false);
            if (characterSelectionView != null) characterSelectionView.gameObject.SetActive(false);
            if (gameTableView != null) gameTableView.gameObject.SetActive(false);
            if (resultView != null) resultView.gameObject.SetActive(false);
        }

        private void EnsureUIHierarchy()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1.0f; // Landscape: match height

                if (FindAnyObjectByType<Camera>() == null)
                {
                    var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    camObj.tag = "MainCamera";
                }

                EnsureEventSystem();
            }

            if (FindAnyObjectByType<AudioManager>() == null) new GameObject("AudioManager", typeof(AudioManager));
            if (FindAnyObjectByType<FXManager>() == null) new GameObject("FXManager", typeof(FXManager));

            // Gateways
            if (liveGateway == null)
            {
                var gateway = new GameObject("Gateway").AddComponent<BangLiveGateway>();
                liveGateway = gateway;
                liveGateway.serverWsUrl = useLiveCloudflareServer ? cloudflareWorkerUrl : "ws://localhost:3000";
            }

            // State Store & Flow
            if (gameStateStore == null) gameStateStore = gameObject.AddComponent<GameStateStore>();
            if (flowController == null) flowController = gameObject.AddComponent<GameFlowController>();

            // 0. HOME SCREEN (Trang Chủ)
            if (homeScreen == null)
            {
                var homeObj = CreateFullScreenPanel("HomeScreen", canvas.transform);
                homeScreen = homeObj.AddComponent<HomeScreenUI>();
                homeScreen.backgroundImage = homeObj.GetComponent<Image>();

                // Logo Banner
                var logoObj = new GameObject("LogoBanner", typeof(RectTransform), typeof(Image));
                logoObj.transform.SetParent(homeObj.transform, false);
                var logoRt = logoObj.GetComponent<RectTransform>();
                logoRt.anchoredPosition = new Vector2(0, 300f);
                logoRt.sizeDelta = new Vector2(500, 240);
                homeScreen.logoImage = logoObj.GetComponent<Image>();

                // 4 Main Buttons
                homeScreen.startButton = CreateButton("StartBtn", "🤠 CHƠI ONLINE", new Vector2(0, 70f), new Color(0.85f, 0.45f, 0.15f), homeObj.transform, new Vector2(360, 65));
                homeScreen.galleryButton = CreateButton("GalleryBtn", "🃏 BỘ SƯU TẬP THẺ", new Vector2(0, -5f), new Color(0.2f, 0.55f, 0.75f), homeObj.transform, new Vector2(360, 60));
                homeScreen.questsButton = CreateButton("QuestsBtn", "📜 NHIỆM VỤ", new Vector2(0, -75f), new Color(0.25f, 0.55f, 0.8f), homeObj.transform, new Vector2(360, 60));
                homeScreen.guideButton = CreateButton("GuideBtn", "📖 HƯỚNG DẪN", new Vector2(0, -145f), new Color(0.45f, 0.3f, 0.2f), homeObj.transform, new Vector2(360, 60));

                // Audio Toggle Top Right
                homeScreen.audioToggleButton = CreateButton("AudioBtn", "🔊 BẬT TIẾNG", new Vector2(680f, 430f), new Color(0.3f, 0.3f, 0.3f, 0.8f), homeObj.transform, new Vector2(180, 48));
                homeScreen.audioToggleText = homeScreen.audioToggleButton.GetComponentInChildren<Text>();

                // Quests Popup
                var qPopup = CreatePopupBox("QuestsPopup", "📜 NHIỆM VỤ HẰNG NGÀY", "1. Bắn trúng 3 phát BANG! (Thưởng: 500 Vàng)\n2. Uống 2 chai BIA hồi máu (Thưởng: 300 Vàng)\n3. Thắng 1 trận với vai trò Cảnh Sát Trưởng (Thưởng: 1,000 Vàng)", homeObj.transform);
                homeScreen.questsPopup = qPopup.Item1;
                homeScreen.closeQuestsButton = qPopup.Item2;

                // Guide Popup
                var gPopup = CreatePopupBox("GuidePopup", "📖 HƯỚNG DẪN LUẬT CHƠI BANG!", "• CẢNH SÁT TRƯỞNG: Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội.\n• PHÓ CẢNH SÁT: Bảo vệ Cảnh Sát Trưởng bằng mọi giá.\n• CƯỚP (OUTLAW): Tiêu diệt Cảnh Sát Trưởng.\n• KẺ PHẢN BỘI: Người sống sót cuối cùng và hạ Cảnh Sát Trưởng sau cùng.\n\n• CỰ LY BẮN: Khoảng cách ngắn nhất quanh bàn. Vũ khí tăng tầm bắn.", homeObj.transform);
                homeScreen.guidePopup = gPopup.Item1;
                homeScreen.closeGuideButton = gPopup.Item2;
            }

            // CARD GALLERY VIEW
            if (cardGalleryView == null)
            {
                var galleryObj = CreateFullScreenPanel("CardGalleryView", canvas.transform);
                cardGalleryView = galleryObj.AddComponent<CardGalleryView>();

                // Header
                var headObj = CreateText("Header", "🃏 BỘ SƯU TẬP THẺ BÀI", new Vector2(0, 440f), new Vector2(600, 50), 28, new Color(1f, 0.85f, 0.3f), galleryObj.transform);
                cardGalleryView.titleText = headObj.GetComponent<Text>();
                cardGalleryView.backButton = CreateButton("BackBtn", "⬅ QUAY LẠI", new Vector2(-680f, 440f), new Color(0.45f, 0.25f, 0.15f), galleryObj.transform, new Vector2(180, 50));

                // Scroll View Container
                var scrollObj = new GameObject("CardScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
                scrollObj.transform.SetParent(galleryObj.transform, false);
                var scrollRt = scrollObj.GetComponent<RectTransform>();
                scrollRt.anchoredPosition = new Vector2(0, -30f);
                scrollRt.sizeDelta = new Vector2(1400, 780);
                var scrollBg = scrollObj.GetComponent<Image>();
                scrollBg.color = new Color(0, 0, 0, 0.35f);

                var sRect = scrollObj.GetComponent<ScrollRect>();
                sRect.horizontal = false;
                sRect.vertical = true;

                // Viewport
                var viewPort = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
                viewPort.transform.SetParent(scrollObj.transform, false);
                var vpRt = viewPort.GetComponent<RectTransform>();
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.sizeDelta = Vector2.zero;
                viewPort.GetComponent<Mask>().showMaskGraphic = false;

                // Content Grid
                var contentObj = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
                contentObj.transform.SetParent(viewPort.transform, false);
                var cRt = contentObj.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0, 1);
                cRt.anchorMax = new Vector2(1, 1);
                cRt.pivot = new Vector2(0.5f, 1);
                cRt.sizeDelta = new Vector2(0, 800);

                var glg = contentObj.GetComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(160, 230);
                glg.spacing = new Vector2(25, 25);
                glg.padding = new RectOffset(30, 30, 30, 30);
                glg.childAlignment = TextAnchor.UpperCenter;

                var csf = contentObj.GetComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                sRect.viewport = vpRt;
                sRect.content = cRt;
                cardGalleryView.scrollRect = sRect;
                cardGalleryView.gridContent = contentObj.transform;

                // Detail Modal
                var modalRoot = CreateFullScreenPanel("DetailModal", galleryObj.transform);
                modalRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);

                var boxObj = new GameObject("DetailBox", typeof(RectTransform), typeof(Image));
                boxObj.transform.SetParent(modalRoot.transform, false);
                var bRt = boxObj.GetComponent<RectTransform>();
                bRt.sizeDelta = new Vector2(580, 720);
                boxObj.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f);

                var dCardImgObj = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
                dCardImgObj.transform.SetParent(boxObj.transform, false);
                var dArtRt = dCardImgObj.GetComponent<RectTransform>();
                dArtRt.anchoredPosition = new Vector2(0, 140f);
                dArtRt.sizeDelta = new Vector2(240, 340);
                var dCardImg = dCardImgObj.GetComponent<Image>();
                dCardImg.preserveAspect = true;

                var nameTxtObj = CreateText("Name", "BANG!", new Vector2(0, -60f), new Vector2(500, 45), 26, new Color(1f, 0.85f, 0.3f), boxObj.transform);
                var typeTxtObj = CreateText("Type", "Hành động", new Vector2(0, -105f), new Vector2(500, 30), 16, new Color(0.9f, 0.6f, 0.2f), boxObj.transform);
                var rangeTxtObj = CreateText("Range", "Tầm bắn", new Vector2(0, -145f), new Vector2(500, 30), 15, new Color(0.3f, 0.8f, 1f), boxObj.transform);
                var descTxtObj = CreateText("Desc", "Mô tả", new Vector2(0, -220f), new Vector2(500, 110), 15, Color.white, boxObj.transform);
                descTxtObj.GetComponent<Text>().alignment = TextAnchor.UpperCenter;

                var closeBtn = CreateButton("CloseBtn", "ĐÓNG", new Vector2(0, -305f), new Color(0.85f, 0.45f, 0.15f), boxObj.transform, new Vector2(200, 50));

                cardGalleryView.detailModal = modalRoot;
                cardGalleryView.detailCardImage = dCardImg;
                cardGalleryView.detailCardName = nameTxtObj.GetComponent<Text>();
                cardGalleryView.detailCardType = typeTxtObj.GetComponent<Text>();
                cardGalleryView.detailCardRange = rangeTxtObj.GetComponent<Text>();
                cardGalleryView.detailCardDesc = descTxtObj.GetComponent<Text>();
                cardGalleryView.closeDetailButton = closeBtn;

                galleryObj.SetActive(false);
            }

            if (homeScreen != null && cardGalleryView != null)
            {
                homeScreen.cardGalleryView = cardGalleryView;
            }

            // 1. LOBBY VIEW (Sảnh Chờ / Danh Sách Phòng)
            if (lobbyView == null)
            {
                var lobbyObj = CreateFullScreenPanel("LobbyView", canvas.transform);
                lobbyView = lobbyObj.AddComponent<LobbyView>();
                lobbyView.backgroundImage = lobbyObj.GetComponent<Image>();

                // Back to Home Button
                lobbyView.backToHomeButton = CreateButton("BackHomeBtn", "⬅ TRANG CHỦ", new Vector2(-680f, 430f), new Color(0.45f, 0.25f, 0.15f), lobbyObj.transform, new Vector2(180, 50));

                // Header Title
                var headObj = CreateText("Header", "🤠 SẢNH CHỜ VIỄN TÂY", new Vector2(0, 430f), new Vector2(500, 50), 28, new Color(1f, 0.85f, 0.3f), lobbyObj.transform);
                lobbyView.titleText = headObj.GetComponent<Text>();

                // Actions Bottom
                lobbyView.openCreateRoomPopupButton = CreateButton("CreateRoomBtn", "➕ TẠO PHÒNG MỚI", new Vector2(-220f, -390f), new Color(0.85f, 0.45f, 0.15f), lobbyObj.transform, new Vector2(280, 65));
                lobbyView.joinByPinButton = CreateButton("JoinPinBtn", "🔑 VÀO BẰNG MÃ PIN", new Vector2(220f, -390f), new Color(0.2f, 0.55f, 0.85f), lobbyObj.transform, new Vector2(280, 65));

                // Room List Container
                var listContainer = new GameObject("RoomListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
                listContainer.transform.SetParent(lobbyObj.transform, false);
                var listRt = listContainer.GetComponent<RectTransform>();
                listRt.anchoredPosition = new Vector2(0, 40f);
                listRt.sizeDelta = new Vector2(900, 520);
                var vlg = listContainer.GetComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 15f;
                lobbyView.roomListContentTransform = listContainer.transform;

                // Create Room Popup (with bot option)
                var crPopup = CreatePopupBox("CreateRoomPopup", "➕ TẠO PHÒNG MỚI", "Cài đặt phòng chơi Saloon:", lobbyObj.transform, "TẠO PHÒNG");
                lobbyView.createRoomPopup = crPopup.Item1;
                lobbyView.confirmCreateRoomButton = crPopup.Item2;
                lobbyView.closeCreateRoomPopupButton = crPopup.Item3;

                flowController.lobbyView = lobbyView;
            }

            // 2. WAITING ROOM VIEW (Phòng Chờ)
            if (waitingRoomView == null)
            {
                var waitingObj = CreateFullScreenPanel("WaitingRoomView", canvas.transform);
                waitingRoomView = waitingObj.AddComponent<WaitingRoomView>();

                var codeObj = CreateText("RoomCodeText", "MÃ PHÒNG: SALOON", new Vector2(0, 420f), new Vector2(600, 50), 26, Color.yellow, waitingObj.transform);
                waitingRoomView.roomCodeText = codeObj.GetComponent<Text>();

                var reasonObj = CreateText("ReasonText", "Đang chờ người chơi sẵn sàng...", new Vector2(0, 360f), new Vector2(800, 30), 14, Color.white, waitingObj.transform);
                waitingRoomView.startDisabledReasonText = reasonObj.GetComponent<Text>();

                var seatsContainer = new GameObject("SeatsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                seatsContainer.transform.SetParent(waitingObj.transform, false);
                var sRt = seatsContainer.GetComponent<RectTransform>();
                sRt.anchoredPosition = new Vector2(0, 30f);
                sRt.sizeDelta = new Vector2(1300, 260);
                var hlg = seatsContainer.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 15f;
                waitingRoomView.seatsContainer = seatsContainer.transform;

                // Controls: Start, Add Bot, Ready, Leave
                waitingRoomView.startGameButton = CreateButton("StartBtn", "🚀 BẮT ĐẦU TRẬN ĐẤU", new Vector2(330f, -380f), new Color(0.2f, 0.7f, 0.25f), waitingObj.transform, new Vector2(250, 65));
                waitingRoomView.addBotButton = CreateButton("AddBotBtn", "🤖 THÊM BOT", new Vector2(100f, -380f), new Color(0.85f, 0.5f, 0.15f), waitingObj.transform, new Vector2(180, 65));
                waitingRoomView.readyToggleButton = CreateButton("ReadyBtn", "SẴN SÀNG", new Vector2(-100f, -380f), new Color(0.2f, 0.55f, 0.85f), waitingObj.transform, new Vector2(180, 65));
                waitingRoomView.leaveRoomButton = CreateButton("LeaveBtn", "⬅ RỜI PHÒNG", new Vector2(-310f, -380f), new Color(0.45f, 0.25f, 0.15f), waitingObj.transform, new Vector2(180, 65));

                flowController.waitingRoomView = waitingRoomView;
            }

            // 3. ROLE REVEAL VIEW
            if (roleRevealView == null)
            {
                var roleObj = CreateFullScreenPanel("RoleRevealView", canvas.transform);
                roleRevealView = roleObj.AddComponent<RoleRevealView>();

                var titleObj = CreateText("RoleTitle", "VAI TRÒ CỦA BẠN", new Vector2(0, 300f), new Vector2(600, 40), 22, Color.yellow, roleObj.transform);
                roleRevealView.roleTitleText = titleObj.GetComponent<Text>();

                var roleCardsContainer = new GameObject("RoleCardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                roleCardsContainer.transform.SetParent(roleObj.transform, false);
                var cRt = roleCardsContainer.GetComponent<RectTransform>();
                cRt.anchoredPosition = new Vector2(0, 50f);
                cRt.sizeDelta = new Vector2(1100, 320);
                var hlg = roleCardsContainer.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 20f;
                roleRevealView.roleCardsContainer = roleCardsContainer.transform;

                var goalObj = CreateText("RoleGoal", "Mục tiêu chiến thắng...", new Vector2(0, -180f), new Vector2(800, 50), 16, Color.white, roleObj.transform);
                roleRevealView.roleGoalText = goalObj.GetComponent<Text>();

                var timerObj = CreateText("TimerCountdown", "", new Vector2(0, -250f), new Vector2(500, 30), 14, new Color(0.8f, 0.8f, 0.8f), roleObj.transform);
                roleRevealView.timerCountdownText = timerObj.GetComponent<Text>();

                roleRevealView.continueButton = CreateButton("ContinueBtn", "TIẾP TỤC", new Vector2(0, -320f), new Color(0.2f, 0.7f, 0.25f), roleObj.transform, new Vector2(250, 60));

                flowController.roleRevealView = roleRevealView;
            }

            // 4. CHARACTER SELECTION VIEW
            if (characterSelectionView == null)
            {
                var charObj = CreateFullScreenPanel("CharacterSelectionView", canvas.transform);
                characterSelectionView = charObj.AddComponent<CharacterSelectionView>();

                CreateText("Header", "CHỌN TƯỚNG BẮT ĐẦU TRẬN ĐẤU", new Vector2(0, 420f), new Vector2(800, 50), 26, Color.yellow, charObj.transform);

                var candidatesContainer = new GameObject("CandidatesContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                candidatesContainer.transform.SetParent(charObj.transform, false);
                var cRt = candidatesContainer.GetComponent<RectTransform>();
                cRt.anchoredPosition = new Vector2(0, 30f);
                cRt.sizeDelta = new Vector2(700, 460);
                var hlg = candidatesContainer.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 40f;
                characterSelectionView.candidatesContainer = candidatesContainer.transform;

                characterSelectionView.confirmSelectionButton = CreateButton("ConfirmCharBtn", "XÁC NHẬN CHỌN TƯỚNG", new Vector2(0, -380f), new Color(0.2f, 0.7f, 0.25f), charObj.transform, new Vector2(300, 65));

                flowController.characterSelectionView = characterSelectionView;
            }

            // 5. GAME TABLE VIEW
            if (gameTableView == null)
            {
                var tableObj = CreateFullScreenPanel("GameTableView", canvas.transform);
                gameTableView = tableObj.AddComponent<GameTableView>();
                gameTableView.tableBackgroundImage = tableObj.GetComponent<Image>();

                // Opponent Seats Container (full canvas, opponents placed by arc positions)
                var opponentsContainer = new GameObject("OpponentSeatsContainer", typeof(RectTransform));
                opponentsContainer.transform.SetParent(tableObj.transform, false);
                var ocRt = opponentsContainer.GetComponent<RectTransform>();
                ocRt.anchorMin = Vector2.zero;
                ocRt.anchorMax = Vector2.one;
                ocRt.sizeDelta = Vector2.zero;
                gameTableView.opponentSeatsContainer = ocRt;

                // Center Zone
                var centerZone = new GameObject("CenterZone", typeof(RectTransform));
                centerZone.transform.SetParent(tableObj.transform, false);
                var czRt = centerZone.GetComponent<RectTransform>();
                czRt.anchoredPosition = new Vector2(0, 60f);
                czRt.sizeDelta = new Vector2(500, 200);

                var drawObj = new GameObject("DrawPile", typeof(RectTransform), typeof(Image));
                drawObj.transform.SetParent(centerZone.transform, false);
                var drawRt = drawObj.GetComponent<RectTransform>();
                drawRt.anchoredPosition = new Vector2(-150f, 0);
                drawRt.sizeDelta = new Vector2(85, 125);
                var drawImg = drawObj.GetComponent<Image>();
                drawImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                gameTableView.drawPileImage = drawImg;

                var drawTxtObj = CreateText("Count", "65", new Vector2(0, -75f), new Vector2(80, 24), 15, Color.white, drawObj.transform);
                gameTableView.drawPileCountText = drawTxtObj.GetComponent<Text>();

                var discObj = new GameObject("DiscardPile", typeof(RectTransform), typeof(Image));
                discObj.transform.SetParent(centerZone.transform, false);
                var discRt = discObj.GetComponent<RectTransform>();
                discRt.anchoredPosition = new Vector2(150f, 0);
                discRt.sizeDelta = new Vector2(85, 125);
                gameTableView.discardPileImage = discObj.GetComponent<Image>();

                var discTxtObj = CreateText("Count", "0", new Vector2(0, -75f), new Vector2(80, 24), 15, Color.white, discObj.transform);
                gameTableView.discardPileCountText = discTxtObj.GetComponent<Text>();

                // Turn phase status (top center)
                var phaseObj = CreateText("TurnPhase", "⏳ LƯỢT ĐỐI THỦ", new Vector2(0, 200f), new Vector2(500, 36), 18, Color.white, tableObj.transform);
                gameTableView.turnPhaseStatusText = phaseObj.GetComponent<Text>();

                // Combat log (bottom center, above hand)
                var logObj = CreateText("LogText", "Chào mừng đến bàn đấu Bang!", new Vector2(0, -225f), new Vector2(700, 30), 14, new Color(0.95f, 0.85f, 0.6f), tableObj.transform);
                gameTableView.combatLogText = logObj.GetComponent<Text>();

                // Local Player Dashboard (bottom-left)
                var localDashObj = new GameObject("LocalPlayerDash", typeof(RectTransform));
                localDashObj.transform.SetParent(tableObj.transform, false);
                var ldRt = localDashObj.GetComponent<RectTransform>();
                ldRt.anchoredPosition = new Vector2(-750f, -380f);
                ldRt.sizeDelta = new Vector2(320, 180);

                // Local avatar
                var localAvatarObj = new GameObject("LocalAvatar", typeof(RectTransform), typeof(Image));
                localAvatarObj.transform.SetParent(localDashObj.transform, false);
                var laRt = localAvatarObj.GetComponent<RectTransform>();
                laRt.anchoredPosition = new Vector2(-100f, 20f);
                laRt.sizeDelta = new Vector2(90, 90);
                var laImg = localAvatarObj.GetComponent<Image>();
                laImg.preserveAspect = true;
                gameTableView.localAvatarImage = laImg;

                var localNameObj = CreateText("LocalName", "Người chơi", new Vector2(20f, 60f), new Vector2(200, 28), 14, new Color(1f, 0.9f, 0.4f), localDashObj.transform);
                gameTableView.localNameText = localNameObj.GetComponent<Text>();

                var localRoleObj = CreateText("LocalRole", "⭐ CẢNH SÁT TRƯỞNG", new Vector2(20f, 30f), new Vector2(220, 24), 12, Color.white, localDashObj.transform);
                gameTableView.localRoleText = localRoleObj.GetComponent<Text>();

                // Bullet/health tokens row
                var bulletContainer = new GameObject("BulletContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                bulletContainer.transform.SetParent(localDashObj.transform, false);
                var bcRt = bulletContainer.GetComponent<RectTransform>();
                bcRt.anchoredPosition = new Vector2(20f, -10f);
                bcRt.sizeDelta = new Vector2(220, 40);
                var bhlg = bulletContainer.GetComponent<HorizontalLayoutGroup>();
                bhlg.childAlignment = TextAnchor.MiddleLeft;
                bhlg.spacing = 4f;
                gameTableView.localBulletHealthContainer = bulletContainer.transform;

                // Equipment tray
                var equipTray = new GameObject("EquipmentTray", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                equipTray.transform.SetParent(localDashObj.transform, false);
                var etRt = equipTray.GetComponent<RectTransform>();
                etRt.anchoredPosition = new Vector2(20f, -55f);
                etRt.sizeDelta = new Vector2(240, 65);
                var ehlg = equipTray.GetComponent<HorizontalLayoutGroup>();
                ehlg.childAlignment = TextAnchor.MiddleLeft;
                ehlg.spacing = 6f;
                gameTableView.localEquipmentTray = equipTray.transform;

                // Hand layout (bottom center)
                var handObj = new GameObject("HandLayout", typeof(RectTransform), typeof(HandCardFanLayout));
                handObj.transform.SetParent(tableObj.transform, false);
                var handRt = handObj.GetComponent<RectTransform>();
                handRt.anchoredPosition = new Vector2(0, -400f);
                handRt.sizeDelta = new Vector2(900, 130);
                gameTableView.handCardLayout = handObj.GetComponent<HandCardFanLayout>();

                // Target Banner (top center)
                var tBannerObj = new GameObject("TargetBanner", typeof(RectTransform), typeof(Image));
                tBannerObj.transform.SetParent(tableObj.transform, false);
                var tbRt = tBannerObj.GetComponent<RectTransform>();
                tbRt.anchoredPosition = new Vector2(0, 420f);
                tbRt.sizeDelta = new Vector2(700, 50);
                tBannerObj.GetComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 0.9f);
                var tbTxtObj = CreateText("Text", "🎯 HÃY CHỌN MỤC TIÊU TRÊN BÀN ĐẤU", Vector2.zero, new Vector2(680, 45), 18, Color.yellow, tBannerObj.transform);
                gameTableView.targetBannerObj = tBannerObj;
                gameTableView.targetBannerText = tbTxtObj.GetComponent<Text>();

                // Card Preview Tooltip
                var ttObj = new GameObject("CardPreviewTooltip", typeof(RectTransform), typeof(Image));
                ttObj.transform.SetParent(tableObj.transform, false);
                var ttRt = ttObj.GetComponent<RectTransform>();
                ttRt.anchoredPosition = new Vector2(0, -260f);
                ttRt.sizeDelta = new Vector2(760, 46);
                ttObj.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.9f);
                var ttTxtObj = CreateText("Text", "Xem chi tiết bài", Vector2.zero, new Vector2(740, 40), 14, new Color(1f, 0.9f, 0.6f), ttObj.transform);
                gameTableView.cardPreviewTooltipObj = ttObj;
                gameTableView.cardPreviewTooltipText = ttTxtObj.GetComponent<Text>();

                // Action Buttons (right side)
                gameTableView.drawCardButton = CreateButton("DrawCardBtn", "🃏 RÚT BÀI", new Vector2(720f, 220f), new Color(0.2f, 0.55f, 0.85f), tableObj.transform, new Vector2(180, 60));
                gameTableView.playCardButton = CreateButton("PlayCardBtn", "💥 ĐÁNH BÀI", new Vector2(720f, 150f), new Color(0.85f, 0.45f, 0.15f), tableObj.transform, new Vector2(180, 60));
                gameTableView.playCardButtonText = gameTableView.playCardButton.GetComponentInChildren<Text>();
                gameTableView.cancelTargetButton = CreateButton("CancelTargetBtn", "❌ HỦY CHỌN", new Vector2(720f, 80f), new Color(0.5f, 0.2f, 0.2f), tableObj.transform, new Vector2(180, 50));
                gameTableView.endTurnButton = CreateButton("EndTurnBtn", "⏭ HẾT LƯỢT", new Vector2(720f, 10f), new Color(0.18f, 0.12f, 0.08f, 0.95f), tableObj.transform, new Vector2(180, 60));

                flowController.gameTableView = gameTableView;
            }

            // 6. RESULT VIEW
            if (resultView == null)
            {
                var resultObj = CreateFullScreenPanel("ResultView", canvas.transform);
                resultView = resultObj.AddComponent<ResultView>();

                var winTitle = CreateText("WinnerTitle", "🏆 PHE CẢNH SÁT TRƯỞNG THẮNG!", new Vector2(0, 380f), new Vector2(800, 60), 28, Color.yellow, resultObj.transform);
                resultView.winnerTitleText = winTitle.GetComponent<Text>();

                var resultsContainer = new GameObject("ResultsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                resultsContainer.transform.SetParent(resultObj.transform, false);
                var rRt = resultsContainer.GetComponent<RectTransform>();
                rRt.anchoredPosition = new Vector2(0, 30f);
                rRt.sizeDelta = new Vector2(1300, 280);
                var hlg = resultsContainer.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 15f;
                resultView.playerResultsContainer = resultsContainer.transform;

                resultView.rematchButton = CreateButton("RematchBtn", "🔄 CHƠI LẠI (REMATCH)", new Vector2(160f, -380f), new Color(0.2f, 0.7f, 0.25f), resultObj.transform, new Vector2(260, 65));
                resultView.returnToLobbyButton = CreateButton("LobbyBtn", "🚪 VỀ SẢNH CHÍNH", new Vector2(-160f, -380f), new Color(0.45f, 0.25f, 0.15f), resultObj.transform, new Vector2(220, 65));

                flowController.resultView = resultView;
            }

            // Interaction Controller
            if (interactionController == null)
            {
                var interactObj = new GameObject("InteractionController", typeof(RectTransform), typeof(InteractionController));
                interactObj.transform.SetParent(canvas.transform, false);
                interactionController = interactObj.GetComponent<InteractionController>();

                var modalObj = CreateFullScreenPanel("ModalRoot", interactObj.transform);
                modalObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);
                interactionController.modalRootPanel = modalObj;

                var cardBox = new GameObject("CardBox", typeof(RectTransform), typeof(Image));
                cardBox.transform.SetParent(modalObj.transform, false);
                var cbRt = cardBox.GetComponent<RectTransform>();
                cbRt.sizeDelta = new Vector2(550, 320);
                var cbImg = cardBox.GetComponent<Image>();
                cbImg.color = new Color(0.18f, 0.12f, 0.08f, 0.98f);

                var tObj = CreateText("Title", "BẠN ĐANG BỊ TẤN CÔNG!", new Vector2(0, 95f), new Vector2(500, 40), 20, Color.yellow, cardBox.transform);
                interactionController.titleText = tObj.GetComponent<Text>();

                var mObj = CreateText("Msg", "Cần nộp lá bài NÉ hoặc mất 1 Máu.", new Vector2(0, 30f), new Vector2(480, 60), 15, Color.white, cardBox.transform);
                interactionController.messageText = mObj.GetComponent<Text>();

                var timerObj = CreateText("Timer", "⌛ 15s", new Vector2(0, -30f), new Vector2(200, 30), 16, new Color(1f, 0.85f, 0.3f), cardBox.transform);
                interactionController.timerText = timerObj.GetComponent<Text>();

                var optContainer = new GameObject("Options", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                optContainer.transform.SetParent(cardBox.transform, false);
                var optRt = optContainer.GetComponent<RectTransform>();
                optRt.anchoredPosition = new Vector2(0, -95f);
                optRt.sizeDelta = new Vector2(480, 60);
                var ohlg = optContainer.GetComponent<HorizontalLayoutGroup>();
                ohlg.childAlignment = TextAnchor.MiddleCenter;
                ohlg.spacing = 15f;
                interactionController.optionsContainer = optContainer.transform;
            }

            // Ensure all button listeners are bound
            homeScreen?.BindListeners();
            lobbyView?.BindListeners();
            waitingRoomView?.BindListeners();
            characterSelectionView?.BindListeners();
            gameTableView?.BindListeners();
            resultView?.BindListeners();
        }

        private (GameObject, Button) CreatePopupBox(string name, string title, string content, Transform parent)
        {
            var popupRoot = CreateFullScreenPanel(name, parent);
            popupRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);

            var boxObj = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxObj.transform.SetParent(popupRoot.transform, false);
            var bRt = boxObj.GetComponent<RectTransform>();
            bRt.sizeDelta = new Vector2(700, 440);
            boxObj.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.98f);

            CreateText("Title", title, new Vector2(0, 160f), new Vector2(600, 40), 22, Color.yellow, boxObj.transform);
            CreateText("Content", content, new Vector2(0, 30f), new Vector2(620, 200), 15, Color.white, boxObj.transform);

            var closeBtn = CreateButton("CloseBtn", "ĐÃ HIỂU / ĐÓNG", new Vector2(0, -150f), new Color(0.85f, 0.45f, 0.15f), boxObj.transform, new Vector2(240, 55));
            return (popupRoot, closeBtn);
        }

        private (GameObject, Button, Button) CreatePopupBox(string name, string title, string content, Transform parent, string confirmText)
        {
            var popupRoot = CreateFullScreenPanel(name, parent);
            popupRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);

            var boxObj = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxObj.transform.SetParent(popupRoot.transform, false);
            var bRt = boxObj.GetComponent<RectTransform>();
            bRt.sizeDelta = new Vector2(700, 440);
            boxObj.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.98f);

            CreateText("Title", title, new Vector2(0, 160f), new Vector2(600, 40), 22, Color.yellow, boxObj.transform);
            CreateText("Content", content, new Vector2(0, 40f), new Vector2(620, 150), 16, Color.white, boxObj.transform);

            var confirmBtn = CreateButton("ConfirmBtn", confirmText, new Vector2(140f, -150f), new Color(0.2f, 0.7f, 0.25f), boxObj.transform, new Vector2(220, 55));
            var closeBtn = CreateButton("CloseBtn", "HỦY BỎ", new Vector2(-140f, -150f), new Color(0.45f, 0.25f, 0.15f), boxObj.transform, new Vector2(200, 55));

            return (popupRoot, confirmBtn, closeBtn);
        }

        private GameObject CreateFullScreenPanel(string name, Transform parent)
        {
            var panelObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(parent, false);
            var rt = panelObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var img = panelObj.GetComponent<Image>();
            img.color = new Color(0.12f, 0.08f, 0.06f);
            return panelObj;
        }

        private Button CreateButton(string name, string text, Vector2 anchoredPos, Color color, Transform parent, Vector2? size = null)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size ?? new Vector2(320, 65);

            var img = btnObj.GetComponent<Image>();
            img.color = color;

            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 17;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = text;

            return btnObj.GetComponent<Button>();
        }

        private GameObject CreateText(string name, string text, Vector2 anchoredPos, Vector2 size, int fontSize, Color color, Transform parent)
        {
            var txtObj = new GameObject(name, typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(parent, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchoredPosition = anchoredPos;
            txtRt.sizeDelta = size;
            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = text;
            return txtObj;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
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

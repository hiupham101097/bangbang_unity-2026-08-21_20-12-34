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
        public bool useLiveCloudflareServer = true;
        [Tooltip("REST Worker URL; không dùng URL này cho BangLiveGateway.")]
        public string cloudflareWorkerUrl = "https://blue-frog-fec8.hieupham101097.workers.dev";
        [Tooltip("Public Node.js WebSocket origin, ví dụ wss://bangbang-node-server.onrender.com. Để trống sẽ dùng localServerUrl.")]
        public string liveWebSocketUrl = "";
        public string localServerUrl = "ws://localhost:3000";

        [Header("Core Architecture Controllers")]
        public GameStateStore gameStateStore;
        public GameFlowController flowController;
        public InteractionController interactionController;
        public BangLiveGateway liveGateway;
        public BangCloudflareGateway cloudflareGateway;
        public BangMockGateway mockGateway;

        [Header("Views")]
        public HomeScreenUI homeScreen;
        public CardGalleryView cardGalleryView;
        public LobbyView lobbyView;
        public WaitingRoomView waitingRoomView;
        public RoleRevealView roleRevealView;
        public CharacterSelectionView characterSelectionView;
        public GameTableView gameTableView;
        public ResultView resultView;

        private GameObject _splashOverlay;
        private Text _splashStatus;
        private Button _splashRetryButton;
        private IGameGateway _activeGateway;
        private string _deviceId;

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
            HideViewsDuringBoot();
            CreateSplashOverlay();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM("western_theme");
            }

            // Initialize Gateways & State Store
            if (useLiveCloudflareServer)
            {
                _activeGateway = cloudflareGateway;
                Debug.Log("[GameBootstrap] Using CloudflareGateway -> " + cloudflareGateway.serverBaseUrl);
            }
            else
            {
                _activeGateway = mockGateway;
                Debug.Log("[GameBootstrap] Using MockGateway (offline/local mode)");
            }

            if (gameStateStore != null)
            {
                gameStateStore.BindGateway(_activeGateway);
            }

            // Re-bind FlowController after gateway is set up (timing fix)
            flowController?.BindToStore();

            // Start User Session
            _deviceId = PlayerPrefs.GetString("bang_device_id", Guid.NewGuid().ToString("N").Substring(0, 16));
            PlayerPrefs.SetString("bang_device_id", _deviceId);
            await ConnectSessionAsync();
        }

        private void HideViewsDuringBoot()
        {
            // Dynamically-created views otherwise receive Start/OnEnable while the
            // splash screen is still obtaining the Cloudflare session token.
            if (homeScreen != null) homeScreen.gameObject.SetActive(false);
            if (cardGalleryView != null) cardGalleryView.gameObject.SetActive(false);
            if (lobbyView != null) lobbyView.gameObject.SetActive(false);
            if (waitingRoomView != null) waitingRoomView.gameObject.SetActive(false);
            if (roleRevealView != null) roleRevealView.gameObject.SetActive(false);
            if (characterSelectionView != null) characterSelectionView.gameObject.SetActive(false);
            if (gameTableView != null) gameTableView.gameObject.SetActive(false);
            if (resultView != null) resultView.gameObject.SetActive(false);
        }

        private async System.Threading.Tasks.Task ConnectSessionAsync()
        {
            if (_splashRetryButton != null) _splashRetryButton.gameObject.SetActive(false);
            if (_splashStatus != null) _splashStatus.text = "Đang kết nối và đồng bộ phiên…";
            _deviceId = PlayerPrefs.GetString("bang_device_id", _deviceId);
            bool ready = _activeGateway != null && await _activeGateway.InitializeSessionAsync(_deviceId, "Cao bồi viễn tây");
            if (ready)
            {
                if (_splashOverlay != null) _splashOverlay.SetActive(false);
                ShowHomeScreen();
            }
            else
            {
                if (_splashStatus != null) _splashStatus.text = "Không thể kết nối máy chủ. Kiểm tra mạng rồi thử lại.";
                if (_splashRetryButton != null) _splashRetryButton.gameObject.SetActive(true);
            }
        }

        private void CreateSplashOverlay()
        {
            if (_splashOverlay != null) return;
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            _splashOverlay = CreateFullScreenPanel("SplashOverlay", canvas.transform);
            _splashOverlay.GetComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 1f);
            CreateText("SplashTitle", "BANG ONLINE", new Vector2(0, 80), new Vector2(700, 90), 46, BangUITheme.Brass, _splashOverlay.transform);
            _splashStatus = CreateText("SplashStatus", "Đang khởi tạo…", new Vector2(0, -20), new Vector2(760, 45), 18, Color.white, _splashOverlay.transform).GetComponent<Text>();
            _splashRetryButton = CreateButton("SplashRetry", "THỬ LẠI", new Vector2(0, -110), BangUITheme.Brass, _splashOverlay.transform, new Vector2(220, 58));
            _splashRetryButton.onClick.AddListener(async () => await ConnectSessionAsync());
            _splashRetryButton.gameObject.SetActive(false);
            _splashOverlay.transform.SetAsLastSibling();
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
                scaler.referenceResolution = BangResponsiveLayout.ReferenceResolution;

                if (FindAnyObjectByType<Camera>() == null)
                {
                    var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    camObj.tag = "MainCamera";
                }

                EnsureEventSystem();
            }

            canvas.pixelPerfect = true;

            var responsiveLayout = canvas.GetComponent<BangResponsiveLayout>();
            if (responsiveLayout == null) responsiveLayout = canvas.gameObject.AddComponent<BangResponsiveLayout>();

            if (FindAnyObjectByType<AudioManager>() == null) new GameObject("AudioManager", typeof(AudioManager));
            if (FindAnyObjectByType<VoiceChatManager>() == null) new GameObject("VoiceChatManager", typeof(VoiceChatManager));
            if (FindAnyObjectByType<FXManager>() == null) new GameObject("FXManager", typeof(FXManager));

            // Gateways
            if (mockGateway == null)
            {
                mockGateway = gameObject.AddComponent<BangMockGateway>();
            }

            if (liveGateway == null)
            {
                var gateway = new GameObject("LiveGateway").AddComponent<BangLiveGateway>();
                liveGateway = gateway;
            }
            liveGateway.serverWsUrl = string.IsNullOrWhiteSpace(liveWebSocketUrl) ? localServerUrl : liveWebSocketUrl.Trim();
            if (cloudflareGateway == null)
            {
                var gateway = new GameObject("CloudflareGateway").AddComponent<BangCloudflareGateway>();
                cloudflareGateway = gateway;
            }
            cloudflareGateway.serverBaseUrl = cloudflareWorkerUrl.TrimEnd('/');
            if (BangNetworkClient.Instance != null) BangNetworkClient.Instance.serverBaseUrl = cloudflareWorkerUrl;

            // State Store & Flow
            if (gameStateStore == null) gameStateStore = gameObject.AddComponent<GameStateStore>();
            if (flowController == null) flowController = gameObject.AddComponent<GameFlowController>();

            // 0. HOME SCREEN (Trang Chủ)
            if (homeScreen == null)
            {
                var homeObj = CreateFullScreenPanel("HomeScreen", canvas.transform);
                homeScreen = homeObj.AddComponent<HomeScreenUI>();
                homeScreen.backgroundImage = homeObj.GetComponent<Image>();

                var rightScrim = new GameObject("RightScrim", typeof(RectTransform), typeof(Image));
                rightScrim.transform.SetParent(homeObj.transform, false);
                var scrimRt = rightScrim.GetComponent<RectTransform>();
                scrimRt.anchorMin = new Vector2(1, 0);
                scrimRt.anchorMax = new Vector2(1, 1);
                scrimRt.pivot = new Vector2(1, 0.5f);
                scrimRt.sizeDelta = new Vector2(470, 0);
                rightScrim.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.04f, 0.62f);
                rightScrim.GetComponent<Image>().raycastTarget = false;

                // Logo Banner
                var logoObj = new GameObject("LogoBanner", typeof(RectTransform), typeof(Image));
                logoObj.transform.SetParent(homeObj.transform, false);
                var logoRt = logoObj.GetComponent<RectTransform>();
                logoRt.anchoredPosition = new Vector2(-285f, 70f);
                logoRt.sizeDelta = new Vector2(650, 370);
                homeScreen.logoImage = logoObj.GetComponent<Image>();
                homeScreen.logoImage.raycastTarget = false;

                var profile = CreateSurface("ProfileSurface", new Vector2(-480f, 295f), new Vector2(340, 62), homeObj.transform);
                homeScreen.profileButton = profile.AddComponent<Button>();
                var avatarObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
                avatarObj.transform.SetParent(profile.transform, false);
                avatarObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-135, 0);
                avatarObj.GetComponent<RectTransform>().sizeDelta = new Vector2(46, 46);
                avatarObj.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
                avatarObj.GetComponent<Image>().type = Image.Type.Sliced;
                homeScreen.avatarImage = avatarObj.GetComponent<Image>();
                homeScreen.playerNameText = CreateText("PlayerName", "Cao bồi", new Vector2(22, 11), new Vector2(240, 24), 17, BangUITheme.Ivory, profile.transform).GetComponent<Text>();
                homeScreen.playerNameText.alignment = TextAnchor.MiddleLeft;
                homeScreen.playerBountyText = CreateText("PlayerBounty", "Tân binh • $0", new Vector2(22, -13), new Vector2(240, 20), 13, BangUITheme.Muted, profile.transform).GetComponent<Text>();
                homeScreen.playerBountyText.alignment = TextAnchor.MiddleLeft;

                CreateText("HeroCaption", "MIỀN TÂY KHÔNG CÓ CHỖ CHO KẺ DO DỰ", new Vector2(-285f, -170f), new Vector2(620, 30), 15, BangUITheme.Ivory, homeObj.transform);
                CreateText("HeroSubcaption", "Lập bàn, chọn vai trò và sống sót đến phát súng cuối cùng.", new Vector2(-285f, -200f), new Vector2(620, 26), 14, BangUITheme.Muted, homeObj.transform);

                var homeMenu = CreateSurface("MainMenuSurface", new Vector2(430f, -12f), new Vector2(400, 520), homeObj.transform);
                var menuTitle = CreateText("MenuTitle", "SẴN SÀNG VÀO TRẬN?", new Vector2(0, 205f), new Vector2(340, 38), 23, BangUITheme.Ivory, homeMenu.transform).GetComponent<Text>();
                menuTitle.alignment = TextAnchor.MiddleLeft;
                var menuEyebrow = CreateText("MenuEyebrow", "MULTIPLAYER TRỰC TUYẾN  •  4–8 NGƯỜI", new Vector2(0, 168f), new Vector2(340, 24), 13, BangUITheme.Muted, homeMenu.transform).GetComponent<Text>();
                menuEyebrow.alignment = TextAnchor.MiddleLeft;
                homeScreen.startButton = CreateButton("StartBtn", "CHƠI ONLINE", new Vector2(0, 110f), BangUITheme.Brass, homeMenu.transform, new Vector2(340, 58));
                homeScreen.galleryButton = CreateButton("GalleryBtn", "THƯ VIỆN THẺ", new Vector2(0, 43f), BangUITheme.SurfaceRaised, homeMenu.transform, new Vector2(340, 52));
                homeScreen.questsButton = CreateButton("QuestsBtn", "NHIỆM VỤ", new Vector2(0, -18f), BangUITheme.SurfaceRaised, homeMenu.transform, new Vector2(340, 52));
                homeScreen.guideButton = CreateButton("GuideBtn", "HƯỚNG DẪN CHƠI", new Vector2(0, -79f), BangUITheme.SurfaceRaised, homeMenu.transform, new Vector2(340, 52));

                // Audio Toggle Top Right
                homeScreen.audioToggleButton = CreateButton("AudioBtn", "ÂM THANH: BẬT", new Vector2(0, -148f), BangUITheme.Ink, homeMenu.transform, new Vector2(340, 48));
                homeScreen.audioToggleText = homeScreen.audioToggleButton.GetComponentInChildren<Text>();
                CreateText("BuildInfo", "ONLINE  •  BUILD " + Application.version, new Vector2(0, -220f), new Vector2(340, 22), 12, BangUITheme.Muted, homeMenu.transform);

                // Quests Popup
                var qPopup = CreatePopupBox("QuestsPopup", "NHIỆM VỤ HẰNG NGÀY", "1. Bắn trúng 3 phát BANG! (Thưởng: 500 Vàng)\n2. Uống 2 chai BIA hồi máu (Thưởng: 300 Vàng)\n3. Thắng 1 trận với vai trò Cảnh Sát Trưởng (Thưởng: 1,000 Vàng)", homeObj.transform);
                homeScreen.questsPopup = qPopup.Item1;
                homeScreen.closeQuestsButton = qPopup.Item2;

                // Guide Popup
                var gPopup = CreatePopupBox("GuidePopup", "HƯỚNG DẪN LUẬT CHƠI", "• CẢNH SÁT TRƯỞNG: Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội.\n• PHÓ CẢNH SÁT: Bảo vệ Cảnh Sát Trưởng bằng mọi giá.\n• CƯỚP (OUTLAW): Tiêu diệt Cảnh Sát Trưởng.\n• KẺ PHẢN BỘI: Người sống sót cuối cùng và hạ Cảnh Sát Trưởng sau cùng.\n\n• CỰ LY BẮN: Khoảng cách ngắn nhất quanh bàn. Vũ khí tăng tầm bắn.", homeObj.transform);
                homeScreen.guidePopup = gPopup.Item1;
                homeScreen.closeGuideButton = gPopup.Item2;

                var pPopup = CreatePopupBox("ProfilePopup", "TÀI KHOẢN & AVATAR", "Chọn hình đại diện. Avatar sẽ hiển thị trong phòng và trên bàn đấu.", homeObj.transform);
                homeScreen.profilePopup = pPopup.Item1;
                homeScreen.closeProfileButton = pPopup.Item2;
                var profileBox = pPopup.Item1.transform.Find("Box");
                var avatarGrid = new GameObject("AvatarGrid", typeof(RectTransform), typeof(GridLayoutGroup));
                avatarGrid.transform.SetParent(profileBox, false);
                var avatarGridRt = avatarGrid.GetComponent<RectTransform>();
                avatarGridRt.anchoredPosition = new Vector2(0, 5f);
                avatarGridRt.sizeDelta = new Vector2(600, 230);
                var avatarLayout = avatarGrid.GetComponent<GridLayoutGroup>();
                avatarLayout.cellSize = new Vector2(108, 108);
                avatarLayout.spacing = new Vector2(12, 12);
                avatarLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                avatarLayout.constraintCount = 5;
                avatarLayout.childAlignment = TextAnchor.MiddleCenter;
                homeScreen.avatarOptionsContainer = avatarGrid.transform;
            }

            // CARD GALLERY VIEW
            if (cardGalleryView == null)
            {
                var galleryObj = CreateFullScreenPanel("CardGalleryView", canvas.transform);
                cardGalleryView = galleryObj.AddComponent<CardGalleryView>();

                // Header
                var headObj = CreateText("Header", "🃏 BỘ SƯU TẬP THẺ BÀI", new Vector2(0, 300f), new Vector2(560, 46), 26, new Color(1f, 0.85f, 0.3f), galleryObj.transform);
                cardGalleryView.titleText = headObj.GetComponent<Text>();
                cardGalleryView.backButton = CreateButton("BackBtn", "⬅ QUAY LẠI", new Vector2(-560f, 300f), new Color(0.45f, 0.25f, 0.15f), galleryObj.transform, new Vector2(160, 46));

                // Scroll View Container
                var scrollObj = new GameObject("CardScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
                scrollObj.transform.SetParent(galleryObj.transform, false);
                var scrollRt = scrollObj.GetComponent<RectTransform>();
                scrollRt.anchoredPosition = new Vector2(0, -30f);
                scrollRt.sizeDelta = new Vector2(1120, 550);
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
                lobbyView.backToHomeButton = CreateButton("BackHomeBtn", "QUAY LẠI", new Vector2(-570f, 250f), BangUITheme.SurfaceRaised, lobbyObj.transform, new Vector2(150, 44));

                // Header Title
                var headObj = CreateText("Header", "PHÒNG ĐANG HOẠT ĐỘNG", new Vector2(-290f, 250f), new Vector2(360, 44), 24, BangUITheme.Ivory, lobbyObj.transform);
                lobbyView.titleText = headObj.GetComponent<Text>();
                lobbyView.titleText.alignment = TextAnchor.MiddleLeft;
                lobbyView.connectionStatusText = CreateText("ConnectionStatus", "ĐANG ĐỒNG BỘ MÁY CHỦ", new Vector2(500f, 305f), new Vector2(260, 26), 13, BangUITheme.Muted, lobbyObj.transform).GetComponent<Text>();

                // Actions Bottom
                lobbyView.openCreateRoomPopupButton = CreateButton("CreateRoomBtn", "TẠO PHÒNG MỚI", new Vector2(510f, 250f), BangUITheme.Brass, lobbyObj.transform, new Vector2(240, 46));

                var finderSurface = CreateSurface("RoomFinderSurface", new Vector2(-520f, -40f), new Vector2(260, 500), lobbyObj.transform);
                var finderTitle = CreateText("FinderTitle", "VÀO NHANH", new Vector2(0, 205f), new Vector2(210, 32), 20, BangUITheme.Ivory, finderSurface.transform).GetComponent<Text>();
                finderTitle.alignment = TextAnchor.MiddleLeft;
                CreateText("FinderHint", "Nhập mã phòng được bạn bè chia sẻ", new Vector2(0, 165f), new Vector2(210, 44), 13, BangUITheme.Muted, finderSurface.transform).GetComponent<Text>().alignment = TextAnchor.UpperLeft;
                lobbyView.roomPinInput = CreateInputField("RoomPinInput", "MÃ PHÒNG", new Vector2(0, 108f), new Vector2(210, 50), finderSurface.transform);
                lobbyView.joinByPinButton = CreateButton("JoinPinBtn", "THAM GIA", new Vector2(0, 48f), BangUITheme.Brass, finderSurface.transform, new Vector2(210, 48));
                CreateText("FilterLabel", "BỘ LỌC", new Vector2(0, -18f), new Vector2(210, 26), 13, BangUITheme.Muted, finderSurface.transform).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
                CreateText("FilterAll", "TẤT CẢ PHÒNG", new Vector2(0, -55f), new Vector2(210, 34), 15, BangUITheme.Ivory, finderSurface.transform).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
                CreateText("FilterWaiting", "CÒN CHỖ", new Vector2(0, -92f), new Vector2(210, 34), 15, BangUITheme.Muted, finderSurface.transform).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
                CreateText("FilterPrivate", "PHÒNG RIÊNG", new Vector2(0, -129f), new Vector2(210, 34), 15, BangUITheme.Muted, finderSurface.transform).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

                // Room List Container
                var listSurface = CreateSurface("RoomListSurface", new Vector2(100f, -40f), new Vector2(930, 500), lobbyObj.transform);
                var scrollView = new GameObject("RoomScrollView", typeof(RectTransform), typeof(UnityEngine.UI.ScrollRect));
                scrollView.transform.SetParent(listSurface.transform, false);
                var scrollRt = scrollView.GetComponent<RectTransform>();
                scrollRt.anchorMin = Vector2.zero;
                scrollRt.anchorMax = Vector2.one;
                scrollRt.offsetMin = new Vector2(28, 28);
                scrollRt.offsetMax = new Vector2(-28, -28);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
                viewport.transform.SetParent(scrollView.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero;
                viewportRt.anchorMax = Vector2.one;
                viewportRt.sizeDelta = Vector2.zero;
                viewport.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

                var listContainer = new GameObject("RoomListContent", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
                listContainer.transform.SetParent(viewport.transform, false);
                var listRt = listContainer.GetComponent<RectTransform>();
                listRt.anchorMin = new Vector2(0, 1);
                listRt.anchorMax = new Vector2(1, 1);
                listRt.pivot = new Vector2(0.5f, 1);
                listRt.sizeDelta = Vector2.zero;
                var vlg = listContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 12f;
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.childControlHeight = false;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;
                var fitter = listContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var scroll = scrollView.GetComponent<UnityEngine.UI.ScrollRect>();
                scroll.viewport = viewportRt;
                scroll.content = listRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
                scroll.scrollSensitivity = 32f;
                lobbyView.roomListContentTransform = listContainer.transform;

                // Create Room Popup (with bot option)
                var crPopup = CreatePopupBox("CreateRoomPopup", "TẠO PHÒNG MỚI", "Thiết lập nhanh — có thể thay đổi bot trong phòng chờ", lobbyObj.transform, "TẠO PHÒNG");
                lobbyView.createRoomPopup = crPopup.Item1;
                lobbyView.confirmCreateRoomButton = crPopup.Item2;
                lobbyView.closeCreateRoomPopupButton = crPopup.Item3;
                var createBox = crPopup.Item1.transform.Find("Box");
                lobbyView.roomNameInput = CreateInputField("RoomNameInput", "Tên phòng", new Vector2(-180f, 35f), new Vector2(300, 56), createBox);
                lobbyView.maxPlayersDropdown = CreateDropdown("MaxPlayersDropdown", new[] { "4 người", "5 người", "6 người", "7 người", "8 người" }, 4, new Vector2(180f, 35f), new Vector2(300, 56), createBox);
                lobbyView.turnTimeDropdown = CreateDropdown("TurnTimeDropdown", new[] { "15 giây / lượt", "30 giây / lượt", "45 giây / lượt", "60 giây / lượt" }, 1, new Vector2(-180f, -45f), new Vector2(300, 56), createBox);
                lobbyView.createRoomStatusText = CreateText("CreateStatus", string.Empty, new Vector2(180f, -45f), new Vector2(300, 56), 14, BangUITheme.Muted, createBox).GetComponent<Text>();

                flowController.lobbyView = lobbyView;
            }

            // 2. WAITING ROOM VIEW (Phòng Chờ)
            if (waitingRoomView == null)
            {
                var waitingObj = CreateFullScreenPanel("WaitingRoomView", canvas.transform);
                waitingRoomView = waitingObj.AddComponent<WaitingRoomView>();
                waitingObj.GetComponent<Image>().sprite = CardCatalogDatabase.LoadSprite("UI/LandscapeV2/waiting_room_v2");
                waitingObj.GetComponent<Image>().color = Color.white;

                var codeObj = CreateText("RoomCodeText", "MÃ PHÒNG: SALOON", new Vector2(0, 290f), new Vector2(520, 46), 25, Color.yellow, waitingObj.transform);
                waitingRoomView.roomCodeText = codeObj.GetComponent<Text>();
                waitingRoomView.copyCodeButton = CreateButton("CopyCodeBtn", "SAO CHÉP MÃ", new Vector2(430f, 290f), BangUITheme.SurfaceRaised, waitingObj.transform, new Vector2(170, 46));
                waitingRoomView.playerCountText = CreateText("PlayerCount", "0 / 8 Người", new Vector2(-430f, 290f), new Vector2(210, 38), 18, Color.white, waitingObj.transform).GetComponent<Text>();

                var reasonObj = CreateText("ReasonText", "Đang chờ người chơi sẵn sàng...", new Vector2(0, 245f), new Vector2(720, 28), 14, Color.white, waitingObj.transform);
                waitingRoomView.startDisabledReasonText = reasonObj.GetComponent<Text>();

                var seatsContainer = new GameObject("SeatsContainer", typeof(RectTransform));
                seatsContainer.transform.SetParent(waitingObj.transform, false);
                var sRt = seatsContainer.GetComponent<RectTransform>();
                sRt.anchoredPosition = new Vector2(0, 30f);
                sRt.sizeDelta = new Vector2(1180, 260);
                waitingRoomView.seatsContainer = seatsContainer.transform;

                // Controls: Start, Add Bot, Ready, Leave
                waitingRoomView.startGameButton = CreateButton("StartBtn", "BẮT ĐẦU TRẬN", new Vector2(430f, -300f), BangUITheme.Brass, waitingObj.transform, new Vector2(240, 58));
                waitingRoomView.addBotButton = CreateButton("AddBotBtn", "THÊM BOT", new Vector2(190f, -300f), BangUITheme.SurfaceRaised, waitingObj.transform, new Vector2(170, 58));
                waitingRoomView.removeBotButton = CreateButton("RemoveBotBtn", "BỚT BOT", new Vector2(0f, -380f), BangUITheme.Danger, waitingObj.transform, new Vector2(160, 65));
                waitingRoomView.removeBotButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -300f);
                waitingRoomView.readyToggleButton = CreateButton("ReadyBtn", "SẴN SÀNG", new Vector2(-190f, -300f), BangUITheme.Success, waitingObj.transform, new Vector2(170, 58));
                waitingRoomView.leaveRoomButton = CreateButton("LeaveBtn", "RỜI PHÒNG", new Vector2(-430f, -300f), BangUITheme.SurfaceRaised, waitingObj.transform, new Vector2(170, 58));

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
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                roleRevealView.roleCardsContainer = roleCardsContainer.transform;

                var sheriffReveal = CreateSurface("SheriffReveal", new Vector2(0, 55f), new Vector2(520, 480), roleObj.transform);
                roleRevealView.sheriffRevealRoot = sheriffReveal;
                roleRevealView.sheriffRevealCanvasGroup = sheriffReveal.AddComponent<CanvasGroup>();

                var halo = new GameObject("SheriffHalo", typeof(RectTransform), typeof(Image), typeof(Outline));
                halo.transform.SetParent(sheriffReveal.transform, false);
                halo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 55f);
                halo.GetComponent<RectTransform>().sizeDelta = new Vector2(244, 244);
                halo.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
                halo.GetComponent<Image>().type = Image.Type.Sliced;
                halo.GetComponent<Image>().color = new Color(BangUITheme.Brass.r, BangUITheme.Brass.g, BangUITheme.Brass.b, 0.28f);
                halo.GetComponent<Outline>().effectColor = BangUITheme.Brass;
                halo.GetComponent<Outline>().effectDistance = new Vector2(4, -4);

                var sheriffAvatar = new GameObject("SheriffAvatar", typeof(RectTransform), typeof(Image));
                sheriffAvatar.transform.SetParent(sheriffReveal.transform, false);
                sheriffAvatar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 55f);
                sheriffAvatar.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 210);
                sheriffAvatar.GetComponent<Image>().preserveAspect = true;
                roleRevealView.sheriffAvatarImage = sheriffAvatar.GetComponent<Image>();

                var sheriffBadge = new GameObject("SheriffBadge", typeof(RectTransform), typeof(Image));
                sheriffBadge.transform.SetParent(sheriffReveal.transform, false);
                sheriffBadge.GetComponent<RectTransform>().anchoredPosition = new Vector2(105f, -28f);
                sheriffBadge.GetComponent<RectTransform>().sizeDelta = new Vector2(82, 112);
                sheriffBadge.GetComponent<Image>().sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                sheriffBadge.GetComponent<Image>().preserveAspect = true;

                roleRevealView.sheriffNameText = CreateText("SheriffName", "SHERIFF", new Vector2(0, -92f), new Vector2(440, 46), 28, BangUITheme.Brass, sheriffReveal.transform).GetComponent<Text>();
                roleRevealView.sheriffNameText.fontStyle = FontStyle.Bold;
                roleRevealView.sheriffFirstTurnText = CreateText("SheriffRule", "CẢNH SÁT TRƯỞNG  •  +1 MÁU  •  ĐI LƯỢT ĐẦU", new Vector2(0, -142f), new Vector2(460, 34), 15, BangUITheme.Ivory, sheriffReveal.transform).GetComponent<Text>();
                sheriffReveal.SetActive(false);

                var goalObj = CreateText("RoleGoal", "Mục tiêu chiến thắng...", new Vector2(0, -255f), new Vector2(1100, 58), 16, Color.white, roleObj.transform);
                roleRevealView.roleGoalText = goalObj.GetComponent<Text>();

                var timerObj = CreateText("TimerCountdown", "", new Vector2(0, -285f), new Vector2(500, 30), 14, new Color(0.8f, 0.8f, 0.8f), roleObj.transform);
                roleRevealView.timerCountdownText = timerObj.GetComponent<Text>();

                roleRevealView.continueButton = CreateButton("ContinueBtn", "TIẾP TỤC", new Vector2(0, -310f), BangUITheme.Success, roleObj.transform, new Vector2(250, 56));

                flowController.roleRevealView = roleRevealView;
            }

            // 4. CHARACTER SELECTION VIEW
            if (characterSelectionView == null)
            {
                var charObj = CreateFullScreenPanel("CharacterSelectionView", canvas.transform);
                characterSelectionView = charObj.AddComponent<CharacterSelectionView>();

                CreateText("Header", "CHỌN TƯỚNG BẮT ĐẦU TRẬN ĐẤU", new Vector2(0, 300f), new Vector2(760, 46), 25, Color.yellow, charObj.transform);

                var candidatesContainer = new GameObject("CandidatesContainer", typeof(RectTransform), typeof(GridLayoutGroup));
                candidatesContainer.transform.SetParent(charObj.transform, false);
                var cRt = candidatesContainer.GetComponent<RectTransform>();
                cRt.anchoredPosition = new Vector2(0, 15f);
                cRt.sizeDelta = new Vector2(1180, 500);
                var grid = candidatesContainer.GetComponent<GridLayoutGroup>();
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.spacing = new Vector2(14f, 14f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 8;
                characterSelectionView.candidatesContainer = candidatesContainer.transform;

                var timerObj = CreateText("CharacterTimer", "Còn 30 giây", new Vector2(0, 252f), new Vector2(300, 34), 18, Color.white, charObj.transform);
                characterSelectionView.timerText = timerObj.GetComponent<Text>();

                characterSelectionView.confirmSelectionButton = CreateButton("ConfirmCharBtn", "XÁC NHẬN CHỌN TƯỚNG", new Vector2(0, -305f), new Color(0.2f, 0.7f, 0.25f), charObj.transform, new Vector2(300, 58));

                flowController.characterSelectionView = characterSelectionView;
            }

            // 5. GAME TABLE VIEW
            if (gameTableView == null)
            {
                var tableObj = CreateFullScreenPanel("GameTableView", canvas.transform);
                gameTableView = tableObj.AddComponent<GameTableView>();
                gameTableView.tableBackgroundImage = tableObj.GetComponent<Image>();
                gameTableView.tableBackgroundImage.raycastTarget = false;

                // Keep the painted table and all gameplay coordinates in one 16:9 space.
                var tableAspect = tableObj.AddComponent<AspectRatioFitter>();
                tableAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                tableAspect.aspectRatio = 16f / 9f;

                // Match UI is authored directly in the compact 1280x720 space.
                var tableContent = new GameObject("TableContent", typeof(RectTransform));
                tableContent.transform.SetParent(tableObj.transform, false);
                var tableContentRt = tableContent.GetComponent<RectTransform>();
                tableContentRt.anchorMin = new Vector2(0.5f, 0.5f);
                tableContentRt.anchorMax = new Vector2(0.5f, 0.5f);
                tableContentRt.pivot = new Vector2(0.5f, 0.5f);
                tableContentRt.sizeDelta = new Vector2(1280f, 720f);
                tableContentRt.localScale = Vector3.one;

                // Opponent Seats Container (full canvas, opponents placed by arc positions)
                var opponentsContainer = new GameObject("OpponentSeatsContainer", typeof(RectTransform));
                opponentsContainer.transform.SetParent(tableContent.transform, false);
                var ocRt = opponentsContainer.GetComponent<RectTransform>();
                ocRt.anchorMin = Vector2.zero;
                ocRt.anchorMax = Vector2.one;
                ocRt.sizeDelta = Vector2.zero;
                gameTableView.opponentSeatsContainer = ocRt;

                // Center Zone
                var centerZone = new GameObject("CenterZone", typeof(RectTransform), typeof(Image), typeof(Outline));
                centerZone.transform.SetParent(tableContent.transform, false);
                var czRt = centerZone.GetComponent<RectTransform>();
                czRt.anchoredPosition = new Vector2(0, 10f);
                czRt.sizeDelta = new Vector2(440, 180);
                centerZone.GetComponent<Image>().color = Color.clear;
                centerZone.GetComponent<Image>().raycastTarget = false;
                centerZone.GetComponent<Outline>().enabled = false;

                var drawObj = new GameObject("DrawPile", typeof(RectTransform), typeof(Image));
                drawObj.transform.SetParent(centerZone.transform, false);
                var drawRt = drawObj.GetComponent<RectTransform>();
                drawRt.anchoredPosition = new Vector2(-160f, 0);
                drawRt.sizeDelta = new Vector2(96, 144);
                var drawImg = drawObj.GetComponent<Image>();
                drawImg.sprite = CardCatalogDatabase.LoadSprite("card_back");
                drawImg.color = Color.white;
                drawImg.preserveAspect = true;
                gameTableView.drawPileImage = drawImg;

                var drawTxtObj = CreateText("Count", "65", new Vector2(0, -58f), new Vector2(72, 22), 14, Color.white, drawObj.transform);
                gameTableView.drawPileCountText = drawTxtObj.GetComponent<Text>();

                var discObj = new GameObject("DiscardPile", typeof(RectTransform), typeof(Image));
                discObj.transform.SetParent(centerZone.transform, false);
                var discRt = discObj.GetComponent<RectTransform>();
                discRt.anchoredPosition = new Vector2(160f, 0);
                discRt.sizeDelta = new Vector2(96, 144);
                gameTableView.discardPileImage = discObj.GetComponent<Image>();
                gameTableView.discardPileImage.preserveAspect = true;

                var discTxtObj = CreateText("Count", "0", new Vector2(0, -58f), new Vector2(72, 22), 14, Color.white, discObj.transform);
                gameTableView.discardPileCountText = discTxtObj.GetComponent<Text>();

                // Turn phase status (top-left)
                var phaseObj = CreateText("TurnPhase", "ĐANG CHỜ LƯỢT", new Vector2(0, 324f), new Vector2(270, 42), 18, BangUITheme.Ivory, tableContent.transform);
                gameTableView.turnPhaseStatusText = phaseObj.GetComponent<Text>();
                gameTableView.turnPhaseStatusText.alignment = TextAnchor.MiddleCenter;

                // Combat log (center bottom, above hand)
                var logObj = CreateText("LogText", "Bàn đấu đang sẵn sàng…", new Vector2(0, -92f), new Vector2(520, 30), 14, BangUITheme.Muted, tableContent.transform);
                gameTableView.combatLogText = logObj.GetComponent<Text>();

                // Local Player Dashboard (bottom-left, anchored within 1280x720)
                var localDashObj = new GameObject("LocalPlayerDash", typeof(RectTransform), typeof(Image), typeof(Outline));
                localDashObj.transform.SetParent(tableContent.transform, false);
                var ldRt = localDashObj.GetComponent<RectTransform>();
                ldRt.anchoredPosition = new Vector2(-235f, -258f);
                ldRt.sizeDelta = new Vector2(150, 122);
                localDashObj.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
                localDashObj.GetComponent<Image>().type = Image.Type.Sliced;
                localDashObj.GetComponent<Image>().color = new Color(0.055f, 0.035f, 0.022f, 0.92f);
                localDashObj.GetComponent<Outline>().effectColor = BangUITheme.Brass;
                localDashObj.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

                // Local avatar
                var localAvatarObj = new GameObject("LocalAvatar", typeof(RectTransform), typeof(Image));
                localAvatarObj.transform.SetParent(localDashObj.transform, false);
                var laRt = localAvatarObj.GetComponent<RectTransform>();
                laRt.anchoredPosition = new Vector2(-45f, 20f);
                laRt.sizeDelta = new Vector2(62, 62);
                var laImg = localAvatarObj.GetComponent<Image>();
                laImg.preserveAspect = true;
                gameTableView.localAvatarImage = laImg;

                var localNameObj = CreateText("LocalName", "Người chơi", new Vector2(32f, 42f), new Vector2(86, 22), 13, new Color(1f, 0.9f, 0.4f), localDashObj.transform);
                gameTableView.localNameText = localNameObj.GetComponent<Text>();

                var localRoleObj = CreateText("LocalRole", "⭐ CẢNH SÁT TRƯỞNG", new Vector2(32f, 18f), new Vector2(88, 32), 10, Color.white, localDashObj.transform);
                gameTableView.localRoleText = localRoleObj.GetComponent<Text>();

                // Bullet/health tokens row
                var bulletContainer = new GameObject("BulletContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                bulletContainer.transform.SetParent(localDashObj.transform, false);
                var bcRt = bulletContainer.GetComponent<RectTransform>();
                bcRt.anchoredPosition = new Vector2(5f, -18f);
                bcRt.sizeDelta = new Vector2(128, 28);
                var bhlg = bulletContainer.GetComponent<HorizontalLayoutGroup>();
                bhlg.childAlignment = TextAnchor.MiddleLeft;
                bhlg.spacing = 4f;
                gameTableView.localBulletHealthContainer = bulletContainer.transform;

                // Equipment tray
                var equipTray = new GameObject("EquipmentTray", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                equipTray.transform.SetParent(localDashObj.transform, false);
                var etRt = equipTray.GetComponent<RectTransform>();
                etRt.anchoredPosition = new Vector2(5f, -48f);
                etRt.sizeDelta = new Vector2(128, 28);
                var ehlg = equipTray.GetComponent<HorizontalLayoutGroup>();
                ehlg.childAlignment = TextAnchor.MiddleLeft;
                ehlg.spacing = 6f;
                gameTableView.localEquipmentTray = equipTray.transform;

                // Hand layout (bottom center)
                var handObj = new GameObject("HandLayout", typeof(RectTransform), typeof(HandCardFanLayout));
                handObj.transform.SetParent(tableContent.transform, false);
                var handRt = handObj.GetComponent<RectTransform>();
                handRt.anchorMin = new Vector2(0f, 0f);
                handRt.anchorMax = new Vector2(1f, 0f);
                handRt.pivot = new Vector2(0.5f, 0f);
                handRt.anchoredPosition = new Vector2(0, 4f);
                handRt.sizeDelta = new Vector2(0, 205f);
                gameTableView.handCardLayout = handObj.GetComponent<HandCardFanLayout>();
                gameTableView.handCardLayout.cardSize = new Vector2(118f, 177f);
                gameTableView.handCardLayout.cardSpacing = 64f;
                gameTableView.handCardLayout.maxFanAngle = 3f;
                gameTableView.handCardLayout.arcHeight = 10f;
                gameTableView.handCardLayout.horizontalPadding = 610f;
                gameTableView.handCardLayout.baseCenterX = 60f;
                gameTableView.handCardLayout.baseCenterY = 91f;

                // Target Banner (below center zone)
                var tBannerObj = new GameObject("TargetBanner", typeof(RectTransform), typeof(Image));
                tBannerObj.transform.SetParent(tableContent.transform, false);
                var tbRt = tBannerObj.GetComponent<RectTransform>();
                tbRt.anchoredPosition = new Vector2(0, 112f);
                tbRt.sizeDelta = new Vector2(360, 44);
                tBannerObj.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
                tBannerObj.GetComponent<Image>().type = Image.Type.Sliced;
                tBannerObj.GetComponent<Image>().color = new Color(0.36f, 0.16f, 0.08f, 0.96f);
                var tbTxtObj = CreateText("Text", "CHỌN MỤC TIÊU HỢP LỆ", Vector2.zero, new Vector2(340, 40), 15, BangUITheme.Ivory, tBannerObj.transform);
                gameTableView.targetBannerObj = tBannerObj;
                gameTableView.targetBannerText = tbTxtObj.GetComponent<Text>();

                // Card Preview Tooltip (just above hand cards)
                var ttObj = new GameObject("CardPreviewTooltip", typeof(RectTransform), typeof(Image));
                ttObj.transform.SetParent(tableContent.transform, false);
                var ttRt = ttObj.GetComponent<RectTransform>();
                ttRt.anchoredPosition = new Vector2(70f, -112f);
                ttRt.sizeDelta = new Vector2(500, 38);
                ttObj.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.9f);
                var ttTxtObj = CreateText("Text", "Chọn một lá bài để xem công dụng", Vector2.zero, new Vector2(480, 34), 13, new Color(1f, 0.9f, 0.6f), ttObj.transform);
                gameTableView.cardPreviewTooltipObj = ttObj;
                gameTableView.cardPreviewTooltipText = ttTxtObj.GetComponent<Text>();

                // Action Buttons — right column, all within x ≤ 560 (half of 1280/canvas)
                // DrawCard shown during DRAW phase (step 1)
                gameTableView.drawCardButton = CreateButton("DrawCardBtn", "1  •  RÚT 2 LÁ", new Vector2(412f, -252f), BangUITheme.Brass, tableContent.transform, new Vector2(170, 50));
                // PlayCard shown after selecting a card (step 2)
                gameTableView.playCardButton = CreateButton("PlayCardBtn", "2  •  ĐÁNH BÀI", new Vector2(412f, -252f), BangUITheme.Brass, tableContent.transform, new Vector2(170, 50));
                gameTableView.playCardButtonText = gameTableView.playCardButton.GetComponentInChildren<Text>();
                // Cancel shown alongside PlayCard
                gameTableView.cancelTargetButton = CreateButton("CancelTargetBtn", "HỦY CHỌN", new Vector2(412f, -194f), BangUITheme.Danger, tableContent.transform, new Vector2(160, 46));
                // EndTurn always visible during PLAY phase (step 3)
                gameTableView.endTurnButton = CreateButton("EndTurnBtn", "3  •  KẾT THÚC LƯỢT", new Vector2(412f, -252f), BangUITheme.SurfaceRaised, tableContent.transform, new Vector2(170, 50));
                // Ability button (Sid Ketchum only)
                gameTableView.abilityButton = CreateButton("AbilityBtn", "KỸ NĂNG", new Vector2(412f, -136f), new Color(0.42f, 0.24f, 0.62f), tableContent.transform, new Vector2(160, 46));
                gameTableView.abilityButton.gameObject.SetActive(false);

                var micButton = CreateButton("VoiceMicBtn", "MIC: TẮT", new Vector2(-490f, 310f), BangUITheme.SurfaceRaised, tableContent.transform, new Vector2(160, 50));
                var voice = FindAnyObjectByType<VoiceChatManager>();
                if (voice != null) voice.Initialize(micButton);

                var introRoot = CreateFullScreenPanel("MatchStartOverlay", tableContent.transform);
                introRoot.GetComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 0.96f);
                var introHalo = new GameObject("SheriffIntroHalo", typeof(RectTransform), typeof(Image), typeof(Outline));
                introHalo.transform.SetParent(introRoot.transform, false);
                introHalo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 105f);
                introHalo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 300);
                introHalo.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
                introHalo.GetComponent<Image>().type = Image.Type.Sliced;
                introHalo.GetComponent<Image>().color = new Color(BangUITheme.Brass.r, BangUITheme.Brass.g, BangUITheme.Brass.b, 0.24f);
                introHalo.GetComponent<Outline>().effectColor = BangUITheme.Brass;
                introHalo.GetComponent<Outline>().effectDistance = new Vector2(5, -5);

                var introAvatarObj = new GameObject("SheriffIntroAvatar", typeof(RectTransform), typeof(Image));
                introAvatarObj.transform.SetParent(introRoot.transform, false);
                introAvatarObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 105f);
                introAvatarObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 260);
                var introAvatar = introAvatarObj.GetComponent<Image>();
                introAvatar.preserveAspect = true;

                var introBadge = new GameObject("SheriffIntroBadge", typeof(RectTransform), typeof(Image));
                introBadge.transform.SetParent(introRoot.transform, false);
                introBadge.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, 15f);
                introBadge.GetComponent<RectTransform>().sizeDelta = new Vector2(98, 138);
                introBadge.GetComponent<Image>().sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                introBadge.GetComponent<Image>().preserveAspect = true;

                var introTitle = CreateText("IntroTitle", "CẢNH SÁT TRƯỞNG ĐÃ LỘ DIỆN", new Vector2(0, 350f), new Vector2(1000, 72), 36, BangUITheme.Brass, introRoot.transform).GetComponent<Text>();
                var introSubtitle = CreateText("IntroSubtitle", "SHERIFF  •  +1 MÁU  •  ĐI LƯỢT ĐẦU", new Vector2(0, -105f), new Vector2(1000, 50), 21, Color.white, introRoot.transform).GetComponent<Text>();
                var introCountdown = CreateText("IntroCountdown", "TRẬN ĐẤU BẮT ĐẦU SAU 5 GIÂY", new Vector2(0, -175f), new Vector2(700, 42), 18, BangUITheme.Muted, introRoot.transform).GetComponent<Text>();
                var introController = tableObj.AddComponent<MatchStartSequenceUI>();
                introController.Initialize(introRoot, introTitle, introSubtitle, introCountdown, introAvatar);
                introRoot.SetActive(false);

                flowController.gameTableView = gameTableView;
            }

            // 6. RESULT VIEW
            if (resultView == null)
            {
                var resultObj = CreateFullScreenPanel("ResultView", canvas.transform);
                resultView = resultObj.AddComponent<ResultView>();

                var winTitle = CreateText("WinnerTitle", "🏆 PHE CẢNH SÁT TRƯỞNG THẮNG!", new Vector2(0, 290f), new Vector2(760, 54), 27, Color.yellow, resultObj.transform);
                resultView.winnerTitleText = winTitle.GetComponent<Text>();

                var resultsContainer = new GameObject("ResultsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                resultsContainer.transform.SetParent(resultObj.transform, false);
                var rRt = resultsContainer.GetComponent<RectTransform>();
                rRt.anchoredPosition = new Vector2(0, 30f);
                rRt.sizeDelta = new Vector2(1180, 280);
                var hlg = resultsContainer.GetComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 15f;
                resultView.playerResultsContainer = resultsContainer.transform;

                resultView.rematchButton = CreateButton("RematchBtn", "🔄 CHƠI LẠI (REMATCH)", new Vector2(160f, -300f), new Color(0.2f, 0.7f, 0.25f), resultObj.transform, new Vector2(260, 58));
                resultView.returnToLobbyButton = CreateButton("LobbyBtn", "🚪 VỀ SẢNH CHÍNH", new Vector2(-160f, -300f), new Color(0.45f, 0.25f, 0.15f), resultObj.transform, new Vector2(220, 58));

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
                cbRt.sizeDelta = new Vector2(1150, 360);
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
                optRt.anchoredPosition = new Vector2(0, -75f);
                optRt.sizeDelta = new Vector2(1050, 70);
                var ohlg = optContainer.GetComponent<HorizontalLayoutGroup>();
                ohlg.childAlignment = TextAnchor.MiddleCenter;
                ohlg.spacing = 15f;
                interactionController.optionsContainer = optContainer.transform;

                interactionController.confirmButton = CreateButton("InteractionConfirm", "XÁC NHẬN", new Vector2(155f, -145f), BangUITheme.Success, cardBox.transform, new Vector2(260, 56));
                interactionController.confirmButtonText = interactionController.confirmButton.GetComponentInChildren<Text>();
                interactionController.cancelButton = CreateButton("InteractionCancel", "PASS / BỎ QUA", new Vector2(-155f, -145f), BangUITheme.Danger, cardBox.transform, new Vector2(260, 56));
            }

            // Ensure all button listeners are bound
            EnsureContextualGuide(canvas.transform);
            EnsureNetworkOverlay(canvas.transform);
            if (canvas.GetComponent<BangBang.UI.ChatOverlay>() == null) canvas.gameObject.AddComponent<BangBang.UI.ChatOverlay>();
            if (canvas.GetComponent<BangUITheme>() == null) canvas.gameObject.AddComponent<BangUITheme>();

            homeScreen?.BindListeners();
            lobbyView?.BindListeners();
            waitingRoomView?.BindListeners();
            characterSelectionView?.BindListeners();
            gameTableView?.BindListeners();
            resultView?.BindListeners();
        }

        private void EnsureContextualGuide(Transform canvasTransform)
        {
            if (canvasTransform.Find("GlobalSafeArea") != null) return;

            var safeRoot = new GameObject("GlobalSafeArea", typeof(RectTransform));
            safeRoot.transform.SetParent(canvasTransform, false);
            var safeRt = safeRoot.GetComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeRoot.AddComponent<SafeAreaFitter>();

            var guideRoot = new GameObject("ContextualGuide", typeof(RectTransform), typeof(Image), typeof(ContextualGuideUI));
            guideRoot.transform.SetParent(safeRoot.transform, false);
            var guideRt = guideRoot.GetComponent<RectTransform>();
            guideRt.anchorMin = new Vector2(0.5f, 1f);
            guideRt.anchorMax = new Vector2(0.5f, 1f);
            guideRt.pivot = new Vector2(0.5f, 1f);
            guideRt.anchoredPosition = new Vector2(0f, -12f);
            guideRt.sizeDelta = new Vector2(500f, 60f);
            var guideImage = guideRoot.GetComponent<Image>();
            guideImage.color = new Color(0.09f, 0.075f, 0.065f, 0.94f);
            guideImage.raycastTarget = false;

            var eyebrowObj = CreateText("Eyebrow", "BƯỚC HIỆN TẠI", new Vector2(0f, 14f), new Vector2(460f, 18f), 11, BangUITheme.Brass, guideRoot.transform);
            var instructionObj = CreateText("Instruction", "Đang đồng bộ trạng thái trận đấu…", new Vector2(0f, -10f), new Vector2(460f, 28f), 14, BangUITheme.Ivory, guideRoot.transform);
            eyebrowObj.GetComponent<Text>().raycastTarget = false;
            instructionObj.GetComponent<Text>().raycastTarget = false;

            guideRoot.GetComponent<ContextualGuideUI>().Initialize(
                guideRoot,
                eyebrowObj.GetComponent<Text>(),
                instructionObj.GetComponent<Text>());
            safeRoot.transform.SetAsLastSibling();
        }

        private void EnsureNetworkOverlay(Transform canvasTransform)
        {
            if (canvasTransform.Find("NetworkStatusOverlay") != null) return;
            var root = CreateFullScreenPanel("NetworkStatusOverlay", canvasTransform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            var status = CreateText("NetworkStatus", "Đang kết nối lại và đồng bộ trận đấu…", Vector2.zero, new Vector2(900, 60), 24, Color.white, root.transform).GetComponent<Text>();
            var controller = canvasTransform.gameObject.AddComponent<NetworkStatusOverlay>();
            controller.Initialize(root, status);
            root.transform.SetAsLastSibling();
            root.SetActive(false);
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
            boxObj.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
            boxObj.GetComponent<Image>().type = Image.Type.Sliced;

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
            bRt.sizeDelta = new Vector2(760, 480);
            boxObj.GetComponent<Image>().color = BangUITheme.Surface;
            boxObj.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
            boxObj.GetComponent<Image>().type = Image.Type.Sliced;

            CreateText("Title", title, new Vector2(0, 160f), new Vector2(600, 40), 22, Color.yellow, boxObj.transform);
            CreateText("Content", content, new Vector2(0, 105f), new Vector2(620, 34), 14, BangUITheme.Muted, boxObj.transform);

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

        private GameObject CreateSurface(string name, Vector2 position, Vector2 size, Transform parent)
        {
            var surface = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            surface.transform.SetParent(parent, false);
            var rt = surface.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            surface.GetComponent<Image>().color = new Color(BangUITheme.Surface.r, BangUITheme.Surface.g, BangUITheme.Surface.b, 0.94f);
            surface.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
            surface.GetComponent<Image>().type = Image.Type.Sliced;
            var outline = surface.GetComponent<Outline>();
            outline.effectColor = new Color(BangUITheme.Brass.r, BangUITheme.Brass.g, BangUITheme.Brass.b, 0.24f);
            outline.effectDistance = new Vector2(1f, -1f);
            return surface;
        }

        private InputField CreateInputField(string name, string placeholder, Vector2 position, Vector2 size, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(Outline));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            root.GetComponent<Image>().color = BangUITheme.Ink;
            root.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
            root.GetComponent<Image>().type = Image.Type.Sliced;
            root.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.12f);
            var value = CreateText("Text", string.Empty, Vector2.zero, size - new Vector2(32, 0), 16, BangUITheme.Ivory, root.transform).GetComponent<Text>();
            value.alignment = TextAnchor.MiddleLeft;
            var hint = CreateText("Placeholder", placeholder, Vector2.zero, size - new Vector2(32, 0), 16, BangUITheme.Muted, root.transform).GetComponent<Text>();
            hint.alignment = TextAnchor.MiddleLeft;
            var input = root.GetComponent<InputField>();
            input.textComponent = value;
            input.placeholder = hint;
            input.characterLimit = 30;
            return input;
        }

        private Dropdown CreateDropdown(string name, string[] options, int selected, Vector2 position, Vector2 size, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(Outline));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            root.GetComponent<Image>().color = BangUITheme.Ink;
            root.GetComponent<Image>().sprite = BangUITheme.RoundedSprite;
            root.GetComponent<Image>().type = Image.Type.Sliced;
            root.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.12f);
            var label = CreateText("Label", options[selected], new Vector2(-10, 0), size - new Vector2(42, 0), 16, BangUITheme.Ivory, root.transform).GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            CreateText("Arrow", "▼", new Vector2(size.x * 0.5f - 25f, 0), new Vector2(32, size.y), 16, BangUITheme.Brass, root.transform);
            var dropdown = root.GetComponent<Dropdown>();
            dropdown.captionText = label;
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1);
            templateRt.anchoredPosition = new Vector2(0, -4);
            templateRt.sizeDelta = new Vector2(0, Mathf.Min(220, options.Length * 48));
            template.GetComponent<Image>().color = BangUITheme.SurfaceRaised;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.sizeDelta = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, options.Length * 48);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 1);
            itemRt.anchorMax = new Vector2(1, 1);
            itemRt.pivot = new Vector2(0.5f, 1);
            itemRt.sizeDelta = new Vector2(0, 48);
            var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            var itemBgRt = itemBg.GetComponent<RectTransform>();
            itemBgRt.anchorMin = Vector2.zero;
            itemBgRt.anchorMax = Vector2.one;
            itemBgRt.sizeDelta = Vector2.zero;
            itemBg.GetComponent<Image>().color = BangUITheme.SurfaceRaised;
            var itemLabel = CreateText("Item Label", "Option", Vector2.zero, new Vector2(size.x - 28, 48), 15, BangUITheme.Ivory, item.transform).GetComponent<Text>();
            itemLabel.alignment = TextAnchor.MiddleLeft;
            item.GetComponent<Toggle>().targetGraphic = itemBg.GetComponent<Image>();

            var scroll = template.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.value = selected;
            template.SetActive(false);
            return dropdown;
        }

        private Button CreateButton(string name, string text, Vector2 anchoredPos, Color color, Transform parent, Vector2? size = null)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size ?? new Vector2(320, 65);

            var img = btnObj.GetComponent<Image>();
            img.color = color;
            img.sprite = BangUITheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            var shadow = btnObj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(0f, -4f);

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

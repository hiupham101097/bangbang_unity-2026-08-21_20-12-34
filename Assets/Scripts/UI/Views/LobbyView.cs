using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class LobbyView : MonoBehaviour
    {
        [Header("Header & Navigation")]
        public Image backgroundImage;
        public Button backToHomeButton;
        public Text titleText;
        public Text connectionStatusText;

        [Header("Room Actions")]
        public Button openCreateRoomPopupButton;
        public InputField roomPinInput;
        public Button joinByPinButton;
        public Button refreshRoomsButton;

        [Header("Create Room Popup")]
        public GameObject createRoomPopup;
        public InputField roomNameInput;
        public Dropdown maxPlayersDropdown;
        public Toggle addBotsToggle; // Quyền chọn thêm Bot chơi chung
        public Dropdown botCountDropdown;
        public Toggle privateRoomToggle;
        public InputField passwordInput;
        public Dropdown turnTimeDropdown;
        public Button confirmCreateRoomButton;
        public Button closeCreateRoomPopupButton;
        public Text createRoomStatusText;

        [Header("Room List Scroll Content")]
        public Transform roomListContentTransform;

        private readonly List<GameObject> _roomItems = new List<GameObject>();

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            SetupVisuals();
            if (createRoomPopup != null) createRoomPopup.SetActive(false);

            if (GameStateStore.Instance?.Gateway != null)
            {
                GameStateStore.Instance.Gateway.OnRoomListUpdated += RenderRoomList;
                GameStateStore.Instance.Gateway.OnConnectionStateChanged += UpdateConnectionState;
            }

            HandleRefreshRoomsClicked();
        }

        public void BindListeners()
        {
            if (backToHomeButton != null)
            {
                backToHomeButton.onClick.RemoveAllListeners();
                backToHomeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    gameObject.SetActive(false);
                    if (GameBootstrap.Instance?.homeScreen != null)
                    {
                        GameBootstrap.Instance.homeScreen.gameObject.SetActive(true);
                    }
                });
            }

            if (openCreateRoomPopupButton != null)
            {
                openCreateRoomPopupButton.onClick.RemoveAllListeners();
                openCreateRoomPopupButton.onClick.AddListener(() => ShowCreateRoomPopup(true));
            }

            if (closeCreateRoomPopupButton != null)
            {
                closeCreateRoomPopupButton.onClick.RemoveAllListeners();
                closeCreateRoomPopupButton.onClick.AddListener(() => ShowCreateRoomPopup(false));
            }

            if (confirmCreateRoomButton != null)
            {
                confirmCreateRoomButton.onClick.RemoveAllListeners();
                confirmCreateRoomButton.onClick.AddListener(HandleCreateRoomConfirmed);
            }

            if (joinByPinButton != null)
            {
                joinByPinButton.onClick.RemoveAllListeners();
                joinByPinButton.onClick.AddListener(HandleJoinByPinClicked);
            }

            if (refreshRoomsButton != null)
            {
                refreshRoomsButton.onClick.RemoveAllListeners();
                refreshRoomsButton.onClick.AddListener(HandleRefreshRoomsClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance?.Gateway != null)
            {
                GameStateStore.Instance.Gateway.OnRoomListUpdated -= RenderRoomList;
                GameStateStore.Instance.Gateway.OnConnectionStateChanged -= UpdateConnectionState;
            }
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("wild_west_town");
                if (townSprite != null)
                {
                    backgroundImage.sprite = townSprite;
                    backgroundImage.color = new Color(0.45f, 0.45f, 0.45f);
                }
            }
            if (GameStateStore.Instance?.Gateway != null)
                UpdateConnectionState(GameStateStore.Instance.Gateway.CurrentConnectionState);
        }

        public void ShowCreateRoomPopup(bool show)
        {
            if (createRoomPopup != null) createRoomPopup.SetActive(show);
            AudioManager.Instance?.PlaySFX("button_tap");
        }

        private async void HandleCreateRoomConfirmed()
        {
            string rName = roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text) ? roomNameInput.text : "Quán Rượu Saloon";
            int maxP = maxPlayersDropdown != null ? maxPlayersDropdown.value + 4 : 8;
            bool isPriv = privateRoomToggle != null && privateRoomToggle.isOn;
            string pass = passwordInput != null ? passwordInput.text : "";
            int turnSec = turnTimeDropdown != null ? (turnTimeDropdown.value + 1) * 15 : 30;

            if (GameStateStore.Instance?.Gateway == null)
            {
                SetCreateRoomStatus("Chưa kết nối máy chủ. Hãy kiểm tra lại kết nối.", true);
                return;
            }

            SetCreateRoomStatus("Đang tạo phòng…", false);
            if (confirmCreateRoomButton != null) confirmCreateRoomButton.interactable = false;
            GameStateStore.Instance?.SetRequestPending(true);

            bool ok = await GameStateStore.Instance.Gateway.CreateRoomAsync(rName, maxP, isPriv, pass, turnSec);
            if (!ok)
            {
                GameStateStore.Instance.SetRequestPending(false);
                SetCreateRoomStatus("Không thể tạo phòng. Kiểm tra server rồi thử lại.", true);
                if (confirmCreateRoomButton != null) confirmCreateRoomButton.interactable = true;
                return;
            }

            SetCreateRoomStatus(string.Empty, false);
            ShowCreateRoomPopup(false);
            GameFlowController.Instance?.TransitionToState(ServerGameState.WAITING);
            if (confirmCreateRoomButton != null) confirmCreateRoomButton.interactable = true;
        }

        private void SetCreateRoomStatus(string message, bool isError)
        {
            if (createRoomStatusText == null) return;
            createRoomStatusText.text = message;
            createRoomStatusText.color = isError ? BangUITheme.Danger : BangUITheme.Muted;
        }

        private async void HandleJoinByPinClicked()
        {
            string pin = roomPinInput != null ? roomPinInput.text.Trim() : "";
            if (string.IsNullOrEmpty(pin)) return;

            GameStateStore.Instance?.SetRequestPending(true);
            AudioManager.Instance?.PlaySFX("button_tap");

            if (GameStateStore.Instance?.Gateway != null)
            {
                bool ok = await GameStateStore.Instance.Gateway.JoinRoomAsync(pin);
                if (!ok) GameStateStore.Instance.SetRequestPending(false);
            }
        }

        private async void HandleRefreshRoomsClicked()
        {
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.RefreshRoomListAsync();
            }
        }

        private void RenderRoomList(List<RoomSummaryDTO> rooms)
        {
            if (roomListContentTransform == null) return;

            foreach (var item in _roomItems) Destroy(item);
            _roomItems.Clear();

            if (rooms == null) return;

            foreach (var r in rooms)
            {
                var itemObj = CreateRoomListItem(r);
                itemObj.transform.SetParent(roomListContentTransform, false);
                _roomItems.Add(itemObj);
            }
        }

        private GameObject CreateRoomListItem(RoomSummaryDTO room)
        {
            var itemObj = new GameObject("Room_" + room.roomCode, typeof(RectTransform), typeof(Image));
            var rt = itemObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(880, 80);

            var img = itemObj.GetComponent<Image>();
            img.color = new Color(0.18f, 0.12f, 0.08f, 0.95f);

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(itemObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(-230f, 0);
            nameRt.sizeDelta = new Vector2(340, 40);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 18;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color = new Color(0.95f, 0.85f, 0.5f);
            nameTxt.text = room.roomName;

            // Player count & Ping
            var infoObj = new GameObject("Info", typeof(RectTransform), typeof(Text));
            infoObj.transform.SetParent(itemObj.transform, false);
            var infoRt = infoObj.GetComponent<RectTransform>();
            infoRt.anchoredPosition = new Vector2(100f, 0);
            infoRt.sizeDelta = new Vector2(200, 40);
            var infoTxt = infoObj.GetComponent<Text>();
            infoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            infoTxt.fontSize = 15;
            infoTxt.alignment = TextAnchor.MiddleCenter;
            infoTxt.color = Color.white;
            infoTxt.text = room.currentPlayers + "/" + room.maxPlayers + " • 🟢 " + room.pingMs + "ms";

            // Join Button
            var joinObj = new GameObject("JoinBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            joinObj.transform.SetParent(itemObj.transform, false);
            var joinRt = joinObj.GetComponent<RectTransform>();
            joinRt.anchoredPosition = new Vector2(330f, 0);
            joinRt.sizeDelta = new Vector2(140, 50);
            var jImg = joinObj.GetComponent<Image>();
            jImg.color = new Color(0.2f, 0.6f, 0.25f);

            var jTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            jTxtObj.transform.SetParent(joinObj.transform, false);
            var jTxtRt = jTxtObj.GetComponent<RectTransform>();
            jTxtRt.anchorMin = Vector2.zero;
            jTxtRt.anchorMax = Vector2.one;
            var jTxt = jTxtObj.GetComponent<Text>();
            jTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            jTxt.fontSize = 16;
            jTxt.fontStyle = FontStyle.Bold;
            jTxt.alignment = TextAnchor.MiddleCenter;
            jTxt.color = Color.white;
            jTxt.text = "VÀO BÀN";

            var btn = joinObj.GetComponent<Button>();
            btn.onClick.AddListener(async () =>
            {
                AudioManager.Instance?.PlaySFX("button_tap");
                GameStateStore.Instance?.SetRequestPending(true);
                if (GameStateStore.Instance?.Gateway != null)
                {
                    bool ok = await GameStateStore.Instance.Gateway.JoinRoomAsync(room.roomId);
                    if (!ok) GameStateStore.Instance.SetRequestPending(false);
                }
            });

            return itemObj;
        }

        private void UpdateConnectionState(ConnectionState state)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = state == ConnectionState.Connected ? "ONLINE • MÁY CHỦ SẴN SÀNG" : "OFFLINE • KIỂM TRA KẾT NỐI";
                connectionStatusText.color = state == ConnectionState.Connected ? BangUITheme.Success : BangUITheme.Danger;
            }
        }
    }
}

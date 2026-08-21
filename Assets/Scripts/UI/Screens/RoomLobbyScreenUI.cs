using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Screens
{
    public class RoomLobbyScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image backgroundImage;
        public Text roomTitleText;
        public Text roomPinText;
        public Transform playerSlotsContainer;
        public Button startGameButton;
        public Button addBotButton;
        public Button leaveRoomButton;
        public Button readyToggleButton;
        public Text readyButtonText;

        public event Action OnStartGameRequested;

        private int _currentLobbyCount = 5;
        private bool _isReady = true;

        private void Awake()
        {
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.Home);
                });
            }

            if (addBotButton != null)
            {
                addBotButton.onClick.AddListener(() =>
                {
                    if (_currentLobbyCount < 7)
                    {
                        _currentLobbyCount++;
                        AudioManager.Instance?.PlaySFX("button_tap");
                        PopulateSlots();
                    }
                });
            }

            if (readyToggleButton != null)
            {
                readyToggleButton.onClick.AddListener(() =>
                {
                    _isReady = !_isReady;
                    if (readyButtonText != null) readyButtonText.text = _isReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
                    AudioManager.Instance?.PlaySFX("button_tap");
                });
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("bang_shot");
                    OnStartGameRequested?.Invoke();
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.Battle);
                });
            }
        }

        private void Start()
        {
            SetupVisuals();
            PopulateSlots();
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("wild_west_town");
                if (townSprite != null) backgroundImage.sprite = townSprite;
                backgroundImage.color = new Color(0.3f, 0.3f, 0.3f);
            }

            if (roomTitleText != null) roomTitleText.text = "🤠 PHÒNG SALOON VIỄN TÂY #01";
            if (roomPinText != null) roomPinText.text = "MÃ PIN: SALOON";
        }

        public void PopulateSlots()
        {
            if (playerSlotsContainer == null) return;

            foreach (Transform child in playerSlotsContainer) Destroy(child.gameObject);

            string[] botNames = { "Bill Độc Nhãn", "Apache Jack", "Django Nhanh Nhẹn", "Doc Holliday", "Jesse Râu Đen", "Billy Cao Kều" };
            string[] charIds = { "willy_the_kid", "bart_cassidy", "black_jack", "calamity_janet", "el_gringo", "kit_carlson", "rose_oolan" };

            for (int i = 0; i < 7; i++)
            {
                bool isOccupied = i < _currentLobbyCount;
                bool isLocal = i == 0;
                string pName = isLocal ? "Cao bồi của bạn (Chủ phòng)" : (isOccupied ? botNames[(i - 1) % botNames.Length] : "Trống (Bấm Thêm Bot)");
                string charId = charIds[i % charIds.Length];

                var slotObj = CreateSlotCard(i, isOccupied, isLocal, pName, charId);
                slotObj.transform.SetParent(playerSlotsContainer, false);
            }
        }

        private GameObject CreateSlotCard(int index, bool isOccupied, bool isLocal, string pName, string charId)
        {
            var slotObj = new GameObject("Slot_" + index, typeof(RectTransform), typeof(Image));
            var rt = slotObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(170, 240);

            var bgImg = slotObj.GetComponent<Image>();
            bgImg.color = isOccupied ? new Color(0.18f, 0.12f, 0.08f, 0.95f) : new Color(0.1f, 0.08f, 0.06f, 0.6f);

            // Avatar
            var avatarObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarObj.transform.SetParent(slotObj.transform, false);
            var avatarRt = avatarObj.GetComponent<RectTransform>();
            avatarRt.anchoredPosition = new Vector2(0, 35f);
            avatarRt.sizeDelta = new Vector2(90, 90);
            var avatarImg = avatarObj.GetComponent<Image>();
            avatarImg.preserveAspect = true;

            if (isOccupied)
            {
                var sprite = CardCatalogDatabase.LoadSprite("Characters/" + charId);
                if (sprite != null) avatarImg.sprite = sprite;
            }
            else
            {
                avatarImg.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(slotObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, -35f);
            nameRt.sizeDelta = new Vector2(160, 36);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = isLocal ? new Color(1f, 0.85f, 0.3f) : Color.white;
            nameTxt.text = pName;

            // Status Badge
            var statusObj = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusObj.transform.SetParent(slotObj.transform, false);
            var statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchoredPosition = new Vector2(0, -75f);
            statusRt.sizeDelta = new Vector2(150, 26);
            var statusTxt = statusObj.GetComponent<Text>();
            statusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusTxt.fontSize = 12;
            statusTxt.fontStyle = FontStyle.Bold;
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = isOccupied ? new Color(0.3f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            statusTxt.text = isOccupied ? "🟢 ĐÃ SẴN SÀNG" : "⚪ ĐANG CHỜ";

            return slotObj;
        }
    }
}

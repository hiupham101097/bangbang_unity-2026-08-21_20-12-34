using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Screens
{
    [Serializable]
    public class MockRoomItem
    {
        public string id;
        public string name;
        public string code;
        public int currentPlayers;
        public int maxPlayers;
        public int pingMs;
    }

    public class RoomListScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image backgroundImage;
        public Transform roomListContentTransform;
        public Button createRoomButton;
        public Button joinByCodeButton;
        public Button backToHomeButton;
        public InputField roomCodeInput;

        private readonly List<GameObject> _roomItemObjects = new List<GameObject>();

        private void Awake()
        {
            if (backToHomeButton != null)
            {
                backToHomeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.Home);
                });
            }

            if (createRoomButton != null)
            {
                createRoomButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
                });
            }

            if (joinByCodeButton != null)
            {
                joinByCodeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
                });
            }
        }

        private void Start()
        {
            SetupVisuals();
            PopulateMockRooms();
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("wild_west_town");
                if (townSprite != null) backgroundImage.sprite = townSprite;
                backgroundImage.color = new Color(0.4f, 0.4f, 0.4f);
            }
        }

        public void PopulateMockRooms()
        {
            if (roomListContentTransform == null) return;

            foreach (var obj in _roomItemObjects) Destroy(obj);
            _roomItemObjects.Clear();

            var sampleRooms = new List<MockRoomItem> {
                new MockRoomItem { id = "room_1", name = "🤠 Quán Rượu Saloon #01", code = "SALOON", currentPlayers = 5, maxPlayers = 7, pingMs = 24 },
                new MockRoomItem { id = "room_2", name = "🌵 Đấu Trường Cát Cháy", code = "DESERT", currentPlayers = 4, maxPlayers = 7, pingMs = 35 },
                new MockRoomItem { id = "room_3", name = "⭐ Trụ Sở Cảnh Sát Trưởng", code = "SHERIFF", currentPlayers = 6, maxPlayers = 7, pingMs = 18 },
                new MockRoomItem { id = "room_4", name = "🚂 Chuyến Tàu Vàng Wells Fargo", code = "TRAIN", currentPlayers = 3, maxPlayers = 7, pingMs = 42 }
            };

            foreach (var room in sampleRooms)
            {
                var itemObj = CreateRoomItem(room);
                itemObj.transform.SetParent(roomListContentTransform, false);
                _roomItemObjects.Add(itemObj);
            }
        }

        private GameObject CreateRoomItem(MockRoomItem room)
        {
            var itemObj = new GameObject("RoomItem_" + room.code, typeof(RectTransform), typeof(Image));
            var rt = itemObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800, 75);

            var bgImg = itemObj.GetComponent<Image>();
            bgImg.color = new Color(0.18f, 0.12f, 0.08f, 0.92f); // Mahogany wood

            // Room Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(itemObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(-220f, 0);
            nameRt.sizeDelta = new Vector2(300, 40);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 18;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color = new Color(0.95f, 0.85f, 0.5f);
            nameTxt.text = room.name;

            // Player Count
            var countObj = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countObj.transform.SetParent(itemObj.transform, false);
            var countRt = countObj.GetComponent<RectTransform>();
            countRt.anchoredPosition = new Vector2(100f, 0);
            countRt.sizeDelta = new Vector2(150, 40);
            var countTxt = countObj.GetComponent<Text>();
            countTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countTxt.fontSize = 16;
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.color = Color.white;
            countTxt.text = room.currentPlayers + " / " + room.maxPlayers + " Cao Bồi";

            // Join Button
            var joinBtnObj = new GameObject("JoinButton", typeof(RectTransform), typeof(Image), typeof(Button));
            joinBtnObj.transform.SetParent(itemObj.transform, false);
            var joinBtnRt = joinBtnObj.GetComponent<RectTransform>();
            joinBtnRt.anchoredPosition = new Vector2(310f, 0);
            joinBtnRt.sizeDelta = new Vector2(130, 48);
            var joinBtnImg = joinBtnObj.GetComponent<Image>();
            joinBtnImg.color = new Color(0.2f, 0.6f, 0.25f);

            var joinTxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            joinTxtObj.transform.SetParent(joinBtnObj.transform, false);
            var joinTxtRt = joinTxtObj.GetComponent<RectTransform>();
            joinTxtRt.anchorMin = Vector2.zero;
            joinTxtRt.anchorMax = Vector2.one;
            var joinTxt = joinTxtObj.GetComponent<Text>();
            joinTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            joinTxt.fontSize = 16;
            joinTxt.fontStyle = FontStyle.Bold;
            joinTxt.alignment = TextAnchor.MiddleCenter;
            joinTxt.color = Color.white;
            joinTxt.text = "VÀO PHÒNG";

            var btn = joinBtnObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("button_tap");
                ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
            });

            return itemObj;
        }
    }
}

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
    public class WaitingRoomView : MonoBehaviour
    {
        [Header("Room Header")]
        public Text roomCodeText;
        public Button copyCodeButton;
        public Text playerCountText;

        [Header("Seats Layout (7 Seats)")]
        public Transform seatsContainer;

        [Header("Action Controls")]
        public Button readyToggleButton;
        public Text readyButtonText;
        public Button startGameButton;
        public Button addBotButton; // Nút thêm Bot chơi chung
        public Button leaveRoomButton;
        public Text startDisabledReasonText;

        private readonly List<GameObject> _seatCardObjects = new List<GameObject>();
        private bool _isLocalReady;

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += RenderWaitingRoom;
                if (GameStateStore.Instance.CurrentSnapshot != null)
                {
                    RenderWaitingRoom(GameStateStore.Instance.CurrentSnapshot);
                }
            }
        }

        public void BindListeners()
        {
            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.RemoveAllListeners();
                copyCodeButton.onClick.AddListener(() =>
                {
                    if (GameStateStore.Instance?.CurrentSnapshot != null)
                    {
                        GUIUtility.systemCopyBuffer = GameStateStore.Instance.CurrentSnapshot.roomCode;
                        AudioManager.Instance?.PlaySFX("button_tap");
                    }
                });
            }

            if (addBotButton != null)
            {
                addBotButton.onClick.RemoveAllListeners();
                addBotButton.onClick.AddListener(HandleAddBotClicked);
            }

            if (readyToggleButton != null)
            {
                readyToggleButton.onClick.RemoveAllListeners();
                readyToggleButton.onClick.AddListener(HandleToggleReadyClicked);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveAllListeners();
                startGameButton.onClick.AddListener(HandleStartGameClicked);
            }

            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(HandleLeaveRoomClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= RenderWaitingRoom;
            }
        }

        public void RenderWaitingRoom(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null || snapshot.state != ServerGameState.WAITING) return;

            if (roomCodeText != null) roomCodeText.text = "MÃ PHÒNG: " + snapshot.roomCode;
            if (playerCountText != null) playerCountText.text = snapshot.players.Count + " / 7 Người";

            var local = snapshot.players.Find(p => p.id == GameStateStore.Instance.LocalPlayerId);
            bool isHost = local != null && local.isHost;

            if (readyToggleButton != null)
            {
                readyToggleButton.gameObject.SetActive(!isHost);
                _isLocalReady = local != null && local.isReady;
                if (readyButtonText != null) readyButtonText.text = _isLocalReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
            }

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);

                bool canStart = snapshot.players.Count >= 4 && snapshot.players.TrueForAll(p => p.isReady || p.isHost);
                startGameButton.interactable = canStart && !GameStateStore.Instance.IsRequestPending;

                if (startDisabledReasonText != null)
                {
                    if (snapshot.players.Count < 4)
                        startDisabledReasonText.text = "Cần tối thiểu 4 người chơi để bắt đầu (hiện có " + snapshot.players.Count + "/4).";
                    else if (!canStart)
                        startDisabledReasonText.text = "Đang chờ tất cả người chơi Sẵn Sàng...";
                    else
                        startDisabledReasonText.text = "Đã đủ điều kiện! Bấm Bắt Đầu.";
                }
            }

            RenderSeatSlots(snapshot);
        }

        private void RenderSeatSlots(MatchStateSnapshotDTO snapshot)
        {
            if (seatsContainer == null) return;
            foreach (var s in _seatCardObjects) Destroy(s);
            _seatCardObjects.Clear();

            for (int i = 0; i < 7; i++)
            {
                var player = i < snapshot.players.Count ? snapshot.players[i] : null;
                var seatObj = CreateSeatCard(i, player);
                seatObj.transform.SetParent(seatsContainer, false);
                _seatCardObjects.Add(seatObj);
            }
        }

        private GameObject CreateSeatCard(int seatIndex, PlayerSnapshotDTO player)
        {
            var seatObj = new GameObject("Seat_" + seatIndex, typeof(RectTransform), typeof(Image));
            var rt = seatObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(170, 240);

            var bgImg = seatObj.GetComponent<Image>();
            bgImg.color = player != null ? new Color(0.18f, 0.12f, 0.08f, 0.95f) : new Color(0.1f, 0.08f, 0.06f, 0.5f);

            // Avatar
            var avatarObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarObj.transform.SetParent(seatObj.transform, false);
            var avatarRt = avatarObj.GetComponent<RectTransform>();
            avatarRt.anchoredPosition = new Vector2(0, 35f);
            avatarRt.sizeDelta = new Vector2(90, 90);
            var avatarImg = avatarObj.GetComponent<Image>();
            avatarImg.preserveAspect = true;

            if (player != null)
            {
                var sprite = CardCatalogDatabase.LoadSprite(string.IsNullOrEmpty(player.characterId) ? "Characters/willy_the_kid" : "Characters/" + player.characterId);
                if (sprite != null) avatarImg.sprite = sprite;
            }
            else
            {
                avatarImg.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(seatObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, -35f);
            nameRt.sizeDelta = new Vector2(160, 36);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = (player != null && player.isHost) ? new Color(1f, 0.85f, 0.3f) : Color.white;
            nameTxt.text = player != null ? player.name + (player.isHost ? " ⭐" : "") : "Ghế Trống";

            // Status Badge
            var statusObj = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusObj.transform.SetParent(seatObj.transform, false);
            var statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchoredPosition = new Vector2(0, -75f);
            statusRt.sizeDelta = new Vector2(150, 26);
            var statusTxt = statusObj.GetComponent<Text>();
            statusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusTxt.fontSize = 12;
            statusTxt.fontStyle = FontStyle.Bold;
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = player != null ? (player.isReady ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.6f, 0.2f)) : Color.gray;
            statusTxt.text = player != null ? (player.isReady ? "🟢 ĐÃ SẴN SÀNG" : "⏳ ĐANG CHỜ") : "⚪ TRỐNG";

            return seatObj;
        }

        private void HandleAddBotClicked()
        {
            var snap = GameStateStore.Instance?.CurrentSnapshot;
            if (snap == null || snap.players.Count >= 7) return;

            string[] botPool = { "Bill Độc Nhãn", "Apache Jack", "Django Nhanh Nhẹn", "Doc Holliday", "Jesse Râu Đen", "Billy Cao Kều" };
            int nextIdx = snap.players.Count;
            string botName = botPool[(nextIdx - 1) % botPool.Length];

            snap.players.Add(new PlayerSnapshotDTO
            {
                id = "bot_" + nextIdx,
                name = botName,
                seat = nextIdx,
                isHost = false,
                isReady = true,
                currentHealth = 4,
                maxHealth = 4
            });

            AudioManager.Instance?.PlaySFX("button_tap");
            RenderWaitingRoom(snap);
        }

        private async void HandleToggleReadyClicked()
        {
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.ToggleReadyAsync(!_isLocalReady);
            }
        }

        private async void HandleStartGameClicked()
        {
            AudioManager.Instance?.PlaySFX("bang_shot");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.StartGameAsync();
            }
        }

        private async void HandleLeaveRoomClicked()
        {
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.LeaveRoomAsync();
            }
        }
    }
}

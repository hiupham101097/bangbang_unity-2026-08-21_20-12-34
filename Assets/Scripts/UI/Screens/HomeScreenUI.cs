using System;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Screens
{
    public class HomeScreenUI : MonoBehaviour
    {
        [Header("UI Background & Profile")]
        public Image backgroundImage;
        public Image playerAvatarImage;
        public Text playerNameText;
        public Text playerBountyText;

        [Header("Main Menu Action Buttons")]
        public Button quickPlayButton; // Chơi Nhanh Offline Bot
        public Button onlineRoomsButton; // Danh Sách Phòng Online
        public Button collectionButton; // Bộ Sưu Tập Tướng
        public Button settingsButton;

        public event Action OnQuickPlayClicked;
        public event Action OnOnlineRoomsClicked;

        private void Awake()
        {
            if (quickPlayButton != null)
            {
                quickPlayButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    OnQuickPlayClicked?.Invoke();
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomLobby);
                });
            }

            if (onlineRoomsButton != null)
            {
                onlineRoomsButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    OnOnlineRoomsClicked?.Invoke();
                    ScreenManager.Instance?.SwitchToScreen(AppScreenState.RoomList);
                });
            }
        }

        private void Start()
        {
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("wild_west_town");
                if (townSprite != null) backgroundImage.sprite = townSprite;
            }

            if (playerAvatarImage != null)
            {
                var charSprite = CardCatalogDatabase.LoadSprite("Characters/willy_the_kid");
                if (charSprite != null) playerAvatarImage.sprite = charSprite;
            }

            if (playerNameText != null)
            {
                playerNameText.text = "Cao bồi Miền Tây";
            }

            if (playerBountyText != null)
            {
                playerBountyText.text = "Tiền thưởng: $12,500";
            }
        }
    }
}

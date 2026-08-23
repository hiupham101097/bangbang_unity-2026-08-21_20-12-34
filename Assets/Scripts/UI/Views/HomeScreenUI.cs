using System;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class HomeScreenUI : MonoBehaviour
    {
        [Header("Background & Profile")]
        public Image backgroundImage;
        public Image logoImage;
        public Image avatarImage;
        public Text playerNameText;
        public Text playerBountyText;

        [Header("3 Main Home Buttons")]
        public Button startButton; // BẮT ĐẦU -> Chuyển vào Sảnh Chờ (Lobby)
        public Button galleryButton; // BỘ SƯU TẬP THẺ -> Mở Thư Viện Bài
        public Button questsButton; // NHIỆM VỤ -> Mở Popup Nhiệm vụ
        public Button guideButton;  // HƯỚNG DẪN -> Mở Popup Hướng dẫn
        public Button audioToggleButton; // Bật/Tắt Âm thanh
        public Text audioToggleText;

        [Header("Views Reference")]
        public CardGalleryView cardGalleryView;

        [Header("Quests Popup")]
        public GameObject questsPopup;
        public Button closeQuestsButton;

        [Header("Guide Popup")]
        public GameObject guidePopup;
        public Button closeGuideButton;

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            SetupVisuals();
            if (questsPopup != null) questsPopup.SetActive(false);
            if (guidePopup != null) guidePopup.SetActive(false);
            if (cardGalleryView != null) cardGalleryView.gameObject.SetActive(false);
        }

        public void BindListeners()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    gameObject.SetActive(false);
                    GameFlowController.Instance?.TransitionToState(ServerGameState.LOBBY);
                });
            }

            if (galleryButton != null)
            {
                galleryButton.onClick.RemoveAllListeners();
                galleryButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (cardGalleryView != null)
                    {
                        cardGalleryView.PopulateCards();
                        cardGalleryView.BindListeners(() =>
                        {
                            cardGalleryView.gameObject.SetActive(false);
                            gameObject.SetActive(true);
                        });
                        gameObject.SetActive(false);
                        cardGalleryView.gameObject.SetActive(true);
                    }
                });
            }

            if (audioToggleButton != null)
            {
                audioToggleButton.onClick.RemoveAllListeners();
                audioToggleButton.onClick.AddListener(() =>
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.ToggleMute();
                        if (audioToggleText != null)
                        {
                            audioToggleText.text = AudioManager.Instance.IsMuted ? "🔇 TẮT TIẾNG" : "🔊 BẬT TIẾNG";
                        }
                    }
                });
            }

            if (questsButton != null)
            {
                questsButton.onClick.RemoveAllListeners();
                questsButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (questsPopup != null) questsPopup.SetActive(true);
                });
            }

            if (closeQuestsButton != null)
            {
                closeQuestsButton.onClick.RemoveAllListeners();
                closeQuestsButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (questsPopup != null) questsPopup.SetActive(false);
                });
            }

            if (guideButton != null)
            {
                guideButton.onClick.RemoveAllListeners();
                guideButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (guidePopup != null) guidePopup.SetActive(true);
                });
            }

            if (closeGuideButton != null)
            {
                closeGuideButton.onClick.RemoveAllListeners();
                closeGuideButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (guidePopup != null) guidePopup.SetActive(false);
                });
            }
        }

        public void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("UI/western_home_landscape");
                if (townSprite != null)
                {
                    backgroundImage.sprite = townSprite;
                    backgroundImage.color = Color.white;
                }
            }

            if (logoImage != null)
            {
                var logoSprite = CardCatalogDatabase.LoadSprite("bang_bang_logo");
                if (logoSprite != null)
                {
                    logoImage.sprite = logoSprite;
                    logoImage.preserveAspect = true;
                }
            }

            if (avatarImage != null)
            {
                var charSprite = CardCatalogDatabase.LoadSprite("Characters/willy_the_kid");
                if (charSprite != null) avatarImage.sprite = charSprite;
            }

            if (playerNameText != null) playerNameText.text = "Cao bồi của bạn";
            if (playerBountyText != null) playerBountyText.text = "Tiền thưởng: $15,000 🪙";
        }
    }
}

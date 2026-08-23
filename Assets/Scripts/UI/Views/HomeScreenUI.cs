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
        public Button profileButton;
        public GameObject profilePopup;
        public Button closeProfileButton;
        public Transform avatarOptionsContainer;

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
            if (profilePopup != null) profilePopup.SetActive(false);
            if (cardGalleryView != null) cardGalleryView.gameObject.SetActive(false);
            BuildAvatarOptions();
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
                            audioToggleText.text = AudioManager.Instance.IsMuted ? "ÂM THANH: TẮT" : "ÂM THANH: BẬT";
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

            if (profileButton != null)
            {
                profileButton.onClick.RemoveAllListeners();
                profileButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (profilePopup != null) profilePopup.SetActive(true);
                });
            }

            if (closeProfileButton != null)
            {
                closeProfileButton.onClick.RemoveAllListeners();
                closeProfileButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (profilePopup != null) profilePopup.SetActive(false);
                });
            }
        }

        private void BuildAvatarOptions()
        {
            if (avatarOptionsContainer == null) return;
            foreach (Transform child in avatarOptionsContainer) Destroy(child.gameObject);
            foreach (var sprite in AvatarCatalog.LoadAll())
            {
                var option = new GameObject("Avatar_" + sprite.name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                option.transform.SetParent(avatarOptionsContainer, false);
                option.GetComponent<RectTransform>().sizeDelta = new Vector2(108, 108);
                option.GetComponent<LayoutElement>().preferredWidth = 108;
                option.GetComponent<LayoutElement>().preferredHeight = 108;
                option.GetComponent<Image>().sprite = sprite;
                option.GetComponent<Image>().preserveAspect = true;
                string avatarId = sprite.name;
                option.GetComponent<Button>().onClick.AddListener(async () =>
                {
                    AvatarCatalog.SelectedId = avatarId;
                    if (avatarImage != null) avatarImage.sprite = AvatarCatalog.Load(avatarId);
                    if (GameStateStore.Instance?.Gateway is BangCloudflareGateway cloudflare)
                        await cloudflare.UpdateAvatarAsync(avatarId);
                    if (profilePopup != null) profilePopup.SetActive(false);
                });
            }
        }

        public void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("UI/LandscapeV2/home_v3");
                if (townSprite != null)
                {
                    backgroundImage.sprite = townSprite;
                    backgroundImage.color = Color.white;
                }
            }

            if (logoImage != null)
            {
                var logoSprite = CardCatalogDatabase.LoadSprite("UI/LandscapeV2/logo_v3");
                if (logoSprite != null)
                {
                    logoImage.sprite = logoSprite;
                    logoImage.preserveAspect = true;
                }
            }

            if (avatarImage != null)
            {
                var charSprite = AvatarCatalog.Load(AvatarCatalog.SelectedId);
                if (charSprite != null) avatarImage.sprite = charSprite;
            }

            if (playerNameText != null) playerNameText.text = "Cao bồi của bạn";
            if (playerBountyText != null) playerBountyText.text = "TÂN BINH  •  $15,000";
        }
    }
}

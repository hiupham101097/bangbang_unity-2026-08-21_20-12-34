using System.Collections;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Screens
{
    public class SplashScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image backgroundImage;
        public Image logoImage;
        public Image loadingProgressBar;
        public Text loadingStatusText;
        public Text versionText;

        private void Start()
        {
            SetupVisuals();
            StartCoroutine(LoadingSequenceCoroutine());
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null)
            {
                var townSprite = CardCatalogDatabase.LoadSprite("wild_west_town");
                if (townSprite != null) backgroundImage.sprite = townSprite;
                backgroundImage.color = new Color(0.65f, 0.65f, 0.65f); // Saloon atmosphere
            }

            if (logoImage != null)
            {
                var logoSprite = CardCatalogDatabase.LoadSprite("bang_bang_logo");
                if (logoSprite != null) logoImage.sprite = logoSprite;
            }

            if (versionText != null)
            {
                versionText.text = "v2.0.0 - Unity Mobile HD Edition";
            }
        }

        private IEnumerator LoadingSequenceCoroutine()
        {
            AudioManager.Instance?.PlaySFX("splash_intro");

            string[] tips = {
                "Đang nạp súng và đạn chì...",
                "Đang chuẩn bị ngựa chiến Mustang...",
                "Đang pha chế bia tươi tại quán rượu Saloon...",
                "Đang chia thẻ bài vai trò bí mật...",
                "Chào mừng cao bồi đến Miền Tây Hoang Dã!"
            };

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.deltaTime * 0.45f;
                if (loadingProgressBar != null) loadingProgressBar.fillAmount = Mathf.Clamp01(progress);

                int tipIndex = Mathf.Clamp((int)(progress * tips.Length), 0, tips.Length - 1);
                if (loadingStatusText != null)
                {
                    loadingStatusText.text = tips[tipIndex] + " (" + (int)(progress * 100f) + "%)";
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            // Transition to Home Screen
            ScreenManager.Instance?.SwitchToScreen(AppScreenState.Home);
        }
    }
}

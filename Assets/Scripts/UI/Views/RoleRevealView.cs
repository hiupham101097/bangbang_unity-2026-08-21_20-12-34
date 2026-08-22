using System;
using System.Collections;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class RoleRevealView : MonoBehaviour
    {
        [Header("Card UI")]
        public Image roleCardImage;
        public Text roleTitleText;
        public Text roleGoalText;
        public Button acknowledgeButton;
        public Text timerCountdownText;

        private bool _isCardFlipped;

        private void Awake()
        {
            if (acknowledgeButton != null)
            {
                acknowledgeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                });
            }
        }

        private void OnEnable()
        {
            _isCardFlipped = false;
            StartCoroutine(RoleRevealSequenceCoroutine());
        }

        private IEnumerator RoleRevealSequenceCoroutine()
        {
            AudioManager.Instance?.PlaySFX("card_draw");

            var local = GameStateStore.Instance?.LocalPlayer;
            string roleKey = local != null && !string.IsNullOrEmpty(local.role) ? local.role.ToLower() : "outlaw";

            // Start face down
            if (roleCardImage != null)
            {
                roleCardImage.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                roleCardImage.color = new Color(0.4f, 0.4f, 0.4f);
            }

            if (roleTitleText != null) roleTitleText.text = "CHẠM ĐỂ LẬT VAI TRÒ BÍ MẬT";
            if (roleGoalText != null) roleGoalText.text = "Vai trò của bạn được giữ bí mật (ngoại trừ Cảnh Sát Trưởng).";

            yield return new WaitForSeconds(1.0f);

            // Flip Card
            if (roleCardImage != null)
            {
                for (float t = 1f; t >= 0f; t -= Time.deltaTime * 6f)
                {
                    roleCardImage.transform.localScale = new Vector3(t, 1f, 1f);
                    yield return null;
                }

                var roleSprite = CardCatalogDatabase.LoadSprite("role_cards/" + roleKey + "_card");
                if (roleSprite != null) roleCardImage.sprite = roleSprite;
                roleCardImage.color = Color.white;

                for (float t = 0f; t <= 1f; t += Time.deltaTime * 6f)
                {
                    roleCardImage.transform.localScale = new Vector3(t, 1f, 1f);
                    yield return null;
                }
                roleCardImage.transform.localScale = Vector3.one;
            }

            _isCardFlipped = true;
            AudioManager.Instance?.PlaySFX("card_play");

            // Goal descriptions
            if (roleTitleText != null)
            {
                roleTitleText.text = roleKey == "sheriff" ? "⭐ CẢNH SÁT TRƯỞNG" :
                                    roleKey == "deputy" ? "🛡️ PHÓ CẢNH SÁT" :
                                    roleKey == "outlaw" ? "💀 NGOÀI VÒNG PHÁP LUẬT" : "🗡️ KẺ PHẢN BỘI";
                roleTitleText.color = roleKey == "sheriff" ? new Color(1f, 0.85f, 0.2f) :
                                      roleKey == "deputy" ? new Color(0.3f, 0.7f, 1f) :
                                      roleKey == "outlaw" ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.4f, 1f);
            }

            if (roleGoalText != null)
            {
                roleGoalText.text = roleKey == "sheriff" ? "Mục tiêu: Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội để bảo vệ thị trấn!" :
                                    roleKey == "deputy" ? "Mục tiêu: Bảo vệ Cảnh Sát Trưởng bằng mọi giá và tiêu diệt bọn Cướp!" :
                                    roleKey == "outlaw" ? "Mục tiêu: Tiêu diệt Cảnh Sát Trưởng để chiếm đoạt thị trấn!" :
                                    "Mục tiêu: Trở thành người sống sót cuối cùng và hạ gục Cảnh Sát Trưởng sau cùng!";
            }

            // 5s Countdown
            for (int sec = 5; sec >= 0; sec--)
            {
                if (timerCountdownText != null) timerCountdownText.text = "Tự động tiếp tục sau " + sec + "s...";
                yield return new WaitForSeconds(1.0f);
            }

            // Auto-advance if flow controller hasn't moved on yet
            if (GameFlowController.Instance != null &&
                GameFlowController.Instance.CurrentState == ServerGameState.DEALING_ROLES)
            {
                GameFlowController.Instance.TransitionToState(ServerGameState.SELECTING_CHARACTER);
            }
        }
    }
}

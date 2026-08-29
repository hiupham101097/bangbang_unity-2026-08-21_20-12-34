using System;
using System.Collections.Generic;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class GameOverUI : MonoBehaviour
    {
        public static GameOverUI Instance { get; private set; }

        [Header("UI References")]
        public GameObject panel;
        public Text winnerTitleText;
        public Text winnerDescText;
        public Image winnerBannerImage;
        public Button restartButton;

        public event Action OnRestartRequested;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (panel != null) panel.SetActive(false);
            if (restartButton != null)
                restartButton.onClick.AddListener(() => OnRestartRequested?.Invoke());
        }

        public void ShowGameOver(string winner, List<PlayerModel> players)
        {
            if (panel != null) UIAnimator.Instance.ShowModal(panel, 0.5f);

            if (winner == "sheriff")
            {
                if (winnerTitleText != null) winnerTitleText.text = "CẢNH TRƯỞNG CHIẾN THẮNG!";
                if (winnerDescText != null) winnerDescText.text = "Công lý đã được thực thi! Toàn bộ băng cướp và kẻ phản bội đã bị tiêu diệt.";
            }
            else if (winner == "outlaw")
            {
                if (winnerTitleText != null) winnerTitleText.text = "BĂNG CƯỚP CHIẾN THẮNG!";
                if (winnerDescText != null) winnerDescText.text = "Cảnh Trưởng đã gục ngã! Băng cướp miền Tây đã chiếm trọn thị trấn!";
            }
            else if (winner == "renegade")
            {
                if (winnerTitleText != null) winnerTitleText.text = "KẺ PHẢN BỘI CHIẾN THẮNG!";
                if (winnerDescText != null) winnerDescText.text = "Kế hoạch hoàn hảo! Kẻ phản bội là người duy nhất sống sót cuối cùng.";
            }
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}

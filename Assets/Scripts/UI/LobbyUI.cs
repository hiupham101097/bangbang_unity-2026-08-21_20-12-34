using System;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [Header("UI Components")]
        public GameObject lobbyPanel;
        public InputField playerNameInput;
        public InputField roomCodeInput;
        public Slider botCountSlider;
        public Text botCountText;
        public Button playOfflineButton;
        public Button createOnlineRoomButton;
        public Button joinOnlineRoomButton;

        public event Action<int, string> OnStartOfflineMatch; // botCount, playerName
        public event Action<string, string> OnJoinOnlineMatch; // roomCode, playerName

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (botCountSlider != null)
            {
                botCountSlider.minValue = 4;
                botCountSlider.maxValue = 7;
                botCountSlider.value = 5;
                botCountSlider.onValueChanged.AddListener((val) =>
                {
                    if (botCountText != null) botCountText.text = "Số người chơi: " + (int)val;
                });
            }

            if (playOfflineButton != null)
            {
                playOfflineButton.onClick.AddListener(() =>
                {
                    string pName = playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text) ? playerNameInput.text : "Cao bồi bạn";
                    int count = botCountSlider != null ? (int)botCountSlider.value : 5;
                    OnStartOfflineMatch?.Invoke(count, pName);
                    Hide();
                });
            }
        }

        public void Show()
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }

        public void Hide()
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
        }
    }
}

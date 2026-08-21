using System;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class PromptResponseModal : MonoBehaviour
    {
        public static PromptResponseModal Instance { get; private set; }

        [Header("UI Components")]
        public GameObject modalPanel;
        public Text promptTitleText;
        public Text promptDescText;
        public Image timerFillBar;
        public Button respondActionButton; // e.g. "Dùng NÉ" or "Đánh BANG"
        public Text actionButtonText;
        public Button takeDamageButton; // "Chịu Sát Thương (-1 HP)"

        private float _remainingDuration;
        private float _totalDuration = 15f;
        private Action<bool> _onResolvedCallback;
        private bool _isPromptActive;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (modalPanel != null) modalPanel.SetActive(false);

            if (respondActionButton != null)
                respondActionButton.onClick.AddListener(() => Resolve(true));

            if (takeDamageButton != null)
                takeDamageButton.onClick.AddListener(() => Resolve(false));
        }

        public void ShowPrompt(string title, string description, string actionText, bool hasRequiredCard, Action<bool> onResolved, float duration = 15f)
        {
            _onResolvedCallback = onResolved;
            _totalDuration = duration;
            _remainingDuration = duration;
            _isPromptActive = true;

            if (promptTitleText != null) promptTitleText.text = title;
            if (promptDescText != null) promptDescText.text = description;
            if (actionButtonText != null) actionButtonText.text = actionText;

            if (respondActionButton != null)
            {
                respondActionButton.interactable = hasRequiredCard;
            }

            if (modalPanel != null) modalPanel.SetActive(true);
        }

        private void Update()
        {
            if (_isPromptActive)
            {
                _remainingDuration -= Time.deltaTime;
                if (timerFillBar != null)
                {
                    timerFillBar.fillAmount = Mathf.Clamp01(_remainingDuration / _totalDuration);
                }

                if (_remainingDuration <= 0)
                {
                    Resolve(false); // timeout -> take damage
                }
            }
        }

        private void Resolve(bool useAction)
        {
            if (!_isPromptActive) return;
            _isPromptActive = false;
            if (modalPanel != null) modalPanel.SetActive(false);
            _onResolvedCallback?.Invoke(useAction);
        }
    }
}

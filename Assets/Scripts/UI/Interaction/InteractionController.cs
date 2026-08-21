using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Interaction
{
    public class InteractionController : MonoBehaviour
    {
        public static InteractionController Instance { get; private set; }

        [Header("Unified Modal UI Components")]
        public GameObject modalRootPanel;
        public Text titleText;
        public Text messageText;
        public Text timerText;
        public Transform optionsContainer;
        public Button confirmButton;
        public Text confirmButtonText;
        public Button cancelButton;
        public GameObject loadingSpinnerObj;

        private InteractionPromptDTO _currentPrompt;
        private readonly List<string> _selectedCardIds = new List<string>();
        private readonly List<string> _selectedPlayerIds = new List<string>();
        private int _selectedOptionIndex;

        public event Action<string, List<string>, List<string>, int> OnInteractionSubmitted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnActiveInteractionChanged += HandleInteractionChanged;
                GameStateStore.Instance.OnRequestPendingChanged += HandleRequestPendingChanged;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(SubmitCurrentInteraction);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(CancelCurrentInteraction);
            }

            HideModal();
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnActiveInteractionChanged -= HandleInteractionChanged;
                GameStateStore.Instance.OnRequestPendingChanged -= HandleRequestPendingChanged;
            }
        }

        private void Update()
        {
            if (_currentPrompt != null && _currentPrompt.expiresAt > 0 && timerText != null)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long remainingMs = _currentPrompt.expiresAt - now;
                int remainingSec = Mathf.Max(0, (int)(remainingMs / 1000));
                timerText.text = "⌛ " + remainingSec + "s";

                if (remainingSec <= 0 && GameStateStore.Instance != null && !GameStateStore.Instance.IsRequestPending)
                {
                    SubmitDefaultAutoAction();
                }
            }
        }

        private void HandleInteractionChanged(InteractionPromptDTO prompt)
        {
            _currentPrompt = prompt;
            if (prompt == null)
            {
                HideModal();
                return;
            }

            // Only show if directed to local player
            string localId = GameStateStore.Instance != null ? GameStateStore.Instance.LocalPlayerId : "";
            if (!string.IsNullOrEmpty(prompt.actorPlayerId) && prompt.actorPlayerId != localId)
            {
                HideModal();
                return;
            }

            ShowModal(prompt);
        }

        private void HandleRequestPendingChanged(bool pending)
        {
            if (loadingSpinnerObj != null) loadingSpinnerObj.SetActive(pending);
            if (confirmButton != null) confirmButton.interactable = !pending;
            if (cancelButton != null) cancelButton.interactable = !pending;
        }

        public void ShowModal(InteractionPromptDTO prompt)
        {
            _selectedCardIds.Clear();
            _selectedPlayerIds.Clear();
            _selectedOptionIndex = 0;

            if (modalRootPanel != null) modalRootPanel.SetActive(true);
            if (titleText != null) titleText.text = prompt.title;
            if (messageText != null) messageText.text = prompt.message;
            if (cancelButton != null) cancelButton.gameObject.SetActive(prompt.canCancel);

            PopulateOptions(prompt);
            UpdateConfirmButtonState();
        }

        public void HideModal()
        {
            if (modalRootPanel != null) modalRootPanel.SetActive(false);
            _currentPrompt = null;
        }

        private void PopulateOptions(InteractionPromptDTO prompt)
        {
            if (optionsContainer == null) return;
            foreach (Transform child in optionsContainer) Destroy(child.gameObject);

            if (prompt.options != null && prompt.options.Count > 0)
            {
                for (int i = 0; i < prompt.options.Count; i++)
                {
                    int optIdx = i;
                    string optName = prompt.options[i];

                    var btnObj = new GameObject("Opt_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    btnObj.transform.SetParent(optionsContainer, false);
                    var rt = btnObj.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(260, 55);

                    var img = btnObj.GetComponent<Image>();
                    img.color = new Color(0.2f, 0.45f, 0.25f);

                    var txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    txtObj.transform.SetParent(btnObj.transform, false);
                    var txtRt = txtObj.GetComponent<RectTransform>();
                    txtRt.anchorMin = Vector2.zero;
                    txtRt.anchorMax = Vector2.one;
                    var txt = txtObj.GetComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 16;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.text = optName.ToUpper();

                    var btn = btnObj.GetComponent<Button>();
                    btn.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("button_tap");
                        _selectedOptionIndex = optIdx;
                        SubmitCurrentInteraction();
                    });
                }
            }
        }

        public void SelectCard(string cardId)
        {
            if (_currentPrompt == null) return;

            if (_selectedCardIds.Contains(cardId))
            {
                _selectedCardIds.Remove(cardId);
            }
            else
            {
                if (_selectedCardIds.Count < _currentPrompt.maxSelections)
                {
                    _selectedCardIds.Add(cardId);
                }
            }

            UpdateConfirmButtonState();
        }

        public void SelectPlayer(string targetPlayerId)
        {
            if (_currentPrompt == null) return;

            if (_selectedPlayerIds.Contains(targetPlayerId))
            {
                _selectedPlayerIds.Remove(targetPlayerId);
            }
            else
            {
                if (_selectedPlayerIds.Count < _currentPrompt.maxSelections)
                {
                    _selectedPlayerIds.Add(targetPlayerId);
                }
            }

            UpdateConfirmButtonState();
        }

        private void UpdateConfirmButtonState()
        {
            if (confirmButton == null || _currentPrompt == null) return;

            bool isValid = true;
            if (_currentPrompt.type == "SELECT_CARDS" || _currentPrompt.type == "DISCARD")
            {
                isValid = _selectedCardIds.Count >= _currentPrompt.minSelections && _selectedCardIds.Count <= _currentPrompt.maxSelections;
                if (confirmButtonText != null)
                {
                    confirmButtonText.text = "XÁC NHẬN (" + _selectedCardIds.Count + "/" + _currentPrompt.maxSelections + ")";
                }
            }
            else if (_currentPrompt.type == "SELECT_TARGET")
            {
                isValid = _selectedPlayerIds.Count >= _currentPrompt.minSelections;
            }

            confirmButton.interactable = isValid && !(GameStateStore.Instance != null && GameStateStore.Instance.IsRequestPending);
        }

        private void SubmitCurrentInteraction()
        {
            if (_currentPrompt == null) return;
            GameStateStore.Instance?.SetRequestPending(true);

            OnInteractionSubmitted?.Invoke(_currentPrompt.interactionId, _selectedPlayerIds, _selectedCardIds, _selectedOptionIndex);
            GameStateStore.Instance?.Gateway?.SubmitInteractionAsync(_currentPrompt.interactionId, "SUBMIT", _selectedPlayerIds, _selectedCardIds, _selectedOptionIndex);
            HideModal();
        }

        private void CancelCurrentInteraction()
        {
            if (_currentPrompt == null) return;
            GameStateStore.Instance?.Gateway?.SubmitInteractionAsync(_currentPrompt.interactionId, "CANCEL");
            HideModal();
        }

        private void SubmitDefaultAutoAction()
        {
            if (_currentPrompt == null) return;
            GameStateStore.Instance?.Gateway?.SubmitInteractionAsync(_currentPrompt.interactionId, _currentPrompt.defaultAction ?? "AUTO");
            HideModal();
        }
    }
}

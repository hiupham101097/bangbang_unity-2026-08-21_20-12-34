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

            if (prompt.validCardIds != null)
            {
                foreach (string cardId in prompt.validCardIds)
                {
                    var info = CardCatalogDatabase.GetCardInfo(cardId);
                    var cardButton = CreateOptionButton("Card_" + cardId, info.name, new Color(0.55f, 0.32f, 0.12f));
                    cardButton.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("button_tap");
                        SelectCard(cardId);
                        var image = cardButton.GetComponent<Image>();
                        if (image != null) image.color = _selectedCardIds.Contains(cardId) ? new Color(0.85f, 0.62f, 0.16f) : new Color(0.55f, 0.32f, 0.12f);
                    });
                }
            }

            if (prompt.validPlayerIds != null)
            {
                foreach (string playerId in prompt.validPlayerIds)
                {
                    string capturedId = playerId;
                    var snapshot = GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null;
                    var player = snapshot != null ? snapshot.players.Find(p => p.id == capturedId) : null;
                    var playerButton = CreateOptionButton("Player_" + capturedId, player != null ? player.name : capturedId, new Color(0.18f, 0.38f, 0.55f));
                    playerButton.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("button_tap");
                        SelectPlayer(capturedId);
                        var image = playerButton.GetComponent<Image>();
                        if (image != null) image.color = _selectedPlayerIds.Contains(capturedId) ? new Color(0.85f, 0.62f, 0.16f) : new Color(0.18f, 0.38f, 0.55f);
                    });
                }
            }

            if (prompt.options != null && prompt.options.Count > 0)
            {
                for (int i = 0; i < prompt.options.Count; i++)
                {
                    int optIdx = i;
                    string optName = prompt.options[i];

                    var btn = CreateOptionButton("Opt_" + i, optName.ToUpperInvariant(), optName == "PASS" ? new Color(0.45f, 0.18f, 0.16f) : new Color(0.2f, 0.45f, 0.25f));
                    btn.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("button_tap");
                        _selectedOptionIndex = optIdx;
                        SubmitCurrentInteraction();
                    });
                }
            }
        }

        private Button CreateOptionButton(string objectName, string label, Color color)
        {
            var btnObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(optionsContainer, false);
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 58);
            btnObj.GetComponent<Image>().color = color;
            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>(); txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.sizeDelta = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); txt.fontSize = 18; txt.fontStyle = FontStyle.Bold; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white; txt.text = label; txt.raycastTarget = false;
            return btnObj.GetComponent<Button>();
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
            if (_currentPrompt.type == "SELECT_CARDS" || _currentPrompt.type == "DISCARD" || _currentPrompt.type == "RESPOND" || _currentPrompt.type == "DUEL" || _currentPrompt.type == "CHOOSE_CARD")
            {
                isValid = _selectedCardIds.Count >= _currentPrompt.minSelections && _selectedCardIds.Count <= _currentPrompt.maxSelections;
                if ((_currentPrompt.type == "RESPOND" || _currentPrompt.type == "DUEL") && _selectedCardIds.Count != _currentPrompt.requiredCount) isValid = false;
                if (confirmButtonText != null)
                {
                    confirmButtonText.text = "XÁC NHẬN (" + _selectedCardIds.Count + "/" + _currentPrompt.maxSelections + ")";
                }
            }
            else if (_currentPrompt.type == "SELECT_TARGET")
            {
                isValid = _selectedPlayerIds.Count >= _currentPrompt.minSelections;
            }
            else if (_currentPrompt.type == "CHOOSE_OPTION")
            {
                isValid = true;
            }

            confirmButton.interactable = isValid && !(GameStateStore.Instance != null && GameStateStore.Instance.IsRequestPending);
        }

        private async void SubmitCurrentInteraction()
        {
            if (_currentPrompt == null) return;
            string interactionId = _currentPrompt.interactionId;
            var players = new List<string>(_selectedPlayerIds);
            var cards = new List<string>(_selectedCardIds);
            int optionIndex = _selectedOptionIndex;
            GameStateStore.Instance?.SetRequestPending(true);

            OnInteractionSubmitted?.Invoke(interactionId, players, cards, optionIndex);
            HideModal();
            bool ok = GameStateStore.Instance?.Gateway != null && await GameStateStore.Instance.Gateway.SubmitInteractionAsync(interactionId, "SUBMIT", players, cards, optionIndex);
            if (!ok) GameStateStore.Instance?.SetRequestPending(false);
        }

        private async void CancelCurrentInteraction()
        {
            if (_currentPrompt == null) return;
            string interactionId = _currentPrompt.interactionId;
            HideModal();
            bool ok = GameStateStore.Instance?.Gateway != null && await GameStateStore.Instance.Gateway.SubmitInteractionAsync(interactionId, "CANCEL");
            if (!ok) GameStateStore.Instance?.SetRequestPending(false);
        }

        private async void SubmitDefaultAutoAction()
        {
            if (_currentPrompt == null) return;
            string interactionId = _currentPrompt.interactionId;
            string defaultAction = _currentPrompt.defaultAction ?? "AUTO";
            HideModal();
            bool ok = GameStateStore.Instance?.Gateway != null && await GameStateStore.Instance.Gateway.SubmitInteractionAsync(interactionId, defaultAction);
            if (!ok) GameStateStore.Instance?.SetRequestPending(false);
        }
    }
}

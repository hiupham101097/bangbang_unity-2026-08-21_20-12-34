using System.Collections.Generic;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;

namespace BangBang.UI
{
    public sealed class ChatOverlay : MonoBehaviour
    {
        private GameObject _panel;
        private UnityEngine.UI.Text _log;
        private UnityEngine.UI.InputField _input;
        private UnityEngine.UI.Button _toggleButton;
        private UnityEngine.UI.Text _toggleText;
        private readonly Queue<string> _lines = new Queue<string>();
        private bool _isOpen;
        private int _unread;
        private bool _interactionBlocking;

        private void Start()
        {
            BuildUi();
            var store = GameStateStore.Instance;
            if (store?.Gateway != null) store.Gateway.OnChatMessage += HandleMessage;
            if (store != null) store.OnActiveInteractionChanged += HandleInteraction;
            if (store != null) store.OnStateSnapshotUpdated += HandleSnapshot;
            SetOpen(false);
        }

        private void OnDestroy()
        {
            var store = GameStateStore.Instance;
            if (store?.Gateway != null) store.Gateway.OnChatMessage -= HandleMessage;
            if (store != null) store.OnActiveInteractionChanged -= HandleInteraction;
            if (store != null) store.OnStateSnapshotUpdated -= HandleSnapshot;
        }

        private void BuildUi()
        {
            var toggle = new GameObject("ChatToggle", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            toggle.transform.SetParent(transform, false);
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 0f); toggleRect.anchorMax = new Vector2(1f, 0f); toggleRect.pivot = new Vector2(1f, 0f);
            toggleRect.anchoredPosition = new Vector2(-18f, 18f); toggleRect.sizeDelta = new Vector2(150f, 52f);
            toggle.GetComponent<UnityEngine.UI.Image>().color = new Color(0.14f, 0.09f, 0.05f, 0.96f);
            _toggleButton = toggle.GetComponent<UnityEngine.UI.Button>();
            _toggleText = CreateText("Text", toggle.transform, Vector2.zero, new Vector2(142, 48), 16);
            _toggleButton.onClick.AddListener(() => SetOpen(!_isOpen));

            _panel = new GameObject("ChatPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _panel.transform.SetParent(transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f, 82f); rect.sizeDelta = new Vector2(420f, 280f);
            _panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.03f, 0.025f, 0.92f);

            _log = CreateText("Messages", _panel.transform, new Vector2(0, 32), new Vector2(390, 190), 17);
            _log.alignment = TextAnchor.LowerLeft;
            _input = new GameObject("Input", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.InputField)).GetComponent<UnityEngine.UI.InputField>();
            _input.transform.SetParent(_panel.transform, false);
            _input.GetComponent<RectTransform>().anchoredPosition = new Vector2(-42, -100); _input.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 52);
            _input.GetComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.12f, 0.09f, 1f);
            _input.textComponent = CreateText("Text", _input.transform, Vector2.zero, new Vector2(280, 48), 17);
            _input.characterLimit = 240;
            _input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            _input.onEndEdit.AddListener(value => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Send(); });
            _input.placeholder = CreateText("Placeholder", _input.transform, Vector2.zero, new Vector2(280, 48), 17);
            ((UnityEngine.UI.Text)_input.placeholder).text = "Nhập tin nhắn…";

            var button = new GameObject("Send", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(_panel.transform, false);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(160, -100); button.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 52);
            button.GetComponent<UnityEngine.UI.Image>().color = new Color(0.58f, 0.38f, 0.12f);
            CreateText("Text", button.transform, Vector2.zero, new Vector2(86, 48), 17).text = "GỬI";
            button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Send);
        }

        private UnityEngine.UI.Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            text.transform.SetParent(parent, false); text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white;
            return text;
        }

        private async void Send()
        {
            string message = _input.text.Trim();
            if (message.Length == 0) return;
            _input.text = string.Empty;
            if (GameStateStore.Instance?.Gateway != null)
            {
                bool sent = await GameStateStore.Instance.Gateway.SendChatAsync(message);
                if (!sent) _input.text = message;
                else _input.ActivateInputField();
            }
        }

        private void HandleMessage(ChatMessageDTO message)
        {
            if (message == null) return;
            _lines.Enqueue($"{message.playerName}: {message.message}");
            while (_lines.Count > 8) _lines.Dequeue();
            _log.text = string.Join("\n", _lines);
            if (!_isOpen) { _unread++; RefreshToggle(); }
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;
            if (open) _unread = 0;
            if (_panel != null) _panel.SetActive(open && !_interactionBlocking);
            RefreshToggle();
            if (open && _input != null) _input.ActivateInputField();
        }

        private void RefreshToggle()
        {
            if (_toggleText != null) _toggleText.text = _isOpen ? "ĐÓNG CHAT" : (_unread > 0 ? "CHAT (" + _unread + ")" : "CHAT");
        }

        private void HandleInteraction(InteractionPromptDTO prompt)
        {
            _interactionBlocking = prompt != null;
            if (_panel != null) _panel.SetActive(_isOpen && !_interactionBlocking);
        }

        private void HandleSnapshot(MatchStateSnapshotDTO snapshot)
        {
            if (_toggleButton != null) _toggleButton.gameObject.SetActive(snapshot != null && snapshot.state != ServerGameState.LOBBY);
            _interactionBlocking = snapshot != null && snapshot.activeInteraction != null;
            if (_panel != null) _panel.SetActive(_isOpen && !_interactionBlocking);
        }
    }
}

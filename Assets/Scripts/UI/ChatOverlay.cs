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
        private readonly Queue<string> _lines = new Queue<string>();

        private void Start()
        {
            BuildUi();
            var store = GameStateStore.Instance;
            if (store?.Gateway != null) store.Gateway.OnChatMessage += HandleMessage;
            if (store != null) store.OnActiveInteractionChanged += HandleInteraction;
            if (store != null) store.OnStateSnapshotUpdated += HandleSnapshot;
            _panel.SetActive(false);
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
            _panel = new GameObject("ChatPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _panel.transform.SetParent(transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f, 18f); rect.sizeDelta = new Vector2(420f, 260f);
            _panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.03f, 0.025f, 0.92f);

            _log = CreateText("Messages", _panel.transform, new Vector2(0, 32), new Vector2(390, 190), 15);
            _log.alignment = TextAnchor.LowerLeft;
            _input = new GameObject("Input", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.InputField)).GetComponent<UnityEngine.UI.InputField>();
            _input.transform.SetParent(_panel.transform, false);
            _input.GetComponent<RectTransform>().anchoredPosition = new Vector2(-42, -100); _input.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 42);
            _input.GetComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.12f, 0.09f, 1f);
            _input.textComponent = CreateText("Text", _input.transform, Vector2.zero, new Vector2(280, 38), 15);
            _input.placeholder = CreateText("Placeholder", _input.transform, Vector2.zero, new Vector2(280, 38), 15);
            ((UnityEngine.UI.Text)_input.placeholder).text = "Nhập tin nhắn…";

            var button = new GameObject("Send", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(_panel.transform, false);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(160, -100); button.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 42);
            button.GetComponent<UnityEngine.UI.Image>().color = new Color(0.58f, 0.38f, 0.12f);
            CreateText("Text", button.transform, Vector2.zero, new Vector2(76, 40), 14).text = "GỬI";
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
            if (GameStateStore.Instance?.Gateway != null) await GameStateStore.Instance.Gateway.SendChatAsync(message);
        }

        private void HandleMessage(ChatMessageDTO message)
        {
            if (message == null) return;
            _lines.Enqueue($"{message.playerName}: {message.message}");
            while (_lines.Count > 8) _lines.Dequeue();
            _log.text = string.Join("\n", _lines);
        }

        private void HandleInteraction(InteractionPromptDTO prompt) { if (_panel != null) _panel.SetActive(prompt == null); }
        private void HandleSnapshot(MatchStateSnapshotDTO snapshot) { if (_panel != null && snapshot != null) _panel.SetActive(snapshot.state != ServerGameState.LOBBY && snapshot.activeInteraction == null); }
    }
}

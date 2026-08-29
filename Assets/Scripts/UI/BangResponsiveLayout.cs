using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    /// <summary>
    /// Keeps the runtime-built landscape UI readable from 1280x720 through
    /// ultrawide and tablet displays without changing gameplay coordinates.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class BangResponsiveLayout : MonoBehaviour
    {
        private Vector2Int _lastScreenSize;

        private void Awake() => Apply(true);
        private void Start() => Apply(true);

        private void Update()
        {
            var current = new Vector2Int(Screen.width, Screen.height);
            if (current != _lastScreenSize) Apply(true);
        }

        public void Apply(bool restyleChildren)
        {
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            scaler.matchWidthOrHeight = aspect >= 2f ? 1f : 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            if (!restyleChildren) return;
            foreach (var text in GetComponentsInChildren<Text>(true)) ImproveText(text);
            foreach (var button in GetComponentsInChildren<Button>(true)) ImproveButton(button);
        }

        private static void ImproveText(Text text)
        {
            bool isButton = text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null;
            bool isHeading = text.name.Contains("Title") || text.name.Contains("Header") ||
                             text.name.Contains("TurnPhase") || text.name.Contains("Winner");
            int minimum = isHeading ? 28 : isButton ? 22 : 18;
            text.fontSize = Mathf.Max(text.fontSize, minimum);
            text.raycastTarget = false;
        }

        private static void ImproveButton(Button button)
        {
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return;
            var size = rect.sizeDelta;
            if (rect.anchorMin.y == rect.anchorMax.y) size.y = Mathf.Max(size.y, 72f);
            if (rect.anchorMin.x == rect.anchorMax.x) size.x = Mathf.Max(size.x, 140f);
            rect.sizeDelta = size;
        }
    }
}

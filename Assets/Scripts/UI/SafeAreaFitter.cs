using UnityEngine;

namespace BangBang.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreen;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            _rectTransform.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _rectTransform.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}

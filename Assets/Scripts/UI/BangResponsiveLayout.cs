using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    /// <summary>
    /// Keeps the runtime-built landscape UI perfectly scaled for all devices 
    /// (ultrawide, tablet, etc.) without changing gameplay coordinates and without
    /// buggy Update loops that can cause UI to shrink or explode.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class BangResponsiveLayout : MonoBehaviour
    {
        // Keep controls and text readable on the small landscape viewport used by
        // the WebGL/mobile shell. Screens authored at 1920x1080 compact their
        // spacing separately instead of shrinking the entire UI to unreadable size.
        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        private void Awake()
        {
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            
            // Expand preserves the full landscape canvas on unusual aspect ratios.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
        }
    }

    /// <summary>
    /// Compacts whitespace from legacy 1920x1080 screens into the 1280x720 runtime
    /// canvas while keeping controls and typography at a readable size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BangCompactScreenLayout : MonoBehaviour
    {
        private const float PositionScale = 2f / 3f;
        private const float LargeContainerThreshold = 640f;
        private bool _applied;

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            if (_applied) return;
            _applied = true;

            CompactChildren(transform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private static void CompactChildren(Transform parent)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                var rect = child as RectTransform;
                if (rect == null) continue;

                if (rect.anchorMin == rect.anchorMax)
                {
                    rect.anchoredPosition *= PositionScale;

                    // Large panels carry the old screen dimensions. Compact those,
                    // but preserve actual controls and text so their tap/read size grows.
                    bool isControl = child.GetComponent<Selectable>() != null ||
                                     child.GetComponent<Text>() != null;
                    if (!isControl)
                    {
                        Vector2 size = rect.sizeDelta;
                        if (Mathf.Abs(size.x) > LargeContainerThreshold) size.x *= PositionScale;
                        if (Mathf.Abs(size.y) > LargeContainerThreshold) size.y *= PositionScale;
                        rect.sizeDelta = size;
                    }
                }

                CompactChildren(child);
            }
        }
    }
}

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

}

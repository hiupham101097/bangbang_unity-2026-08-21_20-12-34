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
        private void Awake()
        {
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            
            // Expand mode automatically ensures the UI is never cropped.
            // On ultrawide (e.g., 21:9), it scales to height (720) and expands width (e.g., 1680).
            // On tablets (e.g., 4:3), it scales to width (1280) and expands height (e.g., 960).
            // This is the native, mathematically correct way for landscape games.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
        }
    }
}

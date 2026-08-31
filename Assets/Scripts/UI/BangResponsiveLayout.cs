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
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private void Awake()
        {
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            
            // Expand mode automatically ensures the UI is never cropped.
            // The runtime UI was authored in a 1920x1080 coordinate space. Expand keeps
            // that entire design visible and only adds extra canvas on unusual aspects.
            // This is the native, mathematically correct way for landscape games.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
        }
    }
}

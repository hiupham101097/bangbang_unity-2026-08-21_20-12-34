using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    /// <summary>
    /// Applies the shared mobile visual language to the runtime-built uGUI hierarchy.
    /// It intentionally styles existing objects in place so serialized view references stay valid.
    /// </summary>
    public sealed class BangUITheme : MonoBehaviour
    {
        public static readonly Color Ink = new Color32(24, 19, 17, 255);
        public static readonly Color Surface = new Color32(42, 32, 28, 242);
        public static readonly Color SurfaceRaised = new Color32(61, 45, 37, 250);
        public static readonly Color Brass = new Color32(218, 166, 74, 255);
        public static readonly Color BrassPressed = new Color32(176, 126, 48, 255);
        public static readonly Color Ivory = new Color32(247, 236, 210, 255);
        public static readonly Color Muted = new Color32(190, 174, 151, 255);
        public static readonly Color Success = new Color32(67, 169, 119, 255);
        public static readonly Color Danger = new Color32(186, 68, 57, 255);
        public static readonly Color Scrim = new Color32(10, 8, 7, 205);

        private static Sprite _runtimeRoundedSprite;
        public static Sprite RoundedSprite
        {
            get
            {
                if (_runtimeRoundedSprite == null) _runtimeRoundedSprite = BuildRoundedSprite();
                return _runtimeRoundedSprite;
            }
        }

        [SerializeField] private Sprite roundedPanelSprite;
        private bool _applied;

        private void Start() => Apply();

        public void Apply()
        {
            if (_applied) return;
            _applied = true;

            foreach (var button in GetComponentsInChildren<Button>(true)) StyleButton(button);
            foreach (var text in GetComponentsInChildren<Text>(true)) StyleText(text);
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = image.GetComponent<Selectable>() != null || image.GetComponent<Mask>() != null;
            }
        }

        private void StyleButton(Button button)
        {
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = roundedPanelSprite != null ? roundedPanelSprite : RoundedSprite;
                image.type = Image.Type.Sliced;

                bool destructive = button.name.Contains("Leave") || button.name.Contains("Cancel");
                bool primary = button.name.Contains("Start") || button.name.Contains("Confirm") || button.name.Contains("PlayCard");
                image.color = destructive ? Danger : primary ? Brass : SurfaceRaised;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.43f, 0.4f, 0.55f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 64f;
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 64;
            const float radius = 14f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BangUI_RoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - ((size - 1) * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - ((size - 1) * 0.5f - radius), 0f);
                    float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            sprite.name = "BangUI_RoundedRect";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void StyleText(Text text)
        {
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            if (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null)
            {
                string buttonName = text.transform.parent.name;
                bool primary = buttonName.Contains("Start") || buttonName.Contains("Confirm") || buttonName.Contains("CreateRoom");
                text.color = primary ? Ink : Ivory;
                text.fontStyle = FontStyle.Bold;
                text.fontSize = Mathf.Max(text.fontSize, 20);
                text.raycastTarget = false;
            }
            else if (text.name.Contains("Header") || text.name.Contains("Title") || text.name.Contains("TurnPhase"))
            {
                text.color = Ivory;
                text.fontStyle = FontStyle.Bold;
            }
            else if (text.color.grayscale > 0.85f)
            {
                text.color = Ivory;
            }
        }
    }
}

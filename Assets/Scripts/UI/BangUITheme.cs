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
                if (roundedPanelSprite != null)
                {
                    image.sprite = roundedPanelSprite;
                    image.type = Image.Type.Sliced;
                }

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
            layout.minHeight = 52f;
        }

        private static void StyleText(Text text)
        {
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            if (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null)
            {
                text.color = Ink;
                text.fontStyle = FontStyle.Bold;
                text.fontSize = Mathf.Max(text.fontSize, 16);
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

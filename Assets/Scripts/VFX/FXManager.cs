using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.VFX
{
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        [Header("Canvas Reference")]
        public Canvas mainCanvas;

        private GameObject _tracerLineObj;
        private RectTransform _tracerLineRt;
        private Image _tracerLineImg;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitTracerLine();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitTracerLine()
        {
            if (mainCanvas == null) mainCanvas = FindAnyObjectByType<Canvas>();

            _tracerLineObj = new GameObject("TargetingTracerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _tracerLineObj.transform.SetParent(mainCanvas != null ? mainCanvas.transform : transform, false);
            _tracerLineRt = _tracerLineObj.GetComponent<RectTransform>();
            _tracerLineImg = _tracerLineObj.GetComponent<Image>();
            _tracerLineImg.color = new Color(1f, 0.2f, 0.2f, 0.75f);
            _tracerLineRt.pivot = new Vector2(0, 0.5f);
            _tracerLineObj.SetActive(false);
        }

        public void DrawTargetingLine(Vector2 fromScreenPos, Vector2 toScreenPos)
        {
            if (_tracerLineObj == null) InitTracerLine();
            if (mainCanvas == null) return;

            Vector2 fromLocal, toLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, fromScreenPos, null, out fromLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, toScreenPos, null, out toLocal);

            Vector2 diff = toLocal - fromLocal;
            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            _tracerLineRt.anchoredPosition = fromLocal;
            _tracerLineRt.sizeDelta = new Vector2(distance, 6f);
            _tracerLineRt.localRotation = Quaternion.Euler(0, 0, angle);
            _tracerLineObj.SetActive(true);
        }

        public void HideTargetingLine()
        {
            if (_tracerLineObj != null) _tracerLineObj.SetActive(false);
        }

        public void SpawnFloatingText(Vector2 screenPosition, string text, Color color)
        {
            StartCoroutine(FloatingTextCoroutine(screenPosition, text, color));
        }

        private IEnumerator FloatingTextCoroutine(Vector2 startPos, string text, Color color)
        {
            if (mainCanvas == null) mainCanvas = FindAnyObjectByType<Canvas>();

            var textObj = new GameObject("FloatingText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(mainCanvas != null ? mainCanvas.transform : transform, false);

            var rt = textObj.GetComponent<RectTransform>();
            rt.anchoredPosition = startPos;
            rt.sizeDelta = new Vector2(280, 70);

            var txt = textObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = text;

            var outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            float elapsed = 0f;
            float duration = 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = startPos + new Vector2(0, t * 90f);
                txt.color = new Color(color.r, color.g, color.b, 1f - (t * t));
                yield return null;
            }

            Destroy(textObj);
        }

        public void TriggerScreenShake(float intensity = 8f, float duration = 0.25f)
        {
            StartCoroutine(ScreenShakeCoroutine(intensity, duration));
        }

        private IEnumerator ScreenShakeCoroutine(float intensity, float duration)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Vector3 originalPos = cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = (Random.value - 0.5f) * intensity * 0.1f;
                float y = (Random.value - 0.5f) * intensity * 0.1f;
                cam.transform.position = originalPos + new Vector3(x, y, 0);
                yield return null;
            }

            cam.transform.position = originalPos;
        }
    }
}

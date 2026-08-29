using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class UIAnimator : MonoBehaviour
    {
        private static UIAnimator _instance;
        public static UIAnimator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("UIAnimator");
                    _instance = go.AddComponent<UIAnimator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<GameObject, Coroutine> _activeAnimations = new Dictionary<GameObject, Coroutine>();

        // ─────────────────────────────────────────────────────────────────
        // MODAL ANIMATIONS
        // ─────────────────────────────────────────────────────────────────

        public void ShowModal(GameObject panel, float duration = 0.3f)
        {
            if (panel == null) return;

            if (_activeAnimations.TryGetValue(panel, out Coroutine existing))
            {
                if (existing != null) StopCoroutine(existing);
                _activeAnimations.Remove(panel);
            }

            _activeAnimations[panel] = StartCoroutine(PopInRoutine(panel, duration));
        }

        public void HideModal(GameObject panel, float duration = 0.15f)
        {
            if (panel == null || !panel.activeSelf) return;

            if (_activeAnimations.TryGetValue(panel, out Coroutine existing))
            {
                if (existing != null) StopCoroutine(existing);
                _activeAnimations.Remove(panel);
            }

            _activeAnimations[panel] = StartCoroutine(PopOutRoutine(panel, duration));
        }

        private IEnumerator PopInRoutine(GameObject panel, float duration)
        {
            panel.SetActive(true);
            Transform target = panel.transform;
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();

            float time = 0f;
            target.localScale = Vector3.one * 0.7f;
            cg.alpha = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);

                float alphaT = 1f - Mathf.Pow(1f - t, 3f);
                cg.alpha = alphaT;

                float s = 1.70158f;
                float t1 = t - 1f;
                float scaleT = t1 * t1 * ((s + 1f) * t1 + s) + 1f;

                target.localScale = Vector3.one * scaleT;
                yield return null;
            }

            target.localScale = Vector3.one;
            cg.alpha = 1f;
            _activeAnimations.Remove(panel);
        }

        private IEnumerator PopOutRoutine(GameObject panel, float duration)
        {
            Transform target = panel.transform;
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();

            float time = 0f;
            Vector3 startScale = target.localScale;
            float startAlpha = cg.alpha;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float easeInT = t * t;

                cg.alpha = Mathf.Lerp(startAlpha, 0f, easeInT);
                target.localScale = Vector3.Lerp(startScale, Vector3.one * 0.7f, easeInT);
                yield return null;
            }

            target.localScale = Vector3.one * 0.7f;
            cg.alpha = 0f;
            panel.SetActive(false);
            _activeAnimations.Remove(panel);
        }

        // ─────────────────────────────────────────────────────────────────
        // CARD PLAY ANIMATIONS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ném lá bài từ vị trí nguồn lên trung tâm bàn theo cung arc.
        /// Dùng cho mọi lá bài trước khi gửi lên server.
        /// </summary>
        public void PlayCardThrowAnimation(RectTransform cardRect, Vector2 targetCanvasPos, Canvas canvas, Action onComplete)
        {
            StartCoroutine(CardThrowRoutine(cardRect, targetCanvasPos, canvas, onComplete));
        }

        private IEnumerator CardThrowRoutine(RectTransform cardRect, Vector2 targetPos, Canvas canvas, Action onComplete)
        {
            if (cardRect == null) { onComplete?.Invoke(); yield break; }

            float duration = 0.32f;
            float time = 0f;
            Vector2 startPos = cardRect.anchoredPosition;
            Vector3 startScale = cardRect.localScale;
            Quaternion startRot = cardRect.localRotation;

            // Arc midpoint — lift up by 120 units
            Vector2 midPos = Vector2.Lerp(startPos, targetPos, 0.5f) + Vector2.up * 120f;

            CanvasGroup cg = cardRect.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardRect.gameObject.AddComponent<CanvasGroup>();

            cardRect.SetAsLastSibling();

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                // Ease out
                float et = 1f - Mathf.Pow(1f - t, 2f);

                // Quadratic bezier arc
                Vector2 p0p1 = Vector2.Lerp(startPos, midPos, et);
                Vector2 p1p2 = Vector2.Lerp(midPos, targetPos, et);
                cardRect.anchoredPosition = Vector2.Lerp(p0p1, p1p2, et);

                // Rotate and scale down as it flies
                cardRect.localRotation = Quaternion.Lerp(startRot, Quaternion.Euler(0, 0, -15f), et);
                cardRect.localScale = Vector3.Lerp(startScale, Vector3.one * 0.55f, et);
                cg.alpha = Mathf.Lerp(1f, 0f, et * et);

                yield return null;
            }

            cardRect.gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        /// <summary>
        /// BANG! — Đường đạn đỏ phóng từ người chơi đến mục tiêu.
        /// </summary>
        public void PlayBangAnimation(Canvas canvas, Vector2 fromScreenPos, Vector2 toScreenPos, Action onComplete)
        {
            StartCoroutine(BangBulletRoutine(canvas, fromScreenPos, toScreenPos, onComplete));
        }

        private IEnumerator BangBulletRoutine(Canvas canvas, Vector2 fromScreen, Vector2 toScreen, Action onComplete)
        {
            if (canvas == null) { onComplete?.Invoke(); yield break; }

            // Convert screen to canvas local
            RectTransform canvasRt = canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, fromScreen, null, out Vector2 fromLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, toScreen, null, out Vector2 toLocal);

            // Bullet flash object
            var bulletObj = new GameObject("BangBullet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bulletObj.transform.SetParent(canvas.transform, false);
            var bulletRt = bulletObj.GetComponent<RectTransform>();
            bulletRt.pivot = new Vector2(0f, 0.5f);
            bulletRt.sizeDelta = new Vector2(0f, 8f);

            var bulletImg = bulletObj.GetComponent<Image>();
            bulletImg.color = new Color(1f, 0.85f, 0.1f, 0.95f);

            // Muzzle flash
            var muzzleObj = new GameObject("MuzzleFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            muzzleObj.transform.SetParent(canvas.transform, false);
            var muzzleRt = muzzleObj.GetComponent<RectTransform>();
            muzzleRt.anchoredPosition = fromLocal;
            muzzleRt.sizeDelta = new Vector2(60f, 60f);
            muzzleObj.GetComponent<Image>().color = new Color(1f, 0.6f, 0.1f, 0.9f);

            Vector2 diff = toLocal - fromLocal;
            float totalDist = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            bulletRt.anchoredPosition = fromLocal;
            bulletRt.localRotation = Quaternion.Euler(0, 0, angle);

            float duration = 0.22f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float et = t; // linear travel

                // Grow bullet length as it travels
                float currentDist = totalDist * et;
                bulletRt.sizeDelta = new Vector2(currentDist, 8f);
                bulletRt.anchoredPosition = fromLocal;

                // Fade muzzle
                float muzzleAlpha = Mathf.Lerp(0.9f, 0f, t * 3f);
                muzzleObj.GetComponent<Image>().color = new Color(1f, 0.6f, 0.1f, Mathf.Max(0f, muzzleAlpha));

                yield return null;
            }

            // Impact flash at target
            muzzleRt.anchoredPosition = toLocal;
            muzzleObj.GetComponent<Image>().color = new Color(1f, 0.3f, 0.1f, 0.85f);

            // Fade out
            float fadeTime = 0f;
            float fadeDuration = 0.15f;
            while (fadeTime < fadeDuration)
            {
                fadeTime += Time.deltaTime;
                float ft = fadeTime / fadeDuration;
                bulletImg.color = new Color(1f, 0.85f, 0.1f, 1f - ft);
                muzzleObj.GetComponent<Image>().color = new Color(1f, 0.3f, 0.1f, 0.85f * (1f - ft));
                yield return null;
            }

            Destroy(bulletObj);
            Destroy(muzzleObj);
            onComplete?.Invoke();
        }

        /// <summary>
        /// NÉ — Flash xanh lá bảo vệ quanh avatar mục tiêu + screen hiệu ứng.
        /// </summary>
        public void PlayNegateAnimation(Canvas canvas, Vector2 targetScreenPos, Action onComplete)
        {
            StartCoroutine(NegateShieldRoutine(canvas, targetScreenPos, onComplete));
        }

        private IEnumerator NegateShieldRoutine(Canvas canvas, Vector2 targetScreen, Action onComplete)
        {
            if (canvas == null) { onComplete?.Invoke(); yield break; }

            RectTransform canvasRt = canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, targetScreen, null, out Vector2 targetLocal);

            // Outer ring
            var ringObj = new GameObject("NeShield", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            ringObj.transform.SetParent(canvas.transform, false);
            var ringRt = ringObj.GetComponent<RectTransform>();
            ringRt.anchoredPosition = targetLocal;
            ringRt.sizeDelta = new Vector2(130f, 130f);

            var ringImg = ringObj.GetComponent<Image>();
            ringImg.color = new Color(0.2f, 1f, 0.4f, 0f);

            var ringOutline = ringObj.GetComponent<Outline>();
            ringOutline.effectColor = new Color(0.2f, 1f, 0.4f, 0.9f);
            ringOutline.effectDistance = new Vector2(4, -4);

            // "NÉ!" floating text
            var textObj = new GameObject("NeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(canvas.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchoredPosition = targetLocal + Vector2.up * 80f;
            textRt.sizeDelta = new Vector2(200f, 60f);

            var txt = textObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 38;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.2f, 1f, 0.4f, 1f);
            txt.text = "NÉ!";
            textObj.GetComponent<Outline>().effectColor = new Color(0f, 0.3f, 0.1f, 0.9f);
            textObj.GetComponent<Outline>().effectDistance = new Vector2(2, -2);

            float duration = 0.5f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                // Shield: expand and fade
                float shieldT = t < 0.3f ? t / 0.3f : 1f - ((t - 0.3f) / 0.7f);
                ringRt.sizeDelta = new Vector2(130f + t * 50f, 130f + t * 50f);
                ringImg.color = new Color(0.2f, 1f, 0.4f, shieldT * 0.35f);
                ringOutline.effectColor = new Color(0.2f, 1f, 0.4f, shieldT * 0.9f);

                // Text: float up and fade
                textRt.anchoredPosition = targetLocal + Vector2.up * (80f + t * 40f);
                float textAlpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) / 0.5f);
                txt.color = new Color(0.2f, 1f, 0.4f, textAlpha);

                yield return null;
            }

            Destroy(ringObj);
            Destroy(textObj);
            onComplete?.Invoke();
        }

        /// <summary>
        /// Animation thẻ thông thường (Beer, Saloon, equipment...) — scale up rồi fade.
        /// </summary>
        public void PlayGenericCardAnimation(Canvas canvas, Vector2 screenPos, string cardName, Action onComplete)
        {
            StartCoroutine(GenericCardRoutine(canvas, screenPos, cardName, onComplete));
        }

        private IEnumerator GenericCardRoutine(Canvas canvas, Vector2 screenPos, string cardName, Action onComplete)
        {
            if (canvas == null) { onComplete?.Invoke(); yield break; }

            RectTransform canvasRt = canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out Vector2 localPos);

            // Popup label
            var popObj = new GameObject("CardPlayFX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            popObj.transform.SetParent(canvas.transform, false);
            var popRt = popObj.GetComponent<RectTransform>();
            popRt.anchoredPosition = localPos;
            popRt.sizeDelta = new Vector2(320f, 68f);
            var popImg = popObj.GetComponent<Image>();
            popImg.color = new Color(BangUITheme.Brass.r, BangUITheme.Brass.g, BangUITheme.Brass.b, 0.92f);
            popImg.sprite = BangUITheme.RoundedSprite;
            popImg.type = Image.Type.Sliced;

            var txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(popObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = BangUITheme.Ink;
            txt.text = cardName.ToUpperInvariant();
            txt.raycastTarget = false;

            float duration = 0.55f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                // Spring pop in then float up and fade
                float scaleT = t < 0.2f
                    ? EaseOutBack(t / 0.2f)
                    : 1f + (t - 0.2f) * 0.1f;
                popRt.localScale = Vector3.one * scaleT;
                popRt.anchoredPosition = localPos + Vector2.up * (t * 60f);

                float alpha = t < 0.4f ? 1f : 1f - ((t - 0.4f) / 0.6f);
                popImg.color = new Color(BangUITheme.Brass.r, BangUITheme.Brass.g, BangUITheme.Brass.b, alpha * 0.92f);
                txt.color = new Color(BangUITheme.Ink.r, BangUITheme.Ink.g, BangUITheme.Ink.b, alpha);

                yield return null;
            }

            Destroy(popObj);
            onComplete?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static float EaseOutBack(float t)
        {
            const float s = 1.70158f;
            float t1 = t - 1f;
            return t1 * t1 * ((s + 1f) * t1 + s) + 1f;
        }
    }
}

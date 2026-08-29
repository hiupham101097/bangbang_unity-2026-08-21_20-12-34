using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
                
                // Ease out cubic for alpha
                float alphaT = 1f - Mathf.Pow(1f - t, 3f);
                cg.alpha = alphaT;

                // Ease out back for scale (smooth spring)
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

                // Ease in quad
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
    }
}

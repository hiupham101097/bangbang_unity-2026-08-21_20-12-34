using System;
using System.Collections.Generic;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class HandCardFanLayout : MonoBehaviour
    {
        [Header("Hand Cards Alignment")]
        public Vector2 cardSize = new Vector2(240f, 340f);
        public float cardSpacing = 175f;
        public float maxFanAngle = 8f;
        public float arcHeight = 22f;
        public float horizontalPadding = 300f;
        public float baseCenterX = 0f;
        public float baseCenterY = 0f;
        public GameObject cardPrefab;

        private readonly List<CardUI> _cardUIs = new List<CardUI>();
        private float _lastLayoutWidth = -1f;

        public IReadOnlyList<CardUI> Cards => _cardUIs;

        public event Action<CardUI> OnCardClicked;
        public event Action<CardUI, Vector2> OnCardDragging;
        public event Action<CardUI, Vector2> OnCardDropped;

        public void UpdateHand(List<string> cardIds)
        {
            while (_cardUIs.Count < cardIds.Count)
            {
                var cardObj = CreateCardObject();
                var cardUI = cardObj.GetComponent<CardUI>();
                _cardUIs.Add(cardUI);
            }

            while (_cardUIs.Count > cardIds.Count)
            {
                var last = _cardUIs[_cardUIs.Count - 1];
                _cardUIs.RemoveAt(_cardUIs.Count - 1);
                Destroy(last.gameObject);
            }

            for (int i = 0; i < cardIds.Count; i++)
            {
                _cardUIs[i].Setup(cardIds[i]);
            }

            RecalculateFanPositions();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            float width = (transform as RectTransform)?.rect.width ?? 0f;
            if (Mathf.Abs(width - _lastLayoutWidth) > 1f) RecalculateFanPositions();
        }

        public void RecalculateFanPositions()
        {
            int count = _cardUIs.Count;
            if (count == 0) return;

            var container = transform as RectTransform;
            float containerWidth = container != null ? container.rect.width : 0f;
            float maxAvailableWidth = Mathf.Max(cardSize.x, containerWidth - horizontalPadding);
            float effectiveSpacing = count > 1 ? Mathf.Min(cardSpacing, maxAvailableWidth / (count - 1)) : 0f;
            float totalWidth = (count - 1) * effectiveSpacing;
            float startX = baseCenterX - (totalWidth / 2f);

            float angleStep = count > 1 ? (maxFanAngle * 2f) / (count - 1) : 0f;
            float startAngle = count > 1 ? maxFanAngle : 0f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float normalizedOffset = (t - 0.5f) * 2f;
                float archY = (1f - normalizedOffset * normalizedOffset) * arcHeight;

                // Whole-pixel placement avoids soft sampling on Screen Space canvases.
                float posX = Mathf.Round(startX + (i * effectiveSpacing));
                float posY = Mathf.Round(baseCenterY + archY);
                float rotZ = startAngle - (i * angleStep);

                _cardUIs[i].targetAnchoredPosition = new Vector2(posX, posY);
                _cardUIs[i].targetRotationZ = rotZ;
                _cardUIs[i].transform.SetSiblingIndex(i);
            }

            _lastLayoutWidth = containerWidth;
        }

        private GameObject CreateCardObject()
        {
            if (cardPrefab != null)
            {
                var go = Instantiate(cardPrefab, transform);
                BindCardEvents(go.GetComponent<CardUI>());
                return go;
            }

            var cardObj = new GameObject("CardUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CardUI));
            cardObj.transform.SetParent(transform, false);

            var rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = cardSize;

            var bgImg = cardObj.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.025f, 0.015f, 0.96f);

            // Border
            var borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(cardObj.transform, false);
            var borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(7, 7);
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.72f, 0.48f, 0.16f, 0.9f);
            borderImg.raycastTarget = false;

            // Artwork
            var artObj = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(cardObj.transform, false);
            var artRt = artObj.GetComponent<RectTransform>();
            artRt.anchorMin = Vector2.zero;
            artRt.anchorMax = Vector2.one;
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            var artImg = artObj.GetComponent<Image>();
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            // Title
            var titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 0.15f);
            titleRt.anchorMax = new Vector2(0.95f, 0.32f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            var titleTxt = titleObj.GetComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 16;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.2f, 0.1f, 0.05f);
            titleObj.SetActive(false);

            // Suit & Rank badge
            var suitObj = new GameObject("SuitRank", typeof(RectTransform), typeof(Text));
            suitObj.transform.SetParent(cardObj.transform, false);
            var suitRt = suitObj.GetComponent<RectTransform>();
            suitRt.anchorMin = new Vector2(0.05f, 0.02f);
            suitRt.anchorMax = new Vector2(0.95f, 0.15f);
            suitRt.offsetMin = Vector2.zero;
            suitRt.offsetMax = Vector2.zero;
            var suitTxt = suitObj.GetComponent<Text>();
            suitTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            suitTxt.fontSize = 15;
            suitTxt.alignment = TextAnchor.MiddleCenter;
            suitTxt.color = Color.black;
            suitTxt.raycastTarget = false;

            // Glowing Neon Green Outline
            var glowObj = new GameObject("NeonGlow", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(cardObj.transform, false);
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.sizeDelta = new Vector2(8, 8);
            var glowImg = glowObj.GetComponent<Image>();
            glowImg.color = new Color(0.3f, 1f, 0.3f, 0.85f);
            glowImg.raycastTarget = false;
            glowObj.SetActive(false);

            var cardUI = cardObj.GetComponent<CardUI>();
            cardUI.cardArtwork = artImg;
            cardUI.cardBackground = bgImg;
            cardUI.cardBorder = borderImg;
            cardUI.titleText = titleTxt;
            cardUI.suitRankText = suitTxt;
            cardUI.glowingNeonBorder = glowImg;

            BindCardEvents(cardUI);
            return cardObj;
        }

        private void BindCardEvents(CardUI cardUI)
        {
            cardUI.OnCardClicked += (c) => OnCardClicked?.Invoke(c);
            cardUI.OnCardDragging += (c, screenPos) => OnCardDragging?.Invoke(c, screenPos);
            cardUI.OnCardDropped += (c, screenPos) => OnCardDropped?.Invoke(c, screenPos);
        }
    }
}

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
        public float cardSpacing = 115f;
        public float maxFanAngle = 12f;
        public float arcHeight = 15f;
        public float baseCenterX = 120f;
        public float baseCenterY = -375f;
        public GameObject cardPrefab;

        private readonly List<CardUI> _cardUIs = new List<CardUI>();

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

        public void RecalculateFanPositions()
        {
            int count = _cardUIs.Count;
            if (count == 0) return;

            float maxAvailableWidth = 620f;
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

                float posX = startX + (i * effectiveSpacing);
                float posY = baseCenterY + archY;
                float rotZ = startAngle - (i * angleStep);

                _cardUIs[i].targetAnchoredPosition = new Vector2(posX, posY);
                _cardUIs[i].targetRotationZ = rotZ;
            }
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
            rt.sizeDelta = new Vector2(110, 160);

            var bgImg = cardObj.GetComponent<Image>();
            bgImg.color = new Color(0.96f, 0.93f, 0.88f); // Parchment

            // Border
            var borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(cardObj.transform, false);
            var borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(-4, -4);
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.45f, 0.28f, 0.15f);

            // Artwork
            var artObj = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(cardObj.transform, false);
            var artRt = artObj.GetComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.08f, 0.32f);
            artRt.anchorMax = new Vector2(0.92f, 0.92f);
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            var artImg = artObj.GetComponent<Image>();
            artImg.preserveAspect = true;

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
            titleTxt.fontSize = 12;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.2f, 0.1f, 0.05f);

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
            suitTxt.fontSize = 11;
            suitTxt.alignment = TextAnchor.MiddleCenter;
            suitTxt.color = Color.black;

            // Glowing Neon Green Outline
            var glowObj = new GameObject("NeonGlow", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(cardObj.transform, false);
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.sizeDelta = new Vector2(8, 8);
            var glowImg = glowObj.GetComponent<Image>();
            glowImg.color = new Color(0.3f, 1f, 0.3f, 0.85f);
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
            cardUI.OnCardDragging += (c, screenPos) => OnCardDragging?.Invoke(c, screenPos);
            cardUI.OnCardDropped += (c, screenPos) => OnCardDropped?.Invoke(c, screenPos);
        }
    }
}

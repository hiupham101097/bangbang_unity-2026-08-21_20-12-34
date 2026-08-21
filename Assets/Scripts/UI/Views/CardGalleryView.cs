using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class CardGalleryView : MonoBehaviour
    {
        [Header("Header")]
        public Text titleText;
        public Button backButton;

        [Header("Scroll Grid")]
        public ScrollRect scrollRect;
        public Transform gridContent;

        [Header("Detail Modal")]
        public GameObject detailModal;
        public Image detailCardImage;
        public Text detailCardName;
        public Text detailCardType;
        public Text detailCardRange;
        public Text detailCardDesc;
        public Button closeDetailButton;

        private readonly List<GameObject> _cardItems = new List<GameObject>();

        public void BindListeners(Action onBack)
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    onBack?.Invoke();
                });
            }

            if (closeDetailButton != null)
            {
                closeDetailButton.onClick.RemoveAllListeners();
                closeDetailButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    ShowDetailModal(false);
                });
            }
        }

        public void PopulateCards()
        {
            if (gridContent == null) return;

            foreach (var item in _cardItems) Destroy(item);
            _cardItems.Clear();

            var cards = CardCatalogDatabase.GetAllCards();

            foreach (var card in cards)
            {
                var cardObj = CreateGalleryCardItem(card);
                _cardItems.Add(cardObj);
            }

            if (detailModal != null) detailModal.SetActive(false);
        }

        private GameObject CreateGalleryCardItem(CardInfo card)
        {
            var itemObj = new GameObject("CardItem_" + card.id, typeof(RectTransform), typeof(Image), typeof(Button));
            itemObj.transform.SetParent(gridContent, false);

            var rt = itemObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 230);

            var bgImg = itemObj.GetComponent<Image>();
            bgImg.sprite = CardCatalogDatabase.LoadSprite(card.resourcePath);
            bgImg.color = Color.white;

            var btn = itemObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("button_tap");
                OpenCardDetail(card);
            });

            return itemObj;
        }

        public void OpenCardDetail(CardInfo card)
        {
            if (detailModal == null) return;

            if (detailCardImage != null)
            {
                detailCardImage.sprite = CardCatalogDatabase.LoadSprite(card.resourcePath);
            }

            if (detailCardName != null)
            {
                detailCardName.text = card.vietnameseName;
            }

            if (detailCardType != null)
            {
                detailCardType.text = card.type == CardType.BrownAction ? "🏷️ THẺ HÀNH ĐỘNG (DÙNG 1 LẦN)" : "🛡️ THẺ TRANG BỊ (ĐẶT TRƯỚC MẶT)";
                detailCardType.color = card.type == CardType.BrownAction ? new Color(0.85f, 0.45f, 0.2f) : new Color(0.25f, 0.65f, 1f);
            }

            if (detailCardRange != null)
            {
                if (card.requiresTarget)
                {
                    detailCardRange.text = card.targetRangeOne ? "🎯 Tầm tác dụng: Cự ly 1" : card.targetAnyRange ? "🎯 Tầm tác dụng: Toàn bàn" : "🎯 Tầm tác dụng: Theo tầm súng";
                }
                else
                {
                    detailCardRange.text = "🎯 Tầm tác dụng: Bản thân / Toàn bàn";
                }
            }

            if (detailCardDesc != null)
            {
                detailCardDesc.text = card.description;
            }

            ShowDetailModal(true);
        }

        private void ShowDetailModal(bool show)
        {
            if (detailModal != null) detailModal.SetActive(show);
        }
    }
}

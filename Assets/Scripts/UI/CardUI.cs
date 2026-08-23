using System;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Card Data")]
        public string cardId;
        public CardInfo info;

        [Header("UI Elements")]
        public Image cardArtwork;
        public Image cardBorder;
        public Image cardBackground;
        public Text titleText;
        public Text descText;
        public Text suitRankText;
        public Image glowingNeonBorder; // Green glow on selected

        public Vector2 targetAnchoredPosition;
        public float targetRotationZ;
        public Vector3 targetScale = Vector3.one;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private bool _isDragging;
        private Vector2 _dragOffset;
        private int _originalSiblingIndex;

        public event Action<CardUI> OnCardClicked;
        public event Action<CardUI, Vector2> OnCardDragging;
        public event Action<CardUI, Vector2> OnCardDropped;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(string id)
        {
            cardId = id;
            info = CardCatalogDatabase.GetCardInfo(id);

            if (titleText != null)
            {
                titleText.text = info.name;
            }

            if (descText != null)
            {
                descText.text = info.description;
            }

            if (cardArtwork != null)
            {
                var sprite = CardCatalogDatabase.LoadSprite(info.resourcePath);
                if (sprite != null)
                {
                    cardArtwork.sprite = sprite;
                    cardArtwork.color = Color.white;
                }
            }

            var instanceParts = id.Split(new[] { "__" }, StringSplitOptions.None);
            if (instanceParts.Length >= 4 && suitRankText != null)
            {
                string suit = instanceParts[2].ToLowerInvariant();
                string suitSymbol = suit == "hearts" ? "♥" : suit == "diamonds" ? "♦" : suit == "spades" ? "♠" : "♣";
                suitRankText.text = instanceParts[3].ToUpperInvariant() + " " + suitSymbol;
                suitRankText.color = (suit == "hearts" || suit == "diamonds") ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.15f, 0.15f, 0.15f);
            }

            SetHighlight(false);
        }

        private void Update()
        {
            if (!_isDragging && _rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, targetAnchoredPosition, Time.deltaTime * 18f);
                _rectTransform.localRotation = Quaternion.Lerp(_rectTransform.localRotation, Quaternion.Euler(0, 0, targetRotationZ), Time.deltaTime * 18f);
                _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, targetScale, Time.deltaTime * 18f);
            }
        }

        public void SetHighlight(bool highlighted)
        {
            if (glowingNeonBorder != null)
            {
                glowingNeonBorder.gameObject.SetActive(highlighted);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                targetScale = Vector3.one * 1.15f;
                targetAnchoredPosition += new Vector2(0, 45f);
                SetHighlight(true);
                transform.SetAsLastSibling();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                targetScale = Vector3.one;
                SetHighlight(false);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnCardClicked?.Invoke(this);
        }

        public void OnPointerUp(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.9f;
            targetScale = Vector3.one * 1.15f;
            SetHighlight(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out _dragOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rectTransform != null && _canvas != null)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, eventData.position, eventData.pressEventCamera, out pos);
                _rectTransform.anchoredPosition = pos;
                _rectTransform.localRotation = Quaternion.identity;
                OnCardDragging?.Invoke(this, eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1.0f;
            targetScale = Vector3.one;
            SetHighlight(false);
            transform.SetSiblingIndex(_originalSiblingIndex);
            OnCardDropped?.Invoke(this, eventData.position);
        }
    }
}

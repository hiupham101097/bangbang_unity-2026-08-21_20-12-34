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
        private bool _isHovered;
        private bool _isSelected;
        private bool _isPlayable = true;
        private bool _suppressPointerUp;

        public bool IsSelected => _isSelected;
        public bool IsPlayable => _isPlayable;

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
            bool cardChanged = cardId != id;
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

            if (suitRankText != null && TryReadSuitAndRank(id, out string suit, out string rank))
            {
                string suitSymbol = suit == "hearts" ? "♥" : suit == "diamonds" ? "♦" : suit == "spades" ? "♠" : "♣";
                suitRankText.text = rank.ToUpperInvariant() + " " + suitSymbol;
                suitRankText.color = (suit == "hearts" || suit == "diamonds") ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.15f, 0.15f, 0.15f);
            }

            if (cardChanged) SetSelected(false);
        }

        private static bool TryReadSuitAndRank(string id, out string suit, out string rank)
        {
            suit = string.Empty;
            rank = string.Empty;
            var instanceParts = id.Split(new[] { "__" }, StringSplitOptions.None);
            if (instanceParts.Length >= 4)
            {
                suit = instanceParts[2].ToLowerInvariant();
                rank = instanceParts[3];
                return true;
            }

            var legacyParts = id.Split('_');
            if (legacyParts.Length < 3) return false;
            suit = legacyParts[legacyParts.Length - 2].ToLowerInvariant();
            rank = legacyParts[legacyParts.Length - 1];
            if (suit == "heart") suit = "hearts";
            else if (suit == "diamond") suit = "diamonds";
            else if (suit == "spade") suit = "spades";
            else if (suit == "club") suit = "clubs";
            return suit == "hearts" || suit == "diamonds" || suit == "spades" || suit == "clubs";
        }

        private void Update()
        {
            if (!_isDragging && _rectTransform != null)
            {
                // Hover must never change the card's position. Touch devices can
                // keep a pointer in the hovered state after a tap, which used to
                // make an unselected card remain (or appear to fly) above the hand.
                float lift = _isSelected ? 78f : 0f;
                Vector2 visualPosition = targetAnchoredPosition + new Vector2(0f, lift);
                _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, visualPosition, Time.deltaTime * 18f);
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

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (!selected) _isHovered = false;
            targetScale = selected ? Vector3.one * 1.12f : _isHovered ? Vector3.one * 1.07f : Vector3.one;
            SetHighlight(selected || _isHovered);
        }

        public void SetPlayable(bool playable)
        {
            _isPlayable = playable;
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = playable || _isSelected ? 1f : 0.58f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                _isHovered = true;
                _originalSiblingIndex = transform.GetSiblingIndex();
                targetScale = _isSelected ? Vector3.one * 1.12f : Vector3.one * 1.07f;
                SetHighlight(true);
                transform.SetAsLastSibling();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                _isHovered = false;
                targetScale = _isSelected ? Vector3.one * 1.12f : Vector3.one;
                SetHighlight(_isSelected);
                transform.SetSiblingIndex(_originalSiblingIndex);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _suppressPointerUp = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_suppressPointerUp)
            {
                _suppressPointerUp = false;
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
                OnCardClicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _suppressPointerUp = true;
            _isHovered = false;
            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.9f;
            targetScale = Vector3.one * 1.08f;
            SetHighlight(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out _dragOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rectTransform != null && _canvas != null)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out pos);
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
            targetScale = _isSelected ? Vector3.one * 1.12f : Vector3.one;
            SetHighlight(_isSelected);
            transform.SetSiblingIndex(_originalSiblingIndex);
            OnCardDropped?.Invoke(this, eventData.position);
        }
    }
}

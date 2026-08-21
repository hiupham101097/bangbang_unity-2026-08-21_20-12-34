using System;
using System.Collections.Generic;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class PlayerSeatUI : MonoBehaviour
    {
        public string playerId;
        public int seatIndex;
        public PlayerModel playerModel;
        public bool isLocalPlayer;

        [Header("UI Frame Components")]
        public Image avatarImage;
        public Image avatarFrameImage;
        public Image roleBadgeStar; // Sheriff star
        public Image heartHpBadge; // Red Heart
        public Text hpText;
        public Text nameText;
        public Text roleText;
        public Image turnActiveGlow;
        public GameObject crosshairTargetObj;
        public Button seatSelectButton;

        [Header("Hand Cards Back & Equipment Shelf")]
        public Transform equipmentRowTransform;
        public Transform handCardsBackTransform;

        private readonly List<GameObject> _equippedCardObjects = new List<GameObject>();
        private readonly List<GameObject> _handBackObjects = new List<GameObject>();

        public event Action<string> OnSeatClicked;

        private void Awake()
        {
            if (seatSelectButton != null)
            {
                seatSelectButton.onClick.AddListener(() => OnSeatClicked?.Invoke(playerId));
            }
        }

        public void SetupSeat(PlayerModel player, int calculatedDistance = -1, bool isLocal = false)
        {
            playerModel = player;
            playerId = player.id;
            seatIndex = player.seat;
            isLocalPlayer = isLocal;

            // Name
            if (nameText != null)
            {
                nameText.text = player.name;
                nameText.color = isLocal ? new Color(1f, 0.9f, 0.4f) : Color.white;
            }

            // Avatar sprite
            if (avatarImage != null && player.character != null)
            {
                var sprite = CardCatalogDatabase.LoadSprite(player.character.resourcePath);
                if (sprite != null)
                {
                    avatarImage.sprite = sprite;
                    avatarImage.color = player.isAlive ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.7f);
                }
            }

            // Sheriff Golden Star
            if (roleBadgeStar != null)
            {
                roleBadgeStar.gameObject.SetActive(player.role == RoleType.Sheriff);
            }

            // Heart HP Badge
            if (hpText != null)
            {
                hpText.text = player.health.ToString();
            }

            // Role text below avatar
            if (roleText != null)
            {
                if (player.role == RoleType.Sheriff || player.isRoleRevealed || !player.isAlive || isLocal)
                {
                    var rInfo = CardCatalogDatabase.GetRoleInfo(player.role);
                    roleText.text = rInfo.vietnameseName;
                    roleText.color = player.role == RoleType.Sheriff ? new Color(1f, 0.85f, 0.2f) :
                                     player.role == RoleType.Deputy ? new Color(0.35f, 0.75f, 1f) :
                                     player.role == RoleType.Outlaw ? new Color(1f, 0.35f, 0.35f) :
                                     new Color(0.85f, 0.45f, 1f);
                }
                else
                {
                    roleText.text = "Ẩn danh";
                    roleText.color = new Color(0.7f, 0.7f, 0.7f);
                }
            }

            // Build equipment cards shelf & hand back cards
            UpdateEquipmentShelf(player);
            UpdateOpponentHandBack(player);

            SetTargetCrosshair(false);
        }

        public void SetTurnActive(bool isActive)
        {
            if (turnActiveGlow != null) turnActiveGlow.gameObject.SetActive(isActive);
        }

        public void SetTargetCrosshair(bool show)
        {
            if (crosshairTargetObj != null) crosshairTargetObj.SetActive(show);
        }

        public Vector2 GetScreenCenterPosition()
        {
            return RectTransformUtility.WorldToScreenPoint(null, transform.position);
        }

        private void UpdateEquipmentShelf(PlayerModel p)
        {
            if (equipmentRowTransform == null) return;

            // Clear old
            foreach (var obj in _equippedCardObjects) Destroy(obj);
            _equippedCardObjects.Clear();

            // Recreate equipment cards in a tight horizontal row
            foreach (var eqId in p.equipment)
            {
                var cardInfo = CardCatalogDatabase.GetCardInfo(eqId);
                var cardObj = CreateMiniCard(cardInfo);
                cardObj.transform.SetParent(equipmentRowTransform, false);
                _equippedCardObjects.Add(cardObj);
            }
        }

        private void UpdateOpponentHandBack(PlayerModel p)
        {
            if (handCardsBackTransform == null || isLocalPlayer) return;

            foreach (var obj in _handBackObjects) Destroy(obj);
            _handBackObjects.Clear();

            // Show 1 to 3 overlapping card backs to represent hand count
            int displayCount = Mathf.Min(3, p.cardCount);
            for (int i = 0; i < displayCount; i++)
            {
                var backObj = new GameObject("HandBack_" + i, typeof(RectTransform), typeof(Image));
                backObj.transform.SetParent(handCardsBackTransform, false);
                var rt = backObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(40, 58);
                rt.anchoredPosition = new Vector2(i * 12f, 0);

                var img = backObj.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                _handBackObjects.Add(backObj);
            }
        }

        private GameObject CreateMiniCard(CardInfo info)
        {
            var cardObj = new GameObject("MiniCard_" + info.id, typeof(RectTransform), typeof(Image));
            var rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(44, 62);

            var img = cardObj.GetComponent<Image>();
            img.sprite = CardCatalogDatabase.LoadSprite(info.resourcePath);
            img.color = Color.white;

            return cardObj;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using BangBang.UI.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class GameTableView : MonoBehaviour
    {
        [Header("Table Visuals")]
        public Image tableBackgroundImage;
        public Transform opponentSeatsContainer;
        public GameObject playerSeatPrefab;

        [Header("Center Decks & Combat Zone")]
        public Image drawPileImage;
        public Text drawPileCountText;
        public Image discardPileImage;
        public Text discardPileCountText;
        public Text combatLogText;
        public Text turnPhaseStatusText;

        [Header("Local Player Dashboard (Bottom)")]
        public Image localAvatarImage;
        public Text localNameText;
        public Text localRoleText;
        public Transform localBulletHealthContainer;
        public Transform localEquipmentTray;
        public HandCardFanLayout handCardLayout;
        public Button drawCardButton;
        public Button playCardButton;
        public Text playCardButtonText;
        public Button endTurnButton;
        public Button cancelTargetButton;
        public GameObject targetBannerObj;
        public Text targetBannerText;
        public GameObject cardPreviewTooltipObj;
        public Text cardPreviewTooltipText;
        public Text timerText;

        private readonly List<PlayerSeatUI> _seatUIs = new List<PlayerSeatUI>();
        private readonly List<GameObject> _localEquipCards = new List<GameObject>();
        private readonly List<GameObject> _bulletTokens = new List<GameObject>();

        private CardUI _selectedCardUI;
        private string _selectedTargetId;

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            if (tableBackgroundImage != null)
            {
                var tableSprite = CardCatalogDatabase.LoadSprite("room_table");
                if (tableSprite != null)
                {
                    tableBackgroundImage.sprite = tableSprite;
                    tableBackgroundImage.color = Color.white;
                }
            }

            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += RenderTableSnapshot;
                GameStateStore.Instance.OnCombatLogAdded += (msg) =>
                {
                    if (combatLogText != null) combatLogText.text = msg;
                };
            }

            if (handCardLayout != null)
            {
                handCardLayout.OnCardClicked += HandleCardSelected;
                handCardLayout.OnCardDropped += HandleHandCardPlayed;
            }

            if (targetBannerObj != null) targetBannerObj.SetActive(false);
            if (cardPreviewTooltipObj != null) cardPreviewTooltipObj.SetActive(false);
            if (playCardButton != null) playCardButton.gameObject.SetActive(false);
            if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(false);
        }

        public void BindListeners()
        {
            if (drawCardButton != null)
            {
                drawCardButton.onClick.RemoveAllListeners();
                drawCardButton.onClick.AddListener(HandleDrawCardClicked);
            }

            if (playCardButton != null)
            {
                playCardButton.onClick.RemoveAllListeners();
                playCardButton.onClick.AddListener(HandlePlayCardButtonClicked);
            }

            if (cancelTargetButton != null)
            {
                cancelTargetButton.onClick.RemoveAllListeners();
                cancelTargetButton.onClick.AddListener(CancelCardSelection);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(HandleEndTurnClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= RenderTableSnapshot;
            }
        }

        public void RenderTableSnapshot(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null) return;

            string localId = GameStateStore.Instance != null ? GameStateStore.Instance.LocalPlayerId : "";
            var local = snapshot.players.Find(p => p.id == localId);

            // 1. Center Decks & Status
            if (drawPileCountText != null) drawPileCountText.text = snapshot.drawPileCount.ToString();
            if (discardPileCountText != null) discardPileCountText.text = snapshot.discardPileCount.ToString();
            if (discardPileImage != null && !string.IsNullOrEmpty(snapshot.topDiscardCardId))
            {
                var info = CardCatalogDatabase.GetCardInfo(snapshot.topDiscardCardId);
                discardPileImage.sprite = CardCatalogDatabase.LoadSprite(info.resourcePath);
            }

            bool isMyTurn = snapshot.currentTurnPlayerId == localId;
            if (turnPhaseStatusText != null)
            {
                turnPhaseStatusText.text = isMyTurn ? "🟢 LƯỢT CỦA BẠN" : "⏳ LƯỢT ĐỐI THỦ";
                turnPhaseStatusText.color = isMyTurn ? new Color(0.3f, 1f, 0.4f) : Color.white;
            }

            // 2. Buttons State
            if (drawCardButton != null)
            {
                drawCardButton.gameObject.SetActive(isMyTurn && snapshot.currentPhase == "draw");
                drawCardButton.interactable = !GameStateStore.Instance.IsRequestPending;
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = isMyTurn && snapshot.currentPhase == "play" && !GameStateStore.Instance.IsRequestPending;
            }

            // 3. Local Dashboard
            if (local != null)
            {
                if (localNameText != null) localNameText.text = local.name;
                if (localRoleText != null)
                {
                    localRoleText.text = local.role == "sheriff" ? "⭐ CẢNH SÁT TRƯỞNG" :
                                         local.role == "deputy" ? "🛡️ PHÓ CẢNH SÁT" :
                                         local.role == "outlaw" ? "💀 CƯỚP (OUTLAW)" : "🗡️ PHẢN BỘI (RENEGADE)";
                }

                if (localAvatarImage != null && !string.IsNullOrEmpty(local.characterId))
                {
                    var charInfo = CardCatalogDatabase.GetCharacterInfo(local.characterId);
                    localAvatarImage.sprite = CardCatalogDatabase.LoadSprite(charInfo.resourcePath);
                }

                RenderBulletHealth(local.currentHealth, local.maxHealth);
                RenderLocalEquipment(local.equipment);

                if (handCardLayout != null)
                {
                    handCardLayout.UpdateHand(local.hand);
                }
            }

            // 4. Opponents Seats
            RenderOpponentSeats(snapshot, localId);
        }

        private void RenderBulletHealth(int current, int max)
        {
            if (localBulletHealthContainer == null) return;
            foreach (var b in _bulletTokens) Destroy(b);
            _bulletTokens.Clear();

            for (int i = 0; i < max; i++)
            {
                var bulletObj = new GameObject("Bullet_" + i, typeof(RectTransform), typeof(Image));
                bulletObj.transform.SetParent(localBulletHealthContainer, false);
                var rt = bulletObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(22, 38);

                var img = bulletObj.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite("health_bullet");
                img.color = i < current ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.5f);

                _bulletTokens.Add(bulletObj);
            }
        }

        private void RenderLocalEquipment(List<string> equipment)
        {
            if (localEquipmentTray == null) return;
            foreach (var eq in _localEquipCards) Destroy(eq);
            _localEquipCards.Clear();

            if (equipment == null) return;

            foreach (var eqId in equipment)
            {
                var cardInfo = CardCatalogDatabase.GetCardInfo(eqId);
                var eqCard = new GameObject("Eq_" + cardInfo.id, typeof(RectTransform), typeof(Image));
                eqCard.transform.SetParent(localEquipmentTray, false);
                var rt = eqCard.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(75, 110);

                var img = eqCard.GetComponent<Image>();
                img.sprite = CardCatalogDatabase.LoadSprite(cardInfo.resourcePath);
                img.color = Color.white;

                _localEquipCards.Add(eqCard);
            }
        }

        private void RenderOpponentSeats(MatchStateSnapshotDTO snapshot, string localPlayerId)
        {
            var opponents = snapshot.players.Where(p => p.id != localPlayerId).ToList();

            while (_seatUIs.Count < opponents.Count)
            {
                var seatObj = new GameObject("OpponentSeat_" + _seatUIs.Count, typeof(RectTransform), typeof(PlayerSeatUI));
                seatObj.transform.SetParent(opponentSeatsContainer != null ? opponentSeatsContainer : transform, false);
                var seatUI = seatObj.GetComponent<PlayerSeatUI>();
                _seatUIs.Add(seatUI);
            }

            for (int i = 0; i < opponents.Count; i++)
            {
                var p = opponents[i];
                var model = new PlayerModel
                {
                    id = p.id,
                    name = p.name,
                    seat = p.seat,
                    isAlive = p.isAlive,
                    health = p.currentHealth,
                    maxHealth = p.maxHealth,
                    characterId = p.characterId,
                    character = CardCatalogDatabase.GetCharacterInfo(p.characterId),
                    role = p.isRoleRevealed && p.role == "sheriff" ? RoleType.Sheriff : RoleType.Outlaw,
                    isRoleRevealed = p.isRoleRevealed,
                    equipment = p.equipment,
                    hand = new List<string>() // empty for opponent
                };

                _seatUIs[i].SetupSeat(model, p.effectiveDistanceToLocal, false);
                _seatUIs[i].SetTargetHighlight(p.isTargetable);

                string seatTargetId = p.id;
                _seatUIs[i].OnSeatClicked -= HandleOpponentSeatClicked;
                _seatUIs[i].OnSeatClicked += HandleOpponentSeatClicked;

                // Position on horseshoe arc
                float t = opponents.Count > 1 ? (float)i / (opponents.Count - 1) : 0.5f;
                float angle = Mathf.Lerp(195f, -15f, t) * Mathf.Deg2Rad;
                var rt = _seatUIs[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(Mathf.Cos(angle) * 690f, Mathf.Sin(angle) * 270f + 50f);
            }
        }

        private void HandleCardSelected(CardUI card)
        {
            if (card == null || GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;

            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (snapshot == null || snapshot.currentTurnPlayerId != GameStateStore.Instance.LocalPlayerId) return;

            AudioManager.Instance?.PlaySFX("button_tap");
            _selectedCardUI = card;
            _selectedTargetId = null;

            // Show Card Tooltip Preview
            if (cardPreviewTooltipObj != null)
            {
                cardPreviewTooltipObj.SetActive(true);
                if (cardPreviewTooltipText != null)
                {
                    cardPreviewTooltipText.text = "<b>" + card.info.vietnameseName + "</b>: " + card.info.description;
                }
            }

            if (card.info.requiresTarget)
            {
                // Enter targeting mode
                if (targetBannerObj != null)
                {
                    targetBannerObj.SetActive(true);
                    if (targetBannerText != null)
                    {
                        targetBannerText.text = "🎯 HÃY CHỌN 1 MỤC TIÊU TRÊN BÀN ĐẤU";
                    }
                }

                if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(true);
                if (playCardButton != null) playCardButton.gameObject.SetActive(false);
            }
            else
            {
                // Instant Action Card (No target required: Beer, Saloon, Gatling, Equipment...)
                if (targetBannerObj != null) targetBannerObj.SetActive(false);
                if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(true);

                if (playCardButton != null)
                {
                    playCardButton.gameObject.SetActive(true);
                    playCardButton.interactable = true;
                    if (playCardButtonText != null)
                    {
                        playCardButtonText.text = "💥 ĐÁNH BÀI: " + card.info.vietnameseName;
                    }
                }
            }
        }

        private void HandleOpponentSeatClicked(string targetPlayerId)
        {
            if (_selectedCardUI == null || !_selectedCardUI.info.requiresTarget) return;

            var snapshot = GameStateStore.Instance?.CurrentSnapshot;
            if (snapshot == null) return;

            var targetPlayer = snapshot.players.Find(p => p.id == targetPlayerId);
            if (targetPlayer == null || !targetPlayer.isAlive) return;

            AudioManager.Instance?.PlaySFX("button_tap");
            _selectedTargetId = targetPlayerId;

            // Show targeting tracer line
            if (FXManager.Instance != null)
            {
                var targetSeat = _seatUIs.Find(s => s.playerId == targetPlayerId);
                if (targetSeat != null)
                {
                    Vector2 myPos = new Vector2(Screen.width * 0.5f, 100f);
                    FXManager.Instance.DrawTargetingLine(myPos, targetSeat.GetScreenCenterPosition());
                }
            }

            if (targetBannerText != null)
            {
                targetBannerText.text = "🎯 ĐÃ CHỌN: <b>" + targetPlayer.name + "</b>";
            }

            if (playCardButton != null)
            {
                playCardButton.gameObject.SetActive(true);
                playCardButton.interactable = true;
                if (playCardButtonText != null)
                {
                    playCardButtonText.text = "💥 BẮN VÀO: " + targetPlayer.name;
                }
            }
        }

        private async void HandlePlayCardButtonClicked()
        {
            if (_selectedCardUI == null || GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;

            if (_selectedCardUI.info.requiresTarget && string.IsNullOrEmpty(_selectedTargetId))
            {
                return;
            }

            string cardId = _selectedCardUI.cardId;
            string targetId = _selectedTargetId;
            var info = _selectedCardUI.info;

            CancelCardSelection();

            GameStateStore.Instance.SetRequestPending(true);
            AudioManager.Instance?.PlaySFX(info.id == "bang" ? "bang_shot" : "card_play");

            if (GameStateStore.Instance?.Gateway != null)
            {
                var targetList = !string.IsNullOrEmpty(targetId) ? new List<string> { targetId } : null;
                await GameStateStore.Instance.Gateway.PlayCardAsync(cardId, targetList);
            }
        }

        public void CancelCardSelection()
        {
            _selectedCardUI = null;
            _selectedTargetId = null;

            if (FXManager.Instance != null) FXManager.Instance.HideTargetingLine();
            if (targetBannerObj != null) targetBannerObj.SetActive(false);
            if (cardPreviewTooltipObj != null) cardPreviewTooltipObj.SetActive(false);
            if (playCardButton != null) playCardButton.gameObject.SetActive(false);
            if (cancelTargetButton != null) cancelTargetButton.gameObject.SetActive(false);
        }

        private async void HandleDrawCardClicked()
        {
            CancelCardSelection();
            AudioManager.Instance?.PlaySFX("card_draw");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.RequestDrawAsync();
            }
        }

        private async void HandleHandCardPlayed(CardUI card, Vector2 screenPos)
        {
            // Drag and drop support
            if (GameStateStore.Instance == null || GameStateStore.Instance.IsRequestPending) return;

            var snapshot = GameStateStore.Instance.CurrentSnapshot;
            if (snapshot == null || snapshot.currentTurnPlayerId != GameStateStore.Instance.LocalPlayerId) return;

            string targetId = null;
            if (card.info.requiresTarget)
            {
                var targetableOpp = snapshot.players.Find(p => p.id != GameStateStore.Instance.LocalPlayerId && p.isTargetable && p.isAlive);
                if (targetableOpp != null) targetId = targetableOpp.id;
            }

            CancelCardSelection();
            GameStateStore.Instance.SetRequestPending(true);
            AudioManager.Instance?.PlaySFX(card.info.id == "bang" ? "bang_shot" : "card_play");

            if (GameStateStore.Instance?.Gateway != null)
            {
                var targetList = targetId != null ? new List<string> { targetId } : null;
                await GameStateStore.Instance.Gateway.PlayCardAsync(card.cardId, targetList);
            }
        }

        private async void HandleEndTurnClicked()
        {
            CancelCardSelection();
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.EndTurnAsync();
            }
        }
    }
}

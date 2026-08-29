using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Data;
using BangBang.Core.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public class TableManager : MonoBehaviour
    {
        [Header("Saloon Green Table Layout")]
        public RectTransform tableContainer;
        public Image tableBackground;
        public GameObject playerSeatPrefab;

        [Header("Center Decks & Combat Target Zone")]
        public RectTransform centerZoneContainer;
        public Text targetTitleText;
        public Text targetNameText;
        public Image playedCardCenterImage;
        public Image targetCardCenterImage;
        public GameObject centerArrowObj;
        public GameObject centerCrosshairObj;
        public Text actionHistoryText;

        [Header("Draw & Discard Piles")]
        public Image drawPileImage;
        public Text drawPileCountText;
        public Image discardPileImage;
        public Text discardPileCountText;

        private readonly List<PlayerSeatUI> _seats = new List<PlayerSeatUI>();

        public event Action<string> OnPlayerSeatSelected;

        public void SetupTable(MatchStateModel state, string localPlayerId)
        {
            if (tableBackground != null)
            {
                var tableSprite = CardCatalogDatabase.LoadSprite("room_table");
                if (tableSprite != null)
                {
                    tableBackground.sprite = tableSprite;
                    tableBackground.color = Color.white;
                }
            }

            var players = state.players;
            int totalPlayers = players.Count;

            int localSeatIndex = 0;
            var localPlayer = players.Find(p => p.id == localPlayerId);
            if (localPlayer != null) localSeatIndex = localPlayer.seat;

            while (_seats.Count < totalPlayers)
            {
                var seatObj = CreateDigitalBangSeatObject();
                var seatUI = seatObj.GetComponent<PlayerSeatUI>();
                seatUI.OnSeatClicked += (pId) => OnPlayerSeatSelected?.Invoke(pId);
                _seats.Add(seatUI);
            }

            while (_seats.Count > totalPlayers)
            {
                var last = _seats[_seats.Count - 1];
                _seats.RemoveAt(_seats.Count - 1);
                Destroy(last.gameObject);
            }

            for (int i = 0; i < totalPlayers; i++)
            {
                var p = players[i];
                var seatUI = _seats[i];
                bool isLocal = p.id == localPlayerId;

                int relativePos = (p.seat - localSeatIndex + totalPlayers) % totalPlayers;
                Vector2 pos = CalculateBangSeatPosition(relativePos, totalPlayers, isLocal);

                var rt = seatUI.GetComponent<RectTransform>();
                rt.anchoredPosition = pos;

                int dist = localPlayer != null && !isLocal ? BangGameRules.CalculateDistance(state, localPlayer.id, p.id) : 0;
                seatUI.SetupSeat(p, dist, isLocal);
                seatUI.SetTurnActive(state.currentTurnPlayerId == p.id);
            }

            // Draw Pile
            if (drawPileImage != null)
            {
                drawPileImage.sprite = CardCatalogDatabase.LoadCardBackSprite();
                drawPileImage.preserveAspect = true;
            }
            if (drawPileCountText != null)
            {
                drawPileCountText.text = state.deck.Count.ToString();
            }

            // Discard Pile
            if (discardPileImage != null && state.discard.Count > 0)
            {
                var top = state.discard[state.discard.Count - 1];
                var topInfo = CardCatalogDatabase.GetCardInfo(top);
                discardPileImage.sprite = CardCatalogDatabase.LoadSprite(topInfo.resourcePath);
                discardPileImage.gameObject.SetActive(true);
            }
            else if (discardPileImage != null)
            {
                discardPileImage.gameObject.SetActive(false);
            }
            if (discardPileCountText != null)
            {
                discardPileCountText.text = state.discard.Count.ToString();
            }
        }

        public void SetCombatActionDisplay(string actorName, string targetName, string cardId, string targetCardId = null)
        {
            if (targetNameText != null)
            {
                targetNameText.text = !string.IsNullOrEmpty(targetName) ? targetName.ToUpper() : "";
                if (targetTitleText != null) targetTitleText.gameObject.SetActive(!string.IsNullOrEmpty(targetName));
            }

            if (playedCardCenterImage != null && !string.IsNullOrEmpty(cardId))
            {
                var cardInfo = CardCatalogDatabase.GetCardInfo(cardId);
                playedCardCenterImage.sprite = CardCatalogDatabase.LoadSprite(cardInfo.resourcePath);
                playedCardCenterImage.gameObject.SetActive(true);
            }
            else if (playedCardCenterImage != null)
            {
                playedCardCenterImage.gameObject.SetActive(false);
            }

            if (actionHistoryText != null)
            {
                actionHistoryText.text = actorName + " played " + CardCatalogDatabase.GetCardInfo(cardId).name + " ➔ " + targetName;
            }
        }

        public PlayerSeatUI GetSeatByPlayerId(string playerId)
        {
            return _seats.FirstOrDefault(s => s.playerId == playerId);
        }

        public PlayerSeatUI GetSeatUnderScreenPosition(Vector2 screenPos)
        {
            foreach (var seat in _seats)
            {
                var rt = seat.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos))
                {
                    return seat;
                }
            }
            return null;
        }

        public void HighlightValidTargets(List<string> validPlayerIds)
        {
            foreach (var seat in _seats)
            {
                bool isValid = validPlayerIds != null && validPlayerIds.Contains(seat.playerId);
                seat.SetTargetCrosshair(isValid);
            }
        }

        public void ClearTargetHighlights()
        {
            foreach (var seat in _seats) seat.SetTargetCrosshair(false);
        }

        private Vector2 CalculateBangSeatPosition(int relativeIndex, int total, bool isLocal)
        {
            if (isLocal)
            {
                // Main Hero Portrait at bottom left of center
                return new Vector2(-280f, -370f);
            }

            // Positions matching the 7-player digital screenshot:
            // 1: Willy the Kid (Left Bottom)
            // 2: Jesse Jones (Left Top)
            // 3: Rico.pa44 (Top Left)
            // 4: Pixie Pete (Top Right)
            // 5: Rose Doolan (Right Top)
            // 6: Slab the Killer (Right Bottom)
            if (total == 7)
            {
                Vector2[] pos7 = {
                    new Vector2(-680f, -140f), // Willy (Left Bottom)
                    new Vector2(-700f, 150f),  // Jesse (Left Top)
                    new Vector2(-360f, 310f),  // Rico (Top Left)
                    new Vector2(360f, 310f),   // Pixie (Top Right)
                    new Vector2(700f, 150f),   // Rose (Right Top)
                    new Vector2(680f, -140f)   // Slab (Right Bottom)
                };
                return pos7[Mathf.Clamp(relativeIndex - 1, 0, 5)];
            }
            if (total == 5)
            {
                Vector2[] pos5 = {
                    new Vector2(-680f, 40f),
                    new Vector2(-340f, 300f),
                    new Vector2(340f, 300f),
                    new Vector2(680f, 40f)
                };
                return pos5[Mathf.Clamp(relativeIndex - 1, 0, 3)];
            }

            // Generic Horseshoe curve
            int oppCount = total - 1;
            float t = oppCount > 1 ? (float)(relativeIndex - 1) / (oppCount - 1) : 0.5f;
            float angle = Mathf.Lerp(195f, -15f, t) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle) * 690f, Mathf.Sin(angle) * 270f + 50f);
        }

        private GameObject CreateDigitalBangSeatObject()
        {
            var seatObj = new GameObject("PlayerSeatUI", typeof(RectTransform), typeof(PlayerSeatUI));
            seatObj.transform.SetParent(tableContainer != null ? tableContainer : transform, false);

            var rt = seatObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 150);

            // Circular Wooden Frame & Avatar
            var frameObj = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(seatObj.transform, false);
            var frameRt = frameObj.GetComponent<RectTransform>();
            frameRt.sizeDelta = new Vector2(90, 90);
            frameRt.anchoredPosition = new Vector2(0, 15f);
            var frameImg = frameObj.GetComponent<Image>();
            frameImg.color = new Color(0.4f, 0.25f, 0.12f); // Wood brown ring

            var avatarObj = new GameObject("AvatarMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            avatarObj.transform.SetParent(frameObj.transform, false);
            var avatarRt = avatarObj.GetComponent<RectTransform>();
            avatarRt.sizeDelta = new Vector2(80, 80);
            var avatarImg = avatarObj.GetComponent<Image>();
            avatarImg.preserveAspect = true;

            // Sheriff Golden Star Badge
            var starObj = new GameObject("SheriffStar", typeof(RectTransform), typeof(Image));
            starObj.transform.SetParent(frameObj.transform, false);
            var starRt = starObj.GetComponent<RectTransform>();
            starRt.anchoredPosition = new Vector2(-36f, -32f);
            starRt.sizeDelta = new Vector2(32, 32);
            var starImg = starObj.GetComponent<Image>();
            starImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
            starObj.SetActive(false);

            // Red Heart HP Badge
            var heartObj = new GameObject("HeartHpBadge", typeof(RectTransform), typeof(Image));
            heartObj.transform.SetParent(frameObj.transform, false);
            var heartRt = heartObj.GetComponent<RectTransform>();
            heartRt.anchoredPosition = new Vector2(34f, -32f);
            heartRt.sizeDelta = new Vector2(30, 30);
            var heartImg = heartObj.GetComponent<Image>();
            heartImg.color = new Color(0.85f, 0.15f, 0.15f); // Crimson red heart

            var hpTxtObj = new GameObject("HpText", typeof(RectTransform), typeof(Text));
            hpTxtObj.transform.SetParent(heartObj.transform, false);
            var hpTxtRt = hpTxtObj.GetComponent<RectTransform>();
            hpTxtRt.anchorMin = Vector2.zero;
            hpTxtRt.anchorMax = Vector2.one;
            var hpTxt = hpTxtObj.GetComponent<Text>();
            hpTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpTxt.fontSize = 15;
            hpTxt.fontStyle = FontStyle.Bold;
            hpTxt.alignment = TextAnchor.MiddleCenter;
            hpTxt.color = Color.white;
            hpTxt.text = "4";

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(seatObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, 68f);
            nameRt.sizeDelta = new Vector2(160, 22);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            // Role text
            var roleObj = new GameObject("Role", typeof(RectTransform), typeof(Text));
            roleObj.transform.SetParent(seatObj.transform, false);
            var roleRt = roleObj.GetComponent<RectTransform>();
            roleRt.anchoredPosition = new Vector2(0, -42f);
            roleRt.sizeDelta = new Vector2(140, 20);
            var roleTxt = roleObj.GetComponent<Text>();
            roleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roleTxt.fontSize = 12;
            roleTxt.alignment = TextAnchor.MiddleCenter;
            roleTxt.color = new Color(0.9f, 0.8f, 0.4f);

            // Equipment Row Shelf (next to avatar)
            var eqObj = new GameObject("EquipmentShelf", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            eqObj.transform.SetParent(seatObj.transform, false);
            var eqRt = eqObj.GetComponent<RectTransform>();
            eqRt.anchoredPosition = new Vector2(85f, 15f);
            eqRt.sizeDelta = new Vector2(100, 65);
            var hlg = eqObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 4f;

            // Crosshair
            var crosshairObj = new GameObject("Crosshair", typeof(RectTransform), typeof(Image), typeof(Button));
            crosshairObj.transform.SetParent(seatObj.transform, false);
            var crossRt = crosshairObj.GetComponent<RectTransform>();
            crossRt.sizeDelta = new Vector2(100, 100);
            var crossImg = crosshairObj.GetComponent<Image>();
            crossImg.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            crosshairObj.SetActive(false);

            var seatUI = seatObj.GetComponent<PlayerSeatUI>();
            seatUI.avatarImage = avatarImg;
            seatUI.avatarFrameImage = frameImg;
            seatUI.nameText = nameTxt;
            seatUI.roleText = roleTxt;
            seatUI.roleBadgeStar = starImg;
            seatUI.heartHpBadge = heartImg;
            seatUI.hpText = hpTxt;
            seatUI.equipmentRowTransform = eqObj.transform;
            seatUI.crosshairTargetObj = crosshairObj;
            seatUI.seatSelectButton = crosshairObj.GetComponent<Button>();

            return seatObj;
        }
    }
}

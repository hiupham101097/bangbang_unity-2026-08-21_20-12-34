using System;
using System.Collections;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class RoleRevealView : MonoBehaviour
    {
        [Header("Containers & Texts")]
        public Transform roleCardsContainer;
        public Text roleTitleText;
        public Text roleGoalText;
        public Button continueButton;
        public Text timerCountdownText;

        private bool _isFlipped;
        private readonly List<GameObject> _cardObjects = new List<GameObject>();

        private void OnEnable()
        {
            _isFlipped = false;
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (roleTitleText != null) roleTitleText.text = "CHẠM VÀO THẺ CỦA BẠN ĐỂ LẬT";
            if (roleGoalText != null) roleGoalText.text = "";
            RenderRoleCards();
        }

        private void Start()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("button_tap");
                    if (GameFlowController.Instance != null &&
                        GameFlowController.Instance.CurrentState == ServerGameState.DEALING_ROLES)
                    {
                        GameFlowController.Instance.TransitionToState(ServerGameState.SELECTING_CHARACTER);
                    }
                });
            }
        }

        private void RenderRoleCards()
        {
            var snapshot = GameStateStore.Instance?.CurrentSnapshot;
            if (snapshot == null || snapshot.players == null) return;

            if (roleCardsContainer == null) return;
            foreach (var c in _cardObjects) Destroy(c);
            _cardObjects.Clear();

            string localId = GameStateStore.Instance.LocalPlayerId;
            var localPlayer = snapshot.players.Find(p => p.id == localId);
            string roleKey = localPlayer != null && !string.IsNullOrEmpty(localPlayer.role) ? localPlayer.role.ToLower() : "outlaw";
            bool isSheriff = roleKey == "sheriff";

            int playerCount = snapshot.players.Count;

            for (int i = 0; i < playerCount; i++)
            {
                var p = snapshot.players[i];
                bool isLocal = p.id == localId;

                var cardObj = new GameObject("RoleCard_" + p.id, typeof(RectTransform), typeof(Image), typeof(Button));
                cardObj.transform.SetParent(roleCardsContainer, false);
                var rt = cardObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(200, 300);

                var bgImg = cardObj.GetComponent<Image>();
                bgImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card");
                bgImg.color = new Color(0.6f, 0.4f, 0.2f); // face down tint

                var frontContainer = new GameObject("FrontContent", typeof(RectTransform), typeof(Image));
                frontContainer.transform.SetParent(cardObj.transform, false);
                var fRt = frontContainer.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;
                var fImg = frontContainer.GetComponent<Image>();
                fImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/" + roleKey + "_card");
                frontContainer.SetActive(false);

                Outline outline = null;
                if (isLocal)
                {
                    outline = cardObj.AddComponent<Outline>();
                    outline.effectColor = Color.yellow;
                    outline.effectDistance = new Vector2(4, -4);
                }

                var btn = cardObj.GetComponent<Button>();
                btn.interactable = isLocal;
                btn.onClick.AddListener(() =>
                {
                    if (isLocal && !_isFlipped)
                    {
                        _isFlipped = true;
                        AudioManager.Instance?.PlaySFX("button_tap");
                        StartCoroutine(FlipRoleCardCoroutine(cardObj, bgImg, frontContainer, roleKey));
                        if (outline != null) Destroy(outline);
                    }
                });

                _cardObjects.Add(cardObj);

                // Auto flip if sheriff
                if (isLocal && isSheriff)
                {
                    _isFlipped = true;
                    if (outline != null) Destroy(outline);
                    StartCoroutine(AutoFlipSheriffCoroutine(cardObj, bgImg, frontContainer, roleKey));
                }
            }
        }

        private IEnumerator AutoFlipSheriffCoroutine(GameObject cardObj, Image bgImg, GameObject frontContainer, string roleKey)
        {
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(FlipRoleCardCoroutine(cardObj, bgImg, frontContainer, roleKey));
        }

        private IEnumerator FlipRoleCardCoroutine(GameObject cardObj, Image bgImg, GameObject frontContainer, string roleKey)
        {
            AudioManager.Instance?.PlaySFX("card_draw");
            for (float t = 1f; t >= 0f; t -= Time.deltaTime * 6f)
            {
                cardObj.transform.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }

            bgImg.sprite = null;
            bgImg.color = Color.white;
            frontContainer.SetActive(true);

            for (float t = 0f; t <= 1f; t += Time.deltaTime * 6f)
            {
                cardObj.transform.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }
            cardObj.transform.localScale = Vector3.one;

            AudioManager.Instance?.PlaySFX("card_play");

            if (roleTitleText != null)
            {
                roleTitleText.text = roleKey == "sheriff" ? "⭐ CẢNH SÁT TRƯỞNG" :
                                    roleKey == "deputy" ? "🛡️ PHÓ CẢNH SÁT" :
                                    roleKey == "outlaw" ? "💀 NGOÀI VÒNG PHÁP LUẬT" : "🗡️ KẺ PHẢN BỘI";
                roleTitleText.color = roleKey == "sheriff" ? new Color(1f, 0.85f, 0.2f) :
                                      roleKey == "deputy" ? new Color(0.3f, 0.7f, 1f) :
                                      roleKey == "outlaw" ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.4f, 1f);
            }

            if (roleGoalText != null)
            {
                roleGoalText.text = roleKey == "sheriff" ? "Mục tiêu: Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội để bảo vệ thị trấn!" :
                                    roleKey == "deputy" ? "Mục tiêu: Bảo vệ Cảnh Sát Trưởng bằng mọi giá và tiêu diệt bọn Cướp!" :
                                    roleKey == "outlaw" ? "Mục tiêu: Tiêu diệt Cảnh Sát Trưởng để chiếm đoạt thị trấn!" :
                                    "Mục tiêu: Trở thành người sống sót cuối cùng và hạ gục Cảnh Sát Trưởng sau cùng!";
            }

            if (continueButton != null) continueButton.gameObject.SetActive(true);

            // Start countdown
            for (int sec = 8; sec >= 0; sec--)
            {
                if (timerCountdownText != null) timerCountdownText.text = "Tự động tiếp tục sau " + sec + "s...";
                yield return new WaitForSeconds(1.0f);
            }

            if (GameFlowController.Instance != null &&
                GameFlowController.Instance.CurrentState == ServerGameState.DEALING_ROLES)
            {
                GameFlowController.Instance.TransitionToState(ServerGameState.SELECTING_CHARACTER);
            }
        }
    }
}

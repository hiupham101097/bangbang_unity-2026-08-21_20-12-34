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
    public class CharacterSelectionView : MonoBehaviour
    {
        [Header("2 Candidate Card Containers")]
        public Transform candidatesContainer;
        public Text timerText;
        public Button confirmSelectionButton;
        public Text confirmButtonText;
        public GameObject waitingOthersOverlay;

        private string _selectedCharacterId;
        private readonly List<GameObject> _cardObjects = new List<GameObject>();

        private void OnEnable()
        {
            var snapshot = GameStateStore.Instance?.CurrentSnapshot;
            if (snapshot != null)
            {
                RenderCandidates(snapshot);
            }
        }

        private void Start()
        {
            BindListeners();
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += RenderCandidates;
                if (GameStateStore.Instance.CurrentSnapshot != null)
                {
                    RenderCandidates(GameStateStore.Instance.CurrentSnapshot);
                }
            }
        }

        public void BindListeners()
        {
            if (confirmSelectionButton != null)
            {
                confirmSelectionButton.onClick.RemoveAllListeners();
                confirmSelectionButton.onClick.AddListener(HandleConfirmSelectionClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= RenderCandidates;
            }
        }

        public void RenderCandidates(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null || snapshot.state != ServerGameState.SELECTING_CHARACTER) return;

            if (waitingOthersOverlay != null) waitingOthersOverlay.SetActive(false);
            _selectedCharacterId = null;

            if (candidatesContainer == null) return;
            foreach (var c in _cardObjects) Destroy(c);
            _cardObjects.Clear();

            string localId = GameStateStore.Instance.LocalPlayerId;
            var localPlayer = snapshot.players.Find(p => p.id == localId);
            var candidateIds = localPlayer != null && localPlayer.characterOptions != null && localPlayer.characterOptions.Count > 0 
                ? localPlayer.characterOptions 
                : new List<string> { "willy_the_kid", "calamity_janet" };

            foreach (var id in candidateIds)
            {
                var cardObj = CreateCharacterChoiceCard(id);
                cardObj.transform.SetParent(candidatesContainer, false);
                _cardObjects.Add(cardObj);
            }

            UpdateConfirmButton();
            if (confirmSelectionButton != null) confirmSelectionButton.gameObject.SetActive(false);
        }

        private GameObject CreateCharacterChoiceCard(string charId)
        {
            var info = CardCatalogDatabase.GetCharacterInfo(charId);

            var cardObj = new GameObject("CharCard_" + charId, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 440);

            var bgImg = cardObj.GetComponent<Image>();
            bgImg.sprite = CardCatalogDatabase.LoadSprite("role_cards/sheriff_card"); 
            bgImg.color = new Color(0.6f, 0.4f, 0.2f); // Face down color tint

            // Create front container
            var frontContainer = new GameObject("FrontContent", typeof(RectTransform));
            frontContainer.transform.SetParent(cardObj.transform, false);
            var fRt = frontContainer.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;
            frontContainer.SetActive(false);

            // Portrait
            var portObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portObj.transform.SetParent(frontContainer.transform, false);
            var portRt = portObj.GetComponent<RectTransform>();
            portRt.anchoredPosition = new Vector2(0, 80f);
            portRt.sizeDelta = new Vector2(200, 200);
            var portImg = portObj.GetComponent<Image>();
            portImg.sprite = CardCatalogDatabase.LoadSprite(info.resourcePath);
            portImg.preserveAspect = true;

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(frontContainer.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, -45f);
            nameRt.sizeDelta = new Vector2(280, 36);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 20;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = new Color(1f, 0.85f, 0.3f);
            nameTxt.text = info.name;

            // Skill Description
            var skillObj = new GameObject("Skill", typeof(RectTransform), typeof(Text));
            skillObj.transform.SetParent(frontContainer.transform, false);
            var skillRt = skillObj.GetComponent<RectTransform>();
            skillRt.anchoredPosition = new Vector2(0, -110f);
            skillRt.sizeDelta = new Vector2(260, 75);
            var skillTxt = skillObj.GetComponent<Text>();
            skillTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skillTxt.fontSize = 13;
            skillTxt.alignment = TextAnchor.MiddleCenter;
            skillTxt.color = new Color(0.9f, 0.9f, 0.9f);
            skillTxt.text = "<b>[" + info.abilityName + "]</b>\n" + info.description;

            // Bullets / HP
            var hpObj = new GameObject("HP", typeof(RectTransform), typeof(Text));
            hpObj.transform.SetParent(frontContainer.transform, false);
            var hpRt = hpObj.GetComponent<RectTransform>();
            hpRt.anchoredPosition = new Vector2(0, -175f);
            hpRt.sizeDelta = new Vector2(200, 30);
            var hpTxt = hpObj.GetComponent<Text>();
            hpTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpTxt.fontSize = 16;
            hpTxt.fontStyle = FontStyle.Bold;
            hpTxt.alignment = TextAnchor.MiddleCenter;
            hpTxt.color = new Color(1f, 0.3f, 0.3f);
            hpTxt.text = "MÁU: " + info.maxHealth + " ♥ (Đạn)";

            // Instruction Text (Face down)
            var instObj = new GameObject("Instruction", typeof(RectTransform), typeof(Text));
            instObj.transform.SetParent(cardObj.transform, false);
            var instRt = instObj.GetComponent<RectTransform>();
            instRt.anchoredPosition = Vector2.zero;
            instRt.sizeDelta = new Vector2(280, 50);
            var instTxt = instObj.GetComponent<Text>();
            instTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instTxt.fontSize = 20;
            instTxt.fontStyle = FontStyle.Bold;
            instTxt.alignment = TextAnchor.MiddleCenter;
            instTxt.color = Color.white;
            instTxt.text = "CHẠM ĐỂ LẬT";

            bool isFlipped = false;
            var btn = cardObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("button_tap");
                if (!isFlipped)
                {
                    StartCoroutine(FlipCardCoroutine(cardObj, bgImg, frontContainer, instObj));
                    isFlipped = true;
                }
                else
                {
                    _selectedCharacterId = charId;
                    HighlightSelectedCard();
                    UpdateConfirmButton();
                }
            });

            return cardObj;
        }

        private IEnumerator FlipCardCoroutine(GameObject cardObj, Image bgImg, GameObject frontContainer, GameObject instObj)
        {
            AudioManager.Instance?.PlaySFX("card_draw");
            for (float t = 1f; t >= 0f; t -= Time.deltaTime * 6f)
            {
                cardObj.transform.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }

            bgImg.sprite = null;
            bgImg.color = new Color(0.2f, 0.14f, 0.1f, 0.98f);
            instObj.SetActive(false);
            frontContainer.SetActive(true);

            for (float t = 0f; t <= 1f; t += Time.deltaTime * 6f)
            {
                cardObj.transform.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }
            cardObj.transform.localScale = Vector3.one;

            if (confirmSelectionButton != null) confirmSelectionButton.gameObject.SetActive(true);
        }

        private void HighlightSelectedCard()
        {
            foreach (var go in _cardObjects)
            {
                var img = go.GetComponent<Image>();
                bool isSelected = go.name == "CharCard_" + _selectedCharacterId;
                img.color = isSelected ? new Color(0.4f, 0.3f, 0.15f, 1f) : new Color(0.2f, 0.14f, 0.1f, 0.98f);
                
                var outline = go.GetComponent<Outline>();
                if (isSelected)
                {
                    if (outline == null) outline = go.AddComponent<Outline>();
                    outline.effectColor = Color.yellow;
                    outline.effectDistance = new Vector2(4, -4);
                }
                else if (outline != null)
                {
                    Destroy(outline);
                }
            }
        }

        private void UpdateConfirmButton()
        {
            if (confirmSelectionButton != null)
            {
                confirmSelectionButton.interactable = !string.IsNullOrEmpty(_selectedCharacterId) && !GameStateStore.Instance.IsRequestPending;
            }
        }

        private async void HandleConfirmSelectionClicked()
        {
            if (string.IsNullOrEmpty(_selectedCharacterId)) return;

            AudioManager.Instance?.PlaySFX("card_play");
            GameStateStore.Instance?.SetRequestPending(true);
            if (waitingOthersOverlay != null) waitingOthersOverlay.SetActive(true);

            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.SelectCharacterAsync(_selectedCharacterId);
            }
        }
    }
}

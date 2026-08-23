using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;

namespace BangBang.UI.Views
{
    public sealed class CharacterSelectionView : MonoBehaviour
    {
        public Transform candidatesContainer;
        public UnityEngine.UI.Text timerText;
        public UnityEngine.UI.Button confirmSelectionButton;
        public UnityEngine.UI.Text confirmButtonText;
        public GameObject waitingOthersOverlay;

        private readonly List<GameObject> _cards = new List<GameObject>();
        private string _selectedCharacterId;
        private MatchStateSnapshotDTO _snapshot;

        private void Start()
        {
            BindListeners();
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated += RenderCandidates;
        }

        private void OnEnable()
        {
            RenderCandidates(GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null);
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated -= RenderCandidates;
        }

        private void Update()
        {
            if (_snapshot == null || timerText == null) return;
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            timerText.text = "Còn " + Mathf.Max(0, Mathf.CeilToInt((_snapshot.deadlineAt - now) / 1000f)) + " giây";
        }

        public void BindListeners()
        {
            if (confirmSelectionButton == null) return;
            confirmSelectionButton.onClick.RemoveAllListeners();
            confirmSelectionButton.onClick.AddListener(ConfirmCharacter);
        }

        public void RenderCandidates(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null || (snapshot.state != ServerGameState.CHARACTER_DRAFT && snapshot.state != ServerGameState.CHARACTER_REVEAL)) return;
            _snapshot = snapshot;
            ClearCards();

            if (snapshot.state == ServerGameState.CHARACTER_REVEAL)
            {
                if (waitingOthersOverlay != null) waitingOthersOverlay.SetActive(false);
                foreach (var player in snapshot.players) CreateRevealedPlayerCard(player);
                if (confirmSelectionButton != null) confirmSelectionButton.gameObject.SetActive(false);
                return;
            }

            var privateState = snapshot.privateState;
            bool confirmed = privateState != null && !string.IsNullOrEmpty(privateState.selectedCharacterId);
            if (waitingOthersOverlay != null) waitingOthersOverlay.SetActive(snapshot.state == ServerGameState.CHARACTER_REVEAL || confirmed);
            bool hasOptions = privateState != null && privateState.draftCharacterOptions != null && privateState.draftCharacterOptions.Count == 2;
            if (hasOptions)
            {
                foreach (string id in privateState.draftCharacterOptions) CreateCharacterCard(id);
                if (confirmSelectionButton != null) confirmSelectionButton.gameObject.SetActive(snapshot.state == ServerGameState.CHARACTER_DRAFT && !confirmed);
            }
            else
            {
                _selectedCharacterId = null;
                int count = snapshot.draftSlotCount > 0 ? snapshot.draftSlotCount : snapshot.players.Count * 2;
                for (int slot = 0; slot < count; slot++) CreateDraftSlot(slot, privateState);
                if (confirmSelectionButton != null) confirmSelectionButton.gameObject.SetActive(false);
            }
            UpdateConfirmButton();
        }

        private void CreateDraftSlot(int slot, PrivatePlayerState privateState)
        {
            bool locked = _snapshot.lockedDraftSlots != null && _snapshot.lockedDraftSlots.Contains(slot);
            bool mine = privateState != null && privateState.draftCharacterSlots != null && privateState.draftCharacterSlots.Contains(slot);
            var card = new GameObject("CharacterSlot_" + slot, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            card.transform.SetParent(candidatesContainer, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 144);
            var image = card.GetComponent<UnityEngine.UI.Image>();
            image.sprite = CardCatalogDatabase.LoadSprite("card_back");
            image.color = locked && !mine ? new Color(0.32f, 0.32f, 0.32f, 0.7f) : Color.white;
            if (mine)
            {
                var outline = card.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(1f, 0.78f, 0.2f);
                outline.effectDistance = new Vector2(3, -3);
            }
            var button = card.GetComponent<UnityEngine.UI.Button>();
            button.interactable = !locked && _snapshot.state == ServerGameState.CHARACTER_DRAFT && !GameStateStore.Instance.IsRequestPending;
            int captured = slot;
            button.onClick.AddListener(async () =>
            {
                AudioManager.Instance?.PlaySFX("card_draw");
                GameStateStore.Instance.SetRequestPending(true);
                bool sent = await GameStateStore.Instance.Gateway.PickCharacterSlotAsync(captured);
                if (!sent) GameStateStore.Instance.SetRequestPending(false);
            });
            _cards.Add(card);
        }

        private void CreateCharacterCard(string id)
        {
            var info = CardCatalogDatabase.GetCharacterInfo(id);
            var card = new GameObject("CharacterOption_" + id, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            card.transform.SetParent(candidatesContainer, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 420);
            card.GetComponent<UnityEngine.UI.Image>().color = new Color(0.14f, 0.09f, 0.06f, 0.98f);
            CreateImage(card.transform, "Portrait", info.resourcePath, new Vector2(0, 75), new Vector2(210, 210));
            CreateText(card.transform, "Name", info.name, new Vector2(0, -55), new Vector2(280, 36), 21, new Color(1f, 0.82f, 0.3f));
            CreateText(card.transform, "Ability", info.abilityName + "\n" + info.description, new Vector2(0, -125), new Vector2(270, 90), 14, Color.white);
            CreateText(card.transform, "HP", "HP: " + info.maxHealth, new Vector2(0, -185), new Vector2(180, 30), 17, new Color(1f, 0.4f, 0.3f));
            card.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                _selectedCharacterId = id;
                HighlightSelection();
                UpdateConfirmButton();
            });
            _cards.Add(card);
        }

        private void CreateRevealedPlayerCard(PlayerSnapshotDTO player)
        {
            var info = CardCatalogDatabase.GetCharacterInfo(player.characterId);
            var card = new GameObject("Revealed_" + player.id, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            card.transform.SetParent(candidatesContainer, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 250);
            card.GetComponent<UnityEngine.UI.Image>().color = player.publicRoleId == "sheriff" ? new Color(0.42f, 0.29f, 0.08f) : new Color(0.14f, 0.09f, 0.06f);
            CreateImage(card.transform, "Portrait", info.resourcePath, new Vector2(0, 35), new Vector2(130, 130));
            CreateText(card.transform, "Player", player.name, new Vector2(0, 112), new Vector2(160, 26), 14, Color.white);
            CreateText(card.transform, "Character", info.name, new Vector2(0, -48), new Vector2(160, 28), 14, new Color(1f, 0.82f, 0.3f));
            CreateText(card.transform, "HP", "HP " + player.currentHealth + "/" + player.maxHealth, new Vector2(0, -88), new Vector2(150, 26), 14, new Color(1f, 0.42f, 0.32f));
            _cards.Add(card);
        }

        private void HighlightSelection()
        {
            foreach (var card in _cards)
            {
                var outline = card.GetComponent<UnityEngine.UI.Outline>();
                bool selected = card.name == "CharacterOption_" + _selectedCharacterId;
                if (selected && outline == null) outline = card.AddComponent<UnityEngine.UI.Outline>();
                if (selected) { outline.effectColor = Color.yellow; outline.effectDistance = new Vector2(4, -4); }
                else if (outline != null) Destroy(outline);
            }
        }

        private void UpdateConfirmButton()
        {
            if (confirmSelectionButton != null)
                confirmSelectionButton.interactable = !string.IsNullOrEmpty(_selectedCharacterId) && GameStateStore.Instance != null && !GameStateStore.Instance.IsRequestPending;
        }

        private async void ConfirmCharacter()
        {
            if (string.IsNullOrEmpty(_selectedCharacterId) || GameStateStore.Instance == null) return;
            AudioManager.Instance?.PlaySFX("card_play");
            GameStateStore.Instance.SetRequestPending(true);
            bool sent = await GameStateStore.Instance.Gateway.SelectCharacterAsync(_selectedCharacterId);
            if (!sent) GameStateStore.Instance.SetRequestPending(false);
        }

        private void ClearCards()
        {
            foreach (var card in _cards) Destroy(card);
            _cards.Clear();
        }

        private static void CreateImage(Transform parent, string name, string resource, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.sprite = CardCatalogDatabase.LoadSprite(resource); image.preserveAspect = true; image.raycastTarget = false;
        }

        private static void CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            var text = obj.GetComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
        }
    }
}

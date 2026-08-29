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
    public sealed class RoleRevealView : MonoBehaviour
    {
        public Transform roleCardsContainer;
        public Text roleTitleText;
        public Text roleGoalText;
        public Button continueButton;
        public Text timerCountdownText;
        [Header("Public Sheriff Reveal")]
        public GameObject sheriffRevealRoot;
        public CanvasGroup sheriffRevealCanvasGroup;
        public Image sheriffAvatarImage;
        public Text sheriffNameText;
        public Text sheriffFirstTurnText;

        private readonly List<GameObject> _cards = new List<GameObject>();
        private MatchStateSnapshotDTO _snapshot;
        private string _animatedSheriffId;
        private Vector2 _draftCardSize = new Vector2(150f, 225f);

        private void Start()
        {
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (sheriffRevealRoot != null) sheriffRevealRoot.SetActive(false);
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated += Render;
        }

        private void OnEnable() => Render(GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null);

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated -= Render;
        }

        private void Update()
        {
            if (_snapshot == null || timerCountdownText == null) return;
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int seconds = Mathf.Max(0, Mathf.CeilToInt((_snapshot.deadlineAt - now) / 1000f));
            timerCountdownText.text = _snapshot.state == ServerGameState.ROLE_DRAFT
                ? "Còn " + seconds + " giây để chọn"
                : "Chọn nhân vật sau " + seconds + " giây";
        }

        private void Render(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null || (snapshot.state != ServerGameState.ROLE_DRAFT && snapshot.state != ServerGameState.ROLE_LOCK_WAIT)) return;
            _snapshot = snapshot;
            var privateState = snapshot.privateState;
            bool assigned = privateState != null && !string.IsNullOrEmpty(privateState.roleId);
            bool roleLocked = snapshot.state == ServerGameState.ROLE_LOCK_WAIT;

            if (roleTitleText != null)
                roleTitleText.text = roleLocked ? "VAI TRÒ CỦA BẠN ĐÃ KHÓA" : (assigned ? "VAI TRÒ RIÊNG CỦA BẠN" : "BƯỚC 1/3 — CHỌN VAI TRÒ");
            if (roleGoalText != null)
                roleGoalText.text = assigned
                    ? GoalFor(privateState.roleId) + "\nĐang chờ những người chơi còn lại…"
                    : "Chọn một lá úp. Máy chủ sẽ khóa lựa chọn và giữ bí mật vai trò.";

            if (roleCardsContainer != null) roleCardsContainer.gameObject.SetActive(true);
            RenderSheriffReveal(false, null);
            if (roleCardsContainer == null) return;

            foreach (var card in _cards) Destroy(card);
            _cards.Clear();
            int ownSlot = privateState != null ? privateState.draftRoleSlot : -1;
            int visibleCount = assigned && ownSlot >= 0 ? 1 : Mathf.Max(1, snapshot.draftSlotCount > 0 ? snapshot.draftSlotCount : snapshot.players.Count);
            ConfigureCardLayout(visibleCount);
            if (assigned && ownSlot >= 0)
            {
                CreateSlot(ownSlot, ownSlot, true, true);
            }
            else if (!roleLocked)
            {
                int count = snapshot.draftSlotCount > 0 ? snapshot.draftSlotCount : snapshot.players.Count;
                for (int i = 0; i < count; i++)
                    CreateSlot(i, ownSlot, false, snapshot.lockedDraftSlots != null && snapshot.lockedDraftSlots.Contains(i));
            }
        }

        private void RenderSheriffReveal(bool show, PlayerSnapshotDTO sheriff)
        {
            if (sheriffRevealRoot == null) return;
            sheriffRevealRoot.SetActive(show && sheriff != null);
            if (!show || sheriff == null) return;
            if (sheriffAvatarImage != null) sheriffAvatarImage.sprite = AvatarCatalog.Load(sheriff.avatarId, sheriff.id);
            if (sheriffNameText != null) sheriffNameText.text = sheriff.name.ToUpperInvariant();
            if (sheriffFirstTurnText != null) sheriffFirstTurnText.text = "CẢNH SÁT TRƯỞNG  •  +1 MÁU  •  ĐI LƯỢT ĐẦU";
            if (_animatedSheriffId == sheriff.id) return;
            _animatedSheriffId = sheriff.id;
            StartCoroutine(AnimateSheriffReveal());
        }

        private IEnumerator AnimateSheriffReveal()
        {
            var rect = sheriffRevealRoot.GetComponent<RectTransform>();
            if (sheriffRevealCanvasGroup != null) sheriffRevealCanvasGroup.alpha = 0f;
            rect.localScale = Vector3.one * 0.68f;
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
                rect.localScale = Vector3.one * Mathf.Lerp(0.68f, 1f, eased);
                if (sheriffRevealCanvasGroup != null) sheriffRevealCanvasGroup.alpha = eased;
                yield return null;
            }
            rect.localScale = Vector3.one;
            if (sheriffRevealCanvasGroup != null) sheriffRevealCanvasGroup.alpha = 1f;
            AudioManager.Instance?.PlaySFX("card_play");
        }

        private void CreateSlot(int slot, int ownSlot, bool assigned, bool locked)
        {
            var card = new GameObject("RoleSlot_" + slot, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(roleCardsContainer, false);
            bool isMine = slot == ownSlot;
            card.GetComponent<RectTransform>().sizeDelta = isMine && assigned ? new Vector2(230, 345) : _draftCardSize;
            var image = card.GetComponent<Image>();
            image.sprite = isMine && assigned
                ? CardCatalogDatabase.LoadSprite("role_cards/" + RoleSprite(GameStateStore.Instance.LocalPrivateState.roleId) + "_card")
                : CardCatalogDatabase.LoadCardBackSprite();
            image.preserveAspect = true;
            image.color = locked && !isMine ? new Color(0.35f, 0.35f, 0.35f, 0.72f) : Color.white;
            if (isMine)
            {
                var outline = card.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.78f, 0.2f);
                outline.effectDistance = new Vector2(4, -4);
            }
            var button = card.GetComponent<Button>();
            button.interactable = !locked && ownSlot < 0 && _snapshot.state == ServerGameState.ROLE_DRAFT && !GameStateStore.Instance.IsRequestPending;
            int capturedSlot = slot;
            button.onClick.AddListener(async () =>
            {
                AudioManager.Instance?.PlaySFX("card_draw");
                GameStateStore.Instance.SetRequestPending(true);
                bool sent = await GameStateStore.Instance.Gateway.PickRoleAsync(capturedSlot);
                if (!sent) GameStateStore.Instance.SetRequestPending(false);
            });
            _cards.Add(card);
        }

        private void ConfigureCardLayout(int count)
        {
            if (roleCardsContainer == null) return;
            var layout = roleCardsContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) return;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = count <= 5 ? 20f : 10f;

            // Scale card size based on screen height for responsive design
            float screenH = Screen.height > 0 ? Screen.height : 1080f;
            float baseH = Mathf.Clamp(screenH * 0.38f, 200f, 420f);
            float baseW = baseH / 1.5f;

            // On wide screens with many cards, shrink to fit
            float containerW = (roleCardsContainer as RectTransform)?.rect.width ?? 900f;
            if (containerW > 100f)
            {
                float maxWidthPerCard = (containerW - layout.spacing * (count - 1)) / count - 4f;
                if (maxWidthPerCard < baseW)
                {
                    baseW = Mathf.Max(110f, maxWidthPerCard);
                    baseH = baseW * 1.5f;
                }
            }

            _draftCardSize = new Vector2(baseW, baseH);
        }

        private static string RoleSprite(string role)
        {
            if (role == "outlaw") return "raider";
            if (role == "renegade") return "traitor";
            return role;
        }

        private static string GoalFor(string role)
        {
            switch (role)
            {
                case "sheriff": return "CẢNH SÁT TRƯỞNG — Loại toàn bộ Outlaw và Renegade.";
                case "deputy": return "PHÓ CẢNH SÁT — Bảo vệ Sheriff và loại phe đối địch.";
                case "outlaw": return "OUTLAW — Hạ Sheriff để giành chiến thắng.";
                default: return "RENEGADE — Trở thành người sống sót cuối cùng và hạ Sheriff sau cùng.";
            }
        }
    }
}

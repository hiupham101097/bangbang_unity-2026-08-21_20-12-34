using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;

namespace BangBang.UI.Views
{
    public sealed class RoleRevealView : MonoBehaviour
    {
        public Transform roleCardsContainer;
        public UnityEngine.UI.Text roleTitleText;
        public UnityEngine.UI.Text roleGoalText;
        public UnityEngine.UI.Button continueButton;
        public UnityEngine.UI.Text timerCountdownText;

        private readonly List<GameObject> _cards = new List<GameObject>();
        private MatchStateSnapshotDTO _snapshot;

        private void Start()
        {
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated += Render;
        }

        private void OnEnable()
        {
            Render(GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null);
        }

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
            if (roleTitleText != null)
                roleTitleText.text = snapshot.state == ServerGameState.ROLE_DRAFT
                    ? (assigned ? "VAI TRÒ CỦA BẠN" : "BƯỚC 1/3 — CHỌN VAI TRÒ")
                    : "CẢNH SÁT TRƯỞNG ĐÃ LỘ DIỆN";
            if (roleGoalText != null)
                roleGoalText.text = assigned ? GoalFor(privateState.roleId) : "Chọn một lá úp. Máy chủ sẽ khóa lựa chọn và giữ bí mật vai trò.";

            if (roleCardsContainer == null) return;
            foreach (var card in _cards) Destroy(card);
            _cards.Clear();
            int count = snapshot.draftSlotCount > 0 ? snapshot.draftSlotCount : snapshot.players.Count;
            int ownSlot = privateState != null ? privateState.draftRoleSlot : -1;
            for (int i = 0; i < count; i++)
                CreateSlot(i, ownSlot, assigned, snapshot.lockedDraftSlots != null && snapshot.lockedDraftSlots.Contains(i));
        }

        private void CreateSlot(int slot, int ownSlot, bool assigned, bool locked)
        {
            var card = new GameObject("RoleSlot_" + slot, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            card.transform.SetParent(roleCardsContainer, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 225);
            var image = card.GetComponent<UnityEngine.UI.Image>();
            bool isMine = slot == ownSlot;
            image.sprite = isMine && assigned
                ? CardCatalogDatabase.LoadSprite("role_cards/" + RoleSprite(GameStateStore.Instance.LocalPrivateState.roleId) + "_card")
                : CardCatalogDatabase.LoadSprite("card_back");
            image.color = locked && !isMine ? new Color(0.35f, 0.35f, 0.35f, 0.72f) : Color.white;
            if (isMine)
            {
                var outline = card.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(1f, 0.78f, 0.2f);
                outline.effectDistance = new Vector2(4, -4);
            }

            var button = card.GetComponent<UnityEngine.UI.Button>();
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

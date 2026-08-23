using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;

namespace BangBang.UI.Views
{
    public sealed class MatchStartSequenceUI : MonoBehaviour
    {
        private GameObject _root;
        private UnityEngine.UI.Text _title;
        private UnityEngine.UI.Text _subtitle;

        public void Initialize(GameObject root, UnityEngine.UI.Text title, UnityEngine.UI.Text subtitle)
        {
            _root = root;
            _title = title;
            _subtitle = subtitle;
        }

        private void Start()
        {
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated += Render;
            Render(GameStateStore.Instance != null ? GameStateStore.Instance.CurrentSnapshot : null);
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated -= Render;
        }

        private void Render(MatchStateSnapshotDTO snapshot)
        {
            if (_root == null) return;
            bool visible = snapshot != null && (snapshot.state == ServerGameState.INITIAL_DEAL || snapshot.state == ServerGameState.TURN_START);
            _root.SetActive(visible);
            if (!visible) return;

            var sheriff = snapshot.players != null ? snapshot.players.Find(p => p.publicRoleId == "sheriff") : null;
            if (_title != null) _title.text = snapshot.state == ServerGameState.INITIAL_DEAL ? "BƯỚC 3/3 — PHÁT BÀI" : "TRẬN ĐẤU BẮT ĐẦU";
            if (_subtitle != null)
            {
                int handCount = snapshot.privateState != null && snapshot.privateState.hand != null ? snapshot.privateState.hand.Count : 0;
                _subtitle.text = snapshot.state == ServerGameState.INITIAL_DEAL
                    ? "Bạn nhận " + handCount + " lá • Máy chủ đang chia bài"
                    : (sheriff != null ? "Sheriff " + sheriff.name + " đi trước" : "Sheriff đi trước");
            }
        }
    }
}

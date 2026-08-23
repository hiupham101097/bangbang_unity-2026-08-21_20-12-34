using System;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public sealed class MatchStartSequenceUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _subtitle;
        private Text _countdown;
        private Image _sheriffAvatar;
        private long _deadlineAt;

        public void Initialize(GameObject root, Text title, Text subtitle, Text countdown, Image sheriffAvatar)
        {
            _root = root;
            _title = title;
            _subtitle = subtitle;
            _countdown = countdown;
            _sheriffAvatar = sheriffAvatar;
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

        private void Update()
        {
            if (_root == null || !_root.activeSelf || _deadlineAt <= 0) return;
            long remaining = _deadlineAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining / 1000f));
            if (_countdown != null) _countdown.text = "TRẬN ĐẤU BẮT ĐẦU SAU " + seconds + " GIÂY";
            if (remaining < -1000) _root.SetActive(false);
        }

        private void Render(MatchStateSnapshotDTO snapshot)
        {
            if (_root == null) return;
            bool visible = snapshot != null && snapshot.state == ServerGameState.INITIAL_DEAL;
            _root.SetActive(visible);
            if (!visible) return;

            _deadlineAt = snapshot.deadlineAt;
            var sheriff = snapshot.players?.Find(player => player.publicRoleId == "sheriff");
            if (_title != null) _title.text = "CẢNH SÁT TRƯỞNG ĐÃ LỘ DIỆN";
            if (_subtitle != null) _subtitle.text = sheriff != null
                ? sheriff.name.ToUpperInvariant() + "  •  +1 MÁU  •  ĐI LƯỢT ĐẦU"
                : "CẢNH SÁT TRƯỞNG SẼ ĐI LƯỢT ĐẦU";
            if (_sheriffAvatar != null && sheriff != null)
                _sheriffAvatar.sprite = AvatarCatalog.Load(sheriff.avatarId, sheriff.id);
        }
    }
}

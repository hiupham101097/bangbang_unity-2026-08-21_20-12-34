using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;

namespace BangBang.UI
{
    public sealed class NetworkStatusOverlay : MonoBehaviour
    {
        private GameObject _root;
        private UnityEngine.UI.Text _status;
        private IGameGateway _gateway;

        public void Initialize(GameObject root, UnityEngine.UI.Text status)
        {
            _root = root;
            _status = status;
        }

        private void Start()
        {
            _gateway = GameStateStore.Instance != null ? GameStateStore.Instance.Gateway : null;
            if (_gateway != null) _gateway.OnConnectionStateChanged += Render;
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_gateway != null) _gateway.OnConnectionStateChanged -= Render;
        }

        private void Render(ConnectionState state)
        {
            bool inSession = GameStateStore.Instance != null && GameStateStore.Instance.CurrentSnapshot != null;
            bool show = inSession && state != ConnectionState.Connected;
            if (_root != null) _root.SetActive(show);
            if (!show || _status == null) return;
            _status.text = state == ConnectionState.Reconnecting
                ? "Đang kết nối lại và đồng bộ trận đấu…"
                : "Mất kết nối máy chủ — đang thử lại…";
        }
    }
}

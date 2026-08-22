using System;
using System.Collections.Generic;
using BangBang.Core.Network;
using UnityEngine;

namespace BangBang.Core.State
{
    public class GameStateStore : MonoBehaviour
    {
        public static GameStateStore Instance { get; private set; }

        public IGameGateway Gateway { get; private set; }
        public MatchStateSnapshotDTO CurrentSnapshot { get; private set; }
        public bool IsRequestPending { get; private set; }

        public string LocalPlayerId => Gateway != null ? Gateway.LocalPlayerId : "p_local";

        public PlayerSnapshotDTO LocalPlayer
        {
            get
            {
                if (CurrentSnapshot == null || CurrentSnapshot.players == null) return null;
                return CurrentSnapshot.players.Find(p => p.id == LocalPlayerId);
            }
        }

        public PrivatePlayerState LocalPrivateState
        {
            get
            {
                if (CurrentSnapshot == null) return null;
                return CurrentSnapshot.privateState;
            }
        }

        public event Action<MatchStateSnapshotDTO> OnStateSnapshotUpdated;
        public event Action<InteractionPromptDTO> OnActiveInteractionChanged;
        public event Action<string> OnCombatLogAdded;
        public event Action<bool> OnRequestPendingChanged;
        public event Action<string> OnGatewayErrorMessage;

        private int _lastLogCount;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void BindGateway(IGameGateway gateway)
        {
            if (Gateway != null)
            {
                Gateway.OnSnapshotReceived -= HandleSnapshotReceived;
                Gateway.OnInteractionReceived -= HandleInteractionReceived;
                Gateway.OnActionRejected -= HandleActionRejected;
                Gateway.OnErrorMessage -= HandleErrorMessage;
            }

            Gateway = gateway;

            if (Gateway != null)
            {
                Gateway.OnSnapshotReceived += HandleSnapshotReceived;
                Gateway.OnInteractionReceived += HandleInteractionReceived;
                Gateway.OnActionRejected += HandleActionRejected;
                Gateway.OnErrorMessage += HandleErrorMessage;
            }
        }

        public void SetRequestPending(bool pending)
        {
            IsRequestPending = pending;
            OnRequestPendingChanged?.Invoke(pending);
        }

        private void HandleSnapshotReceived(MatchStateSnapshotDTO snapshot)
        {
            SetRequestPending(false);
            CurrentSnapshot = snapshot;

            // Check new combat logs
            if (snapshot.combatLogs != null && snapshot.combatLogs.Count > _lastLogCount)
            {
                for (int i = _lastLogCount; i < snapshot.combatLogs.Count; i++)
                {
                    OnCombatLogAdded?.Invoke(snapshot.combatLogs[i]);
                }
                _lastLogCount = snapshot.combatLogs.Count;
            }

            OnStateSnapshotUpdated?.Invoke(snapshot);

            if (snapshot.activeInteraction != null)
            {
                OnActiveInteractionChanged?.Invoke(snapshot.activeInteraction);
            }
            else
            {
                OnActiveInteractionChanged?.Invoke(null);
            }
        }

        private void HandleInteractionReceived(InteractionPromptDTO interaction)
        {
            SetRequestPending(false);
            OnActiveInteractionChanged?.Invoke(interaction);
        }

        private void HandleActionRejected(string requestId, string reason)
        {
            SetRequestPending(false);
            Debug.LogWarning("[GameStateStore] Action rejected: " + reason);
            OnGatewayErrorMessage?.Invoke(reason);
        }

        private void HandleErrorMessage(string error)
        {
            SetRequestPending(false);
            Debug.LogError("[GameStateStore] Gateway Error: " + error);
            OnGatewayErrorMessage?.Invoke(error);
        }
    }
}

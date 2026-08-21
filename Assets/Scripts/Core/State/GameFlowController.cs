using System;
using BangBang.Core.Network;
using BangBang.UI.Views;
using UnityEngine;

namespace BangBang.Core.State
{
    public class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        [Header("Views References")]
        public LobbyView lobbyView;
        public WaitingRoomView waitingRoomView;
        public RoleRevealView roleRevealView;
        public CharacterSelectionView characterSelectionView;
        public GameTableView gameTableView;
        public ResultView resultView;

        public ServerGameState CurrentState { get; private set; } = ServerGameState.LOBBY;

        public event Action<ServerGameState> OnStateChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += HandleSnapshotUpdated;
            }

            // Default to Lobby
            TransitionToState(ServerGameState.LOBBY);
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= HandleSnapshotUpdated;
            }
        }

        private void HandleSnapshotUpdated(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null)
            {
                TransitionToState(ServerGameState.LOBBY);
                return;
            }

            if (snapshot.state != CurrentState)
            {
                TransitionToState(snapshot.state);
            }
        }

        public void TransitionToState(ServerGameState newState)
        {
            CurrentState = newState;

            // Manage View Visibilities
            if (lobbyView != null) lobbyView.gameObject.SetActive(newState == ServerGameState.LOBBY);
            if (waitingRoomView != null) waitingRoomView.gameObject.SetActive(newState == ServerGameState.WAITING);
            if (roleRevealView != null) roleRevealView.gameObject.SetActive(newState == ServerGameState.DEALING_ROLES);
            if (characterSelectionView != null) characterSelectionView.gameObject.SetActive(newState == ServerGameState.SELECTING_CHARACTER);
            
            bool isTableState = newState == ServerGameState.INITIALIZING || 
                                newState == ServerGameState.PLAYING || 
                                newState == ServerGameState.WAITING_RESPONSE || 
                                newState == ServerGameState.TURN_ENDING;
            if (gameTableView != null) gameTableView.gameObject.SetActive(isTableState);

            if (resultView != null) resultView.gameObject.SetActive(newState == ServerGameState.FINISHED);

            OnStateChanged?.Invoke(newState);
        }
    }
}

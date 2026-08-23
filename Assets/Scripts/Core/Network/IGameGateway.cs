using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BangBang.Core.Network
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public interface IGameGateway
    {
        ConnectionState CurrentConnectionState { get; }
        string LocalPlayerId { get; }

        event Action<MatchStateSnapshotDTO> OnSnapshotReceived;
        event Action<InteractionPromptDTO> OnInteractionReceived;
        event Action<string, string> OnActionRejected; // requestId, reason
        event Action<List<RoomSummaryDTO>> OnRoomListUpdated;
        event Action<ConnectionState> OnConnectionStateChanged;
        event Action<string> OnErrorMessage;

        Task<bool> InitializeSessionAsync(string deviceId, string displayName);
        Task<bool> RefreshRoomListAsync();
        Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool isPrivate, string password, int turnSeconds);
        Task<bool> JoinRoomAsync(string roomCodeOrId, string password = "");
        Task<bool> LeaveRoomAsync();
        Task<bool> ToggleReadyAsync(bool isReady);
        Task<bool> AddBotAsync();
        Task<bool> StartGameAsync();
        Task<bool> SelectCharacterAsync(string characterId);
        Task<bool> RequestDrawAsync();
        Task<bool> PlayCardAsync(string cardId, List<string> targetPlayerIds = null, List<string> selectedCardIds = null);
        Task<bool> SubmitInteractionAsync(string interactionId, string action, List<string> selectedPlayers = null, List<string> selectedCards = null, int optionIndex = 0);
        Task<bool> EndTurnAsync(List<string> discardCardIds = null);
        Task<bool> RequestRematchAsync();
    }
}

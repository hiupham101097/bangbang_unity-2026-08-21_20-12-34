using System;
using System.Collections.Generic;

namespace BangBang.Core.Network
{
    public enum ServerGameState
    {
        LOBBY,
        WAITING,
        DEALING_ROLES,
        SELECTING_CHARACTER,
        INITIALIZING,
        PLAYING,
        WAITING_RESPONSE,
        TURN_ENDING,
        FINISHED
    }

    public enum InteractionType
    {
        SELECT_TARGET,
        SELECT_CARD,
        SELECT_CARDS,
        CHOOSE_OPTION,
        RESPOND,
        DISCARD
    }

    [Serializable]
    public class RoomCreatedResponseDTO
    {
        public string roomId;
    }

    [Serializable]
    public class InteractionPromptDTO
    {
        public string interactionId;
        public string type; // InteractionType as string
        public string actorPlayerId;
        public string title;
        public string message;
        public int minSelections = 1;
        public int maxSelections = 1;
        public List<string> validPlayerIds = new List<string>();
        public List<string> validCardIds = new List<string>();
        public List<string> options = new List<string>();
        public long expiresAt; // Unix timestamp in ms from server
        public bool canCancel;
        public string defaultAction; // e.g. "take_damage", "random_discard"
    }

    [Serializable]
    public class PlayerSnapshotDTO
    {
        public string id;
        public string name;
        public int seat;
        public bool isHost;
        public bool isReady;
        public bool isConnected = true;
        public bool isAlive = true;
        public int currentHealth;
        public int maxHealth;
        public string characterId;
        public List<string> characterOptions = new List<string>();
        public string role; // Only populated for local player or revealed Sheriff/dead players
        public bool isRoleRevealed;
        public int handCount;
        public List<string> hand = new List<string>(); // ONLY for local player, empty for opponents
        public List<string> equipment = new List<string>(); // Public equipment cards
        public int effectiveDistanceToLocal = 1; // Server-calculated distance
        public bool isTargetable; // Flagged by server during targeting interactions
    }

    [Serializable]
    public class MatchStateSnapshotDTO
    {
        public string roomId;
        public string roomCode;
        public string hostPlayerId;
        public ServerGameState state;
        public string currentTurnPlayerId;
        public string currentPhase; // "draw", "play", "discard"
        public int turnNumber;
        public List<PlayerSnapshotDTO> players = new List<PlayerSnapshotDTO>();
        public int drawPileCount;
        public string topDiscardCardId;
        public int discardPileCount;
        public InteractionPromptDTO activeInteraction;
        public string winnerRole; // Populate when FINISHED
        public string winnerTeam;
        public List<string> combatLogs = new List<string>();
        public long serverTime;
        public int sequence;
    }

    [Serializable]
    public class ClientActionRequestDTO
    {
        public string requestId;
        public string roomId;
        public string playerId;
        public string action; // "CREATE_ROOM", "JOIN_ROOM", "READY", "START_GAME", "SELECT_CHARACTER", "DRAW", "PLAY_CARD", "INTERACTION_RESPONSE", "END_TURN", "REMATCH"
        public string cardId;
        public List<string> targetPlayerIds = new List<string>();
        public List<string> selectedCardIds = new List<string>();
        public int optionIndex;
        public string interactionId;
    }

    [Serializable]
    public class RoomSummaryDTO
    {
        public string roomId;
        public string roomName;
        public string roomCode;
        public int currentPlayers;
        public int maxPlayers;
        public bool isPrivate;
        public int turnTimeSeconds;
        public int pingMs;
    }
}

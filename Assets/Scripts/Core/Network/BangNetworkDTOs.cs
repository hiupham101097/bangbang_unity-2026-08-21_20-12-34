using System;
using System.Collections.Generic;

namespace BangBang.Core.Network
{
    [Serializable]
    public class ChatMessageDTO
    {
        public string playerId;
        public string playerName;
        public string message;
        public long sentAt;
    }

    [Serializable]
    public class SessionResumeRequestDTO
    {
        public string deviceId;
        public string clientVersion;
        public string accessToken;
    }

    [Serializable]
    public class SessionReadyDTO
    {
        public string playerId;
        public bool resumed;
        public long serverTime;
        public string accessToken;
    }

    public enum ServerGameState
    {
        LOBBY,
        WAITING,
        ROLE_DRAFT,
        ROLE_LOCK_WAIT,
        CHARACTER_DRAFT,
        CHARACTER_REVEAL,
        INITIAL_DEAL,
        TURN_START,
        JUDGEMENT,
        DRAW,
        PLAY,
        RESPONSE,
        DISCARD,
        GAME_OVER
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
        public string playerId;
        public bool success;
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
        public int requiredCount = 1;
        public string requiredCardType;
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
        public string avatarId;
        public int seat;
        public bool isBot;
        public bool isHost;
        public bool isReady;
        public bool isConnected = true;
        public bool isAlive = true;
        public int currentHealth;
        public int maxHealth;
        public string characterId;
        public string publicRoleId;
        public bool isRoleRevealed;
        public int handCount;
        public List<string> equipment = new List<string>();
        public int effectiveDistanceToLocal = 1;
        public bool isTargetable;
    }

    [Serializable]
    public class PrivatePlayerState
    {
        public string roleId;
        public List<string> hand = new List<string>();
        public List<string> draftCharacterOptions = new List<string>();
        public int draftRoleSlot = -1;
        public List<int> draftCharacterSlots = new List<int>();
        public string selectedCharacterId;
    }
    
    [Serializable]
    public class RuleConfig
    {
        public int maxPlayers;
        public int botCount;
        public int turnTimeSec;
        public string startingHandMode;
        public int roleDraftSec;
        public int characterDraftSec;
        public int responseTimeSec;
    }

    [Serializable]
    public class MatchStateSnapshotDTO
    {
        public string roomId;
        public string roomCode;
        public string hostPlayerId;
        public ServerGameState state;
        public string phaseId;
        public long deadlineAt;
        public int draftSlotCount;
        public List<int> lockedDraftSlots = new List<int>();
        public string judgementCard;
        public string judgementEffect;
        public string judgementResult;
        public string currentTurnPlayerId;
        public string currentPhase; // "draw", "play", "discard"
        public int turnNumber;
        public List<PlayerSnapshotDTO> players = new List<PlayerSnapshotDTO>();
        public PrivatePlayerState privateState;
        public int drawPileCount;
        public string topDiscardCardId;
        public int discardPileCount;
        public InteractionPromptDTO activeInteraction;
        public string winnerRole; // Populate when FINISHED
        public string winnerTeam;
        public List<string> combatLogs = new List<string>();
        public long serverTime;
        public int sequence;
        public int revision;
        public RuleConfig rules;
    }

    [Serializable]
    public class ClientActionRequestDTO
    {
        public string requestId;
        public string roomId;
        public string playerId;
        public string action; // "CREATE_ROOM", "JOIN_ROOM", "READY", "START_GAME", "SELECT_CHARACTER", "DRAW", "PLAY_CARD", "INTERACTION_RESPONSE", "END_TURN", "REMATCH"
        public string cardId;
        public string characterId;
        public List<string> targetPlayerIds = new List<string>();
        public List<string> selectedCardIds = new List<string>();
        public int optionIndex;
        public string interactionId;
        public string actionId;
        public int stateRevision;
    }

    [Serializable]
    public class DiscardRequestDTO
    {
        public string actionId;
        public int stateRevision;
        public List<string> cardIds = new List<string>();
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

    [Serializable]
    public class RoomSummaryListDTO
    {
        public List<RoomSummaryDTO> items = new List<RoomSummaryDTO>();
    }
}

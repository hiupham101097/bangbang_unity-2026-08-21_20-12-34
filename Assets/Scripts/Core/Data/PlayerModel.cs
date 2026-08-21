using System;
using System.Collections.Generic;
using UnityEngine;

namespace BangBang.Core.Data
{
    [Serializable]
    public class PlayerModel
    {
        public string id;
        public string name;
        public int seat;
        public bool isBot;
        public bool isReady;
        public bool isAlive = true;
        public int health = 4;
        public int maxHealth = 4;
        public int cardCount;

        public RoleType role = RoleType.Outlaw;
        public bool isRoleRevealed;
        public string characterId;
        public CharacterInfo character;
        public List<string> characterOptions = new List<string>();
        public bool characterChosen;

        // Cards
        public List<string> hand = new List<string>();
        public List<string> equipment = new List<string>();
        public int weaponRange = 1;

        // Modifiers
        public bool hasMustang;
        public bool hasAppaloosa;
        public bool hasBarrel;
        public bool hasDynamite;
        public bool isInJail;
        public bool hasVolcanic; // can shoot unlimited Bangs

        public void ResetModifiers()
        {
            weaponRange = 1;
            hasMustang = false;
            hasAppaloosa = false;
            hasBarrel = false;
            hasDynamite = false;
            isInJail = false;
            hasVolcanic = false;

            foreach (var eq in equipment)
            {
                var cardType = CardCatalogDatabase.GetTypeOf(eq);
                if (cardType == "gun_range_2") weaponRange = 2;
                else if (cardType == "gun_range_3") weaponRange = 3;
                else if (cardType == "gun_range_4") weaponRange = 4;
                else if (cardType == "gun_range_5") weaponRange = 5;
                else if (cardType == "volcanic") { weaponRange = 1; hasVolcanic = true; }
                else if (cardType == "mustang") hasMustang = true;
                else if (cardType == "appaloosa") hasAppaloosa = true;
                else if (cardType == "barrel") hasBarrel = true;
                else if (cardType == "dynamite") hasDynamite = true;
                else if (cardType == "jail") isInJail = true;
            }
        }
    }

    [Serializable]
    public class PendingActionModel
    {
        public string id;
        public string actorPlayerId;
        public string targetPlayerId;
        public string actionType; // bang, gatling, indiani, duello, general_store, rescue
        public string requiredCardType; // dodge, bang
        public int requiredDodges = 1;
        public long deadline;
        public string cardId;
        public List<string> openedCardIds = new List<string>();
        public List<string> choices = new List<string>();
    }

    public enum GamePhase
    {
        Lobby,
        RoleSelection,
        RoleReveal,
        CharacterSelection,
        ChoosingCharacter,
        TurnStart,
        PlayPhase,
        WaitingResponse,
        DiscardPhase,
        GameOver
    }

    [Serializable]
    public class MatchStateModel
    {
        public string id;
        public string code;
        public string hostId;
        public int maxPlayers = 7;
        public int turnDurationSeconds = 25;
        public string status = "waiting"; // waiting, starting, playing, finished
        public GamePhase phase = GamePhase.Lobby;
        public List<PlayerModel> players = new List<PlayerModel>();
        public List<string> deck = new List<string>();
        public List<string> discard = new List<string>();
        public string currentTurnPlayerId;
        public int turnNumber;
        public long turnDeadline;
        public int bangUsedThisTurn;
        public PendingActionModel pendingBang;
        public string winner; // sheriff, outlaw, renegade
        public List<string> publicLog = new List<string>();
        public string lastPlayedCardId;
        public string lastActionActorId;
        public string lastActionTargetId;
    }
}

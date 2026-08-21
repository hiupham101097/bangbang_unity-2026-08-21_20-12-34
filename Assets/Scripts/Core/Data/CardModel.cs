using System;
using System.Collections.Generic;
using UnityEngine;

namespace BangBang.Core.Data
{
    public enum CardType
    {
        BrownAction,    // Instant action, discarded after use (Bang, Dodge, Beer, etc.)
        BlueEquipment,  // Weapon or item that stays equipped on table (Mustang, Barrel, Guns, etc.)
    }

    public enum CardSuit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs
    }

    [Serializable]
    public class CardInfo
    {
        public string id;
        public string code; // raw card id like "bang_1_heart_ace"
        public string name;
        public string vietnameseName;
        public string description;
        public CardType type;
        public CardSuit suit;
        public string rank; // "A", "2".."10", "J", "Q", "K"
        public string resourcePath;
        public int rangeModifier; // for guns (2..5) or Mustang (+1 def) / Appaloosa (-1 dist)
        public bool requiresTarget;
        public bool targetAnyRange; // e.g. Cat Balou, Duel
        public bool targetRangeOne; // e.g. Panico

        public CardInfo() { }

        public CardInfo(string id, string vietnameseName, string description, CardType type, string resourcePath, bool requiresTarget = false, int rangeMod = 0)
        {
            this.id = id;
            this.name = id.ToUpper();
            this.vietnameseName = vietnameseName;
            this.description = description;
            this.type = type;
            this.resourcePath = resourcePath;
            this.requiresTarget = requiresTarget;
            this.rangeModifier = rangeMod;
        }
    }
}

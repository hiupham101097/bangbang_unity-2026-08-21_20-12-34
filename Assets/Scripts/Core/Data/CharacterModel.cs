using System;
using UnityEngine;

namespace BangBang.Core.Data
{
    [Serializable]
    public class CharacterInfo
    {
        public string id;
        public string name;
        public string abilityName;
        public string abilityDescription;
        public string description => abilityDescription;
        public int maxHealth;
        public string resourcePath;

        public CharacterInfo() { }

        public CharacterInfo(string id, string name, string abilityName, string abilityDescription, int maxHealth, string resourcePath)
        {
            this.id = id;
            this.name = name;
            this.abilityName = abilityName;
            this.abilityDescription = abilityDescription;
            this.maxHealth = maxHealth;
            this.resourcePath = resourcePath;
        }
    }

    public enum RoleType
    {
        Sheriff,    // Cảnh Trưởng (Lộ diện, +1 Máu, Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội)
        Deputy,     // Phó Cảnh Trưởng (Ẩn danh, Bảo vệ Cảnh Trưởng)
        Outlaw,     // Cướp / Raider (Ẩn danh, Tiêu diệt Cảnh Trưởng)
        Renegade    // Kẻ Phản Bội / Traitor (Ẩn danh, Sống sót cuối cùng, giết Cảnh Trưởng sau cùng)
    }

    [Serializable]
    public class RoleInfo
    {
        public RoleType type;
        public string name;
        public string vietnameseName;
        public string goalDescription;
        public bool isRevealed;
        public string resourcePath;

        public RoleInfo(RoleType type, string vietnameseName, string goalDescription, string resourcePath, bool isRevealed = false)
        {
            this.type = type;
            this.name = type.ToString().ToLower();
            this.vietnameseName = vietnameseName;
            this.goalDescription = goalDescription;
            this.resourcePath = resourcePath;
            this.isRevealed = isRevealed;
        }
    }
}

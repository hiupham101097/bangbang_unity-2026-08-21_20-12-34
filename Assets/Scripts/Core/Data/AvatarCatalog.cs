using System;
using UnityEngine;

namespace BangBang.Core.Data
{
    public static class AvatarCatalog
    {
        public const string PlayerPrefsKey = "bang_avatar_id";
        private static readonly string[] DefaultIds =
        {
            "quick_jack", "iron_rose", "doctor_lee", "lucky_joe2",
            "role_sheriff", "role_deputy", "role_raider", "role_guardian", "role_traitor"
        };

        public static string SelectedId
        {
            get => PlayerPrefs.GetString(PlayerPrefsKey, DefaultIds[0]);
            set
            {
                PlayerPrefs.SetString(PlayerPrefsKey, Normalize(value));
                PlayerPrefs.Save();
            }
        }

        public static string Normalize(string avatarId)
        {
            if (!string.IsNullOrEmpty(avatarId) && avatarId.EndsWith("_0", StringComparison.Ordinal))
                avatarId = avatarId.Substring(0, avatarId.Length - 2);
            return Array.IndexOf(DefaultIds, avatarId) >= 0 ? avatarId : DefaultIds[0];
        }

        public static string ForPlayer(string avatarId, string playerId)
        {
            if (!string.IsNullOrEmpty(avatarId)) return Normalize(avatarId);
            int hash = string.IsNullOrEmpty(playerId) ? 0 : playerId.GetHashCode();
            return DefaultIds[(hash & int.MaxValue) % DefaultIds.Length];
        }

        public static Sprite Load(string avatarId, string playerId = null) =>
            Resources.Load<Sprite>("avatar/" + ForPlayer(avatarId, playerId));

        public static Sprite[] LoadAll() => Resources.LoadAll<Sprite>("avatar");
    }
}

using System;
using System.Linq;
using BangBang.Core.Data;
using BangBang.Core.Network;

namespace BangBang.Core.Logic
{
    /// <summary>
    /// Client-side projection of the server rules used to present legal actions.
    /// The server remains authoritative; this class prevents the UI from offering
    /// obviously invalid cards and targets while a snapshot is being displayed.
    /// </summary>
    public static class MatchActionRules
    {
        public static bool IsLocalPlayPhase(MatchStateSnapshotDTO snapshot, string localPlayerId)
        {
            return snapshot != null &&
                   snapshot.state == ServerGameState.PLAY &&
                   string.Equals(snapshot.currentPhase, "PLAY", StringComparison.OrdinalIgnoreCase) &&
                   snapshot.currentTurnPlayerId == localPlayerId;
        }

        public static bool CanSelectCard(
            MatchStateSnapshotDTO snapshot,
            string localPlayerId,
            string cardId,
            out string blockedReason)
        {
            blockedReason = string.Empty;
            if (snapshot == null)
            {
                blockedReason = "Đang chờ đồng bộ bàn đấu.";
                return false;
            }

            var local = snapshot.players?.Find(player => player.id == localPlayerId);
            if (local == null || !local.isAlive)
            {
                blockedReason = "Bạn đang ở chế độ xem trận.";
                return false;
            }

            if (snapshot.currentTurnPlayerId != localPlayerId)
            {
                blockedReason = "Hãy chờ đến lượt của bạn.";
                return false;
            }

            if (!string.Equals(snapshot.currentPhase, "PLAY", StringComparison.OrdinalIgnoreCase))
            {
                blockedReason = string.Equals(snapshot.currentPhase, "DRAW", StringComparison.OrdinalIgnoreCase)
                    ? "Bạn cần rút bài trước khi đánh bài."
                    : "Chưa đến bước đánh bài.";
                return false;
            }

            if (snapshot.privateState?.hand == null || !snapshot.privateState.hand.Contains(cardId))
            {
                blockedReason = "Lá bài này không còn trên tay.";
                return false;
            }

            string type = CardCatalogDatabase.GetTypeOf(cardId);
            if (type == "dodge" && !ActsAsBang(local, type))
            {
                blockedReason = "NÉ chỉ dùng khi phản ứng với đòn tấn công.";
                return false;
            }

            if (type == "beer" && local.currentHealth >= local.maxHealth)
            {
                blockedReason = "Bạn đang đầy Máu nên chưa thể dùng BIA.";
                return false;
            }

            if (RequiresTarget(snapshot, localPlayerId, cardId) &&
                !snapshot.players.Any(player => IsValidTarget(snapshot, localPlayerId, player.id, cardId)))
            {
                blockedReason = "Hiện không có mục tiêu hợp lệ cho lá bài này.";
                return false;
            }

            return true;
        }

        public static bool RequiresTarget(MatchStateSnapshotDTO snapshot, string localPlayerId, string cardId)
        {
            var info = CardCatalogDatabase.GetCardInfo(cardId);
            var local = snapshot?.players?.Find(player => player.id == localPlayerId);
            return info.requiresTarget || (local != null && ActsAsBang(local, CardCatalogDatabase.GetTypeOf(cardId)));
        }

        public static bool IsValidTarget(
            MatchStateSnapshotDTO snapshot,
            string localPlayerId,
            string targetPlayerId,
            string cardId)
        {
            if (snapshot?.players == null || string.IsNullOrEmpty(targetPlayerId) || targetPlayerId == localPlayerId)
                return false;

            var local = snapshot.players.Find(player => player.id == localPlayerId);
            var target = snapshot.players.Find(player => player.id == targetPlayerId);
            if (local == null || target == null || !local.isAlive || !target.isAlive) return false;

            string type = CardCatalogDatabase.GetTypeOf(cardId);
            var info = CardCatalogDatabase.GetCardInfo(cardId);

            if (type == "bang" || ActsAsBang(local, type))
                return target.isTargetable && target.effectiveDistanceToLocal <= GetWeaponRange(local);
            if (type == "panico")
                return target.effectiveDistanceToLocal <= 1 &&
                       (target.handCount > 0 || (target.equipment != null && target.equipment.Count > 0));
            if (type == "cat_balou")
                return target.handCount > 0 || (target.equipment != null && target.equipment.Count > 0);
            if (type == "jail")
                return target.publicRoleId != "sheriff" &&
                       (target.equipment == null || !target.equipment.Any(item => CardCatalogDatabase.GetTypeOf(item) == "jail"));
            if (type == "duello" || info.targetAnyRange) return true;
            if (info.targetRangeOne) return target.effectiveDistanceToLocal <= 1;
            return info.requiresTarget && target.isTargetable;
        }

        private static bool ActsAsBang(PlayerSnapshotDTO local, string cardType)
        {
            return cardType == "dodge" && local.characterId == "calamity_janet";
        }

        public static int GetWeaponRange(PlayerSnapshotDTO player)
        {
            int range = 1;
            if (player?.equipment == null) return range;
            foreach (string card in player.equipment)
            {
                switch (CardCatalogDatabase.GetTypeOf(card))
                {
                    case "gun_range_2": range = Math.Max(range, 2); break;
                    case "gun_range_3": range = Math.Max(range, 3); break;
                    case "gun_range_4": range = Math.Max(range, 4); break;
                    case "gun_range_5": range = Math.Max(range, 5); break;
                }
            }
            return range;
        }
    }
}

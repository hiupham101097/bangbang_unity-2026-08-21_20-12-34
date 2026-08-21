using System;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Data;
using UnityEngine;

namespace BangBang.Core.Logic
{
    public static class BangGameRules
    {
        /// <summary>
        /// Calculates the cyclic distance between attacker and target on the table,
        /// accounting for alive players, Mustang, Appaloosa, Paul Regret, and Rose Doolan.
        /// </summary>
        public static int CalculateDistance(MatchStateModel state, string attackerId, string targetId)
        {
            if (attackerId == targetId) return 0;

            var alivePlayers = state.players.Where(p => p.isAlive).OrderBy(p => p.seat).ToList();
            var attackerIndex = alivePlayers.FindIndex(p => p.id == attackerId);
            var targetIndex = alivePlayers.FindIndex(p => p.id == targetId);

            if (attackerIndex == -1 || targetIndex == -1) return 99;

            int count = alivePlayers.Count;
            int directDistance = Math.Abs(attackerIndex - targetIndex);
            int cyclicDistance = Math.Min(directDistance, count - directDistance);

            var attacker = alivePlayers[attackerIndex];
            var target = alivePlayers[targetIndex];

            // Attacker modifiers (sees target closer)
            int sightBonus = 0;
            if (attacker.hasAppaloosa || attacker.characterId == "rose_oolan")
            {
                sightBonus += 1;
            }

            // Target modifiers (target appears farther)
            int targetDefense = 0;
            if (target.hasMustang || target.characterId == "paul_regret")
            {
                targetDefense += 1;
            }

            int finalDistance = cyclicDistance + targetDefense - sightBonus;
            return Math.Max(1, finalDistance);
        }

        public static bool CanReachTarget(MatchStateModel state, string attackerId, string targetId, int weaponRange)
        {
            int distance = CalculateDistance(state, attackerId, targetId);
            return distance <= weaponRange;
        }

        public static List<PlayerModel> GetValidTargets(MatchStateModel state, string attackerId, string cardId)
        {
            var attacker = state.players.Find(p => p.id == attackerId);
            if (attacker == null || !attacker.isAlive) return new List<PlayerModel>();

            var type = CardCatalogDatabase.GetTypeOf(cardId);
            var aliveEnemies = state.players.Where(p => p.isAlive && p.id != attackerId).ToList();

            if (type == "bang")
            {
                int weaponRange = attacker.weaponRange;
                return aliveEnemies.Where(target => CanReachTarget(state, attackerId, target.id, weaponRange)).ToList();
            }
            if (type == "panico")
            {
                return aliveEnemies.Where(target => CalculateDistance(state, attackerId, target.id) <= 1 && (target.hand.Count > 0 || target.equipment.Count > 0)).ToList();
            }
            if (type == "cat_balou" || type == "duello")
            {
                return aliveEnemies.Where(target => target.hand.Count > 0 || target.equipment.Count > 0 || type == "duello").ToList();
            }
            if (type == "jail")
            {
                // Cannot put Sheriff in jail or someone already in jail
                return aliveEnemies.Where(target => target.role != RoleType.Sheriff && !target.isInJail).ToList();
            }

            return new List<PlayerModel>();
        }

        public static bool CanPlayCard(MatchStateModel state, string playerId, string cardId)
        {
            var player = state.players.Find(p => p.id == playerId);
            if (player == null || !player.isAlive) return false;
            if (state.currentTurnPlayerId != playerId) return false;
            if (state.phase != GamePhase.PlayPhase) return false;

            var type = CardCatalogDatabase.GetTypeOf(cardId);

            if (type == "bang" || (player.characterId == "calamity_janet" && type == "dodge"))
            {
                bool unlimited = player.hasVolcanic || player.characterId == "willy_the_kid";
                if (!unlimited && state.bangUsedThisTurn >= 1) return false;

                return GetValidTargets(state, playerId, "bang").Count > 0;
            }

            if (type == "beer")
            {
                int aliveCount = state.players.Count(p => p.isAlive);
                if (aliveCount <= 2 && player.health >= player.maxHealth) return false;
                return player.health < player.maxHealth;
            }

            if (type == "panico" || type == "cat_balou" || type == "duello" || type == "jail")
            {
                return GetValidTargets(state, playerId, cardId).Count > 0;
            }

            if (type == "dynamite")
            {
                return !player.hasDynamite;
            }

            return true;
        }

        public static int GetRequiredDodges(PlayerModel attacker)
        {
            if (attacker != null && attacker.characterId == "slab_the_killer")
            {
                return 2; // Slab the killer requires 2 dodges!
            }
            return 1;
        }

        public static string CheckGameOverWinner(MatchStateModel state)
        {
            var sheriff = state.players.Find(p => p.role == RoleType.Sheriff);
            var alivePlayers = state.players.Where(p => p.isAlive).ToList();

            if (sheriff != null && !sheriff.isAlive)
            {
                if (alivePlayers.Count == 1 && alivePlayers[0].role == RoleType.Renegade)
                {
                    return "renegade";
                }
                return "outlaw";
            }

            bool badGuysDead = alivePlayers.All(p => p.role == RoleType.Sheriff || p.role == RoleType.Deputy);
            if (badGuysDead)
            {
                return "sheriff";
            }

            return null;
        }
    }
}

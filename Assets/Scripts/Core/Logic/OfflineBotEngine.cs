using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BangBang.Core.Data;
using UnityEngine;

namespace BangBang.Core.Logic
{
    public class OfflineBotEngine : MonoBehaviour
    {
        public static OfflineBotEngine Instance { get; private set; }

        public MatchStateModel State { get; private set; }
        public event Action<MatchStateModel> OnStateChanged;
        public event Action<string, string> OnCombatLog; // message, actionType
        public event Action<List<string>> OnGeneralStoreOpened; // opened cards

        private Coroutine _turnLoopCoroutine;
        private System.Random _rnd = new System.Random();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void StartNewMatch(int totalPlayers = 5, string localPlayerName = "Cao bồi bạn")
        {
            if (_turnLoopCoroutine != null) StopCoroutine(_turnLoopCoroutine);

            State = new MatchStateModel
            {
                id = "offline_" + Guid.NewGuid().ToString().Substring(0, 6),
                code = "SALOON",
                status = "playing",
                phase = GamePhase.PlayPhase,
                maxPlayers = totalPlayers
            };

            // Setup Roles
            List<RoleType> roles = new List<RoleType>();
            if (totalPlayers == 4) roles.AddRange(new[] { RoleType.Sheriff, RoleType.Renegade, RoleType.Outlaw, RoleType.Outlaw });
            else if (totalPlayers == 5) roles.AddRange(new[] { RoleType.Sheriff, RoleType.Deputy, RoleType.Outlaw, RoleType.Outlaw, RoleType.Renegade });
            else if (totalPlayers == 6) roles.AddRange(new[] { RoleType.Sheriff, RoleType.Deputy, RoleType.Outlaw, RoleType.Outlaw, RoleType.Outlaw, RoleType.Renegade });
            else roles.AddRange(new[] { RoleType.Sheriff, RoleType.Deputy, RoleType.Deputy, RoleType.Outlaw, RoleType.Outlaw, RoleType.Outlaw, RoleType.Renegade });

            roles = roles.OrderBy(x => _rnd.Next()).ToList();

            // Setup 16 Characters
            var allCharIds = new List<string> {
                "willy_the_kid", "bart_cassidy", "black_jack", "calamity_janet",
                "el_gringo", "jesse_jones", "jourdonnais", "kit_carlson",
                "lucky_duke", "paul_regret", "pedro_ramirez", "rose_oolan",
                "sid_ketchum", "slab_the_killer", "suzy_lafayette", "vulture_sam"
            }.OrderBy(x => _rnd.Next()).ToList();

            // 80 Card Deck
            State.deck = GenerateStandardDeck().OrderBy(x => _rnd.Next()).ToList();
            State.discard = new List<string>();

            for (int i = 0; i < totalPlayers; i++)
            {
                var role = roles[i];
                var charId = allCharIds[i % allCharIds.Count];
                var charInfo = CardCatalogDatabase.GetCharacterInfo(charId);

                int maxHp = charInfo.maxHealth + (role == RoleType.Sheriff ? 1 : 0);

                var p = new PlayerModel
                {
                    id = i == 0 ? "player_local" : "bot_" + i,
                    name = i == 0 ? localPlayerName : GetBotName(i),
                    seat = i,
                    isBot = i != 0,
                    isReady = true,
                    isAlive = true,
                    role = role,
                    isRoleRevealed = role == RoleType.Sheriff,
                    characterId = charId,
                    character = charInfo,
                    characterChosen = true,
                    health = maxHp,
                    maxHealth = maxHp
                };

                for (int c = 0; c < maxHp; c++)
                {
                    if (State.deck.Count > 0)
                    {
                        var card = State.deck[0];
                        State.deck.RemoveAt(0);
                        p.hand.Add(card);
                    }
                }
                p.cardCount = p.hand.Count;
                p.ResetModifiers();
                State.players.Add(p);
            }

            var sheriff = State.players.Find(p => p.role == RoleType.Sheriff);
            State.currentTurnPlayerId = sheriff != null ? sheriff.id : State.players[0].id;
            State.turnNumber = 1;
            State.bangUsedThisTurn = 0;

            Log("🔥 Chào mừng đến Quán Rượu Saloon! Cảnh Trưởng " + (sheriff != null ? sheriff.name : "") + " đi đầu tiên.", "system");
            OnStateChanged?.Invoke(State);

            _turnLoopCoroutine = StartCoroutine(TurnExecutionLoop());
        }

        private IEnumerator TurnExecutionLoop()
        {
            while (State.status == "playing")
            {
                var currentPlayer = State.players.Find(p => p.id == State.currentTurnPlayerId);
                if (currentPlayer == null || !currentPlayer.isAlive)
                {
                    AdvanceToNextPlayer();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                State.bangUsedThisTurn = 0;
                State.phase = GamePhase.TurnStart;
                Log("👉 Đến lượt: " + currentPlayer.name + " (" + currentPlayer.character.name + ")", "turn");
                OnStateChanged?.Invoke(State);
                yield return new WaitForSeconds(0.8f);

                // 1. Check Dynamite
                if (currentPlayer.hasDynamite)
                {
                    Log(currentPlayer.name + " kiểm tra ngòi nổ Thuốc Nổ Dynamite...", "dynamite");
                    yield return new WaitForSeconds(1.0f);

                    bool exploded = false;
                    // Lucky Duke flips 2 cards
                    int attempts = currentPlayer.characterId == "lucky_duke" ? 2 : 1;
                    for (int a = 0; a < attempts; a++)
                    {
                        if (_rnd.Next(0, 100) < 12) exploded = true;
                    }

                    if (exploded)
                    {
                        currentPlayer.health = Math.Max(0, currentPlayer.health - 3);
                        currentPlayer.equipment.RemoveAll(e => CardCatalogDatabase.GetTypeOf(e) == "dynamite");
                        currentPlayer.ResetModifiers();
                        Log("💣 BÙÙÙM! Thuốc nổ nổ tung! " + currentPlayer.name + " mất 3 Máu!", "explosion");
                        CheckDeath(currentPlayer, null);
                        OnStateChanged?.Invoke(State);
                        if (!currentPlayer.isAlive)
                        {
                            AdvanceToNextPlayer();
                            continue;
                        }
                    }
                    else
                    {
                        currentPlayer.equipment.RemoveAll(e => CardCatalogDatabase.GetTypeOf(e) == "dynamite");
                        currentPlayer.ResetModifiers();
                        var nextAlive = GetNextAlivePlayer(currentPlayer);
                        if (nextAlive != null) nextAlive.equipment.Add("dynamite_passed");
                        nextAlive?.ResetModifiers();
                        Log("Thuốc nổ không nổ, chuyển sang cho " + nextAlive?.name, "info");
                    }
                }

                // 2. Check Jail
                if (currentPlayer.isInJail)
                {
                    Log(currentPlayer.name + " đang trong Tù, thử vượt ngục...", "jail");
                    yield return new WaitForSeconds(1.0f);

                    bool escape = false;
                    int attempts = currentPlayer.characterId == "lucky_duke" ? 2 : 1;
                    for (int a = 0; a < attempts; a++)
                    {
                        if (_rnd.Next(0, 100) < 25) escape = true;
                    }

                    currentPlayer.equipment.RemoveAll(e => CardCatalogDatabase.GetTypeOf(e) == "jail");
                    currentPlayer.ResetModifiers();

                    if (!escape)
                    {
                        Log(currentPlayer.name + " vượt ngục thất bại! Mất lượt đi.", "jail");
                        OnStateChanged?.Invoke(State);
                        yield return new WaitForSeconds(1.0f);
                        AdvanceToNextPlayer();
                        continue;
                    }
                    else
                    {
                        Log(currentPlayer.name + " vượt ngục thành công!", "info");
                    }
                }

                // 3. Draw Phase with Character Abilities
                yield return StartCoroutine(HandleCharacterDrawPhase(currentPlayer));

                State.phase = GamePhase.PlayPhase;
                OnStateChanged?.Invoke(State);

                if (currentPlayer.isBot)
                {
                    yield return StartCoroutine(RunBotPlayPhase(currentPlayer));
                }
                else
                {
                    float waitTime = 0;
                    while (State.currentTurnPlayerId == currentPlayer.id && State.phase == GamePhase.PlayPhase && waitTime < 45f)
                    {
                        waitTime += Time.deltaTime;
                        yield return null;
                    }
                }

                // 4. Discard Phase
                if (currentPlayer.isAlive && currentPlayer.hand.Count > currentPlayer.health)
                {
                    State.phase = GamePhase.DiscardPhase;
                    int discardNeeded = currentPlayer.hand.Count - currentPlayer.health;
                    for (int d = 0; d < discardNeeded; d++)
                    {
                        if (currentPlayer.hand.Count > 0)
                        {
                            var disc = currentPlayer.hand[0];
                            currentPlayer.hand.RemoveAt(0);
                            State.discard.Add(disc);
                        }
                    }
                    currentPlayer.cardCount = currentPlayer.hand.Count;
                    Log(currentPlayer.name + " bỏ " + discardNeeded + " lá bài thừa.", "discard");
                    CheckSuzyLafayette(currentPlayer);
                    OnStateChanged?.Invoke(State);
                    yield return new WaitForSeconds(0.5f);
                }

                // 5. Check Win
                string winner = BangGameRules.CheckGameOverWinner(State);
                if (!string.IsNullOrEmpty(winner))
                {
                    State.status = "finished";
                    State.winner = winner;
                    State.phase = GamePhase.GameOver;
                    Log("🏆 TRẬN ĐẤU KẾT THÚC! Phe " + winner.ToUpper() + " giành chiến thắng!", "gameover");
                    OnStateChanged?.Invoke(State);
                    yield break;
                }

                AdvanceToNextPlayer();
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator HandleCharacterDrawPhase(PlayerModel p)
        {
            // Kit Carlson: Looks at 3 cards, chooses 2
            if (p.characterId == "kit_carlson")
            {
                Log(p.name + " (Kit Carlson) nhìn 3 lá trên cùng và chọn 2 lá tốt nhất!", "ability");
                DrawCards(p, 2);
                yield return new WaitForSeconds(0.6f);
                yield break;
            }

            // Jesse Jones: Can steal 1st card from another player
            if (p.characterId == "jesse_jones")
            {
                var otherWithCards = State.players.FirstOrDefault(o => o.isAlive && o.id != p.id && o.hand.Count > 0);
                if (otherWithCards != null && _rnd.Next(0, 100) < 60)
                {
                    var stolen = otherWithCards.hand[0];
                    otherWithCards.hand.RemoveAt(0);
                    otherWithCards.cardCount = otherWithCards.hand.Count;
                    p.hand.Add(stolen);
                    DrawCards(p, 1); // 2nd card from deck
                    Log(p.name + " (Jesse Jones) rút 1 lá từ tay của " + otherWithCards.name + " và 1 lá từ bộ bài!", "ability");
                    CheckSuzyLafayette(otherWithCards);
                    yield return new WaitForSeconds(0.6f);
                    yield break;
                }
            }

            // Pedro Ramirez: Can draw 1st card from discard pile
            if (p.characterId == "pedro_ramirez" && State.discard.Count > 0)
            {
                var topDiscard = State.discard[State.discard.Count - 1];
                State.discard.RemoveAt(State.discard.Count - 1);
                p.hand.Add(topDiscard);
                DrawCards(p, 1);
                Log(p.name + " (Pedro Ramirez) nhặt 1 lá từ chồng bài bỏ và 1 lá từ bộ bài!", "ability");
                yield return new WaitForSeconds(0.6f);
                yield break;
            }

            // Black Jack: Shows 2nd card, if Hearts/Diamonds draws 3rd card
            if (p.characterId == "black_jack")
            {
                DrawCards(p, 2);
                bool luckyHeartOrDiamond = _rnd.Next(0, 100) < 50;
                if (luckyHeartOrDiamond)
                {
                    DrawCards(p, 1);
                    Log(p.name + " (Black Jack) lật lá thứ hai trúng Chất Đỏ! Được rút thêm lá thứ 3!", "ability");
                }
                else
                {
                    Log(p.name + " (Black Jack) rút 2 lá bài.", "draw");
                }
                yield return new WaitForSeconds(0.6f);
                yield break;
            }

            // Normal draw 2 cards
            DrawCards(p, 2);
            Log(p.name + " rút 2 lá bài.", "draw");
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator RunBotPlayPhase(PlayerModel bot)
        {
            yield return new WaitForSeconds(0.8f);

            // Sid Ketchum: Can discard 2 cards to heal 1 HP
            if (bot.characterId == "sid_ketchum" && bot.health < bot.maxHealth && bot.hand.Count >= 3)
            {
                var c1 = bot.hand[0];
                var c2 = bot.hand[1];
                bot.hand.Remove(c1);
                bot.hand.Remove(c2);
                State.discard.Add(c1);
                State.discard.Add(c2);
                bot.health++;
                Log(bot.name + " (Sid Ketchum) bỏ 2 lá để hồi 1 Máu (" + bot.health + "/" + bot.maxHealth + ")", "ability");
                OnStateChanged?.Invoke(State);
                yield return new WaitForSeconds(0.6f);
            }

            // Equip Blue Items
            var blueCards = bot.hand.Where(c => CardCatalogDatabase.GetCardInfo(c).type == CardType.BlueEquipment).ToList();
            foreach (var card in blueCards)
            {
                var type = CardCatalogDatabase.GetTypeOf(card);
                if (type == "jail") continue;

                bot.hand.Remove(card);
                bot.equipment.Add(card);
                bot.ResetModifiers();
                Log(bot.name + " trang bị " + CardCatalogDatabase.GetCardInfo(card).vietnameseName, "equip");
                CheckSuzyLafayette(bot);
                OnStateChanged?.Invoke(State);
                yield return new WaitForSeconds(0.6f);
            }

            // Beer (Disabled if only 2 players left)
            int aliveCount = State.players.Count(p => p.isAlive);
            if (bot.health < bot.maxHealth && aliveCount > 2)
            {
                var beer = bot.hand.FirstOrDefault(c => CardCatalogDatabase.GetTypeOf(c) == "beer");
                if (beer != null)
                {
                    bot.hand.Remove(beer);
                    State.discard.Add(beer);
                    bot.health = Math.Min(bot.maxHealth, bot.health + 1);
                    Log(bot.name + " uống 1 chai Bia, hồi 1 Máu (" + bot.health + "/" + bot.maxHealth + ")", "beer");
                    CheckSuzyLafayette(bot);
                    OnStateChanged?.Invoke(State);
                    yield return new WaitForSeconds(0.6f);
                }
            }

            // Special Action Cards: Gatling, Indiani, Saloon, Wells Fargo, Dilizenza
            var special = bot.hand.FirstOrDefault(c => {
                var t = CardCatalogDatabase.GetTypeOf(c);
                return t == "gatling" || t == "indiani" || t == "saloon" || t == "wells_fargo" || t == "dilizenza";
            });

            if (special != null)
            {
                var t = CardCatalogDatabase.GetTypeOf(special);
                bot.hand.Remove(special);
                State.discard.Add(special);
                CheckSuzyLafayette(bot);

                if (t == "gatling")
                {
                    Log(bot.name + " xả SÚNG MÁY GATLING toàn bộ bàn đấu!", "gatling");
                    OnStateChanged?.Invoke(State);
                    yield return StartCoroutine(ExecuteAreaAttack(bot, "dodge", "Gatling"));
                }
                else if (t == "indiani")
                {
                    Log(bot.name + " thả BẦY THỔ DÂN INDIANI tấn công toàn bộ bàn!", "indiani");
                    OnStateChanged?.Invoke(State);
                    yield return StartCoroutine(ExecuteAreaAttack(bot, "bang", "Indiani"));
                }
                else if (t == "saloon")
                {
                    foreach (var p in State.players.Where(p => p.isAlive)) p.health = Math.Min(p.maxHealth, p.health + 1);
                    Log(bot.name + " mở tiệc SALOON! Tất cả mọi người hồi 1 Máu.", "saloon");
                    OnStateChanged?.Invoke(State);
                }
                else if (t == "dilizenza") { DrawCards(bot, 2); Log(bot.name + " rút 2 lá từ Xe Thồ!", "draw"); }
                else if (t == "wells_fargo") { DrawCards(bot, 3); Log(bot.name + " rút 3 lá từ Wells Fargo!", "draw"); }

                yield return new WaitForSeconds(0.8f);
            }

            // Steal / Discard cards: Panico & Cat Balou
            var stealCard = bot.hand.FirstOrDefault(c => CardCatalogDatabase.GetTypeOf(c) == "panico" || CardCatalogDatabase.GetTypeOf(c) == "cat_balou");
            if (stealCard != null)
            {
                var tType = CardCatalogDatabase.GetTypeOf(stealCard);
                var validTargets = BangGameRules.GetValidTargets(State, bot.id, tType);
                var target = SelectSmartTarget(bot, validTargets);
                if (target != null)
                {
                    bot.hand.Remove(stealCard);
                    State.discard.Add(stealCard);
                    CheckSuzyLafayette(bot);

                    if (target.equipment.Count > 0)
                    {
                        var eq = target.equipment[0];
                        target.equipment.Remove(eq);
                        target.ResetModifiers();
                        if (tType == "panico") { bot.hand.Add(eq); Log(bot.name + " cướp trang bị " + CardCatalogDatabase.GetCardInfo(eq).vietnameseName + " của " + target.name + "!", "steal"); }
                        else { State.discard.Add(eq); Log(bot.name + " bắn hủy trang bị " + CardCatalogDatabase.GetCardInfo(eq).vietnameseName + " của " + target.name + "!", "discard"); }
                    }
                    else if (target.hand.Count > 0)
                    {
                        var h = target.hand[0];
                        target.hand.Remove(h);
                        target.cardCount = target.hand.Count;
                        if (tType == "panico") { bot.hand.Add(h); Log(bot.name + " cướp 1 lá trên tay của " + target.name + "!", "steal"); }
                        else { State.discard.Add(h); Log(bot.name + " hủy 1 lá trên tay của " + target.name + "!", "discard"); }
                    }
                    OnStateChanged?.Invoke(State);
                    yield return new WaitForSeconds(0.6f);
                }
            }

            // Attack with Bang (Smart Target)
            var bangCard = bot.hand.FirstOrDefault(c => CardCatalogDatabase.GetTypeOf(c) == "bang" || (bot.characterId == "calamity_janet" && CardCatalogDatabase.GetTypeOf(c) == "dodge"));
            if (bangCard != null && (State.bangUsedThisTurn == 0 || bot.hasVolcanic || bot.characterId == "willy_the_kid"))
            {
                var targets = BangGameRules.GetValidTargets(State, bot.id, "bang");
                var target = SelectSmartTarget(bot, targets);
                if (target != null)
                {
                    bot.hand.Remove(bangCard);
                    State.discard.Add(bangCard);
                    State.bangUsedThisTurn++;
                    Log(bot.name + " bắn BANG! vào " + target.name + "!", "bang");
                    CheckSuzyLafayette(bot);
                    OnStateChanged?.Invoke(State);

                    yield return StartCoroutine(HandleBangTargetResponse(bot, target, bangCard));
                }
            }

            yield return new WaitForSeconds(0.4f);
        }

        private PlayerModel SelectSmartTarget(PlayerModel bot, List<PlayerModel> validTargets)
        {
            if (validTargets == null || validTargets.Count == 0) return null;

            var sheriff = State.players.FirstOrDefault(p => p.isAlive && p.role == RoleType.Sheriff);

            switch (bot.role)
            {
                case RoleType.Outlaw:
                    if (sheriff != null && validTargets.Contains(sheriff)) return sheriff;
                    return validTargets.OrderBy(t => t.health).First();

                case RoleType.Deputy:
                    var nonSheriff = validTargets.Where(t => t.role != RoleType.Sheriff).ToList();
                    return nonSheriff.Count > 0 ? nonSheriff.OrderBy(t => t.health).First() : validTargets.First();

                case RoleType.Renegade:
                    int aliveCount = State.players.Count(p => p.isAlive);
                    if (aliveCount > 2)
                    {
                        var nonSheriffList = validTargets.Where(t => t.role != RoleType.Sheriff).ToList();
                        if (nonSheriffList.Count > 0) return nonSheriffList.OrderBy(t => t.health).First();
                    }
                    return validTargets.OrderBy(t => t.health).First();

                default:
                    return validTargets.OrderBy(t => t.health).First();
            }
        }

        private IEnumerator ExecuteAreaAttack(PlayerModel attacker, string requiredCardType, string attackName)
        {
            var enemies = State.players.Where(p => p.isAlive && p.id != attacker.id).ToList();
            foreach (var victim in enemies)
            {
                yield return new WaitForSeconds(0.6f);

                if (victim.isBot)
                {
                    var responseCard = victim.hand.FirstOrDefault(c => CardCatalogDatabase.GetTypeOf(c) == requiredCardType || (victim.characterId == "calamity_janet" && CardCatalogDatabase.GetTypeOf(c) != requiredCardType));
                    if (responseCard != null)
                    {
                        victim.hand.Remove(responseCard);
                        State.discard.Add(responseCard);
                        victim.cardCount = victim.hand.Count;
                        Log(victim.name + " nộp " + (requiredCardType == "dodge" ? "NÉ" : "BANG") + " chống lại " + attackName + "!", "dodge");
                        CheckSuzyLafayette(victim);
                    }
                    else
                    {
                        victim.health = Math.Max(0, victim.health - 1);
                        Log(victim.name + " không có bài phòng thủ! Mất 1 Máu (" + victim.health + "/" + victim.maxHealth + ")", "damage");
                        TriggerDamageAbilities(victim, attacker);
                        CheckDeath(victim, attacker);
                    }
                }
                else
                {
                    // Local player response
                    bool hasCard = victim.hand.Any(c => CardCatalogDatabase.GetTypeOf(c) == requiredCardType || (victim.characterId == "calamity_janet"));
                    State.pendingBang = new PendingActionModel
                    {
                        id = Guid.NewGuid().ToString(),
                        actorPlayerId = attacker.id,
                        targetPlayerId = victim.id,
                        actionType = attackName.ToLower(),
                        requiredCardType = requiredCardType,
                        deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15000
                    };
                    State.phase = GamePhase.WaitingResponse;
                    OnStateChanged?.Invoke(State);

                    float wait = 0;
                    while (State.pendingBang != null && wait < 15f)
                    {
                        wait += Time.deltaTime;
                        yield return null;
                    }

                    if (State.pendingBang != null) ResolvePendingResponse(false);
                }

                OnStateChanged?.Invoke(State);
            }
        }

        public IEnumerator HandleBangTargetResponse(PlayerModel shooter, PlayerModel target, string cardId)
        {
            int reqDodges = BangGameRules.GetRequiredDodges(shooter);

            // Barrel check
            if (target.hasBarrel || target.characterId == "jourdonnais")
            {
                Log(target.name + " lật Thùng Gỗ Barrel...", "barrel");
                yield return new WaitForSeconds(0.8f);

                bool barrelSuccess = false;
                int attempts = target.characterId == "lucky_duke" ? 2 : 1;
                for (int a = 0; a < attempts; a++)
                {
                    if (_rnd.Next(0, 100) < 25) barrelSuccess = true;
                }

                if (barrelSuccess)
                {
                    Log(target.name + " lật trúng Cơ! Thùng gỗ chắn đạn thành công!", "dodge");
                    OnStateChanged?.Invoke(State);
                    yield break;
                }
            }

            if (target.isBot)
            {
                var availableDodges = target.hand.Where(c => CardCatalogDatabase.GetTypeOf(c) == "dodge" || (target.characterId == "calamity_janet" && CardCatalogDatabase.GetTypeOf(c) == "bang")).ToList();
                if (availableDodges.Count >= reqDodges)
                {
                    for (int d = 0; d < reqDodges; d++)
                    {
                        var dodgeCard = availableDodges[d];
                        target.hand.Remove(dodgeCard);
                        State.discard.Add(dodgeCard);
                    }
                    target.cardCount = target.hand.Count;
                    Log(target.name + " tung người NÉ phát đạn (" + reqDodges + " lá NÉ)!", "dodge");
                    CheckSuzyLafayette(target);
                    OnStateChanged?.Invoke(State);
                    yield return new WaitForSeconds(0.6f);
                }
                else
                {
                    target.health = Math.Max(0, target.health - 1);
                    Log(target.name + " trúng đạn! Mất 1 Máu (" + target.health + "/" + target.maxHealth + ")", "damage");
                    TriggerDamageAbilities(target, shooter);
                    CheckDeath(target, shooter);
                    OnStateChanged?.Invoke(State);
                    yield return new WaitForSeconds(0.6f);
                }
            }
            else
            {
                State.pendingBang = new PendingActionModel
                {
                    id = Guid.NewGuid().ToString(),
                    actorPlayerId = shooter.id,
                    targetPlayerId = target.id,
                    actionType = "bang",
                    requiredCardType = "dodge",
                    requiredDodges = reqDodges,
                    cardId = cardId,
                    deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15000
                };
                State.phase = GamePhase.WaitingResponse;
                OnStateChanged?.Invoke(State);

                float wait = 0;
                while (State.pendingBang != null && wait < 15f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }

                if (State.pendingBang != null) ResolvePendingResponse(false);
            }
        }

        public void ResolvePendingResponse(bool useDodge)
        {
            if (State.pendingBang == null) return;

            var target = State.players.Find(p => p.id == State.pendingBang.targetPlayerId);
            var shooter = State.players.Find(p => p.id == State.pendingBang.actorPlayerId);
            string reqType = State.pendingBang.requiredCardType ?? "dodge";
            State.pendingBang = null;
            State.phase = GamePhase.PlayPhase;

            if (target != null)
            {
                if (useDodge)
                {
                    var card = target.hand.FirstOrDefault(c => CardCatalogDatabase.GetTypeOf(c) == reqType || (target.characterId == "calamity_janet"));
                    if (card != null) target.hand.Remove(card);
                    target.cardCount = target.hand.Count;
                    Log(target.name + " đã sử dụng bài phòng thủ thành công!", "dodge");
                    CheckSuzyLafayette(target);
                }
                else
                {
                    target.health = Math.Max(0, target.health - 1);
                    Log(target.name + " nhận 1 sát thương! (" + target.health + "/" + target.maxHealth + ")", "damage");
                    TriggerDamageAbilities(target, shooter);
                    CheckDeath(target, shooter);
                }
            }

            OnStateChanged?.Invoke(State);
        }

        public void LocalPlayerPlayCard(string cardId, string targetPlayerId = null)
        {
            var local = State.players.Find(p => p.id == "player_local");
            if (local == null || !BangGameRules.CanPlayCard(State, local.id, cardId)) return;

            var info = CardCatalogDatabase.GetCardInfo(cardId);
            var type = CardCatalogDatabase.GetTypeOf(cardId);

            local.hand.Remove(cardId);
            local.cardCount = local.hand.Count;

            if (info.type == CardType.BlueEquipment && type != "jail")
            {
                local.equipment.Add(cardId);
                local.ResetModifiers();
                Log(local.name + " trang bị " + info.vietnameseName, "equip");
                CheckSuzyLafayette(local);
                OnStateChanged?.Invoke(State);
                return;
            }

            State.discard.Add(cardId);
            CheckSuzyLafayette(local);

            if (type == "bang" || (local.characterId == "calamity_janet" && type == "dodge"))
            {
                State.bangUsedThisTurn++;
                var target = State.players.Find(p => p.id == targetPlayerId);
                Log(local.name + " bắn BANG! vào " + (target != null ? target.name : "kẻ địch") + "!", "bang");
                OnStateChanged?.Invoke(State);
                if (target != null) StartCoroutine(HandleBangTargetResponse(local, target, cardId));
                return;
            }

            if (type == "gatling")
            {
                Log(local.name + " xả súng máy GATLING toàn bộ bàn đấu!", "gatling");
                OnStateChanged?.Invoke(State);
                StartCoroutine(ExecuteAreaAttack(local, "dodge", "Gatling"));
                return;
            }

            if (type == "indiani")
            {
                Log(local.name + " thả bầy thổ dân INDIANI tấn công toàn bàn!", "indiani");
                OnStateChanged?.Invoke(State);
                StartCoroutine(ExecuteAreaAttack(local, "bang", "Indiani"));
                return;
            }

            if (type == "beer")
            {
                local.health = Math.Min(local.maxHealth, local.health + 1);
                Log(local.name + " uống Bia, hồi 1 Máu (" + local.health + "/" + local.maxHealth + ")", "beer");
                OnStateChanged?.Invoke(State);
                return;
            }

            if (type == "saloon")
            {
                foreach (var p in State.players.Where(p => p.isAlive)) p.health = Math.Min(p.maxHealth, p.health + 1);
                Log(local.name + " mở tiệc Saloon! Mọi người được hồi 1 Máu.", "saloon");
                OnStateChanged?.Invoke(State);
                return;
            }

            if (type == "dilizenza") { DrawCards(local, 2); Log(local.name + " dùng Xe Thồ rút 2 lá!", "draw"); }
            else if (type == "wells_fargo") { DrawCards(local, 3); Log(local.name + " dùng Wells Fargo rút 3 lá!", "draw"); }

            if (type == "panico" || type == "cat_balou")
            {
                var target = State.players.Find(p => p.id == targetPlayerId);
                if (target != null)
                {
                    if (target.hand.Count > 0)
                    {
                        var targetCard = target.hand[0];
                        target.hand.RemoveAt(0);
                        target.cardCount = target.hand.Count;
                        if (type == "panico") { local.hand.Add(targetCard); local.cardCount = local.hand.Count; Log(local.name + " đã cướp 1 lá từ " + target.name + "!", "steal"); }
                        else { State.discard.Add(targetCard); Log(local.name + " đã hủy 1 lá bài của " + target.name + "!", "discard"); }
                        CheckSuzyLafayette(target);
                    }
                    else if (target.equipment.Count > 0)
                    {
                        var eq = target.equipment[0];
                        target.equipment.RemoveAt(0);
                        target.ResetModifiers();
                        if (type == "panico") { local.hand.Add(eq); local.cardCount = local.hand.Count; Log(local.name + " đã cướp trang bị " + CardCatalogDatabase.GetCardInfo(eq).vietnameseName + " của " + target.name + "!", "steal"); }
                        else { State.discard.Add(eq); Log(local.name + " đã bắn rớt trang bị " + CardCatalogDatabase.GetCardInfo(eq).vietnameseName + " của " + target.name + "!", "discard"); }
                    }
                }
            }

            OnStateChanged?.Invoke(State);
        }

        public void LocalPlayerEndTurn()
        {
            if (State.currentTurnPlayerId == "player_local" && State.phase == GamePhase.PlayPhase)
            {
                State.phase = GamePhase.DiscardPhase;
            }
        }

        private void TriggerDamageAbilities(PlayerModel victim, PlayerModel attacker)
        {
            // Bart Cassidy: Draws 1 card each time he loses 1 HP
            if (victim.characterId == "bart_cassidy" && victim.isAlive)
            {
                DrawCards(victim, 1);
                Log(victim.name + " (Bart Cassidy) chịu đòn rút ngay 1 lá bài mới!", "ability");
            }

            // El Gringo: Steals 1 card from attacker
            if (victim.characterId == "el_gringo" && victim.isAlive && attacker != null && attacker.hand.Count > 0)
            {
                var stolen = attacker.hand[0];
                attacker.hand.RemoveAt(0);
                attacker.cardCount = attacker.hand.Count;
                victim.hand.Add(stolen);
                victim.cardCount = victim.hand.Count;
                Log(victim.name + " (El Gringo) trả thù cướp 1 lá trên tay " + attacker.name + "!", "ability");
                CheckSuzyLafayette(attacker);
            }
        }

        private void CheckSuzyLafayette(PlayerModel p)
        {
            // Suzy Lafayette: Draws 1 card immediately when hand is empty (0 cards)
            if (p.characterId == "suzy_lafayette" && p.isAlive && p.hand.Count == 0)
            {
                DrawCards(p, 1);
                Log(p.name + " (Suzy Lafayette) rảnh tay tự động rút ngay 1 lá bài mới!", "ability");
            }
        }

        private void CheckDeath(PlayerModel victim, PlayerModel killer)
        {
            if (victim.health <= 0)
            {
                victim.isAlive = false;
                victim.isRoleRevealed = true;
                Log("☠️ " + victim.name + " ĐÃ BỊ TIÊU DIỆT! Thân phận thật: " + CardCatalogDatabase.GetRoleInfo(victim.role).vietnameseName, "death");

                if (killer != null && killer.role == RoleType.Sheriff && victim.role == RoleType.Deputy)
                {
                    killer.hand.Clear();
                    killer.equipment.Clear();
                    killer.ResetModifiers();
                    Log("⚠️ Cảnh Trưởng giết nhầm Phó Cảnh Trưởng! Phải bỏ toàn bộ bài & trang bị!", "penalty");
                }
                else if (killer != null && victim.role == RoleType.Outlaw && killer.isAlive)
                {
                    DrawCards(killer, 3);
                    Log("🎁 " + killer.name + " tiêu diệt Cướp và nhận thưởng 3 lá bài vàng!", "reward");
                }

                var sam = State.players.Find(p => p.isAlive && p.characterId == "vulture_sam");
                if (sam != null && sam.id != victim.id)
                {
                    sam.hand.AddRange(victim.hand);
                    sam.hand.AddRange(victim.equipment);
                    sam.cardCount = sam.hand.Count;
                    victim.hand.Clear();
                    victim.equipment.Clear();
                    Log("🦅 Kền kền Vulture Sam thu gom toàn bộ bài & trang bị của " + victim.name + "!", "ability");
                }
            }
        }

        private void DrawCards(PlayerModel p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (State.deck.Count == 0)
                {
                    if (State.discard.Count == 0) break;
                    State.deck = State.discard.OrderBy(x => _rnd.Next()).ToList();
                    State.discard.Clear();
                }
                var c = State.deck[0];
                State.deck.RemoveAt(0);
                p.hand.Add(c);
            }
            p.cardCount = p.hand.Count;
        }

        private void AdvanceToNextPlayer()
        {
            var alive = State.players.Where(p => p.isAlive).OrderBy(p => p.seat).ToList();
            if (alive.Count == 0) return;

            var current = alive.Find(p => p.id == State.currentTurnPlayerId);
            int idx = current != null ? alive.IndexOf(current) : 0;
            int nextIdx = (idx + 1) % alive.Count;
            State.currentTurnPlayerId = alive[nextIdx].id;
            State.turnNumber++;
        }

        private PlayerModel GetNextAlivePlayer(PlayerModel from)
        {
            var alive = State.players.Where(p => p.isAlive).OrderBy(p => p.seat).ToList();
            int idx = alive.IndexOf(from);
            if (idx == -1) return null;
            return alive[(idx + 1) % alive.Count];
        }

        private List<string> GenerateStandardDeck()
        {
            var list = new List<string>();
            string[] suits = { "heart", "spade", "diamond", "club" };
            string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

            void Add(string type, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var s = suits[_rnd.Next(suits.Length)];
                    var r = ranks[_rnd.Next(ranks.Length)];
                    list.Add(type + "_" + (i + 1) + "_" + s + "_" + r);
                }
            }

            Add("bang", 25);
            Add("dodge", 12);
            Add("beer", 6);
            Add("panico", 4);
            Add("cat_balou", 4);
            Add("dilizenza", 2);
            Add("wells_fargo", 1);
            Add("general_store", 2);
            Add("duello", 3);
            Add("gatling", 1);
            Add("indiani", 2);
            Add("saloon", 1);
            Add("barrel", 2);
            Add("jail", 3);
            Add("dynamite", 1);
            Add("volcanic", 2);
            Add("gun_range_2", 3);
            Add("gun_range_3", 2);
            Add("gun_range_4", 1);
            Add("gun_range_5", 1);
            Add("mustang", 2);
            Add("appaloosa", 1);

            return list;
        }

        private string GetBotName(int idx)
        {
            string[] names = { "Bill Độc Nhãn", "Apache Jack", "Django Nhanh Nhẹn", "Doc Holliday", "Jesse Râu Đen", "Billy Cao Kều", "Scarlett Mắt Biếc" };
            return names[(idx - 1 + names.Length) % names.Length];
        }

        private void Log(string message, string actionType)
        {
            State.publicLog.Add(message);
            if (State.publicLog.Count > 40) State.publicLog.RemoveAt(0);
            OnCombatLog?.Invoke(message, actionType);
        }
    }
}

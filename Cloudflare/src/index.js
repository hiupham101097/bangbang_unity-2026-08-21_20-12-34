var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/index.ts
import { DurableObject } from "cloudflare:workers";
var json = /* @__PURE__ */ __name((value, status = 200) => new Response(JSON.stringify(value), {
  status,
  headers: { "content-type": "application/json; charset=utf-8", "access-control-allow-origin": "*" }
}), "json");
var fail = /* @__PURE__ */ __name((message, status = 400) => json({ error: message }, status), "fail");
var code = /* @__PURE__ */ __name(() => crypto.randomUUID().replaceAll("-", "").slice(0, 6).toUpperCase(), "code");
var shuffle = /* @__PURE__ */ __name((values) => {
  const copy = [...values];
  for (let index = copy.length - 1; index > 0; index--) {
    const swap = crypto.getRandomValues(new Uint32Array(1))[0] % (index + 1);
    [copy[index], copy[swap]] = [copy[swap], copy[index]];
  }
  return copy;
}, "shuffle");
var typeOf = /* @__PURE__ */ __name((card) => {
  const parts = (card ?? "").split("_");
  return parts[0] === "gun" ? parts.slice(0, 3).join("_") : parts[0];
}, "typeOf");
var deck = /* @__PURE__ */ __name(() => {
  const types = [
    ...Array(25).fill("bang"),
    ...Array(12).fill("dodge"),
    ...Array(6).fill("beer"),
    ...Array(4).fill("panico"),
    ...Array(4).fill("cat_balou"),
    ...Array(2).fill("dilizenza"),
    "wells_fargo",
    ...Array(2).fill("general_store"),
    ...Array(3).fill("duello"),
    "gatling",
    ...Array(2).fill("indiani"),
    "saloon",
    ...Array(2).fill("barrel"),
    ...Array(3).fill("jail"),
    "dynamite",
    ...Array(2).fill("volcanic"),
    ...Array(3).fill("gun_range_2"),
    "gun_range_3",
    "gun_range_4",
    "gun_range_5",
    ...Array(2).fill("mustang"),
    "appaloosa"
  ];
  const ranks = ["ace", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "jack", "queen", "king"];
  const suits = ["spade", "club", "diamond", "heart"];
  return types.map((type, index) => {
    const rank = ranks[Math.floor(index / suits.length) % ranks.length];
    const suit = suits[index % suits.length];
    return `${type}_card${index}_${rank}_${suit}`;
  });
}, "deck");
var roles = /* @__PURE__ */ __name((count) => {
  if (count === 4) return ["sheriff", "renegade", "outlaw", "outlaw"];
  if (count === 8) return ["sheriff", "deputy", "deputy", "outlaw", "outlaw", "outlaw", "renegade", "renegade"];
  const police = Math.floor((count - 1) / 2);
  return ["sheriff", ...Array(police - 1).fill("deputy"), ...Array(count - police - 1).fill("outlaw"), "renegade"];
}, "roles");
var cardSummary = /* @__PURE__ */ __name((card) => {
  if (!card) return "kh\xF4ng c\xF3 l\xE1";
  const parts = card.split("_");
  return `${typeOf(card).toUpperCase()} ${parts.at(-2) ?? ""} ${parts.at(-1) ?? ""}`;
}, "cardSummary");
var roleDeck = /* @__PURE__ */ __name((count) => shuffle(roles(count)).slice(0, count).map((value) => ({ id: `role_${crypto.randomUUID()}`, value })), "roleDeck");
var characterHealth = {
  paul_regret: 3,
  el_gringo: 3,
  vulture_sam: 4,
  calamity_janet: 4,
  black_jack: 4,
  willy_the_kid: 4,
  lucky_duke: 4,
  kit_carlson: 4,
  rose_doolan: 4,
  suzy_lafayette: 4,
  bart_cassidy: 4,
  jesse_jones: 4,
  slab_the_killer: 4,
  sid_ketchum: 4,
  jourdonnais: 4,
  pedro_ramirez: 4
};
var characters = Object.keys(characterHealth);
var index_default = {
  async fetch(request, env) {
    if (request.method === "OPTIONS") return new Response(null, { headers: { "access-control-allow-origin": "*", "access-control-allow-methods": "GET,POST,OPTIONS", "access-control-allow-headers": "authorization,content-type" } });
    const url = new URL(request.url);
    if (url.pathname === "/health") return json({ ok: true, service: "blue-frog-fec8" });
    if (request.method === "POST" && url.pathname === "/v1/session") {
      const body = await request.json();
      if (!body.deviceId || body.deviceId.length < 8) return fail("Thi\u1EBFu deviceId.");
      const allowedAvatars = ["quick_jack", "iron_rose", "doctor_lee", "lucky_joe2", "role_sheriff", "role_deputy", "role_raider", "role_guardian", "role_traitor"];
      const avatarId = allowedAvatars.includes(body.avatarId) ? body.avatarId : "quick_jack";
      const user2 = { id: body.deviceId, name: (body.displayName || "Cao b\u1ED3i").slice(0, 24), avatarId };
      return json({ token: await sign(user2, env.AUTH_SECRET), user: user2 });
    }
    const user = await authenticate(request, env.AUTH_SECRET);
    if (!user) return fail("Phi\xEAn \u0111\u0103ng nh\u1EADp kh\xF4ng h\u1EE3p l\u1EC7.", 401);
    if (request.method === "GET" && url.pathname === "/v1/rooms") {
      return env.DIRECTORY.getByName("lobby").fetch("https://directory/list");
    }
    if (request.method === "POST" && url.pathname === "/v1/rooms") {
      const body = await request.json();
      const roomCode = code();
      const stub2 = env.MATCH.get(env.MATCH.idFromName(roomCode));
      return stub2.fetch(new Request("https://match/internal/create", { method: "POST", body: JSON.stringify({ user, code: roomCode, maxPlayers: body.maxPlayers, turnDurationSeconds: body.turnDurationSeconds }) }));
    }
    const match = url.pathname.match(/^\/v1\/rooms\/([A-Z0-9]+)(?:\/(ws))?$/);
    if (!match) return fail("Kh\xF4ng t\xECm th\u1EA5y API.", 404);
    const stub = env.MATCH.get(env.MATCH.idFromName(match[1]));
    const headers = new Headers(request.headers);
    headers.set("x-bangbang-user", JSON.stringify(user));
    return stub.fetch(new Request(`https://match/${match[2] === "ws" ? "ws" : "command"}`, { method: request.method, headers, body: request.body }));
  }
};
var BangBangDirectory = class extends DurableObject {
  static {
    __name(this, "BangBangDirectory");
  }
  constructor(ctx, env) {
    super(ctx, env);
  }
  async fetch(request) {
    const path = new URL(request.url).pathname;
    if (path === "/upsert" && request.method === "POST") {
      const summary = await request.json();
      await this.ctx.storage.put(`room:${summary.id}`, summary);
      return json({ ok: true });
    }
    if (path === "/list") {
      const entries = await this.ctx.storage.list({ prefix: "room:" });
      const rooms = [...entries.values()].filter((room) => room.status === "waiting" && room.totalCount < room.maxPlayers).sort((left, right) => right.updatedAt - left.updatedAt).slice(0, 20);
      return json({ rooms });
    }
    return fail("Directory route not found.", 404);
  }
};
var BangBangMatch = class extends DurableObject {
  static {
    __name(this, "BangBangMatch");
  }
  stateData;
  constructor(ctx, env) {
    super(ctx, env);
  }
  async fetch(request) {
    const path = new URL(request.url).pathname;
    if (path === "/internal/create") return this.create(request);
    const user = JSON.parse(request.headers.get("x-bangbang-user") || "null");
    if (!user) return fail("Unauthorized", 401);
    if (path === "/ws") return this.websocket(request, user);
    if (path === "/command" && request.method === "GET") {
      const state = await this.load();
      return json({ room: this.snapshot(state, user.id) });
    }
    if (path !== "/command" || request.method !== "POST") return fail("Not found", 404);
    return this.command(user, await request.json());
  }
  async webSocketMessage(ws, message) {
    if (typeof message !== "string") return;
    const user = ws.deserializeAttachment();
    if (!user) return;
    try {
      await this.apply(user, JSON.parse(message));
    } catch (error) {
      ws.send(JSON.stringify({ type: "error", error: error instanceof Error ? error.message : "L\u1ED7i m\xE1y ch\u1EE7" }));
    }
  }
  webSocketClose(ws) {
    ws.close();
  }
  async alarm() {
    const state = await this.load();
    if (state.status === "starting" && state.phase === "role_selection" && state.characterSelectionDeadline && Date.now() >= state.characterSelectionDeadline) {
      this.fillMissingRoles(state);
      await this.startRoleReveal(state);
      await this.save(state);
    } else if (state.status === "starting" && state.phase === "role_reveal" && state.characterSelectionDeadline && Date.now() >= state.characterSelectionDeadline) {
      await this.startCharacterSelection(state);
      await this.save(state);
    } else if (state.status === "starting" && (state.phase === "character_selection" || state.phase === "choosing_character") && state.characterSelectionDeadline && Date.now() >= state.characterSelectionDeadline) {
      await this.fillMissingCharacters(state);
      state.publicLog.push("Het gio chon nhan vat: he thong da chon ngau nhien.");
      await this.finalizeCharacters(state);
      await this.save(state);
    } else if (state.phase === "waiting_response" && state.pendingBang && Date.now() >= state.pendingBang.deadline) {
      const pending = state.pendingBang;
      if (pending.actionType === "rescue") {
        this.resolveRescue(state, pending.targetId, []);
      } else if (pending.actionType === "kit_carlson") {
        this.chooseKitCarlson(state, pending.actorId, (pending.choices ?? []).slice(0, 2));
      } else if (pending.actionType === "lucky_duke_judgment") {
        await this.chooseLuckyDuke(state, pending.actorId, this.preferredLuckyChoice(pending));
      } else if (pending.actionType === "general_store") {
        const card = pending.openedCardIds?.[0];
        if (card) this.chooseGeneralStore(state, pending.currentPickerId ?? pending.targetId, card);
      } else {
        const waitingForRescue = this.damage(state, pending.targetId, pending.actorId, "Kh\xF4ng ph\u1EA3n \u1EE9ng k\u1ECBp", 1, pending, true);
        if (!waitingForRescue) {
          if (pending.actionType === "duello") this.finishPendingResponse(state, pending);
          else this.advancePendingResponse(state, pending);
        }
      }
      await this.save(state);
    } else if (state.status === "playing" && state.phase !== "waiting_response" && state.turnDeadline && Date.now() >= state.turnDeadline) {
      const current = state.players.find((player) => player.id === state.currentTurnPlayerId);
      if (current?.bot && state.phase === "turn_start") await this.runBotTurn(state);
      else await this.resolveTurnTimeout(state);
      await this.save(state);
    }
  }
  async create(request) {
    const data = await request.json();
    const existing = await this.load(false);
    if (existing) return fail("M\xE3 ph\xF2ng tr\xF9ng, h\xE3y t\u1EA1o l\u1EA1i.", 409);
    const maxPlayers = Math.max(4, Math.min(8, Number(data.maxPlayers || 4)));
    const host = { id: data.user.id, name: data.user.name, avatarId: data.user.avatarId, seat: 0, bot: false, ready: false, alive: true, health: 0, maxHealth: 0, cardCount: 0, hand: [], equipment: [], attackRange: 1 };
    const state = { id: data.code, code: data.code, hostId: host.id, maxPlayers, turnDurationSeconds: Math.max(20, Number(data.turnDurationSeconds || 60)), status: "waiting", phase: "lobby", players: [host], deck: [], discard: [], turnNumber: 0, bangUsedThisTurn: 0, publicLog: ["Ph\xF2ng \u0111\xE3 \u0111\u01B0\u1EE3c t\u1EA1o."] };
    await this.save(state);
    return json({ room: this.snapshot(state, data.user.id) }, 201);
  }
  async command(user, command) {
    try {
      const state = await this.apply(user, command);
      return json({ room: this.snapshot(state, user.id) });
    } catch (error) {
      return fail(error instanceof Error ? error.message : "L\u1ED7i m\xE1y ch\u1EE7");
    }
  }
  async apply(user, command) {
    const state = await this.load();
    const payload = command.payload ?? {};
    const requestKey = payload.requestId ? `${user.id}:${String(payload.requestId)}` : "";
    if (requestKey && state.processedRequestIds?.includes(requestKey)) return state;
    if (command.action === "join") {
      if (state.status !== "waiting" || state.players.length >= state.maxPlayers) throw Error("Ph\xF2ng \u0111\xE3 \u0111\u1EA7y ho\u1EB7c \u0111\xE3 b\u1EAFt \u0111\u1EA7u.");
      if (!state.players.some((player) => player.id === user.id)) state.players.push({ id: user.id, name: user.name, avatarId: user.avatarId, seat: state.players.length, bot: false, ready: false, alive: true, health: 0, maxHealth: 0, cardCount: 0, hand: [], equipment: [], attackRange: 1 });
    } else {
      const player = state.players.find((item) => item.id === user.id);
      if (!player) throw Error("B\u1EA1n ch\u01B0a \u1EDF trong ph\xF2ng n\xE0y.");
      if (command.action === "leave") {
        if (state.status !== "waiting") throw Error("Kh\xF4ng th\u1EC3 r\u1EDDi ph\xF2ng khi tr\u1EADn \u0111ang di\u1EC5n ra.");
        state.players = state.players.filter((item) => item.id !== user.id);
        state.players.forEach((item, index) => item.seat = index);
        if (state.hostId === user.id && state.players.length > 0) {
          state.hostId = state.players.find((item) => !item.bot)?.id ?? state.players[0].id;
        }
        if (state.players.every((item) => item.bot)) state.status = "finished";
      } else if (command.action === "ready") {
        if (state.status !== "waiting") throw Error("Tr\u1EADn \u0111\xE3 b\u1EAFt \u0111\u1EA7u.");
        player.ready = Boolean(payload.ready);
      } else if (command.action === "add_bot") {
        if (user.id !== state.hostId || state.status !== "waiting" || state.players.length >= state.maxPlayers) throw Error("Kh\xF4ng th\u1EC3 th\xEAm bot.");
        const seat = state.players.length;
        const botAvatars = ["doctor_lee", "iron_rose", "lucky_joe2", "role_raider", "role_guardian"];
        state.players.push({ id: `bot_${crypto.randomUUID()}`, name: `Bot ${seat}`, avatarId: botAvatars[seat % botAvatars.length], seat, bot: true, ready: true, alive: true, health: 0, maxHealth: 0, cardCount: 0, hand: [], equipment: [], attackRange: 1 });
      } else if (command.action === "remove_bot") {
        const botId = String(payload.botId || "");
        if (user.id !== state.hostId || state.status !== "waiting") throw Error("Kh\xF4ng th\u1EC3 x\xF3a bot.");
        const before = state.players.length;
        state.players = state.players.filter((item) => item.id !== botId || !item.bot);
        if (state.players.length === before) throw Error("Kh\xF4ng t\xECm th\u1EA5y bot.");
        state.players.forEach((item, index) => item.seat = index);
      } else if (command.action === "start") await this.start(state, user.id);
      else if (command.action === "choose_role") await this.chooseRole(state, user.id, String(payload.cardId || ""));
      else if (command.action === "take_character_card") await this.takeCharacterCard(state, user.id, String(payload.cardId || ""));
      else if (command.action === "choose_character") await this.chooseCharacter(state, user.id, String(payload.characterId || ""));
      else if (command.action === "draw") await this.draw(state, user.id, String(payload.targetPlayerId || ""), String(payload.drawSource || "deck"));
      else if (command.action === "play") this.play(state, user.id, String(payload.cardId || ""), String(payload.targetPlayerId || ""), payload);
      else if (command.action === "respond_bang") this.respondBang(state, user.id, String(payload.response || "damage"), String(payload.cardId || ""), Array.isArray(payload.cardIds) ? payload.cardIds.map(String) : []);
      else if (command.action === "rescue") this.resolveRescue(state, user.id, Array.isArray(payload.cardIds) ? payload.cardIds.map(String) : []);
      else if (command.action === "choose_kit_carlson") this.chooseKitCarlson(state, user.id, Array.isArray(payload.cardIds) ? payload.cardIds.map(String) : []);
      else if (command.action === "choose_lucky_duke") await this.chooseLuckyDuke(state, user.id, String(payload.resultCardId || ""));
      else if (command.action === "choose_general_store") this.chooseGeneralStore(state, user.id, String(payload.cardId || ""));
      else if (command.action === "sid_ketchum") this.useSidKetchum(state, user.id, Array.isArray(payload.cardIds) ? payload.cardIds.map(String) : []);
      else if (command.action === "end_turn") await this.endTurn(state, user.id);
      else if (command.action === "discard") await this.discardCards(state, user.id, Array.isArray(payload.cardIds) ? payload.cardIds.map(String) : []);
    }
    for (const candidate of state.players) this.maybeSuzy(state, candidate);
    if (requestKey) state.processedRequestIds = [...state.processedRequestIds ?? [], requestKey].slice(-200);
    await this.save(state);
    return state;
  }
  async start(state, userId) {
    if (state.hostId !== userId || state.status !== "waiting") throw Error("Ch\u1EC9 ch\u1EE7 ph\xF2ng \u0111\u01B0\u1EE3c b\u1EAFt \u0111\u1EA7u.");
    if (state.players.length < 4) throw Error("C\u1EA7n \u0111\u1EE7 4\u20138 ng\u01B0\u1EDDi ch\u01A1i.");
    if (state.players.some((player) => !player.bot && player.id !== userId && !player.ready)) throw Error("Kh\xE1ch ch\u01B0a s\u1EB5n s\xE0ng.");
    state.players.forEach((player) => {
      player.role = void 0;
      player.characterOptions = [];
      player.characterId = void 0;
      player.characterChosen = false;
      player.hand = [];
      player.cardCount = 0;
      player.equipment = [];
      player.attackRange = 1;
    });
    state.roleDeck = roleDeck(state.players.length);
    state.characterDeck = [];
    state.status = "starting";
    state.phase = "role_selection";
    state.characterSelectionDeadline = Date.now() + 6e4;
    state.publicLog.push("Moi nguoi dang chon vai tro.");
    for (const bot of state.players.filter((player) => player.bot)) this.pickRandomRole(state, bot);
    void this.ctx.storage.setAlarm(state.characterSelectionDeadline);
    if (state.players.every((player) => state.roleDeck?.some((card) => card.pickedBy === player.id))) await this.startRoleReveal(state);
  }
  async chooseRole(state, userId, cardId) {
    if (state.status !== "starting" || state.phase !== "role_selection") throw Error("Khong o giai doan chon vai tro.");
    const player = this.player(state, userId);
    if (state.roleDeck?.some((card2) => card2.pickedBy === userId)) throw Error("Ban da chon vai tro.");
    const card = state.roleDeck?.find((item) => item.id === cardId && !item.pickedBy);
    if (!card) throw Error("La vai tro khong hop le.");
    card.pickedBy = userId;
    player.role = card.value;
    if (state.players.every((item) => state.roleDeck?.some((card2) => card2.pickedBy === item.id))) await this.startRoleReveal(state);
  }
  pickRandomRole(state, player) {
    if (state.roleDeck?.some((card2) => card2.pickedBy === player.id)) return;
    const card = shuffle(state.roleDeck?.filter((item) => !item.pickedBy) ?? [])[0];
    if (!card) return;
    card.pickedBy = player.id;
    player.role = card.value;
  }
  fillMissingRoles(state) {
    for (const player of state.players.filter((item) => !state.roleDeck?.some((card) => card.pickedBy === item.id))) this.pickRandomRole(state, player);
  }
  async startRoleReveal(state) {
    this.normalizeRoles(state);
    state.phase = "role_reveal";
    state.characterSelectionDeadline = Date.now() + 1e4;
    state.publicLog.push("Vai tro da duoc lat. Chuan bi chon nhan vat sau 10 giay.");
    void this.ctx.storage.setAlarm(state.characterSelectionDeadline);
  }
  normalizeRoles(state) {
    const required = roles(state.players.length);
    const counts = /* @__PURE__ */ new Map();
    for (const role of required) counts.set(role, (counts.get(role) ?? 0) + 1);
    for (const player of state.players) {
      const role = player.role;
      const remaining = role ? counts.get(role) ?? 0 : 0;
      if (role && remaining > 0) counts.set(role, remaining - 1);
      else player.role = void 0;
    }
    const missing = [...counts.entries()].flatMap(
      ([role, count]) => Array(count).fill(role)
    );
    shuffle(state.players.filter((player) => !player.role)).forEach((player, index) => {
      player.role = missing[index];
    });
  }
  async startCharacterSelection(state) {
    this.normalizeRoles(state);
    const offered = shuffle(characters).slice(0, state.players.length * 2);
    state.characterDeck = offered.map((value) => ({ id: `character_${crypto.randomUUID()}`, value }));
    state.players.forEach((player) => {
      player.characterOptions = [];
      player.characterId = void 0;
      player.characterChosen = false;
    });
    state.phase = "character_selection";
    state.characterSelectionDeadline = Date.now() + 6e4;
    state.publicLog.push("Moi nguoi dang chon 2 la nhan vat.");
    for (const bot of state.players.filter((player) => player.bot)) {
      await this.takeRandomCharacterCard(state, bot);
      await this.takeRandomCharacterCard(state, bot);
      bot.characterId = bot.characterOptions?.[0];
      bot.characterChosen = Boolean(bot.characterId);
    }
    void this.ctx.storage.setAlarm(state.characterSelectionDeadline);
    await this.finalizeCharacters(state);
  }
  async takeCharacterCard(state, userId, cardId) {
    if (state.status !== "starting" || state.phase !== "character_selection") throw Error("Khong o giai doan chon nhan vat.");
    const player = this.player(state, userId);
    player.characterOptions ??= [];
    if (player.characterOptions.length >= 2) throw Error("Ban da chon du 2 la.");
    const card = state.characterDeck?.find((item) => item.id === cardId && !item.pickedBy);
    if (!card) throw Error("La nhan vat khong hop le.");
    card.pickedBy = userId;
    player.characterOptions.push(card.value);
    await this.advanceCharacterChoicePhase(state);
  }
  async takeRandomCharacterCard(state, player) {
    player.characterOptions ??= [];
    if (player.characterOptions.length >= 2) return;
    const card = shuffle(state.characterDeck?.filter((item) => !item.pickedBy) ?? [])[0];
    if (!card) return;
    card.pickedBy = player.id;
    player.characterOptions.push(card.value);
    await this.advanceCharacterChoicePhase(state);
  }
  async advanceCharacterChoicePhase(state) {
    if (state.phase === "character_selection" && state.players.every((player) => (player.characterOptions?.length ?? 0) >= 2)) {
      state.phase = "choosing_character";
      state.publicLog.push("Moi nguoi chon 1 trong 2 la nhan vat.");
      if (state.players.every((player) => player.characterChosen && player.characterId)) await this.finalizeCharacters(state);
    }
  }
  async fillMissingCharacters(state) {
    for (const player of state.players) {
      while ((player.characterOptions?.length ?? 0) < 2) await this.takeRandomCharacterCard(state, player);
      if (!player.characterChosen) {
        player.characterId = player.characterOptions?.[0];
        player.characterChosen = Boolean(player.characterId);
      }
    }
  }
  async chooseCharacter(state, userId, characterId) {
    if (state.status !== "starting" || state.phase !== "character_selection" && state.phase !== "choosing_character") throw Error("Khong o giai doan chon nhan vat.");
    const player = this.player(state, userId);
    if ((player.characterOptions?.length ?? 0) < 2) throw Error("Can chon du 2 la nhan vat truoc.");
    if (!player.characterOptions?.includes(characterId)) throw Error("Nh\xE2n v\u1EADt kh\xF4ng h\u1EE3p l\u1EC7.");
    if (player.characterChosen) {
      if (player.characterId !== characterId) throw Error("B\u1EA1n \u0111\xE3 ch\u1ECDn nh\xE2n v\u1EADt kh\xE1c.");
      await this.finalizeCharacters(state);
      return;
    }
    player.characterId = characterId;
    player.characterChosen = true;
    await this.finalizeCharacters(state);
  }
  async finalizeCharacters(state) {
    if (state.players.some((player) => !player.characterChosen || !player.characterId)) return;
    state.characterSelectionDeadline = void 0;
    const cards = shuffle(deck());
    for (const player of state.players) {
      player.maxHealth = characterHealth[player.characterId] + (player.role === "sheriff" ? 1 : 0);
      player.health = player.maxHealth;
      player.hand = cards.splice(0, player.maxHealth);
      player.cardCount = player.hand.length;
      player.alive = true;
      player.equipment = [];
      player.attackRange = 1;
    }
    state.deck = cards;
    state.discard = [];
    state.status = "playing";
    state.phase = "turn_start";
    state.turnOrder = state.players.slice().sort((a, b) => a.seat - b.seat).map((player) => player.id);
    const sheriff = state.players.find((player) => player.role === "sheriff");
    state.currentPlayerIndex = state.turnOrder.indexOf(sheriff.id);
    state.currentTurnPlayerId = sheriff.id;
    state.roundNumber = 1;
    state.turnNumber = 1;
    state.bangUsedThisTurn = 0;
    state.publicLog.push("Tr\u1EADn \u0111\u1EA5u b\u1EAFt \u0111\u1EA7u.");
    state.publicLog.push("Sheriff di luot dau tien.");
    state.turnDeadline = Date.now() + state.turnDurationSeconds * 1e3;
    void this.ctx.storage.setAlarm(state.turnDeadline);
    await this.runBotTurn(state);
  }
  async draw(state, userId, jesseTargetId = "", drawSource = "deck") {
    this.requireTurn(state, userId, "turn_start");
    const player = this.player(state, userId);
    if (await this.resolveTurnJudgments(state, player)) return;
    const cards = [];
    if (player.characterId === "kit_carlson") {
      const peek = this.takeCards(state, 3);
      if (peek.length <= 2) {
        cards.push(...peek);
      } else {
        state.phase = "waiting_response";
        state.pendingBang = {
          id: crypto.randomUUID(),
          actorId: player.id,
          targetId: player.id,
          deadline: Date.now() + 3e4,
          requiredDodges: 0,
          actionType: "kit_carlson",
          choices: peek
        };
        state.publicLog.push(`${player.name} \u0111ang xem 3 l\xE1 c\u1EE7a Kit Carlson.`);
        void this.ctx.storage.setAlarm(state.pendingBang.deadline);
        this.runBotResponse(state);
        return;
      }
    } else if (player.characterId === "jesse_jones" && jesseTargetId) {
      const victim = state.players.find((candidate) => candidate.alive && candidate.id === jesseTargetId && candidate.hand.length > 0);
      if (victim) {
        const stolen = victim.hand.splice(crypto.getRandomValues(new Uint32Array(1))[0] % victim.hand.length, 1)[0];
        victim.cardCount = victim.hand.length;
        cards.push(stolen);
      }
    }
    if (cards.length === 0 && player.characterId === "pedro_ramirez" && drawSource === "discard" && state.discard.length > 0) {
      cards.push(state.discard.pop());
      state.publicLog.push(`${player.name} l\u1EA5y l\xE1 \u0111\u1EA7u t\u1EEB ch\u1ED3ng b\xE0i b\u1ECF.`);
    }
    cards.push(...this.takeCards(state, 2 - cards.length));
    if (player.characterId === "black_jack" && cards[1]) {
      state.publicLog.push(`${player.name} l\u1EADt l\xE1 th\u1EE9 hai: ${cardSummary(cards[1])}.`);
      if (cards[1].endsWith("_heart") || cards[1].endsWith("_diamond")) cards.push(...this.takeCards(state, 1));
    }
    player.hand.push(...cards);
    player.cardCount = player.hand.length;
    state.phase = "play_phase";
    state.publicLog.push(`${player.name} r\xFAt 2 l\xE1.`);
  }
  chooseKitCarlson(state, userId, cardIds) {
    const pending = state.pendingBang;
    if (!pending || pending.actionType !== "kit_carlson" || pending.actorId !== userId || state.phase !== "waiting_response") throw Error("Kh\xF4ng c\xF3 l\u1EF1a ch\u1ECDn Kit Carlson h\u1EE3p l\u1EC7.");
    const choices = pending.choices ?? [];
    if (cardIds.length !== 2 || new Set(cardIds).size !== 2 || cardIds.some((card) => !choices.includes(card))) throw Error("Kit Carlson ph\u1EA3i ch\u1ECDn \u0111\xFAng 2 trong 3 l\xE1.");
    const returned = choices.find((card) => !cardIds.includes(card));
    const player = this.player(state, userId);
    player.hand.push(...cardIds);
    player.cardCount = player.hand.length;
    if (returned) state.deck.unshift(returned);
    state.pendingBang = void 0;
    state.phase = "play_phase";
    state.publicLog.push(`${player.name} \u0111\xE3 ch\u1ECDn 2 l\xE1 v\u1EDBi Kit Carlson.`);
    this.restoreTurnAlarm(state, player);
  }
  play(state, userId, cardId, targetId, payload = {}) {
    this.requireTurn(state, userId, "play_phase");
    const actor = this.player(state, userId);
    const type = typeOf(cardId);
    const at = actor.hand.indexOf(cardId);
    if (at < 0) throw Error("B\u1EA1n kh\xF4ng c\xF3 l\xE1 b\xE0i n\xE0y.");
    state.lastPlayedCardId = cardId;
    state.lastActionActorId = actor.id;
    state.lastActionTargetId = targetId || void 0;
    if (type === "bang" || type === "dodge" && actor.characterId === "calamity_janet") {
      if (state.bangUsedThisTurn > 0 && actor.characterId !== "willy_the_kid" && !actor.equipment.some((card) => card.startsWith("volcanic"))) throw Error("M\u1ED7i l\u01B0\u1EE3t ch\u1EC9 d\xF9ng 1 BANG.");
      const target = this.player(state, targetId);
      if (!target.alive || target.id === actor.id || this.distance(state, actor, target) > actor.attackRange) throw Error("M\u1EE5c ti\xEAu ngo\xE0i t\u1EA7m b\u1EAFn.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      state.discard.push(cardId);
      state.bangUsedThisTurn++;
      state.phase = "waiting_response";
      state.pendingBang = { id: crypto.randomUUID(), actorId: actor.id, targetId: target.id, cardId, deadline: Date.now() + 1e4, requiredDodges: actor.characterId === "slab_the_killer" ? 2 : 1, actionType: "bang", requiredCardType: "dodge" };
      state.publicLog.push(`${actor.name} BANG ${target.name}.`);
      void this.ctx.storage.setAlarm(state.pendingBang.deadline);
      this.runBotResponse(state);
    } else if (type === "beer") {
      if (state.players.filter((player) => player.alive).length <= 2) throw Error("Beer kh\xF4ng c\xF3 t\xE1c d\u1EE5ng khi ch\u1EC9 c\xF2n 2 ng\u01B0\u1EDDi.");
      if (actor.health >= actor.maxHealth) throw Error("M\xE1u \u0111\xE3 \u0111\u1EA7y.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      actor.health++;
      state.discard.push(cardId);
      state.publicLog.push(`${actor.name} h\u1ED3i 1 m\xE1u.`);
    } else if (type === "dilizenza" || type === "wells") {
      actor.hand.splice(at, 1);
      state.discard.push(cardId);
      this.drawFor(state, actor, type === "dilizenza" ? 2 : 3);
      state.publicLog.push(`${actor.name} r\xFAt th\xEAm b\xE0i.`);
    } else if (type === "saloon") {
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      state.discard.push(cardId);
      for (const player of state.players.filter((player2) => player2.alive)) player.health = Math.min(player.maxHealth, player.health + 1);
      state.publicLog.push(`${actor.name} d\xF9ng Saloon, m\u1ECDi ng\u01B0\u1EDDi h\u1ED3i 1 m\xE1u.`);
    } else if (type === "mustang" || type === "appaloosa") {
      if (actor.equipment.some((card) => typeOf(card) === type)) throw Error("B\u1EA1n \u0111\xE3 c\xF3 m\u1ED9t l\xE1 c\xF9ng t\xEAn \u0111ang \u0111\u1EB7t.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      actor.equipment.push(cardId);
      this.refreshAttackRange(actor);
      state.publicLog.push(`${actor.name} trang b\u1ECB ${type}.`);
    } else if (type === "panico" || type === "cat") {
      const target = this.player(state, targetId);
      if (!targetId || target.id === actor.id || !target.alive) throw Error("C\u1EA7n ch\u1ECDn m\u1EE5c ti\xEAu c\xF2n s\u1ED1ng.");
      if (type === "panico" && this.distance(state, actor, target) > 1) throw Error("Panico ch\u1EC9 d\xF9ng \u1EDF kho\u1EA3ng c\xE1ch 1.");
      const equipmentCardId = typeof payload.equipmentCardId === "string" ? payload.equipmentCardId : "";
      let taken;
      if (equipmentCardId) {
        const index = target.equipment.indexOf(equipmentCardId);
        if (index >= 0) taken = target.equipment.splice(index, 1)[0];
      } else if (target.hand.length > 0) {
        taken = target.hand.splice(crypto.getRandomValues(new Uint32Array(1))[0] % target.hand.length, 1)[0];
      }
      if (!taken) throw Error("M\u1EE5c ti\xEAu kh\xF4ng c\xF2n b\xE0i ho\u1EB7c trang b\u1ECB.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      target.cardCount = target.hand.length;
      state.discard.push(cardId);
      this.refreshAttackRange(target);
      if (type === "panico") {
        actor.hand.push(taken);
        actor.cardCount = actor.hand.length;
        state.publicLog.push(`${actor.name} c\u01B0\u1EDBp 1 l\xE1 c\u1EE7a ${target.name}.`);
      } else {
        state.discard.push(taken);
        state.publicLog.push(`${actor.name} ph\xE1 1 l\xE1 c\u1EE7a ${target.name}.`);
      }
    } else if (type === "general") {
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      state.discard.push(cardId);
      const pickerOrder = this.aliveOrderFrom(state, actor.id).map((player) => player.id);
      const openedCardIds = this.takeCards(state, pickerOrder.length);
      if (openedCardIds.length === 0) throw Error("B\u1ED9 b\xE0i \u0111\xE3 h\u1EBFt.");
      state.phase = "waiting_response";
      state.pendingBang = {
        id: crypto.randomUUID(),
        actorId: actor.id,
        targetId: actor.id,
        cardId,
        deadline: Date.now() + 3e4,
        requiredDodges: 0,
        actionType: "general_store",
        openedCardIds,
        pickerOrder,
        pickerIndex: 0,
        currentPickerId: actor.id
      };
      state.publicLog.push(`${actor.name} m\u1EDF General Store.`);
      void this.ctx.storage.setAlarm(state.pendingBang.deadline);
      this.runBotResponse(state);
    } else if (type === "gatling" || type === "indiani") {
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      state.discard.push(cardId);
      const targets = state.players.filter((player) => player.alive && player.id !== actor.id).map((player) => player.id);
      if (targets.length === 0) throw Error("Kh\xF4ng c\xF3 m\u1EE5c ti\xEAu.");
      state.phase = "waiting_response";
      state.pendingBang = { id: crypto.randomUUID(), actorId: actor.id, targetId: targets[0], cardId, deadline: Date.now() + 1e4, requiredDodges: 1, actionType: type, requiredCardType: type === "gatling" ? "dodge" : "bang", targets, targetIndex: 0 };
      state.publicLog.push(`${actor.name} d\xF9ng ${type}.`);
      void this.ctx.storage.setAlarm(state.pendingBang.deadline);
      this.runBotResponse(state);
    } else if (type === "duello") {
      const target = this.player(state, targetId);
      if (!targetId || target.id === actor.id || !target.alive) throw Error("C\u1EA7n ch\u1ECDn m\u1EE5c ti\xEAu c\xF2n s\u1ED1ng.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      state.discard.push(cardId);
      state.phase = "waiting_response";
      state.pendingBang = { id: crypto.randomUUID(), actorId: actor.id, targetId: target.id, cardId, deadline: Date.now() + 1e4, requiredDodges: 1, actionType: "duello", requiredCardType: "bang", duelPlayerA: actor.id, duelPlayerB: target.id };
      state.publicLog.push(`${actor.name} th\xE1ch \u0111\u1EA5u ${target.name}.`);
      void this.ctx.storage.setAlarm(state.pendingBang.deadline);
      this.runBotResponse(state);
    } else if (this.isWeapon(cardId)) {
      if (actor.equipment.some((card) => typeOf(card) === type)) throw Error("B\u1EA1n \u0111\xE3 \u0111\u1EB7t m\u1ED9t kh\u1EA9u s\xFAng c\xF9ng lo\u1EA1i.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      actor.equipment.push(cardId);
      this.refreshAttackRange(actor);
      state.publicLog.push(`${actor.name} trang b\u1ECB s\xFAng t\u1EA7m ${actor.attackRange}.`);
    } else if (type === "barrel") {
      if (actor.equipment.some((card) => typeOf(card) === "barrel")) throw Error("B\u1EA1n \u0111\xE3 c\xF3 Barrel.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      actor.equipment.push(cardId);
      state.publicLog.push(`${actor.name} \u0111\u1EB7t Barrel.`);
    } else if (type === "dynamite") {
      if (actor.equipment.some((card) => typeOf(card) === "dynamite")) throw Error("B\u1EA1n \u0111\xE3 c\xF3 Dynamite.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      actor.equipment.push(cardId);
      state.publicLog.push(`${actor.name} \u0111\u1EB7t Dynamite.`);
    } else if (type === "jail") {
      const target = this.player(state, targetId);
      if (!targetId || target.id === actor.id || target.role === "sheriff") throw Error("Jail ph\u1EA3i \u0111\u1EB7t l\xEAn ng\u01B0\u1EDDi kh\xE1c, kh\xF4ng ph\u1EA3i C\u1EA3nh s\xE1t tr\u01B0\u1EDFng.");
      if (target.equipment.some((card) => typeOf(card) === "jail")) throw Error("M\u1EE5c ti\xEAu \u0111\xE3 c\xF3 Jail.");
      actor.hand.splice(at, 1);
      actor.cardCount = actor.hand.length;
      target.equipment.push(cardId);
      state.publicLog.push(`${actor.name} nh\u1ED1t ${target.name}.`);
    } else throw Error("Th\u1EBB n\xE0y s\u1EBD \u0111\u01B0\u1EE3c m\u1EDF \u1EDF b\u01B0\u1EDBc hi\u1EC7u \u1EE9ng n\xE2ng cao.");
  }
  respondBang(state, userId, response, cardId, cardIds = []) {
    const pending = state.pendingBang;
    if (!pending || pending.targetId !== userId || state.phase !== "waiting_response") throw Error("Kh\xF4ng c\xF3 ph\u1EA3n \u1EE9ng h\u1EE3p l\u1EC7.");
    const target = this.player(state, userId);
    const required = pending.requiredCardType ?? "dodge";
    if (pending.actionType === "bang" && response !== "dodge" && !pending.judgmentsResolved) {
      const attempts = this.judgmentDodgeAttempts(target);
      if (target.characterId === "lucky_duke" && attempts > 0) {
        this.openLuckyJudgment(state, target, "barrel", void 0, pending, attempts);
        return;
      }
      const automaticDodges = this.heartJudgmentDodges(state, target);
      if (automaticDodges > 0) {
        state.publicLog.push(`${target.name} c\xF3 ${automaticDodges} l\u1EA7n N\xE9 nh\u1EDD Barrel/k\u1EF9 n\u0103ng.`);
        if (automaticDodges >= pending.requiredDodges) {
          this.advancePendingResponse(state, pending);
        } else {
          pending.requiredDodges -= automaticDodges;
          pending.judgmentsResolved = true;
          pending.deadline = Date.now() + 1e4;
          state.pendingBang = pending;
          void this.ctx.storage.setAlarm(pending.deadline);
        }
        return;
      }
    }
    if (response === "dodge" || response === "card") {
      const valid = /* @__PURE__ */ __name((card) => typeOf(card) === required || required === "dodge" && target.characterId === "calamity_janet" && typeOf(card) === "bang", "valid");
      const cards = target.hand.filter(valid);
      if (cards.length < pending.requiredDodges) throw Error("Kh\xF4ng \u0111\u1EE7 b\xE0i ph\u1EA3n \u1EE9ng.");
      const spent = pending.requiredDodges === 1 ? [cardId] : cardIds;
      if (spent.length !== pending.requiredDodges || new Set(spent).size !== spent.length) throw Error("C\u1EA7n ch\u1ECDn \u0111\xFAng s\u1ED1 l\xE1 ph\u1EA3n \u1EE9ng.");
      if (spent.some((card) => !target.hand.includes(card) || !valid(card))) throw Error("L\xE1 ph\u1EA3n \u1EE9ng kh\xF4ng h\u1EE3p l\u1EC7.");
      target.hand = target.hand.filter((card) => !spent.includes(card));
      target.cardCount = target.hand.length;
      state.discard.push(...spent);
      state.publicLog.push(`${target.name} \u0111\xE3 ph\u1EA3n \u1EE9ng.`);
      this.advancePendingResponse(state, pending);
      return;
    }
    const waitingForRescue = this.damage(state, target.id, pending.actorId, pending.actionType ?? "bang", 1, pending, true);
    if (!waitingForRescue) {
      if (pending.actionType === "duello") this.finishPendingResponse(state, pending);
      else this.advancePendingResponse(state, pending);
    }
  }
  chooseGeneralStore(state, userId, cardId) {
    const pending = state.pendingBang;
    if (!pending || pending.actionType !== "general_store" || pending.currentPickerId !== userId) throw Error("Ch\u01B0a \u0111\u1EBFn l\u01B0\u1EE3t ch\u1ECDn \u1EDF General Store.");
    const opened = pending.openedCardIds ?? [];
    const cardIndex = opened.indexOf(cardId);
    if (cardIndex < 0) throw Error("L\xE1 n\xE0y kh\xF4ng c\xF2n trong General Store.");
    opened.splice(cardIndex, 1);
    const player = this.player(state, userId);
    player.hand.push(cardId);
    player.cardCount = player.hand.length;
    const nextIndex = (pending.pickerIndex ?? 0) + 1;
    const nextId = pending.pickerOrder?.[nextIndex];
    if (nextId && opened.length > 0) {
      pending.openedCardIds = opened;
      pending.pickerIndex = nextIndex;
      pending.currentPickerId = nextId;
      pending.targetId = nextId;
      pending.deadline = Date.now() + 3e4;
      state.pendingBang = pending;
      void this.ctx.storage.setAlarm(pending.deadline);
      this.runBotResponse(state);
      return;
    }
    state.discard.push(...opened);
    state.publicLog.push("General Store k\u1EBFt th\xFAc.");
    this.finishPendingResponse(state, pending);
  }
  advancePendingResponse(state, pending) {
    if (pending.actionType === "duello") {
      const next = pending.targetId === pending.duelPlayerA ? pending.duelPlayerB : pending.duelPlayerA;
      if (next && this.player(state, next).alive) {
        pending.targetId = next;
        pending.deadline = Date.now() + 1e4;
        state.pendingBang = pending;
        void this.ctx.storage.setAlarm(pending.deadline);
        this.runBotResponse(state);
        return;
      }
    }
    if (pending.targets) {
      for (let index = (pending.targetIndex ?? 0) + 1; index < pending.targets.length; index++) {
        const next = this.player(state, pending.targets[index]);
        if (next.alive) {
          pending.targetIndex = index;
          pending.targetId = next.id;
          pending.deadline = Date.now() + 1e4;
          state.pendingBang = pending;
          void this.ctx.storage.setAlarm(pending.deadline);
          this.runBotResponse(state);
          return;
        }
      }
    }
    this.finishPendingResponse(state, pending);
  }
  finishPendingResponse(state, pending) {
    state.pendingBang = void 0;
    if (state.status !== "playing") return;
    state.phase = "play_phase";
    const actor = this.player(state, pending.actorId);
    if (actor.bot && state.currentTurnPlayerId === actor.id) {
      state.turnDeadline = Date.now() + 100;
      void this.ctx.storage.setAlarm(state.turnDeadline);
    } else if (state.currentTurnPlayerId === actor.id) {
      state.turnDeadline = Math.max(state.turnDeadline ?? 0, Date.now() + 100);
      void this.ctx.storage.setAlarm(state.turnDeadline);
    }
  }
  restoreTurnAlarm(state, player) {
    if (state.status !== "playing" || state.currentTurnPlayerId !== player.id) return;
    state.turnDeadline = Math.max(state.turnDeadline ?? 0, Date.now() + 100);
    void this.ctx.storage.setAlarm(state.turnDeadline);
  }
  runBotResponse(state) {
    const pending = state.pendingBang;
    if (!pending) return;
    const bot = this.player(state, pending.targetId);
    if (!bot.bot) return;
    if (pending.actionType === "kit_carlson") {
      this.chooseKitCarlson(state, bot.id, (pending.choices ?? []).slice(0, 2));
      return;
    }
    if (pending.actionType === "rescue") {
      this.resolveRescue(state, bot.id, this.autoRescueCards(state, bot, pending.requiredHealth ?? 1));
      return;
    }
    if (pending.actionType === "general_store") {
      const card = pending.openedCardIds?.[0];
      if (card) this.chooseGeneralStore(state, bot.id, card);
      return;
    }
    const required = pending.requiredCardType ?? "dodge";
    const cards = bot.hand.filter((card) => typeOf(card) === required || required === "dodge" && bot.characterId === "calamity_janet" && typeOf(card) === "bang");
    this.respondBang(state, bot.id, cards.length >= pending.requiredDodges ? "card" : "damage", cards[0] ?? "", cards.slice(0, pending.requiredDodges));
  }
  useSidKetchum(state, userId, cardIds) {
    const player = this.player(state, userId);
    if (state.status !== "playing" || !player.alive) throw Error("Kh\xF4ng th\u1EC3 d\xF9ng k\u1EF9 n\u0103ng l\xFAc n\xE0y.");
    if (player.characterId !== "sid_ketchum" || cardIds.length !== 2 || new Set(cardIds).size !== 2 || cardIds.some((card) => !player.hand.includes(card))) throw Error("Sid Ketchum c\u1EA7n b\u1ECF \u0111\xFAng 2 l\xE1 tr\xEAn tay.");
    if (player.health >= player.maxHealth) throw Error("M\xE1u \u0111\xE3 \u0111\u1EA7y.");
    player.hand = player.hand.filter((card) => !cardIds.includes(card));
    player.cardCount = player.hand.length;
    state.discard.push(...cardIds);
    player.health++;
    state.publicLog.push(`${player.name} b\u1ECF 2 l\xE1 \u0111\u1EC3 h\u1ED3i 1 m\xE1u.`);
    this.maybeSuzy(state, player);
  }
  async endTurn(state, userId) {
    this.requireTurn(state, userId, "play_phase");
    const player = this.player(state, userId);
    if (player.hand.length > player.health) {
      state.phase = "discard_phase";
      return;
    }
    await this.advanceTurn(state, "K\u1EBFt th\xFAc l\u01B0\u1EE3t");
  }
  judge(state, player, preferred = () => false) {
    const cards = this.takeCards(state, player.characterId === "lucky_duke" ? 2 : 1);
    if (cards.length === 0) return void 0;
    const chosen = cards.find(preferred) ?? cards[0];
    state.discard.push(...cards);
    if (player.characterId === "lucky_duke" && cards.length === 2) {
      state.publicLog.push(`${player.name} d\xF9ng Lucky Duke ch\u1ECDn ph\xE1n x\xE9t.`);
    }
    return chosen;
  }
  isHeart(card) {
    return card?.endsWith("_heart") === true;
  }
  async resolveTurnJudgments(state, player) {
    const dynamite = player.equipment.find((card) => typeOf(card) === "dynamite");
    if (dynamite) {
      player.equipment = player.equipment.filter((card2) => card2 !== dynamite);
      if (player.characterId === "lucky_duke") {
        this.openLuckyJudgment(state, player, "dynamite", dynamite);
        return true;
      }
      const card = this.judge(
        state,
        player,
        (value) => !(value.endsWith("_spade") && /_(two|three|four|five|six|seven|eight|nine)_spade$/.test(value))
      );
      if (card?.endsWith("_spade") && /_(two|three|four|five|six|seven|eight|nine)_spade$/.test(card)) {
        state.discard.push(dynamite);
        const waitingForRescue = this.damage(state, player.id, player.id, "Dynamite n\u1ED5, m\u1EA5t 3 m\xE1u", 3, void 0, false, "turn_start");
        if (waitingForRescue || !player.alive || state.status !== "playing") return true;
      } else {
        const next = this.nextAlivePlayerWithout(state, player.id, "dynamite");
        if (next) {
          next.equipment.push(dynamite);
          state.publicLog.push(`Dynamite chuy\u1EC3n sang ${next.name}.`);
        } else {
          state.discard.push(dynamite);
        }
      }
    }
    const jail = player.equipment.find((card) => typeOf(card) === "jail");
    if (jail && player.alive) {
      player.equipment = player.equipment.filter((card2) => card2 !== jail);
      state.discard.push(jail);
      if (player.characterId === "lucky_duke") {
        this.openLuckyJudgment(state, player, "jail", jail);
        return true;
      }
      const card = this.judge(state, player, (value) => this.isHeart(value));
      if (!this.isHeart(card)) {
        state.publicLog.push(`${player.name} kh\xF4ng tho\xE1t Jail v\xE0 m\u1EA5t l\u01B0\u1EE3t.`);
        await this.advanceTurn(state, "M\u1EA5t l\u01B0\u1EE3t v\xEC Jail");
        return true;
      }
      state.publicLog.push(`${player.name} tho\xE1t Jail.`);
    }
    return state.status !== "playing";
  }
  openLuckyJudgment(state, player, kind, judgmentCardId, resumePending, attemptsRemaining, dodgesApplied = 0) {
    const choices = this.takeCards(state, 2);
    if (choices.length === 0) throw Error("Kh\xF4ng c\xF2n l\xE1 \u0111\u1EC3 ph\xE1n x\xE9t.");
    state.phase = "waiting_response";
    state.pendingBang = {
      id: crypto.randomUUID(),
      actorId: player.id,
      targetId: player.id,
      deadline: Date.now() + (player.bot ? 100 : 3e4),
      requiredDodges: 0,
      actionType: "lucky_duke_judgment",
      choices,
      judgmentKind: kind,
      judgmentCardId,
      resumePending,
      judgmentAttemptsRemaining: attemptsRemaining,
      judgmentDodgesApplied: dodgesApplied
    };
    state.publicLog.push(`${player.name} ch\u1ECDn 1 trong 2 l\xE1 ph\xE1n x\xE9t.`);
    void this.ctx.storage.setAlarm(state.pendingBang.deadline);
  }
  preferredLuckyChoice(pending) {
    const choices = pending.choices ?? [];
    if (pending.judgmentKind === "jail" || pending.judgmentKind === "barrel") return choices.find((card) => this.isHeart(card)) ?? choices[0] ?? "";
    if (pending.judgmentKind === "dynamite") {
      return choices.find((card) => !(card.endsWith("_spade") && /_(two|three|four|five|six|seven|eight|nine)_spade$/.test(card))) ?? choices[0] ?? "";
    }
    return choices[0] ?? "";
  }
  async chooseLuckyDuke(state, userId, resultCardId) {
    const pending = state.pendingBang;
    if (!pending || pending.actionType !== "lucky_duke_judgment" || pending.actorId !== userId || state.phase !== "waiting_response") throw Error("Kh\xF4ng c\xF3 ph\xE1n x\xE9t Lucky Duke h\u1EE3p l\u1EC7.");
    const choices = pending.choices ?? [];
    if (!choices.includes(resultCardId)) throw Error("L\xE1 ph\xE1n x\xE9t kh\xF4ng h\u1EE3p l\u1EC7.");
    state.discard.push(...choices);
    state.pendingBang = void 0;
    state.phase = "turn_start";
    const player = this.player(state, userId);
    if (pending.judgmentKind === "dynamite") {
      const exploded = resultCardId.endsWith("_spade") && /_(two|three|four|five|six|seven|eight|nine)_spade$/.test(resultCardId);
      if (exploded) {
        if (pending.judgmentCardId) state.discard.push(pending.judgmentCardId);
        const waiting = this.damage(state, player.id, player.id, "Dynamite n\u1ED5, m\u1EA5t 3 m\xE1u", 3, void 0, false, "turn_start");
        if (waiting || !player.alive || state.status !== "playing") return;
      } else if (pending.judgmentCardId) {
        const next = this.nextAlivePlayerWithout(state, player.id, "dynamite");
        if (next) {
          next.equipment.push(pending.judgmentCardId);
          state.publicLog.push(`Dynamite chuy\u1EC3n sang ${next.name}.`);
        } else {
          state.discard.push(pending.judgmentCardId);
        }
      }
      await this.draw(state, userId);
      this.restoreTurnAlarm(state, player);
      return;
    }
    if (pending.judgmentKind === "barrel") {
      const original = pending.resumePending;
      if (!original) throw Error("Thi\u1EBFu h\xE0nh \u0111\u1ED9ng BANG \u0111ang ch\u1EDD.");
      const succeeded = this.isHeart(resultCardId);
      if (succeeded) original.requiredDodges--;
      const applied = (pending.judgmentDodgesApplied ?? 0) + (succeeded ? 1 : 0);
      const attempts = (pending.judgmentAttemptsRemaining ?? 1) - 1;
      if (original.requiredDodges <= 0) {
        state.pendingBang = original;
        this.advancePendingResponse(state, original);
        return;
      }
      if (attempts > 0) {
        this.openLuckyJudgment(state, player, "barrel", void 0, original, attempts, applied);
        return;
      }
      if (applied > 0) {
        original.judgmentsResolved = true;
        original.deadline = Date.now() + 1e4;
        state.pendingBang = original;
        state.phase = "waiting_response";
        void this.ctx.storage.setAlarm(original.deadline);
        return;
      }
      state.pendingBang = original;
      state.phase = "waiting_response";
      const waiting = this.damage(state, player.id, original.actorId, original.actionType ?? "bang", 1, original, true);
      if (!waiting) this.advancePendingResponse(state, original);
      return;
    }
    if (pending.judgmentKind === "jail" && !this.isHeart(resultCardId)) {
      state.publicLog.push(`${player.name} kh\xF4ng tho\xE1t Jail v\xE0 m\u1EA5t l\u01B0\u1EE3t.`);
      await this.advanceTurn(state, "M\u1EA5t l\u01B0\u1EE3t v\xEC Jail");
      return;
    }
    state.publicLog.push(`${player.name} tho\xE1t Jail.`);
    await this.draw(state, userId);
    this.restoreTurnAlarm(state, player);
  }
  judgmentDodgeAttempts(player) {
    return (player.characterId === "jourdonnais" ? 1 : 0) + (player.equipment.some((card) => typeOf(card) === "barrel") ? 1 : 0);
  }
  heartJudgmentDodges(state, player) {
    const attempts = this.judgmentDodgeAttempts(player);
    let successes = 0;
    for (let index = 0; index < attempts; index++) {
      if (this.isHeart(this.judge(state, player, (value) => this.isHeart(value)))) successes++;
    }
    return successes;
  }
  async discardCards(state, userId, cards) {
    this.requireTurn(state, userId, "discard_phase");
    const player = this.player(state, userId);
    const required = player.hand.length - player.health;
    if (cards.length !== required || cards.some((card) => !player.hand.includes(card))) throw Error(`Ph\u1EA3i b\u1ECF \u0111\xFAng ${required} l\xE1.`);
    player.hand = player.hand.filter((card) => !cards.includes(card));
    player.cardCount = player.hand.length;
    state.discard.push(...cards);
    state.phase = "turn_start";
    await this.advanceTurn(state, "B\u1ECF b\xE0i");
  }
  async resolveTurnTimeout(state) {
    const currentId = state.currentTurnPlayerId;
    if (!currentId) return;
    if (state.phase === "turn_start") {
      await this.draw(state, currentId);
      if (state.phase !== "play_phase") return;
    }
    const player = this.player(state, currentId);
    const excess = Math.max(0, player.hand.length - player.health);
    if (excess > 0) {
      const discarded = shuffle(player.hand).slice(0, excess);
      player.hand = player.hand.filter((card) => !discarded.includes(card));
      player.cardCount = player.hand.length;
      state.discard.push(...discarded);
      state.publicLog.push(`${player.name} h\u1EBFt gi\u1EDD v\xE0 b\u1ECF ${discarded.length} l\xE1 d\u01B0.`);
    }
    await this.advanceTurn(state, "H\u1EBFt gi\u1EDD");
  }
  async advanceTurn(state, reason) {
    const order = state.turnOrder ?? state.players.slice().sort((a, b) => a.seat - b.seat).map((player) => player.id);
    if (!order.length || state.status !== "playing") return;
    const startIndex = state.currentPlayerIndex ?? Math.max(0, order.indexOf(state.currentTurnPlayerId ?? ""));
    let nextIndex = startIndex;
    for (let offset = 1; offset <= order.length; offset++) {
      const candidateIndex = (startIndex + offset) % order.length;
      const candidate = state.players.find((player) => player.id === order[candidateIndex]);
      if (candidate?.alive) {
        nextIndex = candidateIndex;
        break;
      }
    }
    if (nextIndex <= startIndex) state.roundNumber = (state.roundNumber ?? 1) + 1;
    state.turnOrder = order;
    state.currentPlayerIndex = nextIndex;
    state.currentTurnPlayerId = order[nextIndex];
    state.turnNumber++;
    state.bangUsedThisTurn = 0;
    state.phase = "turn_start";
    state.pendingBang = void 0;
    state.publicLog.push(reason);
    const nextPlayer = this.player(state, state.currentTurnPlayerId);
    state.turnDeadline = Date.now() + (nextPlayer.bot ? 850 : state.turnDurationSeconds * 1e3);
    void this.ctx.storage.setAlarm(state.turnDeadline);
  }
  async runBotTurn(state) {
    const bot = state.players.find((player) => player.id === state.currentTurnPlayerId);
    if (!bot?.bot || state.status !== "playing" || state.phase !== "turn_start") return;
    await this.draw(state, bot.id);
    if (String(state.phase) !== "play_phase" || !bot.alive) return;
    const opponents = shuffle(state.players.filter((player) => player.alive && player.id !== bot.id));
    const targetsInRange = opponents.filter((player) => this.distance(state, bot, player) <= bot.attackRange);
    const adjacentTargets = opponents.filter((player) => this.distance(state, bot, player) <= 1);
    const beer = bot.hand.find((card) => typeOf(card) === "beer");
    const bang = bot.hand.find((card) => typeOf(card) === "bang");
    const areaAttack = bot.hand.find((card) => typeOf(card) === "gatling" || typeOf(card) === "indiani");
    const duel = bot.hand.find((card) => typeOf(card) === "duello");
    const targetCard = bot.hand.find((card) => typeOf(card) === "panico" || typeOf(card) === "cat");
    const equipment = bot.hand.find((card) => {
      const type = typeOf(card);
      return type === "mustang" || type === "appaloosa" || type === "volcanic" || type === "barrel" || type === "dynamite" || type.startsWith("gun_range");
    });
    const drawCard = bot.hand.find((card) => typeOf(card) === "dilizenza" || typeOf(card) === "wells");
    if (beer && bot.health < bot.maxHealth) {
      this.play(state, bot.id, beer, "");
    } else if (bang && targetsInRange[0]) {
      this.play(state, bot.id, bang, targetsInRange[0].id);
    } else if (areaAttack && opponents.length > 1) {
      this.play(state, bot.id, areaAttack, "");
    } else if (duel && opponents[0]) {
      this.play(state, bot.id, duel, opponents[0].id);
    } else if (targetCard) {
      const type = typeOf(targetCard);
      const candidates = (type === "panico" ? adjacentTargets : opponents).filter((player) => player.hand.length > 0 || player.equipment.length > 0);
      if (candidates[0]) this.play(state, bot.id, targetCard, candidates[0].id);
    } else if (equipment) {
      this.play(state, bot.id, equipment, "");
    } else if (drawCard) {
      this.play(state, bot.id, drawCard, "");
    }
    if (String(state.phase) === "play_phase") await this.endTurn(state, bot.id);
    if (String(state.phase) === "discard_phase" && state.currentTurnPlayerId === bot.id) {
      const excess = Math.max(0, bot.hand.length - bot.health);
      await this.discardCards(state, bot.id, bot.hand.slice(0, excess));
    }
  }
  damage(state, targetId, actorId, log, amount = 1, resumePending, causedByPlayer = true, resumePhase = "play_phase") {
    const target = this.player(state, targetId);
    const actor = state.players.find((player) => player.id === actorId);
    target.health -= amount;
    if (target.characterId === "bart_cassidy") this.drawFor(state, target, amount);
    if (causedByPlayer && target.characterId === "el_gringo" && actor) {
      for (let index = 0; index < amount && actor.hand.length > 0; index++) {
        const stolen = actor.hand.splice(crypto.getRandomValues(new Uint32Array(1))[0] % actor.hand.length, 1)[0];
        target.hand.push(stolen);
      }
      actor.cardCount = actor.hand.length;
      target.cardCount = target.hand.length;
    }
    state.publicLog.push(`${log}: ${target.name}.`);
    if (target.health > 0) return false;
    const requiredHealth = 1 - target.health;
    if (this.availableRescueHealing(state, target) >= requiredHealth) {
      state.phase = "waiting_response";
      state.pendingBang = {
        id: crypto.randomUUID(),
        actorId,
        targetId,
        deadline: Date.now() + 1e4,
        requiredDodges: 0,
        actionType: "rescue",
        requiredHealth,
        resumePending,
        resumePhase,
        causedByPlayer
      };
      state.publicLog.push(`${target.name} c\xF3 10 gi\xE2y \u0111\u1EC3 t\u1EF1 c\u1EE9u.`);
      void this.ctx.storage.setAlarm(state.pendingBang.deadline);
      this.runBotResponse(state);
      return true;
    }
    this.eliminate(state, target, actor, causedByPlayer);
    return false;
  }
  availableRescueHealing(state, player) {
    const beerHealing = state.players.filter((candidate) => candidate.alive).length > 2 ? player.hand.filter((card) => typeOf(card) === "beer").length : 0;
    const sidHealing = player.characterId === "sid_ketchum" ? Math.floor(player.hand.length / 2) : 0;
    return Math.max(beerHealing, sidHealing, player.characterId === "sid_ketchum" ? beerHealing + Math.floor(player.hand.filter((card) => typeOf(card) !== "beer").length / 2) : beerHealing);
  }
  autoRescueCards(state, player, requiredHealth) {
    const selected = [];
    let healing = 0;
    if (state.players.filter((candidate) => candidate.alive).length > 2) {
      for (const card of player.hand.filter((item) => typeOf(item) === "beer")) {
        selected.push(card);
        healing++;
        if (healing >= requiredHealth) return selected;
      }
    }
    if (player.characterId === "sid_ketchum") {
      const remaining = player.hand.filter((card) => !selected.includes(card));
      while (healing < requiredHealth && remaining.length >= 2) {
        selected.push(remaining.shift(), remaining.shift());
        healing++;
      }
    }
    return healing >= requiredHealth ? selected : [];
  }
  resolveRescue(state, userId, cardIds) {
    const pending = state.pendingBang;
    if (!pending || pending.actionType !== "rescue" || pending.targetId !== userId || state.phase !== "waiting_response") throw Error("Kh\xF4ng c\xF3 t\xECnh hu\u1ED1ng c\u1EE9u m\u1EA1ng h\u1EE3p l\u1EC7.");
    const player = this.player(state, userId);
    if (new Set(cardIds).size !== cardIds.length || cardIds.some((card) => !player.hand.includes(card))) throw Error("L\xE1 c\u1EE9u m\u1EA1ng kh\xF4ng h\u1EE3p l\u1EC7.");
    const beerAllowed = state.players.filter((candidate) => candidate.alive).length > 2;
    const beers = beerAllowed ? cardIds.filter((card) => typeOf(card) === "beer") : [];
    const sidCards = cardIds.filter((card) => !beers.includes(card));
    const healing = beers.length + (player.characterId === "sid_ketchum" ? Math.floor(sidCards.length / 2) : 0);
    const required = pending.requiredHealth ?? 1;
    if (player.characterId === "sid_ketchum" && sidCards.length % 2 !== 0) throw Error("Sid Ketchum ph\u1EA3i b\u1ECF b\xE0i theo t\u1EEBng c\u1EB7p.");
    if (cardIds.length > 0 && healing !== required) throw Error(`Ph\u1EA3i h\u1ED3i \u0111\xFAng ${required} m\xE1u \u0111\u1EC3 s\u1ED1ng.`);
    if (cardIds.length > 0) {
      player.hand = player.hand.filter((card) => !cardIds.includes(card));
      player.cardCount = player.hand.length;
      state.discard.push(...cardIds);
      player.health += healing;
      state.publicLog.push(`${player.name} t\u1EF1 c\u1EE9u v\xE0 c\xF2n ${player.health} m\xE1u.`);
    } else {
      const actor = state.players.find((candidate) => candidate.id === pending.actorId);
      this.eliminate(state, player, actor, pending.causedByPlayer === true);
    }
    this.resumeAfterRescue(state, pending);
  }
  eliminate(state, target, actor, causedByPlayer) {
    const loot = [...target.hand, ...target.equipment];
    target.alive = false;
    target.health = 0;
    target.hand = [];
    target.equipment = [];
    target.cardCount = 0;
    const vulture = state.players.find((player) => player.alive && player.characterId === "vulture_sam");
    if (vulture && loot.length > 0) {
      vulture.hand.push(...loot);
      vulture.cardCount = vulture.hand.length;
    } else {
      state.discard.push(...loot);
    }
    state.publicLog.push(`${target.name} b\u1ECB lo\u1EA1i v\xE0 l\u1EADt vai tr\xF2 ${target.role}.`);
    if (causedByPlayer && actor) {
      if (target.role === "outlaw") {
        this.drawFor(state, actor, 3);
        state.publicLog.push(`${actor.name} nh\u1EADn th\u01B0\u1EDFng 3 l\xE1 v\xEC h\u1EA1 C\u01B0\u1EDBp.`);
      } else if (actor.role === "sheriff" && target.role === "deputy") {
        state.discard.push(...actor.hand, ...actor.equipment);
        actor.hand = [];
        actor.equipment = [];
        actor.cardCount = 0;
        this.refreshAttackRange(actor);
        state.publicLog.push(`${actor.name} m\u1EA5t to\xE0n b\u1ED9 b\xE0i v\xEC h\u1EA1 Ph\xF3 c\u1EA3nh s\xE1t.`);
      }
    }
    this.checkWin(state, actor?.id ?? target.id);
  }
  resumeAfterRescue(state, rescue) {
    const original = rescue.resumePending;
    state.pendingBang = void 0;
    if (state.status !== "playing") return;
    state.pendingBang = original;
    if (original) {
      if (original.actionType === "duello") this.finishPendingResponse(state, original);
      else this.advancePendingResponse(state, original);
      return;
    }
    state.pendingBang = void 0;
    state.phase = rescue.resumePhase ?? "play_phase";
    const current = state.players.find((player) => player.id === state.currentTurnPlayerId);
    if (current) this.restoreTurnAlarm(state, current);
  }
  takeCards(state, amount) {
    const cards = [];
    while (cards.length < amount) {
      if (state.deck.length === 0) {
        if (state.discard.length === 0) break;
        state.deck = shuffle(state.discard);
        state.discard = [];
        state.publicLog.push("Ch\u1ED3ng b\xE0i b\u1ECF \u0111\xE3 \u0111\u01B0\u1EE3c x\xE1o l\u1EA1i th\xE0nh ch\u1ED3ng b\xE0i r\xFAt.");
      }
      const card = state.deck.shift();
      if (card) cards.push(card);
    }
    return cards;
  }
  drawFor(state, player, amount) {
    const cards = this.takeCards(state, amount);
    player.hand.push(...cards);
    player.cardCount = player.hand.length;
  }
  maybeSuzy(state, player) {
    if (player.alive && player.characterId === "suzy_lafayette" && player.hand.length === 0 && (state.deck.length > 0 || state.discard.length > 0)) {
      this.drawFor(state, player, 1);
      state.publicLog.push(`${player.name} k\xEDch ho\u1EA1t Suzy Lafayette.`);
    }
  }
  checkWin(state, actorId) {
    const alive = state.players.filter((player) => player.alive);
    const sheriff = state.players.find((player) => player.role === "sheriff");
    if (!sheriff.alive) {
      const loneRenegade = alive.length === 1 && alive[0].role === "renegade";
      state.winner = loneRenegade ? "renegade" : "outlaws";
    } else if (!state.players.some((player) => player.alive && (player.role === "outlaw" || player.role === "renegade"))) state.winner = "law";
    if (state.winner) {
      state.status = "finished";
      state.phase = "game_over";
      state.publicLog.push(`K\u1EBFt th\xFAc: ${state.winner}.`);
    }
  }
  nextAlivePlayer(state, currentId) {
    const alive = state.players.filter((player) => player.alive).sort((a, b) => a.seat - b.seat);
    const index = alive.findIndex((player) => player.id === currentId);
    return alive[(index + 1) % alive.length];
  }
  aliveOrderFrom(state, firstId) {
    const alive = state.players.filter((player) => player.alive).sort((a, b) => a.seat - b.seat);
    const index = alive.findIndex((player) => player.id === firstId);
    if (index < 0) return alive;
    return [...alive.slice(index), ...alive.slice(0, index)];
  }
  isWeapon(cardId) {
    const type = typeOf(cardId);
    return type === "volcanic" || type.startsWith("gun_range");
  }
  refreshAttackRange(player) {
    const ranges = player.equipment.filter((card) => typeOf(card).startsWith("gun_range")).map((card) => Number(typeOf(card).at(-1) || 1));
    player.attackRange = Math.max(1, ...ranges);
  }
  nextAlivePlayerWithout(state, currentId, equipmentType) {
    const alive = state.players.filter((player) => player.alive).sort((a, b) => a.seat - b.seat);
    const index = alive.findIndex((player) => player.id === currentId);
    for (let offset = 1; offset < alive.length; offset++) {
      const candidate = alive[(index + offset) % alive.length];
      if (!candidate.equipment.some((card) => typeOf(card) === equipmentType)) return candidate;
    }
    return void 0;
  }
  distance(state, actor, target) {
    const alive = state.players.filter((player) => player.alive);
    const a = alive.indexOf(actor), b = alive.indexOf(target);
    const base = Math.min((b - a + alive.length) % alive.length, (a - b + alive.length) % alive.length);
    const modifier = (actor.characterId === "rose_doolan" ? -1 : 0) + (target.characterId === "paul_regret" ? 1 : 0) + (actor.equipment.some((card) => typeOf(card) === "appaloosa") ? -1 : 0) + (target.equipment.some((card) => typeOf(card) === "mustang") ? 1 : 0);
    return Math.max(1, base + modifier);
  }
  player(state, id) {
    const player = state.players.find((item) => item.id === id);
    if (!player) throw Error("Kh\xF4ng t\xECm th\u1EA5y ng\u01B0\u1EDDi ch\u01A1i.");
    return player;
  }
  requireTurn(state, userId, phase) {
    if (state.status !== "playing" || state.phase !== phase || state.currentTurnPlayerId !== userId) throw Error("Kh\xF4ng ph\u1EA3i l\u01B0\u1EE3t h\u1EE3p l\u1EC7.");
  }
  async load(required = true) {
    const state = this.stateData ?? await this.ctx.storage.get("match");
    if (!state && required) throw Error("Kh\xF4ng t\xECm th\u1EA5y ph\xF2ng.");
    if (state) this.stateData = state;
    return state;
  }
  async save(state) {
    state.publicLog = state.publicLog.slice(-100);
    this.stateData = state;
    await this.ctx.storage.put("match", state);
    const summary = {
      id: state.id,
      code: state.code,
      hostId: state.hostId,
      maxPlayers: state.maxPlayers,
      turnDurationSeconds: state.turnDurationSeconds,
      status: state.status,
      phase: state.phase,
      totalCount: state.players.length,
      botCount: state.players.filter((player) => player.bot).length,
      updatedAt: Date.now()
    };
    await this.env.DIRECTORY.getByName("lobby").fetch(
      new Request("https://directory/upsert", {
        method: "POST",
        body: JSON.stringify(summary)
      })
    );
    this.broadcast(state);
  }
  websocket(request, user) {
    if (request.headers.get("Upgrade") !== "websocket") return fail("Expected WebSocket", 426);
    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    server.serializeAttachment(user);
    this.ctx.acceptWebSocket(server);
    void this.load().then((state) => server.send(JSON.stringify({ type: "state", room: this.snapshot(state, user.id) })));
    return new Response(null, { status: 101, webSocket: client });
  }
  broadcast(state) {
    for (const ws of this.ctx.getWebSockets()) {
      const user = ws.deserializeAttachment();
      if (user) ws.send(JSON.stringify({ type: "state", room: this.snapshot(state, user.id) }));
    }
  }
  setupDeckFor(cards, userId) {
    return cards?.map((card) => ({
      id: card.id,
      value: card.pickedBy === userId ? card.value : "",
      pickedBy: card.pickedBy
    }));
  }
  snapshot(state, userId) {
    const me = state.players.find((player) => player.id === userId);
    const sheriffPlayerId = state.players.find((player) => player.role === "sheriff")?.id;
    return {
      ...state,
      deck: void 0,
      sheriffPlayerId,
      roleDeck: this.setupDeckFor(state.roleDeck, userId),
      characterDeck: this.setupDeckFor(state.characterDeck, userId),
      players: state.players.map(({ hand, role, characterOptions, characterChosen, ...player }) => ({
        ...player,
        revealedRole: role === "sheriff" && state.phase !== "role_selection" || !player.alive ? role : void 0,
        role: player.id === userId ? role : void 0,
        hand: player.id === userId ? hand : void 0,
        characterOptions: player.id === userId ? characterOptions : void 0,
        characterChosen: player.id === userId ? characterChosen : void 0
      })),
      hand: me?.hand ?? []
    };
  }
};
async function sign(user, secret) {
  const payload = textToBase64(JSON.stringify({ ...user, exp: Date.now() + 1e3 * 60 * 60 * 24 * 30 }));
  return `${payload}.${await signatureFor(payload, secret)}`;
}
__name(sign, "sign");
async function signatureFor(payload, secret) {
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(payload));
  return toBase64(new Uint8Array(signature));
}
__name(signatureFor, "signatureFor");
async function authenticate(request, secret) {
  const token = request.headers.get("authorization")?.replace("Bearer ", "") ?? new URL(request.url).searchParams.get("token");
  if (!token) return null;
  const [payload, signature] = token.split(".");
  if (!payload || !signature || signature !== await signatureFor(payload, secret)) return null;
  const user = JSON.parse(base64ToText(payload));
  return user.exp > Date.now() ? { id: user.id, name: user.name, avatarId: user.avatarId ?? "quick_jack" } : null;
}
__name(authenticate, "authenticate");
function toBase64(bytes) {
  let result = "";
  for (const byte of bytes) result += String.fromCharCode(byte);
  return btoa(result);
}
__name(toBase64, "toBase64");
function textToBase64(value) {
  return toBase64(new TextEncoder().encode(value));
}
__name(textToBase64, "textToBase64");
function base64ToText(value) {
  return new TextDecoder().decode(Uint8Array.from(atob(value), (char) => char.charCodeAt(0)));
}
__name(base64ToText, "base64ToText");
export {
  BangBangDirectory,
  BangBangMatch,
  index_default as default
};
//# sourceMappingURL=index.js.map

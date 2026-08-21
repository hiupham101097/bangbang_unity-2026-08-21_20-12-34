using System;
using System.Collections.Generic;
using UnityEngine;

namespace BangBang.Core.Data
{
    public static class CardCatalogDatabase
    {
        private static readonly Dictionary<string, CardInfo> Cards = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CharacterInfo> Characters = new Dictionary<string, CharacterInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<RoleType, RoleInfo> Roles = new Dictionary<RoleType, RoleInfo>();

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        static CardCatalogDatabase()
        {
            InitializeCards();
            InitializeCharacters();
            InitializeRoles();
        }

        public static string GetTypeOf(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return "";
            var parts = cardId.Split('_');
            if (parts.Length > 2 && parts[0].ToLower() == "gun" && parts[1].ToLower() == "range")
            {
                return parts[0] + "_" + parts[1] + "_" + parts[2];
            }
            return parts[0];
        }

        private static void InitializeCards()
        {
            RegisterCard(new CardInfo("bang", "BANG!", "Tấn công 1 người chơi trong tầm ngắm, gây 1 sát thương.", CardType.BrownAction, "Cards/bang", requiresTarget: true));
            RegisterCard(new CardInfo("dodge", "NÉ!", "Tránh 1 phát bắn BANG! hoặc đòn tấn công yêu cầu NÉ.", CardType.BrownAction, "Cards/ne"));
            RegisterCard(new CardInfo("beer", "BIA", "Hồi 1 Máu cho người sử dụng (vô hiệu khi chỉ còn 2 người).", CardType.BrownAction, "Cards/beer"));
            RegisterCard(new CardInfo("saloon", "SALOON", "Mở tiệc quán rượu! Tất cả người chơi còn sống hồi 1 Máu.", CardType.BrownAction, "Cards/saloon"));
            RegisterCard(new CardInfo("gatling", "SÚNG MÁY GATLING", "Xả đạn liên hồi! Tất cả người chơi khác phải dùng NÉ hoặc mất 1 Máu.", CardType.BrownAction, "Cards/gatling"));
            RegisterCard(new CardInfo("indiani", "THỔ DÂN DA ĐỎ", "Bầy thổ dân ập tới! Tất cả người chơi khác phải nộp thẻ BANG! hoặc mất 1 Máu.", CardType.BrownAction, "Cards/indiani"));
            RegisterCard(new CardInfo("panico", "HOẢNG LOẠN (PANICO)", "Cướp 1 lá bài trên tay hoặc trang bị của người chơi cách bạn cự ly 1.", CardType.BrownAction, "Cards/panico", requiresTarget: true) { targetRangeOne = true });
            RegisterCard(new CardInfo("cat_balou", "CAT BALOU", "Bắn hạ hoặc hủy 1 lá bài bất kỳ của đối thủ ở mọi cự ly.", CardType.BrownAction, "Cards/cat_balou", requiresTarget: true) { targetAnyRange = true });
            RegisterCard(new CardInfo("dilizenza", "XE THỒ (DILIZENZA)", "Đoàn xe tiếp tế tới! Rút thêm 2 lá bài từ bộ bài chung.", CardType.BrownAction, "Cards/dilizenza"));
            RegisterCard(new CardInfo("wells_fargo", "WELLS FARGO", "Chuyến xe bọc thép! Rút ngay 3 lá bài từ bộ bài chung.", CardType.BrownAction, "Cards/wells_fargo"));
            RegisterCard(new CardInfo("general_store", "TIỆM TẠP HÓA", "Lật số lá bài bằng số người sống; mọi người lần lượt chọn 1 lá.", CardType.BrownAction, "Cards/general_store"));
            RegisterCard(new CardInfo("duello", "ĐẤU SÚNG (DUELLO)", "Thách đấu 1 người bất kỳ! Luân phiên đánh BANG, ai hết BANG mất 1 Máu.", CardType.BrownAction, "Cards/duello", requiresTarget: true) { targetAnyRange = true });

            // Equipment
            RegisterCard(new CardInfo("mustang", "NGỰA MUSTANG", "Tăng khoảng cách của bạn thêm +1 đối với tất cả người chơi khác.", CardType.BlueEquipment, "Cards/mustang", rangeMod: 1));
            RegisterCard(new CardInfo("appaloosa", "NGỰA APPALOOSA", "Nhìn thấy tất cả người chơi khác gần hơn -1 khoảng cách.", CardType.BlueEquipment, "Cards/appaloosa", rangeMod: -1));
            RegisterCard(new CardInfo("barrel", "THÙNG GỖ", "Khi bị bắn BANG!, lật bài Cơ (Hearts) để tự động Né thành công!", CardType.BlueEquipment, "Cards/barrel"));
            RegisterCard(new CardInfo("jail", "NHÀ TÙ", "Bỏ tù 1 đối thủ (trừ Cảnh Trưởng). Đến lượt phải bốc bài Cơ để vượt ngục.", CardType.BlueEquipment, "Cards/jail", requiresTarget: true) { targetAnyRange = true });
            RegisterCard(new CardInfo("dynamite", "THUỐC NỔ", "Đặt trước mặt bạn. Mỗi đầu lượt lật bài: Nếu là Bích (2-9), NỔ 3 MÁU! Nếu không, chuyển sang người kế tiếp.", CardType.BlueEquipment, "Cards/dynamite"));

            // Guns
            RegisterCard(new CardInfo("volcanic", "SÚNG VOLCANIC", "Vũ khí tầm 1. Cho phép bắn không giới hạn số lượng BANG! trong 1 lượt.", CardType.BlueEquipment, "Cards/volcanic", rangeMod: 1));
            RegisterCard(new CardInfo("gun_range_2", "SCHOFIELD", "Súng lục tầm 2.", CardType.BlueEquipment, "Cards/gun_range_2", rangeMod: 2));
            RegisterCard(new CardInfo("gun_range_3", "REMINGTON", "Súng trường tầm 3.", CardType.BlueEquipment, "Cards/gun_range_3", rangeMod: 3));
            RegisterCard(new CardInfo("gun_range_4", "REV. CARBINE", "Súng Carbin tầm 4.", CardType.BlueEquipment, "Cards/gun_range_4", rangeMod: 4));
            RegisterCard(new CardInfo("gun_range_5", "WINCHESTER", "Súng bắn tỉa Winchester tầm 5.", CardType.BlueEquipment, "Cards/gun_range_5", rangeMod: 5));
        }

        private static void InitializeCharacters()
        {
            RegisterChar(new CharacterInfo("bart_cassidy", "Bart Cassidy", "Bản Lĩnh", "Mỗi khi mất 1 Máu, được rút ngay 1 lá bài từ bộ bài chung.", 4, "Characters/bart_cassidy"));
            RegisterChar(new CharacterInfo("black_jack", "Black Jack", "May Mắn", "Trong lượt rút bài, lật lá thứ 2: Nếu là Cơ hoặc Rô thì được rút thêm lá thứ 3.", 4, "Characters/black_jack"));
            RegisterChar(new CharacterInfo("calamity_janet", "Calamity Janet", "Linh Hoạt", "Có thể dùng thẻ BANG! như thẻ NÉ và ngược lại.", 4, "Characters/calamity_janet"));
            RegisterChar(new CharacterInfo("el_gringo", "El Gringo", "Báo Thù", "Mỗi khi bị người chơi khác làm mất Máu, rút 1 lá ngẫu nhiên trên tay kẻ đó.", 3, "Characters/el_gringo"));
            RegisterChar(new CharacterInfo("jesse_jones", "Jesse Jones", "Trộm Cắp", "Đầu lượt có thể chọn rút lá đầu tiên từ tay người khác thay vì từ bộ bài.", 4, "Characters/jesse_jones"));
            RegisterChar(new CharacterInfo("jourdonnais", "Jourdonnais", "Lá Chắn Sống", "Mặc định có sẵn Thùng Gỗ: khi bị bắn có thể lật bài Cơ để Né.", 4, "Characters/jourdonnais"));
            RegisterChar(new CharacterInfo("kit_carlson", "Kit Carlson", "Quan Sát", "Đầu lượt nhìn 3 lá trên cùng bộ bài, chọn lấy 2 lá và đặt lại 1 lá lên đầu.", 4, "Characters/kit_carlson"));
            RegisterChar(new CharacterInfo("lucky_duke", "Lucky Duke", "Vận Đỏ", "Mỗi khi phải lật bài thử vận may (Dynamite, Thùng gỗ, Tù), được lật 2 lá chọn 1.", 4, "Characters/lucky_duke"));
            RegisterChar(new CharacterInfo("paul_regret", "Paul Regret", "Thận Trọng", "Mặc định được tính như có Ngựa Mustang: Tất cả mọi người nhìn thấy ở cự ly +1.", 3, "Characters/paul_regret"));
            RegisterChar(new CharacterInfo("pedro_ramirez", "Pedro Ramirez", "Mót Bài", "Đầu lượt có thể chọn rút lá đầu tiên từ chồng bài bỏ (Discard Pile).", 4, "Characters/pedro_ramirez"));
            RegisterChar(new CharacterInfo("rose_oolan", "Rose Doolan", "Mắt Đại Bàng", "Mặc định được tính như có Ngựa Appaloosa: Nhìn thấy mọi người gần hơn -1.", 4, "Characters/rose_oolan"));
            RegisterChar(new CharacterInfo("sid_ketchum", "Sid Ketchum", "Ăn Tạp", "Bất kỳ lúc nào có thể bỏ 2 lá bài trên tay để hồi 1 Máu.", 4, "Characters/sid_ketchum"));
            RegisterChar(new CharacterInfo("slab_the_killer", "Slab the Killer", "Sát Thủ Vô Tình", "Phát bắn BANG! của Slab yêu cầu đối thủ phải dùng 2 lá NÉ mới thoát.", 4, "Characters/slab_the_killer"));
            RegisterChar(new CharacterInfo("suzy_lafayette", "Suzy Lafayette", "Rảnh Tay", "Ngay khi trên tay không còn lá bài nào, được rút ngay 1 lá bài mới.", 4, "Characters/suzy_lafayette"));
            RegisterChar(new CharacterInfo("vulture_sam", "Vulture Sam", "Kền Kền Ăn Xác", "Khi có bất kỳ người chơi nào bị hạ gục, Sam gom toàn bộ bài trên tay & trang bị của họ.", 4, "Characters/vulture_sam"));
            RegisterChar(new CharacterInfo("willy_the_kid", "Willy the Kid", "Liên Thanh", "Có thể đánh số lượng lá BANG! không giới hạn trong 1 lượt.", 4, "Characters/willy_the_kid"));
        }

        private static void InitializeRoles()
        {
            Roles[RoleType.Sheriff] = new RoleInfo(RoleType.Sheriff, "Cảnh Trưởng", "Tiêu diệt toàn bộ Cướp và Kẻ Phản Bội để lập lại hòa bình viễn tây.", "role_cards/sheriff_card", isRevealed: true);
            Roles[RoleType.Deputy] = new RoleInfo(RoleType.Deputy, "Phó Cảnh Trưởng", "Bảo vệ Cảnh Trưởng bằng mọi giá và tiêu diệt bọn Cướp.", "role_cards/deputy_card");
            Roles[RoleType.Outlaw] = new RoleInfo(RoleType.Outlaw, "Băng Cướp (Raider)", "Tiêu diệt Cảnh Trưởng để thống trị thị trấn!", "role_cards/raider_card");
            Roles[RoleType.Renegade] = new RoleInfo(RoleType.Renegade, "Kẻ Phản Bội (Traitor)", "Sống sót đến cuối cùng và hạ gục Cảnh Trưởng trong trận quyết chiến tay đôi!", "role_cards/traitor_card");
        }

        private static void RegisterCard(CardInfo card) => Cards[card.id] = card;
        private static void RegisterChar(CharacterInfo character) => Characters[character.id] = character;

        public static List<CardInfo> GetAllCards() => new List<CardInfo>(Cards.Values);
        public static List<CharacterInfo> GetAllCharacters() => new List<CharacterInfo>(Characters.Values);

        public static CardInfo GetCardInfo(string cardId)
        {
            var type = GetTypeOf(cardId);
            if (Cards.TryGetValue(type, out var info)) return info;
            return new CardInfo(type, type.ToUpper(), "Thẻ bài viễn tây.", CardType.BrownAction, "Cards/bang");
        }

        public static CharacterInfo GetCharacterInfo(string charId)
        {
            if (!string.IsNullOrEmpty(charId) && Characters.TryGetValue(charId, out var info)) return info;
            return new CharacterInfo("willy_the_kid", "Willy the Kid", "Liên Thanh", "Bắn Bang không giới hạn.", 4, "Characters/willy_the_kid");
        }

        public static RoleInfo GetRoleInfo(RoleType role)
        {
            if (Roles.TryGetValue(role, out var info)) return info;
            return Roles[RoleType.Outlaw];
        }

        public static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (SpriteCache.TryGetValue(resourcePath, out var cached) && cached != null) return cached;

            // Remove any trailing .png extension for Resources.Load
            string cleanPath = resourcePath.Replace(".png", "").Replace(".PNG", "");

            // Try exact clean path
            var sprite = Resources.Load<Sprite>(cleanPath);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(cleanPath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            // Try alternate case (e.g. cards vs Cards, characters vs Characters)
            if (sprite == null)
            {
                string altPath = cleanPath;
                if (cleanPath.StartsWith("cards/", StringComparison.OrdinalIgnoreCase)) altPath = "Cards/" + cleanPath.Substring(6);
                else if (cleanPath.StartsWith("characters/", StringComparison.OrdinalIgnoreCase)) altPath = "Characters/" + cleanPath.Substring(11);
                else if (cleanPath.StartsWith("roles/", StringComparison.OrdinalIgnoreCase)) altPath = "Roles/" + cleanPath.Substring(6);

                sprite = Resources.Load<Sprite>(altPath);
                if (sprite == null)
                {
                    var tex = Resources.Load<Texture2D>(altPath);
                    if (tex != null)
                    {
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }

            if (sprite != null) SpriteCache[resourcePath] = sprite;
            return sprite;
        }

        public static AudioClip LoadAudio(string audioName)
        {
            string clean = audioName.Replace(".wav", "").Replace(".mp3", "");
            return Resources.Load<AudioClip>(clean);
        }
    }
}

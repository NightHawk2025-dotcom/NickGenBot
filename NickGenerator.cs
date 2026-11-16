using System.Text;
using System.Text.RegularExpressions;

namespace NickGenBot;

public enum NickStyle { Cool, Cute, Gamer, Dark, Anime, Funny }

public static partial class NickGenerator
{
    private sealed record Pack(string[] Adj, string[] Noun, string[] Ua, string[] Emoji, string[] Decor);

    private static readonly Random Rng = Random.Shared;

    private static readonly Dictionary<NickStyle, Pack> Packs = new()
    {
        [NickStyle.Cool] = new(
            ["Silent", "Frozen", "Neon", "Iron", "Swift", "Royal", "Prime", "Rogue", "Atomic", "Lunar", "Solar", "Ultra"],
            ["Wolf", "Falcon", "Phantom", "Viper", "Titan", "Nova", "Reaper", "Hunter", "Ghost", "Storm", "Blade", "Raven"],
            ["Вовк", "Сокіл", "Грім", "Тінь", "Буревій", "Орел", "Хижак", "Мисливець"],
            ["🐺", "⚡", "🔥", "🦅", "🗡️", "🛡️"],
            ["★ {0} ★", "⚡{0}⚡", "『{0}』", "♛ {0}", "{0} ツ"]),

        [NickStyle.Cute] = new(
            ["Fluffy", "Tiny", "Sweet", "Cosmic", "Sleepy", "Pastel", "Cozy", "Honey", "Candy", "Soft", "Happy", "Dreamy"],
            ["Bunny", "Kitten", "Panda", "Cookie", "Muffin", "Bubble", "Peach", "Mochi", "Cupcake", "Star", "Cloud", "Teddy"],
            ["Зайчик", "Котик", "Пончик", "Цукерка", "Зірочка", "Хмаринка", "Мурчик", "Лапка"],
            ["🐰", "🍑", "🍪", "☁️", "🌸", "🧸"],
            ["✧{0}✧", "♡ {0} ♡", "˚₊‧ {0} ‧₊˚", "🌸{0}🌸", "{0} ♡"]),

        [NickStyle.Gamer] = new(
            ["Pro", "Epic", "Turbo", "Hyper", "Toxic", "Mega", "Sweaty", "Clutch", "Ranked", "Elite", "Crit", "Laggy"],
            ["Sniper", "Noob", "Slayer", "Rusher", "Camper", "Boss", "Legend", "Headshot", "Tank", "Raider", "Pixel", "Respawn"],
            ["Снайпер", "Рашер", "Легенда", "Бустер", "Тіммейт", "Нуб", "Фрагер", "Танк"],
            ["🎮", "🔫", "👾", "🏆", "💀", "🕹️"],
            ["xX{0}Xx", "[PRO] {0}", "{0} GG", "»{0}«", "꧁{0}꧂"]),

        [NickStyle.Dark] = new(
            ["Dark", "Black", "Shadow", "Void", "Crimson", "Night", "Cursed", "Hollow", "Abyssal", "Grim", "Pale", "Silent"],
            ["Soul", "Raven", "Moon", "Crypt", "Echo", "Eclipse", "Specter", "Witch", "Demon", "Omen", "Thorn", "Wraith"],
            ["Тінь", "Морок", "Привид", "Пітьма", "Ворон", "Затемнення", "Прокляття", "Місяць"],
            ["🌑", "🦇", "🕯️", "💀", "🖤", "🥀"],
            ["†{0}†", "☾ {0} ☽", "⸸ {0} ⸸", "꧁{0}꧂", "{0} ✞"]),

        [NickStyle.Anime] = new(
            ["Sakura", "Moonlit", "Spirit", "Crimson", "Silver", "Azure", "Starlit", "Mystic", "Tsundere", "Chibi", "Shonen", "Isekai"],
            ["Kitsune", "Ronin", "Ninja", "Neko", "Samurai", "Sensei", "Waifu", "Oni", "Yokai", "Hikari", "Kage", "Tsuki"],
            ["Кіцуне", "Сакура", "Самурай", "Ніндзя", "Сенсей", "Нека", "Рьосі", "Хікарі"],
            ["🌸", "🦊", "⛩️", "🍥", "🗡️", "🎴"],
            ["「{0}」", "✿ {0} ✿", "{0} ☆彡", "☆ {0} ☆", "﹝{0}﹞"]),

        [NickStyle.Funny] = new(
            ["Sleepy", "Angry", "Confused", "Lazy", "Caffeinated", "Suspicious", "Chaotic", "Spicy", "Broke", "Offline", "Hungry", "Dizzy"],
            ["Potato", "Pickle", "Toaster", "Banana", "Goose", "Raccoon", "Sausage", "Noodle", "Borsch", "Waffle", "Pigeon", "Cabbage"],
            ["Картопля", "Борщ", "Вареник", "Гусак", "Пампух", "Сало", "Галушка", "Огірок"],
            ["🥔", "🤡", "🍌", "🦆", "🥒", "🦝"],
            ["~{0}~", "{0} 🤪", "😂 {0}", "{0} (ні)", "¯\\_{0}_/¯"]),
    };

    private static readonly string[] UsernamePrefixes = ["the_", "its_", "real_", "mr_", "just_"];
    private static readonly string[] UsernameSuffixes = ["_official", "_x", "_ua", "_dev", "_real", "hub", "zone", "_tg"];

    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d", ['е'] = "e",
        ['є'] = "ye", ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i", ['ї'] = "yi", ['й'] = "y",
        ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
        ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "shch", ['ь'] = "", ['ю'] = "yu", ['я'] = "ya", ['\''] = "", ['ʼ'] = "",
        ['ы'] = "y", ['э'] = "e", ['ё'] = "yo", ['ъ'] = "",
    };

    [GeneratedRegex("^[a-z][a-z0-9_]{3,30}[a-z0-9]$")]
    private static partial Regex UsernameRegex();

    /// <summary>Правила Telegram: 5–32 символи, a-z 0-9 _, починається з літери, не закінчується на _, без "__".</summary>
    public static bool IsValidUsername(string name) =>
        UsernameRegex().IsMatch(name) && !name.Contains("__");

    private static T Pick<T>(IReadOnlyList<T> items) => items[Rng.Next(items.Count)];

    private static NickStyle RandomStyle() => Pick(Enum.GetValues<NickStyle>());

    // ───────────── Генерація з нуля ─────────────

    public static IReadOnlyList<string> Nicknames(NickStyle? style, int count = 8)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var guard = 0; result.Count < count && guard < 300; guard++)
        {
            var p = Packs[style ?? RandomStyle()];
            var nick = Rng.Next(6) switch
            {
                0 => $"{Pick(p.Adj)} {Pick(p.Noun)}",
                1 => $"{Pick(p.Adj)}{Pick(p.Noun)}",
                2 => $"{Pick(p.Adj)}_{Pick(p.Noun)}",
                3 => string.Format(Pick(p.Decor), $"{Pick(p.Adj)}{Pick(p.Noun)}"),
                4 => string.Format(Pick(p.Decor), Pick(p.Ua)),
                _ => $"{Pick(p.Ua)} {Pick(p.Emoji)}",
            };

            if (seen.Add(nick)) result.Add(nick);
        }

        return result;
    }

    public static IReadOnlyList<string> Usernames(NickStyle? style, int count = 8)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();

        for (var guard = 0; result.Count < count && guard < 300; guard++)
        {
            var p = Packs[style ?? RandomStyle()];
            var a = Pick(p.Adj).ToLowerInvariant();
            var n = Pick(p.Noun).ToLowerInvariant();

            var name = Rng.Next(7) switch
            {
                0 => $"{a}_{n}",
                1 => $"{a}{n}",
                2 => $"{n}_{a}",
                3 => $"{a}{n}{Rng.Next(10, 100)}",
                4 => $"{n}_{Rng.Next(1, 1000):D2}",
                5 => $"{Pick(UsernamePrefixes)}{a}{n}",
                _ => $"{a}_{n}_{Rng.Next(10, 100)}",
            };

            if (name.Length > 32) continue;
            if (IsValidUsername(name) && seen.Add(name)) result.Add(name);
        }

        return result;
    }

    // ───────────── Генерація з власного слова ─────────────

    public static IReadOnlyList<string> NicknamesFromWord(string word, int count = 8)
    {
        word = word.Trim();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s) && result.Count < count && seen.Add(s)) result.Add(s);
        }

        // Красивий юнікод-шрифт працює тільки для латиниці
        if (word.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or ' ' or '_' or (>= '0' and <= '9')))
        {
            Add(Fancy(word, 0x1D4D0, 0x1D4EA)); // bold script
            Add(Fancy(word, 0x1D56C, 0x1D586)); // bold fraktur
            Add(Fancy(word, 0x1D670, 0x1D68A)); // monospace
        }

        for (var guard = 0; result.Count < count && guard < 100; guard++)
        {
            var p = Packs[RandomStyle()];
            Add(Rng.Next(3) switch
            {
                0 => string.Format(Pick(p.Decor), word),
                1 => $"{word} {Pick(p.Emoji)}",
                _ => $"{Pick(p.Emoji)} {word}",
            });
        }

        return result;
    }

    public static IReadOnlyList<string> UsernamesFromWord(string word, int count = 8)
    {
        var baseName = Sanitize(Transliterate(word));
        if (baseName.Length < 2) return [];

        var result = new List<string>();
        var seen = new HashSet<string>();

        void Add(string s)
        {
            if (result.Count < count && s.Length <= 32 && IsValidUsername(s) && seen.Add(s)) result.Add(s);
        }

        Add(baseName);

        for (var guard = 0; result.Count < count && guard < 200; guard++)
        {
            var noun = Pick(Packs[RandomStyle()].Noun).ToLowerInvariant();
            Add(Rng.Next(6) switch
            {
                0 => baseName + Pick(UsernameSuffixes),
                1 => Pick(UsernamePrefixes) + baseName,
                2 => $"{baseName}{Rng.Next(10, 100)}",
                3 => $"{baseName}_{noun}",
                4 => $"{noun}_{baseName}",
                _ => $"{baseName}_{Rng.Next(1, 1000):D2}",
            });
        }

        return result;
    }

    // ───────────── Унікальні (платні) ─────────────

    private static readonly string[] UVowels = ["a", "e", "i", "o", "u"];
    private static readonly string[] UConsonants = ["b", "d", "f", "g", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z", "j", "h"];
    private static readonly string[] UClusters = ["kr", "tr", "dr", "br", "gr", "pr", "fr", "vr", "st", "sk", "sl", "pl", "bl", "gl", "kl", "fl"];
    private static readonly string[] UMedials = ["rk", "lt", "nd", "st", "rt", "nk", "lm", "rs", "sk", "ld", "rn", "lk"];
    // C — приголосна, K — кластер, M — середина з двох приголосних, V — голосна
    private static readonly string[] UPatterns = ["CVCVC", "CVCVCV", "KVCV", "KVCVC", "KVCVCV", "CVMV", "CVMVC", "VCVCV"];
    private static readonly string[] UBanned = ["fuk", "sex", "dik", "pizd", "huj", "hui", "suk", "nig", "nazi", "rape"];

    /// <summary>Короткі (5–7 літер) вимовні імена без цифр і підкреслень. Доступність тут не перевіряється.</summary>
    public static IReadOnlyList<string> UniqueCandidates(int count)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();

        for (var guard = 0; result.Count < count && guard < count * 20; guard++)
        {
            var sb = new StringBuilder();
            foreach (var ch in Pick(UPatterns))
            {
                sb.Append(ch switch
                {
                    'C' => Pick(UConsonants),
                    'K' => Pick(UClusters),
                    'M' => Pick(UMedials),
                    _ => Pick(UVowels),
                });
            }

            var name = sb.ToString();
            if (UBanned.Any(b => name.Contains(b))) continue;
            if (IsValidUsername(name) && seen.Add(name)) result.Add(name);
        }

        return result;
    }

    // ───────────── Допоміжне ─────────────

    private static string Transliterate(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(Translit.TryGetValue(ch, out var lat) ? lat : ch.ToString());
        return sb.ToString();
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant())
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') sb.Append(ch);
            else if (ch is ' ' or '-' or '.') sb.Append('_');
        }

        var cleaned = Regex.Replace(sb.ToString(), "_{2,}", "_").Trim('_');
        return cleaned.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').Trim('_');
    }

    private static string Fancy(string s, int upperStart, int lowerStart)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (c is >= 'A' and <= 'Z') sb.Append(char.ConvertFromUtf32(upperStart + (c - 'A')));
            else if (c is >= 'a' and <= 'z') sb.Append(char.ConvertFromUtf32(lowerStart + (c - 'a')));
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

using System.Net;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NickGenBot;

public static partial class Handlers
{
    private const string Welcome =
        "👋 Привіт! Я генерую <b>ніки</b> та <b>юзернейми</b>.\n\n" +
        "Обери, що треба, або просто <b>надішли мені слово</b> (наприклад, своє імʼя) — " +
        "зроблю з нього красиві варіанти.\n\n" +
        "💎 А ще є платні <b>унікальні юзернейми</b> — короткі й перевірені на вільність.";

    private const string Checking = "⏳ Перевіряю, які юзернейми вільні…";

    private const string Legend =
        "✅ вільний · ❌ зайнятий · ❔ не вдалося перевірити\n" +
        "<i>Перевірка орієнтовна: зарезервовані чи продані на Fragment імена можуть показуватись як вільні.</i>";

    private static readonly (NickStyle Style, string Label)[] Styles =
    [
        (NickStyle.Cool, "😎 Крутий"),
        (NickStyle.Cute, "🌸 Мілий"),
        (NickStyle.Gamer, "🎮 Геймерський"),
        (NickStyle.Dark, "🖤 Темний"),
        (NickStyle.Anime, "⛩️ Аніме"),
        (NickStyle.Funny, "🤡 Смішний"),
    ];

    // ───────────── Точки входу ─────────────

    public static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            if (update.PreCheckoutQuery is { } pcq)
                await OnPreCheckoutAsync(bot, pcq, ct);
            else if (update.CallbackQuery is { } cq)
                await OnCallbackAsync(bot, cq, ct);
            else if (update.Message is { SuccessfulPayment: { } payment } paidMsg)
                await OnPaymentAsync(bot, paidMsg, payment, ct);
            else if (update.Message is { Text: { } text } msg)
                await OnMessageAsync(bot, msg, text, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[handler error] {ex}");
        }
    }

    public static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        var text = ex is ApiRequestException api
            ? $"Telegram API [{api.ErrorCode}]: {api.Message}"
            : ex.ToString();
        Console.WriteLine($"[polling error] {text}");
        return Task.CompletedTask;
    }

    // ───────────── Повідомлення ─────────────

    private static async Task OnMessageAsync(ITelegramBotClient bot, Message msg, string text, CancellationToken ct)
    {
        text = text.Trim();
        var chatId = msg.Chat.Id;

        if (text.StartsWith('/'))
        {
            var cmd = text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
            switch (cmd)
            {
                case "/start":
                case "/help":
                    await SendAsync(bot, chatId, Welcome, MainMenu(), ct);
                    break;
                case "/nick":
                    await SendAsync(bot, chatId, BuildNickResult(null), ResultKeyboard('n', "r"), ct);
                    break;
                case "/username":
                    var placeholder = await SendAsync(bot, chatId, Checking, null, ct);
                    await EditAsync(bot, placeholder, await BuildUsernameResultAsync(bot, null, ct), ResultKeyboard('u', "r"), ct);
                    break;
                case "/shop":
                case "/balance":
                    var credits = Shop.Store.GetCredits(msg.From?.Id ?? chatId);
                    await SendAsync(bot, chatId, ShopText(credits), ShopKeyboard(credits), ct);
                    break;
                case "/paysupport":
                    await SendAsync(bot, chatId, PaySupportText(), null, ct);
                    break;
                default:
                    await SendAsync(bot, chatId, "Не знаю такої команди 🤷 Спробуй /start", null, ct);
                    break;
            }
            return;
        }

        if (text.Length > 32)
        {
            await SendAsync(bot, chatId, "Занадто довго 😅 Надішли слово до 32 символів.", null, ct);
            return;
        }

        var wait = await SendAsync(bot, chatId, Checking, null, ct);
        var nicks = NickGenerator.NicknamesFromWord(text);
        var checkedUsers = await GetChecker(bot).CheckManyAsync(NickGenerator.UsernamesFromWord(text, 10), ct);
        var users = checkedUsers.OrderBy(u => u.Status).Take(8).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"✨ Варіанти для «{WebUtility.HtmlEncode(text)}»");
        sb.AppendLine();
        sb.AppendLine("🎭 <b>Ніки</b>");
        foreach (var n in nicks) sb.AppendLine($"<code>{WebUtility.HtmlEncode(n)}</code>");
        sb.AppendLine();
        sb.AppendLine("🔗 <b>Юзернейми</b>");
        if (users.Count == 0)
            sb.AppendLine("З цього слова юзернейм не вийшов — потрібні латинські літери чи цифри.");
        foreach (var (name, status) in users) sb.AppendLine(UsernameLine(name, status));
        sb.AppendLine();
        sb.AppendLine("Тапни на варіант — скопіюється.");
        if (users.Count > 0) sb.Append(Legend);

        await EditAsync(bot, wait, sb.ToString(), MainMenu(), ct);
    }

    // ───────────── Кнопки ─────────────

    private static async Task OnCallbackAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);

        if (cq.Message is not { } msg) return;

        var parts = (cq.Data ?? "").Split(':');
        switch (parts[0])
        {
            case "p":
                await OnPremiumCallbackAsync(bot, cq.From.Id, msg, parts, ct);
                break;

            case "m":
                await EditAsync(bot, msg, Welcome, MainMenu(), ct);
                break;

            case "k" when parts.Length == 2:
                await EditAsync(bot, msg, "🎨 Обери стиль:", StylePicker(parts[1][0]), ct);
                break;

            case "g" when parts.Length == 3:
                var kind = parts[1][0];
                var style = ParseStyle(parts[2]);
                if (kind == 'u')
                {
                    // Прибираємо кнопки на час перевірки, щоб не натискали двічі
                    await EditAsync(bot, msg, Checking, null, ct);
                    await EditAsync(bot, msg, await BuildUsernameResultAsync(bot, style, ct), ResultKeyboard(kind, parts[2]), ct);
                }
                else
                {
                    await EditAsync(bot, msg, BuildNickResult(style), ResultKeyboard(kind, parts[2]), ct);
                }
                break;
        }
    }

    // ───────────── Побудова відповідей ─────────────

    private static string BuildNickResult(NickStyle? style)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🎭 <b>Ніки</b> · {StyleLabel(style)}");
        sb.AppendLine();
        foreach (var n in NickGenerator.Nicknames(style)) sb.AppendLine($"<code>{WebUtility.HtmlEncode(n)}</code>");
        sb.AppendLine();
        sb.Append("Тапни на нік — скопіюється.");
        return sb.ToString();
    }

    private static async Task<string> BuildUsernameResultAsync(ITelegramBotClient bot, NickStyle? style, CancellationToken ct)
    {
        // Беремо з запасом і показуємо лише вільні та неперевірені
        var candidates = await GetChecker(bot).CheckManyAsync(NickGenerator.Usernames(style, 16), ct);
        var shown = candidates
            .Where(c => c.Status != UsernameStatus.Taken)
            .OrderBy(c => c.Status)
            .Take(8)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"🔗 <b>Юзернейми</b> · {StyleLabel(style)}");
        sb.AppendLine();
        if (shown.Count == 0) sb.AppendLine("Усі варіанти виявились зайнятими 😅 Натисни «Ще».");
        foreach (var (name, status) in shown) sb.AppendLine(UsernameLine(name, status));
        sb.AppendLine();
        sb.AppendLine("Тапни на юзернейм — скопіюється.");
        sb.Append(Legend);
        return sb.ToString();
    }

    private static string UsernameLine(string name, UsernameStatus status)
    {
        var mark = status switch
        {
            UsernameStatus.Free => "✅",
            UsernameStatus.Taken => "❌",
            _ => "❔",
        };
        return $"{mark} <code>{WebUtility.HtmlEncode(name)}</code>";
    }

    private static UsernameChecker? _checker;
    private static UsernameChecker GetChecker(ITelegramBotClient bot) => _checker ??= new UsernameChecker(bot);

    private static InlineKeyboardMarkup MainMenu() => new(
    [
        [
            InlineKeyboardButton.WithCallbackData("🎭 Ніки", "k:n"),
            InlineKeyboardButton.WithCallbackData("🔗 Юзернейми", "k:u"),
        ],
        [InlineKeyboardButton.WithCallbackData("💎 Унікальні юзернейми (платно)", "p")],
    ]);

    private static InlineKeyboardMarkup StylePicker(char kind)
    {
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < Styles.Length; i += 2)
        {
            rows.Add(Styles.Skip(i).Take(2)
                .Select(s => InlineKeyboardButton.WithCallbackData(s.Label, $"g:{kind}:{s.Style}"))
                .ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("🎲 Будь-який", $"g:{kind}:r")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Меню", "m")]);
        return new InlineKeyboardMarkup(rows);
    }

    private static InlineKeyboardMarkup ResultKeyboard(char kind, string styleCode)
    {
        var other = kind == 'n' ? 'u' : 'n';
        var otherLabel = kind == 'n' ? "🔗 Юзернейми" : "🎭 Ніки";

        return new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData("🔄 Ще", $"g:{kind}:{styleCode}")],
            [
                InlineKeyboardButton.WithCallbackData("🎨 Стиль", $"k:{kind}"),
                InlineKeyboardButton.WithCallbackData(otherLabel, $"g:{other}:{styleCode}"),
            ],
            [InlineKeyboardButton.WithCallbackData("⬅️ Меню", "m")],
        ]);
    }

    private static NickStyle? ParseStyle(string code) =>
        Enum.TryParse<NickStyle>(code, out var s) ? s : null;

    private static string StyleLabel(NickStyle? style) =>
        style is null ? "🎲 Будь-який" : Styles.First(s => s.Style == style).Label;

    // ───────────── Відправка / редагування ─────────────

    private static Task<Message> SendAsync(ITelegramBotClient bot, long chatId, string text, InlineKeyboardMarkup? kb, CancellationToken ct) =>
        bot.SendMessage(chatId, text, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: ct);

    private static async Task EditAsync(ITelegramBotClient bot, Message msg, string text, InlineKeyboardMarkup? kb, CancellationToken ct)
    {
        try
        {
            await bot.EditMessageText(msg.Chat.Id, msg.MessageId, text, parseMode: ParseMode.Html, replyMarkup: kb, cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
        {
            // той самий текст — нічого страшного
        }
    }
}

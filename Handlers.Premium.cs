using System.Net;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace NickGenBot;

// Платна частина: унікальні юзернейми за Telegram Stars
public static partial class Handlers
{
    private const string StarsCurrency = "XTR";

    // ───────────── Тексти й клавіатури ─────────────

    private static string ShopText(int credits) =>
        "💎 <b>Унікальні юзернейми</b>\n\n" +
        $"Короткі (5–7 літер), вимовні, без цифр і підкреслень — і перевірені на вільність просто зараз. У наборі їх {Shop.SetSize}.\n\n" +
        $"Твій баланс: <b>{credits}</b> {SetWord(credits)}\n" +
        "Оплата — Telegram Stars ⭐";

    private static InlineKeyboardMarkup ShopKeyboard(int credits)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (credits > 0)
            rows.Add([InlineKeyboardButton.WithCallbackData($"💎 Отримати набір ({credits})", "p:go")]);

        foreach (var p in Shop.Packs)
            rows.Add([InlineKeyboardButton.WithCallbackData($"⭐ {p.Sets} {SetWord(p.Sets)} — {p.Stars}", $"p:buy:{p.Id}")]);

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Меню", "m")]);
        return new InlineKeyboardMarkup(rows);
    }

    private static string SetWord(int n)
    {
        var m10 = n % 10;
        var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "набір";
        if (m10 is >= 2 and <= 4 && m100 is not (>= 12 and <= 14)) return "набори";
        return "наборів";
    }

    private static string PaySupportText()
    {
        var contact = Environment.GetEnvironmentVariable("SUPPORT_CONTACT") ?? "власнику бота";
        return $"🛟 Проблема з оплатою чи набором? Напиши {WebUtility.HtmlEncode(contact)} і вкажи, що сталось. " +
               "Якщо набір не вдалося видати, ми його повертаємо на баланс.";
    }

    // ───────────── Кнопки магазину ─────────────

    private static async Task OnPremiumCallbackAsync(
        ITelegramBotClient bot, long userId, Message msg, string[] parts, CancellationToken ct)
    {
        var action = parts.Length > 1 ? parts[1] : "";

        switch (action)
        {
            case "buy" when parts.Length == 3:
                var pack = Shop.Find(parts[2]);
                if (pack is not null) await SendInvoice(bot, msg.Chat.Id, pack, ct);
                break;

            case "go":
                await GenerateUniqueAsync(bot, userId, msg.Chat.Id, ct);
                break;

            default:
                var credits = Shop.Store.GetCredits(userId);
                await EditAsync(bot, msg, ShopText(credits), ShopKeyboard(credits), ct);
                break;
        }
    }

    private static Task SendInvoice(ITelegramBotClient bot, long chatId, Pack pack, CancellationToken ct) =>
        bot.SendInvoice(
            chatId: chatId,
            title: pack.Title,
            description: pack.Description,
            payload: pack.Id,
            providerToken: "", // для Telegram Stars токен провайдера порожній
            currency: StarsCurrency,
            prices: new[] { new LabeledPrice(pack.Title, pack.Stars) },
            cancellationToken: ct);

    // ───────────── Оплата ─────────────

    private static async Task OnPreCheckoutAsync(ITelegramBotClient bot, PreCheckoutQuery q, CancellationToken ct)
    {
        var pack = Shop.Find(q.InvoicePayload);

        if (pack is null || q.Currency != StarsCurrency || q.TotalAmount != pack.Stars)
            await bot.AnswerPreCheckoutQuery(q.Id, errorMessage: "Цей пакет уже недоступний. Відкрий /shop ще раз.", cancellationToken: ct);
        else
            await bot.AnswerPreCheckoutQuery(q.Id, cancellationToken: ct);
    }

    private static async Task OnPaymentAsync(ITelegramBotClient bot, Message msg, SuccessfulPayment pay, CancellationToken ct)
    {
        var userId = msg.From?.Id ?? msg.Chat.Id;
        var pack = Shop.Find(pay.InvoicePayload);

        // Ідентифікатор платежу потрібен для підтримки та можливого повернення зірок
        Console.WriteLine($"[payment] user={userId} payload={pay.InvoicePayload} amount={pay.TotalAmount} {pay.Currency} charge={pay.TelegramPaymentChargeId}");

        if (pack is null || pay.Currency != StarsCurrency)
        {
            await SendAsync(bot, msg.Chat.Id, "Оплату отримано, але пакет не розпізнано. Скористайся /paysupport.", null, ct);
            return;
        }

        Shop.Store.AddPurchase(userId, pack, pay.TelegramPaymentChargeId);
        var credits = Shop.Store.GetCredits(userId);

        var kb = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData($"💎 Отримати набір ({credits})", "p:go")],
        ]);

        await SendAsync(bot, msg.Chat.Id,
            $"✅ Оплату отримано! Баланс: <b>{credits}</b> {SetWord(credits)}.", kb, ct);
    }

    // ───────────── Видача набору ─────────────

    private static async Task GenerateUniqueAsync(ITelegramBotClient bot, long userId, long chatId, CancellationToken ct)
    {
        if (!Shop.TryBeginSearch(userId)) return;

        try
        {
            if (!Shop.Store.TryConsume(userId))
            {
                await SendAsync(bot, chatId, "😕 У тебе немає наборів.\n\n" + ShopText(0), ShopKeyboard(0), ct);
                return;
            }

            var wait = await SendAsync(bot, chatId, "⏳ Шукаю унікальні вільні юзернейми… це може зайняти до пів хвилини.", null, ct);

            IReadOnlyList<string> found;
            try
            {
                found = await UniqueFinder.FindAsync(GetChecker(bot), Shop.SetSize, ct);
            }
            catch
            {
                Shop.Store.Refund(userId);
                throw;
            }

            // Не набрали повний набір — не списуємо
            var complete = found.Count >= Shop.SetSize;
            if (!complete) Shop.Store.Refund(userId);

            var credits = Shop.Store.GetCredits(userId);

            var sb = new StringBuilder();
            sb.AppendLine("💎 <b>Унікальні юзернейми</b>");
            sb.AppendLine();

            if (found.Count == 0)
            {
                sb.AppendLine("Цього разу вільних не знайшлось 😕 Набір повернуто на баланс — спробуй ще раз.");
            }
            else
            {
                foreach (var name in found) sb.AppendLine($"✅ <code>{WebUtility.HtmlEncode(name)}</code>");
                sb.AppendLine();
                sb.AppendLine("Тапни — скопіюється. Займай швидше: ім'я може зникнути будь-якої миті.");
                if (!complete) sb.AppendLine($"Знайшлось лише {found.Count}, тому набір не списано.");
                sb.AppendLine("<i>Перевірено щойно, але остаточно підтверджує лише Telegram.</i>");
            }

            sb.AppendLine();
            sb.Append($"Залишилось: <b>{credits}</b> {SetWord(credits)}");

            var kb = new InlineKeyboardMarkup(
            [
                [credits > 0
                    ? InlineKeyboardButton.WithCallbackData($"💎 Ще набір ({credits})", "p:go")
                    : InlineKeyboardButton.WithCallbackData("⭐ Докупити", "p")],
                [InlineKeyboardButton.WithCallbackData("⬅️ Меню", "m")],
            ]);

            await EditAsync(bot, wait, sb.ToString(), kb, ct);
        }
        finally
        {
            Shop.EndSearch(userId);
        }
    }
}

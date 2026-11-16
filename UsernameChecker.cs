using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace NickGenBot;

// Порядок важливий: за ним сортуємо (вільні першими)
public enum UsernameStatus { Free, Unknown, Taken }

/// <summary>
/// Перевірка, чи вільний юзернейм. Два кроки:
/// 1) Bot API getChat("@name") — якщо знайшовся, імʼя точно зайняте;
/// 2) сторінка t.me/name — якщо там немає профілю, імʼя вважаємо вільним.
/// Результат орієнтовний: Bot API не може перевірити зарезервовані/продані на Fragment імена.
/// </summary>
public sealed partial class UsernameChecker(ITelegramBotClient bot)
{
    private static readonly HttpClient Http = CreateHttp();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (UsernameStatus Status, DateTime At)> _cache = new();

    [GeneratedRegex("""<meta\s+property="og:title"\s+content="([^"]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitleRegex();

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; NickGenBot)");
        return http;
    }

    public async Task<IReadOnlyList<(string Name, UsernameStatus Status)>> CheckManyAsync(
        IEnumerable<string> names, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(6);

        var tasks = names.Select(async name =>
        {
            await gate.WaitAsync(ct);
            try { return (Name: name, Status: await CheckAsync(name, ct)); }
            finally { gate.Release(); }
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<UsernameStatus> CheckAsync(string name, CancellationToken ct)
    {
        if (_cache.TryGetValue(name, out var hit) && DateTime.UtcNow - hit.At < CacheTtl)
            return hit.Status;

        var status = await CheckCoreAsync(name, ct);

        if (status != UsernameStatus.Unknown)
            _cache[name] = (status, DateTime.UtcNow);

        return status;
    }

    private async Task<UsernameStatus> CheckCoreAsync(string name, CancellationToken ct)
    {
        // 1. Bot API: якщо Telegram віддав чат/користувача — імʼя точно зайняте
        try
        {
            await bot.GetChat($"@{name}", cancellationToken: ct);
            return UsernameStatus.Taken;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
        {
            // не знайдено — йдемо далі на крок 2
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // flood limit чи мережа — пробуємо крок 2
        }

        // 2. Сторінка t.me/name
        try
        {
            using var resp = await Http.GetAsync($"https://t.me/{name}", ct);
            if (!resp.IsSuccessStatusCode) return UsernameStatus.Unknown;

            var html = await resp.Content.ReadAsStringAsync(ct);
            var m = OgTitleRegex().Match(html);
            if (!m.Success) return UsernameStatus.Unknown;

            var title = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();

            // У неіснуючого імені заголовок виглядає як «Telegram: Contact @name»
            return title.StartsWith("Telegram: Contact @", StringComparison.OrdinalIgnoreCase)
                ? UsernameStatus.Free
                : UsernameStatus.Taken;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return UsernameStatus.Unknown;
        }
    }
}

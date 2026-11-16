using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NickGenBot;

/// <summary>Пакет наборів, який можна купити за Telegram Stars (валюта XTR).</summary>
public sealed record Pack(string Id, int Sets, int Stars, string Title)
{
    public string Description =>
        $"Кількість наборів: {Sets}. У кожному наборі {Shop.SetSize} коротких вільних юзернеймів.";
}

public static class Shop
{
    /// <summary>Скільки юзернеймів в одному наборі.</summary>
    public const int SetSize = 5;

    // ЦІНИ ТУТ. Stars = ціле число зірок. Title для інвойсу — до 32 символів.
    public static readonly Pack[] Packs =
    [
        new("p1", Sets: 1, Stars: 25, Title: "Унікальні юзернейми: 1 набір"),
        new("p5", Sets: 5, Stars: 100, Title: "Унікальні юзернейми: 5 наборів"),
    ];

    public static readonly SupabaseStore Store = SupabaseStore.FromEnvironment();

    public static Pack? Find(string id) => Packs.FirstOrDefault(p => p.Id == id);

    // Захист від подвійного натискання: один пошук на користувача одночасно
    private static readonly HashSet<long> Busy = [];

    public static bool TryBeginSearch(long userId)
    {
        lock (Busy) return Busy.Add(userId);
    }

    public static void EndSearch(long userId)
    {
        lock (Busy) Busy.Remove(userId);
    }
}

/// <summary>
/// Баланс наборів і журнал покупок у Supabase (Postgres). Уся логіка атомарна на боці БД
/// (RPC-функції з supabase.sql), тому подвійне списання/зарахування неможливе.
/// </summary>
public sealed class SupabaseStore
{
    private readonly HttpClient _http;

    public SupabaseStore(string url, string key)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(url.TrimEnd('/') + "/rest/v1/rpc/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        _http.DefaultRequestHeaders.Add("apikey", key);

        // Старі ключі — це JWT (eyJ...), їх треба слати ще й як Bearer. Нові sb_secret_... — лише apikey.
        if (key.StartsWith("eyJ", StringComparison.Ordinal))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public static SupabaseStore FromEnvironment() => new(
        Environment.GetEnvironmentVariable("SUPABASE_URL")!,
        Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")!);

    public Task<int> GetCreditsAsync(long userId, CancellationToken ct) =>
        RpcAsync<int>("get_credits", new { p_user = userId }, retry: true, ct);

    /// <summary>Зараховує покупку. false — якщо цей платіж уже зараховано (повторна доставка).</summary>
    public Task<bool> AddPurchaseAsync(long userId, Pack pack, string chargeId, CancellationToken ct) =>
        RpcAsync<bool>("add_purchase",
            new { p_user = userId, p_charge = chargeId, p_stars = pack.Stars, p_sets = pack.Sets },
            retry: true, ct); // безпечно повторювати: charge_id унікальний

    public Task<bool> TryConsumeAsync(long userId, CancellationToken ct) =>
        RpcAsync<bool>("try_consume", new { p_user = userId }, retry: false, ct);

    public Task<bool> RefundAsync(long userId, CancellationToken ct) =>
        RpcAsync<bool>("refund_set", new { p_user = userId }, retry: false, ct);

    private async Task<T> RpcAsync<T>(string fn, object args, bool retry, CancellationToken ct)
    {
        var attempts = retry ? 3 : 1;

        for (var i = 1; ; i++)
        {
            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(fn, content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"Supabase {fn}: {(int)resp.StatusCode} {body}");

                return JsonSerializer.Deserialize<T>(body)!;
            }
            catch (Exception) when (i < attempts && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * i), ct);
            }
        }
    }
}
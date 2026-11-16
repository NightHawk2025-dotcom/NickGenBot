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

    public static readonly PurchaseStore Store = new(
        Path.Combine(Environment.GetEnvironmentVariable("DATA_DIR") ?? "data", "credits.json"));

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

/// <summary>Баланс наборів і журнал покупок у JSON-файлі.</summary>
public sealed class PurchaseStore
{
    public sealed class Purchase
    {
        public long UserId { get; set; }
        public string ChargeId { get; set; } = "";
        public int Stars { get; set; }
        public int Sets { get; set; }
        public DateTime At { get; set; }
    }

    public sealed class Data
    {
        public Dictionary<long, int> Credits { get; set; } = new();
        public List<Purchase> Purchases { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly Data _data;

    public PurchaseStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _data = File.Exists(path)
            ? JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) ?? new Data()
            : new Data();
    }

    public int GetCredits(long userId)
    {
        lock (_gate) return _data.Credits.GetValueOrDefault(userId);
    }

    /// <summary>Зараховує покупку. false — якщо цей платіж уже зараховано (повторна доставка).</summary>
    public bool AddPurchase(long userId, Pack pack, string chargeId)
    {
        lock (_gate)
        {
            if (_data.Purchases.Any(p => p.ChargeId == chargeId)) return false;

            _data.Purchases.Add(new Purchase
            {
                UserId = userId,
                ChargeId = chargeId,
                Stars = pack.Stars,
                Sets = pack.Sets,
                At = DateTime.UtcNow,
            });
            _data.Credits[userId] = _data.Credits.GetValueOrDefault(userId) + pack.Sets;
            Save();
            return true;
        }
    }

    public bool TryConsume(long userId)
    {
        lock (_gate)
        {
            var have = _data.Credits.GetValueOrDefault(userId);
            if (have <= 0) return false;
            _data.Credits[userId] = have - 1;
            Save();
            return true;
        }
    }

    public void Refund(long userId)
    {
        lock (_gate)
        {
            _data.Credits[userId] = _data.Credits.GetValueOrDefault(userId) + 1;
            Save();
        }
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}

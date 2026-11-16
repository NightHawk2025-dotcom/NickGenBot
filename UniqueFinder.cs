namespace NickGenBot;

/// <summary>Шукає унікальні юзернейми, які зараз точно вільні.</summary>
public static class UniqueFinder
{
    private const int MaxRounds = 6;
    private const int BatchSize = 24;

    public static async Task<IReadOnlyList<string>> FindAsync(UsernameChecker checker, int count, CancellationToken ct)
    {
        var found = new List<string>();
        var tried = new HashSet<string>();

        for (var round = 0; round < MaxRounds && found.Count < count; round++)
        {
            var batch = NickGenerator.UniqueCandidates(BatchSize).Where(tried.Add).ToList();
            if (batch.Count == 0) continue;

            var results = await checker.CheckManyAsync(batch, ct);

            // Лише підтверджено вільні: «не вдалося перевірити» за платний товар не віддаємо
            found.AddRange(results.Where(r => r.Status == UsernameStatus.Free).Select(r => r.Name));
        }

        return found.Take(count).ToList();
    }
}

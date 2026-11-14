namespace ChestCounter;

/// <summary>
/// Pirate token boosts (local time): Saturday 20:00-22:00 x1.5 and Sunday 03:00-05:00 x2.
/// A chest opened during a boost counts as several chests in the pirate token estimate.
/// </summary>
public static class Boosts
{
    /// <summary>Saturdays whose boost ends later than 22:00 (end hour, 24 = midnight).</summary>
    private static readonly Dictionary<DateTime, int> ExtendedSaturdays = new()
    {
        [new DateTime(2026, 11, 14)] = 24,
    };

    /// <summary>Boost multiplier at a local time (1 = no boost).</summary>
    public static double Multiplier(DateTime local)
    {
        var hour = local.Hour;
        if (local.DayOfWeek == DayOfWeek.Saturday && hour >= 20 && hour < ExtendedSaturdays.GetValueOrDefault(local.Date, 22)) return 1.5;
        if (local.DayOfWeek == DayOfWeek.Sunday && hour >= 3 && hour < 5) return 2;
        return 1;
    }

    /// <summary>The boost a chest was opened in: its day and multiplier.</summary>
    public readonly record struct Window(DateTime Day, double Multiplier);

    /// <summary>Opened (not broken) chests of a match that fell in a boost, with their boost.</summary>
    public static IEnumerable<Window> BoostedChests(MatchRecord m)
    {
        var start = (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();
        foreach (var chest in m.Chests)
        {
            if (chest.Destroyed) continue;
            var at = start.AddSeconds(chest.TimeSeconds);
            var mult = Multiplier(at);
            if (mult > 1) yield return new Window(at.Date, mult);
        }
    }

    /// <summary>Opened chests counted with their boost (a chest at x1.5 counts 1.5).</summary>
    public static double EffectiveOpened(MatchRecord m) => m.ChestsOpened + BoostedChests(m).Sum(w => w.Multiplier - 1);

    public static string Label(double multiplier) => "x" + multiplier.ToString("0.#", L.Culture);
}

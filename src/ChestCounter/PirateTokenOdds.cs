namespace ChestCounter;

internal static class PirateTokenOdds
{
    public const int DropDenominator = 137_500;

    /// <summary>At least one success across independent openings at the selected rate (boosted chests count more than 1).</summary>
    public static double Chance(double openedChests) => 1 - Math.Pow(1 - 1.0 / DropDenominator, Math.Max(0, openedChests));

    public static string Format(double openedChests)
    {
        var chance = Chance(openedChests);
        if (chance >= 0.99995)
        {
            return ">" + 0.9999.ToString("P2", L.Culture);
        }
        return chance.ToString(chance is > 0 and < 0.0001 ? "P5" : "P2", L.Culture);
    }
}

namespace ChestCounter;

internal static class PirateTokenOdds
{
    public const int DropDenominator = 137_500;

    public static string Format(int openedChests)
    {
        // At least one success across independent openings at the selected rate.
        var chance = 1 - Math.Pow(1 - 1.0 / DropDenominator, openedChests);
        if (chance >= 0.99995)
        {
            return ">" + 0.9999.ToString("P2", L.Culture);
        }
        return chance.ToString(chance is > 0 and < 0.0001 ? "P5" : "P2", L.Culture);
    }
}

namespace ChestCounter;

/// <summary>Size and opacity choices of the overlay window (percent), offered in the settings.</summary>
public static class OverlayAppearance
{
    public static readonly int[] Sizes = { 70, 80, 90, 100, 115, 130, 150, 175 };

    /// <summary>Below 30 % the numbers can no longer be read.</summary>
    public static readonly int[] Opacities = { 100, 90, 80, 70, 60, 50, 40, 30 };

    public static int ClampSize(int percent) => Math.Clamp(percent, Sizes[0], Sizes[^1]);
    public static int ClampOpacity(int percent) => Math.Clamp(percent, Opacities[^1], 100);

    /// <summary>Next value of the list, back to the first one after the last (or for a value edited by hand).</summary>
    public static int Next(int[] values, int current) => values[(Array.IndexOf(values, current) + 1) % values.Length];
}

/// <summary>Builds the overlay from the saved matches and the recording status.</summary>
public static class OverlayModel
{
    /// <summary>Caption of the right counter between two matches (also used to keep the overlay width stable).</summary>
    public static string LastMatchCaption => L.T("dernière partie", "last match", "última partida", "letztes Match", "последний матч", "前回の試合");

    public static OverlayView Build(TrackerData d, bool inProgress)
    {
        var total = d.Matches.Sum(m => m.ChestsTotal);
        var session = d.Matches.Where(ReportWriter.IsThisSession).ToList();
        var sessionTotal = session.Sum(m => m.ChestsTotal);
        var rows = new List<OverlayRow>();

        // Session: chests, then small grey "n parties · moy. x" (finished matches of this session).
        string? sessionDetail = null;
        if (session.Count > 0)
        {
            var avg = (double)sessionTotal / session.Count;
            var avgText = Math.Round(avg, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
            sessionDetail = $"{L.Matches(session.Count)} · {L.T("moy.", "avg.", "media", "Ø", "сред.", "平均")} {avgText}";
        }
        rows.Add(new OverlayRow(L.T("Session", "Session", "Sesión", "Sitzung", "Сессия", "セッション"), L.Chests(sessionTotal), Detail: sessionDetail));

        var last = d.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        if (last != null)
        {
            rows.Add(new OverlayRow(L.T("Dernière", "Last", "Última", "Letztes", "Последний", "前回"), L.Chests(last.ChestsTotal)));
        }

        // Counters from enabled add-ons (permissions: stats.read + overlay.rows).
        rows.AddRange(AddonRuntime.CounterRows(d, session, last));

        var treasures = d.Matches.Sum(m => m.TreasureFinds.Count);
        rows.Add(new OverlayRow(L.T("Trésors", "Treasures", "Tesoros", "Schätze", "Сокровища", "宝"), $"★ {L.N(treasures)}", Treasure: true));
        var opened = d.Matches.Sum(m => m.ChestsOpened);
        rows.Add(new OverlayRow(L.T("Jeton pirate", "Pirate token", "Ficha pirata", "Piraten-Token", "Пиратский жетон", "海賊トークン"), PirateTokenOdds.Format(opened),
            Detail: L.T("· cumul estimé", "· estimated cumulative chance", "· probabilidad acumulada estimada", "· geschätzte Gesamtchance", "· оценочный суммарный шанс", "· 推定累積確率")));

        // Fortnite finalizes the replay at the end of the match.
        var view = inProgress
            ? new OverlayView(L.N(total), "…", L.T("en cours", "in progress", "en curso", "läuft", "идёт", "進行中"), true,
                L.T("● PARTIE EN COURS", "● MATCH IN PROGRESS", "● PARTIDA EN CURSO", "● MATCH LÄUFT", "● ИДЁТ МАТЧ", "● 試合中"), rows)
            : new OverlayView(L.N(total), last != null ? L.N(last.ChestsTotal) : null, LastMatchCaption, false, null, rows);

        // Script add-ons (permission overlay.script) may change, add or remove anything in the view.
        return AddonRuntime.ApplyScripts(view, d, session, last, inProgress);
    }
}

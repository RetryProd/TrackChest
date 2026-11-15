namespace ChestCounter;

/// <summary>Size and opacity choices of the overlay window (percent), offered in the settings.</summary>
public static class OverlayAppearance
{
    public const int MinSize = 50, MaxSize = 300;
    public static readonly int[] Sizes = { 50, 60, 70, 80, 90, 100, 115, 130, 150, 175, 200, 250, 300 };

    /// <summary>Below 30 % the numbers can no longer be read.</summary>
    public static readonly int[] Opacities = { 100, 90, 80, 70, 60, 50, 40, 30 };

    public static int ClampSize(int percent) => Math.Clamp(percent, MinSize, MaxSize);
    public static int ClampOpacity(int percent) => Math.Clamp(percent, Opacities[^1], 100);

    /// <summary>Next value of the list, back to the first one after the last (or for a value edited by hand).</summary>
    public static int Next(int[] values, int current) => values[(Array.IndexOf(values, current) + 1) % values.Length];
}

/// <summary>Builds the overlay from the saved matches and the recording status.</summary>
public static class OverlayModel
{
    /// <summary>Caption of the right counter between two matches (also used to keep the overlay width stable).</summary>
    public static string LastMatchCaption => L.T("dernière partie", "last match", "última partida", "letztes Match", "последний матч", "前回の試合", "ultima partita", "última partida", "ostatni mecz", "laatste potje", "son maç", "마지막 경기", "上一场比赛", "останній матч", "senaste matchen", "poslední zápas");

    /// <summary>"(12)": the player's own figure, drawn small next to a duo total.</summary>
    public static string Own(string value) => $"({value})";

    /// <summary>Joins small grey texts, skipping the missing ones.</summary>
    private static string? Join(params string?[] parts)
    {
        var kept = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return kept.Count == 0 ? null : string.Join(" ", kept);
    }

    /// <param name="partner">Duo partner figures: every count shows the sum of both players, the player's own
    /// figure in small "(…)". Null = alone. The partner's figures are never added to the history.</param>
    public static OverlayView Build(TrackerData d, bool inProgress, DuoStats? partner = null)
    {
        var duo = partner != null;
        var total = d.Matches.Sum(m => m.ChestsTotal);
        var session = d.Matches.Where(ReportWriter.IsThisSession).ToList();
        var sessionTotal = session.Sum(m => m.ChestsTotal);
        var rows = new List<OverlayRow>();

        // Session: chests, then small grey "n parties · moy. x" (finished matches of this session).
        // In duo: both sessions added up, matches and average over both players.
        var sessionMatches = session.Count + (partner?.SessionMatches ?? 0);
        var sessionSum = sessionTotal + (partner?.Session ?? 0);
        string? sessionDetail = null;
        if (sessionMatches > 0)
        {
            var avg = (double)sessionSum / sessionMatches;
            var avgText = Math.Round(avg, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
            sessionDetail = $"{L.Matches(sessionMatches)} · {L.T("moy.", "avg.", "media", "Ø", "сред.", "平均", "media", "méd.", "śr.", "gem.", "ort.", "평균", "平均", "сер.", "snitt", "prům.")} {avgText}";
        }
        rows.Add(new OverlayRow(L.T("Session", "Session", "Sesión", "Sitzung", "Сессия", "セッション", "Sessione", "Sessão", "Sesja", "Sessie", "Oturum", "세션", "本次", "Сесія", "Session", "Relace"), L.Chests(sessionSum),
            Detail: Join(duo ? Own(L.N(sessionTotal)) : null, sessionDetail), Icon: "Chest.png"));

        var last = d.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        var lastMine = last?.ChestsTotal ?? 0;
        var hasLast = last != null || partner?.HasLast == true;
        var lastSum = lastMine + (partner?.Last ?? 0);
        if (hasLast)
        {
            rows.Add(new OverlayRow(L.T("Dernière", "Last", "Última", "Letztes", "Последний", "前回", "Ultima", "Última", "Ostatni", "Laatste", "Son", "마지막", "上一场", "Останній", "Senaste", "Poslední"), L.Chests(lastSum),
                Detail: duo ? Own(L.N(lastMine)) : null, Icon: "Chest.png"));
        }

        // Software rows (Treasures and Pirate Token), with their icons to the left of the label.
        var treasures = d.Matches.Sum(m => m.TreasureFinds.Count);
        rows.Add(new OverlayRow(L.T("Trésors", "Treasures", "Tesoros", "Schätze", "Сокровища", "宝", "Tesori", "Tesouros", "Skarby", "Schatten", "Hazineler", "보물", "宝藏", "Скарби", "Skatter", "Poklady"), L.N(treasures + (partner?.Treasures ?? 0)),
            Treasure: true, Detail: duo ? Own(L.N(treasures)) : null, Icon: "Treasures.png"));
        var opened = d.Matches.Sum(m => m.ChestsOpened);
        var boost = Boosts.Current;
        var mine = opened * boost;
        // In duo: the chance that at least one of the two gets the token (both players' openings together).
        rows.Add(new OverlayRow(L.T("Jeton pirate", "Pirate token", "Ficha pirata", "Piraten-Token", "Пиратский жетон", "海賊トークン", "Gettone pirata", "Ficha pirata", "Piracki żeton", "Piratenmunt", "Korsan jetonu", "해적 토큰", "海盗代币", "Піратський жетон", "Piratpollett", "Pirátský žeton"), PirateTokenOdds.Format(mine + (partner?.Pirate ?? 0)),
            Color: boost > 1 ? Boosts.Color(boost) : null,
            Detail: Join(duo ? Own(PirateTokenOdds.Format(mine)) : null,
                boost > 1 ? $"· {Boosts.Word.ToLower(L.Culture)} {Boosts.Label(boost)}" : duo ? null : L.T("· cumul estimé", "· estimated cumulative chance", "· probabilidad acumulada estimada", "· geschätzte Gesamtchance", "· оценочный суммарный шанс", "· 推定累積確率", "· probabilità cumulativa stimata", "· chance acumulada estimada", "· szacowana łączna szansa", "· geschatte cumulatieve kans", "· tahmini kümülatif şans", "· 추정 누적 확률", "· 估计累计概率", "· оцінена сукупна ймовірність", "· uppskattad sammanlagd chans", "· odhadovaná kumulativní šance")),
            Icon: "Pirate_Token.png"));

        // Counters from enabled add-ons (permissions: stats.read + overlay.rows), visually separated from the software rows.
        rows.AddRange(AddonRuntime.CounterRows(d, session, last));

        // In duo the header says so, and the captions under the big numbers carry the player's own figures.
        var header = duo ? $"{NativeOverlay.HeaderText} · DUO" : null;
        var totalCaption = duo ? $"{NativeOverlay.CaptionTotal} {Own(L.N(total))}" : null;
        var totalText = L.N(total + (partner?.Total ?? 0));

        // Fortnite finalizes the replay at the end of the match.
        var view = inProgress
            ? new OverlayView(totalText, "…", L.T("en cours", "in progress", "en curso", "läuft", "идёт", "進行中", "in corso", "em andamento", "w toku", "bezig", "sürüyor", "진행 중", "进行中", "триває", "pågår", "probíhá"), true,
                L.T("● PARTIE EN COURS", "● MATCH IN PROGRESS", "● PARTIDA EN CURSO", "● MATCH LÄUFT", "● ИДЁТ МАТЧ", "● 試合中", "● PARTITA IN CORSO", "● PARTIDA EM ANDAMENTO", "● MECZ W TOKU", "● POTJE BEZIG", "● MAÇ SÜRÜYOR", "● 경기 진행 중", "● 比赛进行中", "● МАТЧ ТРИВАЄ", "● MATCH PÅGÅR", "● PROBÍHÁ ZÁPAS"), rows, header, totalCaption)
            : new OverlayView(totalText, hasLast ? L.N(lastSum) : null, duo && last != null ? $"{LastMatchCaption} {Own(L.N(lastMine))}" : LastMatchCaption, false, null, rows, header, totalCaption);

        // Script add-ons (permission overlay.script) may change, add or remove anything in the view.
        return AddonRuntime.ApplyScripts(view, d, session, last, inProgress);
    }
}

using System.Globalization;
using System.Net;
using System.Text;

namespace ChestCounter;

public static class Pretty
{
    public static string Playlist(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist))
        {
            return L.T("mode inconnu", "unknown mode", "modo desconocido");
        }

        var p = playlist.ToLowerInvariant();
        var parts = new List<string>();
        if (p.Contains("habanero") || p.Contains("ranked")) parts.Add(L.T("Classée", "Ranked", "Clasificatoria"));
        if (p.Contains("nobuild")) parts.Add(L.T("Zéro construction", "Zero Build", "Cero construcción"));
        if (p.Contains("solo")) parts.Add(L.T("Solo", "Solo", "Solitario"));
        else if (p.Contains("duo")) parts.Add(L.T("Duo", "Duos", "Dúos"));
        else if (p.Contains("trio")) parts.Add(L.T("Trio", "Trios", "Tríos"));
        else if (p.Contains("squad")) parts.Add(L.T("Section", "Squads", "Escuadrones"));
        return parts.Count > 0 ? string.Join(" · ", parts) : playlist.Replace("Playlist_", "");
    }

    public static string Placement(MatchRecord m)
    {
        if (m.Placement is not int place || place <= 0)
        {
            return L.T("place inconnue", "unknown placement", "puesto desconocido");
        }
        if (place == 1)
        {
            return L.T("Top 1 !", "Victory Royale!", "¡Victoria magistral!");
        }
        return m.TotalPlayers is int total && total > 0 ? $"{L.Ordinal(place)} / {total}" : L.Ordinal(place);
    }

    public static string Clock(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"à 7:16 · Coffre rare" (certain) or "vers 5:04 · Coffre rare (probable)".</summary>
    public static string Obtained(TreasureFind f)
    {
        if (f.ObtainedAtSeconds is not float t)
        {
            return L.T("moment et source inconnus", "time and source unknown", "momento y origen desconocidos");
        }
        var when = f.TimeExact ? $"{L.T("à", "at", "a los")} {Clock(t)}" : $"{L.T("vers", "around", "hacia los")} {Clock(t)}";
        return f.SourceCertain ? $"{when} · {f.SourceLabel}" : $"{when} · {f.SourceLabel} ({Likely})";
    }

    public static string Likely => L.T("probable", "likely", "probable");

    public static string ItemName(string item)
    {
        var name = item;
        foreach (var prefix in new[] { "WID_", "Athena", "AmmoData" })
        {
            name = name.Replace(prefix, "");
        }
        return name.Replace("_", " ").Trim();
    }
}

public static class ReportWriter
{
    public static readonly DateTime SessionStartUtc = DateTime.UtcNow;

    /// <summary>A match belongs to this session if it ended after the tool was started (backlog replays do not count).</summary>
    public static bool IsThisSession(MatchRecord m)
    {
        var start = m.MatchStartUtc ?? m.AnalyzedAtUtc;
        if (start.Kind == DateTimeKind.Unspecified)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }
        var end = start.ToUniversalTime().AddSeconds(m.DurationSeconds ?? 0);
        return end >= SessionStartUtc;
    }
    private static CultureInfo Fr => L.Culture;

    public static void WriteAll(Store store, Config config, TrackerData data)
    {
        store.RegisterPrizeIds(data.Matches.SelectMany(m => m.TreasureFinds)
            .Select(find => find.PrizeId).OfType<string>());
        var view = Profiles.View(data, config);
        WriteAtomic(store.ReportPath, Report(view, Profiles.List(data)));
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static DateTime When(MatchRecord m) => (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();

    private const string Style = """
        :root{--bg:#f6f5f2;--card:#fff;--ink:#1d1d1f;--muted:#6b6b70;--line:#e4e2dd;--accent:#b7791f;--accent-soft:#fbf1dc;--good:#2f7d4f;--star:#8a3ffc}
        @media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#121214;--card:#1b1b1f;--ink:#ededf0;--muted:#9a9aa2;--line:#2c2c32;--accent:#e9b04f;--accent-soft:#33280f;--good:#5cc28a;--star:#b48cff}}
        :root[data-theme="dark"]{--bg:#121214;--card:#1b1b1f;--ink:#ededf0;--muted:#9a9aa2;--line:#2c2c32;--accent:#e9b04f;--accent-soft:#33280f;--good:#5cc28a;--star:#b48cff}
        *{box-sizing:border-box}
        body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.5 "Segoe UI",system-ui,-apple-system,sans-serif}
        main{max-width:1040px;margin:0 auto;padding:28px 16px 48px}
        header{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:baseline;gap:8px;margin-bottom:22px}
        h1{font-size:26px;margin:0;letter-spacing:-.01em}
        h2{font-size:15px;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);margin:30px 0 10px}
        .muted{color:var(--muted)}
        .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px}
        .tile{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}
        .tile .v{font-size:30px;font-weight:700;font-variant-numeric:tabular-nums;line-height:1.2}
        .tile.main{background:var(--accent-soft);border-color:transparent}
        .tile.main .v{font-size:40px;color:var(--accent)}
        .tile .l{color:var(--muted);font-size:13px}
        .card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;overflow-x:auto}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{text-align:left;padding:8px 10px;border-bottom:1px solid var(--line);vertical-align:top}
        th{font-size:12px;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);font-weight:600}
        tr:last-child td{border-bottom:none}
        td.n{text-align:right;font-weight:600}
        td.nw{white-space:nowrap}
        .items{color:var(--muted);font-size:13px}
        .star{color:var(--star);font-weight:600}
        .flag{margin-top:16px;border-color:var(--accent);background:var(--accent-soft)}
        tr.unusual td{background:var(--accent-soft)}
        .card.chart{overflow:visible}
        .bars{display:flex;align-items:stretch;gap:4px;height:140px;padding-top:28px}
        .col{flex:1;min-width:6px;display:flex;align-items:flex-end;position:relative;cursor:default}
        .bar{width:100%;background:var(--accent);border-radius:3px 3px 0 0;transition:filter .12s}
        .bar.zero{background:var(--line);height:3px!important}
        .col:hover .bar{filter:brightness(1.25)}
        .col:hover .bar.zero{background:var(--muted)}
        .tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--ink);color:var(--bg);
             padding:3px 8px;border-radius:6px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:2}
        .col:nth-child(-n+3) .tip{left:0;transform:none}
        .col:nth-last-child(-n+3) .tip{left:auto;right:0;transform:none}
        .col:hover .tip{opacity:1}
        .axis{display:flex;justify-content:space-between;color:var(--muted);font-size:12px;margin-top:6px}
        footer{margin-top:28px;color:var(--muted);font-size:13px}
        .profiles{margin:-12px 0 20px;font-size:13px}
        """;

    private static string Report(TrackerData data, IReadOnlyList<Profile> profiles)
    {
        var matches = data.Matches.OrderByDescending(When).ToList();
        var totalChests = matches.Sum(m => m.ChestsTotal);
        var opened = matches.Sum(m => m.ChestsOpened);
        var avg = matches.Count > 0 ? (double)totalChests / matches.Count : 0;
        var best = matches.Count > 0 ? matches.Max(m => m.ChestsTotal) : 0;
        var treasures = matches.Sum(m => m.TreasureFinds.Count);
        var session = matches.Where(IsThisSession).Sum(m => m.ChestsTotal);
        var wanted = matches.Any(m => m.WantedMaps != null) ? L.N(matches.Sum(m => m.WantedMaps?.Count ?? 0)) : "—";
        var player = matches.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";

        var sb = new StringBuilder();
        var title = L.T("Compteur de coffres", "Chest Counter", "Contador de cofres");
        var updated = L.T(
            $"Mis à jour le {DateTime.Now.ToString("dd/MM/yyyy à HH:mm:ss", Fr)}",
            $"Updated {DateTime.Now.ToString("d MMM yyyy 'at' HH:mm:ss", Fr)}",
            $"Actualizado el {DateTime.Now.ToString("dd/MM/yyyy 'a las' HH:mm:ss", Fr)}");
        sb.Append($"""
            <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{title}</title><style>{Style}</style></head><body><main>
            <header><h1>{title}{(player.Length > 0 ? " · " + H(player) : "")}</h1>
            <span class="muted">{updated}</span></header>
            {ProfilesLine(profiles, player)}
            <section class="tiles">
              <div class="tile main"><div class="v">{L.N(totalChests)}</div><div class="l">{L.T("coffres ouverts ou cassés et canons à butin", "chests opened or broken and loot cannons", "cofres abiertos o rotos y cañones de botín")}</div></div>
              <div class="tile"><div class="v">{PirateTokenOdds.Format(opened)}</div><div class="l">{L.T("jeton pirate · chance cumulée", "pirate token · cumulative chance", "ficha pirata · probabilidad acumulada")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.ChestsDestroyed))}</div><div class="l">{L.T("dont coffres cassés", "including broken chests", "de ellos, cofres rotos")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.SpriteChests))}</div><div class="l">{L.T("dont coffres d’esprit", "including Sprite Chests", "de ellos, cofres de espíritu")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.LootCannons))}</div><div class="l">{L.T("dont canons à butin", "including loot cannons", "de ellos, cañones de botín")}</div></div>
              <div class="tile"><div class="v">{wanted}</div><div class="l">{L.T("cartes Wanted", "Wanted maps", "mapas Wanted")}</div></div>
              <div class="tile"><div class="v">{matches.Count.ToString("N0", Fr)}</div><div class="l">{L.T("parties analysées", "matches analyzed", "partidas analizadas")}</div></div>
              <div class="tile"><div class="v">{avg.ToString("0.0", Fr)}</div><div class="l">{L.T("coffres par partie", "chests per match", "cofres por partida")}</div></div>
              <div class="tile"><div class="v">{best}</div><div class="l">{L.T("record en une partie", "best match", "récord en una partida")}</div></div>
              <div class="tile"><div class="v">{session}</div><div class="l">{L.T("coffres depuis le lancement de l'outil", "chests since the tool was started", "cofres desde que se abrió la herramienta")}</div></div>
              <div class="tile"><div class="v">{treasures}</div><div class="l">{L.T("trésors repérés", "treasures found", "tesoros encontrados")}</div></div>
            </section>
            <p class="muted">{L.T(
                $"Chance estimée d’avoir obtenu au moins un jeton sur {L.N(opened)} ouvertures, au taux choisi de 1 sur {L.N(PirateTokenOdds.DropDenominator)} par coffre.",
                $"Estimated chance of finding at least one token across {L.N(opened)} openings at the selected rate of 1 in {L.N(PirateTokenOdds.DropDenominator)} per chest.",
                $"Probabilidad estimada de obtener al menos una ficha en {L.N(opened)} aperturas, a la tasa elegida de 1 entre {L.N(PirateTokenOdds.DropDenominator)} por cofre.")}</p>
            """);

        // Last 30 days, one bar per day.
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        var perDay = days.Select(d => matches.Where(m => When(m).Date == d).Sum(m => m.ChestsTotal)).ToList();
        var max = Math.Max(1, perDay.Max());
        sb.Append($"<h2>{L.T("Coffres par jour (30 derniers jours)", "Chests per day (last 30 days)", "Cofres por día (últimos 30 días)")}</h2><div class=\"card chart\"><div class=\"bars\">");
        for (var i = 0; i < days.Count; i++)
        {
            var v = perDay[i];
            var h = (int)Math.Round(100.0 * v / max);
            var tip = $"{days[i].ToString(L.T("ddd dd/MM", "ddd d MMM", "ddd dd/MM"), Fr)}{L.T(" : ", ": ", ": ")}{L.Count(v, "coffre", "chest", "cofre")}";
            // Tooltip sits just above the bar top (bar height is a % of the 112px plot area).
            sb.Append($"<div class=\"col\" aria-label=\"{H(tip)}\"><span class=\"tip\" style=\"bottom:calc({Math.Max(h, 3)}% + 6px)\">{H(tip)}</span><div class=\"bar{(v == 0 ? " zero" : "")}\" style=\"height:{h}%\"></div></div>");
        }
        sb.Append($"</div><div class=\"axis\"><span>{days[0].ToString(L.T("dd/MM", "d MMM", "dd/MM"), Fr)}</span><span>{L.T("aujourd'hui", "today", "hoy")}</span></div></div>");

        // Breakdown by chest type, then the other interactions (only loot cannons are in the total).
        var byKind = matches.SelectMany(m => m.Chests)
            .GroupBy(c => c.Kind).OrderByDescending(g => g.Count()).ToList();
        var otherKinds = matches
            .SelectMany(m => m.OtherInteractions.Count > 0
                ? m.OtherInteractions
                : new Dictionary<string, int> { [ChestKinds.AmmoBox] = m.AmmoBoxesOpened, [ChestKinds.OtherContainer] = m.OtherContainersOpened })
            .GroupBy(kv => kv.Key)
            .Select(g => (Kind: g.Key, Count: g.Sum(kv => kv.Value)))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();
        sb.Append($"<h2>{L.T("Par type", "By type", "Por tipo")}</h2><div class=\"card\"><table><tr><th>{L.T("Type", "Type", "Tipo")}</th><th style=\"text-align:right\">{L.T("Total", "Total", "Total")}</th><th style=\"text-align:right\">{L.T("Dont cassés", "Of which broken", "De ellos, rotos")}</th></tr>");
        foreach (var g in byKind)
        {
            sb.Append($"<tr><td>{H(L.Name(g.Key))}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{g.Count(c => c.Destroyed)}</td></tr>");
        }
        foreach (var (kind, count) in otherKinds)
        {
            sb.Append(kind == ChestKinds.Cannon
                ? $"<tr><td>{H(L.Name(kind))}</td><td class=\"n\">{count}</td><td></td></tr>"
                : $"<tr><td class=\"muted\">{H(L.Name(kind))} {L.T("(autre interaction)", "(other interaction)", "(otra interacción)")}</td><td class=\"n muted\">{count}</td><td></td></tr>");
        }
        sb.Append("</table></div>");

        // Treasures.
        var treasureRows = matches.SelectMany(m => m.TreasureFinds.Select(f => (m, f))).ToList();
        if (treasureRows.Count > 0)
        {
            var unusual = treasureRows.Where(r => !r.f.SourceIsChest).ToList();
            if (unusual.Count > 0)
            {
                var kinds = H(string.Join(", ", unusual.Select(r => L.Name(r.f.SourceKind)).Distinct()));
                var count = L.Count(unusual.Count, "trésor obtenu", "trésors obtenus", "treasure", "treasures", "tesoro obtenido", "tesoros obtenidos");
                sb.Append(L.T(
                    $"<div class=\"card flag\">⚑ <b>{count} autrement qu'avec un coffre</b> : {kinds}. Détail dans le tableau ci-dessous.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} obtained other than from a chest</b>: {kinds}. Details in the table below.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} de otra forma que con un cofre</b>: {kinds}. Detalles en la tabla de abajo.</div>"));
            }

            sb.Append($"<h2>{L.T("Trésors obtenus", "Treasures obtained", "Tesoros obtenidos")}</h2><div class=\"card\"><table><tr><th>{L.T("Partie", "Match", "Partida")}</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th><th>{L.T("Obtenu à", "Obtained at", "Momento")}</th><th>{L.T("Source", "Source", "Origen")}</th></tr>");
            foreach (var (m, f) in treasureRows)
            {
                var id = f.PrizeId != null ? $" <span class=\"muted\">({H(f.PrizeId)})</span>" : (f.RawItem.StartsWith("WID_") ? $" <span class=\"muted\">({H(f.RawItem)})</span>" : "");
                var when = f.ObtainedAtSeconds is float t ? (f.TimeExact ? Pretty.Clock(t) : $"{L.T("vers", "around", "hacia los")} {Pretty.Clock(t)}") : "<span class=\"muted\">?</span>";
                var source = $"{(f.SourceIsChest ? "" : "⚑ ")}{H(f.SourceLabel)}"
                    + (f.SourceCertain ? "" : $" <span class=\"muted\">({Pretty.Likely})</span>")
                    + (!f.SourceIsChest && !string.IsNullOrEmpty(f.SourceBlueprint) ? $"<br><span class=\"muted\">{H(f.SourceBlueprint)}</span>" : "")
                    + (f.AlsoPossible.Count > 0 ? $"<br><span class=\"muted\">{L.T("aussi possible : ", "also possible: ", "también posible: ")}{H(string.Join(", ", f.AlsoPossible.Select(L.Name)))}</span>" : "");
                sb.Append($"<tr{(f.SourceIsChest ? "" : " class=\"unusual\"")}><td class=\"nw\">{L.DateTimeShort(When(m))}</td><td class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}{id}</td><td class=\"nw\">{when}</td><td>{source}</td></tr>");
            }
            sb.Append("</table></div>");

            // Which kinds of containers / interactions have produced treasures so far.
            var bySource = treasureRows.GroupBy(r => r.f.SourceLabel).OrderByDescending(g => g.Count()).ToList();
            sb.Append($"<h2>{L.T("D'où viennent les trésors", "Where the treasures come from", "De dónde vienen los tesoros")}</h2><div class=\"card\"><table><tr><th>{L.T("Source", "Source", "Origen")}</th><th style=\"text-align:right\">{L.T("Trésors", "Treasures", "Tesoros")}</th><th style=\"text-align:right\">{L.T("dont certains", "of which certain", "de ellos, seguros")}</th></tr>");
            foreach (var g in bySource)
            {
                var isChest = g.First().f.SourceIsChest;
                sb.Append($"<tr{(isChest ? "" : " class=\"unusual\"")}><td>{(isChest ? "" : "⚑ ")}{H(g.Key)}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{g.Count(r => r.f.SourceCertain)}</td></tr>");
            }
            sb.Append("</table></div>");
        }

        sb.Append($"<h2>{L.T("Historique des parties", "Match history", "Historial de partidas")}</h2><div class=\"card\"><table><tr><th>{L.T("Date", "Date", "Fecha")}</th><th>{L.T("Mode", "Mode", "Modo")}</th><th>{L.T("Place", "Placement", "Puesto")}</th><th>{L.T("Coffres", "Chests", "Cofres")}</th><th>{L.T("Cassés", "Broken", "Rotos")}</th><th>{L.T("Esprits", "Sprites", "Espíritus")}</th><th>Wanted</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th></tr>");
        foreach (var m in matches)
        {
            var treasure = string.Join("<br>", m.TreasureFinds.Select(f => $"<span class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>"));
            sb.Append($"<tr><td>{L.DateTimeShort(When(m))}");
            if (m.Chests.Count > 0)
            {
                sb.Append($"<details><summary>{L.T("Voir les coffres", "View chests", "Ver cofres")}</summary><ul>");
                foreach (var chest in m.Chests)
                {
                    var destroyed = chest.Destroyed ? L.T(" · détruit", " · destroyed", " · destruido") : "";
                    sb.Append($"<li>{Pretty.Clock(chest.TimeSeconds)} · {H(L.Name(chest.Kind))}{destroyed}</li>");
                }
                sb.Append("</ul></details>");
            }
            sb.Append($"</td><td>{H(Pretty.Playlist(m.Playlist))}</td><td>{H(Pretty.Placement(m))}</td><td class=\"n\">{m.ChestsTotal}</td><td class=\"n\">{m.ChestsDestroyed}</td><td class=\"n\">{m.SpriteChests}</td>");
            sb.Append("<td class=\"n\">");
            if (m.WantedMaps is { Count: > 0 } maps)
            {
                sb.Append($"<details><summary>{maps.Count}</summary>{string.Join("<br>", maps.Select(Pretty.Clock))}</details>");
            }
            else
            {
                sb.Append(m.WantedMaps == null ? "—" : "0");
            }
            sb.Append($"</td><td class=\"items\">{treasure}</td></tr>");
        }
        sb.Append("</table></div>");

        sb.Append(L.T(
            """
            <footer>Le total inclut les coffres ouverts et cassés. Les cartes Wanted sont comptées lors du premier ramassage après une affiche.
            Un tiret indique une partie conservée depuis une ancienne analyse.
            ★★ = coffre doré de la chasse au trésor (cosmétique, accès boutique…),
            détecté de façon certaine. ★ = trésor reçu grâce à un de tes coffres (moment exact), nommé quand l'objet est identifiable.
            Les noms des récompenses se modifient dans treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>The total includes opened and broken chests. Wanted maps are counted on the first pickup from a poster.
            A dash marks a match saved by an older analysis.
            ★★ = golden treasure hunt chest (cosmetic, shop access…), detected for sure.
            ★ = treasure received from one of your own chests (exact moment), named when the item can be identified.
            Prize names can be edited in treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>El total incluye cofres abiertos y rotos. Los mapas Wanted se cuentan al recogerlos por primera vez tras un cartel.
            Un guion indica una partida guardada en un análisis anterior.
            ★★ = cofre dorado de la búsqueda del tesoro (cosmético, acceso a la tienda…),
            detectado con seguridad. ★ = tesoro recibido de uno de tus cofres (momento exacto), con nombre cuando el objeto se puede identificar.
            Los nombres de las recompensas se pueden editar en treasures.json.</footer>
            </main></body></html>
            """));
        return sb.ToString();
    }

    private static string ProfilesLine(IReadOnlyList<Profile> profiles, string player)
    {
        var others = profiles.Where(p => p.Name != player).Select(p => H(p.Name)).ToList();
        if (others.Count == 0)
        {
            return "";
        }
        return $"""<div class="profiles muted">{L.T("Autres profils sur ce PC : ", "Other profiles on this PC: ", "Otros perfiles en este PC: ")}{string.Join(", ", others)} · {L.T("sélectionne le compte dans TrackChest", "select the account in TrackChest", "elige la cuenta en TrackChest")}</div>""";
    }
}

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
            return L.T("mode inconnu", "unknown mode", "modo desconocido", "unbekannter Modus", "неизвестный режим", "不明なモード", "modalità sconosciuta", "modo desconhecido", "nieznany tryb", "onbekende modus", "bilinmeyen mod", "알 수 없는 모드", "未知模式", "невідомий режим", "okänt läge", "neznámý režim");
        }

        var p = playlist.ToLowerInvariant();
        var parts = new List<string>();
        if (p.Contains("habanero") || p.Contains("ranked")) parts.Add(L.T("Classée", "Ranked", "Clasificatoria", "Gewertet", "Рейтинговый", "ランク", "Classificata", "Ranqueada", "Rankingowy", "Ranked", "Dereceli", "경쟁전", "排位", "Рейтинговий", "Rankad", "Hodnocené"));
        if (p.Contains("nobuild")) parts.Add(L.T("Zéro construction", "Zero Build", "Cero construcción", "Null Bauen", "Без строительства", "ゼロビルド", "Zero Build", "Construção Zero", "Zero budowania", "Zero Build", "Sıfır İnşa", "제로 빌드", "零建造", "Без будівництва", "Zero Build", "Zero Build"));
        if (p.Contains("solo")) parts.Add(L.T("Solo", "Solo", "Solitario", "Solo", "Соло", "ソロ", "Singolo", "Solo", "Solo", "Solo", "Solo", "솔로", "单人", "Соло", "Solo", "Sólo"));
        else if (p.Contains("duo")) parts.Add(L.T("Duo", "Duos", "Dúos", "Duos", "Дуо", "デュオ", "Duo", "Duplas", "Duety", "Duo's", "İkili", "듀오", "双人", "Дуети", "Duo", "Dvojice"));
        else if (p.Contains("trio")) parts.Add(L.T("Trio", "Trios", "Tríos", "Trios", "Трио", "トリオ", "Trio", "Trios", "Tria", "Trio's", "Üçlü", "트리오", "三人", "Тріо", "Trio", "Trojice"));
        else if (p.Contains("squad")) parts.Add(L.T("Section", "Squads", "Escuadrones", "Squads", "Отряды", "スクワッド", "Squadre", "Esquadrões", "Drużyny", "Squads", "Takım", "스쿼드", "四人", "Загони", "Lag", "Čtveřice"));
        return parts.Count > 0 ? string.Join(" · ", parts) : playlist.Replace("Playlist_", "");
    }

    public static string Placement(MatchRecord m)
    {
        if (m.Placement is not int place || place <= 0)
        {
            return L.T("place inconnue", "unknown placement", "puesto desconocido", "Platzierung unbekannt", "место неизвестно", "順位不明", "piazzamento sconosciuto", "colocação desconhecida", "nieznane miejsce", "onbekende plaats", "bilinmeyen sıralama", "알 수 없는 순위", "未知名次", "невідоме місце", "okänd placering", "neznámé umístění");
        }
        if (place == 1)
        {
            return L.T("Top 1 !", "Victory Royale!", "¡Victoria magistral!", "Victory Royale!", "Королевская победа!", "ビクトリーロイヤル！", "Vittoria Reale!", "Vitória Royale!", "Królewskie zwycięstwo!", "Victory Royale!", "Zafer Royale!", "빅토리 로얄!", "大吉大利！", "Королівська перемога!", "Victory Royale!", "Královské vítězství!");
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
            return L.T("moment et source inconnus", "time and source unknown", "momento y origen desconocidos", "Zeitpunkt und Quelle unbekannt", "время и источник неизвестны", "時刻と入手元は不明", "momento e origine sconosciuti", "momento e origem desconhecidos", "czas i źródło nieznane", "tijd en bron onbekend", "zaman ve kaynak bilinmiyor", "시간 및 출처 알 수 없음", "时间和来源未知", "час і джерело невідомі", "tid och källa okända", "čas a zdroj neznámé");
        }
        var when = f.TimeExact ? $"{L.T("à", "at", "a los", "um", "в", "時刻", "alle", "às", "o", "om", "saat", "시각", "于", "о", "kl.", "v")} {Clock(t)}" : $"{L.T("vers", "around", "hacia los", "gegen", "около", "約", "verso", "por volta de", "około", "rond", "yaklaşık", "약", "约", "близько", "cirka", "kolem")} {Clock(t)}";
        return f.SourceCertain ? $"{when} · {f.SourceLabel}" : $"{when} · {f.SourceLabel} ({Likely})";
    }

    public static string Likely => L.T("probable", "likely", "probable", "wahrscheinlich", "вероятно", "推定", "probabile", "provável", "prawdopodobnie", "waarschijnlijk", "muhtemelen", "가능성 높음", "可能", "імовірно", "troligen", "pravděpodobně");

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
        WriteAtomic(store.ReportPath, Report(view, Profiles.List(data, config)));
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Img(string key) => OverlayIcons.DataUri(key) is { } uri ? $"<img class=\"ico\" src=\"{uri}\" alt=\"\">" : "";

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
        .ico{width:16px;height:16px;vertical-align:-3px;margin-right:5px;object-fit:contain}
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
        var title = L.T("Compteur de coffres", "Chest Counter", "Contador de cofres", "Truhenzähler", "Счётчик сундуков", "宝箱カウンター", "Contatore di forzieri", "Contador de baús", "Licznik skrzyń", "Kistenteller", "Sandık Sayacı", "상자 카운터", "宝箱计数器", "Лічильник скринь", "Kisträknare", "Počítadlo truhel");
        var updated = L.T(
            $"Mis à jour le {DateTime.Now.ToString("dd/MM/yyyy à HH:mm:ss", Fr)}",
            $"Updated {DateTime.Now.ToString("d MMM yyyy 'at' HH:mm:ss", Fr)}",
            $"Actualizado el {DateTime.Now.ToString("dd/MM/yyyy 'a las' HH:mm:ss", Fr)}",
            $"Aktualisiert am {DateTime.Now.ToString("dd.MM.yyyy 'um' HH:mm:ss", Fr)}",
            $"Обновлено {DateTime.Now.ToString("dd.MM.yyyy 'в' HH:mm:ss", Fr)}",
            $"更新日時 {DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", Fr)}",
            $"Aggiornato il {DateTime.Now.ToString("d MMM yyyy 'alle' HH:mm:ss", Fr)}",
            $"Atualizado em {DateTime.Now.ToString("d MMM yyyy 'às' HH:mm:ss", Fr)}",
            $"Zaktualizowano {DateTime.Now.ToString("d MMM yyyy 'o' HH:mm:ss", Fr)}",
            $"Bijgewerkt op {DateTime.Now.ToString("d MMM yyyy 'om' HH:mm:ss", Fr)}",
            $"Güncellenme: {DateTime.Now.ToString("d MMM yyyy HH:mm:ss", Fr)}",
            $"업데이트: {DateTime.Now.ToString("yyyy. M. d. HH:mm:ss", Fr)}",
            $"更新于 {DateTime.Now.ToString("yyyy年M月d日 HH:mm:ss", Fr)}",
            $"Оновлено {DateTime.Now.ToString("d MMM yyyy 'о' HH:mm:ss", Fr)}",
            $"Uppdaterad {DateTime.Now.ToString("d MMM yyyy 'kl.' HH:mm:ss", Fr)}",
            $"Aktualizováno {DateTime.Now.ToString("d. MMMM yyyy 'v' HH:mm:ss", Fr)}");
        sb.Append($"""
            <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{title}</title><style>{Style}</style></head><body><main>
            <header><h1>{title}{(player.Length > 0 ? " · " + H(player) : "")}</h1>
            <span class="muted">{updated}</span></header>
            {ProfilesLine(profiles, player)}
            <section class="tiles">
              <div class="tile main"><div class="v">{L.N(totalChests)}</div><div class="l">{Img("Chest.png")}{L.T("coffres ouverts ou cassés et canons à butin", "chests opened or broken and loot cannons", "cofres abiertos o rotos y cañones de botín", "geöffnete oder zerstörte Truhen und Beutekanonen", "открытые или разбитые сундуки и пушки с добычей", "開けた・壊した宝箱と戦利品キャノン", "forzieri aperti o rotti e cannoni del bottino", "baús abertos ou quebrados e canhões de saque", "skrzynie otwarte lub rozbite i działa z łupem", "geopende of kapotte kisten en buitkanonnen", "açılan veya kırılan sandıklar ve ganimet topları", "열거나 부순 상자 및 전리품 대포", "打开或打破的宝箱和战利品大炮", "відкриті або розбиті скрині та гармати зі здобиччю", "kistor öppnade eller krossade och bytekanoner", "otevřené nebo rozbité truhly a kořistní děla")}</div></div>
              <div class="tile"><div class="v">{PirateTokenOdds.Format(opened)}</div><div class="l">{Img("Pirate_Token.png")}{L.T("jeton pirate · chance cumulée", "pirate token · cumulative chance", "ficha pirata · probabilidad acumulada", "Piraten-Token · Gesamtchance", "пиратский жетон · суммарный шанс", "海賊トークン · 累積確率", "gettone pirata · probabilità cumulativa", "ficha pirata · chance acumulada", "piracki żeton · łączna szansa", "piratenmunt · cumulatieve kans", "korsan jetonu · kümülatif şans", "해적 토큰 · 누적 확률", "海盗代币 · 累计概率", "піратський жетон · сукупна ймовірність", "piratpollett · sammanlagd chans", "pirátský žeton · kumulativní šance")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.ChestsDestroyed))}</div><div class="l">{Img("break_chest.png")}{L.T("dont coffres cassés", "including broken chests", "de ellos, cofres rotos", "davon zerstörte Truhen", "из них разбитых сундуков", "うち壊した宝箱", "inclusi i forzieri rotti", "incluindo baús quebrados", "w tym rozbite skrzynie", "inclusief kapotte kisten", "kırılan sandıklar dahil", "부순 상자 포함", "含打破的宝箱", "разом із розбитими скринями", "inklusive krossade kistor", "včetně rozbitých truhel")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.SpriteChests))}</div><div class="l">{Img("Esprit_Chest.png")}{L.T("dont coffres d’esprit", "including Sprite Chests", "de ellos, cofres de espíritu", "davon Geistertruhen", "из них сундуков духов", "うちスプライト宝箱", "inclusi i forzieri degli spiriti", "incluindo Baús de Espírito", "w tym skrzynie duszków", "inclusief geestkisten", "Ruh Sandıkları dahil", "스프라이트 상자 포함", "含精灵宝箱", "разом зі скринями духів", "inklusive andekistor", "včetně truhel skřítků")}</div></div>
              <div class="tile"><div class="v">{L.N(matches.Sum(m => m.LootCannons))}</div><div class="l">{Img("Loot_Cannons.png")}{L.T("dont canons à butin", "including loot cannons", "de ellos, cañones de botín", "davon Beutekanonen", "из них пушек с добычей", "うち戦利品キャノン", "inclusi i cannoni del bottino", "incluindo canhões de saque", "w tym działa z łupem", "inclusief buitkanonnen", "ganimet topları dahil", "전리품 대포 포함", "含战利品大炮", "разом із гарматами зі здобиччю", "inklusive bytekanoner", "včetně kořistních děl")}</div></div>
              <div class="tile"><div class="v">{wanted}</div><div class="l">{Img("Wanted_Card.png")}{L.T("cartes Wanted", "Wanted maps", "mapas Wanted", "Wanted-Karten", "Карты Wanted", "Wanted マップ", "Mappe Wanted", "Mapas Wanted", "Mapy Wanted", "Wanted-kaarten", "Wanted haritaları", "Wanted 지도", "Wanted 地图", "Карти Wanted", "Wanted-kartor", "Mapy Wanted")}</div></div>
              <div class="tile"><div class="v">{matches.Count.ToString("N0", Fr)}</div><div class="l">{L.T("parties analysées", "matches analyzed", "partidas analizadas", "analysierte Matches", "проанализировано матчей", "分析した試合", "partite analizzate", "partidas analisadas", "przeanalizowane mecze", "geanalyseerde potjes", "analiz edilen maç", "분석한 경기", "已分析比赛", "проаналізовано матчів", "analyserade matcher", "analyzované zápasy")}</div></div>
              <div class="tile"><div class="v">{avg.ToString("0.0", Fr)}</div><div class="l">{L.T("coffres par partie", "chests per match", "cofres por partida", "Truhen pro Match", "сундуков за матч", "1試合あたりの宝箱", "forzieri per partita", "baús por partida", "skrzynie na mecz", "kisten per potje", "maç başına sandık", "경기당 상자", "每场宝箱", "скринь за матч", "kistor per match", "truhel na zápas")}</div></div>
              <div class="tile"><div class="v">{best}</div><div class="l">{L.T("record en une partie", "best match", "récord en una partida", "Rekord in einem Match", "рекорд за матч", "1試合の最高記録", "miglior partita", "melhor partida", "najlepszy mecz", "beste potje", "en iyi maç", "최고 경기", "最佳比赛", "найкращий матч", "bästa match", "nejlepší zápas")}</div></div>
              <div class="tile"><div class="v">{session}</div><div class="l">{L.T("coffres depuis le lancement de l'outil", "chests since the tool was started", "cofres desde que se abrió la herramienta", "Truhen seit dem Start des Programms", "сундуков с момента запуска программы", "ツール起動後の宝箱", "forzieri dall'avvio dello strumento", "baús desde que a ferramenta foi iniciada", "skrzynie od uruchomienia narzędzia", "kisten sinds de start van de tool", "araç başlatıldığından beri sandık", "도구 실행 이후 상자", "工具启动以来的宝箱", "скринь від запуску програми", "kistor sedan verktyget startades", "truhel od spuštění nástroje")}</div></div>
              <div class="tile"><div class="v">{treasures}</div><div class="l">{Img("Treasures.png")}{L.T("trésors repérés", "treasures found", "tesoros encontrados", "gefundene Schätze", "найдено сокровищ", "見つけた宝", "tesori trovati", "tesouros encontrados", "znalezione skarby", "gevonden schatten", "bulunan hazine", "찾은 보물", "找到的宝藏", "знайдено скарбів", "hittade skatter", "nalezené poklady")}</div></div>
            </section>
            <p class="muted">{L.T(
                $"Chance estimée d’avoir obtenu au moins un jeton sur {L.N(opened)} ouvertures, au taux choisi de 1 sur {L.N(PirateTokenOdds.DropDenominator)} par coffre.",
                $"Estimated chance of finding at least one token across {L.N(opened)} openings at the selected rate of 1 in {L.N(PirateTokenOdds.DropDenominator)} per chest.",
                $"Probabilidad estimada de obtener al menos una ficha en {L.N(opened)} aperturas, a la tasa elegida de 1 entre {L.N(PirateTokenOdds.DropDenominator)} por cofre.",
                $"Geschätzte Chance, bei {L.N(opened)} Öffnungen mindestens ein Token zu erhalten, bei der gewählten Rate von 1 zu {L.N(PirateTokenOdds.DropDenominator)} pro Truhe.",
                $"Оценочный шанс получить хотя бы один жетон за {L.N(opened)} открытий при выбранной вероятности 1 к {L.N(PirateTokenOdds.DropDenominator)} на сундук.",
                $"宝箱1個あたり {L.N(PirateTokenOdds.DropDenominator)} 分の1 の確率で、{L.N(opened)} 回開けて少なくとも1枚のトークンを得る推定確率です。",
                $"Probabilità stimata di trovare almeno un gettone su {L.N(opened)} aperture al tasso scelto di 1 su {L.N(PirateTokenOdds.DropDenominator)} per forziere.",
                $"Chance estimada de encontrar pelo menos uma ficha em {L.N(opened)} aberturas, com a taxa escolhida de 1 em {L.N(PirateTokenOdds.DropDenominator)} por baú.",
                $"Szacowana szansa na znalezienie co najmniej jednego żetonu przy {L.N(opened)} otwarciach, przy wybranej stawce 1 na {L.N(PirateTokenOdds.DropDenominator)} na skrzynię.",
                $"Geschatte kans om minstens één munt te vinden over {L.N(opened)} openingen, met de gekozen kans van 1 op {L.N(PirateTokenOdds.DropDenominator)} per kist.",
                $"Seçilen sandık başına 1/{L.N(PirateTokenOdds.DropDenominator)} oranıyla, {L.N(opened)} açılışta en az bir jeton bulma tahmini şansı.",
                $"상자당 1/{L.N(PirateTokenOdds.DropDenominator)}의 선택한 확률로 {L.N(opened)}번 열었을 때 토큰을 하나 이상 찾을 추정 확률.",
                $"按所选每个宝箱 1/{L.N(PirateTokenOdds.DropDenominator)} 的概率，在 {L.N(opened)} 次开启中至少找到一枚代币的估计概率。",
                $"Оцінена ймовірність знайти хоча б один жетон за {L.N(opened)} відкриттів при вибраній частоті 1 з {L.N(PirateTokenOdds.DropDenominator)} на скриню.",
                $"Uppskattad chans att hitta minst en pollett på {L.N(opened)} öppningar med den valda chansen 1 på {L.N(PirateTokenOdds.DropDenominator)} per kista.",
                $"Odhadovaná šance najít alespoň jeden žeton při {L.N(opened)} otevřeních se zvolenou šancí 1 z {L.N(PirateTokenOdds.DropDenominator)} na truhlu.")}</p>
            """);

        // Last 30 days, one bar per day.
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        var perDay = days.Select(d => matches.Where(m => When(m).Date == d).Sum(m => m.ChestsTotal)).ToList();
        var max = Math.Max(1, perDay.Max());
        sb.Append($"<h2>{L.T("Coffres par jour (30 derniers jours)", "Chests per day (last 30 days)", "Cofres por día (últimos 30 días)", "Truhen pro Tag (letzte 30 Tage)", "Сундуки по дням (последние 30 дней)", "1日あたりの宝箱（過去30日）", "Forzieri al giorno (ultimi 30 giorni)", "Baús por dia (últimos 30 dias)", "Skrzynie dziennie (ostatnie 30 dni)", "Kisten per dag (laatste 30 dagen)", "Günlük sandık (son 30 gün)", "일별 상자 (최근 30일)", "每日宝箱（最近 30 天）", "Скрині за день (останні 30 днів)", "Kistor per dag (senaste 30 dagarna)", "Truhly za den (posledních 30 dní)")}</h2><div class=\"card chart\"><div class=\"bars\">");
        for (var i = 0; i < days.Count; i++)
        {
            var v = perDay[i];
            var h = (int)Math.Round(100.0 * v / max);
            var tip = $"{days[i].ToString(L.T("ddd dd/MM", "ddd d MMM", "ddd dd/MM", "ddd dd.MM.", "ddd dd.MM", "M/d (ddd)", "ddd d MMM", "ddd d MMM", "ddd d MMM", "ddd d MMM", "d MMM ddd", "M월 d일 (ddd)", "M月d日 ddd", "ddd d MMM", "ddd d MMM", "ddd d. M."), Fr)}{L.T(" : ", ": ", ": ", ": ", ": ", "：", ": ", ": ", ": ", ": ", ": ", ": ", "： ", ": ", ": ", ": ")}{L.Chests(v)}";
            // Tooltip sits just above the bar top (bar height is a % of the 112px plot area).
            sb.Append($"<div class=\"col\" aria-label=\"{H(tip)}\"><span class=\"tip\" style=\"bottom:calc({Math.Max(h, 3)}% + 6px)\">{H(tip)}</span><div class=\"bar{(v == 0 ? " zero" : "")}\" style=\"height:{h}%\"></div></div>");
        }
        sb.Append($"</div><div class=\"axis\"><span>{days[0].ToString(L.T("dd/MM", "d MMM", "dd/MM", "dd.MM.", "dd.MM", "M/d", "d MMM", "d MMM", "d MMM", "d MMM", "d MMM", "M월 d일", "M月d日", "d MMM", "d MMM", "d. M."), Fr)}</span><span>{L.T("aujourd'hui", "today", "hoy", "heute", "сегодня", "今日", "oggi", "hoje", "dziś", "vandaag", "bugün", "오늘", "今天", "сьогодні", "i dag", "dnes")}</span></div></div>");

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
        sb.Append($"<h2>{L.T("Par type", "By type", "Por tipo", "Nach Typ", "По типу", "種類別", "Per tipo", "Por tipo", "Według typu", "Per type", "Türe göre", "유형별", "按类型", "За типом", "Per typ", "Podle typu")}</h2><div class=\"card\"><table><tr><th>{L.T("Type", "Type", "Tipo", "Typ", "Тип", "種類", "Tipo", "Tipo", "Typ", "Type", "Tür", "유형", "类型", "Тип", "Typ", "Typ")}</th><th style=\"text-align:right\">{L.T("Total", "Total", "Total", "Gesamt", "Всего", "合計", "Totale", "Total", "Łącznie", "Totaal", "Toplam", "합계", "总计", "Усього", "Totalt", "Celkem")}</th><th style=\"text-align:right\">{L.T("Dont cassés", "Of which broken", "De ellos, rotos", "Davon zerstört", "Из них разбито", "うち破壊", "Di cui rotti", "Dos quais quebrados", "W tym rozbite", "Waarvan kapot", "Kırılanlar", "그중 부숨", "其中打破", "З них розбито", "Varav krossade", "Z toho rozbité")}</th></tr>");
        foreach (var g in byKind)
        {
            sb.Append($"<tr><td>{H(L.Name(g.Key))}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{g.Count(c => c.Destroyed)}</td></tr>");
        }
        foreach (var (kind, count) in otherKinds)
        {
            sb.Append(kind == ChestKinds.Cannon
                ? $"<tr><td>{H(L.Name(kind))}</td><td class=\"n\">{count}</td><td></td></tr>"
                : $"<tr><td class=\"muted\">{H(L.Name(kind))} {L.T("(autre interaction)", "(other interaction)", "(otra interacción)", "(andere Interaktion)", "(другое взаимодействие)", "（その他の操作）", "(altra interazione)", "(outra interação)", "(inna interakcja)", "(andere interactie)", "(başka etkileşim)", "(기타 상호작용)", "（其他互动）", "(інша взаємодія)", "(annan interaktion)", "(jiná interakce)")}</td><td class=\"n muted\">{count}</td><td></td></tr>");
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
                var count = L.TreasuresObtained(unusual.Count);
                sb.Append(L.T(
                    $"<div class=\"card flag\">⚑ <b>{count} autrement qu'avec un coffre</b> : {kinds}. Détail dans le tableau ci-dessous.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} obtained other than from a chest</b>: {kinds}. Details in the table below.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} de otra forma que con un cofre</b>: {kinds}. Detalles en la tabla de abajo.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} auf andere Weise als aus einer Truhe</b>: {kinds}. Details in der Tabelle unten.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} не из сундука</b>: {kinds}. Подробности в таблице ниже.</div>",
                    $"<div class=\"card flag\">⚑ <b>宝箱以外から {count}</b>: {kinds}。詳細は下の表を参照してください。</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} ottenuti non da un forziere</b>: {kinds}. Dettagli nella tabella qui sotto.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} obtidos fora de um baú</b>: {kinds}. Detalhes na tabela abaixo.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} zdobyte poza skrzynią</b>: {kinds}. Szczegóły w tabeli poniżej.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} verkregen buiten een kist</b>: {kinds}. Details in de tabel hieronder.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} sandık dışından elde edildi</b>: {kinds}. Ayrıntılar aşağıdaki tabloda.</div>",
                    $"<div class=\"card flag\">⚑ <b>상자 외에서 얻은 {count}</b>: {kinds}. 자세한 내용은 아래 표를 참고하세요.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} 并非来自宝箱</b>：{kinds}。详情见下表。</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} отримано не зі скрині</b>: {kinds}. Подробиці в таблиці нижче.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} erhållna utanför en kista</b>: {kinds}. Detaljer i tabellen nedan.</div>",
                    $"<div class=\"card flag\">⚑ <b>{count} získáno mimo truhlu</b>: {kinds}. Podrobnosti v tabulce níže.</div>"));
            }

            sb.Append($"<h2>{L.T("Trésors obtenus", "Treasures obtained", "Tesoros obtenidos", "Erhaltene Schätze", "Полученные сокровища", "入手した宝", "Tesori ottenuti", "Tesouros obtidos", "Zdobyte skarby", "Verkregen schatten", "Elde edilen hazineler", "얻은 보물", "获得的宝藏", "Отримані скарби", "Erhållna skatter", "Získané poklady")}</h2><div class=\"card\"><table><tr><th>{L.T("Partie", "Match", "Partida", "Match", "Матч", "試合", "Partita", "Partida", "Mecz", "Potje", "Maç", "경기", "比赛", "Матч", "Match", "Zápas")}</th><th>{L.T("Trésor", "Treasure", "Tesoro", "Schatz", "Сокровище", "宝", "Tesoro", "Tesouro", "Skarb", "Schat", "Hazine", "보물", "宝藏", "Скарб", "Skatt", "Poklad")}</th><th>{L.T("Obtenu à", "Obtained at", "Momento", "Erhalten um", "Получено в", "入手時刻", "Ottenuto a", "Obtido em", "Zdobyto o", "Verkregen om", "Elde edilme anı", "획득 시점", "获得时间", "Отримано о", "Erhållen vid", "Získáno v")}</th><th>{L.T("Source", "Source", "Origen", "Quelle", "Источник", "入手元", "Origine", "Origem", "Źródło", "Bron", "Kaynak", "출처", "来源", "Джерело", "Källa", "Zdroj")}</th></tr>");
            foreach (var (m, f) in treasureRows)
            {
                var id = f.PrizeId != null ? $" <span class=\"muted\">({H(f.PrizeId)})</span>" : (f.RawItem.StartsWith("WID_") ? $" <span class=\"muted\">({H(f.RawItem)})</span>" : "");
                var when = f.ObtainedAtSeconds is float t ? (f.TimeExact ? Pretty.Clock(t) : $"{L.T("vers", "around", "hacia los", "gegen", "около", "約", "verso", "por volta de", "około", "rond", "yaklaşık", "약", "约", "близько", "cirka", "kolem")} {Pretty.Clock(t)}") : "<span class=\"muted\">?</span>";
                var source = $"{(f.SourceIsChest ? "" : "⚑ ")}{H(f.SourceLabel)}"
                    + (f.SourceCertain ? "" : $" <span class=\"muted\">({Pretty.Likely})</span>")
                    + (!f.SourceIsChest && !string.IsNullOrEmpty(f.SourceBlueprint) ? $"<br><span class=\"muted\">{H(f.SourceBlueprint)}</span>" : "")
                    + (f.AlsoPossible.Count > 0 ? $"<br><span class=\"muted\">{L.T("aussi possible : ", "also possible: ", "también posible: ", "auch möglich: ", "также возможно: ", "他の候補: ", "anche possibile: ", "também possível: ", "możliwe też: ", "ook mogelijk: ", "şu da olabilir: ", "다른 가능성: ", "也可能： ", "також можливо: ", "också möjligt: ", "možné také: ")}{H(string.Join(", ", f.AlsoPossible.Select(L.Name)))}</span>" : "");
                sb.Append($"<tr{(f.SourceIsChest ? "" : " class=\"unusual\"")}><td class=\"nw\">{L.DateTimeShort(When(m))}</td><td class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}{id}</td><td class=\"nw\">{when}</td><td>{source}</td></tr>");
            }
            sb.Append("</table></div>");

            // Which kinds of containers / interactions have produced treasures so far.
            var bySource = treasureRows.GroupBy(r => r.f.SourceLabel).OrderByDescending(g => g.Count()).ToList();
            sb.Append($"<h2>{L.T("D'où viennent les trésors", "Where the treasures come from", "De dónde vienen los tesoros", "Woher die Schätze stammen", "Откуда берутся сокровища", "宝の入手元", "Da dove vengono i tesori", "De onde vêm os tesouros", "Skąd pochodzą skarby", "Waar de schatten vandaan komen", "Hazineler nereden geliyor", "보물의 출처", "宝藏来源", "Звідки беруться скарби", "Var skatterna kommer ifrån", "Odkud poklady pocházejí")}</h2><div class=\"card\"><table><tr><th>{L.T("Source", "Source", "Origen", "Quelle", "Источник", "入手元", "Origine", "Origem", "Źródło", "Bron", "Kaynak", "출처", "来源", "Джерело", "Källa", "Zdroj")}</th><th style=\"text-align:right\">{L.T("Trésors", "Treasures", "Tesoros", "Schätze", "Сокровища", "宝", "Tesori", "Tesouros", "Skarby", "Schatten", "Hazineler", "보물", "宝藏", "Скарби", "Skatter", "Poklady")}</th><th style=\"text-align:right\">{L.T("dont certains", "of which certain", "de ellos, seguros", "davon sicher", "из них точно", "うち確定", "di cui certi", "dos quais certos", "w tym pewne", "waarvan zeker", "kesin olanlar", "그중 확실", "其中确定", "з них напевно", "varav säkra", "z toho jisté")}</th></tr>");
            foreach (var g in bySource)
            {
                var isChest = g.First().f.SourceIsChest;
                sb.Append($"<tr{(isChest ? "" : " class=\"unusual\"")}><td>{(isChest ? "" : "⚑ ")}{H(g.Key)}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{g.Count(r => r.f.SourceCertain)}</td></tr>");
            }
            sb.Append("</table></div>");
        }

        sb.Append($"<h2>{L.T("Historique des parties", "Match history", "Historial de partidas", "Match-Verlauf", "История матчей", "試合履歴", "Cronologia delle partite", "Histórico de partidas", "Historia meczów", "Potjesgeschiedenis", "Maç geçmişi", "경기 기록", "比赛记录", "Історія матчів", "Matchhistorik", "Historie zápasů")}</h2><div class=\"card\"><table><tr><th>{L.T("Date", "Date", "Fecha", "Datum", "Дата", "日時", "Data", "Data", "Data", "Datum", "Tarih", "날짜", "日期", "Дата", "Datum", "Datum")}</th><th>{L.T("Mode", "Mode", "Modo", "Modus", "Режим", "モード", "Modalità", "Modo", "Tryb", "Modus", "Mod", "모드", "模式", "Режим", "Läge", "Režim")}</th><th>{L.T("Place", "Placement", "Puesto", "Platz", "Место", "順位", "Piazzamento", "Colocação", "Miejsce", "Plaats", "Sıralama", "순위", "名次", "Місце", "Placering", "Umístění")}</th><th>{Img("Chest.png")}{L.T("Coffres", "Chests", "Cofres", "Truhen", "Сундуки", "宝箱", "Forzieri", "Baús", "Skrzynie", "Kisten", "Sandıklar", "상자", "宝箱", "Скрині", "Kistor", "Truhly")}</th><th>{Img("break_chest.png")}{L.T("Cassés", "Broken", "Rotos", "Zerstört", "Разбито", "破壊", "Rotti", "Quebrados", "Rozbite", "Kapot", "Kırılan", "부숨", "打破", "Розбито", "Krossade", "Rozbité")}</th><th>{Img("Esprit_Chest.png")}{L.T("Esprits", "Sprites", "Espíritus", "Geister", "Духи", "スプライト", "Spiriti", "Espíritos", "Duszki", "Geesten", "Ruhlar", "스프라이트", "精灵", "Духи", "Andar", "Skřítci")}</th><th>{Img("Wanted_Card.png")}Wanted</th><th>{Img("Treasures.png")}{L.T("Trésor", "Treasure", "Tesoro", "Schatz", "Сокровище", "宝", "Tesoro", "Tesouro", "Skarb", "Schat", "Hazine", "보물", "宝藏", "Скарб", "Skatt", "Poklad")}</th></tr>");
        foreach (var m in matches)
        {
            var treasure = string.Join("<br>", m.TreasureFinds.Select(f => $"<span class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>"));
            sb.Append($"<tr><td>{L.DateTimeShort(When(m))}");
            if (m.Chests.Count > 0)
            {
                sb.Append($"<details><summary>{L.T("Voir les coffres", "View chests", "Ver cofres", "Truhen anzeigen", "Показать сундуки", "宝箱を表示", "Vedi forzieri", "Ver baús", "Pokaż skrzynie", "Kisten bekijken", "Sandıkları gör", "상자 보기", "查看宝箱", "Переглянути скрині", "Visa kistor", "Zobrazit truhly")}</summary><ul>");
                foreach (var chest in m.Chests)
                {
                    var destroyed = chest.Destroyed ? L.T(" · détruit", " · destroyed", " · destruido", " · zerstört", " · разрушен", " · 破壊", " · distrutto", " · destruído", " · zniszczona", " · vernietigd", " · yok edildi", " · 파괴됨", " · 已摧毁", " · знищено", " · förstörd", " · zničená") : "";
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
            """,
            """
            <footer>Die Summe umfasst geöffnete und zerstörte Truhen. Wanted-Karten werden beim ersten Aufheben nach einem Plakat gezählt.
            Ein Strich kennzeichnet ein Match aus einer älteren Analyse.
            ★★ = goldene Truhe der Schatzsuche (Kosmetik, Shop-Zugang…), sicher erkannt.
            ★ = Schatz aus einer deiner eigenen Truhen (genauer Zeitpunkt), benannt, wenn der Gegenstand erkennbar ist.
            Die Namen der Belohnungen lassen sich in treasures.json ändern.</footer>
            </main></body></html>
            """,
            """
            <footer>Итог включает открытые и разбитые сундуки. Карты Wanted учитываются при первом подборе после плаката.
            Прочерк означает матч, сохранённый более старым анализом.
            ★★ = золотой сундук охоты за сокровищами (косметика, доступ к магазину…), определён точно.
            ★ = сокровище из одного из ваших сундуков (точный момент), с названием, если предмет удаётся определить.
            Названия наград можно изменить в treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>合計には開けた宝箱と壊した宝箱が含まれます。Wanted マップはポスターの後に最初に拾ったときに数えられます。
            ダッシュは古い分析で保存された試合を示します。
            ★★ = トレジャーハントの金の宝箱（コスメティック、ショップへのアクセスなど）、確実に検出。
            ★ = 自分の宝箱から得た宝（正確な時刻）。アイテムを特定できる場合は名前を表示します。
            報酬の名前は treasures.json で変更できます。</footer>
            </main></body></html>
            """,
            """
            <footer>Il totale include i forzieri aperti e rotti. Le mappe Wanted vengono contate al primo raccoglimento dopo un manifesto.
            Un trattino indica una partita salvata da un'analisi precedente.
            ★★ = forziere d'oro della caccia al tesoro (cosmetico, accesso al negozio…), rilevato con certezza.
            ★ = tesoro ricevuto da uno dei tuoi forzieri (momento esatto), con nome quando l'oggetto è identificabile.
            I nomi delle ricompense si modificano in treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>O total inclui baús abertos e quebrados. Os mapas Wanted são contados na primeira coleta após um cartaz.
            Um traço indica uma partida salva por uma análise antiga.
            ★★ = baú dourado da caça ao tesouro (cosmético, acesso à loja…), detectado com certeza.
            ★ = tesouro recebido de um dos seus baús (momento exato), com nome quando o item pode ser identificado.
            Os nomes dos prêmios podem ser editados em treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>Suma obejmuje otwarte i rozbite skrzynie. Mapy Wanted są liczone przy pierwszym podniesieniu po plakacie.
            Myślnik oznacza mecz zapisany przez starszą analizę.
            ★★ = złota skrzynia z poszukiwania skarbów (kosmetyk, dostęp do sklepu…), wykryta na pewno.
            ★ = skarb otrzymany z jednej z twoich skrzyń (dokładny moment), nazwany, gdy przedmiot da się rozpoznać.
            Nazwy nagród można edytować w treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>Het totaal omvat geopende en kapotte kisten. Wanted-kaarten worden geteld bij de eerste keer oppakken na een poster.
            Een streepje geeft een potje aan dat door een oudere analyse is opgeslagen.
            ★★ = gouden kist van de schattenjacht (cosmetisch, toegang tot de winkel…), met zekerheid gedetecteerd.
            ★ = schat ontvangen uit een van je eigen kisten (exact moment), met naam als het voorwerp herkenbaar is.
            De namen van de prijzen kun je aanpassen in treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>Toplam, açılan ve kırılan sandıkları içerir. Wanted haritaları bir afişten sonraki ilk alımda sayılır.
            Kısa çizgi, eski bir analizle kaydedilmiş bir maçı gösterir.
            ★★ = hazine avının altın sandığı (kozmetik, mağaza erişimi…), kesin olarak algılandı.
            ★ = kendi sandıklarından birinden alınan hazine (tam an), eşya tanınabildiğinde adıyla.
            Ödül adları treasures.json dosyasında düzenlenebilir.</footer>
            </main></body></html>
            """,
            """
            <footer>합계에는 연 상자와 부순 상자가 포함됩니다. Wanted 지도는 포스터 이후 처음 주울 때 집계됩니다.
            대시는 이전 분석으로 저장된 경기를 나타냅니다.
            ★★ = 보물찾기 황금 상자 (코스메틱, 상점 이용권…), 확실히 감지됨.
            ★ = 내 상자에서 받은 보물 (정확한 시점), 아이템을 식별할 수 있으면 이름 표시.
            보상 이름은 treasures.json에서 수정할 수 있습니다.</footer>
            </main></body></html>
            """,
            """
            <footer>总数包括打开和打破的宝箱。Wanted 地图在海报后的首次拾取时计数。
            短横线表示由旧版分析保存的比赛。
            ★★ = 寻宝金色宝箱（外观、商店权限…），确定检测到。
            ★ = 从你自己的宝箱获得的宝藏（准确时刻），可识别物品时显示名称。
            奖励名称可在 treasures.json 中修改。</footer>
            </main></body></html>
            """,
            """
            <footer>Загальна кількість включає відкриті й розбиті скрині. Карти Wanted рахуються під час першого підбирання після плаката.
            Рисочка позначає матч, збережений старішим аналізом.
            ★★ = золота скриня полювання за скарбами (косметика, доступ до магазину…), виявлена напевно.
            ★ = скарб, отриманий з однієї з ваших скринь (точний момент), з назвою, коли предмет можна визначити.
            Назви нагород можна змінити в treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>Totalen omfattar öppnade och krossade kistor. Wanted-kartor räknas vid första upplockningen efter en affisch.
            Ett streck markerar en match som sparats av en äldre analys.
            ★★ = gyllene kista från skattjakten (kosmetika, butiksåtkomst…), säkert upptäckt.
            ★ = skatt från en av dina egna kistor (exakt tidpunkt), namngiven när föremålet kan identifieras.
            Namnen på belöningarna kan ändras i treasures.json.</footer>
            </main></body></html>
            """,
            """
            <footer>Součet zahrnuje otevřené i rozbité truhly. Mapy Wanted se počítají při prvním sebrání po plakátu.
            Pomlčka označuje zápas uložený starší analýzou.
            ★★ = zlatá truhla z hledání pokladu (kosmetika, přístup do obchodu…), zjištěná s jistotou.
            ★ = poklad získaný z jedné z tvých truhel (přesný okamžik), pojmenovaný, když lze předmět rozpoznat.
            Názvy odměn lze upravit v treasures.json.</footer>
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
        return $"""<div class="profiles muted">{L.T("Autres profils sur ce PC : ", "Other profiles on this PC: ", "Otros perfiles en este PC: ", "Weitere Profile auf diesem PC: ", "Другие профили на этом ПК: ", "この PC の他のプロフィール: ", "Altri profili su questo PC: ", "Outros perfis neste PC: ", "Inne profile na tym komputerze: ", "Andere profielen op deze pc: ", "Bu bilgisayardaki diğer profiller: ", "이 PC의 다른 프로필: ", "此电脑上的其他档案： ", "Інші профілі на цьому ПК: ", "Andra profiler på den här datorn: ", "Další profily na tomto počítači: ")}{string.Join(", ", others)} · {L.T("sélectionne le compte dans TrackChest", "select the account in TrackChest", "elige la cuenta en TrackChest", "wähle das Konto in TrackChest", "выберите учётную запись в TrackChest", "TrackChest でアカウントを選択してください", "seleziona l'account in TrackChest", "selecione a conta no TrackChest", "wybierz konto w TrackChest", "selecteer het account in TrackChest", "hesabı TrackChest'te seç", "TrackChest에서 계정을 선택하세요", "在 TrackChest 中选择账户", "виберіть обліковий запис у TrackChest", "välj kontot i TrackChest", "vyber účet v TrackChest")}</div>""";
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Jint.Native;
using Jint.Native.Function;

namespace ChestCounter;

/// <summary>
/// Script add-ons: a <c>script.js</c> that defines <c>overlay(view, data)</c> and returns the overlay to display.
///
/// Sandbox: the script runs in Jint, a JavaScript interpreter written in .NET, with no access to .NET objects (CLR
/// interop is off), so it has no file, network, process or registry access. Only plain JSON goes in (the current
/// overlay and the statistics) and only plain JSON comes out, which is then validated. Time, memory, statement and
/// recursion limits stop a script that loops or grows forever.
/// </summary>
public static class AddonScript
{
    public const string FileName = "script.js";
    public const int MaxScriptBytes = 32 * 1024;
    private const int MaxMatches = 500;
    private const int MaxRows = 16;

    private static readonly Regex ColorPattern = new(@"^#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant);

    private static Engine NewEngine() => new(options => options
        .Strict()
        .TimeoutInterval(TimeSpan.FromMilliseconds(1000))
        .MaxStatements(2_000_000)
        .LimitMemory(64_000_000)
        .LimitRecursion(64));

    /// <summary>Install-time check: the script compiles, runs its top level within the limits and defines overlay().</summary>
    public static void Check(string source)
    {
        try
        {
            var engine = NewEngine();
            engine.Execute(source);
            if (engine.GetValue("overlay") is not Function)
                throw new InvalidDataException(L.T("script.js doit définir une fonction overlay(view, data).", "script.js must define a function overlay(view, data).", "script.js debe definir una función overlay(view, data).", "script.js muss eine Funktion overlay(view, data) definieren.", "script.js должен определять функцию overlay(view, data).", "script.js には overlay(view, data) 関数が必要です。", "script.js deve definire una funzione overlay(view, data).", "script.js deve definir uma função overlay(view, data).", "script.js musi definiować funkcję overlay(view, data).", "script.js moet een functie overlay(view, data) definiëren.", "script.js bir overlay(view, data) işlevi tanımlamalıdır.", "script.js는 overlay(view, data) 함수를 정의해야 합니다.", "script.js 必须定义函数 overlay(view, data)。", "script.js має визначати функцію overlay(view, data).", "script.js måste definiera en funktion overlay(view, data).", "script.js musí definovat funkci overlay(view, data)."));
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidDataException(L.T($"script.js invalide : {ex.Message}", $"Invalid script.js: {ex.Message}", $"script.js no válido: {ex.Message}", $"Ungültige script.js: {ex.Message}", $"Недопустимый script.js: {ex.Message}", $"script.js が無効です: {ex.Message}", $"script.js non valido: {ex.Message}", $"script.js inválido: {ex.Message}", $"Nieprawidłowy script.js: {ex.Message}", $"Ongeldig script.js: {ex.Message}", $"Geçersiz script.js: {ex.Message}", $"잘못된 script.js: {ex.Message}", $"script.js 无效：{ex.Message}", $"Недійсний script.js: {ex.Message}", $"Ogiltig script.js: {ex.Message}", $"Neplatný script.js: {ex.Message}"), ex);
        }
    }

    /// <summary>Runs one script on the overlay. Throws on any error, timeout or invalid result: the caller keeps the previous view.</summary>
    public static OverlayView Apply(string source, OverlayView view, string dataJson)
    {
        var engine = NewEngine();
        engine.Execute(source);
        engine.SetValue("__view", ViewToJson(view));
        engine.SetValue("__data", dataJson);
        var result = engine.Evaluate("JSON.stringify((function () { var v = JSON.parse(__view); var r = overlay(v, JSON.parse(__data)); return r === undefined ? v : r; })())");
        if (!result.IsString()) throw new InvalidDataException("overlay() doit renvoyer un objet.");
        return ViewFromJson(result.AsString(), view);
    }

    /// <summary>The statistics given to scripts (permission stats.read), as JSON. Most recent match first.</summary>
    public static string DataJson(TrackerData data, IReadOnlyList<MatchRecord> session, MatchRecord? last, bool inProgress)
    {
        static object Match(MatchRecord m, bool inSession) => new
        {
            start = m.MatchStartUtc ?? m.AnalyzedAtUtc,
            durationSeconds = m.DurationSeconds,
            playlist = m.Playlist,
            placement = m.Placement,
            totalPlayers = m.TotalPlayers,
            eliminations = m.Eliminations,
            chests = m.ChestsTotal,
            opened = m.ChestsOpened,
            destroyed = m.ChestsDestroyed,
            sprites = m.SpriteChests,
            cannons = m.LootCannons,
            wanted = m.WantedMaps?.Count,
            treasures = m.TreasureFinds.Count,
            inSession,
            chestList = m.Chests.Select(c => new { time = c.TimeSeconds, kind = L.Name(c.Kind), destroyed = c.Destroyed }),
        };

        var matches = data.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).Take(MaxMatches)
            .Select(m => Match(m, session.Contains(m))).ToList();
        return JsonSerializer.Serialize(new
        {
            player = last?.PlayerName,
            inProgress,
            language = L.Code,
            total = new { matches = data.Matches.Count, chests = data.Matches.Sum(m => m.ChestsTotal), opened = data.Matches.Sum(m => m.ChestsOpened) },
            session = new { matches = session.Count, chests = session.Sum(m => m.ChestsTotal) },
            matches,
        });
    }

    private static string ViewToJson(OverlayView v) => JsonSerializer.Serialize(new
    {
        header = v.HeaderText ?? L.T("COFFRES", "CHESTS", "COFRES", "TRUHEN", "СУНДУКИ", "宝箱", "FORZIERI", "BAÚS", "SKRZYNIE", "KISTEN", "SANDIKLAR", "상자", "宝箱", "СКРИНІ", "KISTOR", "TRUHLY"),
        total = v.Total,
        totalCaption = v.TotalCaption ?? "total",
        current = v.Current,
        currentCaption = v.CurrentCaption,
        badge = v.Badge,
        rows = v.Rows.Select(r => new { label = r.Label, value = r.Value, detail = r.Detail, color = r.Color, treasure = r.Treasure, icon = r.Icon, separator = r.Separator }),
    });

    /// <summary>Reads what the script returned, keeping only known fields with safe lengths. Missing fields keep their value.</summary>
    private static OverlayView ViewFromJson(string json, OverlayView previous)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("overlay() doit renvoyer un objet.");

        string? Text(JsonElement parent, string name, int max, string? fallback)
        {
            if (!parent.TryGetProperty(name, out var e)) return fallback;
            return e.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => Clean(e.GetString()!, max),
                JsonValueKind.Number => Clean(e.GetRawText(), max),
                _ => throw new InvalidDataException($"« {name} » doit être un texte."),
            };
        }

        var rows = previous.Rows;
        if (root.TryGetProperty("rows", out var rowsElement))
        {
            if (rowsElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("« rows » doit être une liste.");
            if (rowsElement.GetArrayLength() > MaxRows) throw new InvalidDataException($"{MaxRows} lignes au maximum.");
            var list = new List<OverlayRow>();
            foreach (var r in rowsElement.EnumerateArray())
            {
                if (r.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Chaque ligne doit être un objet.");
                var color = Text(r, "color", 7, null);
                if (color != null && !ColorPattern.IsMatch(color)) throw new InvalidDataException($"Couleur invalide : {color} (#RRGGBB).");
                var icon = Text(r, "icon", 36, null);
                if (icon != null && !AddonRules.IsIconName(icon)) icon = null;
                list.Add(new OverlayRow(
                    Text(r, "label", 24, "") ?? "",
                    Text(r, "value", 32, "") ?? "",
                    r.TryGetProperty("treasure", out var t) && t.ValueKind == JsonValueKind.True,
                    Text(r, "detail", 48, null),
                    color,
                    icon,
                    r.TryGetProperty("separator", out var sep) && sep.ValueKind == JsonValueKind.True));
            }
            rows = list;
        }

        return previous with
        {
            HeaderText = Text(root, "header", 24, previous.HeaderText),
            Total = Text(root, "total", 16, previous.Total) ?? "",
            TotalCaption = Text(root, "totalCaption", 24, previous.TotalCaption),
            Current = Text(root, "current", 16, previous.Current),
            CurrentCaption = Text(root, "currentCaption", 24, previous.CurrentCaption) ?? "",
            Badge = Text(root, "badge", 32, previous.Badge),
            Rows = rows,
        };
    }

    private static string Clean(string value, int max)
    {
        var text = new string(value.Where(c => !char.IsControl(c)).ToArray());
        return text.Length <= max ? text : text[..max];
    }
}

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChestCounter;

// An add-on is a manifest.json that declares extra overlay counters and/or overlay colours, which TrackChest reads and
// validates itself, plus, for a script add-on (permission overlay.script), a script.js run in a sandbox (AddonScript):
// no .NET, file or network access, only JSON in and out, with time and memory limits. What an add-on may touch is
// limited by its declared permissions, which the player sees before installing.

/// <summary>Where an add-on comes from: verified = reviewed and signed catalog, community = catalog, local = a file picked by hand.</summary>
public enum AddonTier { Verified, Community, Local }

/// <summary>One extra line of the overlay, computed from the saved matches.</summary>
public sealed class AddonCounter
{
    public string Label { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Scope { get; set; } = "total";
    /// <summary>Adds "· n dernière partie" after a total.</summary>
    public bool ShowLast { get; set; }
    /// <summary>Optional small icon drawn to the left of the label (e.g. "break_chest.png", "Esprit_Chest.png", "Wanted_Card.png", "Loot_Cannons.png").</summary>
    public string? Icon { get; set; }
}

/// <summary>Overlay colours as "#RRGGBB". Missing ones keep the default look.</summary>
public sealed class AddonTheme
{
    public string? Background { get; set; }
    public string? Border { get; set; }
    public string? Line { get; set; }
    public string? Muted { get; set; }
    public string? Text { get; set; }
    public string? Accent { get; set; }
    public string? Treasure { get; set; }

    public bool IsEmpty => Background == null && Border == null && Line == null && Muted == null
        && Text == null && Accent == null && Treasure == null;

    public OverlayTheme ToOverlayTheme()
    {
        var d = OverlayTheme.Default;
        uint Pick(string? hex, uint fallback) => hex == null ? fallback : OverlayTheme.FromHex(hex);
        return new OverlayTheme(Pick(Background, d.Background), Pick(Border, d.Border), Pick(Line, d.Line),
            Pick(Muted, d.Muted), Pick(Text, d.Text), Pick(Accent, d.Accent), Pick(Treasure, d.Treasure));
    }
}

public sealed class AddonManifest
{
    public int Schema { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>Old field, no longer part of the format: still read (so older packages stay valid) but never shown.</summary>
    public string? Author { get; set; }
    public string Description { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    public List<AddonCounter> Counters { get; set; } = new();
    public AddonTheme? Theme { get; set; }

    /// <summary>Source of script.js (permission overlay.script), read from the package; never part of manifest.json.</summary>
    [JsonIgnore]
    public string? Script { get; set; }

    /// <summary>PNG icon files bundled in the add-on package (filename -> bytes).</summary>
    [JsonIgnore]
    public Dictionary<string, byte[]> Icons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasScript => Permissions.Contains(AddonRules.OverlayScript);
}

public static class AddonRules
{
    public const string StatsRead = "stats.read", OverlayRows = "overlay.rows", OverlayTheme = "overlay.theme", OverlayScript = "overlay.script";
    public static readonly string[] AllPermissions = [StatsRead, OverlayRows, OverlayTheme, OverlayScript];
    public static readonly string[] Metrics = ["chests", "opened", "destroyed", "sprites", "buried", "wanted", "cannons", "treasures", "matches"];
    public static readonly string[] Scopes = ["total", "last", "session"];
    public const int MaxCounters = 8, MaxZipBytes = 64 * 1024, MaxManifestBytes = 16 * 1024, MaxIconBytes = 32 * 1024;

    private static readonly Regex IdPattern = new(@"^[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new(@"^\d{1,4}\.\d{1,4}\.\d{1,4}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ColorPattern = new(@"^#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant);
    private static readonly Regex HexPattern = new(@"^[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex IconPattern = new(@"^[A-Za-z0-9_-]{1,32}(\.png)?\z", RegexOptions.CultureInvariant);

    public static bool IsId(string? s) => s is { Length: >= 3 and <= 40 } && IdPattern.IsMatch(s);
    public static bool IsVersion(string? s) => s != null && VersionPattern.IsMatch(s);
    public static bool IsSha256(string? s) => s != null && HexPattern.IsMatch(s);
    public static bool IsIconName(string? s) => s != null && IconPattern.IsMatch(s);
    public static bool IsIconFile(string? s) => s != null && s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && IconPattern.IsMatch(s);
    public static bool IsText(string? s, int min, int max) => s != null && s.Length >= min && s.Length <= max && !s.Any(char.IsControl);

    /// <summary>All the problems of a manifest (empty = valid).</summary>
    public static List<string> Validate(AddonManifest m)
    {
        var errors = new List<string>();
        if (m.Schema != 1) errors.Add("schema doit valoir 1.");
        if (!IsId(m.Id)) errors.Add("id invalide (3 à 40 caractères : minuscules, chiffres et tirets).");
        if (!IsText(m.Name, 1, 40)) errors.Add("name invalide (1 à 40 caractères).");
        if (!IsVersion(m.Version)) errors.Add("version invalide (format 1.0.0).");
        if (!IsText(m.Description, 0, 200)) errors.Add("description invalide (200 caractères maximum).");

        var permissions = m.Permissions ?? new();
        if (permissions.Any(p => !AllPermissions.Contains(p))) errors.Add("permission inconnue.");
        if (permissions.Distinct().Count() != permissions.Count) errors.Add("permission en double.");

        var counters = m.Counters ?? new();
        if (counters.Count > MaxCounters) errors.Add($"{MaxCounters} compteurs au maximum.");
        foreach (var c in counters)
        {
            if (!IsText(c.Label, 1, 24)) errors.Add("label de compteur invalide (1 à 24 caractères).");
            if (!Metrics.Contains(c.Metric)) errors.Add($"metric inconnue : {c.Metric}.");
            if (!Scopes.Contains(c.Scope)) errors.Add($"scope inconnu : {c.Scope}.");
            if (c.Icon != null && !IsIconName(c.Icon)) errors.Add($"icône de compteur invalide : {c.Icon}.");
        }
        if (counters.Count > 0 && !(permissions.Contains(StatsRead) && permissions.Contains(OverlayRows)))
            errors.Add("les compteurs demandent les permissions stats.read et overlay.rows.");

        if (m.Theme != null)
        {
            if (!permissions.Contains(OverlayTheme)) errors.Add("le thème demande la permission overlay.theme.");
            foreach (var color in new[] { m.Theme.Background, m.Theme.Border, m.Theme.Line, m.Theme.Muted, m.Theme.Text, m.Theme.Accent, m.Theme.Treasure })
            {
                if (color != null && !ColorPattern.IsMatch(color)) errors.Add($"couleur invalide : {color} (format #RRGGBB).");
            }
            if (m.Theme.IsEmpty) errors.Add("le thème est vide.");
        }
        var script = permissions.Contains(OverlayScript);
        if (script && !permissions.Contains(StatsRead)) errors.Add("le script demande aussi la permission stats.read.");
        if (counters.Count == 0 && m.Theme == null && !script) errors.Add("l’add-on ne contient ni compteur, ni thème, ni script.");
        if (permissions.Contains(StatsRead) && counters.Count == 0 && !script) errors.Add("permission stats.read sans compteur ni script.");
        if (permissions.Contains(OverlayRows) && counters.Count == 0) errors.Add("permission overlay.rows sans compteur.");
        if (permissions.Contains(OverlayTheme) && m.Theme == null) errors.Add("permission overlay.theme sans thème.");
        return errors;
    }

    /// <summary>What a permission lets the add-on do, in the player's language.</summary>
    public static string Describe(string permission) => permission switch
    {
        StatsRead => L.T("Lire tes statistiques (coffres, parties, trésors)", "Read your statistics (chests, matches, treasures)", "Leer tus estadísticas (cofres, partidas, tesoros)", "Deine Statistiken lesen (Truhen, Matches, Schätze)", "Читать вашу статистику (сундуки, матчи, сокровища)", "統計を読み取る（宝箱、試合、宝）", "Leggere le tue statistiche (forzieri, partite, tesori)", "Ler suas estatísticas (baús, partidas, tesouros)", "Odczyt twoich statystyk (skrzynie, mecze, skarby)", "Je statistieken lezen (kisten, potjes, schatten)", "İstatistiklerini okuma (sandıklar, maçlar, hazineler)", "통계 읽기 (상자, 경기, 보물)", "读取你的统计数据（宝箱、比赛、宝藏）", "Читати вашу статистику (скрині, матчі, скарби)", "Läsa din statistik (kistor, matcher, skatter)", "Číst tvé statistiky (truhly, zápasy, poklady)"),
        OverlayRows => L.T("Ajouter des lignes à l’overlay", "Add lines to the overlay", "Añadir líneas al overlay", "Zeilen zum Overlay hinzufügen", "Добавлять строки в оверлей", "オーバーレイに行を追加する", "Aggiungere righe all'overlay", "Adicionar linhas à sobreposição", "Dodawanie wierszy do nakładki", "Regels aan de overlay toevoegen", "Kaplamaya satır ekleme", "오버레이에 줄 추가", "向叠加层添加行", "Додавати рядки до оверлею", "Lägga till rader i överlägget", "Přidávat řádky do překryvu"),
        OverlayTheme => L.T("Changer les couleurs de l’overlay", "Change the overlay colours", "Cambiar los colores del overlay", "Die Farben des Overlays ändern", "Менять цвета оверлея", "オーバーレイの色を変更する", "Cambiare i colori dell'overlay", "Mudar as cores da sobreposição", "Zmiana kolorów nakładki", "De kleuren van de overlay wijzigen", "Kaplamanın renklerini değiştirme", "오버레이 색상 변경", "更改叠加层颜色", "Змінювати кольори оверлею", "Ändra överläggets färger", "Měnit barvy překryvu"),
        OverlayScript => L.T("Exécuter un script qui peut modifier, ajouter ou supprimer tout ce qu’affiche l’overlay (bac à sable : pas d’accès aux fichiers ni à Internet)",
            "Run a script that can change, add or remove anything shown in the overlay (sandbox: no file or Internet access)",
            "Ejecutar un script que puede cambiar, añadir o quitar todo lo que muestra el overlay (sin acceso a archivos ni a Internet)",
            "Ein Skript ausführen, das alles im Overlay ändern, hinzufügen oder entfernen kann (Sandbox: kein Datei- oder Internetzugriff)",
            "Запускать скрипт, который может изменять, добавлять или удалять всё, что показывает оверлей (песочница: без доступа к файлам и Интернету)",
            "オーバーレイの表示内容をすべて変更・追加・削除できるスクリプトを実行する（サンドボックス: ファイルやインターネットにはアクセス不可）",
            "Eseguire uno script che può modificare, aggiungere o rimuovere tutto ciò che mostra l'overlay (sandbox: nessun accesso a file o Internet)",
            "Executar um script que pode alterar, adicionar ou remover tudo o que a sobreposição mostra (sandbox: sem acesso a arquivos nem à Internet)",
            "Uruchamianie skryptu, który może zmienić, dodać lub usunąć wszystko, co pokazuje nakładka (piaskownica: bez dostępu do plików i Internetu)",
            "Een script uitvoeren dat alles in de overlay kan wijzigen, toevoegen of verwijderen (sandbox: geen toegang tot bestanden of internet)",
            "Kaplamada gösterilen her şeyi değiştirebilen, ekleyebilen veya kaldırabilen bir betik çalıştırma (korumalı alan: dosya ve İnternet erişimi yok)",
            "오버레이에 표시되는 모든 것을 변경, 추가 또는 제거할 수 있는 스크립트 실행 (샌드박스: 파일 및 인터넷 접근 불가)",
            "运行可以更改、添加或删除叠加层显示内容的脚本（沙盒：无法访问文件或互联网）",
            "Запускати скрипт, який може змінити, додати або прибрати все, що показує оверлей (пісочниця: без доступу до файлів та Інтернету)",
            "Köra ett skript som kan ändra, lägga till eller ta bort allt som överlägget visar (sandlåda: ingen åtkomst till filer eller internet)",
            "Spouštět skript, který může změnit, přidat nebo odebrat cokoli, co překryv zobrazuje (sandbox: bez přístupu k souborům a internetu)"),
        _ => permission,
    };
}

internal static class AddonJson
{
    /// <summary>Strict: a key that is not part of the format is an error, not silently ignored.</summary>
    public static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// <summary>An add-on file: a small .zip that holds exactly one manifest.json.</summary>
public static class AddonPackage
{
    /// <summary>Reads and fully validates a package. Throws InvalidDataException (message for the player) otherwise.</summary>
    public static AddonManifest Read(byte[] zip)
    {
        if (zip.Length is 0 or > AddonRules.MaxZipBytes)
            throw new InvalidDataException(L.T("Fichier d’add-on vide ou trop gros.", "Add-on file is empty or too large.", "Archivo de add-on vacío o demasiado grande.", "Add-on-Datei ist leer oder zu groß.", "Файл дополнения пуст или слишком велик.", "アドオンファイルが空か、大きすぎます。", "File del componente aggiuntivo vuoto o troppo grande.", "Arquivo do complemento vazio ou grande demais.", "Plik dodatku jest pusty lub za duży.", "Add-onbestand is leeg of te groot.", "Eklenti dosyası boş veya çok büyük.", "애드온 파일이 비어 있거나 너무 큽니다.", "插件文件为空或过大。", "Файл доповнення порожній або завеликий.", "Tilläggsfilen är tom eller för stor.", "Soubor doplňku je prázdný nebo příliš velký."));

        byte[] manifestBytes;
        byte[]? scriptBytes = null;
        var icons = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            var allowed = names.Count is >= 1 and <= 10 && names.Contains("manifest.json")
                && names.All(n => n is "manifest.json" or AddonScript.FileName || AddonRules.IsIconFile(n));
            if (!allowed)
                throw new InvalidDataException(L.T("Un add-on contient manifest.json, ses icônes .png et script.js s’il a un script : aucun autre fichier.", "An add-on holds manifest.json, its .png icons, plus script.js for a script add-on: no other file.", "Un add-on contiene manifest.json, sus iconos .png y, si tiene script, script.js: ningún otro archivo.", "Ein Add-on enthält manifest.json, seine .png-Symbole und bei Skript-Add-ons script.js: keine anderen Dateien.", "Дополнение содержит manifest.json, значки .png и, если есть скрипт, script.js: никаких других файлов.", "アドオンに含めるのは manifest.json、.png アイコン、および（スクリプトがある場合）script.js のみです。", "Un componente aggiuntivo contiene manifest.json, le icone .png, più script.js per un componente con script: nessun altro file.", "Um complemento contém manifest.json, seus ícones .png, mais script.js para um complemento com script: nenhum outro arquivo.", "Dodatek zawiera manifest.json, ikony .png oraz script.js w przypadku dodatku ze skryptem: żadnych innych plików.", "Een add-on bevat manifest.json, .png-pictogrammen, plus script.js voor een script-add-on: geen andere bestanden.", "Bir eklenti manifest.json, .png simgeleri, betikli eklentide ayrıca script.js içerir: başka dosya yok.", "애드온에는 manifest.json, .png 아이콘과, 스크립트 애드온의 경우 script.js만 들어 있습니다: 다른 파일은 없습니다.", "插件包含 manifest.json、.png 图标，脚本插件另含 script.js：不能有其他文件。", "Доповнення містить manifest.json, значки .png, а для доповнення зі скриптом ще й script.js: жодних інших файлів.", "Ett tillägg innehåller manifest.json, .png-ikoner, plus script.js för ett skripttillägg: inga andra filer.", "Doplněk obsahuje manifest.json, ikony .png a u skriptového doplňku také script.js: žádné jiné soubory."));
            manifestBytes = ReadEntry(archive.GetEntry("manifest.json")!, AddonRules.MaxManifestBytes, "manifest.json");
            if (archive.GetEntry(AddonScript.FileName) is { } scriptEntry)
                scriptBytes = ReadEntry(scriptEntry, AddonScript.MaxScriptBytes, AddonScript.FileName);
            foreach (var iconEntry in archive.Entries.Where(e => AddonRules.IsIconFile(e.FullName)))
                icons[iconEntry.FullName] = ReadEntry(iconEntry, AddonRules.MaxIconBytes, iconEntry.FullName);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            throw new InvalidDataException(L.T("Fichier d’add-on illisible.", "Unreadable add-on file.", "Archivo de add-on ilegible.", "Add-on-Datei nicht lesbar.", "Не удаётся прочитать файл дополнения.", "アドオンファイルを読み取れません。", "File del componente aggiuntivo illeggibile.", "Arquivo do complemento ilegível.", "Nieczytelny plik dodatku.", "Onleesbaar add-onbestand.", "Okunamayan eklenti dosyası.", "읽을 수 없는 애드온 파일입니다.", "无法读取插件文件。", "Файл доповнення неможливо прочитати.", "Oläslig tilläggsfil.", "Nečitelný soubor doplňku."), ex);
        }

        var manifest = ParseManifest(manifestBytes);
        manifest.Icons = icons;
        if (manifest.HasScript != (scriptBytes != null))
            throw new InvalidDataException(manifest.HasScript
                ? L.T("La permission overlay.script demande un fichier script.js.", "The overlay.script permission needs a script.js file.", "El permiso overlay.script necesita un archivo script.js.", "Die Berechtigung overlay.script benötigt eine Datei script.js.", "Разрешению overlay.script нужен файл script.js.", "overlay.script 権限には script.js ファイルが必要です。", "Il permesso overlay.script richiede un file script.js.", "A permissão overlay.script exige um arquivo script.js.", "Uprawnienie overlay.script wymaga pliku script.js.", "De machtiging overlay.script vereist een bestand script.js.", "overlay.script izni bir script.js dosyası gerektirir.", "overlay.script 권한에는 script.js 파일이 필요합니다.", "overlay.script 权限需要 script.js 文件。", "Дозвіл overlay.script потребує файлу script.js.", "Behörigheten overlay.script kräver en script.js-fil.", "Oprávnění overlay.script vyžaduje soubor script.js.")
                : L.T("script.js demande la permission overlay.script.", "script.js needs the overlay.script permission.", "script.js necesita el permiso overlay.script.", "script.js benötigt die Berechtigung overlay.script.", "script.js требует разрешения overlay.script.", "script.js には overlay.script 権限が必要です。", "script.js richiede il permesso overlay.script.", "script.js exige a permissão overlay.script.", "script.js wymaga uprawnienia overlay.script.", "script.js vereist de machtiging overlay.script.", "script.js, overlay.script iznini gerektirir.", "script.js에는 overlay.script 권한이 필요합니다.", "script.js 需要 overlay.script 权限。", "script.js потребує дозволу overlay.script.", "script.js kräver behörigheten overlay.script.", "script.js vyžaduje oprávnění overlay.script."));
        if (scriptBytes != null)
        {
            if (scriptBytes.Length >= 3 && scriptBytes[0] == 0xEF && scriptBytes[1] == 0xBB && scriptBytes[2] == 0xBF)
                throw new InvalidDataException("script.js ne doit pas avoir de BOM (UTF-8 sans BOM).");
            string source;
            try { source = new System.Text.UTF8Encoding(false, true).GetString(scriptBytes); }
            catch (System.Text.DecoderFallbackException ex) { throw new InvalidDataException("script.js doit être en UTF-8.", ex); }
            AddonScript.Check(source);
            manifest.Script = source;
        }
        return manifest;
    }

    /// <summary>Reads a zip entry, stopping as soon as it goes over the limit (zip bombs).</summary>
    private static byte[] ReadEntry(ZipArchiveEntry entry, int limit, string name)
    {
        var tooBig = L.T($"{name} est trop gros.", $"{name} is too large.", $"{name} es demasiado grande.", $"{name} ist zu groß.", $"{name} слишком велик.", $"{name} が大きすぎます。", $"{name} è troppo grande.", $"{name} é grande demais.", $"{name} jest za duży.", $"{name} is te groot.", $"{name} çok büyük.", $"{name}이(가) 너무 큽니다.", $"{name} 过大。", $"{name} завеликий.", $"{name} är för stor.", $"{name} je příliš velký.");
        if (entry.Length > limit) throw new InvalidDataException(tooBig);
        using var input = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit) throw new InvalidDataException(tooBig);
        }
        return buffer.ToArray();
    }

    public static AddonManifest ParseManifest(byte[] json)
    {
        AddonManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AddonManifest>(json, AddonJson.Strict);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(L.T($"manifest.json invalide : {ex.Message}", $"Invalid manifest.json: {ex.Message}", $"manifest.json no válido: {ex.Message}", $"Ungültige manifest.json: {ex.Message}", $"Недопустимый manifest.json: {ex.Message}", $"manifest.json が無効です: {ex.Message}", $"manifest.json non valido: {ex.Message}", $"manifest.json inválido: {ex.Message}", $"Nieprawidłowy manifest.json: {ex.Message}", $"Ongeldig manifest.json: {ex.Message}", $"Geçersiz manifest.json: {ex.Message}", $"잘못된 manifest.json: {ex.Message}", $"manifest.json 无效：{ex.Message}", $"Недійсний manifest.json: {ex.Message}", $"Ogiltig manifest.json: {ex.Message}", $"Neplatný manifest.json: {ex.Message}"), ex);
        }
        if (manifest == null) throw new InvalidDataException("manifest.json vide.");
        var errors = AddonRules.Validate(manifest);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        return manifest;
    }
}

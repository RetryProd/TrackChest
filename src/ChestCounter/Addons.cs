using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChestCounter;

// Add-ons are DATA, never code: a manifest.json that declares extra overlay counters and/or overlay colours.
// TrackChest reads and validates it itself, so an add-on cannot run anything on the PC. What an add-on may touch
// is limited by its declared permissions, which the player sees before installing.

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
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    public List<AddonCounter> Counters { get; set; } = new();
    public AddonTheme? Theme { get; set; }
}

public static class AddonRules
{
    public const string StatsRead = "stats.read", OverlayRows = "overlay.rows", OverlayTheme = "overlay.theme";
    public static readonly string[] AllPermissions = [StatsRead, OverlayRows, OverlayTheme];
    public static readonly string[] Metrics = ["chests", "opened", "destroyed", "sprites", "wanted", "cannons", "treasures", "matches"];
    public static readonly string[] Scopes = ["total", "last", "session"];
    public const int MaxCounters = 8, MaxZipBytes = 64 * 1024, MaxManifestBytes = 16 * 1024;

    private static readonly Regex IdPattern = new(@"^[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new(@"^\d{1,4}\.\d{1,4}\.\d{1,4}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ColorPattern = new(@"^#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant);
    private static readonly Regex HexPattern = new(@"^[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant);

    public static bool IsId(string? s) => s is { Length: >= 3 and <= 40 } && IdPattern.IsMatch(s);
    public static bool IsVersion(string? s) => s != null && VersionPattern.IsMatch(s);
    public static bool IsSha256(string? s) => s != null && HexPattern.IsMatch(s);
    public static bool IsText(string? s, int min, int max) => s != null && s.Length >= min && s.Length <= max && !s.Any(char.IsControl);

    /// <summary>All the problems of a manifest (empty = valid).</summary>
    public static List<string> Validate(AddonManifest m)
    {
        var errors = new List<string>();
        if (m.Schema != 1) errors.Add("schema doit valoir 1.");
        if (!IsId(m.Id)) errors.Add("id invalide (3 à 40 caractères : minuscules, chiffres et tirets).");
        if (!IsText(m.Name, 1, 40)) errors.Add("name invalide (1 à 40 caractères).");
        if (!IsVersion(m.Version)) errors.Add("version invalide (format 1.0.0).");
        if (!IsText(m.Author, 1, 40)) errors.Add("author invalide (1 à 40 caractères).");
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
        if (counters.Count == 0 && m.Theme == null) errors.Add("l’add-on ne contient ni compteur ni thème.");
        if (permissions.Contains(StatsRead) && counters.Count == 0) errors.Add("permission stats.read sans compteur.");
        if (permissions.Contains(OverlayRows) && counters.Count == 0) errors.Add("permission overlay.rows sans compteur.");
        if (permissions.Contains(OverlayTheme) && m.Theme == null) errors.Add("permission overlay.theme sans thème.");
        return errors;
    }

    /// <summary>What a permission lets the add-on do, in the player's language.</summary>
    public static string Describe(string permission) => permission switch
    {
        StatsRead => L.T("Lire tes statistiques (coffres, parties, trésors)", "Read your statistics (chests, matches, treasures)", "Leer tus estadísticas (cofres, partidas, tesoros)"),
        OverlayRows => L.T("Ajouter des lignes à l’overlay", "Add lines to the overlay", "Añadir líneas al overlay"),
        OverlayTheme => L.T("Changer les couleurs de l’overlay", "Change the overlay colours", "Cambiar los colores del overlay"),
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
            throw new InvalidDataException(L.T("Fichier d’add-on vide ou trop gros.", "Add-on file is empty or too large.", "Archivo de add-on vacío o demasiado grande."));

        byte[] manifestBytes;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            if (archive.Entries.Count != 1 || archive.Entries[0].FullName != "manifest.json")
                throw new InvalidDataException(L.T("Un add-on ne contient que manifest.json (pas de code, pas d’autres fichiers).", "An add-on holds only manifest.json (no code, no other files).", "Un add-on solo contiene manifest.json (sin código ni otros archivos)."));
            var entry = archive.Entries[0];
            if (entry.Length > AddonRules.MaxManifestBytes)
                throw new InvalidDataException(L.T("manifest.json est trop gros.", "manifest.json is too large.", "manifest.json es demasiado grande."));

            using var input = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > AddonRules.MaxManifestBytes)
                    throw new InvalidDataException(L.T("manifest.json est trop gros.", "manifest.json is too large.", "manifest.json es demasiado grande."));
            }
            manifestBytes = buffer.ToArray();
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            throw new InvalidDataException(L.T("Fichier d’add-on illisible.", "Unreadable add-on file.", "Archivo de add-on ilegible."), ex);
        }

        return ParseManifest(manifestBytes);
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
            throw new InvalidDataException(L.T($"manifest.json invalide : {ex.Message}", $"Invalid manifest.json: {ex.Message}", $"manifest.json no válido: {ex.Message}"), ex);
        }
        if (manifest == null) throw new InvalidDataException("manifest.json vide.");
        var errors = AddonRules.Validate(manifest);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        return manifest;
    }
}

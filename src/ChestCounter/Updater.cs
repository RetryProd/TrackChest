using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChestCounter;

/// <summary>A newer version announced by the signed update.json of the latest GitHub release.</summary>
internal sealed record UpdateInfo(Version Version, string Notes, string Sha256, long Size);

/// <summary>
/// Automatic update through GitHub Releases. Each release holds update.json (version, SHA-256 and size of the .msi),
/// update.json.sig (ECDSA signature made with the same key as the add-on catalog, checked with the key built into the
/// exe) and the .msi. The address of the .msi is built here from the signed version, never read from a file, and the
/// download is checked against the signed size and SHA-256: a modified release is refused.
/// </summary>
internal static class Updater
{
    internal const string Repository = "RetryProd/TrackChest";
    private const string LatestBase = "https://github.com/" + Repository + "/releases/latest/download/";
    private const long MaxMsiBytes = 200L * 1024 * 1024;

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 4 })
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "TrackChest-updater" } },
    };

    public static Version Current { get; } = ReadCurrent();

    private static Version ReadCurrent()
    {
        var text = typeof(Updater).Assembly.GetName().Version;
        return text == null ? new Version(0, 0, 0) : new Version(text.Major, text.Minor, Math.Max(text.Build, 0));
    }

    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Current.Build}";

    /// <summary>The newer release, or null when this version is the latest. Throws when the check cannot be trusted.</summary>
    public static async Task<UpdateInfo?> CheckAsync(Version? pretendCurrent = null, CancellationToken cancel = default)
    {
        if (!AddonTrust.IsConfigured) throw new InvalidOperationException(L.T("Mises à jour désactivées : la clé de signature n’est pas intégrée à cette version.", "Updates disabled: the signing key is not built into this version.", "Actualizaciones desactivadas: la clave de firma no está incluida en esta versión.", "Updates deaktiviert: Der Signaturschlüssel ist in dieser Version nicht enthalten.", "Обновления отключены: ключ подписи не встроен в эту версию.", "アップデートは無効です: このバージョンには署名鍵が含まれていません。"));
        var json = await GetBytesAsync(LatestBase + "update.json", 16 * 1024, cancel);
        var signature = System.Text.Encoding.ASCII.GetString(await GetBytesAsync(LatestBase + "update.json.sig", 1024, cancel));
        if (!AddonTrust.Verify(json, signature)) throw new InvalidOperationException(L.T("La signature de la mise à jour est invalide : elle est ignorée.", "The update signature is invalid: it is ignored.", "La firma de la actualización no es válida: se ignora.", "Die Signatur des Updates ist ungültig: Es wird ignoriert.", "Подпись обновления недействительна: оно пропущено.", "アップデートの署名が無効なため、無視されます。"));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!Version.TryParse(root.GetProperty("version").GetString(), out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidOperationException(L.T("Version de mise à jour illisible.", "Unreadable update version.", "Versión de actualización ilegible.", "Update-Version nicht lesbar.", "Не удаётся прочитать версию обновления.", "アップデートのバージョンを読み取れません。"));
        var sha = root.GetProperty("sha256").GetString() ?? "";
        var size = root.GetProperty("size").GetInt64();
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit) || size <= 0 || size > MaxMsiBytes)
            throw new InvalidOperationException(L.T("Informations de mise à jour invalides.", "Invalid update information.", "Información de actualización no válida.", "Ungültige Update-Informationen.", "Недопустимые сведения об обновлении.", "アップデート情報が無効です。"));
        if (version <= (pretendCurrent ?? Current)) return null;
        var notes = root.TryGetProperty("notes", out var n) ? (n.GetString() ?? "") : "";
        return new UpdateInfo(version, notes.Length > 600 ? notes[..600] : notes, sha.ToUpperInvariant(), size);
    }

    /// <summary>Downloads the .msi, checks size and SHA-256, and returns its path.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, CancellationToken cancel = default)
    {
        var v = $"{update.Version.Major}.{update.Version.Minor}.{update.Version.Build}";
        var url = $"https://github.com/{Repository}/releases/download/v{v}/TrackChest-{v}.msi";
        var bytes = await GetBytesAsync(url, update.Size, cancel);
        if (bytes.Length != update.Size || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(update.Sha256)))
            throw new InvalidOperationException(L.T("Le fichier téléchargé ne correspond pas à la version annoncée : il est supprimé.", "The downloaded file does not match the announced version: it is deleted.", "El archivo descargado no coincide con la versión anunciada: se elimina.", "Die heruntergeladene Datei passt nicht zur angekündigten Version: Sie wird gelöscht.", "Загруженный файл не соответствует заявленной версии: он удалён.", "ダウンロードしたファイルが告知されたバージョンと一致しないため、削除されました。"));
        var folder = Path.Combine(Path.GetTempPath(), "TrackChest-update");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"TrackChest-{v}.msi");
        await File.WriteAllBytesAsync(path, bytes, cancel);
        return path;
    }

    /// <summary>
    /// Starts the installer (per user, no administrator rights), which replaces the files and reopens TrackChest.
    /// The command line is passed to cmd.exe as is: ArgumentList would escape the inner quotes as \" , which cmd does
    /// not understand, and msiexec then received a broken path and installed nothing (bug of 1.1.0 to 1.1.2).
    /// A short pause lets this instance exit before its files are replaced; the log helps when an update fails.
    /// </summary>
    public static void Install(string msiPath)
    {
        var exe = InstalledExe;
        var log = Path.Combine(Path.GetDirectoryName(msiPath)!, "install.log");
        var command = $"ping -n 3 127.0.0.1 >nul & msiexec /i \"{msiPath}\" /passive /norestart /l*v \"{log}\" && start \"\" \"{exe}\"";
        var start = new ProcessStartInfo("cmd.exe", $"/d /c \"{command}\"") { UseShellExecute = false, CreateNoWindow = true };
        Process.Start(start);
    }

    /// <summary>Where the .msi installs TrackChest (per user).</summary>
    public static string InstalledExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TrackChest", "TrackChest.exe");

    private static async Task<byte[]> GetBytesAsync(string url, long max, CancellationToken cancel)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException(L.T("Redirection non sécurisée refusée.", "Insecure redirect refused.", "Redirección no segura rechazada.", "Unsichere Weiterleitung abgelehnt.", "Небезопасное перенаправление отклонено.", "安全でないリダイレクトを拒否しました。"));
        if (response.Content.Headers.ContentLength is { } length && length > max) throw new InvalidOperationException("Fichier trop gros.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel)) > 0)
        {
            if (buffer.Length + read > max) throw new InvalidOperationException("Fichier trop gros.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

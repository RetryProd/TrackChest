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
        if (!AddonTrust.IsConfigured) throw new InvalidOperationException(L.T("Mises à jour désactivées : la clé de signature n’est pas intégrée à cette version.", "Updates disabled: the signing key is not built into this version.", "Actualizaciones desactivadas: la clave de firma no está incluida en esta versión.", "Updates deaktiviert: Der Signaturschlüssel ist in dieser Version nicht enthalten.", "Обновления отключены: ключ подписи не встроен в эту версию.", "アップデートは無効です: このバージョンには署名鍵が含まれていません。", "Aggiornamenti disattivati: la chiave di firma non è inclusa in questa versione.", "Atualizações desativadas: a chave de assinatura não está incluída nesta versão.", "Aktualizacje wyłączone: klucz podpisu nie jest wbudowany w tę wersję.", "Updates uitgeschakeld: de ondertekeningssleutel zit niet in deze versie.", "Güncellemeler devre dışı: imzalama anahtarı bu sürüme dahil değil.", "업데이트 비활성화: 이 버전에는 서명 키가 포함되어 있지 않습니다.", "更新已停用：此版本未内置签名密钥。", "Оновлення вимкнено: ключ підпису не вбудовано в цю версію.", "Uppdateringar inaktiverade: signeringsnyckeln är inte inbyggd i den här versionen.", "Aktualizace vypnuty: podpisový klíč není součástí této verze."));
        var json = await GetBytesAsync(LatestBase + "update.json", 16 * 1024, cancel);
        var signature = System.Text.Encoding.ASCII.GetString(await GetBytesAsync(LatestBase + "update.json.sig", 1024, cancel));
        if (!AddonTrust.Verify(json, signature)) throw new InvalidOperationException(L.T("La signature de la mise à jour est invalide : elle est ignorée.", "The update signature is invalid: it is ignored.", "La firma de la actualización no es válida: se ignora.", "Die Signatur des Updates ist ungültig: Es wird ignoriert.", "Подпись обновления недействительна: оно пропущено.", "アップデートの署名が無効なため、無視されます。", "La firma dell'aggiornamento non è valida: viene ignorata.", "A assinatura da atualização é inválida: ela é ignorada.", "Podpis aktualizacji jest nieprawidłowy: zostaje zignorowana.", "De handtekening van de update is ongeldig: hij wordt genegeerd.", "Güncelleme imzası geçersiz: yok sayılıyor.", "업데이트 서명이 잘못되었습니다: 무시합니다.", "更新签名无效：已忽略。", "Підпис оновлення недійсний: його проігноровано.", "Uppdateringens signatur är ogiltig: den ignoreras.", "Podpis aktualizace je neplatný: ignoruje se."));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!Version.TryParse(root.GetProperty("version").GetString(), out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidOperationException(L.T("Version de mise à jour illisible.", "Unreadable update version.", "Versión de actualización ilegible.", "Update-Version nicht lesbar.", "Не удаётся прочитать версию обновления.", "アップデートのバージョンを読み取れません。", "Versione dell'aggiornamento illeggibile.", "Versão da atualização ilegível.", "Nieczytelna wersja aktualizacji.", "Onleesbare updateversie.", "Okunamayan güncelleme sürümü.", "읽을 수 없는 업데이트 버전입니다.", "无法读取更新版本。", "Версію оновлення неможливо прочитати.", "Oläslig uppdateringsversion.", "Nečitelná verze aktualizace."));
        var sha = root.GetProperty("sha256").GetString() ?? "";
        var size = root.GetProperty("size").GetInt64();
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit) || size <= 0 || size > MaxMsiBytes)
            throw new InvalidOperationException(L.T("Informations de mise à jour invalides.", "Invalid update information.", "Información de actualización no válida.", "Ungültige Update-Informationen.", "Недопустимые сведения об обновлении.", "アップデート情報が無効です。", "Informazioni di aggiornamento non valide.", "Informações de atualização inválidas.", "Nieprawidłowe informacje o aktualizacji.", "Ongeldige update-informatie.", "Geçersiz güncelleme bilgisi.", "잘못된 업데이트 정보입니다.", "更新信息无效。", "Недійсні дані оновлення.", "Ogiltig uppdateringsinformation.", "Neplatné informace o aktualizaci."));
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
            throw new InvalidOperationException(L.T("Le fichier téléchargé ne correspond pas à la version annoncée : il est supprimé.", "The downloaded file does not match the announced version: it is deleted.", "El archivo descargado no coincide con la versión anunciada: se elimina.", "Die heruntergeladene Datei passt nicht zur angekündigten Version: Sie wird gelöscht.", "Загруженный файл не соответствует заявленной версии: он удалён.", "ダウンロードしたファイルが告知されたバージョンと一致しないため、削除されました。", "Il file scaricato non corrisponde alla versione annunciata: viene eliminato.", "O arquivo baixado não corresponde à versão anunciada: ele é excluído.", "Pobrany plik nie zgadza się z zapowiedzianą wersją: zostaje usunięty.", "Het gedownloade bestand komt niet overeen met de aangekondigde versie: het wordt verwijderd.", "İndirilen dosya duyurulan sürümle eşleşmiyor: siliniyor.", "다운로드한 파일이 발표된 버전과 일치하지 않습니다: 삭제합니다.", "下载的文件与公布的版本不符：已删除。", "Завантажений файл не відповідає оголошеній версії: його видалено.", "Den nedladdade filen matchar inte den aviserade versionen: den raderas.", "Stažený soubor neodpovídá ohlášené verzi: maže se."));
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
        if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException(L.T("Redirection non sécurisée refusée.", "Insecure redirect refused.", "Redirección no segura rechazada.", "Unsichere Weiterleitung abgelehnt.", "Небезопасное перенаправление отклонено.", "安全でないリダイレクトを拒否しました。", "Reindirizzamento non sicuro rifiutato.", "Redirecionamento inseguro recusado.", "Odrzucono niebezpieczne przekierowanie.", "Onveilige omleiding geweigerd.", "Güvensiz yönlendirme reddedildi.", "안전하지 않은 리디렉션을 거부했습니다.", "已拒绝不安全的重定向。", "Небезпечне перенаправлення відхилено.", "Osäker omdirigering nekades.", "Nebezpečné přesměrování bylo odmítnuto."));
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

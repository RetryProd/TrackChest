using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChestCounter;

/// <summary>Signatures of the catalog: ECDSA P-256 / SHA-256. The public key is built into the exe.</summary>
public static class AddonTrust
{
    private const string KeyResource = "addon-public.key";
    private static readonly Lazy<string?> BuiltInKey = new(() =>
    {
        using var stream = typeof(AddonTrust).Assembly.GetManifestResourceStream(KeyResource);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Trim();
        return text.Length == 0 ? null : text;
    });

    /// <summary>False until the maintainer drops assets\addon-public.key and rebuilds: no catalog can be trusted before.</summary>
    public static bool IsConfigured => BuiltInKey.Value != null;

    public static bool Verify(byte[] data, string signatureBase64) =>
        BuiltInKey.Value is { } key && Verify(data, signatureBase64, key);

    public static bool Verify(byte[] data, string signatureBase64, string publicKeyBase64)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64.Trim()), out _);
            return ecdsa.VerifyData(data, Convert.FromBase64String(signatureBase64.Trim()), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    public static (string PrivateKey, string PublicKey) NewKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()), Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()));
    }

    public static string Sign(byte[] data, string privateKeyBase64)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64.Trim()), out _);
        return Convert.ToBase64String(ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }
}

/// <summary>An entry of the signed catalog (index.json).</summary>
public sealed class CatalogEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>Old field, ignored (older catalogs).</summary>
    public string? Author { get; set; }
    public string Description { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    /// <summary>"verified" (reviewed) or "community" (automatic checks only).</summary>
    public string Tier { get; set; } = "community";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int Size { get; set; }

    public AddonTier TierValue => Tier == "verified" ? AddonTier.Verified : AddonTier.Community;

    public bool IsValid() =>
        AddonRules.IsId(Id) && AddonRules.IsText(Name, 1, 40) && AddonRules.IsVersion(Version)
        && AddonRules.IsText(Description, 0, 200) && Tier is "verified" or "community"
        && Permissions.All(p => AddonRules.AllPermissions.Contains(p)) && AddonRules.IsSha256(Sha256)
        && Size is > 0 and <= AddonRules.MaxZipBytes
        && Uri.TryCreate(Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

public sealed class CatalogIndex
{
    public int Version { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; }
    public List<CatalogEntry> Addons { get; set; } = new();
    /// <summary>"id" (every version) or "id@1.2.3": add-ons that must not run any more.</summary>
    public List<string> Revoked { get; set; } = new();

    public bool IsRevoked(string id, string version) => Revoked.Contains(id) || Revoked.Contains($"{id}@{version}");
}

/// <summary>Download of the signed catalog and of its packages. Everything is checked before anything is kept.</summary>
public static class AddonCatalog
{
    private const int MaxIndexBytes = 256 * 1024;
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 3 })
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders = { { "User-Agent", "TrackChest-Addons" } },
    };

    /// <summary>The URL of index.json: the config, else assets\addon-catalog.txt built into the exe, else empty.</summary>
    public static string ResolveUrl(Config config)
    {
        if (!string.IsNullOrWhiteSpace(config.AddonCatalogUrl)) return config.AddonCatalogUrl.Trim();
        using var stream = typeof(AddonCatalog).Assembly.GetManifestResourceStream("addon-catalog.txt");
        if (stream == null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    /// <summary>Fetches index.json and its .sig, refuses it unless the signature matches the built-in key.</summary>
    public static async Task<CatalogIndex> FetchIndexAsync(string url, CancellationToken cancellation)
    {
        if (!AddonTrust.IsConfigured)
            throw new InvalidOperationException(L.T("Clé publique du catalogue absente de cette version : catalogue indisponible.", "Catalog public key missing from this build: catalog unavailable.", "Falta la clave pública del catálogo en esta versión: catálogo no disponible.", "Öffentlicher Katalogschlüssel fehlt in dieser Version: Katalog nicht verfügbar.", "В этой версии нет открытого ключа каталога: каталог недоступен.", "このバージョンにはカタログの公開鍵がありません: カタログは利用できません。", "Chiave pubblica del catalogo assente in questa build: catalogo non disponibile.", "Chave pública do catálogo ausente nesta versão: catálogo indisponível.", "Brak klucza publicznego katalogu w tej wersji: katalog niedostępny.", "Openbare sleutel van de catalogus ontbreekt in deze build: catalogus niet beschikbaar.", "Bu sürümde katalog açık anahtarı yok: katalog kullanılamıyor.", "이 빌드에 카탈로그 공개 키가 없습니다: 카탈로그를 사용할 수 없습니다.", "此版本缺少目录公钥：目录不可用。", "У цій збірці немає відкритого ключа каталогу: каталог недоступний.", "Katalogens publika nyckel saknas i denna version: katalogen är inte tillgänglig.", "V této verzi chybí veřejný klíč katalogu: katalog není k dispozici."));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(L.T("L’adresse du catalogue doit être en https.", "The catalog address must be https.", "La dirección del catálogo debe ser https.", "Die Katalogadresse muss https verwenden.", "Адрес каталога должен использовать https.", "カタログのアドレスは https である必要があります。", "L'indirizzo del catalogo deve essere https.", "O endereço do catálogo deve ser https.", "Adres katalogu musi używać https.", "Het catalogusadres moet https zijn.", "Katalog adresi https olmalıdır.", "카탈로그 주소는 https여야 합니다.", "目录地址必须是 https。", "Адреса каталогу має бути https.", "Katalogadressen måste vara https.", "Adresa katalogu musí být https."));

        var index = await GetAsync(uri, MaxIndexBytes, cancellation);
        var signature = await GetAsync(new Uri(url + ".sig"), 4096, cancellation);
        if (!AddonTrust.Verify(index, System.Text.Encoding.ASCII.GetString(signature)))
            throw new InvalidDataException(L.T("Signature du catalogue invalide : il a peut-être été modifié. Rien n’est utilisé.", "Invalid catalog signature: it may have been tampered with. Nothing is used.", "Firma del catálogo no válida: puede haber sido modificado. No se usa nada.", "Ungültige Katalogsignatur: Er wurde möglicherweise verändert. Es wird nichts verwendet.", "Недействительная подпись каталога: возможно, он был изменён. Ничего не используется.", "カタログの署名が無効です。改ざんされた可能性があります。何も使用されません。", "Firma del catalogo non valida: potrebbe essere stato manomesso. Non viene usato nulla.", "Assinatura do catálogo inválida: ele pode ter sido adulterado. Nada é usado.", "Nieprawidłowy podpis katalogu: mógł zostać zmieniony. Nic nie zostanie użyte.", "Ongeldige catalogushandtekening: er is mogelijk mee geknoeid. Er wordt niets gebruikt.", "Geçersiz katalog imzası: değiştirilmiş olabilir. Hiçbir şey kullanılmıyor.", "잘못된 카탈로그 서명: 변조되었을 수 있습니다. 아무것도 사용하지 않습니다.", "目录签名无效：可能已被篡改。不使用任何内容。", "Недійсний підпис каталогу: його могли підробити. Нічого не використовується.", "Ogiltig katalogsignatur: den kan ha manipulerats. Inget används.", "Neplatný podpis katalogu: mohl být pozměněn. Nic se nepoužije."));

        CatalogIndex? parsed;
        try { parsed = JsonSerializer.Deserialize<CatalogIndex>(index, AddonJson.Strict); }
        catch (JsonException ex) { throw new InvalidDataException("Catalogue illisible.", ex); }
        if (parsed == null || parsed.Version != 1) throw new InvalidDataException("Catalogue illisible ou version inconnue.");
        parsed.Addons = parsed.Addons.Where(e => e.IsValid()).ToList();
        return parsed;
    }

    /// <summary>Downloads a package and checks size, SHA-256 and that the manifest says what the catalog said.</summary>
    public static async Task<(byte[] Zip, AddonManifest Manifest)> DownloadAsync(CatalogEntry entry, CatalogIndex index, CancellationToken cancellation)
    {
        if (!entry.IsValid()) throw new InvalidDataException(L.T("Entrée de catalogue invalide.", "Invalid catalog entry.", "Entrada de catálogo no válida.", "Ungültiger Katalogeintrag.", "Недопустимая запись каталога.", "カタログの項目が無効です。", "Voce del catalogo non valida.", "Entrada do catálogo inválida.", "Nieprawidłowy wpis katalogu.", "Ongeldig catalogusitem.", "Geçersiz katalog girdisi.", "잘못된 카탈로그 항목입니다.", "目录条目无效。", "Недійсний запис каталогу.", "Ogiltig katalogpost.", "Neplatná položka katalogu."));
        if (index.IsRevoked(entry.Id, entry.Version))
            throw new InvalidDataException(L.T("Cet add-on a été révoqué.", "This add-on has been revoked.", "Este add-on ha sido revocado.", "Dieses Add-on wurde widerrufen.", "Это дополнение отозвано.", "このアドオンは取り消されました。", "Questo componente aggiuntivo è stato revocato.", "Este complemento foi revogado.", "Ten dodatek został unieważniony.", "Deze add-on is ingetrokken.", "Bu eklenti iptal edildi.", "이 애드온은 취소되었습니다.", "此插件已被撤销。", "Це доповнення відкликано.", "Detta tillägg har återkallats.", "Tento doplněk byl odvolán."));

        var zip = await GetAsync(new Uri(entry.Url), AddonRules.MaxZipBytes, cancellation);
        if (zip.Length != entry.Size || !Convert.ToHexString(SHA256.HashData(zip)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L.T("Le fichier téléchargé ne correspond pas au catalogue (empreinte ou taille).", "The downloaded file does not match the catalog (hash or size).", "El archivo descargado no coincide con el catálogo (huella o tamaño).", "Die heruntergeladene Datei stimmt nicht mit dem Katalog überein (Prüfsumme oder Größe).", "Загруженный файл не совпадает с каталогом (хеш или размер).", "ダウンロードしたファイルがカタログと一致しません（ハッシュまたはサイズ）。", "Il file scaricato non corrisponde al catalogo (hash o dimensione).", "O arquivo baixado não corresponde ao catálogo (hash ou tamanho).", "Pobrany plik nie zgadza się z katalogiem (skrót lub rozmiar).", "Het gedownloade bestand komt niet overeen met de catalogus (hash of grootte).", "İndirilen dosya katalogla eşleşmiyor (hash veya boyut).", "다운로드한 파일이 카탈로그와 일치하지 않습니다 (해시 또는 크기).", "下载的文件与目录不符（哈希或大小）。", "Завантажений файл не відповідає каталогу (хеш або розмір).", "Den nedladdade filen matchar inte katalogen (hash eller storlek).", "Stažený soubor neodpovídá katalogu (hash nebo velikost)."));

        var manifest = AddonPackage.Read(zip);
        if (manifest.Id != entry.Id || manifest.Version != entry.Version
            || !manifest.Permissions.OrderBy(p => p).SequenceEqual(entry.Permissions.OrderBy(p => p)))
            throw new InvalidDataException(L.T("Le contenu de l’add-on ne correspond pas à sa fiche du catalogue.", "The add-on content does not match its catalog entry.", "El contenido del add-on no coincide con su ficha del catálogo.", "Der Inhalt des Add-ons stimmt nicht mit seinem Katalogeintrag überein.", "Содержимое дополнения не совпадает с его записью в каталоге.", "アドオンの内容がカタログの登録内容と一致しません。", "Il contenuto del componente non corrisponde alla sua voce nel catalogo.", "O conteúdo do complemento não corresponde à sua entrada no catálogo.", "Zawartość dodatku nie zgadza się z jego wpisem w katalogu.", "De inhoud van de add-on komt niet overeen met het catalogusitem.", "Eklentinin içeriği katalog girdisiyle eşleşmiyor.", "애드온 내용이 카탈로그 항목과 일치하지 않습니다.", "插件内容与其目录条目不符。", "Вміст доповнення не відповідає його запису в каталозі.", "Tilläggets innehåll matchar inte dess katalogpost.", "Obsah doplňku neodpovídá jeho položce v katalogu."));
        return (zip, manifest);
    }

    private static async Task<byte[]> GetAsync(Uri uri, int limit, CancellationToken cancellation)
    {
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException(L.T("Redirection non sécurisée refusée.", "Insecure redirect refused.", "Redirección no segura rechazada.", "Unsichere Weiterleitung abgelehnt.", "Небезопасное перенаправление отклонено.", "安全でないリダイレクトを拒否しました。", "Reindirizzamento non sicuro rifiutato.", "Redirecionamento inseguro recusado.", "Odrzucono niebezpieczne przekierowanie.", "Onveilige omleiding geweigerd.", "Güvensiz yönlendirme reddedildi.", "안전하지 않은 리디렉션을 거부했습니다.", "已拒绝不安全的重定向。", "Небезпечне перенаправлення відхилено.", "Osäker omdirigering nekades.", "Nebezpečné přesměrování bylo odmítnuto."));
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Fichier trop gros.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellation)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit) throw new InvalidDataException("Fichier trop gros.");
        }
        return buffer.ToArray();
    }
}

public sealed class AddonState
{
    public string Version { get; set; } = "";
    public string Tier { get; set; } = "local";
    public bool Enabled { get; set; } = true;
    public bool Revoked { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime InstalledUtc { get; set; }
    /// <summary>Labels of the add-on's counters the player turned off with "Configure" (null = all shown).</summary>
    public List<string>? HiddenCounters { get; set; }
}

public sealed record InstalledAddon(AddonManifest Manifest, AddonState State)
{
    public AddonTier Tier => State.Tier switch { "verified" => AddonTier.Verified, "community" => AddonTier.Community, _ => AddonTier.Local };
    public bool IsActive => State.Enabled && !State.Revoked;
    public bool IsCounterShown(AddonCounter counter) => State.HiddenCounters?.Contains(counter.Label) != true;
}

/// <summary>Installed add-ons: %APPDATA%\TrackChest\addons\&lt;id&gt;\package.zip, plus addons\state.json.</summary>
public sealed class AddonStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _root;

    public AddonStore(string dataFolder)
    {
        _root = Path.Combine(dataFolder, "addons");
    }

    private string StatePath => Path.Combine(_root, "state.json");
    private string PackagePath(string id) => Path.Combine(_root, id, "package.zip");

    private Dictionary<string, AddonState> LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
                return JsonSerializer.Deserialize<Dictionary<string, AddonState>>(File.ReadAllText(StatePath), Json) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        return new();
    }

    private void SaveState(Dictionary<string, AddonState> state)
    {
        Directory.CreateDirectory(_root);
        var tmp = StatePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
        File.Move(tmp, StatePath, overwrite: true);
    }

    /// <summary>Every installed add-on whose package is still intact (same SHA-256 as at install time, still valid).</summary>
    public List<InstalledAddon> Load()
    {
        var result = new List<InstalledAddon>();
        foreach (var (id, state) in LoadState())
        {
            try
            {
                if (!AddonRules.IsId(id)) continue;
                var zip = File.ReadAllBytes(PackagePath(id));
                if (!Convert.ToHexString(SHA256.HashData(zip)).Equals(state.Sha256, StringComparison.OrdinalIgnoreCase)) continue;
                var manifest = AddonPackage.Read(zip);
                if (manifest.Id == id) result.Add(new InstalledAddon(manifest, state));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }
        return result.OrderBy(a => a.State.InstalledUtc).ToList();
    }

    /// <summary>Installs (or updates) a validated package. A theme add-on turns the other themes off.</summary>
    public InstalledAddon Install(byte[] zip, AddonTier tier)
    {
        var manifest = AddonPackage.Read(zip);
        var state = LoadState();
        var directory = Path.Combine(_root, manifest.Id);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(PackagePath(manifest.Id), zip);

        var entry = new AddonState
        {
            Version = manifest.Version,
            Tier = tier.ToString().ToLowerInvariant(),
            Enabled = true,
            Sha256 = Convert.ToHexString(SHA256.HashData(zip)),
            InstalledUtc = DateTime.UtcNow,
            // An update keeps the counters the player turned off (those that still exist).
            HiddenCounters = state.TryGetValue(manifest.Id, out var previous) && previous.HiddenCounters is { Count: > 0 } hidden
                && hidden.Where(label => manifest.Counters.Any(c => c.Label == label)).ToList() is { Count: > 0 } kept
                ? kept : null,
        };
        state[manifest.Id] = entry;
        SaveState(state);
        if (manifest.Theme != null) DisableOtherThemes(manifest.Id);
        return new InstalledAddon(manifest, entry);
    }

    public void SetEnabled(string id, bool enabled)
    {
        var state = LoadState();
        if (!state.TryGetValue(id, out var entry) || (enabled && entry.Revoked)) return;
        entry.Enabled = enabled;
        SaveState(state);
        if (enabled && Load().FirstOrDefault(a => a.Manifest.Id == id)?.Manifest.Theme != null) DisableOtherThemes(id);
    }

    /// <summary>Shows or hides one counter of an add-on on the overlay ("Configure" button).</summary>
    public void SetCounterShown(string id, string label, bool shown)
    {
        var state = LoadState();
        if (!state.TryGetValue(id, out var entry)) return;
        var hidden = entry.HiddenCounters ?? new List<string>();
        hidden.RemoveAll(l => l == label);
        if (!shown) hidden.Add(label);
        entry.HiddenCounters = hidden.Count > 0 ? hidden : null;
        SaveState(state);
    }

    public void Remove(string id)
    {
        if (!AddonRules.IsId(id)) return;
        var state = LoadState();
        if (state.Remove(id)) SaveState(state);
        var directory = Path.Combine(_root, id);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    /// <summary>Turns off (and flags) every installed add-on that the signed catalog revokes. Returns their names.</summary>
    public List<string> ApplyRevocations(CatalogIndex index)
    {
        var names = new List<string>();
        var state = LoadState();
        foreach (var addon in Load())
        {
            if (!index.IsRevoked(addon.Manifest.Id, addon.Manifest.Version) || addon.State.Revoked) continue;
            if (state.TryGetValue(addon.Manifest.Id, out var entry))
            {
                entry.Revoked = true;
                entry.Enabled = false;
                names.Add(addon.Manifest.Name);
            }
        }
        if (names.Count > 0) SaveState(state);
        return names;
    }

    private void DisableOtherThemes(string keepId)
    {
        var state = LoadState();
        var changed = false;
        foreach (var addon in Load().Where(a => a.Manifest.Theme != null && a.Manifest.Id != keepId && a.State.Enabled))
        {
            if (state.TryGetValue(addon.Manifest.Id, out var entry)) { entry.Enabled = false; changed = true; }
        }
        if (changed) SaveState(state);
    }
}

/// <summary>What the enabled add-ons add to the overlay.</summary>
public static class AddonRuntime
{
    private static volatile IReadOnlyList<AddonCounter> _counters = Array.Empty<AddonCounter>();
    private static volatile IReadOnlyList<(string Id, string Source)> _scripts = Array.Empty<(string, string)>();
    private static readonly Dictionary<string, (int Failures, string Message)> ScriptErrors = new();
    private const int MaxFailures = 3;

    /// <summary>Reads the enabled add-ons: counters and scripts for the next overlay update, theme applied at once.</summary>
    public static void Load(AddonStore store)
    {
        var active = store.Load().Where(a => a.IsActive).ToList();
        var counters = active.Where(a => a.Manifest.Permissions.Contains(AddonRules.OverlayRows))
            .SelectMany(a => a.Manifest.Counters.Where(a.IsCounterShown)).Take(10).ToList();
        _counters = counters;
        var addonIcons = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var addon in active)
        {
            foreach (var (name, bytes) in addon.Manifest.Icons) addonIcons[name] = bytes;
        }
        OverlayIcons.SetAddonIcons(addonIcons);
        _scripts = active.Where(a => a.Manifest.HasScript && a.Manifest.Script != null)
            .Select(a => (a.Manifest.Id, a.Manifest.Script!)).ToList();
        lock (ScriptErrors) ScriptErrors.Clear(); // a reload (enable, update) gives every script a new chance
        var theme = active.LastOrDefault(a => a.Manifest.Theme != null && a.Manifest.Permissions.Contains(AddonRules.OverlayTheme))?.Manifest.Theme;
        NativeOverlay.SetTheme(theme?.ToOverlayTheme());
    }

    /// <summary>Last error of a script add-on (null = none), shown in the Add-on window.</summary>
    public static string? ScriptError(string id)
    {
        lock (ScriptErrors) return ScriptErrors.TryGetValue(id, out var e) ? e.Message : null;
    }

    /// <summary>
    /// Runs the script add-ons one after the other on the overlay (each gets the result of the previous one).
    /// A script that fails keeps the previous view; after three failures in a row it is skipped until the next reload.
    /// </summary>
    public static OverlayView ApplyScripts(OverlayView view, TrackerData data, IReadOnlyList<MatchRecord> session, MatchRecord? last, bool inProgress)
    {
        var scripts = _scripts;
        if (scripts.Count == 0) return view;
        var dataJson = AddonScript.DataJson(data, session, last, inProgress);
        foreach (var (id, source) in scripts)
        {
            lock (ScriptErrors)
            {
                if (ScriptErrors.TryGetValue(id, out var state) && state.Failures >= MaxFailures) continue;
            }
            try
            {
                view = AddonScript.Apply(source, view, dataJson);
                lock (ScriptErrors) ScriptErrors.Remove(id);
            }
            catch (Exception ex)
            {
                lock (ScriptErrors)
                {
                    var failures = ScriptErrors.TryGetValue(id, out var state) ? state.Failures + 1 : 1;
                    ScriptErrors[id] = (failures, ex.Message);
                }
            }
        }
        return view;
    }

    public static List<OverlayRow> CounterRows(TrackerData data, IReadOnlyList<MatchRecord> session, MatchRecord? last)
    {
        var rows = new List<OverlayRow>();
        foreach (var counter in _counters)
        {
            IReadOnlyList<MatchRecord> scope = counter.Scope switch
            {
                "session" => session,
                "last" => last == null ? Array.Empty<MatchRecord>() : new[] { last },
                _ => data.Matches,
            };
            var value = Compute(counter.Metric, scope);
            string? detail = null;
            if (counter.ShowLast && counter.Scope == "total" && last != null && Compute(counter.Metric, new[] { last }) is int latest)
            {
                detail = $"· {L.N(latest)} {L.T("dernière partie", "last match", "última partida", "letztes Match", "последний матч", "前回の試合", "ultima partita", "última partida", "ostatni mecz", "laatste potje", "son maç", "마지막 경기", "上一场比赛", "останній матч", "senaste matchen", "poslední zápas")}";
            }
            var icon = counter.Icon ?? DefaultMetricIcon(counter.Metric);
            rows.Add(new OverlayRow(counter.Label, value is int n ? L.N(n) : "—", Detail: detail, Icon: icon, Separator: rows.Count == 0));
        }
        return rows;
    }

    private static string? DefaultMetricIcon(string metric) => metric switch
    {
        "chests" or "opened" or "buried" => "Chest.png",
        "destroyed" => "break_chest.png",
        "sprites" => "Esprit_Chest.png",
        "wanted" => "Wanted_Card.png",
        "cannons" => "Loot_Cannons.png",
        "treasures" => "Treasures.png",
        _ => null,
    };

    /// <summary>Null = not available for these matches (older analysis without Wanted maps).</summary>
    private static int? Compute(string metric, IReadOnlyList<MatchRecord> matches) => metric switch
    {
        "chests" => matches.Sum(m => m.ChestsTotal),
        "opened" => matches.Sum(m => m.ChestsOpened),
        "destroyed" => matches.Sum(m => m.ChestsDestroyed),
        "sprites" => matches.Sum(m => m.SpriteChests),
        "buried" => matches.Sum(m => m.BuriedChests),
        "wanted" => matches.Any(m => m.WantedMaps != null) ? matches.Sum(m => m.WantedMaps?.Count ?? 0) : null,
        "cannons" => matches.Sum(m => m.LootCannons),
        "treasures" => matches.Sum(m => m.TreasureFinds.Count),
        "matches" => matches.Count,
        _ => null,
    };
}

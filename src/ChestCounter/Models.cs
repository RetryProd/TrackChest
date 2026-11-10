using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChestCounter;

public sealed class ChestRecord
{
    public float TimeSeconds { get; set; }
    public string Kind { get; set; } = "";
    public string Blueprint { get; set; } = "";
    /// <summary>True when the chest was destroyed by the player instead of being opened.</summary>
    public bool Destroyed { get; set; }
    public List<string> Items { get; set; } = new();
    public List<string> TreasureItems { get; set; } = new();
}

public sealed class TreasureFind
{
    /// <summary>Name to display (re-resolved from treasures.json so renaming updates old matches).</summary>
    public string DisplayName => PrizeId != null ? TreasureNames.Friendly(PrizeId) : L.Name(Item);

    public string Item { get; set; } = "";
    public string RawItem { get; set; } = "";
    /// <summary>Prize id sent by the server with the golden chest, e.g. "DelilahPrize:dpid_item4".</summary>
    public string? PrizeId { get; set; }
    /// <summary>True when detected from the golden treasure chest itself.</summary>
    public bool FromGoldenChest { get; set; }
    /// <summary>True when detected only from the prize handler (exact time, item unknown unless sent at that moment).</summary>
    public bool FromPrizeHandler { get; set; }
    /// <summary>True when the time is exact (golden chest or prize handler), false when estimated.</summary>
    public bool TimeExact { get; set; }
    /// <summary>True when the source chest was destroyed rather than opened.</summary>
    public bool SourceDestroyed { get; set; }
    public string SourceLabel => SourceDestroyed ? $"{L.Name(SourceKind)} ({L.T("détruit", "destroyed", "destruido", "zerstört", "разрушен", "破壊", "distrutto", "destruído", "zniszczona", "vernietigd", "yok edildi", "파괴됨", "已摧毁", "знищено", "förstörd", "zničená")})" : L.Name(SourceKind);
    /// <summary>When it was obtained (replay seconds). Exact for golden chests, estimated otherwise.</summary>
    public float? ObtainedAtSeconds { get; set; }
    /// <summary>Kind of container / interaction it came from (Coffre, Canon à butin...).</summary>
    public string SourceKind { get; set; } = "";
    public string SourceBlueprint { get; set; } = "";
    public bool SourceIsChest { get; set; } = true;
    /// <summary>True when the source is known for sure (golden chest), false when it is the last interaction before use.</summary>
    public bool SourceCertain { get; set; }
    /// <summary>For estimated sources: other (non-chest) interactions between the source and the first use.</summary>
    public List<string> AlsoPossible { get; set; } = new();
}

public sealed class MatchRecord
{
    public string SessionId { get; set; } = "";
    public string ReplayFile { get; set; } = "";
    public DateTime AnalyzedAtUtc { get; set; }
    public DateTime? MatchStartUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Playlist { get; set; }
    public string? GameVersion { get; set; }
    public string? PlayerName { get; set; }
    public string? EpicId { get; set; }
    public bool TrackedByUsername { get; set; }
    /// <summary>Set when none of the configured names played this match (name of the replay owner).</summary>
    public string? NotTrackedOwner { get; set; }
    public bool IsReplayOwner { get; set; }
    public int? Placement { get; set; }
    public int? TotalPlayers { get; set; }
    public int? Eliminations { get; set; }
    public int ChestsOpened { get; set; }
    public int AmmoBoxesOpened { get; set; }
    public int OtherContainersOpened { get; set; }
    /// <summary>Non-chest interactions by kind (loot cannons, ammo boxes, food boxes...).</summary>
    public Dictionary<string, int> OtherInteractions { get; set; } = new();
    public int AnalyzerVersion { get; set; } = 1;
    public bool IsBattleRoyale { get; set; } = true;
    public List<ChestRecord> Chests { get; set; } = new();
    /// <summary>Replay timestamps of Wanted maps collected from posters. Null for older analyses.</summary>
    public List<float>? WantedMaps { get; set; }
    public List<TreasureFind> TreasureFinds { get; set; } = new();

    [JsonIgnore]
    public int ChestsDestroyed => Chests.Count(chest => chest.Destroyed);

    [JsonIgnore]
    public int LootCannons => OtherInteractions.GetValueOrDefault(ChestKinds.Cannon);

    /// <summary>Chests opened or broken, plus loot cannons fired.</summary>
    [JsonIgnore]
    public int ChestsTotal => ChestsOpened + ChestsDestroyed + LootCannons;

    [JsonIgnore]
    public int SpriteChests => Chests.Count(chest => chest.Kind == ChestKinds.PosterChest);
}

public sealed class Config
{
    /// <summary>Selected profile (Epic id or name of the account the player plays on); null = most recent one.</summary>
    public string? Profile { get; set; }
    public string DemosFolder { get; set; } = "";

    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }
    /// <summary>Locked overlay = click-through, cannot be moved (Ctrl+Shift+L to toggle).</summary>
    public bool OverlayLocked { get; set; } = true;

    /// <summary>Size of the overlay in percent (100 = normal), on top of the Windows display scaling.</summary>
    public int OverlaySize { get; set; } = 100;

    /// <summary>Opacity of the overlay in percent (100 = opaque).</summary>
    public int OverlayOpacity { get; set; } = 100;

    /// <summary>Address of the add-on catalog (index.json). Empty = the one built into the exe, if any. Only a catalog signed with the built-in key is trusted.</summary>
    public string? AddonCatalogUrl { get; set; }

    /// <summary>Look for a newer version on GitHub when the app starts (the installation itself always asks first).</summary>
    public bool CheckUpdates { get; set; } = true;

    /// <summary>Interface language (one of L.Codes: "fr", "en", "es", "de", "ru", "ja", "it", "pt", "pl", "nl", "tr", "ko", "zh", "uk", "sv", "cs"). Empty = language of Windows.</summary>
    public string? Language { get; set; }

}

public sealed class TrackerData
{
    public int Version { get; set; } = 1;
    public int AnalyzerVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<MatchRecord> Matches { get; set; } = new();

    /// <summary>Key = full path, size and last write time. Value = import status.</summary>
    public Dictionary<string, string> ProcessedFiles { get; set; } = new();
}

/// <summary>Reads/writes JSON files atomically in the data folder.</summary>
public sealed partial class Store
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Folder { get; }

    public static string DefaultFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "TrackChest");
        // The app used to be called ChestOpen: keep its history and settings.
        var old = Path.Combine(appData, "ChestOpen");
        if (!Directory.Exists(folder) && Directory.Exists(old))
        {
            try { Directory.Move(old, folder); }
            catch (IOException) { return old; }
            catch (UnauthorizedAccessException) { return old; }
        }
        return folder;
    }

    public string ConfigPath => Path.Combine(Folder, "config.json");

    public string DataPath => Path.Combine(Folder, "data.json");
    public string ReportPath => Path.Combine(Folder, "report.html");
    public string PrizeNamesPath => Path.Combine(Folder, "treasures.json");
    public string ErrorLogPath => Path.Combine(Folder, "errors.log");

    /// <summary>Known treasure hunt prize ids (week 1), in the chosen language. Unknown ids are added automatically with an empty name.</summary>
    public static Dictionary<string, string> DefaultPrizeNames => new()
    {
        ["DelilahPrize:dpid_item1"] = L.T("Gantelets d'A$AP Rocky en or massif (pioche) ?", "A$AP Rocky solid gold gauntlets (pickaxe) ?", "Guanteletes de oro macizo de A$AP Rocky (pico) ?", "Massivgold-Handschuhe von A$AP Rocky (Spitzhacke) ?", "Перчатки A$AP Rocky из чистого золота (кирка) ?", "A$AP Rocky の純金ガントレット（収集ツール）？", "Guanti d'oro massiccio di A$AP Rocky (piccone) ?", "Manoplas de ouro maciço do A$AP Rocky (picareta) ?", "Rękawice z litego złota A$AP Rocky'ego (kilof) ?", "Massief gouden handschoenen van A$AP Rocky (houweel) ?", "A$AP Rocky'nin som altın eldivenleri (kazma) ?", "A$AP Rocky 순금 건틀릿 (곡괭이) ?", "A$AP Rocky 纯金护手（采集工具）？", "Рукавиці з чистого золота A$AP Rocky (кирка) ?", "A$AP Rockys handskar i massivt guld (hacka) ?", "Rukavice z ryzího zlata A$AP Rockyho (krumpáč) ?"),
        ["DelilahPrize:dpid_item2"] = L.T("Plunder in Paradise (écran de chargement)", "Plunder in Paradise (loading screen)", "Plunder in Paradise (pantalla de carga)", "Plunder in Paradise (Ladebildschirm)", "Plunder in Paradise (экран загрузки)", "Plunder in Paradise（ロード画面）", "Plunder in Paradise (schermata di caricamento)", "Plunder in Paradise (tela de carregamento)", "Plunder in Paradise (ekran wczytywania)", "Plunder in Paradise (laadscherm)", "Plunder in Paradise (yükleme ekranı)", "Plunder in Paradise (로딩 화면)", "Plunder in Paradise（载入画面）", "Plunder in Paradise (екран завантаження)", "Plunder in Paradise (laddningsskärm)", "Plunder in Paradise (načítací obrazovka)"),
        ["DelilahPrize:dpid_item3"] = L.T("Mercedes-AMG G 63 (véhicule) ?", "Mercedes-AMG G 63 (vehicle) ?", "Mercedes-AMG G 63 (vehículo) ?", "Mercedes-AMG G 63 (Fahrzeug) ?", "Mercedes-AMG G 63 (транспорт) ?", "Mercedes-AMG G 63（乗り物）？", "Mercedes-AMG G 63 (veicolo) ?", "Mercedes-AMG G 63 (veículo) ?", "Mercedes-AMG G 63 (pojazd) ?", "Mercedes-AMG G 63 (voertuig) ?", "Mercedes-AMG G 63 (araç) ?", "Mercedes-AMG G 63 (차량) ?", "Mercedes-AMG G 63（载具）？", "Mercedes-AMG G 63 (транспорт) ?", "Mercedes-AMG G 63 (fordon) ?", "Mercedes-AMG G 63 (vozidlo) ?"),
        ["DelilahPrize:dpid_item4"] = L.T("No Trespassing (piste musicale)", "No Trespassing (music track)", "No Trespassing (pista musical)", "No Trespassing (Musiktitel)", "No Trespassing (музыкальный трек)", "No Trespassing（ミュージック）", "No Trespassing (brano musicale)", "No Trespassing (faixa musical)", "No Trespassing (utwór muzyczny)", "No Trespassing (muzieknummer)", "No Trespassing (müzik parçası)", "No Trespassing (음악 트랙)", "No Trespassing（音乐曲目）", "No Trespassing (музичний трек)", "No Trespassing (musikspår)", "No Trespassing (hudební skladba)"),
    };

    public void LoadPrizeNames()
    {
        var names = Load<Dictionary<string, string>>(PrizeNamesPath);
        if (names == null)
        {
            names = DefaultPrizeNames;
            Save(PrizeNamesPath, names);
        }

        TreasureNames.PrizeNames.Clear();
        foreach (var kv in names)
        {
            TreasureNames.PrizeNames[kv.Key] = kv.Value;
        }
    }

    /// <summary>Adds unknown prize ids to treasures.json so the user can name them. Returns the new ids.</summary>
    public List<string> RegisterPrizeIds(IEnumerable<string> ids)
    {
        var added = ids.Where(id => !TreasureNames.PrizeNames.ContainsKey(id)).Distinct().ToList();
        if (added.Count == 0)
        {
            return added;
        }

        var names = Load<Dictionary<string, string>>(PrizeNamesPath) ?? DefaultPrizeNames;
        foreach (var id in added)
        {
            names[id] = "";
            TreasureNames.PrizeNames[id] = "";
        }
        Save(PrizeNamesPath, names);
        return added;
    }

    public Store(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
    }

    public Config? LoadConfig() => Load<Config>(ConfigPath);
    public void SaveConfig(Config config) => Save(ConfigPath, config);
    /// <summary>A new history starts at the current analysis version (nothing to re-analyze).</summary>
    public TrackerData LoadData() => Load<TrackerData>(DataPath) ?? new TrackerData { AnalyzerVersion = ReplayAnalyzer.CurrentVersion };
    public void SaveData(TrackerData data) => Save(DataPath, data);

    private static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch (Exception ex)
        {
            // Keep a copy of the unreadable file rather than silently losing the history.
            var backup = path + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(path, backup, overwrite: true);
            Console.WriteLine(L.T($"Fichier illisible ({ex.Message}), copie de sauvegarde : {backup}", $"Unreadable file ({ex.Message}), backup copy: {backup}", $"Archivo ilegible ({ex.Message}), copia de seguridad: {backup}", $"Datei nicht lesbar ({ex.Message}), Sicherungskopie: {backup}", $"Не удаётся прочитать файл ({ex.Message}), резервная копия: {backup}", $"ファイルを読み取れません（{ex.Message}）。バックアップ: {backup}", $"File illeggibile ({ex.Message}), copia di backup: {backup}", $"Arquivo ilegível ({ex.Message}), cópia de segurança: {backup}", $"Nieczytelny plik ({ex.Message}), kopia zapasowa: {backup}", $"Onleesbaar bestand ({ex.Message}), reservekopie: {backup}", $"Okunamayan dosya ({ex.Message}), yedek kopya: {backup}", $"읽을 수 없는 파일 ({ex.Message}), 백업 사본: {backup}", $"无法读取的文件（{ex.Message}），备份副本：{backup}", $"Файл неможливо прочитати ({ex.Message}), резервна копія: {backup}", $"Oläslig fil ({ex.Message}), säkerhetskopia: {backup}", $"Nečitelný soubor ({ex.Message}), záložní kopie: {backup}"));

            return null;
        }
    }

    private static void Save<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, path, overwrite: true);
    }
}

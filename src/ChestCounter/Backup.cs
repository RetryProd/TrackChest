using System.IO.Compression;
using System.Text.Json;

namespace ChestCounter;

/// <summary>What an import changed.</summary>
public sealed record ImportResult(int Added, int Updated, int Already);

/// <summary>
/// Backup of the history in a single .zip (data.json + treasures.json), so it survives a PC reset.
/// Importing merges into the current history: a match is never counted twice.
/// </summary>
public sealed partial class Store
{
    private const string BackupDataEntry = "data.json";
    private const string BackupPrizesEntry = "treasures.json";

    /// <summary>Writes the history (and the treasure names) to a .zip. Replaces the file when it exists.</summary>
    public void ExportBackup(string path, TrackerData data)
    {
        var tmp = path + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                WriteEntry(zip, BackupDataEntry, JsonSerializer.Serialize(data, Json));
                var prizes = Load<Dictionary<string, string>>(PrizeNamesPath);
                if (prizes != null) WriteEntry(zip, BackupPrizesEntry, JsonSerializer.Serialize(prizes, Json));
            }
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>
    /// Merges a backup into <paramref name="data"/>. Throws InvalidDataException when the file is not a TrackChest backup.
    /// The caller saves the data afterwards.
    /// </summary>
    public ImportResult ImportBackup(string path, TrackerData data)
    {
        TrackerData? backup = null;
        Dictionary<string, string>? prizes = null;
        var hasData = false;
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.GetEntry(BackupDataEntry) is { } entry)
            {
                hasData = true;
                backup = JsonSerializer.Deserialize<TrackerData>(ReadEntry(entry), Json);
                if (zip.GetEntry(BackupPrizesEntry) is { } prizeEntry)
                {
                    prizes = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadEntry(prizeEntry), Json);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            throw new InvalidDataException(L.T("Sauvegarde illisible ou endommagée.", "Unreadable or damaged backup.", "Copia de seguridad ilegible o dañada.", "Sicherung nicht lesbar oder beschädigt.", "Резервная копия не читается или повреждена.", "バックアップを読み取れないか、破損しています。", "Backup illeggibile o danneggiato.", "Backup ilegível ou danificado.", "Kopia zapasowa nieczytelna lub uszkodzona.", "Back-up onleesbaar of beschadigd.", "Yedek okunamıyor veya hasarlı.", "백업을 읽을 수 없거나 손상되었습니다.", "备份无法读取或已损坏。", "Резервну копію неможливо прочитати або її пошкоджено.", "Säkerhetskopian är oläslig eller skadad.", "Záloha je nečitelná nebo poškozená."), ex);
        }
        if (!hasData)
        {
            throw new InvalidDataException(L.T("Ce fichier n’est pas une sauvegarde TrackChest.", "This file is not a TrackChest backup.", "Este archivo no es una copia de seguridad de TrackChest.", "Diese Datei ist keine TrackChest-Sicherung.", "Этот файл не является резервной копией TrackChest.", "このファイルは TrackChest のバックアップではありません。", "Questo file non è un backup di TrackChest.", "Este arquivo não é um backup do TrackChest.", "Ten plik nie jest kopią zapasową TrackChest.", "Dit bestand is geen TrackChest-back-up.", "Bu dosya bir TrackChest yedeği değil.", "이 파일은 TrackChest 백업이 아닙니다.", "此文件不是 TrackChest 备份。", "Цей файл не є резервною копією TrackChest.", "Den här filen är ingen TrackChest-säkerhetskopia.", "Tento soubor není záloha TrackChest."));
        }
        if (backup == null)
        {
            throw new InvalidDataException(L.T("Sauvegarde vide.", "Empty backup.", "Copia de seguridad vacía.", "Leere Sicherung.", "Пустая резервная копия.", "バックアップが空です。", "Backup vuoto.", "Backup vazio.", "Pusta kopia zapasowa.", "Lege back-up.", "Boş yedek.", "빈 백업입니다.", "空备份。", "Порожня резервна копія.", "Tom säkerhetskopia.", "Prázdná záloha."));
        }

        int added = 0, updated = 0, already = 0;
        foreach (var match in backup.Matches)
        {
            var index = data.Matches.FindIndex(m => m.SessionId == match.SessionId && Profiles.Key(m) == Profiles.Key(match));
            if (index < 0)
            {
                data.Matches.Add(match);
                added++;
            }
            else if (data.Matches[index].AnalyzerVersion < match.AnalyzerVersion)
            {
                data.Matches[index] = match;
                updated++;
            }
            else
            {
                already++;
            }
        }

        if (backup.CreatedUtc < data.CreatedUtc) data.CreatedUtc = backup.CreatedUtc;
        // Matches of an older analysis are re-read from the replays still on this PC at the next scan.
        if (backup.AnalyzerVersion != ReplayAnalyzer.CurrentVersion) data.ProcessedFiles.Clear();

        if (prizes is { Count: > 0 })
        {
            var names = Load<Dictionary<string, string>>(PrizeNamesPath) ?? DefaultPrizeNames;
            foreach (var (id, name) in prizes)
            {
                if (!names.TryGetValue(id, out var current) || string.IsNullOrWhiteSpace(current))
                {
                    names[id] = name;
                }
            }
            Save(PrizeNamesPath, names);
            foreach (var kv in names) TreasureNames.PrizeNames[kv.Key] = kv.Value;
        }

        return new ImportResult(added, updated, already);
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

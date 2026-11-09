using System.Security.Cryptography;

namespace ChestCounter;

internal sealed record ScanResult(int Files, int Errors, int Incomplete, string? LiveReplay, bool FolderMissing, bool Changed)
{
    public bool InProgress => LiveReplay != null;
}

/// <summary>Single-writer import of finalized local Fortnite replays.</summary>
internal sealed class ReplayScanner(Store store, Config config, TrackerData data)
{
    public ScanResult Scan(CancellationToken cancellation, Action<int, int>? progress = null)
    {
        if (!Directory.Exists(config.DemosFolder))
        {
            return new(0, 0, 0, null, true, false);
        }

        var files = new DirectoryInfo(config.DemosFolder).EnumerateFiles("*.replay")
            .OrderBy(f => f.LastWriteTimeUtc).ToList();
        var candidates = new List<(FileInfo File, string Key, ReplayState State)>();
        string? liveReplay = null;
        var errors = 0;
        var incomplete = 0;
        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            var key = FileKey(file);
            if (key == null) continue;
            if (data.ProcessedFiles.TryGetValue(key, out var status))
            {
                if (status == "erreur") errors++;
                if (status == "incomplet") incomplete++;
                continue;
            }

            var state = ReplayHeaderProbe.Read(file.FullName);
            if (state == ReplayState.Unavailable) continue;
            var age = DateTime.UtcNow - file.LastWriteTimeUtc;
            if (state == ReplayState.Live && age < TimeSpan.FromMinutes(10))
            {
                liveReplay = file.FullName;
                continue;
            }
            if (age >= TimeSpan.FromSeconds(state == ReplayState.Finished ? 4 : 30))
            {
                candidates.Add((file, key, state));
            }
        }

        var changed = false;
        for (var i = 0; i < candidates.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Invoke(i, candidates.Count);
            var (file, key, state) = candidates[i];
            if (FileKey(file) != key) continue;

            string status;
            MatchRecord? match = null;
            try
            {
                status = state switch
                {
                    ReplayState.Live or ReplayState.NoKey => "incomplet",
                    ReplayState.Finished => "lu",
                    _ => throw new InvalidDataException("En-tête de replay illisible."),
                };
                if (state == ReplayState.Finished)
                {
                    match = ReadMatch(file, cancellation);
                    if (!match.IsBattleRoyale)
                    {
                        status = "autre mode";
                        match = null;
                    }
                    else if (!match.IsReplayOwner)
                    {
                        throw new InvalidDataException("Impossible d’identifier le joueur qui a enregistré le replay.");
                    }
                }
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 2 or 3 or 32 or 33)
            {
                continue; // Fortnite may briefly hold the file. Retry on the next scan.
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested)
            {
                status = "erreur";
                match = null;
                File.AppendAllText(store.ErrorLogPath, $"{DateTime.Now:g} · {file.Name} · {ex.Message}{Environment.NewLine}");
            }

            cancellation.ThrowIfCancellationRequested();
            if (FileKey(file) != key) continue;
            if (match != null)
            {
                var existing = data.Matches.FindIndex(m =>
                    m.SessionId == match.SessionId && Profiles.Key(m) == Profiles.Key(match));
                if (existing < 0)
                {
                    data.Matches.Add(match);
                    changed = true;
                }
                else if (data.Matches[existing].AnalyzerVersion < match.AnalyzerVersion)
                {
                    data.Matches[existing] = match;
                    changed = true;
                }
            }
            if (status == "erreur") errors++;
            if (status == "incomplet") incomplete++;
            data.ProcessedFiles[key] = status;
            store.SaveData(data);
        }

        return new(files.Count, errors, incomplete, liveReplay, false, changed);
    }

    private static MatchRecord ReadMatch(FileInfo file, CancellationToken cancellation)
    {
        using var stream = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var analyzer = new ReplayAnalyzer(timeout.Token);
        MatchRecord match;
        try
        {
            match = analyzer.Analyze(stream, file.Name, null);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new InvalidDataException("Temps maximal d’analyse dépassé.");
        }
        if (analyzer.IsPartialReplay)
        {
            throw new InvalidDataException("Le replay contient un bloc tronqué.");
        }
        if (string.IsNullOrWhiteSpace(match.SessionId))
        {
            stream.Position = 0;
            match.SessionId = "sha256-" + Convert.ToHexString(SHA256.HashData(stream));
        }
        return match;
    }

    private static string? FileKey(FileInfo file)
    {
        file.Refresh();
        return file.Exists ? $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : null;
    }
}

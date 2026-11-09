using System.Security.Cryptography;
using System.Text;

namespace ChestCounter;

internal static class Program
{
    internal static string DefaultReplays => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");

    internal static string DataFolderKey(string folder) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToUpperInvariant())));

    [STAThread]
    private static int Main(string[] args)
    {
        string? replays = null;
        string? dataFolder = null;
        string? preview = null;
        var once = false;
        var help = false;
        var watchFortnite = false;
        var launchedByFortnite = false;
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--demos" when i + 1 < args.Length: replays = Path.GetFullPath(args[++i]); break;
                    case "--data" when i + 1 < args.Length: dataFolder = Path.GetFullPath(args[++i]); break;
                    case "--overlay-preview" when i + 1 < args.Length: preview = Path.GetFullPath(args[++i]); break;
                    case "--once": once = true; break;
                    case "--watch-fortnite": watchFortnite = true; break;
                    case "--fortnite-auto": launchedByFortnite = true; break;
                    case "--help" or "-h": help = true; break;
                    case "--addon-keygen" or "--addon-sign" or "--addon-verify" or "--addon-check":
                        return AddonTools.Run(args);
                    default: throw new ArgumentException($"Option invalide ou incomplète. {args[i]}");
                }
            }

            if (once || preview != null || help)
            {
                NativeConsole.Ensure(attachToParent: true);
                Console.OutputEncoding = Encoding.UTF8;
            }
            if (help)
            {
                Console.WriteLine("TrackChest [--demos dossier] [--once] [--data dossier] [--overlay-preview fichier.png]");
                Console.WriteLine("Add-ons : --addon-keygen dossier | --addon-sign fichier cle-privee | --addon-verify fichier cle-publique | --addon-check add-on.zip");
                return 0;
            }

            dataFolder ??= Store.DefaultFolder();
            if (watchFortnite) return FortniteStartup.Watch(dataFolder);

            var store = new Store(dataFolder);
            using var instance = new Mutex(false, @"Local\TrackChest." + DataFolderKey(store.Folder));
            try
            {
                if (!instance.WaitOne(0))
                {
                    if (launchedByFortnite) return 0;
                    throw new InvalidOperationException("TrackChest utilise déjà cet historique. Ferme cette instance pour continuer.");
                }
            }
            catch (AbandonedMutexException) { } // Windows transferred ownership to this instance.

            try
            {
                var config = store.LoadConfig() ?? new Config();
                if (replays != null) config.DemosFolder = replays;
                store.LoadPrizeNames();
                AddonRuntime.Load(new AddonStore(store.Folder));
                var data = store.LoadData();
                if (data.AnalyzerVersion != ReplayAnalyzer.CurrentVersion)
                {
                    data.ProcessedFiles.Clear();
                    data.AnalyzerVersion = ReplayAnalyzer.CurrentVersion;
                }

                if (preview != null)
                {
                    NativeOverlay.Update(OverlayModel.Build(Profiles.View(data, config), false));
                    NativeOverlay.Start(-20000, -20000, true, (_, _) => { }, _ => { });
                    return NativeOverlay.Snapshot(preview) ? 0 : 1;
                }
                if (once)
                {
                    if (string.IsNullOrWhiteSpace(config.DemosFolder)) config.DemosFolder = DefaultReplays;
                    return RunOnce(store, config, data);
                }

                ApplicationConfiguration.Initialize();
                if (replays != null) store.SaveConfig(config);
                Application.Run(new CounterWindow(store, config, data, launchedByFortnite));
                return 0;
            }
            finally { instance.ReleaseMutex(); }
        }
        catch (Exception ex)
        {
            if (watchFortnite) return 1;
            if (args.Length > 0 && !once && preview == null) NativeConsole.Ensure(attachToParent: true);
            if (args.Length > 0) Console.Error.WriteLine(ex.Message);
            else MessageBox.Show(ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int RunOnce(Store store, Config config, TrackerData data)
    {
        var scan = new ReplayScanner(store, config, data).Scan(CancellationToken.None);
        if (scan.FolderMissing)
        {
            Console.Error.WriteLine($"Dossier de replays introuvable. {config.DemosFolder}");
            return 1;
        }

        ReportWriter.WriteAll(store, config, data);
        Console.WriteLine($"{L.Count(scan.Files, "fichier", "file", "archivo")} · {scan.Errors} illisible(s) · {scan.Incomplete} incomplet(s)");
        foreach (var profile in Profiles.List(data))
        {
            var matches = data.Matches.Where(m => Profiles.Key(m) == profile.Key)
                .OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).ToList();
            var wanted = matches.Any(m => m.WantedMaps != null) ? L.N(matches.Sum(m => m.WantedMaps?.Count ?? 0)) : "—";
            Console.WriteLine($"{L.Count(profile.Matches, "partie", "match", "partida")} · {L.N(profile.Chests)} coffres · dernière partie {matches[0].ChestsTotal} coffres");
            Console.WriteLine($"Cassés {L.N(matches.Sum(m => m.ChestsDestroyed))} · Esprits {L.N(matches.Sum(m => m.SpriteChests))} · Cartes Wanted {wanted}");
        }
        Console.WriteLine($"Rapport local  {store.ReportPath}");
        return scan.Errors > 0 ? 2 : 0;
    }
}

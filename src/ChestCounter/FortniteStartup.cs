using System.ComponentModel;
using System.Diagnostics;

namespace ChestCounter;

internal static class FortniteStartup
{
    private const string GameProcess = "FortniteClient-Win64-Shipping";
    private static string Shortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "TrackChest avec Fortnite.lnk");

    /// <summary>True when TrackChest opens with Fortnite (the startup shortcut exists).</summary>
    public static bool IsEnabled => File.Exists(Shortcut);

    /// <summary>Same as Demarrage-Automatique.ps1: creates or removes the startup shortcut (the watcher stops by itself).</summary>
    public static void SetEnabled(bool enable)
    {
        if (!enable)
        {
            if (File.Exists(Shortcut)) File.Delete(Shortcut);
            return;
        }

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException(L.T("Exécutable TrackChest introuvable.", "TrackChest executable not found.", "No se encontró el ejecutable de TrackChest.", "TrackChest-Programmdatei nicht gefunden.", "Исполняемый файл TrackChest не найден.", "TrackChest の実行ファイルが見つかりません。", "Eseguibile di TrackChest non trovato.", "Executável do TrackChest não encontrado.", "Nie znaleziono pliku wykonywalnego TrackChest.", "Uitvoerbaar bestand van TrackChest niet gevonden.", "TrackChest yürütülebilir dosyası bulunamadı.", "TrackChest 실행 파일을 찾을 수 없습니다.", "未找到 TrackChest 可执行文件。", "Виконуваний файл TrackChest не знайдено.", "TrackChest-programfilen hittades inte.", "Spustitelný soubor TrackChest nebyl nalezen."));
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host indisponible.");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic link = shell.CreateShortcut(Shortcut);
        try
        {
            link.TargetPath = executable;
            link.Arguments = "--watch-fortnite";
            link.WorkingDirectory = Path.GetDirectoryName(executable);
            link.Description = "Ouvre TrackChest au lancement de Fortnite";
            link.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        // Start the watcher now (a second one exits at once: it holds a mutex).
        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--watch-fortnite" } })?.Dispose();
    }

    public static int Watch(string dataFolder)
    {
        using var watcher = new Mutex(false, @"Local\TrackChest.FortniteWatch." + Program.DataFolderKey(dataFolder), out var first);
        if (!first) return 0;

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException(L.T("Exécutable TrackChest introuvable.", "TrackChest executable not found.", "No se encontró el ejecutable de TrackChest.", "TrackChest-Programmdatei nicht gefunden.", "Исполняемый файл TrackChest не найден.", "TrackChest の実行ファイルが見つかりません。", "Eseguibile di TrackChest non trovato.", "Executável do TrackChest não encontrado.", "Nie znaleziono pliku wykonywalnego TrackChest.", "Uitvoerbaar bestand van TrackChest niet gevonden.", "TrackChest yürütülebilir dosyası bulunamadı.", "TrackChest 실행 파일을 찾을 수 없습니다.", "未找到 TrackChest 可执行文件。", "Виконуваний файл TrackChest не знайдено.", "TrackChest-programfilen hittades inte.", "Spustitelný soubor TrackChest nebyl nalezen."));
        using var current = Process.GetCurrentProcess();
        var session = current.SessionId;
        int? previous = null;
        while (File.Exists(Shortcut))
        {
            var game = FindGame(session);
            if (game == null || (game != previous && Launch(executable, dataFolder)))
            {
                previous = game;
            }
            Thread.Sleep(TimeSpan.FromSeconds(4));
        }
        return 0;
    }

    private static int? FindGame(int session)
    {
        int? game = null;
        foreach (var process in Process.GetProcessesByName(GameProcess))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId == session && (game == null || process.Id < game))
                    {
                        game = process.Id;
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Fortnite can exit between the process listing and the session check.
                }
            }
        }
        return game;
    }

    private static bool Launch(string executable, string dataFolder)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            ArgumentList = { "--fortnite-auto", "--data", dataFolder },
        };
        try
        {
            using var process = Process.Start(start);
            return process != null;
        }
        catch (Win32Exception)
        {
            // The executable may be temporarily unavailable during an update.
            return false;
        }
    }
}

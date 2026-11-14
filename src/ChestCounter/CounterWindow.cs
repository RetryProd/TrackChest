using System.Diagnostics;

namespace ChestCounter;

internal sealed partial class CounterWindow : Form
{
    private readonly Store _store;
    private readonly Config _config;
    private readonly TrackerData _data;
    private readonly ReplayScanner _scanner;
    private readonly bool _startMinimized;
    private readonly CancellationTokenSource _closing = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 4000 };
    private string? _liveReplay;
    private bool _updatingProfiles;
    private bool _scanning;
    private bool _inProgress;
    private bool _overlayStarted;
    private bool _reportDirty = true;

    public CounterWindow(Store store, Config config, TrackerData data, bool launchedByFortnite)
    {
        _store = store;
        _config = config;
        _data = data;
        _scanner = new(store, config, data);
        _startMinimized = launchedByFortnite && !string.IsNullOrWhiteSpace(config.DemosFolder);

        BuildLayout();
        if (_startMinimized) WindowState = FormWindowState.Minimized;
        _folder.Text = string.IsNullOrWhiteSpace(_config.DemosFolder) ? Program.DefaultReplays : _config.DemosFolder;

        Shown += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_config.DemosFolder)) ChooseFolder();
            if (string.IsNullOrWhiteSpace(_config.DemosFolder)) return;
            StartOverlay();
            RefreshStats();
            await Scan();
            if (_config.CheckUpdates && !_startMinimized) await RunUpdate(manual: false);
        };
        _timer.Tick += async (_, _) => await Scan();
        _browse.Click += async (_, _) =>
        {
            _timer.Stop();
            if (!ChooseFolder())
            {
                if (!string.IsNullOrWhiteSpace(_config.DemosFolder)) _timer.Start();
                return;
            }
            StartOverlay();
            await Scan();
        };
        _scan.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_config.DemosFolder) && !ChooseFolder()) return;
            StartOverlay();
            foreach (var key in _data.ProcessedFiles.Where(entry => entry.Value == "erreur").Select(entry => entry.Key).ToList())
                _data.ProcessedFiles.Remove(key);
            await Scan();
        };
        _report.Click += (_, _) => Process.Start(new ProcessStartInfo(_store.ReportPath) { UseShellExecute = true });
        _overlay.Click += (_, _) => NativeOverlay.Toggle();
        _position.Click += (_, _) => NativeOverlay.ToggleLock();
        _export.Click += (_, _) => ExportBackup();
        _import.Click += (_, _) => ImportBackup();
        _addons.Click += (_, _) => ShowAddons();
        _settings.Click += (_, _) => ShowSettings();
        _profiles.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingProfiles || _profiles.SelectedItem is not Profile profile) return;
            _config.Profile = profile.Key;
            _store.SaveConfig(_config);
            RefreshStats();
            ReportWriter.WriteAll(_store, _config, _data);
        };
    }

    protected override bool ShowWithoutActivation => _startMinimized;

    /// <summary>Runs a backup action with the folder watcher paused, so nothing changes the history under it.</summary>
    private void WithScanPaused(Action action)
    {
        if (_scanning) return;
        var wasRunning = _timer.Enabled;
        _timer.Stop();
        try { action(); }
        finally { if (wasRunning && !_closing.IsCancellationRequested) _timer.Start(); }
    }

    private void ExportBackup() => WithScanPaused(() =>
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Exporter l’historique TrackChest",
            Filter = "Sauvegarde TrackChest (*.zip)|*.zip",
            FileName = $"TrackChest-sauvegarde-{DateTime.Now:yyyy-MM-dd}.zip",
            DefaultExt = "zip",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _store.SaveData(_data);
            _store.ExportBackup(dialog.FileName, _data);
            MessageBox.Show(this,
                $"Historique exporté : {L.Count(_data.Matches.Count, "partie", "parties", "match", "matches", "partida", "partidas")}.\n\n{dialog.FileName}\n\nGarde ce fichier hors du PC (clé USB, cloud) pour pouvoir le restaurer avec Importer.",
                "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Impossible d’écrire la sauvegarde.\n{ex.Message}", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    private void ImportBackup() => WithScanPaused(() =>
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Importer une sauvegarde TrackChest",
            Filter = "Sauvegarde TrackChest (*.zip)|*.zip",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var result = _store.ImportBackup(dialog.FileName, _data);
            _store.SaveData(_data);
            _reportDirty = true;
            RefreshStats();
            ReportWriter.WriteAll(_store, _config, _data);
            _reportDirty = false;
            _report.Enabled = File.Exists(_store.ReportPath);
            MessageBox.Show(this,
                $"Sauvegarde importée.\n\n{result.Added} parties ajoutées · {result.Updated} mises à jour · {result.Already} déjà présentes.",
                "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = Scan();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    private bool ChooseFolder()
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Choisis le dossier qui contient tes fichiers Fortnite .replay.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = _folder.Text,
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return false;
        _config.DemosFolder = picker.SelectedPath;
        _folder.Text = picker.SelectedPath;
        SetLiveReplay(null);
        _store.SaveConfig(_config);
        return true;
    }

    private void StartOverlay()
    {
        if (_overlayStarted) return;
        NativeOverlay.SetAppearance(_config.OverlaySize, _config.OverlayOpacity);
        NativeOverlay.Start(_config.OverlayX, _config.OverlayY, _config.OverlayLocked,
            (x, y) => OnUi(() => { _config.OverlayX = x; _config.OverlayY = y; _store.SaveConfig(_config); }),
            locked => OnUi(() => { _config.OverlayLocked = locked; _store.SaveConfig(_config); }));
        _overlayStarted = NativeOverlay.IsRunning;
    }

    private void OnUi(Action action)
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(action);
    }

    private async Task Scan()
    {
        if (_scanning || _closing.IsCancellationRequested) return;
        _scanning = true;
        _timer.Stop();
        _browse.Enabled = _scan.Enabled = _profiles.Enabled = _export.Enabled = _import.Enabled = false;
        IProgress<(int Done, int Total)> progress = new Progress<(int Done, int Total)>(p =>
            SetStatus("IMPORT EN COURS", $"Analyse des replays  ·  {p.Done + 1} / {p.Total}", Gold));
        try
        {
            var result = await Task.Run(() => _scanner.Scan(_closing.Token, (done, total) => progress.Report((done, total))));
            var liveChanged = SetLiveReplay(result.LiveReplay);
            if (_reportDirty || result.Changed || liveChanged) RefreshStats();
            else if (_inProgress) NativeOverlay.Update(BuildOverlay(Profiles.View(_data, _config)));
            if (_reportDirty || result.Changed)
            {
                ReportWriter.WriteAll(_store, _config, _data);
                _reportDirty = false;
            }
            _report.Enabled = File.Exists(_store.ReportPath);
            var message = result.FolderMissing ? "Dossier introuvable. Choisis l’emplacement des rediffusions."
                : result.Files == 0 ? "Aucun replay trouvé. Active l’enregistrement dans Fortnite."
                : $"{result.Files} replays dans le dossier  ·  {result.Incomplete} incomplets  ·  {result.Errors} illisibles";
            if (!_overlayStarted) message += "  ·  Overlay indisponible";
            if (result.FolderMissing || result.Errors > 0 || !_overlayStarted)
            {
                if (result.InProgress) message += "  ·  Partie en cours";
                SetStatus("À VÉRIFIER", message, Gold);
            }
            else if (result.InProgress) SetStatus("PARTIE EN COURS", message, Gold);
            else if (result.Files == 0) SetStatus("EN ATTENTE", message, Muted);
            else SetStatus("SUIVI ACTIF", message, Soft);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetStatus("À VÉRIFIER", $"Impossible d’analyser les replays. {ex.Message}", Gold);
        }
        finally
        {
            _scanning = false;
            _browse.Enabled = _scan.Enabled = _profiles.Enabled = _export.Enabled = _import.Enabled = true;
            if (_closing.IsCancellationRequested) Close();
            else _timer.Start();
        }
    }

    private void RefreshStats()
    {
        var profiles = Profiles.List(_data);
        var current = Profiles.Current(_data, _config);
        if (current != null && _config.Profile != current.Key)
        {
            _config.Profile = current.Key;
            _store.SaveConfig(_config);
        }
        _updatingProfiles = true;
        _profiles.DataSource = profiles;
        _profiles.SelectedItem = current;
        _updatingProfiles = false;

        var profileData = Profiles.View(_data, _config);
        var view = BuildOverlay(profileData);
        var last = profileData.Matches
            .OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        _totalValue.Text = view.Total;
        _lastValue.Text = last == null ? "—" : L.N(last.ChestsTotal);
        _pirateValue.Text = PirateTokenOdds.Format(profileData.Matches.Sum(m => m.ChestsOpened));
        NativeOverlay.Update(view);
    }

    private OverlayView BuildOverlay(TrackerData profileData) => OverlayModel.Build(profileData, _inProgress);

    private bool SetLiveReplay(string? path)
    {
        if (string.Equals(_liveReplay, path, StringComparison.OrdinalIgnoreCase)) return false;
        _liveReplay = path;
        _inProgress = path != null;
        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _closing.Cancel();
        e.Cancel = _scanning;
        base.OnFormClosing(e);
    }
}

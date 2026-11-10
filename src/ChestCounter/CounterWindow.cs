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
            Title = L.T("Exporter l’historique TrackChest", "Export the TrackChest history", "Exportar el historial de TrackChest", "TrackChest-Verlauf exportieren", "Экспорт истории TrackChest", "TrackChest の履歴をエクスポート", "Esporta la cronologia di TrackChest", "Exportar o histórico do TrackChest", "Eksportuj historię TrackChest", "De TrackChest-geschiedenis exporteren", "TrackChest geçmişini dışa aktar", "TrackChest 기록 내보내기", "导出 TrackChest 历史记录", "Експорт історії TrackChest", "Exportera TrackChest-historiken", "Exportovat historii TrackChest"),
            Filter = L.T("Sauvegarde TrackChest (*.zip)|*.zip", "TrackChest backup (*.zip)|*.zip", "Copia de seguridad de TrackChest (*.zip)|*.zip", "TrackChest-Sicherung (*.zip)|*.zip", "Резервная копия TrackChest (*.zip)|*.zip", "TrackChest バックアップ (*.zip)|*.zip", "Backup TrackChest (*.zip)|*.zip", "Backup do TrackChest (*.zip)|*.zip", "Kopia zapasowa TrackChest (*.zip)|*.zip", "TrackChest-back-up (*.zip)|*.zip", "TrackChest yedeği (*.zip)|*.zip", "TrackChest 백업 (*.zip)|*.zip", "TrackChest 备份 (*.zip)|*.zip", "Резервна копія TrackChest (*.zip)|*.zip", "TrackChest-säkerhetskopia (*.zip)|*.zip", "Záloha TrackChest (*.zip)|*.zip"),
            FileName = L.T($"TrackChest-sauvegarde-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-copia-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-Sicherung-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-kopia-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-back-up-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-yedek-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-备份-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-backup-{DateTime.Now:yyyy-MM-dd}.zip", $"TrackChest-zaloha-{DateTime.Now:yyyy-MM-dd}.zip"),
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
                L.T($"Historique exporté : {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nGarde ce fichier hors du PC (clé USB, cloud) pour pouvoir le restaurer avec Importer.", $"History exported: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nKeep this file off the PC (USB stick, cloud) so you can restore it with Import.", $"Historial exportado: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nGuarda este archivo fuera del PC (memoria USB, nube) para poder restaurarlo con Importar.", $"Verlauf exportiert: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nBewahre diese Datei außerhalb des PCs auf (USB-Stick, Cloud), um sie mit Importieren wiederherzustellen.", $"История экспортирована: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nХраните этот файл вне ПК (флешка, облако), чтобы восстановить его через «Импорт».", $"履歴をエクスポートしました: {L.Matches(_data.Matches.Count)}。\n\n{dialog.FileName}\n\n「インポート」で復元できるよう、このファイルは PC の外（USB メモリ、クラウド）に保管してください。", $"Cronologia esportata: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nConserva questo file fuori dal PC (chiavetta USB, cloud) per poterlo ripristinare con Importa.", $"Histórico exportado: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nGuarde este arquivo fora do PC (pendrive, nuvem) para poder restaurá-lo com Importar.", $"Historia wyeksportowana: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nPrzechowuj ten plik poza komputerem (pendrive, chmura), aby móc go przywrócić przez Importuj.", $"Geschiedenis geëxporteerd: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nBewaar dit bestand buiten de pc (usb-stick, cloud) zodat je het met Importeren kunt herstellen.", $"Geçmiş dışa aktarıldı: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nİçe Aktar ile geri yükleyebilmek için bu dosyayı bilgisayarın dışında (USB bellek, bulut) sakla.", $"기록을 내보냈습니다: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\n가져오기로 복원할 수 있도록 이 파일을 PC 밖(USB, 클라우드)에 보관하세요.", $"历史记录已导出：{L.Matches(_data.Matches.Count)}。\n\n{dialog.FileName}\n\n请将此文件保存在电脑之外（U 盘、云端），以便通过“导入”恢复。", $"Історію експортовано: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nЗберігайте цей файл поза ПК (флешка, хмара), щоб відновити його через «Імпорт».", $"Historiken exporterad: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nFörvara filen utanför datorn (USB-minne, moln) så att du kan återställa den med Importera.", $"Historie exportována: {L.Matches(_data.Matches.Count)}.\n\n{dialog.FileName}\n\nUchovávej tento soubor mimo počítač (flash disk, cloud), abys ho mohl obnovit přes Import."),
                "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.T($"Impossible d’écrire la sauvegarde.\n{ex.Message}", $"Could not write the backup.\n{ex.Message}", $"No se pudo escribir la copia de seguridad.\n{ex.Message}", $"Die Sicherung konnte nicht geschrieben werden.\n{ex.Message}", $"Не удалось записать резервную копию.\n{ex.Message}", $"バックアップを書き込めませんでした。\n{ex.Message}", $"Impossibile scrivere il backup.\n{ex.Message}", $"Não foi possível gravar o backup.\n{ex.Message}", $"Nie udało się zapisać kopii zapasowej.\n{ex.Message}", $"Kon de back-up niet schrijven.\n{ex.Message}", $"Yedek yazılamadı.\n{ex.Message}", $"백업을 쓸 수 없습니다.\n{ex.Message}", $"无法写入备份。\n{ex.Message}", $"Не вдалося записати резервну копію.\n{ex.Message}", $"Det gick inte att skriva säkerhetskopian.\n{ex.Message}", $"Zálohu se nepodařilo zapsat.\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    private void ImportBackup() => WithScanPaused(() =>
    {
        using var dialog = new OpenFileDialog
        {
            Title = L.T("Importer une sauvegarde TrackChest", "Import a TrackChest backup", "Importar una copia de seguridad de TrackChest", "TrackChest-Sicherung importieren", "Импорт резервной копии TrackChest", "TrackChest のバックアップをインポート", "Importa un backup di TrackChest", "Importar um backup do TrackChest", "Importuj kopię zapasową TrackChest", "Een TrackChest-back-up importeren", "TrackChest yedeğini içe aktar", "TrackChest 백업 가져오기", "导入 TrackChest 备份", "Імпорт резервної копії TrackChest", "Importera en TrackChest-säkerhetskopia", "Importovat zálohu TrackChest"),
            Filter = L.T("Sauvegarde TrackChest (*.zip)|*.zip", "TrackChest backup (*.zip)|*.zip", "Copia de seguridad de TrackChest (*.zip)|*.zip", "TrackChest-Sicherung (*.zip)|*.zip", "Резервная копия TrackChest (*.zip)|*.zip", "TrackChest バックアップ (*.zip)|*.zip", "Backup TrackChest (*.zip)|*.zip", "Backup do TrackChest (*.zip)|*.zip", "Kopia zapasowa TrackChest (*.zip)|*.zip", "TrackChest-back-up (*.zip)|*.zip", "TrackChest yedeği (*.zip)|*.zip", "TrackChest 백업 (*.zip)|*.zip", "TrackChest 备份 (*.zip)|*.zip", "Резервна копія TrackChest (*.zip)|*.zip", "TrackChest-säkerhetskopia (*.zip)|*.zip", "Záloha TrackChest (*.zip)|*.zip"),
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
                L.T($"Sauvegarde importée.\n\n{result.Added} parties ajoutées · {result.Updated} mises à jour · {result.Already} déjà présentes.", $"Backup imported.\n\n{result.Added} matches added · {result.Updated} updated · {result.Already} already there.", $"Copia importada.\n\n{result.Added} partidas añadidas · {result.Updated} actualizadas · {result.Already} ya presentes.", $"Sicherung importiert.\n\n{result.Added} Matches hinzugefügt · {result.Updated} aktualisiert · {result.Already} bereits vorhanden.", $"Резервная копия импортирована.\n\nДобавлено матчей: {result.Added} · обновлено: {result.Updated} · уже было: {result.Already}.", $"バックアップをインポートしました。\n\n追加 {result.Added} 試合 · 更新 {result.Updated} · 既存 {result.Already}", $"Backup importato.\n\n{result.Added} partite aggiunte · {result.Updated} aggiornate · {result.Already} già presenti.", $"Backup importado.\n\n{result.Added} partidas adicionadas · {result.Updated} atualizadas · {result.Already} já presentes.", $"Kopia zapasowa zaimportowana.\n\nDodane mecze: {result.Added} · zaktualizowane: {result.Updated} · już obecne: {result.Already}.", $"Back-up geïmporteerd.\n\n{result.Added} potjes toegevoegd · {result.Updated} bijgewerkt · {result.Already} al aanwezig.", $"Yedek içe aktarıldı.\n\n{result.Added} maç eklendi · {result.Updated} güncellendi · {result.Already} zaten vardı.", $"백업을 가져왔습니다.\n\n경기 {result.Added}개 추가 · {result.Updated}개 업데이트 · {result.Already}개 이미 있음.", $"备份已导入。\n\n新增 {result.Added} 场比赛 · 更新 {result.Updated} 场 · 已存在 {result.Already} 场。", $"Резервну копію імпортовано.\n\nДодано матчів: {result.Added} · оновлено: {result.Updated} · уже було: {result.Already}.", $"Säkerhetskopian importerad.\n\n{result.Added} matcher tillagda · {result.Updated} uppdaterade · {result.Already} fanns redan.", $"Záloha importována.\n\nPřidané zápasy: {result.Added} · aktualizované: {result.Updated} · již přítomné: {result.Already}."),
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
            Description = L.T("Choisis le dossier qui contient tes fichiers Fortnite .replay.", "Choose the folder that holds your Fortnite .replay files.", "Elige la carpeta que contiene tus archivos .replay de Fortnite.", "Wähle den Ordner mit deinen Fortnite-.replay-Dateien.", "Выберите папку с вашими файлами Fortnite .replay.", "Fortnite の .replay ファイルがあるフォルダーを選択してください。", "Scegli la cartella che contiene i tuoi file .replay di Fortnite.", "Escolha a pasta que contém seus arquivos .replay do Fortnite.", "Wybierz folder z plikami .replay z Fortnite.", "Kies de map met je Fortnite-.replaybestanden.", "Fortnite .replay dosyalarının bulunduğu klasörü seç.", "Fortnite .replay 파일이 있는 폴더를 선택하세요.", "选择存放 Fortnite .replay 文件的文件夹。", "Виберіть папку з вашими файлами Fortnite .replay.", "Välj mappen med dina Fortnite-.replay-filer.", "Vyber složku se soubory .replay z Fortnite."),
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
            SetStatus(L.T("IMPORT EN COURS", "IMPORTING", "IMPORTANDO", "IMPORT LÄUFT", "ИМПОРТ", "インポート中", "IMPORTAZIONE", "IMPORTANDO", "IMPORTOWANIE", "IMPORTEREN", "İÇE AKTARILIYOR", "가져오는 중", "正在导入", "ІМПОРТ", "IMPORTERAR", "IMPORT"), L.T($"Analyse des replays  ·  {p.Done + 1} / {p.Total}", $"Analyzing replays  ·  {p.Done + 1} / {p.Total}", $"Analizando replays  ·  {p.Done + 1} / {p.Total}", $"Replays werden analysiert  ·  {p.Done + 1} / {p.Total}", $"Анализ повторов  ·  {p.Done + 1} / {p.Total}", $"リプレイを分析中  ·  {p.Done + 1} / {p.Total}", $"Analisi dei replay  ·  {p.Done + 1} / {p.Total}", $"Analisando replays  ·  {p.Done + 1} / {p.Total}", $"Analiza powtórek  ·  {p.Done + 1} / {p.Total}", $"Replays analyseren  ·  {p.Done + 1} / {p.Total}", $"Tekrarlar analiz ediliyor  ·  {p.Done + 1} / {p.Total}", $"리플레이 분석 중  ·  {p.Done + 1} / {p.Total}", $"正在分析回放  ·  {p.Done + 1} / {p.Total}", $"Аналіз повторів  ·  {p.Done + 1} / {p.Total}", $"Analyserar repriser  ·  {p.Done + 1} / {p.Total}", $"Analýza záznamů  ·  {p.Done + 1} / {p.Total}"), Gold));
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
            var message = result.FolderMissing ? L.T("Dossier introuvable. Choisis l’emplacement des rediffusions.", "Folder not found. Choose where your replays are.", "Carpeta no encontrada. Elige dónde están tus repeticiones.", "Ordner nicht gefunden. Wähle den Speicherort deiner Wiederholungen.", "Папка не найдена. Укажите расположение повторов.", "フォルダーが見つかりません。リプレイの場所を選択してください。", "Cartella non trovata. Scegli dove si trovano i tuoi replay.", "Pasta não encontrada. Escolha onde estão seus replays.", "Nie znaleziono folderu. Wybierz, gdzie są twoje powtórki.", "Map niet gevonden. Kies waar je replays staan.", "Klasör bulunamadı. Tekrarlarının nerede olduğunu seç.", "폴더를 찾을 수 없습니다. 리플레이가 있는 위치를 선택하세요.", "未找到文件夹。请选择回放所在位置。", "Папку не знайдено. Виберіть, де лежать ваші повтори.", "Mappen hittades inte. Välj var dina repriser finns.", "Složka nenalezena. Vyber, kde jsou tvé záznamy.")
                : result.Files == 0 ? L.T("Aucun replay trouvé. Active l’enregistrement dans Fortnite.", "No replay found. Turn on recording in Fortnite.", "No se encontró ningún replay. Activa la grabación en Fortnite.", "Kein Replay gefunden. Aktiviere die Aufnahme in Fortnite.", "Повторы не найдены. Включите запись в Fortnite.", "リプレイが見つかりません。Fortnite で録画を有効にしてください。", "Nessun replay trovato. Attiva la registrazione in Fortnite.", "Nenhum replay encontrado. Ative a gravação no Fortnite.", "Nie znaleziono powtórek. Włącz nagrywanie w Fortnite.", "Geen replay gevonden. Zet opnemen aan in Fortnite.", "Tekrar bulunamadı. Fortnite'ta kaydı aç.", "리플레이가 없습니다. Fortnite에서 녹화를 켜세요.", "未找到回放。请在 Fortnite 中开启录制。", "Повторів не знайдено. Увімкніть запис у Fortnite.", "Ingen repris hittades. Slå på inspelning i Fortnite.", "Žádný záznam nenalezen. Zapni nahrávání ve Fortnite.")
                : L.T($"{result.Files} replays dans le dossier  ·  {result.Incomplete} incomplets  ·  {result.Errors} illisibles", $"{result.Files} replays in the folder  ·  {result.Incomplete} incomplete  ·  {result.Errors} unreadable", $"{result.Files} replays en la carpeta  ·  {result.Incomplete} incompletos  ·  {result.Errors} ilegibles", $"{result.Files} Replays im Ordner  ·  {result.Incomplete} unvollständig  ·  {result.Errors} nicht lesbar", $"Повторов в папке: {result.Files}  ·  неполных: {result.Incomplete}  ·  нечитаемых: {result.Errors}", $"フォルダー内のリプレイ {result.Files} 件  ·  不完全 {result.Incomplete}  ·  読み取り不可 {result.Errors}", $"{result.Files} replay nella cartella  ·  {result.Incomplete} incompleti  ·  {result.Errors} illeggibili", $"{result.Files} replays na pasta  ·  {result.Incomplete} incompletos  ·  {result.Errors} ilegíveis", $"Powtórki w folderze: {result.Files}  ·  niepełne: {result.Incomplete}  ·  nieczytelne: {result.Errors}", $"{result.Files} replays in de map  ·  {result.Incomplete} onvolledig  ·  {result.Errors} onleesbaar", $"Klasörde {result.Files} tekrar  ·  {result.Incomplete} eksik  ·  {result.Errors} okunamıyor", $"폴더의 리플레이 {result.Files}개  ·  불완전 {result.Incomplete}개  ·  읽을 수 없음 {result.Errors}개", $"文件夹中有 {result.Files} 个回放  ·  {result.Incomplete} 个不完整  ·  {result.Errors} 个无法读取", $"Повторів у папці: {result.Files}  ·  неповних: {result.Incomplete}  ·  нечитабельних: {result.Errors}", $"{result.Files} repriser i mappen  ·  {result.Incomplete} ofullständiga  ·  {result.Errors} olästa", $"Záznamy ve složce: {result.Files}  ·  neúplné: {result.Incomplete}  ·  nečitelné: {result.Errors}");
            if (!_overlayStarted) message += L.T("  ·  Overlay indisponible", "  ·  Overlay unavailable", "  ·  Overlay no disponible", "  ·  Overlay nicht verfügbar", "  ·  Оверлей недоступен", "  ·  オーバーレイ利用不可", "  ·  Overlay non disponibile", "  ·  Sobreposição indisponível", "  ·  Nakładka niedostępna", "  ·  Overlay niet beschikbaar", "  ·  Kaplama kullanılamıyor", "  ·  오버레이 사용 불가", "  ·  叠加层不可用", "  ·  Оверлей недоступний", "  ·  Överlägget är inte tillgängligt", "  ·  Překryv není k dispozici");
            if (result.FolderMissing || result.Errors > 0 || !_overlayStarted)
            {
                if (result.InProgress) message += L.T("  ·  Partie en cours", "  ·  Match in progress", "  ·  Partida en curso", "  ·  Match läuft", "  ·  Идёт матч", "  ·  試合中", "  ·  Partita in corso", "  ·  Partida em andamento", "  ·  Mecz w toku", "  ·  Potje bezig", "  ·  Maç sürüyor", "  ·  경기 진행 중", "  ·  比赛进行中", "  ·  Матч триває", "  ·  Match pågår", "  ·  Probíhá zápas");
                SetStatus(L.T("À VÉRIFIER", "CHECK", "REVISAR", "PRÜFEN", "ПРОВЕРИТЬ", "要確認", "CONTROLLA", "VERIFICAR", "SPRAWDŹ", "CONTROLEER", "KONTROL ET", "확인", "检查", "ПЕРЕВІРТЕ", "KONTROLLERA", "ZKONTROLUJ"), message, Gold);
            }
            else if (result.InProgress) SetStatus(L.T("PARTIE EN COURS", "MATCH IN PROGRESS", "PARTIDA EN CURSO", "MATCH LÄUFT", "ИДЁТ МАТЧ", "試合中", "PARTITA IN CORSO", "PARTIDA EM ANDAMENTO", "MECZ W TOKU", "POTJE BEZIG", "MAÇ SÜRÜYOR", "경기 진행 중", "比赛进行中", "МАТЧ ТРИВАЄ", "MATCH PÅGÅR", "PROBÍHÁ ZÁPAS"), message, Gold);
            else if (result.Files == 0) SetStatus(L.T("EN ATTENTE", "WAITING", "EN ESPERA", "WARTEN", "ОЖИДАНИЕ", "待機中", "IN ATTESA", "AGUARDANDO", "OCZEKIWANIE", "WACHTEN", "BEKLENİYOR", "대기 중", "等待中", "ОЧІКУВАННЯ", "VÄNTAR", "ČEKÁNÍ"), message, Muted);
            else SetStatus(L.T("SUIVI ACTIF", "TRACKING ON", "SEGUIMIENTO ACTIVO", "AKTIV", "ОТСЛЕЖИВАНИЕ", "追跡中", "MONITORAGGIO ATTIVO", "MONITORAMENTO ATIVO", "ŚLEDZENIE WŁĄCZONE", "VOLGEN AAN", "TAKİP AÇIK", "추적 중", "追踪已开启", "ВІДСТЕЖЕННЯ УВІМКНЕНО", "SPÅRNING PÅ", "SLEDOVÁNÍ ZAPNUTO"), message, Green);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetStatus(L.T("À VÉRIFIER", "CHECK", "REVISAR", "PRÜFEN", "ПРОВЕРИТЬ", "要確認", "CONTROLLA", "VERIFICAR", "SPRAWDŹ", "CONTROLEER", "KONTROL ET", "확인", "检查", "ПЕРЕВІРТЕ", "KONTROLLERA", "ZKONTROLUJ"), L.T($"Impossible d’analyser les replays. {ex.Message}", $"Could not analyze the replays. {ex.Message}", $"No se pudieron analizar los replays. {ex.Message}", $"Die Replays konnten nicht analysiert werden. {ex.Message}", $"Не удалось проанализировать повторы. {ex.Message}", $"リプレイを分析できませんでした。{ex.Message}", $"Impossibile analizzare i replay. {ex.Message}", $"Não foi possível analisar os replays. {ex.Message}", $"Nie udało się przeanalizować powtórek. {ex.Message}", $"Kon de replays niet analyseren. {ex.Message}", $"Tekrarlar analiz edilemedi. {ex.Message}", $"리플레이를 분석할 수 없습니다. {ex.Message}", $"无法分析回放。{ex.Message}", $"Не вдалося проаналізувати повтори. {ex.Message}", $"Det gick inte att analysera repriserna. {ex.Message}", $"Záznamy se nepodařilo analyzovat. {ex.Message}"), Gold);
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

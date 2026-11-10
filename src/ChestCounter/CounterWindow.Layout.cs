using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ChestCounter;

internal sealed partial class CounterWindow
{
    // Near-black canvas with a soft grey glow, translucent rounded cards, white primary button,
    // gold accents.
    private static readonly Color Canvas = Color.FromArgb(7, 9, 14);
    private static readonly Color Surface = Color.FromArgb(15, 19, 28);
    private static readonly Color Field = Color.FromArgb(10, 12, 17);
    private static readonly Color Border = Color.FromArgb(33, 37, 46);
    private static readonly Color BorderHover = Color.FromArgb(52, 56, 66);
    private static readonly Color Ink = Color.FromArgb(248, 250, 252);
    private static readonly Color Soft = Color.FromArgb(203, 213, 225);
    private static readonly Color Muted = Color.FromArgb(148, 163, 184);
    private static readonly Color Gold = Color.FromArgb(255, 200, 96);
    private static readonly Color Green = Color.FromArgb(74, 222, 128);

    private readonly TextBox _folder = new()
    {
        ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Field, ForeColor = Soft,
        Dock = DockStyle.Top,
    };
    private readonly ComboBox _profiles = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, DrawMode = DrawMode.OwnerDrawFixed,
        BackColor = Field, ForeColor = Ink, Dock = DockStyle.Fill, Margin = Padding.Empty,
        DisplayMember = nameof(Profile.Name),
    };
    private readonly PillButton _browse = new("");
    private readonly PillButton _scan = new("");
    private readonly PillButton _report = new("", Tone.Primary);
    private readonly PillButton _overlay = new("");
    private readonly PillButton _position = new("");
    private readonly PillButton _export = new("");
    private readonly PillButton _import = new("");
    private readonly PillButton _addons = new("");
    private readonly PillButton _settings = new("");
    private readonly Label _totalValue = TextLabel("—", 26, Gold, FontStyle.Bold);
    private readonly Label _lastValue = TextLabel("—", 26, Ink, FontStyle.Bold);
    private readonly Label _pirateValue = TextLabel("—", 26, Ink, FontStyle.Bold);
    private readonly PillLabel _statusTag = new() { ForeColor = Muted };
    private readonly Label _status = TextLabel("", 9, Muted);
    private readonly ToolTip _tips = new();
    private bool _windowReady;

    /// <summary>Texts of the controls that live as long as the window (the others are rebuilt with the layout).</summary>
    private void ApplyTexts()
    {
        Text = L.T("TrackChest · Compteur de coffres Fortnite", "TrackChest · Fortnite chest counter", "TrackChest · Contador de cofres de Fortnite",
            "TrackChest · Fortnite-Truhenzähler", "TrackChest · Счётчик сундуков Fortnite", "TrackChest · Fortnite 宝箱カウンター", "TrackChest · Contatore di forzieri Fortnite", "TrackChest · Contador de baús do Fortnite", "TrackChest · Licznik skrzyń w Fortnite", "TrackChest · Fortnite-kistenteller", "TrackChest · Fortnite sandık sayacı", "TrackChest · Fortnite 상자 카운터", "TrackChest · Fortnite 宝箱计数器", "TrackChest · Лічильник скринь Fortnite", "TrackChest · Kisträknare för Fortnite", "TrackChest · Počítadlo truhel ve Fortnite");
        _folder.AccessibleName = L.T("Dossier des fichiers replay", "Replay files folder", "Carpeta de archivos replay", "Ordner der Replay-Dateien", "Папка файлов повторов", "リプレイファイルのフォルダー", "Cartella dei file replay", "Pasta dos arquivos de replay", "Folder plików powtórek", "Map met replaybestanden", "Tekrar dosyaları klasörü", "리플레이 파일 폴더", "回放文件文件夹", "Папка файлів повторів", "Mapp för reprisfiler", "Složka se záznamy");
        _profiles.AccessibleName = L.T("Compte Fortnite", "Fortnite account", "Cuenta de Fortnite", "Fortnite-Konto", "Учётная запись Fortnite", "Fortnite アカウント", "Account Fortnite", "Conta do Fortnite", "Konto Fortnite", "Fortnite-account", "Fortnite hesabı", "Fortnite 계정", "Fortnite 账户", "Обліковий запис Fortnite", "Fortnite-konto", "Účet Fortnite");
        _browse.Text = L.T("Parcourir…", "Browse…", "Examinar…", "Durchsuchen…", "Обзор…", "参照…", "Sfoglia…", "Procurar…", "Przeglądaj…", "Bladeren…", "Gözat…", "찾아보기…", "浏览…", "Огляд…", "Bläddra…", "Procházet…");
        _scan.Text = L.T("Actualiser", "Refresh", "Actualizar", "Aktualisieren", "Обновить", "更新", "Aggiorna", "Atualizar", "Odśwież", "Vernieuwen", "Yenile", "새로 고침", "刷新", "Оновити", "Uppdatera", "Obnovit");
        _report.Text = L.T("Historique", "History", "Historial", "Verlauf", "История", "履歴", "Cronologia", "Histórico", "Historia", "Geschiedenis", "Geçmiş", "기록", "历史记录", "Історія", "Historik", "Historie");
        _overlay.Text = L.T("Afficher / masquer l’overlay", "Show / hide overlay", "Mostrar / ocultar overlay", "Overlay ein-/ausblenden", "Показать / скрыть оверлей", "オーバーレイ表示 / 非表示", "Mostra / nascondi overlay", "Mostrar / ocultar sobreposição", "Pokaż / ukryj nakładkę", "Overlay tonen / verbergen", "Kaplamayı göster / gizle", "오버레이 표시 / 숨기기", "显示 / 隐藏叠加层", "Показати / сховати оверлей", "Visa / dölj överlägg", "Zobrazit / skrýt překryv");
        _position.Text = L.T("Verrouiller / déplacer", "Lock / move", "Bloquear / mover", "Sperren / verschieben", "Закрепить / переместить", "ロック / 移動", "Blocca / sposta", "Travar / mover", "Zablokuj / przesuń", "Vergrendelen / verplaatsen", "Kilitle / taşı", "잠금 / 이동", "锁定 / 移动", "Заблокувати / перемістити", "Lås / flytta", "Zamknout / přesunout");
        _export.Text = L.T("Exporter", "Export", "Exportar", "Exportieren", "Экспорт", "エクスポート", "Esporta", "Exportar", "Eksportuj", "Exporteren", "Dışa aktar", "내보내기", "导出", "Експорт", "Exportera", "Exportovat");
        _import.Text = L.T("Importer", "Import", "Importar", "Importieren", "Импорт", "インポート", "Importa", "Importar", "Importuj", "Importeren", "İçe aktar", "가져오기", "导入", "Імпорт", "Importera", "Importovat");
        _addons.Text = L.T("Add-on", "Add-ons", "Add-ons", "Add-ons", "Дополнения", "アドオン", "Componenti", "Complementos", "Dodatki", "Add-ons", "Eklentiler", "애드온", "插件", "Доповнення", "Tillägg", "Doplňky");
        _settings.Text = L.T("Paramètres", "Settings", "Ajustes", "Einstellungen", "Настройки", "設定", "Impostazioni", "Configurações", "Ustawienia", "Instellingen", "Ayarlar", "설정", "设置", "Налаштування", "Inställningar", "Nastavení");
        _tips.SetToolTip(_export, L.T("Sauvegarde l’historique dans un fichier .zip, avant de réinitialiser le PC",
            "Saves the history to a .zip file, before resetting the PC", "Guarda el historial en un archivo .zip, antes de restablecer el PC",
            "Speichert den Verlauf in einer .zip-Datei, bevor du den PC zurücksetzt", "Сохраняет историю в файл .zip перед сбросом ПК",
            "PC をリセットする前に、履歴を .zip ファイルに保存します",
            "Salva la cronologia in un file .zip, prima di reinstallare il PC",
            "Salva o histórico em um arquivo .zip, antes de formatar o PC",
            "Zapisuje historię do pliku .zip, przed reinstalacją komputera",
            "Slaat de geschiedenis op in een .zip-bestand, voordat je de pc opnieuw installeert",
            "Bilgisayarı sıfırlamadan önce geçmişi bir .zip dosyasına kaydeder",
            "PC를 초기화하기 전에 기록을 .zip 파일로 저장합니다",
            "在重置电脑前将历史记录保存为 .zip 文件",
            "Зберігає історію у файл .zip перед перевстановленням ПК",
            "Sparar historiken i en .zip-fil, innan datorn återställs",
            "Uloží historii do souboru .zip před přeinstalací počítače"));
        _tips.SetToolTip(_import, L.T("Restaure ou ajoute l’historique d’une sauvegarde .zip", "Restores or adds the history from a .zip backup",
            "Restaura o añade el historial de una copia .zip", "Stellt den Verlauf aus einer .zip-Sicherung wieder her oder fügt ihn hinzu",
            "Восстанавливает или добавляет историю из резервной копии .zip", ".zip バックアップから履歴を復元または追加します", "Ripristina o aggiunge la cronologia da un backup .zip", "Restaura ou adiciona o histórico a partir de um backup .zip", "Przywraca lub dodaje historię z kopii zapasowej .zip", "Herstelt of voegt de geschiedenis toe uit een .zip-back-up", "Geçmişi bir .zip yedeğinden geri yükler veya ekler", ".zip 백업에서 기록을 복원하거나 추가합니다", "从 .zip 备份恢复或添加历史记录", "Відновлює або додає історію з резервної копії .zip", "Återställer eller lägger till historiken från en .zip-säkerhetskopia", "Obnoví nebo přidá historii ze zálohy .zip"));
    }

    /// <summary>Rebuilds the window in the language just chosen: same controls and data, new texts.</summary>
    private void RebuildForLanguage()
    {
        SuspendLayout();
        var old = Controls.Cast<Control>().ToList();
        Controls.Clear();
        BuildLayout();
        foreach (var control in old) control.Dispose();
        ResumeLayout(true);
    }

    private void BuildLayout()
    {
        ApplyTexts();
        if (!_windowReady) SetupWindow();
        BuildContent();
    }

    private void SetupWindow()
    {
        _windowReady = true;
        _statusTag.Text = L.T("À CONFIGURER", "SET UP", "CONFIGURAR", "EINRICHTEN", "НАСТРОЙКА", "未設定", "CONFIGURA", "CONFIGURAR", "KONFIGURUJ", "INSTELLEN", "AYARLA", "설정하기", "设置", "НАЛАШТУЙТЕ", "STÄLL IN", "NASTAV");
        _status.Text = L.T("Choisis le dossier de tes rediffusions Fortnite.", "Choose your Fortnite replays folder.", "Elige la carpeta de tus repeticiones de Fortnite.",
            "Wähle den Ordner deiner Fortnite-Wiederholungen.", "Выберите папку с повторами Fortnite.", "Fortnite のリプレイフォルダーを選択してください。", "Scegli la cartella dei replay di Fortnite.", "Escolha sua pasta de replays do Fortnite.", "Wybierz folder z powtórkami Fortnite.", "Kies je map met Fortnite-replays.", "Fortnite tekrar klasörünü seç.", "Fortnite 리플레이 폴더를 선택하세요.", "选择你的 Fortnite 回放文件夹。", "Виберіть папку з повторами Fortnite.", "Välj mappen med dina Fortnite-repriser.", "Vyber složku se záznamy Fortnite.");
        _profiles.DrawItem += DrawProfile;
        Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 670);
        MinimumSize = new Size(690, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Canvas;
        ForeColor = Ink;
        _report.Enabled = false;
    }

    private void BuildContent()
    {
        var scroll = new GlowPanel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = Grid([100], 118, 124, 204, 164);
        layout.BackColor = Color.Transparent;
        layout.Dock = DockStyle.Top;
        layout.Padding = new Padding(30);
        layout.Height = 670;
        Controls.Add(scroll);
        scroll.Controls.Add(layout);

        var hero = new HeroPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 11, 20, 9), Margin = new Padding(0, 0, 0, 18) };
        var heroGrid = Grid([100], 18, 38, 20);
        heroGrid.BackColor = Color.Transparent;
        heroGrid.Padding = new Padding(74, 0, 0, 0);
        heroGrid.Controls.Add(TextLabel(L.T("FORTNITE  ·  REDIFFUSIONS", "FORTNITE  ·  REPLAYS", "FORTNITE  ·  REPETICIONES", "FORTNITE  ·  WIEDERHOLUNGEN", "FORTNITE  ·  ПОВТОРЫ", "FORTNITE  ·  リプレイ", "FORTNITE  ·  REPLAY", "FORTNITE  ·  REPLAYS", "FORTNITE  ·  POWTÓRKI", "FORTNITE  ·  REPLAYS", "FORTNITE  ·  TEKRARLAR", "FORTNITE  ·  리플레이", "FORTNITE  ·  回放", "FORTNITE  ·  ПОВТОРИ", "FORTNITE  ·  REPRISER", "FORTNITE  ·  ZÁZNAMY"), 8.5f, Gold, FontStyle.Bold), 0, 0);
        heroGrid.Controls.Add(TextLabel("TrackChest", 22, Ink, FontStyle.Bold), 0, 1);
        heroGrid.Controls.Add(TextLabel(L.T("Compteur de coffres et suivi du jeton pirate", "Chest counter and pirate token tracker", "Contador de cofres y seguimiento de la ficha pirata", "Truhenzähler und Piraten-Token-Tracker", "Счётчик сундуков и отслеживание пиратского жетона", "宝箱カウンターと海賊トークンの追跡", "Contatore di forzieri e monitoraggio del gettone pirata", "Contador de baús e rastreador da ficha pirata", "Licznik skrzyń i śledzenie pirackiego żetonu", "Kistenteller en piratenmuntvolger", "Sandık sayacı ve korsan jetonu takibi", "상자 카운터 및 해적 토큰 추적기", "宝箱计数器和海盗代币追踪器", "Лічильник скринь і відстеження піратського жетона", "Kisträknare och spårning av piratpolletten", "Počítadlo truhel a sledování pirátského žetonu"), 9.5f, Muted), 0, 2);
        hero.Controls.Add(heroGrid);
        layout.Controls.Add(hero, 0, 0);

        var stats = Grid([34, 33, 33], 106);
        stats.Margin = new Padding(0, 0, 0, 18);
        stats.Controls.Add(Metric(L.T("TOTAL COFFRES", "TOTAL CHESTS", "TOTAL DE COFRES", "TRUHEN GESAMT", "ВСЕГО СУНДУКОВ", "宝箱の合計", "FORZIERI TOTALI", "TOTAL DE BAÚS", "SKRZYNIE ŁĄCZNIE", "TOTAAL KISTEN", "TOPLAM SANDIK", "총 상자", "宝箱总数", "УСЬОГО СКРИНЬ", "KISTOR TOTALT", "TRUHLY CELKEM"), _totalValue,
            L.T("Coffres et canons à butin", "Chests and loot cannons", "Cofres y cañones de botín", "Truhen und Beutekanonen", "Сундуки и пушки с добычей", "宝箱と戦利品キャノン", "Forzieri e cannoni del bottino", "Baús e canhões de saque", "Skrzynie i działa z łupem", "Kisten en buitkanonnen", "Sandıklar ve ganimet topları", "상자 및 전리품 대포", "宝箱和战利品大炮", "Скрині та гармати зі здобиччю", "Kistor och bytekanoner", "Truhly a kořistní děla"), new Padding(0, 0, 7, 0)), 0, 0);
        stats.Controls.Add(Metric(L.T("DERNIÈRE PARTIE", "LAST MATCH", "ÚLTIMA PARTIDA", "LETZTES MATCH", "ПОСЛЕДНИЙ МАТЧ", "前回の試合", "ULTIMA PARTITA", "ÚLTIMA PARTIDA", "OSTATNI MECZ", "LAATSTE POTJE", "SON MAÇ", "마지막 경기", "上一场比赛", "ОСТАННІЙ МАТЧ", "SENASTE MATCHEN", "POSLEDNÍ ZÁPAS"), _lastValue,
            L.T("Coffres comptés", "Chests counted", "Cofres contados", "Gezählte Truhen", "Подсчитано сундуков", "数えた宝箱", "Forzieri contati", "Baús contados", "Policzone skrzynie", "Getelde kisten", "Sayılan sandıklar", "집계된 상자", "已统计宝箱", "Пораховані скрині", "Räknade kistor", "Započítané truhly"), new Padding(4, 0, 4, 0)), 1, 0);
        stats.Controls.Add(Metric(L.T("JETON PIRATE", "PIRATE TOKEN", "FICHA PIRATA", "PIRATEN-TOKEN", "ПИРАТСКИЙ ЖЕТОН", "海賊トークン", "GETTONE PIRATA", "FICHA PIRATA", "PIRACKI ŻETON", "PIRATENMUNT", "KORSAN JETONU", "해적 토큰", "海盗代币", "ПІРАТСЬКИЙ ЖЕТОН", "PIRATPOLLETT", "PIRÁTSKÝ ŽETON"), _pirateValue,
            L.T("Probabilité cumulée estimée", "Estimated cumulative chance", "Probabilidad acumulada estimada", "Geschätzte Gesamtchance", "Оценочный суммарный шанс", "推定累積確率", "Probabilità cumulativa stimata", "Chance acumulada estimada", "Szacowana łączna szansa", "Geschatte cumulatieve kans", "Tahmini kümülatif şans", "추정 누적 확률", "估计累计概率", "Оцінена сукупна ймовірність", "Uppskattad sammanlagd chans", "Odhadovaná kumulativní šance"), new Padding(7, 0, 0, 0)), 2, 0);
        layout.Controls.Add(stats, 0, 1);

        var replays = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 18));
        var replayGrid = Grid([100], 30, 24, 38, 42, 30);
        var replayHeader = Grid([65, 35], 30);
        replayHeader.Controls.Add(TextLabel(L.T("Rediffusions", "Replays", "Repeticiones", "Wiederholungen", "Повторы", "リプレイ", "Replay", "Replays", "Powtórki", "Replays", "Tekrarlar", "리플레이", "回放", "Повтори", "Repriser", "Záznamy"), 13, Ink, FontStyle.Bold), 0, 0);
        replayHeader.Controls.Add(_statusTag, 1, 0);
        replayGrid.Controls.Add(replayHeader, 0, 0);
        replayGrid.Controls.Add(TextLabel(L.T("Dossier des fichiers .replay", ".replay files folder", "Carpeta de archivos .replay", "Ordner der .replay-Dateien", "Папка файлов .replay", ".replay ファイルのフォルダー", "Cartella dei file .replay", "Pasta dos arquivos .replay", "Folder plików .replay", "Map met .replaybestanden", ".replay dosyaları klasörü", ".replay 파일 폴더", ".replay 文件文件夹", "Папка файлів .replay", "Mapp för .replay-filer", "Složka souborů .replay"), 9, Muted), 0, 1);

        var folderRow = Grid([78, 22], 38);
        var folderFrame = FieldFrame(new Padding(12, 10, 12, 0), new Padding(0, 0, 8, 0));
        folderFrame.Controls.Add(_folder);
        folderRow.Controls.Add(folderFrame, 0, 0);
        folderRow.Controls.Add(_browse, 1, 0);
        replayGrid.Controls.Add(folderRow, 0, 2);

        var accountRow = Grid([19, 81], 38);
        accountRow.Margin = new Padding(0, 4, 0, 0);
        accountRow.Controls.Add(TextLabel(L.T("Compte Fortnite", "Fortnite account", "Cuenta de Fortnite", "Fortnite-Konto", "Аккаунт Fortnite", "アカウント", "Account Fortnite", "Conta do Fortnite", "Konto Fortnite", "Fortnite-account", "Fortnite hesabı", "Fortnite 계정", "Fortnite 账户", "Обліковий запис Fortnite", "Fortnite-konto", "Účet Fortnite"), 9, Muted), 0, 0);
        var profileFrame = FieldFrame(new Padding(6, 6, 6, 0), Padding.Empty);
        // The flat combo box draws a light border and a white arrow button: both are clipped out of a
        // field-coloured box, and a drawn chevron replaces the arrow.
        var profileClip = new Panel { Dock = DockStyle.Top, BackColor = Field };
        var chevron = new Chevron { Dock = DockStyle.Right, Width = 30 };
        chevron.Click += (_, _) =>
        {
            if (!_profiles.Enabled) return;
            _profiles.Focus();
            _profiles.DroppedDown = !_profiles.DroppedDown;
        };
        _profiles.EnabledChanged += (_, _) => chevron.Invalidate();
        _profiles.Dock = DockStyle.None;
        profileClip.Controls.Add(chevron);
        profileClip.Controls.Add(_profiles);
        void FitProfiles()
        {
            profileClip.Height = _profiles.Height - 2;
            var arrow = SystemInformation.VerticalScrollBarWidth + 4;
            _profiles.Bounds = new Rectangle(-1, -1, profileClip.Width - chevron.Width + arrow + 2, _profiles.Height);
            _profiles.DropDownWidth = Math.Max(1, profileClip.Width);
        }
        profileClip.Resize += (_, _) => FitProfiles();
        _profiles.FontChanged += (_, _) => FitProfiles();
        profileFrame.Controls.Add(profileClip);
        accountRow.Controls.Add(profileFrame, 1, 0);
        replayGrid.Controls.Add(accountRow, 0, 3);
        _status.AutoEllipsis = true;
        replayGrid.Controls.Add(_status, 0, 4);
        replays.Controls.Add(replayGrid);
        layout.Controls.Add(replays, 0, 2);

        var actions = Card(new Padding(18, 10, 18, 8), Padding.Empty);
        var actionGrid = Grid([100], 28, 38, 8, 38, 26);
        actionGrid.Controls.Add(TextLabel(L.T("Commandes", "Actions", "Acciones", "Aktionen", "Действия", "操作", "Azioni", "Ações", "Akcje", "Acties", "İşlemler", "작업", "操作", "Дії", "Åtgärder", "Akce"), 13, Ink, FontStyle.Bold), 0, 0);
        var buttons = Grid([19, 20, 31, 30], 38);
        _report.Margin = _scan.Margin = _overlay.Margin = new Padding(0, 0, 8, 0);
        buttons.Controls.Add(_report, 0, 0);
        buttons.Controls.Add(_scan, 1, 0);
        buttons.Controls.Add(_overlay, 2, 0);
        buttons.Controls.Add(_position, 3, 0);
        actionGrid.Controls.Add(buttons, 0, 1);
        var backupRow = Grid([19, 20, 31, 30], 38);
        _export.Margin = _import.Margin = _addons.Margin = new Padding(0, 0, 8, 0);
        backupRow.Controls.Add(_export, 0, 0);
        backupRow.Controls.Add(_import, 1, 0);
        backupRow.Controls.Add(_addons, 2, 0);
        backupRow.Controls.Add(_settings, 3, 0);
        actionGrid.Controls.Add(backupRow, 0, 3);
        actionGrid.Controls.Add(TextLabel(L.T("Plein écran fenêtré  ·  Ctrl+Maj+O pour afficher  ·  Ctrl+Maj+L pour déplacer",
            "Windowed fullscreen  ·  Ctrl+Shift+O to show  ·  Ctrl+Shift+L to move",
            "Pantalla completa en ventana  ·  Ctrl+Mayús+O para mostrar  ·  Ctrl+Mayús+L para mover",
            "Vollbild (Fenster)  ·  Strg+Umschalt+O zum Anzeigen  ·  Strg+Umschalt+L zum Verschieben",
            "Оконный полноэкранный режим  ·  Ctrl+Shift+O — показать  ·  Ctrl+Shift+L — переместить",
            "ウィンドウ フルスクリーン  ·  Ctrl+Shift+O で表示  ·  Ctrl+Shift+L で移動",
            "Schermo intero in finestra  ·  Ctrl+Shift+O per mostrare  ·  Ctrl+Shift+L per spostare",
            "Tela cheia em janela  ·  Ctrl+Shift+O para mostrar  ·  Ctrl+Shift+L para mover",
            "Pełny ekran w oknie  ·  Ctrl+Shift+O, aby pokazać  ·  Ctrl+Shift+L, aby przesunąć",
            "Venster op volledig scherm  ·  Ctrl+Shift+O om te tonen  ·  Ctrl+Shift+L om te verplaatsen",
            "Pencereli tam ekran  ·  Göstermek için Ctrl+Shift+O  ·  Taşımak için Ctrl+Shift+L",
            "창 전체 화면  ·  Ctrl+Shift+O로 표시  ·  Ctrl+Shift+L로 이동",
            "窗口化全屏  ·  Ctrl+Shift+O 显示  ·  Ctrl+Shift+L 移动",
            "Повноекранний у вікні  ·  Ctrl+Shift+O — показати  ·  Ctrl+Shift+L — перемістити",
            "Helskärm i fönster  ·  Ctrl+Shift+O för att visa  ·  Ctrl+Shift+L för att flytta",
            "Celá obrazovka v okně  ·  Ctrl+Shift+O pro zobrazení  ·  Ctrl+Shift+L pro přesun"), 9, Muted), 0, 4);
        actions.Controls.Add(actionGrid);
        layout.Controls.Add(actions, 0, 3);

        ActiveControl = _browse;
    }

    /// <summary>Paints the whole window at once (no flicker of the rounded cards while resizing).</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }

    private void DrawProfile(object? sender, DrawItemEventArgs e)
    {
        var inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
        var selected = inList && (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? BorderHover : Field);
        e.Graphics.FillRectangle(background, e.Bounds);
        if (e.Index < 0 || e.Index >= _profiles.Items.Count) return;

        var bounds = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, _profiles.GetItemText(_profiles.Items[e.Index]), _profiles.Font, bounds,
            _profiles.Enabled ? Ink : Muted,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void SetStatus(string tag, string message, Color accent)
    {
        _statusTag.Text = tag;
        _statusTag.ForeColor = accent;
        _status.Text = message;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        StyleTitleBar(Handle);
    }

    /// <summary>Dark title bar (Windows 11): same colour as the window, light text.</summary>
    private static void StyleTitleBar(IntPtr handle)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemInformation.HighContrast)
        {
            const int borderColor = 34;
            const int captionColor = 35;
            const int textColor = 36;
            var caption = ColorTranslator.ToWin32(Canvas);
            var text = ColorTranslator.ToWin32(Ink);
            var border = ColorTranslator.ToWin32(Border);
            DwmSetWindowAttribute(handle, borderColor, in border, sizeof(int));
            DwmSetWindowAttribute(handle, captionColor, in caption, sizeof(int));
            DwmSetWindowAttribute(handle, textColor, in text, sizeof(int));
        }
    }

    private static TableLayoutPanel Grid(int[] widths, params int[] heights)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = widths.Length, RowCount = heights.Length,
        };
        foreach (var width in widths) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        foreach (var height in heights) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        return grid;
    }

    private static RoundPanel Card(Padding padding, Padding margin) =>
        new() { Dock = DockStyle.Fill, Padding = padding, Margin = margin };

    private static RoundPanel FieldFrame(Padding padding, Padding margin) =>
        new() { Dock = DockStyle.Fill, Padding = padding, Margin = margin, BackColor = Field, Radius = 8 };

    private static RoundPanel Metric(string caption, Label value, string detail, Padding margin)
    {
        var card = Card(new Padding(18, 10, 16, 10), margin);
        var grid = Grid([100], 18, 48, 20);
        grid.Controls.Add(TextLabel(caption, 8.5f, Muted, FontStyle.Bold), 0, 0);
        grid.Controls.Add(value, 0, 1);
        grid.Controls.Add(TextLabel(detail, 9, Muted), 0, 2);
        card.Controls.Add(grid);
        return card;
    }

    private static Label TextLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, ForeColor = color, BackColor = Color.Transparent, Font = new Font("Segoe UI", size, style),
        Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft,
    };

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Alpha(Color color, int alpha) => Color.FromArgb(alpha, color);

    /// <summary>Paints what is behind a control (its parent, and theirs when transparent) so its rounded corners blend in.</summary>
    private sealed class ParentPainter : Control
    {
        private static readonly ParentPainter Instance = new();

        public static void Behind(Control child, Graphics g)
        {
            if (child.Parent is not { } parent) return;
            var state = g.Save();
            g.TranslateTransform(-child.Left, -child.Top);
            var area = new PaintEventArgs(g, child.Bounds);
            Instance.InvokePaintBackground(parent, area);
            Instance.InvokePaint(parent, area);
            g.Restore(state);
        }
    }

    /// <summary>Window background: near black with a soft grey glow top left and a faint blue one bottom right.</summary>
    private sealed class GlowPanel : Panel
    {
        public GlowPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Canvas;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Canvas);
            var w = Math.Max(1, ClientSize.Width);
            var h = Math.Max(1, ClientSize.Height);
            Glow(g, new RectangleF(-w * 0.55f, -h * 0.75f, w * 1.1f, h * 1.5f), Alpha(Muted, 22));
            Glow(g, new RectangleF(w * 0.45f, h * 0.25f, w * 1.1f, h * 1.5f), Color.FromArgb(16, 56, 97, 251));
        }

        private static void Glow(Graphics g, RectangleF area, Color center)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(area);
            using var brush = new PathGradientBrush(path) { CenterColor = center, SurroundColors = [Color.Transparent] };
            g.FillPath(brush, path);
        }
    }

    /// <summary>Rounded card with a thin light border.</summary>
    private class RoundPanel : Panel
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 12;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color Edge { get; set; } = Border;

        public RoundPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Surface;
        }

        protected float Dpi => DeviceDpi / 96f;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            ParentPainter.Behind(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = Rounded(bounds, Radius * Dpi);
            FillCard(e.Graphics, path, bounds);
            using var pen = new Pen(Edge);
            e.Graphics.DrawPath(pen, path);
        }

        protected virtual void FillCard(Graphics g, GraphicsPath path, RectangleF bounds)
        {
            using var fill = new SolidBrush(BackColor);
            g.FillPath(fill, path);
        }
    }

    /// <summary>Header card: soft grey gradient and the TrackChest chest.</summary>
    private sealed class HeroPanel : RoundPanel
    {
        private static readonly Bitmap? Logo = LoadLogo();

        protected override void FillCard(Graphics g, GraphicsPath path, RectangleF bounds)
        {
            using var fill = new LinearGradientBrush(bounds, Color.FromArgb(24, 28, 38), Surface, LinearGradientMode.Horizontal);
            fill.Blend = new Blend { Positions = [0f, 0.45f, 1f], Factors = [0f, 0.85f, 1f] };
            g.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var s = Dpi;
            var area = new RectangleF(20 * s, (Height - 58 * s) / 2f, 58 * s, 58 * s);
            if (Logo is { } logo)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, area);
            }
        }

        private static Bitmap? LoadLogo()
        {
            using var icon = AppIcon(128);
            return icon?.ToBitmap();
        }
    }

    private enum Tone { Normal, Primary }

    /// <summary>Small drawn icon before the label of a PillButton.</summary>
    private enum PillGlyph { None, Download, Check }

    /// <summary>Rounded button: white (primary) or translucent (normal).</summary>
    private sealed class PillButton : Button
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Tone Tone { get => _toneValue; set { _toneValue = value; Invalidate(); } }
        private Tone _toneValue;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public PillGlyph Glyph { get => _glyph; set { _glyph = value; Invalidate(); } }
        private PillGlyph _glyph;
        private bool _hover, _down;

        public PillButton(string text, Tone tone = Tone.Normal)
        {
            _toneValue = tone;
            Text = text;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI Semibold", 9.75f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ParentPainter.Behind(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var s = DeviceDpi / 96f;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = Rounded(bounds, 10 * s);

            Color fill, edge, text;
            if (!Enabled)
            {
                (fill, edge, text) = (Alpha(Color.White, 6), Alpha(Color.White, 14), Alpha(Muted, 150));
            }
            else
            {
                (fill, edge, text) = _toneValue switch
                {
                    Tone.Primary => (_down ? Soft : _hover ? Color.White : Ink, Color.Transparent, Canvas),
                    _ => (Alpha(Color.White, _down ? 4 : _hover ? 16 : 8), _hover ? BorderHover : Alpha(Color.White, 22), _hover ? Ink : Soft),
                };
            }

            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            if (edge.A > 0)
            {
                using var pen = new Pen(edge);
                g.DrawPath(pen, path);
            }
            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Alpha(Soft, 170), 1.5f * s);
                using var outer = Rounded(RectangleF.Inflate(bounds, -2 * s, -2 * s), 8 * s);
                g.DrawPath(ring, outer);
            }
            if (_glyph != PillGlyph.None && Text.Length > 0)
            {
                // Icon + label, centred together.
                var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                var textWidth = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags).Width;
                var iconWidth = 14f * s;
                var gap = 8f * s;
                var left = (Width - (iconWidth + gap + textWidth)) / 2f;
                var cx = left + iconWidth / 2f;
                var cy = Height / 2f;
                using (var pen = new Pen(text, 1.7f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (_glyph == PillGlyph.Download)
                    {
                        g.DrawLine(pen, cx, cy - 6f * s, cx, cy + 3f * s);
                        g.DrawLines(pen, [new PointF(cx - 4f * s, cy - 1f * s), new PointF(cx, cy + 3f * s), new PointF(cx + 4f * s, cy - 1f * s)]);
                        g.DrawLine(pen, cx - 6f * s, cy + 7f * s, cx + 6f * s, cy + 7f * s);
                    }
                    else
                    {
                        g.DrawLines(pen, [new PointF(cx - 5f * s, cy), new PointF(cx - 1.5f * s, cy + 3.5f * s), new PointF(cx + 5.5f * s, cy - 4f * s)]);
                    }
                }
                TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(left + iconWidth + gap), 0, (int)textWidth + 4, Height), text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | flags);
                return;
            }
            if (Text is "−" or "+")
            {
                // Drawn symbols: the font glyphs are too small and thin to look right.
                using var pen = new Pen(text, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var cx = Width / 2f;
                var cy = Height / 2f;
                var half = 5.5f * s;
                g.DrawLine(pen, cx - half, cy, cx + half, cy);
                if (Text == "+") g.DrawLine(pen, cx, cy - half, cx, cy + half);
                return;
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Drop-down chevron drawn over the field (replaces the system arrow button).</summary>
    private sealed class Chevron : Control
    {
        private bool _hover;

        public Chevron()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Field;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var s = DeviceDpi / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cx = Width / 2f;
            var cy = Height / 2f;
            using var pen = new Pen(_hover ? Ink : Muted, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, [new PointF(cx - 4.5f * s, cy - 2f * s), new PointF(cx, cy + 2.5f * s), new PointF(cx + 4.5f * s, cy - 2f * s)]);
        }
    }

    /// <summary>Status badge: tinted pill aligned right, coloured by ForeColor.</summary>
    private sealed class PillLabel : Label
    {
        public PillLabel()
        {
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(Text)) return;
            var g = e.Graphics;
            var s = DeviceDpi / 96f;
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var size = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags);
            var w = Math.Min(Width - 1, size.Width + 22 * s);
            var h = Math.Min(Height - 1, size.Height + 10 * s);
            var pill = new RectangleF(Width - 1 - w, (Height - h) / 2f, w, h);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(pill, h / 2f))
            {
                using var fill = new SolidBrush(Alpha(ForeColor, 26));
                using var pen = new Pen(Alpha(ForeColor, 102));
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(pill), ForeColor,
                flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>TrackChest icon (assets\TrackChest.ico) at the closest available size.</summary>
    internal static Icon? AppIcon(int size)
    {
        using var stream = typeof(CounterWindow).Assembly.GetManifestResourceStream("TrackChest.ico");
        return stream == null ? null : new Icon(stream, size, size);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, in int value, int size);
}

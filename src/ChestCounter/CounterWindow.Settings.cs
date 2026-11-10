using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ChestCounter;

internal sealed partial class CounterWindow
{

    private void ShowSettings()
    {
        // Choosing a language closes the window: everything is rebuilt in the new language, then it opens again.
        while (true)
        {
            using var form = new SettingsForm(_config, () => _store.SaveConfig(_config), () => _browse.PerformClick(), () => _ = RunUpdate(manual: true));
            form.ShowDialog(this);
            if (!form.LanguageChanged) return;
            ApplyLanguage();
        }
    }

    /// <summary>Redraws the window, the overlay and the report in the language just chosen.</summary>
    private void ApplyLanguage()
    {
        RebuildForLanguage();
        RefreshStats();
        _reportDirty = true;
        _ = Scan();
    }

    /// <summary>Settings window: overlay size and opacity (applied at once), replay folder, start with Fortnite.</summary>
    private sealed class SettingsForm : Form
    {
        private readonly Config _config;
        private readonly Action _save;
        private readonly Label _sizeValue = TextLabel("", 10, Ink, FontStyle.Bold);
        private readonly Label _opacityValue = TextLabel("", 10, Ink, FontStyle.Bold);
        private readonly PathLabel _folder = new()
        {
            UseMnemonic = false, BackColor = Color.Transparent, ForeColor = Soft,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = L.T("Dossier des fichiers replay", "Replay files folder", "Carpeta de archivos replay", "Ordner der Replay-Dateien", "Папка файлов повторов", "リプレイファイルのフォルダー"),
        };
        private readonly PillButton _startup = new("");
        private readonly PillButton _autoCheck = new("");
        private readonly PillButton _language = new("");
        private readonly ContextMenuStrip _languageMenu = new();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _languageMenu.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>True when a new language was chosen (the window closes so everything can be rebuilt).</summary>
        public bool LanguageChanged { get; private set; }

        public SettingsForm(Config config, Action save, Action browse, Action checkUpdate)
        {
            _config = config;
            _save = save;
            Text = L.T("TrackChest · Paramètres", "TrackChest · Settings", "TrackChest · Ajustes", "TrackChest · Einstellungen", "TrackChest · Настройки", "TrackChest · 設定");
            Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 730);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Canvas;
            ForeColor = Ink;

            var root = new GlowPanel { Dock = DockStyle.Fill };
            var layout = Grid([100], 44, 102, 144, 110, 102, 144, 40);
            layout.BackColor = Color.Transparent;
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(22);
            root.Controls.Add(layout);
            Controls.Add(root);

            layout.Controls.Add(TextLabel(L.T("Paramètres", "Settings", "Ajustes", "Einstellungen", "Настройки", "設定"), 16, Ink, FontStyle.Bold), 0, 0);

            // Language
            var language = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var languageGrid = Grid([100], 28, 38);
            languageGrid.Controls.Add(TextLabel(L.T("Langue", "Language", "Idioma", "Sprache", "Язык", "言語"), 13, Ink, FontStyle.Bold), 0, 0);
            var languageRow = SideRow();
            languageRow.Controls.Add(TextLabel(L.T("Langue de l’interface et de l’historique", "Language of the interface and history",
                "Idioma de la interfaz y del historial", "Sprache der Oberfläche und des Verlaufs", "Язык интерфейса и истории", "画面と履歴の言語"), 9.5f, Muted), 0, 0);
            _language.Text = L.Auto ? $"Auto · {L.NativeName(L.Code)}" : L.NativeName(L.Code);
            _language.Click += (_, _) => ShowLanguageMenu();
            languageRow.Controls.Add(_language, 1, 0);
            languageGrid.Controls.Add(languageRow, 0, 1);
            language.Controls.Add(languageGrid);
            layout.Controls.Add(language, 0, 1);

            // Overlay
            var overlay = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var overlayGrid = Grid([100], 28, 40, 40);
            overlayGrid.Controls.Add(TextLabel(L.T("Overlay", "Overlay", "Overlay", "Overlay", "Оверлей", "オーバーレイ"), 13, Ink, FontStyle.Bold), 0, 0);
            overlayGrid.Controls.Add(Stepper(L.T("Taille", "Size", "Tamaño", "Größe", "Размер", "サイズ"), _sizeValue, dir => ChangeSize(dir)), 0, 1);
            overlayGrid.Controls.Add(Stepper(L.T("Opacité", "Opacity", "Opacidad", "Deckkraft", "Непрозрачность", "不透明度"), _opacityValue, dir => ChangeOpacity(dir)), 0, 2);
            overlay.Controls.Add(overlayGrid);
            layout.Controls.Add(overlay, 0, 2);

            // Replays folder
            var replays = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var replayGrid = Grid([100], 28, 38);
            replayGrid.Controls.Add(TextLabel(L.T("Dossier des replays", "Replays folder", "Carpeta de replays", "Replay-Ordner", "Папка повторов", "リプレイフォルダー"), 13, Ink, FontStyle.Bold), 0, 0);
            var folderRow = SideRow();
            var folderFrame = FieldFrame(new Padding(12, 0, 12, 0), new Padding(0, 0, 8, 0));
            folderFrame.Controls.Add(_folder);
            var change = new PillButton(L.T("Modifier", "Change", "Cambiar", "Ändern", "Изменить", "変更"));
            change.Click += (_, _) =>
            {
                browse();
                _folder.Text = _config.DemosFolder;
            };
            folderRow.Controls.Add(folderFrame, 0, 0);
            folderRow.Controls.Add(change, 1, 0);
            replayGrid.Controls.Add(folderRow, 0, 1);
            replays.Controls.Add(replayGrid);
            layout.Controls.Add(replays, 0, 3);

            // Start with Fortnite
            var start = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var startGrid = Grid([100], 28, 38);
            startGrid.Controls.Add(TextLabel(L.T("Démarrage automatique", "Automatic start", "Inicio automático", "Automatischer Start", "Автозапуск", "自動起動"), 13, Ink, FontStyle.Bold), 0, 0);
            var startRow = SideRow();
            startRow.Controls.Add(TextLabel(L.T("Ouvre TrackChest avec Fortnite", "Opens TrackChest with Fortnite", "Abre TrackChest con Fortnite", "Öffnet TrackChest mit Fortnite", "Открывать TrackChest вместе с Fortnite", "Fortnite と一緒に TrackChest を起動"), 9.5f, Muted), 0, 0);
            _startup.Click += (_, _) => ToggleStartup();
            startRow.Controls.Add(_startup, 1, 0);
            startGrid.Controls.Add(startRow, 0, 1);
            start.Controls.Add(startGrid);
            layout.Controls.Add(start, 0, 4);

            // Updates
            var updates = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var updateGrid = Grid([100], 28, 38, 38);
            updateGrid.Controls.Add(TextLabel(L.T("Mises à jour", "Updates", "Actualizaciones", "Updates", "Обновления", "アップデート"), 13, Ink, FontStyle.Bold), 0, 0);
            var versionRow = SideRow();
            versionRow.Controls.Add(TextLabel($"{L.T("Version", "Version", "Versión", "Version", "Версия", "バージョン")} {Updater.CurrentText}", 9.5f, Muted), 0, 0);
            var check = new PillButton(L.T("Rechercher", "Check now", "Buscar", "Suchen", "Проверить", "確認"));
            check.Click += (_, _) => checkUpdate();
            versionRow.Controls.Add(check, 1, 0);
            updateGrid.Controls.Add(versionRow, 0, 1);
            var autoRow = SideRow();
            autoRow.Controls.Add(TextLabel(L.T("Vérifier au démarrage", "Check at startup", "Comprobar al iniciar", "Beim Start prüfen", "Проверять при запуске", "起動時に確認"), 9.5f, Muted), 0, 0);
            _autoCheck.Click += (_, _) => { _config.CheckUpdates = !_config.CheckUpdates; _save(); RefreshAutoCheck(); };
            autoRow.Controls.Add(_autoCheck, 1, 0);
            updateGrid.Controls.Add(autoRow, 0, 2);
            updates.Controls.Add(updateGrid);
            layout.Controls.Add(updates, 0, 5);

            var closeRow = SideRow();
            var close = new PillButton(L.T("Fermer", "Close", "Cerrar", "Schließen", "Закрыть", "閉じる"), Tone.Primary);
            close.Click += (_, _) => Close();
            closeRow.Controls.Add(close, 1, 0);
            layout.Controls.Add(closeRow, 0, 6);

            AcceptButton = close;
            _folder.Text = _config.DemosFolder;
            RefreshValues();
            RefreshStartup();
            RefreshAutoCheck();
        }

        private static string OnOff(bool on) => on
            ? L.T("Activé", "On", "Activado", "An", "Вкл.", "オン")
            : L.T("Désactivé", "Off", "Desactivado", "Aus", "Выкл.", "オフ");

        /// <summary>Menu under the language button: automatic (language of Windows) or one of the six languages.</summary>
        private void ShowLanguageMenu()
        {
            var menu = _languageMenu;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Font = Font;
            menu.Items.Clear();
            void Add(string text, string? code)
            {
                var current = code == null ? L.Auto : !L.Auto && L.Code == code;
                var item = new ToolStripMenuItem(text) { Checked = current, ForeColor = Ink };
                item.Click += (_, _) => ChooseLanguage(code);
                menu.Items.Add(item);
            }
            Add(L.T("Automatique", "Automatic", "Automático", "Automatisch", "Автоматически", "自動") + $" ({L.NativeName(L.SystemDefault)})", null);
            menu.Items.Add(new ToolStripSeparator());
            foreach (var code in L.Codes) Add(L.NativeName(code), code);
            menu.Show(_language, new Point(0, _language.Height));
        }

        private void ChooseLanguage(string? code)
        {
            if (code == null ? L.Auto : !L.Auto && L.Code == code) return;
            _config.Language = code;
            _save();
            L.Set(code);
            LanguageChanged = true;
            Close();
        }

        /// <summary>Dark colours for the language menu, matching the window.</summary>
        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Surface;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => BorderHover;
            public override Color MenuItemSelected => BorderHover;
            public override Color MenuItemSelectedGradientBegin => BorderHover;
            public override Color MenuItemSelectedGradientEnd => BorderHover;
            public override Color CheckBackground => Field;
            public override Color CheckSelectedBackground => Field;
            public override Color CheckPressedBackground => Field;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }

        private void RefreshAutoCheck()
        {
            _autoCheck.Text = OnOff(_config.CheckUpdates);
            _autoCheck.Tone = _config.CheckUpdates ? Tone.Primary : Tone.Normal;
        }

        private void RefreshValues()
        {
            _sizeValue.Text = $"{OverlayAppearance.ClampSize(_config.OverlaySize)} %";
            _opacityValue.Text = $"{OverlayAppearance.ClampOpacity(_config.OverlayOpacity)} %";
        }

        private static int Step(int[] values, int current, int direction)
        {
            var sorted = values.Order().ToArray();
            var nearest = 0;
            for (var i = 1; i < sorted.Length; i++)
            {
                if (Math.Abs(sorted[i] - current) < Math.Abs(sorted[nearest] - current)) nearest = i;
            }
            return sorted[Math.Clamp(nearest + direction, 0, sorted.Length - 1)];
        }

        private void ApplyAppearance()
        {
            NativeOverlay.SetAppearance(_config.OverlaySize, _config.OverlayOpacity);
            _save();
            RefreshValues();
        }

        private void ChangeSize(int direction)
        {
            _config.OverlaySize = Step(OverlayAppearance.Sizes, _config.OverlaySize, direction);
            ApplyAppearance();
        }

        private void ChangeOpacity(int direction)
        {
            _config.OverlayOpacity = Step(OverlayAppearance.Opacities, _config.OverlayOpacity, direction);
            ApplyAppearance();
        }

        private void ToggleStartup()
        {
            try
            {
                FortniteStartup.SetEnabled(!FortniteStartup.IsEnabled);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or COMException)
            {
                MessageBox.Show(this, L.T($"Impossible de modifier le démarrage automatique.\n{ex.Message}", $"Could not change the automatic start.\n{ex.Message}", $"No se pudo cambiar el inicio automático.\n{ex.Message}", $"Der automatische Start konnte nicht geändert werden.\n{ex.Message}", $"Не удалось изменить автозапуск.\n{ex.Message}", $"自動起動を変更できませんでした。\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshStartup();
        }

        private void RefreshStartup()
        {
            _startup.Text = OnOff(FortniteStartup.IsEnabled);
            _startup.Tone = FortniteStartup.IsEnabled ? Tone.Primary : Tone.Normal;
        }

        /// <summary>Folder path, vertically centred, shortened in the middle when too long.</summary>
        private sealed class PathLabel : Label
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>Row whose controls share one right column: every button of the window is ButtonWidth wide.</summary>
        private static TableLayoutPanel SideRow(int height = 38)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ButtonWidth));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            return row;
        }

        private const int ButtonWidth = 168;

        /// <summary>Row: label, then "−" button, value, "+" button in the right column.</summary>
        private static Control Stepper(string label, Label value, Action<int> change)
        {
            var row = SideRow(40);
            var steps = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 3, RowCount = 1 };
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            steps.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            value.TextAlign = ContentAlignment.MiddleCenter;
            var minus = new PillButton("−");
            var plus = new PillButton("+");
            minus.Margin = plus.Margin = new Padding(0, 3, 0, 3);
            minus.Click += (_, _) => change(-1);
            plus.Click += (_, _) => change(1);
            steps.Controls.Add(minus, 0, 0);
            steps.Controls.Add(value, 1, 0);
            steps.Controls.Add(plus, 2, 0);
            row.Controls.Add(TextLabel(label, 10, Soft), 0, 0);
            row.Controls.Add(steps, 1, 0);
            return row;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemInformation.HighContrast)
            {
                var border = ColorTranslator.ToWin32(Border);
                var caption = ColorTranslator.ToWin32(Canvas);
                var text = ColorTranslator.ToWin32(Ink);
                DwmSetWindowAttribute(Handle, 34, in border, sizeof(int));
                DwmSetWindowAttribute(Handle, 35, in caption, sizeof(int));
                DwmSetWindowAttribute(Handle, 36, in text, sizeof(int));
            }
        }
    }
}

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ChestCounter;

internal sealed partial class CounterWindow
{

    private void ShowSettings()
    {
        using var form = new SettingsForm(_config, () => _store.SaveConfig(_config), () => _browse.PerformClick(), () => _ = RunUpdate(manual: true));
        form.ShowDialog(this);
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
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Dossier des fichiers replay",
        };
        private readonly PillButton _startup = new("");
        private readonly PillButton _autoCheck = new("");

        public SettingsForm(Config config, Action save, Action browse, Action checkUpdate)
        {
            _config = config;
            _save = save;
            Text = "TrackChest · Paramètres";
            Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 628);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Canvas;
            ForeColor = Ink;

            var root = new GlowPanel { Dock = DockStyle.Fill };
            var layout = Grid([100], 44, 144, 110, 102, 144, 40);
            layout.BackColor = Color.Transparent;
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(22);
            root.Controls.Add(layout);
            Controls.Add(root);

            layout.Controls.Add(TextLabel("Paramètres", 16, Ink, FontStyle.Bold), 0, 0);

            // Overlay
            var overlay = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var overlayGrid = Grid([100], 28, 40, 40);
            overlayGrid.Controls.Add(TextLabel("Overlay", 13, Ink, FontStyle.Bold), 0, 0);
            overlayGrid.Controls.Add(Stepper("Taille", _sizeValue, dir => ChangeSize(dir)), 0, 1);
            overlayGrid.Controls.Add(Stepper("Opacité", _opacityValue, dir => ChangeOpacity(dir)), 0, 2);
            overlay.Controls.Add(overlayGrid);
            layout.Controls.Add(overlay, 0, 1);

            // Replays folder
            var replays = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var replayGrid = Grid([100], 28, 38);
            replayGrid.Controls.Add(TextLabel("Dossier des replays", 13, Ink, FontStyle.Bold), 0, 0);
            var folderRow = SideRow();
            var folderFrame = FieldFrame(new Padding(12, 0, 12, 0), new Padding(0, 0, 8, 0));
            folderFrame.Controls.Add(_folder);
            var change = new PillButton("Modifier");
            change.Click += (_, _) =>
            {
                browse();
                _folder.Text = _config.DemosFolder;
            };
            folderRow.Controls.Add(folderFrame, 0, 0);
            folderRow.Controls.Add(change, 1, 0);
            replayGrid.Controls.Add(folderRow, 0, 1);
            replays.Controls.Add(replayGrid);
            layout.Controls.Add(replays, 0, 2);

            // Start with Fortnite
            var start = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var startGrid = Grid([100], 28, 38);
            startGrid.Controls.Add(TextLabel("Démarrage automatique", 13, Ink, FontStyle.Bold), 0, 0);
            var startRow = SideRow();
            startRow.Controls.Add(TextLabel("Ouvre TrackChest avec Fortnite", 9.5f, Muted), 0, 0);
            _startup.Click += (_, _) => ToggleStartup();
            startRow.Controls.Add(_startup, 1, 0);
            startGrid.Controls.Add(startRow, 0, 1);
            start.Controls.Add(startGrid);
            layout.Controls.Add(start, 0, 3);

            // Updates
            var updates = Card(new Padding(18, 12, 18, 10), new Padding(0, 0, 0, 14));
            var updateGrid = Grid([100], 28, 38, 38);
            updateGrid.Controls.Add(TextLabel("Mises à jour", 13, Ink, FontStyle.Bold), 0, 0);
            var versionRow = SideRow();
            versionRow.Controls.Add(TextLabel($"Version {Updater.CurrentText}", 9.5f, Muted), 0, 0);
            var check = new PillButton("Rechercher");
            check.Click += (_, _) => checkUpdate();
            versionRow.Controls.Add(check, 1, 0);
            updateGrid.Controls.Add(versionRow, 0, 1);
            var autoRow = SideRow();
            autoRow.Controls.Add(TextLabel("Vérifier au démarrage", 9.5f, Muted), 0, 0);
            _autoCheck.Click += (_, _) => { _config.CheckUpdates = !_config.CheckUpdates; _save(); RefreshAutoCheck(); };
            autoRow.Controls.Add(_autoCheck, 1, 0);
            updateGrid.Controls.Add(autoRow, 0, 2);
            updates.Controls.Add(updateGrid);
            layout.Controls.Add(updates, 0, 4);

            var closeRow = SideRow();
            var close = new PillButton("Fermer", Tone.Primary);
            close.Click += (_, _) => Close();
            closeRow.Controls.Add(close, 1, 0);
            layout.Controls.Add(closeRow, 0, 5);

            AcceptButton = close;
            _folder.Text = _config.DemosFolder;
            RefreshValues();
            RefreshStartup();
            RefreshAutoCheck();
        }

        private void RefreshAutoCheck()
        {
            _autoCheck.Text = _config.CheckUpdates ? "Activé" : "Désactivé";
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
                MessageBox.Show(this, $"Impossible de modifier le démarrage automatique.\n{ex.Message}", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshStartup();
        }

        private void RefreshStartup()
        {
            _startup.Text = FortniteStartup.IsEnabled ? "Activé" : "Désactivé";
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

namespace ChestCounter;

internal sealed partial class CounterWindow
{
    /// <summary>
    /// "Configure" window of an add-on with counters: turns each of its counters on or off
    /// (e.g. Broken, Sprites, Wanted maps, Loot cannons). Every click is applied to the overlay at once, to try it out.
    /// </summary>
    private sealed class AddonConfigForm : Form
    {
        private readonly AddonStore _addons;
        private readonly string _id;
        private readonly Action _changed;
        private readonly List<(AddonCounter Counter, PillButton Button)> _counterToggles = new();
        private readonly List<Image> _images = new();

        public AddonConfigForm(AddonStore addons, string id, Action changed)
        {
            _addons = addons;
            _id = id;
            _changed = changed;
            var addon = Current() ?? throw new InvalidOperationException("Add-on introuvable.");

            Text = $"TrackChest · {addon.Manifest.Name}";
            Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 10);
            BackColor = Canvas;
            ForeColor = Ink;

            const int RowHeight = 44;
            var counters = addon.Manifest.Counters;
            // Padding 2×26, title 34, hint 36, counter card (32 + rows + 16 padding + 12 margin), buttons 50.
            ClientSize = new Size(460, 52 + 34 + 36 + 32 + counters.Count * RowHeight + 28 + 50 + 6);

            var root = new GlowPanel { Dock = DockStyle.Fill };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(26), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.Controls.Add(layout);
            Controls.Add(root);

            var title = TextLabel(addon.Manifest.Name, 15, Ink, FontStyle.Bold);
            title.AutoEllipsis = true;
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(TextLabel(L.T("Chaque changement s’applique tout de suite à l’overlay : essaie-le.", "Every change is applied to the overlay at once: try it out.", "Cada cambio se aplica al instante en el overlay: pruébalo.", "Jede Änderung gilt sofort im Overlay: probier es aus.", "Каждое изменение сразу применяется к оверлею: попробуйте.", "変更はすぐにオーバーレイに反映されます。試してみてください。", "Ogni modifica si applica subito all'overlay: provala.", "Cada alteração é aplicada na hora à sobreposição: experimente.", "Każda zmiana od razu trafia do nakładki: wypróbuj.", "Elke wijziging wordt meteen op de overlay toegepast: probeer het.", "Her değişiklik kaplamaya hemen uygulanır: dene.", "모든 변경 사항이 오버레이에 즉시 적용됩니다. 직접 해 보세요.", "每项更改都会立即应用到叠加层：试试看。", "Кожна зміна одразу застосовується до оверлею: спробуйте.", "Varje ändring syns direkt i överlägget: prova.", "Každá změna se hned projeví v překryvu: vyzkoušej to."), 9, Muted), 0, 1);

            // One switch per counter
            var list = Card(new Padding(16, 8, 12, 8), new Padding(0, 0, 0, 12));
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, ColumnCount = 1, RowCount = counters.Count + 1 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            grid.Controls.Add(TextLabel(L.T("Lignes de l’overlay", "Overlay lines", "Líneas del overlay", "Overlay-Zeilen", "Строки оверлея", "オーバーレイの行", "Righe dell'overlay", "Linhas da sobreposição", "Wiersze nakładki", "Overlayregels", "Kaplama satırları", "오버레이 줄", "叠加层行", "Рядки оверлею", "Överläggsrader", "Řádky překryvu"), 11, Ink, FontStyle.Bold), 0, 0);
            for (var i = 0; i < counters.Count; i++)
            {
                var counter = counters[i];
                var button = new PillButton("");
                button.Click += (_, _) =>
                {
                    if (Current() is not { } a) return;
                    _addons.SetCounterShown(_id, counter.Label, !a.IsCounterShown(counter));
                    Apply();
                };
                _counterToggles.Add((counter, button));
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
                grid.Controls.Add(ToggleRow(CounterIcon(addon, counter), counter.Label, button, RowHeight), 0, i + 1);
            }
            list.Controls.Add(grid);
            layout.Controls.Add(list, 0, 2);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var close = new PillButton(L.T("Fermer", "Close", "Cerrar", "Schließen", "Закрыть", "閉じる", "Chiudi", "Fechar", "Zamknij", "Sluiten", "Kapat", "닫기", "关闭", "Закрити", "Stäng", "Zavřít"), Tone.Primary)
            {
                Margin = new Padding(0, 8, 0, 0),
            };
            close.Click += (_, _) => Close();
            bottom.Controls.Add(close, 1, 0);
            layout.Controls.Add(bottom, 0, 3);
            AcceptButton = close;
            CancelButton = close;

            Refresh(addon);
        }

        private InstalledAddon? Current() => _addons.Load().FirstOrDefault(a => a.Manifest.Id == _id);

        /// <summary>Reloads the add-ons (the overlay is redrawn with the new lines) and shows the new state.</summary>
        private void Apply()
        {
            _changed();
            if (Current() is { } addon) Refresh(addon);
            else Close();
        }

        private void Refresh(InstalledAddon addon)
        {
            foreach (var (counter, button) in _counterToggles)
            {
                var shown = addon.IsCounterShown(counter);
                button.Text = OnOff(shown);
                button.Tone = shown && addon.IsActive ? Tone.Primary : Tone.Normal;
                button.Enabled = addon.IsActive; // the lines only show while the add-on is on
            }
        }

        private static string OnOff(bool on) => on
            ? L.T("Activé", "On", "Activado", "An", "Вкл.", "オン", "Attivo", "Ligado", "Wł.", "Aan", "Açık", "켜짐", "开", "Увімк.", "På", "Zap.")
            : L.T("Désactivé", "Off", "Desactivado", "Aus", "Выкл.", "オフ", "Disattivo", "Desligado", "Wył.", "Uit", "Kapalı", "꺼짐", "关", "Вимк.", "Av", "Vyp.");

        /// <summary>Icon of the counter: from the add-on package, else one built into TrackChest.</summary>
        private Image? CounterIcon(InstalledAddon addon, AddonCounter counter)
        {
            if (counter.Icon is not { } icon) return null;
            var file = icon.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? icon : icon + ".png";
            if (addon.Manifest.Icons.TryGetValue(file, out var bytes))
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    using var decoded = Image.FromStream(stream);
                    var image = new Bitmap(decoded);
                    _images.Add(image);
                    return image;
                }
                catch (ArgumentException) { }
            }
            return OverlayIcons.Get(file);
        }

        private static TableLayoutPanel ToggleRow(Image? icon, string text, PillButton button, int height, bool bold = false)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, ColumnCount = 3, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, icon != null ? 30 : 0));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            if (icon != null)
            {
                row.Controls.Add(new PictureBox
                {
                    Image = icon, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, BackColor = Color.Transparent,
                    Margin = new Padding(0, (height - 22) / 2, 8, (height - 22) / 2),
                }, 0, 0);
            }
            var label = TextLabel(text, 10.5f, Ink, bold ? FontStyle.Bold : FontStyle.Regular);
            label.AutoEllipsis = true;
            row.Controls.Add(label, 1, 0);
            button.Margin = new Padding(0, (height - 34) / 2, 0, (height - 34) / 2);
            row.Controls.Add(button, 2, 0);
            return row;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            StyleTitleBar(Handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var image in _images) image.Dispose();
            base.Dispose(disposing);
        }
    }
}

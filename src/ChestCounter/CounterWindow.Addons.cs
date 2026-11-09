using System.Diagnostics;

namespace ChestCounter;

internal sealed partial class CounterWindow
{
    private void ShowAddons()
    {
        var addons = new AddonStore(_store.Folder);
        using var form = new AddonsForm(_config, addons, () =>
        {
            AddonRuntime.Load(addons);
            if (!_scanning) RefreshStats();
        });
        form.ShowDialog(this);
    }

    /// <summary>Add-on window: installed add-ons (on/off, remove), the signed catalog, and a local file install.</summary>
    private sealed class AddonsForm : Form
    {
        private readonly Config _config;
        private readonly AddonStore _addons;
        private readonly Action _changed;
        private readonly FlowLayoutPanel _list = new()
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty,
        };
        private readonly Label _status = TextLabel("", 9, Muted);
        private readonly CancellationTokenSource _closing = new();
        private CatalogIndex? _catalog;
        private string _catalogMessage = "";
        private bool _busy;

        public AddonsForm(Config config, AddonStore addons, Action changed)
        {
            _config = config;
            _addons = addons;
            _changed = changed;
            Text = "TrackChest · Add-on";
            Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(660, 600);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 10);
            BackColor = Canvas;
            ForeColor = Ink;

            var root = new GlowPanel { Dock = DockStyle.Fill };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(26), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.Controls.Add(layout);
            Controls.Add(root);

            var head = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            head.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var guide = new PillButton("Créer un add-on") { Margin = new Padding(0, 4, 0, 4) };
            guide.Click += (_, _) => OpenGuide();
            head.Controls.Add(TextLabel("Add-on", 16, Ink, FontStyle.Bold), 0, 0);
            head.Controls.Add(guide, 1, 0);
            layout.Controls.Add(head, 0, 0);
            layout.Controls.Add(TextLabel("Compteurs et thèmes pour l’overlay. Aucun code : seulement des réglages vérifiés.", 9, Muted), 0, 1);
            layout.Controls.Add(_list, 0, 2);
            _list.SizeChanged += (_, _) => FitRows();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), ColumnCount = 4, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var refresh = new PillButton("Actualiser") { Margin = new Padding(0, 0, 8, 0) };
            var file = new PillButton("Ajouter un fichier…") { Margin = new Padding(0, 0, 8, 0) };
            var close = new PillButton("Fermer", Tone.Primary);
            refresh.Click += async (_, _) => await RefreshCatalog(showErrors: true);
            file.Click += (_, _) => InstallFromFile();
            close.Click += (_, _) => Close();
            bottom.Controls.Add(_status, 0, 0);
            bottom.Controls.Add(refresh, 1, 0);
            bottom.Controls.Add(file, 2, 0);
            bottom.Controls.Add(close, 3, 0);
            layout.Controls.Add(bottom, 0, 3);

            AcceptButton = close;
            Rebuild();
            Shown += async (_, _) => await RefreshCatalog(showErrors: false);
            FormClosing += (_, _) => _closing.Cancel();
        }

        /// <summary>Opens the developer guide published next to the catalog (same site, root page).</summary>
        private void OpenGuide()
        {
            var url = AddonCatalog.ResolveUrl(_config);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                MessageBox.Show(this, "Aucun catalogue n’est configuré, donc pas de guide en ligne.\nLe guide est dans le dossier addons-registry (README.md et site-src\\index.html).", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var guideUrl = new Uri(uri, "./").ToString();
            Process.Start(new ProcessStartInfo(guideUrl) { UseShellExecute = true });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            StyleTitleBar(Handle);
        }

        // ---- catalog ------------------------------------------------------------------------
        private async Task RefreshCatalog(bool showErrors)
        {
            if (_busy) return;
            var url = AddonCatalog.ResolveUrl(_config);
            if (url.Length == 0)
            {
                _catalog = null;
                _catalogMessage = "Aucun catalogue configuré. Tu peux quand même ajouter un fichier d’add-on.";
                Rebuild();
                return;
            }

            _busy = true;
            _status.Text = "Lecture du catalogue…";
            try
            {
                _catalog = await AddonCatalog.FetchIndexAsync(url, _closing.Token);
                _catalogMessage = _catalog.Addons.Count == 0 ? "Le catalogue est vide." : "";
                var revoked = _addons.ApplyRevocations(_catalog);
                if (revoked.Count > 0)
                {
                    _changed();
                    MessageBox.Show(this, $"Désactivés car révoqués par le catalogue :\n• {string.Join("\n• ", revoked)}", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                _status.Text = "";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or TaskCanceledException)
            {
                _catalog = null;
                _catalogMessage = $"Catalogue indisponible : {ex.Message}";
                _status.Text = "";
                if (showErrors) MessageBox.Show(this, ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { _busy = false; }
            if (!IsDisposed) Rebuild();
        }

        private async Task InstallFromCatalog(CatalogEntry entry)
        {
            if (_busy || _catalog == null) return;
            _busy = true;
            _status.Text = "Téléchargement…";
            try
            {
                var (zip, _) = await AddonCatalog.DownloadAsync(entry, _catalog, _closing.Token);
                _addons.Install(zip, entry.TierValue);
                _changed();
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or TaskCanceledException)
            {
                MessageBox.Show(this, ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { _busy = false; _status.Text = ""; }
        }

        private void InstallFromFile()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Ajouter un add-on", Filter = "Add-on TrackChest (*.zip)|*.zip", CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var zip = File.ReadAllBytes(dialog.FileName);
                var manifest = AddonPackage.Read(zip);
                if (!Confirm(manifest.Name, manifest.Version, manifest.Author, manifest.Permissions, AddonTier.Local)) return;
                _addons.Install(zip, AddonTier.Local);
                _changed();
                Rebuild();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Shows who, which version and what the add-on may do, before anything is installed.</summary>
        private bool Confirm(string name, string version, string author, IEnumerable<string> permissions, AddonTier tier)
        {
            var origin = tier switch
            {
                AddonTier.Verified => "Add-on vérifié : relu et signé par le catalogue TrackChest.",
                AddonTier.Community => "Add-on de la communauté : contrôlé automatiquement, mais pas relu.",
                _ => "Add-on local NON vérifié : n’installe que des fichiers dont tu connais l’auteur.",
            };
            var text = $"Installer « {name} » {version} (par {author}) ?\n\nCet add-on pourra seulement :\n• {string.Join("\n• ", permissions.Select(AddonRules.Describe))}\n\n{origin}\nIl ne contient aucun code et ne peut ni lire tes fichiers ni utiliser Internet.";
            return MessageBox.Show(this, text, "TrackChest", MessageBoxButtons.YesNo,
                tier == AddonTier.Verified ? MessageBoxIcon.Question : MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        // ---- list ---------------------------------------------------------------------------
        private void Rebuild()
        {
            _list.SuspendLayout();
            foreach (Control old in _list.Controls) old.Dispose();
            _list.Controls.Clear();

            var installed = _addons.Load();
            _list.Controls.Add(Header($"Installés ({installed.Count})"));
            if (installed.Count == 0) _list.Controls.Add(Note("Aucun add-on installé."));
            foreach (var addon in installed)
            {
                var toggle = new PillButton(addon.IsActive ? "Désactiver" : "Activer") { Enabled = !addon.State.Revoked };
                var remove = new PillButton("Supprimer");
                toggle.Click += (_, _) => { _addons.SetEnabled(addon.Manifest.Id, !addon.IsActive); _changed(); Rebuild(); };
                remove.Click += (_, _) =>
                {
                    if (MessageBox.Show(this, $"Supprimer « {addon.Manifest.Name} » ?", "TrackChest", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _addons.Remove(addon.Manifest.Id);
                    _changed();
                    Rebuild();
                };
                var state = addon.State.Revoked ? "RÉVOQUÉ · " : addon.IsActive ? "" : "désactivé · ";
                _list.Controls.Add(Row(addon.Manifest.Name, addon.Manifest.Version, $"{state}{TierText(addon.Tier)} · par {addon.Manifest.Author}",
                    TierColor(addon.Tier), addon.Manifest.Description, toggle, remove));
            }

            _list.Controls.Add(Header("Catalogue"));
            if (_catalog == null) _list.Controls.Add(Note(_catalogMessage.Length > 0 ? _catalogMessage : "Lecture du catalogue…"));
            else
            {
                if (_catalogMessage.Length > 0) _list.Controls.Add(Note(_catalogMessage));
                foreach (var entry in _catalog.Addons.Where(e => !_catalog.IsRevoked(e.Id, e.Version)))
                {
                    var current = installed.FirstOrDefault(a => a.Manifest.Id == entry.Id);
                    var isCurrent = current != null && current.Manifest.Version == entry.Version;
                    var button = new PillButton(isCurrent ? "Installé" : current != null ? "Mettre à jour" : "Installer", isCurrent ? Tone.Normal : Tone.Primary)
                    {
                        Enabled = !isCurrent,
                        Glyph = isCurrent ? PillGlyph.Check : PillGlyph.Download,
                    };
                    button.Click += async (_, _) =>
                    {
                        if (_busy || !Confirm(entry.Name, entry.Version, entry.Author, entry.Permissions, entry.TierValue)) return;
                        // Immediate feedback while the file is downloaded and checked.
                        button.Enabled = false;
                        button.Glyph = PillGlyph.None;
                        button.Text = "Installation…";
                        await InstallFromCatalog(entry);
                        if (!IsDisposed) Rebuild();   // back to "Installé" (or to "Installer" after an error)
                    };
                    _list.Controls.Add(Row(entry.Name, entry.Version, $"{TierText(entry.TierValue)} · par {entry.Author}", TierColor(entry.TierValue), entry.Description, button));
                }
            }
            _list.ResumeLayout(true);
            FitRows();
        }

        private static string TierText(AddonTier tier) => tier switch { AddonTier.Verified => "✓ Vérifié", AddonTier.Community => "Communauté", _ => "Local, non vérifié" };
        private static Color TierColor(AddonTier tier) => tier switch { AddonTier.Verified => Ink, AddonTier.Community => Gold, _ => Muted };

        private static Label Header(string text) => new()
        {
            Text = text, ForeColor = Ink, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = false, Height = 34,
            TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 6, 0, 6),
        };

        private static Label Note(string text) => new()
        {
            Text = text, ForeColor = Muted, Font = new Font("Segoe UI", 9.5f), AutoSize = false, Height = 40,
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2, 0, 0, 6),
        };

        private static RoundPanel Row(string name, string version, string line2, Color line2Color, string description, params PillButton[] buttons)
        {
            var row = new RoundPanel { Height = 92, Padding = new Padding(16, 10, 14, 10), Margin = new Padding(0, 0, 0, 10) };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var title = TextLabel($"{name}  ·  {version}", 11, Ink, FontStyle.Bold);
            title.AutoEllipsis = true;
            var origin = TextLabel(line2, 9, line2Color);
            origin.AutoEllipsis = true;
            var desc = TextLabel(description, 9, Muted);
            desc.AutoEllipsis = true;
            desc.TextAlign = ContentAlignment.TopLeft;
            text.Controls.Add(title, 0, 0);
            text.Controls.Add(origin, 0, 1);
            text.Controls.Add(desc, 0, 2);
            grid.Controls.Add(text, 0, 0);

            // Compact buttons (36 px) centred in the row, instead of stretched to its whole height.
            var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = buttons.Length + 2, BackColor = Color.Transparent };
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var buttonHeight = buttons.Length == 1 ? 36 : 32;   // two stacked buttons must fit in the row
            for (var i = 0; i < buttons.Length; i++)
            {
                side.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHeight + (i < buttons.Length - 1 ? 6 : 0)));
                buttons[i].Margin = new Padding(0, 0, 0, i < buttons.Length - 1 ? 6 : 0);
                side.Controls.Add(buttons[i], 0, i + 1);
            }
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.Controls.Add(side, 1, 0);
            row.Controls.Add(grid);
            return row;
        }

        private void FitRows()
        {
            var width = _list.ClientSize.Width - 2;
            if (width <= 0) return;
            foreach (Control c in _list.Controls) c.Width = width;
        }
    }
}

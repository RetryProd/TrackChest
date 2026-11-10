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
            var guide = new PillButton(L.T("Créer un add-on", "Create an add-on", "Crear un add-on", "Add-on erstellen", "Создать дополнение", "アドオンを作成")) { Margin = new Padding(0, 4, 0, 4) };
            guide.Click += (_, _) => OpenGuide();
            head.Controls.Add(TextLabel("Add-on", 16, Ink, FontStyle.Bold), 0, 0);
            head.Controls.Add(guide, 1, 0);
            layout.Controls.Add(head, 0, 0);
            layout.Controls.Add(TextLabel(L.T("Compteurs, thèmes et scripts pour l’overlay. Les scripts tournent dans un bac à sable.", "Counters, themes and scripts for the overlay. Scripts run in a sandbox.", "Contadores, temas y scripts para el overlay. Los scripts se ejecutan en un entorno aislado.", "Zähler, Designs und Skripte für das Overlay. Skripte laufen in einer Sandbox.", "Счётчики, темы и скрипты для оверлея. Скрипты работают в песочнице.", "オーバーレイ用のカウンター、テーマ、スクリプト。スクリプトはサンドボックスで実行されます。"), 9, Muted), 0, 1);
            layout.Controls.Add(_list, 0, 2);
            _list.SizeChanged += (_, _) => FitRows();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), ColumnCount = 4, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var refresh = new PillButton(L.T("Actualiser", "Refresh", "Actualizar", "Aktualisieren", "Обновить", "更新")) { Margin = new Padding(0, 0, 8, 0) };
            var file = new PillButton(L.T("Ajouter un fichier…", "Add a file…", "Añadir un archivo…", "Datei hinzufügen…", "Добавить файл…", "ファイルを追加…")) { Margin = new Padding(0, 0, 8, 0) };
            var close = new PillButton(L.T("Fermer", "Close", "Cerrar", "Schließen", "Закрыть", "閉じる"), Tone.Primary);
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
                MessageBox.Show(this, L.T("Aucun catalogue n’est configuré, donc pas de guide en ligne.\nLe guide est dans le dossier addons-registry (README.md et site-src\\index.html).", "No catalog is configured, so there is no online guide.\nThe guide is in the addons-registry folder (README.md and site-src\\index.html).", "No hay ningún catálogo configurado, así que no hay guía en línea.\nLa guía está en la carpeta addons-registry (README.md y site-src\\index.html).", "Kein Katalog eingerichtet, daher keine Online-Anleitung.\nDie Anleitung liegt im Ordner addons-registry (README.md und site-src\\index.html).", "Каталог не настроен, поэтому онлайн-руководства нет.\nРуководство находится в папке addons-registry (README.md и site-src\\index.html).", "カタログが設定されていないため、オンラインガイドはありません。\nガイドは addons-registry フォルダーにあります（README.md と site-src\\index.html）。"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _catalogMessage = L.T("Aucun catalogue configuré. Tu peux quand même ajouter un fichier d’add-on.", "No catalog configured. You can still add an add-on file.", "No hay catálogo configurado. Aun así puedes añadir un archivo de add-on.", "Kein Katalog eingerichtet. Du kannst trotzdem eine Add-on-Datei hinzufügen.", "Каталог не настроен. Вы всё равно можете добавить файл дополнения.", "カタログが設定されていません。アドオンファイルは追加できます。");
                Rebuild();
                return;
            }

            _busy = true;
            _status.Text = L.T("Lecture du catalogue…", "Reading the catalog…", "Leyendo el catálogo…", "Katalog wird gelesen…", "Чтение каталога…", "カタログを読み込み中…");
            try
            {
                _catalog = await AddonCatalog.FetchIndexAsync(url, _closing.Token);
                _catalogMessage = _catalog.Addons.Count == 0 ? L.T("Le catalogue est vide.", "The catalog is empty.", "El catálogo está vacío.", "Der Katalog ist leer.", "Каталог пуст.", "カタログは空です。") : "";
                var revoked = _addons.ApplyRevocations(_catalog);
                if (revoked.Count > 0)
                {
                    _changed();
                    MessageBox.Show(this, L.T($"Désactivés car révoqués par le catalogue :\n• {string.Join("\n• ", revoked)}", $"Disabled because the catalog revoked them:\n• {string.Join("\n• ", revoked)}", $"Desactivados porque el catálogo los revocó:\n• {string.Join("\n• ", revoked)}", $"Deaktiviert, da vom Katalog widerrufen:\n• {string.Join("\n• ", revoked)}", $"Отключены, так как отозваны каталогом:\n• {string.Join("\n• ", revoked)}", $"カタログで取り消されたため無効にしました:\n• {string.Join("\n• ", revoked)}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                _status.Text = "";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or TaskCanceledException)
            {
                _catalog = null;
                _catalogMessage = L.T($"Catalogue indisponible : {ex.Message}", $"Catalog unavailable: {ex.Message}", $"Catálogo no disponible: {ex.Message}", $"Katalog nicht verfügbar: {ex.Message}", $"Каталог недоступен: {ex.Message}", $"カタログを利用できません: {ex.Message}");
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
            _status.Text = L.T("Téléchargement…", "Downloading…", "Descargando…", "Wird heruntergeladen…", "Загрузка…", "ダウンロード中…");
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
                Title = L.T("Ajouter un add-on", "Add an add-on", "Añadir un add-on", "Add-on hinzufügen", "Добавить дополнение", "アドオンを追加"), Filter = L.T("Add-on TrackChest (*.zip)|*.zip", "TrackChest add-on (*.zip)|*.zip", "Add-on de TrackChest (*.zip)|*.zip", "TrackChest-Add-on (*.zip)|*.zip", "Дополнение TrackChest (*.zip)|*.zip", "TrackChest アドオン (*.zip)|*.zip"), CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var zip = File.ReadAllBytes(dialog.FileName);
                var manifest = AddonPackage.Read(zip);
                if (!Confirm(manifest.Name, manifest.Version, manifest.Permissions, AddonTier.Local)) return;
                _addons.Install(zip, AddonTier.Local);
                _changed();
                Rebuild();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Shows the name, version, trust level and what the add-on may do, before anything is installed.</summary>
        private bool Confirm(string name, string version, IEnumerable<string> permissions, AddonTier tier)
        {
            var origin = tier switch
            {
                AddonTier.Verified => L.T("Add-on vérifié : relu et signé par le catalogue TrackChest.", "Verified add-on: reviewed and signed by the TrackChest catalog.", "Add-on verificado: revisado y firmado por el catálogo de TrackChest.", "Geprüftes Add-on: vom TrackChest-Katalog geprüft und signiert.", "Проверенное дополнение: просмотрено и подписано каталогом TrackChest.", "検証済みアドオン: TrackChest カタログによりレビュー・署名されています。"),
                AddonTier.Community => L.T("Add-on de la communauté : contrôlé automatiquement, mais pas relu.", "Community add-on: checked automatically, but not reviewed.", "Add-on de la comunidad: comprobado automáticamente, pero no revisado.", "Community-Add-on: automatisch geprüft, aber nicht manuell kontrolliert.", "Дополнение сообщества: проверено автоматически, но не просмотрено.", "コミュニティアドオン: 自動チェック済みですが、レビューされていません。"),
                _ => L.T("Add-on local NON vérifié : n’installe que des fichiers dont tu connais la provenance.", "UNVERIFIED local add-on: only install files whose origin you know.", "Add-on local NO verificado: instala solo archivos cuyo origen conozcas.", "NICHT geprüftes lokales Add-on: Installiere nur Dateien, deren Herkunft du kennst.", "НЕпроверенное локальное дополнение: устанавливайте только файлы известного происхождения.", "未検証のローカルアドオン: 出所のわかるファイルだけをインストールしてください。"),
            };
            var list = permissions.ToList();
            var safety = list.Contains(AddonRules.OverlayScript)
                ? L.T("Son script tourne dans un bac à sable : il ne peut ni lire tes fichiers ni utiliser Internet. Il peut en revanche changer tout ce qu’affiche l’overlay : installe-le seulement si tu fais confiance à son origine.", "Its script runs in a sandbox: it cannot read your files or use the Internet. It can however change anything the overlay shows: only install it if you trust where it comes from.", "Su script se ejecuta en un entorno aislado: no puede leer tus archivos ni usar Internet. Sin embargo, puede cambiar todo lo que muestra el overlay: instálalo solo si confías en su origen.", "Sein Skript läuft in einer Sandbox: Es kann weder deine Dateien lesen noch das Internet nutzen. Es kann aber alles ändern, was das Overlay anzeigt: Installiere es nur, wenn du seiner Herkunft vertraust.", "Его скрипт работает в песочнице: он не может читать ваши файлы или пользоваться Интернетом. Но он может изменить всё, что показывает оверлей: устанавливайте его, только если доверяете источнику.", "スクリプトはサンドボックスで実行され、ファイルの読み取りやインターネットの利用はできません。ただしオーバーレイの表示はすべて変更できるため、出所を信頼できる場合のみインストールしてください。")
                : L.T("Il ne contient aucun code et ne peut ni lire tes fichiers ni utiliser Internet.", "It contains no code and cannot read your files or use the Internet.", "No contiene código y no puede leer tus archivos ni usar Internet.", "Es enthält keinen Code und kann weder deine Dateien lesen noch das Internet nutzen.", "Он не содержит кода и не может читать ваши файлы или пользоваться Интернетом.", "コードを含まず、ファイルの読み取りやインターネットの利用はできません。");
            var text = L.T($"Installer « {name} » {version} ?\n\nCet add-on pourra seulement :\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Install “{name}” {version}?\n\nThis add-on will only be able to:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"¿Instalar «{name}» {version}?\n\nEste add-on solo podrá:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"„{name}“ {version} installieren?\n\nDieses Add-on darf nur:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Установить «{name}» {version}?\n\nЭто дополнение сможет только:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"「{name}」{version} をインストールしますか？\n\nこのアドオンができること:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}");
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
            _list.Controls.Add(Header(L.T($"Installés ({installed.Count})", $"Installed ({installed.Count})", $"Instalados ({installed.Count})", $"Installiert ({installed.Count})", $"Установлено ({installed.Count})", $"インストール済み（{installed.Count}）")));
            if (installed.Count == 0) _list.Controls.Add(Note(L.T("Aucun add-on installé.", "No add-on installed.", "Ningún add-on instalado.", "Kein Add-on installiert.", "Дополнения не установлены.", "インストール済みのアドオンはありません。")));
            foreach (var addon in installed)
            {
                var toggle = new PillButton(addon.IsActive ? L.T("Désactiver", "Disable", "Desactivar", "Deaktivieren", "Отключить", "無効にする") : L.T("Activer", "Enable", "Activar", "Aktivieren", "Включить", "有効にする")) { Enabled = !addon.State.Revoked };
                var remove = new PillButton(L.T("Supprimer", "Remove", "Eliminar", "Entfernen", "Удалить", "削除"));
                toggle.Click += (_, _) => { _addons.SetEnabled(addon.Manifest.Id, !addon.IsActive); _changed(); Rebuild(); };
                remove.Click += (_, _) =>
                {
                    if (MessageBox.Show(this, L.T($"Supprimer « {addon.Manifest.Name} » ?", $"Remove “{addon.Manifest.Name}”?", $"¿Eliminar «{addon.Manifest.Name}»?", $"„{addon.Manifest.Name}“ entfernen?", $"Удалить «{addon.Manifest.Name}»?", $"「{addon.Manifest.Name}」を削除しますか？"), "TrackChest", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _addons.Remove(addon.Manifest.Id);
                    _changed();
                    Rebuild();
                };
                var state = addon.State.Revoked ? L.T("RÉVOQUÉ · ", "REVOKED · ", "REVOCADO · ", "WIDERRUFEN · ", "ОТОЗВАНО · ", "取り消し済み · ") : addon.IsActive ? "" : L.T("désactivé · ", "disabled · ", "desactivado · ", "deaktiviert · ", "отключено · ", "無効 · ");
                var kind = addon.Manifest.HasScript ? " · script" : "";
                var scriptError = addon.IsActive ? AddonRuntime.ScriptError(addon.Manifest.Id) : null;
                _list.Controls.Add(Row(addon.Manifest.Name, addon.Manifest.Version,
                    scriptError != null ? L.T($"Erreur du script : {scriptError}", $"Script error: {scriptError}", $"Error del script: {scriptError}", $"Skriptfehler: {scriptError}", $"Ошибка скрипта: {scriptError}", $"スクリプトエラー: {scriptError}") : $"{state}{TierText(addon.Tier)}{kind}",
                    scriptError != null ? Gold : TierColor(addon.Tier), addon.Manifest.Description, toggle, remove));
            }

            _list.Controls.Add(Header(L.T("Catalogue", "Catalog", "Catálogo", "Katalog", "Каталог", "カタログ")));
            if (_catalog == null) _list.Controls.Add(Note(_catalogMessage.Length > 0 ? _catalogMessage : L.T("Lecture du catalogue…", "Reading the catalog…", "Leyendo el catálogo…", "Katalog wird gelesen…", "Чтение каталога…", "カタログを読み込み中…")));
            else
            {
                if (_catalogMessage.Length > 0) _list.Controls.Add(Note(_catalogMessage));
                foreach (var entry in _catalog.Addons.Where(e => !_catalog.IsRevoked(e.Id, e.Version)))
                {
                    var current = installed.FirstOrDefault(a => a.Manifest.Id == entry.Id);
                    var isCurrent = current != null && current.Manifest.Version == entry.Version;
                    var button = new PillButton(isCurrent ? L.T("Installé", "Installed", "Instalado", "Installiert", "Установлено", "インストール済み") : current != null ? L.T("Mettre à jour", "Update", "Actualizar", "Aktualisieren", "Обновить", "更新") : L.T("Installer", "Install", "Instalar", "Installieren", "Установить", "インストール"), isCurrent ? Tone.Normal : Tone.Primary)
                    {
                        Enabled = !isCurrent,
                        Glyph = isCurrent ? PillGlyph.Check : PillGlyph.Download,
                    };
                    button.Click += async (_, _) =>
                    {
                        if (_busy || !Confirm(entry.Name, entry.Version, entry.Permissions, entry.TierValue)) return;
                        // Immediate feedback while the file is downloaded and checked.
                        button.Enabled = false;
                        button.Glyph = PillGlyph.None;
                        button.Text = L.T("Installation…", "Installing…", "Instalando…", "Wird installiert…", "Установка…", "インストール中…");
                        await InstallFromCatalog(entry);
                        if (!IsDisposed) Rebuild();   // back to "Installé" (or to "Installer" after an error)
                    };
                    _list.Controls.Add(Row(entry.Name, entry.Version, TierText(entry.TierValue), TierColor(entry.TierValue), entry.Description, button));
                }
            }
            _list.ResumeLayout(true);
            FitRows();
        }

        private static string TierText(AddonTier tier) => tier switch { AddonTier.Verified => L.T("✓ Vérifié", "✓ Verified", "✓ Verificado", "✓ Geprüft", "✓ Проверено", "✓ 検証済み"), AddonTier.Community => L.T("Communauté", "Community", "Comunidad", "Community", "Сообщество", "コミュニティ"), _ => L.T("Local, non vérifié", "Local, unverified", "Local, sin verificar", "Lokal, nicht geprüft", "Локальное, не проверено", "ローカル、未検証") };
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

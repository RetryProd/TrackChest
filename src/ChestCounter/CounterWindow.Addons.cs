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
            var guide = new PillButton(L.T("Créer un add-on", "Create an add-on", "Crear un add-on", "Add-on erstellen", "Создать дополнение", "アドオンを作成", "Crea un componente", "Criar um complemento", "Utwórz dodatek", "Een add-on maken", "Eklenti oluştur", "애드온 만들기", "创建插件", "Створити доповнення", "Skapa ett tillägg", "Vytvořit doplněk")) { Margin = new Padding(0, 4, 0, 4) };
            guide.Click += (_, _) => OpenGuide();
            head.Controls.Add(TextLabel("Add-on", 16, Ink, FontStyle.Bold), 0, 0);
            head.Controls.Add(guide, 1, 0);
            layout.Controls.Add(head, 0, 0);
            layout.Controls.Add(TextLabel(L.T("Compteurs, thèmes et scripts pour l’overlay. Les scripts tournent dans un bac à sable.", "Counters, themes and scripts for the overlay. Scripts run in a sandbox.", "Contadores, temas y scripts para el overlay. Los scripts se ejecutan en un entorno aislado.", "Zähler, Designs und Skripte für das Overlay. Skripte laufen in einer Sandbox.", "Счётчики, темы и скрипты для оверлея. Скрипты работают в песочнице.", "オーバーレイ用のカウンター、テーマ、スクリプト。スクリプトはサンドボックスで実行されます。", "Contatori, temi e script per l'overlay. Gli script vengono eseguiti in una sandbox.", "Contadores, temas e scripts para a sobreposição. Os scripts rodam em uma sandbox.", "Liczniki, motywy i skrypty dla nakładki. Skrypty działają w piaskownicy.", "Tellers, thema's en scripts voor de overlay. Scripts draaien in een sandbox.", "Kaplama için sayaçlar, temalar ve betikler. Betikler korumalı alanda çalışır.", "오버레이용 카운터, 테마, 스크립트. 스크립트는 샌드박스에서 실행됩니다.", "叠加层的计数器、主题和脚本。脚本在沙盒中运行。", "Лічильники, теми та скрипти для оверлею. Скрипти працюють у пісочниці.", "Räknare, teman och skript för överlägget. Skript körs i en sandlåda.", "Počítadla, motivy a skripty pro překryv. Skripty běží v sandboxu."), 9, Muted), 0, 1);
            layout.Controls.Add(_list, 0, 2);
            _list.SizeChanged += (_, _) => FitRows();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), ColumnCount = 4, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var refresh = new PillButton(L.T("Actualiser", "Refresh", "Actualizar", "Aktualisieren", "Обновить", "更新", "Aggiorna", "Atualizar", "Odśwież", "Vernieuwen", "Yenile", "새로 고침", "刷新", "Оновити", "Uppdatera", "Obnovit")) { Margin = new Padding(0, 0, 8, 0) };
            var file = new PillButton(L.T("Ajouter un fichier…", "Add a file…", "Añadir un archivo…", "Datei hinzufügen…", "Добавить файл…", "ファイルを追加…", "Aggiungi un file…", "Adicionar um arquivo…", "Dodaj plik…", "Een bestand toevoegen…", "Dosya ekle…", "파일 추가…", "添加文件…", "Додати файл…", "Lägg till en fil…", "Přidat soubor…")) { Margin = new Padding(0, 0, 8, 0) };
            var close = new PillButton(L.T("Fermer", "Close", "Cerrar", "Schließen", "Закрыть", "閉じる", "Chiudi", "Fechar", "Zamknij", "Sluiten", "Kapat", "닫기", "关闭", "Закрити", "Stäng", "Zavřít"), Tone.Primary);
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
                MessageBox.Show(this, L.T("Aucun catalogue n’est configuré, donc pas de guide en ligne.\nLe guide est dans le dossier addons-registry (README.md et site-src\\index.html).", "No catalog is configured, so there is no online guide.\nThe guide is in the addons-registry folder (README.md and site-src\\index.html).", "No hay ningún catálogo configurado, así que no hay guía en línea.\nLa guía está en la carpeta addons-registry (README.md y site-src\\index.html).", "Kein Katalog eingerichtet, daher keine Online-Anleitung.\nDie Anleitung liegt im Ordner addons-registry (README.md und site-src\\index.html).", "Каталог не настроен, поэтому онлайн-руководства нет.\nРуководство находится в папке addons-registry (README.md и site-src\\index.html).", "カタログが設定されていないため、オンラインガイドはありません。\nガイドは addons-registry フォルダーにあります（README.md と site-src\\index.html）。", "Nessun catalogo configurato, quindi nessuna guida online.\nLa guida si trova nella cartella addons-registry (README.md e site-src\\index.html).", "Nenhum catálogo configurado, então não há guia online.\nO guia está na pasta addons-registry (README.md e site-src\\index.html).", "Nie skonfigurowano katalogu, więc nie ma poradnika online.\nPoradnik jest w folderze addons-registry (README.md i site-src\\index.html).", "Er is geen catalogus ingesteld, dus er is geen online handleiding.\nDe handleiding staat in de map addons-registry (README.md en site-src\\index.html).", "Katalog yapılandırılmadığı için çevrimiçi kılavuz yok.\nKılavuz addons-registry klasöründe (README.md ve site-src\\index.html).", "카탈로그가 설정되지 않아 온라인 가이드가 없습니다.\n가이드는 addons-registry 폴더에 있습니다 (README.md 및 site-src\\index.html).", "未配置目录，因此没有在线指南。\n指南位于 addons-registry 文件夹（README.md 和 site-src\\index.html）。", "Каталог не налаштовано, тому онлайн-посібника немає.\nПосібник у папці addons-registry (README.md і site-src\\index.html).", "Ingen katalog är inställd, så det finns ingen onlineguide.\nGuiden finns i mappen addons-registry (README.md och site-src\\index.html).", "Není nastaven žádný katalog, takže není k dispozici online průvodce.\nPrůvodce je ve složce addons-registry (README.md a site-src\\index.html)."), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _catalogMessage = L.T("Aucun catalogue configuré. Tu peux quand même ajouter un fichier d’add-on.", "No catalog configured. You can still add an add-on file.", "No hay catálogo configurado. Aun así puedes añadir un archivo de add-on.", "Kein Katalog eingerichtet. Du kannst trotzdem eine Add-on-Datei hinzufügen.", "Каталог не настроен. Вы всё равно можете добавить файл дополнения.", "カタログが設定されていません。アドオンファイルは追加できます。", "Nessun catalogo configurato. Puoi comunque aggiungere un file di componente.", "Nenhum catálogo configurado. Você ainda pode adicionar um arquivo de complemento.", "Nie skonfigurowano katalogu. Nadal możesz dodać plik dodatku.", "Geen catalogus ingesteld. Je kunt nog steeds een add-onbestand toevoegen.", "Katalog yapılandırılmadı. Yine de bir eklenti dosyası ekleyebilirsin.", "카탈로그가 설정되지 않았습니다. 애드온 파일은 계속 추가할 수 있습니다.", "未配置目录。你仍然可以添加插件文件。", "Каталог не налаштовано. Ви все одно можете додати файл доповнення.", "Ingen katalog inställd. Du kan ändå lägga till en tilläggsfil.", "Není nastaven žádný katalog. Soubor doplňku můžeš přesto přidat.");
                Rebuild();
                return;
            }

            _busy = true;
            _status.Text = L.T("Lecture du catalogue…", "Reading the catalog…", "Leyendo el catálogo…", "Katalog wird gelesen…", "Чтение каталога…", "カタログを読み込み中…", "Lettura del catalogo…", "Lendo o catálogo…", "Wczytywanie katalogu…", "Catalogus lezen…", "Katalog okunuyor…", "카탈로그 읽는 중…", "正在读取目录…", "Читання каталогу…", "Läser katalogen…", "Načítání katalogu…");
            try
            {
                _catalog = await AddonCatalog.FetchIndexAsync(url, _closing.Token);
                _catalogMessage = _catalog.Addons.Count == 0 ? L.T("Le catalogue est vide.", "The catalog is empty.", "El catálogo está vacío.", "Der Katalog ist leer.", "Каталог пуст.", "カタログは空です。", "Il catalogo è vuoto.", "O catálogo está vazio.", "Katalog jest pusty.", "De catalogus is leeg.", "Katalog boş.", "카탈로그가 비어 있습니다.", "目录为空。", "Каталог порожній.", "Katalogen är tom.", "Katalog je prázdný.") : "";
                var revoked = _addons.ApplyRevocations(_catalog);
                if (revoked.Count > 0)
                {
                    _changed();
                    MessageBox.Show(this, L.T($"Désactivés car révoqués par le catalogue :\n• {string.Join("\n• ", revoked)}", $"Disabled because the catalog revoked them:\n• {string.Join("\n• ", revoked)}", $"Desactivados porque el catálogo los revocó:\n• {string.Join("\n• ", revoked)}", $"Deaktiviert, da vom Katalog widerrufen:\n• {string.Join("\n• ", revoked)}", $"Отключены, так как отозваны каталогом:\n• {string.Join("\n• ", revoked)}", $"カタログで取り消されたため無効にしました:\n• {string.Join("\n• ", revoked)}", $"Disattivati perché revocati dal catalogo:\n• {string.Join("\n• ", revoked)}", $"Desativados porque o catálogo os revogou:\n• {string.Join("\n• ", revoked)}", $"Wyłączone, ponieważ katalog je unieważnił:\n• {string.Join("\n• ", revoked)}", $"Uitgeschakeld omdat de catalogus ze heeft ingetrokken:\n• {string.Join("\n• ", revoked)}", $"Katalog iptal ettiği için devre dışı bırakıldı:\n• {string.Join("\n• ", revoked)}", $"카탈로그에서 취소되어 비활성화됨:\n• {string.Join("\n• ", revoked)}", $"因目录撤销而停用：\n• {string.Join("\n• ", revoked)}", $"Вимкнено, бо каталог їх відкликав:\n• {string.Join("\n• ", revoked)}", $"Inaktiverade eftersom katalogen återkallade dem:\n• {string.Join("\n• ", revoked)}", $"Vypnuto, protože je katalog odvolal:\n• {string.Join("\n• ", revoked)}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                _status.Text = "";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or TaskCanceledException)
            {
                _catalog = null;
                _catalogMessage = L.T($"Catalogue indisponible : {ex.Message}", $"Catalog unavailable: {ex.Message}", $"Catálogo no disponible: {ex.Message}", $"Katalog nicht verfügbar: {ex.Message}", $"Каталог недоступен: {ex.Message}", $"カタログを利用できません: {ex.Message}", $"Catalogo non disponibile: {ex.Message}", $"Catálogo indisponível: {ex.Message}", $"Katalog niedostępny: {ex.Message}", $"Catalogus niet beschikbaar: {ex.Message}", $"Katalog kullanılamıyor: {ex.Message}", $"카탈로그를 사용할 수 없음: {ex.Message}", $"目录不可用：{ex.Message}", $"Каталог недоступний: {ex.Message}", $"Katalogen är inte tillgänglig: {ex.Message}", $"Katalog není k dispozici: {ex.Message}");
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
            _status.Text = L.T("Téléchargement…", "Downloading…", "Descargando…", "Wird heruntergeladen…", "Загрузка…", "ダウンロード中…", "Download in corso…", "Baixando…", "Pobieranie…", "Downloaden…", "İndiriliyor…", "다운로드 중…", "正在下载…", "Завантаження…", "Laddar ner…", "Stahování…");
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
                Title = L.T("Ajouter un add-on", "Add an add-on", "Añadir un add-on", "Add-on hinzufügen", "Добавить дополнение", "アドオンを追加", "Aggiungi un componente", "Adicionar um complemento", "Dodaj dodatek", "Een add-on toevoegen", "Eklenti ekle", "애드온 추가", "添加插件", "Додати доповнення", "Lägg till ett tillägg", "Přidat doplněk"), Filter = L.T("Add-on TrackChest (*.zip)|*.zip", "TrackChest add-on (*.zip)|*.zip", "Add-on de TrackChest (*.zip)|*.zip", "TrackChest-Add-on (*.zip)|*.zip", "Дополнение TrackChest (*.zip)|*.zip", "TrackChest アドオン (*.zip)|*.zip", "Componente TrackChest (*.zip)|*.zip", "Complemento do TrackChest (*.zip)|*.zip", "Dodatek TrackChest (*.zip)|*.zip", "TrackChest-add-on (*.zip)|*.zip", "TrackChest eklentisi (*.zip)|*.zip", "TrackChest 애드온 (*.zip)|*.zip", "TrackChest 插件 (*.zip)|*.zip", "Доповнення TrackChest (*.zip)|*.zip", "TrackChest-tillägg (*.zip)|*.zip", "Doplněk TrackChest (*.zip)|*.zip"), CheckFileExists = true,
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
                AddonTier.Verified => L.T("Add-on vérifié : relu et signé par le catalogue TrackChest.", "Verified add-on: reviewed and signed by the TrackChest catalog.", "Add-on verificado: revisado y firmado por el catálogo de TrackChest.", "Geprüftes Add-on: vom TrackChest-Katalog geprüft und signiert.", "Проверенное дополнение: просмотрено и подписано каталогом TrackChest.", "検証済みアドオン: TrackChest カタログによりレビュー・署名されています。", "Componente verificato: controllato e firmato dal catalogo TrackChest.", "Complemento verificado: revisado e assinado pelo catálogo do TrackChest.", "Zweryfikowany dodatek: sprawdzony i podpisany przez katalog TrackChest.", "Geverifieerde add-on: beoordeeld en ondertekend door de TrackChest-catalogus.", "Doğrulanmış eklenti: TrackChest kataloğu tarafından incelendi ve imzalandı.", "검증된 애드온: TrackChest 카탈로그에서 검토하고 서명했습니다.", "已验证插件：由 TrackChest 目录审核并签名。", "Перевірене доповнення: переглянуте й підписане каталогом TrackChest.", "Verifierat tillägg: granskat och signerat av TrackChest-katalogen.", "Ověřený doplněk: zkontrolovaný a podepsaný katalogem TrackChest."),
                AddonTier.Community => L.T("Add-on de la communauté : contrôlé automatiquement, mais pas relu.", "Community add-on: checked automatically, but not reviewed.", "Add-on de la comunidad: comprobado automáticamente, pero no revisado.", "Community-Add-on: automatisch geprüft, aber nicht manuell kontrolliert.", "Дополнение сообщества: проверено автоматически, но не просмотрено.", "コミュニティアドオン: 自動チェック済みですが、レビューされていません。", "Componente della community: controllato automaticamente, ma non revisionato.", "Complemento da comunidade: verificado automaticamente, mas não revisado.", "Dodatek społeczności: sprawdzony automatycznie, ale nie przejrzany.", "Community-add-on: automatisch gecontroleerd, maar niet beoordeeld.", "Topluluk eklentisi: otomatik olarak denetlendi ama incelenmedi.", "커뮤니티 애드온: 자동으로 확인되었지만 검토되지 않았습니다.", "社区插件：已自动检查，但未经审核。", "Доповнення спільноти: перевірене автоматично, але не переглянуте.", "Communitytillägg: kontrollerat automatiskt men inte granskat.", "Komunitní doplněk: automaticky zkontrolovaný, ale neprověřený."),
                _ => L.T("Add-on local NON vérifié : n’installe que des fichiers dont tu connais la provenance.", "UNVERIFIED local add-on: only install files whose origin you know.", "Add-on local NO verificado: instala solo archivos cuyo origen conozcas.", "NICHT geprüftes lokales Add-on: Installiere nur Dateien, deren Herkunft du kennst.", "НЕпроверенное локальное дополнение: устанавливайте только файлы известного происхождения.", "未検証のローカルアドオン: 出所のわかるファイルだけをインストールしてください。", "Componente locale NON VERIFICATO: installa solo file di cui conosci l'origine.", "Complemento local NÃO VERIFICADO: instale apenas arquivos cuja origem você conhece.", "NIEZWERYFIKOWANY dodatek lokalny: instaluj tylko pliki, których pochodzenie znasz.", "NIET-GEVERIFIEERDE lokale add-on: installeer alleen bestanden waarvan je de herkomst kent.", "DOĞRULANMAMIŞ yerel eklenti: yalnızca kaynağını bildiğin dosyaları yükle.", "검증되지 않은 로컬 애드온: 출처를 아는 파일만 설치하세요.", "未验证的本地插件：只安装你知道来源的文件。", "НЕПЕРЕВІРЕНЕ локальне доповнення: встановлюйте лише файли, походження яких знаєте.", "OVERIFIERAT lokalt tillägg: installera bara filer vars ursprung du känner till.", "NEOVĚŘENÝ místní doplněk: instaluj jen soubory, jejichž původ znáš."),
            };
            var list = permissions.ToList();
            var safety = list.Contains(AddonRules.OverlayScript)
                ? L.T("Son script tourne dans un bac à sable : il ne peut ni lire tes fichiers ni utiliser Internet. Il peut en revanche changer tout ce qu’affiche l’overlay : installe-le seulement si tu fais confiance à son origine.", "Its script runs in a sandbox: it cannot read your files or use the Internet. It can however change anything the overlay shows: only install it if you trust where it comes from.", "Su script se ejecuta en un entorno aislado: no puede leer tus archivos ni usar Internet. Sin embargo, puede cambiar todo lo que muestra el overlay: instálalo solo si confías en su origen.", "Sein Skript läuft in einer Sandbox: Es kann weder deine Dateien lesen noch das Internet nutzen. Es kann aber alles ändern, was das Overlay anzeigt: Installiere es nur, wenn du seiner Herkunft vertraust.", "Его скрипт работает в песочнице: он не может читать ваши файлы или пользоваться Интернетом. Но он может изменить всё, что показывает оверлей: устанавливайте его, только если доверяете источнику.", "スクリプトはサンドボックスで実行され、ファイルの読み取りやインターネットの利用はできません。ただしオーバーレイの表示はすべて変更できるため、出所を信頼できる場合のみインストールしてください。", "Il suo script viene eseguito in una sandbox: non può leggere i tuoi file né usare Internet. Può però modificare tutto ciò che mostra l'overlay: installalo solo se ti fidi della sua provenienza.", "O script dele roda em uma sandbox: não pode ler seus arquivos nem usar a Internet. Mas pode alterar tudo o que a sobreposição mostra: instale só se confiar na origem.", "Jego skrypt działa w piaskownicy: nie może czytać twoich plików ani korzystać z Internetu. Może jednak zmienić wszystko, co pokazuje nakładka: instaluj go tylko, jeśli ufasz jego źródłu.", "Het script draait in een sandbox: het kan je bestanden niet lezen en geen internet gebruiken. Het kan wel alles wijzigen wat de overlay toont: installeer het alleen als je de bron vertrouwt.", "Betiği korumalı alanda çalışır: dosyalarını okuyamaz ve İnternet'i kullanamaz. Ancak kaplamada gösterilen her şeyi değiştirebilir: yalnızca kaynağına güveniyorsan yükle.", "이 스크립트는 샌드박스에서 실행됩니다: 파일을 읽거나 인터넷을 사용할 수 없습니다. 하지만 오버레이에 표시되는 모든 것을 바꿀 수 있으니, 출처를 신뢰할 때만 설치하세요.", "其脚本在沙盒中运行：无法读取你的文件或使用互联网。但它可以更改叠加层显示的一切：仅在信任来源时安装。", "Його скрипт працює в пісочниці: він не може читати ваші файли чи користуватися Інтернетом. Проте він може змінити все, що показує оверлей: встановлюйте його, лише якщо довіряєте джерелу.", "Dess skript körs i en sandlåda: det kan inte läsa dina filer eller använda internet. Det kan dock ändra allt som överlägget visar: installera det bara om du litar på källan.", "Jeho skript běží v sandboxu: nemůže číst tvé soubory ani používat internet. Může však změnit vše, co překryv zobrazuje: instaluj ho jen tehdy, když zdroji důvěřuješ.")
                : L.T("Il ne contient aucun code et ne peut ni lire tes fichiers ni utiliser Internet.", "It contains no code and cannot read your files or use the Internet.", "No contiene código y no puede leer tus archivos ni usar Internet.", "Es enthält keinen Code und kann weder deine Dateien lesen noch das Internet nutzen.", "Он не содержит кода и не может читать ваши файлы или пользоваться Интернетом.", "コードを含まず、ファイルの読み取りやインターネットの利用はできません。", "Non contiene codice e non può leggere i tuoi file né usare Internet.", "Não contém código e não pode ler seus arquivos nem usar a Internet.", "Nie zawiera kodu i nie może czytać twoich plików ani korzystać z Internetu.", "Het bevat geen code en kan je bestanden niet lezen en geen internet gebruiken.", "Kod içermez; dosyalarını okuyamaz ve İnternet'i kullanamaz.", "코드가 없으며 파일을 읽거나 인터넷을 사용할 수 없습니다.", "不包含代码，无法读取你的文件或使用互联网。", "Воно не містить коду й не може читати ваші файли чи користуватися Інтернетом.", "Det innehåller ingen kod och kan inte läsa dina filer eller använda internet.", "Neobsahuje žádný kód a nemůže číst tvé soubory ani používat internet.");
            var text = L.T($"Installer « {name} » {version} ?\n\nCet add-on pourra seulement :\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Install “{name}” {version}?\n\nThis add-on will only be able to:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"¿Instalar «{name}» {version}?\n\nEste add-on solo podrá:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"„{name}“ {version} installieren?\n\nDieses Add-on darf nur:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Установить «{name}» {version}?\n\nЭто дополнение сможет только:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"「{name}」{version} をインストールしますか？\n\nこのアドオンができること:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Installare “{name}” {version}?\n\nQuesto componente potrà solo:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Instalar “{name}” {version}?\n\nEste complemento só poderá:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Zainstalować „{name}” {version}?\n\nTen dodatek będzie mógł tylko:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"“{name}” {version} installeren?\n\nDeze add-on kan alleen:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"“{name}” {version} yüklensin mi?\n\nBu eklenti yalnızca şunları yapabilecek:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"“{name}” {version}을(를) 설치할까요?\n\n이 애드온은 다음만 할 수 있습니다:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"安装“{name}” {version}？\n\n此插件只能：\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Встановити «{name}» {version}?\n\nЦе доповнення зможе лише:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Installera ”{name}” {version}?\n\nDetta tillägg kommer bara att kunna:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}", $"Nainstalovat „{name}“ {version}?\n\nTento doplněk bude moci pouze:\n• {string.Join("\n• ", list.Select(AddonRules.Describe))}\n\n{origin}\n{safety}");
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
            _list.Controls.Add(Header(L.T($"Installés ({installed.Count})", $"Installed ({installed.Count})", $"Instalados ({installed.Count})", $"Installiert ({installed.Count})", $"Установлено ({installed.Count})", $"インストール済み（{installed.Count}）", $"Installati ({installed.Count})", $"Instalados ({installed.Count})", $"Zainstalowane ({installed.Count})", $"Geïnstalleerd ({installed.Count})", $"Yüklü ({installed.Count})", $"설치됨 ({installed.Count})", $"已安装（{installed.Count}）", $"Встановлені ({installed.Count})", $"Installerade ({installed.Count})", $"Nainstalované ({installed.Count})")));
            if (installed.Count == 0) _list.Controls.Add(Note(L.T("Aucun add-on installé.", "No add-on installed.", "Ningún add-on instalado.", "Kein Add-on installiert.", "Дополнения не установлены.", "インストール済みのアドオンはありません。", "Nessun componente installato.", "Nenhum complemento instalado.", "Brak zainstalowanych dodatków.", "Geen add-on geïnstalleerd.", "Yüklü eklenti yok.", "설치된 애드온이 없습니다.", "未安装插件。", "Немає встановлених доповнень.", "Inget tillägg installerat.", "Žádný doplněk není nainstalován.")));
            foreach (var addon in installed)
            {
                var toggle = new PillButton(addon.IsActive ? L.T("Désactiver", "Disable", "Desactivar", "Deaktivieren", "Отключить", "無効にする", "Disattiva", "Desativar", "Wyłącz", "Uitschakelen", "Devre dışı bırak", "비활성화", "停用", "Вимкнути", "Inaktivera", "Vypnout") : L.T("Activer", "Enable", "Activar", "Aktivieren", "Включить", "有効にする", "Attiva", "Ativar", "Włącz", "Inschakelen", "Etkinleştir", "활성화", "启用", "Увімкнути", "Aktivera", "Zapnout")) { Enabled = !addon.State.Revoked };
                var remove = new PillButton(L.T("Supprimer", "Remove", "Eliminar", "Entfernen", "Удалить", "削除", "Rimuovi", "Remover", "Usuń", "Verwijderen", "Kaldır", "제거", "移除", "Видалити", "Ta bort", "Odebrat"));
                toggle.Click += (_, _) => { _addons.SetEnabled(addon.Manifest.Id, !addon.IsActive); _changed(); Rebuild(); };
                remove.Click += (_, _) =>
                {
                    if (MessageBox.Show(this, L.T($"Supprimer « {addon.Manifest.Name} » ?", $"Remove “{addon.Manifest.Name}”?", $"¿Eliminar «{addon.Manifest.Name}»?", $"„{addon.Manifest.Name}“ entfernen?", $"Удалить «{addon.Manifest.Name}»?", $"「{addon.Manifest.Name}」を削除しますか？", $"Rimuovere “{addon.Manifest.Name}”?", $"Remover “{addon.Manifest.Name}”?", $"Usunąć „{addon.Manifest.Name}”?", $"“{addon.Manifest.Name}” verwijderen?", $"“{addon.Manifest.Name}” kaldırılsın mı?", $"“{addon.Manifest.Name}”을(를) 제거할까요?", $"移除“{addon.Manifest.Name}”？", $"Видалити «{addon.Manifest.Name}»?", $"Ta bort ”{addon.Manifest.Name}”?", $"Odebrat „{addon.Manifest.Name}“?"), "TrackChest", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _addons.Remove(addon.Manifest.Id);
                    _changed();
                    Rebuild();
                };
                var state = addon.State.Revoked ? L.T("RÉVOQUÉ · ", "REVOKED · ", "REVOCADO · ", "WIDERRUFEN · ", "ОТОЗВАНО · ", "取り消し済み · ", "REVOCATO · ", "REVOGADO · ", "UNIEWAŻNIONY · ", "INGETROKKEN · ", "İPTAL EDİLDİ · ", "취소됨 · ", "已撤销 · ", "ВІДКЛИКАНО · ", "ÅTERKALLAT · ", "ODVOLÁNO · ") : addon.IsActive ? "" : L.T("désactivé · ", "disabled · ", "desactivado · ", "deaktiviert · ", "отключено · ", "無効 · ", "disattivato · ", "desativado · ", "wyłączony · ", "uitgeschakeld · ", "devre dışı · ", "비활성화됨 · ", "已停用 · ", "вимкнено · ", "inaktiverat · ", "vypnuto · ");
                var kind = addon.Manifest.HasScript ? " · script" : "";
                var scriptError = addon.IsActive ? AddonRuntime.ScriptError(addon.Manifest.Id) : null;
                _list.Controls.Add(Row(addon.Manifest.Name, addon.Manifest.Version,
                    scriptError != null ? L.T($"Erreur du script : {scriptError}", $"Script error: {scriptError}", $"Error del script: {scriptError}", $"Skriptfehler: {scriptError}", $"Ошибка скрипта: {scriptError}", $"スクリプトエラー: {scriptError}", $"Errore dello script: {scriptError}", $"Erro de script: {scriptError}", $"Błąd skryptu: {scriptError}", $"Scriptfout: {scriptError}", $"Betik hatası: {scriptError}", $"스크립트 오류: {scriptError}", $"脚本错误：{scriptError}", $"Помилка скрипту: {scriptError}", $"Skriptfel: {scriptError}", $"Chyba skriptu: {scriptError}") : $"{state}{TierText(addon.Tier)}{kind}",
                    scriptError != null ? Gold : TierColor(addon.Tier), addon.Manifest.Description,
                    addon.Manifest.Counters.Count > 0 ? ConfigureButton(addon) : toggle, remove));
            }

            _list.Controls.Add(Header(L.T("Catalogue", "Catalog", "Catálogo", "Katalog", "Каталог", "カタログ", "Catalogo", "Catálogo", "Katalog", "Catalogus", "Katalog", "카탈로그", "目录", "Каталог", "Katalog", "Katalog")));
            if (_catalog == null) _list.Controls.Add(Note(_catalogMessage.Length > 0 ? _catalogMessage : L.T("Lecture du catalogue…", "Reading the catalog…", "Leyendo el catálogo…", "Katalog wird gelesen…", "Чтение каталога…", "カタログを読み込み中…", "Lettura del catalogo…", "Lendo o catálogo…", "Wczytywanie katalogu…", "Catalogus lezen…", "Katalog okunuyor…", "카탈로그 읽는 중…", "正在读取目录…", "Читання каталогу…", "Läser katalogen…", "Načítání katalogu…")));
            else
            {
                if (_catalogMessage.Length > 0) _list.Controls.Add(Note(_catalogMessage));
                foreach (var entry in _catalog.Addons.Where(e => !_catalog.IsRevoked(e.Id, e.Version)))
                {
                    var current = installed.FirstOrDefault(a => a.Manifest.Id == entry.Id);
                    var isCurrent = current != null && current.Manifest.Version == entry.Version;
                    var button = new PillButton(isCurrent ? L.T("Installé", "Installed", "Instalado", "Installiert", "Установлено", "インストール済み", "Installato", "Instalado", "Zainstalowane", "Geïnstalleerd", "Yüklü", "설치됨", "已安装", "Встановлені", "Installerade", "Nainstalované") : current != null ? L.T("Mettre à jour", "Update", "Actualizar", "Aktualisieren", "Обновить", "更新", "Aggiorna", "Atualizar", "Aktualizuj", "Bijwerken", "Güncelle", "업데이트", "更新", "Оновити", "Uppdatera", "Aktualizovat") : L.T("Installer", "Install", "Instalar", "Installieren", "Установить", "インストール", "Installa", "Instalar", "Zainstaluj", "Installeren", "Yükle", "설치", "安装", "Встановити", "Installera", "Nainstalovat"), isCurrent ? Tone.Normal : Tone.Primary)
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
                        button.Text = L.T("Installation…", "Installing…", "Instalando…", "Wird installiert…", "Установка…", "インストール中…", "Installazione…", "Instalando…", "Instalowanie…", "Installeren…", "Yükleniyor…", "설치 중…", "正在安装…", "Встановлення…", "Installerar…", "Instalace…");
                        await InstallFromCatalog(entry);
                        if (!IsDisposed) Rebuild();   // back to "Installé" (or to "Installer" after an error)
                    };
                    _list.Controls.Add(Row(entry.Name, entry.Version, TierText(entry.TierValue), TierColor(entry.TierValue), entry.Description, button));
                }
            }
            _list.ResumeLayout(true);
            FitRows();
        }

        /// <summary>"Configure" replaces "Disable" for an add-on with counters: on/off for the add-on and for each counter.</summary>
        private PillButton ConfigureButton(InstalledAddon addon)
        {
            var button = new PillButton(L.T("Configurer", "Configure", "Configurar", "Konfigurieren", "Настроить", "設定", "Configura", "Configurar", "Konfiguruj", "Configureren", "Yapılandır", "구성", "配置", "Налаштувати", "Konfigurera", "Nastavit"), Tone.Primary);
            button.Click += (_, _) =>
            {
                using (var form = new AddonConfigForm(_addons, addon.Manifest.Id, _changed)) form.ShowDialog(this);
                Rebuild();
            };
            return button;
        }

        private static string TierText(AddonTier tier) => tier switch { AddonTier.Verified => L.T("✓ Vérifié", "✓ Verified", "✓ Verificado", "✓ Geprüft", "✓ Проверено", "✓ 検証済み", "✓ Verificato", "✓ Verificado", "✓ Zweryfikowany", "✓ Geverifieerd", "✓ Doğrulandı", "✓ 검증됨", "✓ 已验证", "✓ Перевірено", "✓ Verifierat", "✓ Ověřeno"), AddonTier.Community => L.T("Communauté", "Community", "Comunidad", "Community", "Сообщество", "コミュニティ", "Community", "Comunidade", "Społeczność", "Community", "Topluluk", "커뮤니티", "社区", "Спільнота", "Community", "Komunita"), _ => L.T("Local, non vérifié", "Local, unverified", "Local, sin verificar", "Lokal, nicht geprüft", "Локальное, не проверено", "ローカル、未検証", "Locale, non verificato", "Local, não verificado", "Lokalny, niezweryfikowany", "Lokaal, niet geverifieerd", "Yerel, doğrulanmamış", "로컬, 검증 안 됨", "本地，未验证", "Локальне, неперевірене", "Lokalt, overifierat", "Místní, neověřený") };
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

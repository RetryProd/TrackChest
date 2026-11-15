namespace ChestCounter;

internal sealed partial class CounterWindow
{
    /// <summary>While linked: own figures sent / partner's fetched this often (and right after each new match).</summary>
    private readonly System.Windows.Forms.Timer _duoTimer = new() { Interval = 15_000 };
    private bool _duoSyncing;
    private bool _duoReady;

    private void SetupDuo()
    {
        if (_duoReady) return;
        _duoReady = true;
        _duoTimer.Tick += async (_, _) => await SyncDuo(force: false);
        _duo.Click += (_, _) => ShowDuo();
    }

    private void ShowDuo()
    {
        using var form = new DuoForm(
            create: async () => { await Duo.Create(_config); OnDuoLinked(); return Duo.Code!; },
            join: async code => { await Duo.Join(_config, code); OnDuoLinked(); await SyncDuo(force: true); },
            sync: () => SyncDuo(force: true),
            leave: async () => { _duoTimer.Stop(); await Duo.Leave(_config); OnDuoChanged(); });
        form.ShowDialog(this);
    }

    private void OnDuoLinked()
    {
        _duoTimer.Start();
        OnDuoChanged();
    }

    private void OnDuoChanged()
    {
        _duo.Text = Duo.Linked ? "Duo ●" : "Duo";
        RefreshStats();
    }

    private string? DuoName() => Profiles.Current(_data, _config)?.Name;

    /// <summary>Sends the own figures (when they changed) and fetches the partner's, then redraws if anything moved.</summary>
    private async Task SyncDuo(bool force)
    {
        if (!Duo.Linked || _duoSyncing || _closing.IsCancellationRequested) return;
        _duoSyncing = true;
        try
        {
            var mine = DuoStats.From(Profiles.View(_data, _config), _inProgress);
            var changed = await Duo.Sync(_config, mine, DuoName(), force);
            if (!Duo.Linked) _duoTimer.Stop();
            if (changed || force) OnDuoChanged();
        }
        finally { _duoSyncing = false; }
    }

    /// <summary>The small lines under the big numbers: own figure in "(…)" and the partner while linked, default text otherwise.</summary>
    private void ShowDuoDetails(TrackerData profileData)
    {
        var partner = Duo.PartnerStats;
        if (partner == null)
        {
            foreach (var label in new[] { _totalDetail, _lastDetail, _pirateDetail })
                if (label.Tag is string text) label.Text = text;
            if (Duo.Linked) _totalDetail.Text = Duo.T($"Duo {Duo.Code} · en attente du partenaire", $"Duo {Duo.Code} · waiting for the partner");
            return;
        }
        var mine = DuoStats.From(profileData, _inProgress);
        var with = $"{Duo.T("avec", "with")} {Duo.Partner!.Name}";
        _totalDetail.Text = $"{OverlayModel.Own(L.N(mine.Total))} · {with}";
        _lastDetail.Text = mine.HasLast ? $"{OverlayModel.Own(L.N(mine.Last))} · {with}" : with;
        _pirateDetail.Text = $"{OverlayModel.Own(PirateTokenOdds.Format(mine.Pirate))} · {with}";
    }

    /// <summary>Duo window: create a code or join one, then sync or leave.</summary>
    private sealed class DuoForm : Form
    {
        private readonly Func<Task<string>> _create;
        private readonly Func<string, Task> _join;
        private readonly Func<Task> _sync;
        private readonly Func<Task> _leave;
        private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        private readonly Label _message = TextLabel("", 9, Muted);
        private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 2000 };
        private bool _busy;

        public DuoForm(Func<Task<string>> create, Func<string, Task> join, Func<Task> sync, Func<Task> leave)
        {
            _create = create;
            _join = join;
            _sync = sync;
            _leave = leave;
            Text = "TrackChest · Duo";
            Icon = AppIcon(SystemInformation.IconSize.Width) ?? Icon;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Canvas;
            ForeColor = Ink;

            var root = new GlowPanel { Dock = DockStyle.Fill };
            var layout = Grid([100], 44, 56, 170, 30);
            layout.BackColor = Color.Transparent;
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(22, 18, 22, 12);
            root.Controls.Add(layout);
            Controls.Add(root);

            var titleRow = Grid([70, 30], 44);
            titleRow.Controls.Add(TextLabel(Duo.T("Mode Duo", "Duo mode"), 16, Ink, FontStyle.Bold), 0, 0);
            var close = new PillButton(L.T("Fermer", "Close", "Cerrar", "Schließen", "Закрыть", "閉じる", "Chiudi", "Fechar", "Zamknij", "Sluiten", "Kapat", "닫기", "关闭", "Закрити", "Stäng", "Zavřít"), Tone.Primary) { Margin = new Padding(0, 0, 0, 6) };
            close.Click += (_, _) => Close();
            titleRow.Controls.Add(close, 1, 0);
            layout.Controls.Add(titleRow, 0, 0);

            var intro = TextLabel(Duo.T(
                "Lie-toi avec un ami : tant que vous êtes liés, l’overlay additionne vos coffres (total, session, dernière partie, trésors, jeton pirate). Ton propre chiffre reste affiché en petit entre ( ) et ton historique n’est jamais modifié.",
                "Link with a friend: while you are linked, the overlay adds up your chests (total, session, last match, treasures, pirate token). Your own figure stays shown small in ( ) and your history is never changed."), 9, Muted);
            intro.TextAlign = ContentAlignment.TopLeft;
            layout.Controls.Add(intro, 0, 1);

            var card = Card(new Padding(18, 14, 18, 12), Padding.Empty);
            card.Controls.Add(_body);
            layout.Controls.Add(card, 0, 2);
            _message.AutoEllipsis = true;
            layout.Controls.Add(_message, 0, 3);

            // Redrawn only when the link state moves (a redraw would clear a code being typed).
            _refresh.Tick += (_, _) => { if (!_busy && StateKey() != _shownState) Render(); };
            Shown += (_, _) => _refresh.Start();
            Render();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _refresh.Dispose();
            base.Dispose(disposing);
        }

        private string _shownState = "";

        private static string StateKey() => $"{Duo.Code}|{Duo.PartnerJoined}|{Duo.Partner?.Name}|{Duo.LastSyncUtc:HHmmss}|{Duo.LastError}";

        private void Render()
        {
            _shownState = StateKey();
            _body.SuspendLayout();
            foreach (Control c in _body.Controls.Cast<Control>().ToList()) { _body.Controls.Remove(c); c.Dispose(); }
            var grid = Duo.Linked ? Linked() : NotLinked();
            grid.Dock = DockStyle.Fill;
            _body.Controls.Add(grid);
            _body.ResumeLayout(true);
            if (Duo.LastError != null) { _message.ForeColor = Gold; _message.Text = Duo.LastError; }
        }

        private TableLayoutPanel NotLinked()
        {
            var grid = Grid([100], 26, 40, 14, 26, 40);
            grid.Controls.Add(TextLabel(Duo.T("Créer un code et l’envoyer à ton ami", "Create a code and send it to your friend"), 9.5f, Soft), 0, 0);
            var create = new PillButton(Duo.T("Créer un code", "Create a code"), Tone.Primary) { Margin = new Padding(0, 0, 260, 0) };
            create.Click += async (_, _) => await Run(async () =>
            {
                var code = await _create();
                try { Clipboard.SetText(code); } catch (System.Runtime.InteropServices.ExternalException) { }
                Info(Duo.T($"Code {code} copié : envoie-le à ton ami.", $"Code {code} copied: send it to your friend."));
            });
            grid.Controls.Add(create, 0, 1);
            grid.Controls.Add(TextLabel(Duo.T("Ou entrer le code reçu", "Or enter the code you received"), 9.5f, Soft), 0, 3);
            var row = Grid([60, 40], 40);
            var frame = FieldFrame(new Padding(12, 9, 12, 0), new Padding(0, 0, 8, 0));
            var box = new TextBox
            {
                BorderStyle = BorderStyle.None, BackColor = Field, ForeColor = Ink, Dock = DockStyle.Top, MaxLength = 7,
                CharacterCasing = CharacterCasing.Upper, Font = new Font("Segoe UI", 11, FontStyle.Bold), PlaceholderText = "ABC234",
                AccessibleName = Duo.T("Code duo", "Duo code"),
            };
            frame.Controls.Add(box);
            row.Controls.Add(frame, 0, 0);
            var join = new PillButton(Duo.T("Rejoindre", "Join"));
            join.Click += async (_, _) => await Run(async () =>
            {
                await _join(box.Text);
                Info(Duo.T("Liés ! Vos coffres s’additionnent.", "Linked! Your chests add up."));
            });
            box.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; join.PerformClick(); } };
            row.Controls.Add(join, 1, 0);
            grid.Controls.Add(row, 0, 4);
            return grid;
        }

        private TableLayoutPanel Linked()
        {
            var grid = Grid([100], 26, 44, 30, 44);
            grid.Controls.Add(TextLabel(Duo.T("Code de liaison", "Link code"), 9.5f, Soft), 0, 0);
            var codeRow = Grid([60, 40], 44);
            codeRow.Controls.Add(TextLabel(Duo.Code ?? "", 22, Gold, FontStyle.Bold), 0, 0);
            var copy = new PillButton(Duo.T("Copier", "Copy")) { Margin = new Padding(0, 2, 0, 4) };
            copy.Click += (_, _) =>
            {
                try { Clipboard.SetText(Duo.Code ?? ""); Info(Duo.T("Code copié.", "Code copied.")); }
                catch (System.Runtime.InteropServices.ExternalException) { }
            };
            codeRow.Controls.Add(copy, 1, 0);
            grid.Controls.Add(codeRow, 0, 1);

            var partner = Duo.Partner;
            var status = partner != null
                ? Duo.T($"● Lié avec {partner.Name}", $"● Linked with {partner.Name}") + (Duo.LastSyncUtc is { } at ? Duo.T($"  ·  synchro à {at.ToLocalTime():HH:mm:ss}", $"  ·  synced at {at.ToLocalTime():HH:mm:ss}") : "")
                : Duo.PartnerJoined
                    ? Duo.T("Ton ami est lié : ses chiffres arrivent…", "Your friend is linked: their figures are on the way…")
                    : Duo.T("En attente de ton ami… (envoie-lui le code)", "Waiting for your friend… (send them the code)");
            grid.Controls.Add(TextLabel(status, 9.5f, partner != null ? Green : Muted), 0, 2);

            var buttons = Grid([50, 50], 44);
            var sync = new PillButton(Duo.T("Synchroniser", "Sync"), Tone.Primary) { Margin = new Padding(0, 4, 8, 0) };
            sync.Click += async (_, _) => await Run(async () =>
            {
                await _sync();
                if (Duo.LastError == null) Info(Duo.T("Synchronisé.", "Synced."));
            });
            var leave = new PillButton(Duo.T("Délier", "Unlink")) { Margin = new Padding(0, 4, 0, 0) };
            leave.Click += async (_, _) => await Run(async () =>
            {
                await _leave();
                Info(Duo.T("Liaison terminée : retour à tes seuls chiffres.", "Link ended: back to your own figures."));
            });
            buttons.Controls.Add(sync, 0, 0);
            buttons.Controls.Add(leave, 1, 0);
            grid.Controls.Add(buttons, 0, 3);
            return grid;
        }

        private void Info(string text)
        {
            _message.ForeColor = Muted;
            _message.Text = text;
        }

        private async Task Run(Func<Task> action)
        {
            if (_busy) return;
            _busy = true;
            _body.Enabled = false;
            Info(Duo.T("Connexion…", "Connecting…"));
            try
            {
                await action();
            }
            catch (DuoException ex)
            {
                _message.ForeColor = Gold;
                _message.Text = ex.Message;
                _busy = false;
                if (!IsDisposed) { _body.Enabled = true; Render(); _message.ForeColor = Gold; _message.Text = ex.Message; }
                return;
            }
            _busy = false;
            if (IsDisposed) return;
            _body.Enabled = true;
            var text = _message.Text;
            var color = _message.ForeColor;
            Render();
            if (Duo.LastError == null) { _message.Text = text; _message.ForeColor = color; }
        }
    }
}

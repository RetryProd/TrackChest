namespace ChestCounter;

internal sealed partial class CounterWindow
{
    private bool _updateRunning;

    /// <summary>
    /// Looks for a newer version, asks before installing, downloads and verifies the .msi, then closes the app so the
    /// installer can replace it. Silent when nothing is new or when the check fails, unless the user asked for it.
    /// </summary>
    private async Task RunUpdate(bool manual)
    {
        if (_updateRunning) return;
        _updateRunning = true;
        try
        {
            UpdateInfo? update;
            try { update = await Updater.CheckAsync(); }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException
                or KeyNotFoundException or TaskCanceledException or FormatException)
            {
                if (manual) MessageBox.Show(this, $"Impossible de vérifier les mises à jour.\n{ex.Message}", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (update == null)
            {
                if (manual) MessageBox.Show(this, $"TrackChest {Updater.CurrentText} est à jour.", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var v = update.Version;
            var notes = update.Notes.Length > 0 ? $"\n\n{update.Notes}" : "";
            var answer = MessageBox.Show(this, $"TrackChest {v.Major}.{v.Minor}.{v.Build} est disponible (vous avez {Updater.CurrentText}).{notes}\n\nInstaller maintenant ? TrackChest va se fermer puis se rouvrir. Vos données sont conservées.",
                "TrackChest · Mise à jour", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            string path;
            try
            {
                UseWaitCursor = true;
                path = await Updater.DownloadAsync(update);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException)
            {
                MessageBox.Show(this, $"La mise à jour n’a pas pu être téléchargée.\n{ex.Message}", "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally { UseWaitCursor = false; }

            _store.SaveConfig(_config);
            Updater.Install(path);
            Application.Exit();
        }
        finally { _updateRunning = false; }
    }
}

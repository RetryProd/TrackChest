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
                if (manual) MessageBox.Show(this, L.T($"Impossible de vérifier les mises à jour.\n{ex.Message}", $"Could not check for updates.\n{ex.Message}", $"No se pudieron buscar actualizaciones.\n{ex.Message}", $"Es konnte nicht nach Updates gesucht werden.\n{ex.Message}", $"Не удалось проверить обновления.\n{ex.Message}", $"更新を確認できませんでした。\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (update == null)
            {
                if (manual) MessageBox.Show(this, L.T($"TrackChest {Updater.CurrentText} est à jour.", $"TrackChest {Updater.CurrentText} is up to date.", $"TrackChest {Updater.CurrentText} está actualizado.", $"TrackChest {Updater.CurrentText} ist aktuell.", $"TrackChest {Updater.CurrentText} — последняя версия.", $"TrackChest {Updater.CurrentText} は最新です。"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var v = update.Version;
            var notes = update.Notes.Length > 0 ? $"\n\n{update.Notes}" : "";
            var answer = MessageBox.Show(this, L.T($"TrackChest {v.Major}.{v.Minor}.{v.Build} est disponible (vous avez {Updater.CurrentText}).{notes}\n\nInstaller maintenant ? TrackChest va se fermer puis se rouvrir. Vos données sont conservées.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} is available (you have {Updater.CurrentText}).{notes}\n\nInstall now? TrackChest will close and reopen. Your data is kept.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} está disponible (tienes {Updater.CurrentText}).{notes}\n\n¿Instalar ahora? TrackChest se cerrará y volverá a abrirse. Tus datos se conservan.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} ist verfügbar (du hast {Updater.CurrentText}).{notes}\n\nJetzt installieren? TrackChest wird geschlossen und neu geöffnet. Deine Daten bleiben erhalten.", $"Доступна версия TrackChest {v.Major}.{v.Minor}.{v.Build} (у вас {Updater.CurrentText}).{notes}\n\nУстановить сейчас? TrackChest закроется и откроется снова. Ваши данные сохранятся.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} が利用可能です（現在 {Updater.CurrentText}）。{notes}\n\n今すぐインストールしますか？TrackChest は終了して再起動します。データは保持されます。"),
                L.T("TrackChest · Mise à jour", "TrackChest · Update", "TrackChest · Actualización", "TrackChest · Update", "TrackChest · Обновление", "TrackChest · アップデート"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            string path;
            try
            {
                UseWaitCursor = true;
                path = await Updater.DownloadAsync(update);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException)
            {
                MessageBox.Show(this, L.T($"La mise à jour n’a pas pu être téléchargée.\n{ex.Message}", $"The update could not be downloaded.\n{ex.Message}", $"No se pudo descargar la actualización.\n{ex.Message}", $"Das Update konnte nicht heruntergeladen werden.\n{ex.Message}", $"Не удалось загрузить обновление.\n{ex.Message}", $"アップデートをダウンロードできませんでした。\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

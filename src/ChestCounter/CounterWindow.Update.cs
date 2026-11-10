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
                if (manual) MessageBox.Show(this, L.T($"Impossible de vérifier les mises à jour.\n{ex.Message}", $"Could not check for updates.\n{ex.Message}", $"No se pudieron buscar actualizaciones.\n{ex.Message}", $"Es konnte nicht nach Updates gesucht werden.\n{ex.Message}", $"Не удалось проверить обновления.\n{ex.Message}", $"更新を確認できませんでした。\n{ex.Message}", $"Impossibile cercare aggiornamenti.\n{ex.Message}", $"Não foi possível verificar atualizações.\n{ex.Message}", $"Nie udało się sprawdzić aktualizacji.\n{ex.Message}", $"Kon niet op updates controleren.\n{ex.Message}", $"Güncellemeler denetlenemedi.\n{ex.Message}", $"업데이트를 확인할 수 없습니다.\n{ex.Message}", $"无法检查更新。\n{ex.Message}", $"Не вдалося перевірити оновлення.\n{ex.Message}", $"Det gick inte att söka efter uppdateringar.\n{ex.Message}", $"Aktualizace se nepodařilo zkontrolovat.\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (update == null)
            {
                if (manual) MessageBox.Show(this, L.T($"TrackChest {Updater.CurrentText} est à jour.", $"TrackChest {Updater.CurrentText} is up to date.", $"TrackChest {Updater.CurrentText} está actualizado.", $"TrackChest {Updater.CurrentText} ist aktuell.", $"TrackChest {Updater.CurrentText} — последняя версия.", $"TrackChest {Updater.CurrentText} は最新です。", $"TrackChest {Updater.CurrentText} è aggiornato.", $"O TrackChest {Updater.CurrentText} está atualizado.", $"TrackChest {Updater.CurrentText} jest aktualny.", $"TrackChest {Updater.CurrentText} is up-to-date.", $"TrackChest {Updater.CurrentText} güncel.", $"TrackChest {Updater.CurrentText}은(는) 최신 버전입니다.", $"TrackChest {Updater.CurrentText} 已是最新版本。", $"TrackChest {Updater.CurrentText} — актуальна версія.", $"TrackChest {Updater.CurrentText} är uppdaterad.", $"TrackChest {Updater.CurrentText} je aktuální."), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var v = update.Version;
            var notes = update.Notes.Length > 0 ? $"\n\n{update.Notes}" : "";
            var answer = MessageBox.Show(this, L.T($"TrackChest {v.Major}.{v.Minor}.{v.Build} est disponible (vous avez {Updater.CurrentText}).{notes}\n\nInstaller maintenant ? TrackChest va se fermer puis se rouvrir. Vos données sont conservées.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} is available (you have {Updater.CurrentText}).{notes}\n\nInstall now? TrackChest will close and reopen. Your data is kept.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} está disponible (tienes {Updater.CurrentText}).{notes}\n\n¿Instalar ahora? TrackChest se cerrará y volverá a abrirse. Tus datos se conservan.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} ist verfügbar (du hast {Updater.CurrentText}).{notes}\n\nJetzt installieren? TrackChest wird geschlossen und neu geöffnet. Deine Daten bleiben erhalten.", $"Доступна версия TrackChest {v.Major}.{v.Minor}.{v.Build} (у вас {Updater.CurrentText}).{notes}\n\nУстановить сейчас? TrackChest закроется и откроется снова. Ваши данные сохранятся.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} が利用可能です（現在 {Updater.CurrentText}）。{notes}\n\n今すぐインストールしますか？TrackChest は終了して再起動します。データは保持されます。", $"TrackChest {v.Major}.{v.Minor}.{v.Build} è disponibile (hai la {Updater.CurrentText}).{notes}\n\nInstallare ora? TrackChest si chiuderà e si riaprirà. I tuoi dati vengono conservati.", $"O TrackChest {v.Major}.{v.Minor}.{v.Build} está disponível (você tem a {Updater.CurrentText}).{notes}\n\nInstalar agora? O TrackChest vai fechar e reabrir. Seus dados são mantidos.", $"Dostępny jest TrackChest {v.Major}.{v.Minor}.{v.Build} (masz {Updater.CurrentText}).{notes}\n\nZainstalować teraz? TrackChest zamknie się i uruchomi ponownie. Twoje dane zostaną zachowane.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} is beschikbaar (je hebt {Updater.CurrentText}).{notes}\n\nNu installeren? TrackChest sluit en opent opnieuw. Je gegevens blijven bewaard.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} kullanılabilir (sende {Updater.CurrentText} var).{notes}\n\nŞimdi yüklensin mi? TrackChest kapanıp yeniden açılacak. Verilerin korunur.", $"TrackChest {v.Major}.{v.Minor}.{v.Build}을(를) 사용할 수 있습니다 (현재 {Updater.CurrentText}).{notes}\n\n지금 설치할까요? TrackChest가 닫혔다가 다시 열립니다. 데이터는 유지됩니다.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} 可用（你的版本为 {Updater.CurrentText}）。{notes}\n\n立即安装？TrackChest 将关闭并重新打开。你的数据会保留。", $"Доступна версія TrackChest {v.Major}.{v.Minor}.{v.Build} (у вас {Updater.CurrentText}).{notes}\n\nВстановити зараз? TrackChest закриється й відкриється знову. Ваші дані збережуться.", $"TrackChest {v.Major}.{v.Minor}.{v.Build} finns (du har {Updater.CurrentText}).{notes}\n\nInstallera nu? TrackChest stängs och öppnas igen. Dina data behålls.", $"Je k dispozici TrackChest {v.Major}.{v.Minor}.{v.Build} (máš {Updater.CurrentText}).{notes}\n\nNainstalovat nyní? TrackChest se zavře a znovu otevře. Tvá data zůstanou zachována."),
                L.T("TrackChest · Mise à jour", "TrackChest · Update", "TrackChest · Actualización", "TrackChest · Update", "TrackChest · Обновление", "TrackChest · アップデート", "TrackChest · Aggiornamento", "TrackChest · Atualização", "TrackChest · Aktualizacja", "TrackChest · Update", "TrackChest · Güncelleme", "TrackChest · 업데이트", "TrackChest · 更新", "TrackChest · Оновлення", "TrackChest · Uppdatering", "TrackChest · Aktualizace"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            string path;
            try
            {
                UseWaitCursor = true;
                path = await Updater.DownloadAsync(update);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException)
            {
                MessageBox.Show(this, L.T($"La mise à jour n’a pas pu être téléchargée.\n{ex.Message}", $"The update could not be downloaded.\n{ex.Message}", $"No se pudo descargar la actualización.\n{ex.Message}", $"Das Update konnte nicht heruntergeladen werden.\n{ex.Message}", $"Не удалось загрузить обновление.\n{ex.Message}", $"アップデートをダウンロードできませんでした。\n{ex.Message}", $"Impossibile scaricare l'aggiornamento.\n{ex.Message}", $"Não foi possível baixar a atualização.\n{ex.Message}", $"Nie udało się pobrać aktualizacji.\n{ex.Message}", $"De update kon niet worden gedownload.\n{ex.Message}", $"Güncelleme indirilemedi.\n{ex.Message}", $"업데이트를 다운로드할 수 없습니다.\n{ex.Message}", $"无法下载更新。\n{ex.Message}", $"Не вдалося завантажити оновлення.\n{ex.Message}", $"Uppdateringen kunde inte laddas ner.\n{ex.Message}", $"Aktualizaci se nepodařilo stáhnout.\n{ex.Message}"), "TrackChest", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

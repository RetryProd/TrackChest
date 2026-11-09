# Ligne de commande

`TrackChest.exe` sans option ouvre la fenêtre. Les options servent aux scripts, aux tests et à la publication des
add-ons. L'exe est une application Windows : depuis PowerShell, il rend la main tout de suite, même si son texte
s'affiche ensuite dans le terminal. Pour attendre la fin, utilise `Start-Process -Wait -NoNewWindow` :

```powershell
Start-Process -Wait -NoNewWindow .\dist\TrackChest.exe -ArgumentList '--once'
```

## Options

| Option | Effet |
|---|---|
| `--demos <dossier>` | dossier des replays à utiliser. Avec la fenêtre, il devient le dossier enregistré dans la configuration |
| `--data <dossier>` | dossier des données, à la place de `%APPDATA%\TrackChest` |
| `--once` | analyse les nouveaux replays, régénère le rapport, affiche un résumé par compte puis s'arrête, sans fenêtre |
| `--overlay-preview <fichier.png>` | dessine l'overlay hors de l'écran et l'enregistre en PNG |
| `--watch-fortnite` | processus de veille : lance TrackChest quand Fortnite démarre (utilisé par le raccourci de démarrage) |
| `--fortnite-auto` | ouverture par la veille : la fenêtre principale démarre réduite |
| `--help` | aide |

Une seule instance peut utiliser un même dossier de données. Ferme la fenêtre avant d'utiliser `--once` ou
`--overlay-preview` sur le même dossier, ou passe un autre `--data`.

### Outils d'add-ons

| Option | Effet |
|---|---|
| `--addon-keygen <dossier>` | crée `addon-private.key` (secrète) et `addon-public.key`. N'écrase jamais une clé existante |
| `--addon-sign <fichier> <clé-privée>` | écrit `<fichier>.sig`, la signature du fichier (utilisé pour `index.json`) |
| `--addon-verify <fichier> <clé-publique>` | vérifie `<fichier>` avec `<fichier>.sig` |
| `--addon-check <add-on.zip>` | applique à un paquet les contrôles que TrackChest fait à l'installation |

Leur usage est décrit dans [add-ons.md](add-ons.md).

## Scripts PowerShell

| Script | Rôle |
|---|---|
| `Construire.ps1` | compile `dist\TrackChest.exe` ([compilation.md](compilation.md)) |
| `Lancer-TrackChest.cmd` | lance l'exe compilé (double-clic) |
| `Analyser-Replays.ps1` | raccourci de `--once`. `-Dossier` choisit les replays, `-Donnees` le dossier des données |
| `Demarrage-Automatique.ps1` | active l'ouverture avec Fortnite au démarrage de Windows ; `-Desactiver` la retire |
| `assets\Creer-Icone.ps1` | régénère `assets\TrackChest.ico` depuis `assets\Chest.png` |

L'ouverture avec Fortnite peut aussi s'activer dans **Paramètres → Démarrage automatique**. Le raccourci créé dans le
dossier Démarrage de Windows s'appelle `TrackChest avec Fortnite.lnk`. Quand il est supprimé, le processus de veille
s'arrête de lui-même.

Exemples :

```powershell
.\Analyser-Replays.ps1
.\Analyser-Replays.ps1 -Dossier 'D:\Fortnite\Demos' -Donnees "$env:TEMP\trackchest-test"
.\Demarrage-Automatique.ps1 -Desactiver
```

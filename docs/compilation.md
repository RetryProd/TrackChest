# Compilation

## Prérequis

- Windows 10 ou 11, 64 bits. TrackChest utilise Windows Forms et des appels Win32 : il ne compile et ne tourne que
  sous Windows.
- Le **SDK .NET 10** (x64). Deux emplacements sont acceptés par `Construire.ps1` :
  - le SDK installé normalement, trouvé par la commande `dotnet` ;
  - un SDK installé sans droits administrateur dans `%LOCALAPPDATA%\TrackChest\sdk`, par exemple avec le script
    officiel de Microsoft :

    ```powershell
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1" -UseBasicParsing
    & "$env:TEMP\dotnet-install.ps1" -Channel 10.0 -Architecture x64 -InstallDir "$env:LOCALAPPDATA\TrackChest\sdk" -NoPath
    ```

Aucun paquet NuGet externe n'est nécessaire pour le code de TrackChest.

## Compiler l'exécutable

```powershell
.\Construire.ps1
```

Le script publie `src/ChestCounter/TrackChest.csproj` en mode Release pour `win-x64` :

- un seul fichier (`PublishSingleFile`), autonome (`--self-contained`, le runtime .NET est inclus), compressé ;
- dans `dist\` : `TrackChest.exe`, `CREDITS.md` et les textes des licences des composants.

L'exécutable fait environ 60 Mo et tourne sans installer .NET. Si TrackChest est ouvert pendant la compilation, la
copie dans `dist\` échoue (« Access to the path … is denied ») : ferme-le et relance le script.

## Lancer depuis les sources

```powershell
dotnet run --project src\ChestCounter -- --data "$env:TEMP\trackchest-test"
```

L'option `--data` isole un historique de test, pour ne pas toucher à `%APPDATA%\TrackChest`. Une seule instance peut
utiliser un même dossier de données.

## Ressources intégrées à l'exécutable

Elles sont déclarées dans `TrackChest.csproj` et lues avec `GetManifestResourceStream`.

| Fichier | Rôle | Obligatoire |
|---|---|---|
| `assets/TrackChest.ico` | icône de l'exe, des fenêtres et de l'en-tête | oui |
| `assets/addon-public.key` | clé publique qui vérifie la signature du catalogue d'add-ons | non : sans elle, le catalogue est désactivé |
| `assets/addon-catalog.txt` | adresse par défaut du catalogue (`https://…/index.json`) | non |

Après avoir changé l'un de ces fichiers, recompile : ils sont copiés dans l'exe au moment de la compilation.

### Changer l'icône

L'icône est générée à partir de `assets/Chest.png` (une image carrée, de préférence 512 px ou plus, fond transparent) :

```powershell
.\assets\Creer-Icone.ps1
.\Construire.ps1
```

Le script produit un `.ico` avec les tailles 16 à 256 px. L'Explorateur Windows peut garder l'ancienne icône en cache
un moment.

## Débogage de l'analyse

La variable d'environnement `CHESTCOUNTER_DEBUG` affiche des détails de l'analyse sur la sortie d'erreur. Utilise-la
avec `--once` sur un dossier de test :

```powershell
$env:CHESTCOUNTER_DEBUG = '1'
.\dist\TrackChest.exe --once --demos D:\replays-test --data "$env:TEMP\trackchest-test" 2> debug.txt
Remove-Item Env:CHESTCOUNTER_DEBUG
```

| Valeur | Ce qui est affiché |
|---|---|
| `1` | interactions du joueur suivi, trésors, canons à butin, objets équipés |
| `2` | comme `1`, plus le journal interne du décodeur de `lib/` |
| `3` | chaque classe réseau exportée par le replay (très verbeux) |
| `4` | données brutes non décodées ; `CHESTCOUNTER_PROBE_GROUPS` filtre les groupes (expression régulière, `AbilitySpec` par défaut) |

## Vérifier l'overlay sans lancer Fortnite

```powershell
.\dist\TrackChest.exe --data "$env:APPDATA\TrackChest" --overlay-preview apercu.png
```

L'overlay est dessiné hors de l'écran avec l'historique et les add-ons actifs, puis enregistré en PNG. C'est le moyen le
plus rapide de vérifier un changement d'affichage ou un thème. Ferme TrackChest avant : l'aperçu utilise le même
dossier de données.

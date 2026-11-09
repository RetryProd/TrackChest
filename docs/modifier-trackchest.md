# Modifier TrackChest

Ce guide explique **comment modifier chaque partie du logiciel** : textes, couleurs, fenêtre, overlay, statistiques,
analyse des replays, rapport, réglages, add-ons, site du catalogue. Pour chaque cas, il donne le fichier, l'endroit
précis (nom de la classe, de la méthode ou de la variable) et un exemple de code.

Tous les fichiers de code sont dans `src/ChestCounter/`. Les numéros de ligne donnés « vers la ligne N » sont des
repères : ils bougent quand le fichier change, cherche plutôt le nom indiqué (Ctrl+F).

{% hint style="info" %}
Pour comprendre d'abord comment le code est organisé, lis [Architecture](architecture.md). Pour compiler, lis
[Compilation](compilation.md).
{% endhint %}

## Avant de commencer

### Outils

- **Visual Studio Code** (gratuit) avec l'extension *C# Dev Kit*, ou **Visual Studio Community**. N'importe quel
  éditeur de texte suffit pour de petits changements.
- Le **SDK .NET 10** ([Compilation](compilation.md)).
- **Git**, pour garder l'historique et revenir en arrière.

### La boucle de travail

1. Modifie le code.
2. Lance TrackChest depuis les sources, avec un **dossier de données de test** pour ne jamais abîmer ton vrai
   historique :

   ```powershell
   dotnet run --project src\ChestCounter -- --data "$env:TEMP\trackchest-test"
   ```

   Pour partir de ton vrai historique sans le toucher, copie d'abord `%APPDATA%\TrackChest` dans ce dossier de test.
3. Pour un changement de l'**overlay**, l'aperçu est plus rapide que de lancer Fortnite :

   ```powershell
   dotnet run --project src\ChestCounter -- --data "$env:TEMP\trackchest-test" --overlay-preview "$env:TEMP\apercu.png"
   ```

4. Quand tout va bien : `.\Construire.ps1` (nouvel exe dans `dist\`), puis commit et push.

### Trois règles à ne pas oublier

| Si tu changes… | Alors… |
|---|---|
| la façon dont un replay est analysé (`ReplayAnalyzer.cs`, `lib/`) | augmente `ReplayAnalyzer.CurrentVersion` : les replays seront réanalysés |
| un champ enregistré (`MatchRecord`, `Config`, `TrackerData`) | garde la lecture des anciens fichiers possible (ne renomme pas, ajoute) |
| une règle des add-ons | modifie aussi `Validate-Addon.ps1`, `addon-rules.js` et le schéma (voir Add-ons, § 11) |

## Carte rapide : je veux changer…

| Je veux changer… | Fichier | Où |
|---|---|---|
| le nom affiché, le titre de la fenêtre | `CounterWindow.Layout.cs` | `BuildLayout`, `Text = …` et `TextLabel("TrackChest", …)` |
| l'icône | `assets/Chest.png` | puis `assets\Creer-Icone.ps1` (§ 1) |
| un texte de la fenêtre | `CounterWindow.Layout.cs` | `BuildLayout` et les déclarations en haut de la classe |
| les couleurs de la fenêtre | `CounterWindow.Layout.cs` | palette `Canvas`, `Surface`, `Gold`… en haut du fichier |
| un bouton | `CounterWindow.Layout.cs` + `CounterWindow.cs` | déclaration `PillButton`, placement dans `BuildLayout`, clic dans le constructeur |
| la fenêtre Paramètres | `CounterWindow.Settings.cs` | classe `SettingsForm` |
| la fenêtre Add-on | `CounterWindow.Addons.cs` | classe `AddonsForm` |
| les lignes de l'overlay | `OverlayModel.cs` | `OverlayModel.Build` |
| les couleurs de l'overlay | `NativeOverlay.cs` | `OverlayTheme.Default` (en bas du fichier) |
| les polices et tailles de l'overlay | `NativeOverlay.cs` | `CreateFonts`, constantes `MinWidth`, `Pad`, `ColGap` |
| les raccourcis Ctrl+Maj+O / L | `NativeOverlay.cs` | `RegisterHotKey`, constantes `VK_O`, `VK_L` |
| les tailles et opacités proposées | `OverlayModel.cs` | `OverlayAppearance.Sizes`, `Opacities` |
| ce que compte le total | `Models.cs` | `MatchRecord.ChestsTotal` |
| la règle de la session | `ReportWriter.cs` | `ReportWriter.IsThisSession` |
| le taux du jeton pirate | `PirateTokenOdds.cs` | `DropDenominator` |
| les types de coffres reconnus | `ReplayAnalyzer.cs` | classe `ChestKinds`, méthode `Classify` |
| la page Historique | `ReportWriter.cs` | `Style` (CSS) et `Report` (contenu) |
| la fréquence de surveillance (4 s) | `CounterWindow.cs` | `_timer = new() { Interval = 4000 }` |
| le dossier des données | `Models.cs` | `Store.DefaultFolder` |
| une option en ligne de commande | `Program.cs` | le `switch` sur `args` dans `Main` |
| ce qu'un add-on peut faire | `Addons.cs`, `AddonStore.cs`, `AddonScript.cs` | voir § 11 |
| le site du catalogue | `addons-registry/site-src/` | voir § 12 |

## 1. Nom, icône et textes

### Le nom

Le nom affiché apparaît à plusieurs endroits :

| Où | Fichier |
|---|---|
| nom de l'exe (`TrackChest.exe`) | `TrackChest.csproj` : `<AssemblyName>` |
| titre de la fenêtre | `CounterWindow.Layout.cs` : `Text = "TrackChest · Compteur de coffres Fortnite";` |
| grand titre de l'en-tête | `CounterWindow.Layout.cs` : `TextLabel("TrackChest", 22, Ink, FontStyle.Bold)` |
| titres des autres fenêtres | `CounterWindow.Settings.cs` et `CounterWindow.Addons.cs` : `Text = "TrackChest · …"` |
| messages d'erreur | `Program.cs`, `MessageBox.Show(…, "TrackChest", …)` |
| dossier des données | `Models.cs` : `Store.DefaultFolder` (`"TrackChest"`) |
| scripts | `Construire.ps1`, `Lancer-TrackChest.cmd`, `Analyser-Replays.ps1`, `Demarrage-Automatique.ps1` |

{% hint style="warning" %}
Changer le nom du **dossier des données** fait repartir TrackChest d'un historique vide. Ajoute alors une migration,
comme celle de `Store.DefaultFolder` qui renomme l'ancien dossier `ChestOpen`.
{% endhint %}

### L'icône

Remplace `assets/Chest.png` (image carrée, fond transparent, 512 px ou plus), puis :

```powershell
.\assets\Creer-Icone.ps1   # crée assets\TrackChest.ico (16 à 256 px)
.\Construire.ps1           # l'icône est intégrée à l'exe
```

L'icône sert à l'exe, aux fenêtres et à l'en-tête (`HeroPanel.LoadLogo`).

### Les textes

Les textes sont écrits directement dans le code. Les nouveaux textes passent par `L.T` (`Lang.cs`), qui prévoit le
français, l'anglais et l'espagnol :

```csharp
L.T("Coffres cassés", "Broken chests", "Cofres rotos")   // texte selon la langue
L.N(1549)                                                // nombre formaté : « 1 549 »
L.Count(3, "coffre", "chest", "cofre")                   // « 3 coffres » (pluriel automatique)
```

L'interface est en français : aucun choix de langue n'est proposé pour l'instant. Pour en ajouter un, appelle
`L.Set("en")` au démarrage dans `Program.cs` et ajoute un réglage dans la fenêtre Paramètres (§ 5).

Les types de coffres sont **enregistrés en français** dans `data.json` (« Coffre rare »…) ; `L.Name` les traduit à
l'affichage. Ne change pas ces textes enregistrés (voir § 8).

## 2. Couleurs et style de la fenêtre

### La palette

En haut de `CounterWindow.Layout.cs` :

```csharp
private static readonly Color Canvas = Color.FromArgb(7, 9, 14);        // fond de la fenêtre
private static readonly Color Surface = Color.FromArgb(15, 19, 28);     // fond des cartes
private static readonly Color Field = Color.FromArgb(10, 12, 17);       // fond des champs
private static readonly Color Border = Color.FromArgb(33, 37, 46);      // bordures
private static readonly Color BorderHover = Color.FromArgb(52, 56, 66); // bordure au survol
private static readonly Color Ink = Color.FromArgb(248, 250, 252);      // texte principal
private static readonly Color Soft = Color.FromArgb(203, 213, 225);     // texte des boutons
private static readonly Color Muted = Color.FromArgb(148, 163, 184);    // textes secondaires
private static readonly Color Gold = Color.FromArgb(255, 200, 96);      // accent (total, alertes)
```

Changer une de ces valeurs change toutes les fenêtres d'un coup (principale, Paramètres, Add-on), y compris la barre de
titre (`StyleTitleBar`).

### Les éléments dessinés à la main

Les contrôles de TrackChest sont dessinés par le code, ce qui permet les coins arrondis :

| Contrôle | Classe | Réglages utiles |
|---|---|---|
| fond avec lueur | `GlowPanel` | `OnPaintBackground` : position et couleur des deux lueurs (`Glow(…)`) |
| carte arrondie | `RoundPanel` | `Radius` (12 par défaut), `Edge` (couleur de bordure) |
| en-tête | `HeroPanel` | `FillCard` (dégradé), `OnPaint` (position et taille du logo) |
| bouton | `PillButton` | `Tone.Primary` (blanc) ou `Tone.Normal` ; rayon `10 * s` ; police `Segoe UI Semibold` ; `Glyph` (icône) |
| pastille d'état | `PillLabel` | couleur = `ForeColor` |
| flèche des listes | `Chevron` | dessin du chevron dans `OnPaint` |

Exemple : des boutons plus arrondis. Dans `PillButton.OnPaint`, remplace `Rounded(bounds, 10 * s)` par
`Rounded(bounds, 18 * s)`.

Exemple : un bouton principal doré au lieu de blanc. Dans `PillButton.OnPaint`, la ligne `Tone.Primary => (…)`
définit le fond, la bordure et le texte :

```csharp
Tone.Primary => (_down ? Color.FromArgb(232, 173, 72) : _hover ? Color.FromArgb(255, 214, 130) : Gold, Color.Transparent, Canvas),
```

### Police

La police générale est fixée dans `BuildLayout` : `Font = new Font("Segoe UI", 10);`. Chaque texte précise sa taille
dans `TextLabel(texte, taille, couleur, style)`.

## 3. Mise en page de la fenêtre principale

`BuildLayout` (dans `CounterWindow.Layout.cs`) construit la fenêtre avec des grilles :

```csharp
var layout = Grid([100], 118, 124, 204, 164);   // 4 rangées : en-tête, chiffres, Rediffusions, Commandes (hauteurs en px)
layout.Padding = new Padding(30);              // marge autour de la page
```

`Grid(largeurs, hauteurs…)` crée une grille : les **largeurs** sont des pourcentages de colonnes (leur somme fait 100),
les **hauteurs** des rangées en pixels. Si tu ajoutes du contenu, agrandis la rangée concernée, puis `layout.Height` et
`ClientSize` (taille de la fenêtre) d'autant.

### Ajouter un bouton

1. **Déclarer** le bouton, en haut de `CounterWindow.Layout.cs`, avec les autres :

   ```csharp
   private readonly PillButton _dossier = new("Ouvrir le dossier");
   ```

2. **Le placer** dans `BuildLayout`. Par exemple, une 5e colonne sur la 2e ligne de Commandes :

   ```csharp
   var backupRow = Grid([16, 16, 24, 24, 20], 38);   // 5 colonnes, somme = 100
   _export.Margin = _import.Margin = _addons.Margin = _settings.Margin = new Padding(0, 0, 8, 0);
   backupRow.Controls.Add(_export, 0, 0);
   backupRow.Controls.Add(_import, 1, 0);
   backupRow.Controls.Add(_addons, 2, 0);
   backupRow.Controls.Add(_settings, 3, 0);
   backupRow.Controls.Add(_dossier, 4, 0);
   ```

3. **Lui donner une action**, dans le constructeur de `CounterWindow.cs`, à côté de `_settings.Click` :

   ```csharp
   _dossier.Click += (_, _) => Process.Start(new ProcessStartInfo(_store.Folder) { UseShellExecute = true });
   ```

4. Si l'action modifie l'historique, désactive le bouton pendant un scan, comme les autres, dans `Scan()` :
   `_browse.Enabled = _scan.Enabled = … = false;` puis `= true;` dans le `finally`.

### Ajouter une carte de chiffre

Les trois cartes du haut sont créées par `Metric(titre, valeur, détail, marge)`. Pour une 4e carte :

```csharp
private readonly Label _bestValue = TextLabel("—", 26, Ink, FontStyle.Bold);   // déclaration

var stats = Grid([25, 25, 25, 25], 106);                                       // dans BuildLayout
stats.Controls.Add(Metric("RECORD", _bestValue, "En une partie", new Padding(7, 0, 0, 0)), 3, 0);
```

Puis remplis-la dans `RefreshStats()` (`CounterWindow.cs`) :

```csharp
_bestValue.Text = profileData.Matches.Count == 0 ? "—" : L.N(profileData.Matches.Max(m => m.ChestsTotal));
```

## 4. Overlay

L'overlay est une fenêtre Win32 dessinée par `NativeOverlay.cs`. Son **contenu** vient de `OverlayModel.cs`.

### Les lignes (ajouter, supprimer, renommer, réordonner)

Tout se passe dans `OverlayModel.Build` : chaque `rows.Add(…)` est une ligne, dans l'ordre d'affichage.

```csharp
rows.Add(new OverlayRow(L.T("Session", "Session", "Sesión"), L.Count(sessionTotal, "coffre", "chest", "cofre"), Detail: sessionDetail));
```

`OverlayRow(libellé, valeur, Treasure: couleur trésor, Detail: texte gris, Color: "#RRGGBB")`.

- **Supprimer** une ligne : supprime son `rows.Add(…)`.
- **Renommer** : change le premier texte de `L.T(…)`.
- **Réordonner** : déplace les `rows.Add(…)`.
- **Ajouter** une ligne, par exemple les éliminations :

  ```csharp
  var eliminations = d.Matches.Sum(m => m.Eliminations ?? 0);
  rows.Add(new OverlayRow(L.T("Éliminations", "Eliminations", "Eliminaciones"), L.N(eliminations), Color: "#FF5A5F"));
  ```

Les lignes des add-ons à compteurs sont insérées par `rows.AddRange(AddonRuntime.CounterRows(…))` ; les add-ons à
script passent ensuite sur le résultat (`AddonRuntime.ApplyScripts`).

### Les deux grands chiffres et l'en-tête

À la fin de `OverlayModel.Build`, `new OverlayView(total, chiffreDeDroite, légende, enDirect, badge, rows)` :

- le grand chiffre de gauche (`L.N(total)`) et celui de droite (dernière partie, ou « … » pendant une partie) ;
- `LastMatchCaption` : la légende « dernière partie » ;
- le badge « ● PARTIE EN COURS ».

L'en-tête « COFFRES » et la légende « total » sont dans `NativeOverlay.cs` : `Header` et `CaptionTotal`. Un add-on à
script peut les remplacer (`HeaderText`, `TotalCaption` de `OverlayView`).

### Les couleurs

En bas de `NativeOverlay.cs`, `OverlayTheme.Default` :

```csharp
public static OverlayTheme Default { get; } = new(
    Colorref(9, 12, 18),      // Background : fond
    Colorref(36, 41, 51),     // Border     : bordure arrondie
    Colorref(26, 30, 39),     // Line       : traits de séparation
    Colorref(148, 163, 184),  // Muted      : libellés et légendes
    Colorref(248, 250, 252),  // Text       : valeurs
    Colorref(255, 200, 96),   // Accent     : total et badge
    Colorref(196, 160, 255)); // Treasure   : ligne Trésors
```

Un add-on de thème peut changer ces couleurs sans recompiler (voir [Créer un add-on](creer-un-addon.md)).

### Polices et tailles

Dans `NativeOverlay.CreateFonts` :

```csharp
_fontBig = CreateFontW(-S(38), …, 800, …, "Segoe UI");     // grands chiffres (38 px, très gras)
_fontSmall = CreateFontW(-S(14), …, 500, …, "Segoe UI");   // valeurs des lignes
_fontHeader = CreateFontW(-S(12), …, 700, …, "Segoe UI");  // en-tête et badge
_fontCaption = CreateFontW(-S(11), …, 600, …, "Segoe UI"); // légendes
_fontLabel = CreateFontW(-S(13), …, 600, …, "Segoe UI");   // libellés des lignes
```

Le premier nombre est la taille (`-S(38)` = 38 px, multiplié par la taille choisie dans Paramètres), le 5e la graisse
(400 normal, 700 gras, 800 très gras), le dernier le nom de la police.

Les espacements sont des constantes : `MinWidth = 240` (largeur minimale), `Pad = 16` (marge intérieure),
`ColGap = 36` (écart entre les deux grands chiffres). Le rayon des coins est le `S(18)` de `CreateRoundRectRgn`.

### Tailles et opacités proposées

`OverlayAppearance` (en haut de `OverlayModel.cs`) :

```csharp
public static readonly int[] Sizes = { 70, 80, 90, 100, 115, 130, 150, 175 };
public static readonly int[] Opacities = { 100, 90, 80, 70, 60, 50, 40, 30 };
```

Les boutons − et + de la fenêtre Paramètres passent d'une valeur à la suivante de ces listes.

### Les raccourcis clavier

Dans `NativeOverlay.cs` :

```csharp
HotkeyRegistered = RegisterHotKey(_hwnd, 1, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O);     // Ctrl+Maj+O
LockHotkeyRegistered = RegisterHotKey(_hwnd, 2, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_L); // Ctrl+Maj+L
```

Pour une autre lettre, change `VK_O = 0x4F` ou `VK_L = 0x4C` : le code d'une lettre est son code ASCII en majuscule
(A = `0x41` … Z = `0x5A`). Pour Alt ou Windows au lieu de Ctrl+Maj, déclare les constantes `MOD_ALT = 0x1` ou `MOD_WIN = 0x8` à côté de `MOD_CONTROL`. Pense à mettre à
jour le texte d'aide de la fenêtre (`"Plein écran fenêtré  ·  Ctrl+Maj+O …"` dans `BuildLayout`) et le README.

## 5. Fenêtre Paramètres

Elle est dans `CounterWindow.Settings.cs`, classe `SettingsForm`. Un réglage, c'est trois choses : où il est
**enregistré**, comment il est **affiché**, et ce qu'il **change**.

Exemple complet : régler la fréquence de surveillance (aujourd'hui 4 secondes).

1. **L'enregistrer** dans `config.json` : ajoute une propriété à la classe `Config` (`Models.cs`) :

   ```csharp
   /// <summary>Seconds between two scans of the replay folder.</summary>
   public int ScanSeconds { get; set; } = 4;
   ```

   Les anciens `config.json` qui n'ont pas cette clé gardent la valeur par défaut.

2. **L'appliquer** au démarrage, dans le constructeur de `CounterWindow.cs` :

   ```csharp
   _timer.Interval = Math.Clamp(_config.ScanSeconds, 2, 60) * 1000;
   ```

3. **L'afficher** dans `SettingsForm` : une ligne de plus dans la carte Overlay, avec le même modèle que Taille et
   Opacité (`Stepper`) :

   ```csharp
   private readonly Label _scanValue = TextLabel("", 10, Ink, FontStyle.Bold);   // avec les autres champs

   overlayGrid.Controls.Add(Stepper("Surveillance", _scanValue, dir =>
   {
       _config.ScanSeconds = Math.Clamp(_config.ScanSeconds + dir, 2, 60);
       _save();
       _scanValue.Text = $"{_config.ScanSeconds} s";
   }), 0, 3);
   ```

   Ajoute une rangée à la grille (`Grid([100], 28, 40, 40, 40)`), agrandis la carte (`Grid([100], 44, 184, …)`) et la
   fenêtre (`ClientSize`). Le changement s'applique au prochain démarrage ; pour qu'il s'applique tout de suite, passe
   une action de plus à `SettingsForm` (comme `save` et `browse`) qui met à jour `_timer.Interval`.

## 6. Statistiques et calculs

### Ce que compte le total

`MatchRecord.ChestsTotal` (`Models.cs`) :

```csharp
public int ChestsTotal => ChestsOpened + ChestsDestroyed + LootCannons;
```

Pour ne plus compter les canons à butin, retire `+ LootCannons`. Le total, la session, la dernière partie, le
rapport et les profils suivent automatiquement. Ces valeurs sont **calculées** (`[JsonIgnore]`), pas enregistrées :
le changement s'applique aussi aux anciennes parties.

### Ajouter une statistique

Ajoute une propriété calculée à `MatchRecord`, à côté de `SpriteChests` :

```csharp
/// <summary>Rare chests opened or broken.</summary>
[JsonIgnore]
public int RareChests => Chests.Count(chest => chest.Kind == ChestKinds.Rare);
```

Puis affiche-la où tu veux : une ligne de l'overlay (§ 4), une case du rapport (§ 9),
ou une `metric` d'add-on (§ 11).

### La session

`ReportWriter.IsThisSession` : une partie fait partie de la session si elle s'est terminée après le lancement de
TrackChest. Pour une session « aujourd'hui », remplace le contenu par :

```csharp
return (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime().Date == DateTime.Today;
```

### Le jeton pirate

`PirateTokenOdds.cs` : `DropDenominator = 137_500` est le « 1 sur 137 500 ». Le calcul utilise le nombre de coffres
**ouverts** (`ChestsOpened`), choisi dans `OverlayModel.Build` et `ReportWriter.Report`.

## 7. Surveillance et données

| Réglage | Où |
|---|---|
| fréquence de surveillance | `CounterWindow.cs` : `_timer = new() { Interval = 4000 }` (millisecondes) |
| dossier des replays par défaut | `Program.cs` : `DefaultReplays` |
| dossier des données | `Models.cs` : `Store.DefaultFolder` |
| fichiers enregistrés | `Models.cs` : `Store` (`ConfigPath`, `DataPath`, `ReportPath`…) |
| délai avant de lire un replay terminé | `ReplayScanner.cs` : `TimeSpan.FromSeconds(state == ReplayState.Finished ? 4 : 30)` |
| durée maximale d'analyse d'un replay | `ReplayScanner.ReadMatch` : `timeout.CancelAfter(TimeSpan.FromMinutes(2))` |

Le format des fichiers est décrit dans [Données et sauvegarde](donnees.md).

## 8. Analyse des replays

`ReplayAnalyzer.cs` lit chaque replay. C'est la partie la plus délicate : teste toujours sur des replays connus.

### Reconnaître un nouveau type de coffre

`ChestKinds.Classify` (en bas de `ReplayAnalyzer.cs`) range chaque conteneur fouillé selon le nom de son « blueprint » :

```csharp
if (b.Contains("lootcannon") || b.Contains("cannon")) return Cannon;
if (b.Contains("ammo")) return AmmoBox;
…
if (b.Contains("rarechest") || b.Contains("legendary") || b.Contains("rare_chest")) return Rare;
```

Pour un nouveau type :

1. Ajoute une constante, par exemple `public const string Golden = "Coffre doré";`.
2. Ajoute-la à `ChestSet` si c'est un **coffre** (compté dans le total), sinon elle sera une « autre interaction ».
3. Ajoute une règle dans `Classify`, **avant** les règles plus générales (l'ordre compte).
4. Ajoute sa traduction dans `Lang.cs` (dictionnaire utilisé par `L.Name`), par exemple
   `["Coffre doré"] = ("Golden chest", "Cofre dorado"),`.
5. Augmente `ReplayAnalyzer.CurrentVersion`.

Pour trouver le nom des blueprints, lance une analyse avec `CHESTCOUNTER_DEBUG=1` : chaque interaction est affichée
(`DBG interact … Kind Label`). Voir [Compilation → Débogage](compilation.md).

### Après tout changement de l'analyse

Augmente `public const int CurrentVersion` dans `ReplayAnalyzer`. Au lancement suivant, les replays encore présents
sont réanalysés. Les parties dont le replay a été supprimé par Fortnite gardent leur ancienne analyse.

Les modifications de la bibliothèque `lib/` doivent être notées dans `lib/PATCHES.md`.

## 9. Page Historique

`ReportWriter.cs` écrit `report.html`.

- **Le style** est la constante `Style` (du CSS), vers le début du fichier : couleurs, polices, tableaux.
- **Le contenu** est construit par `Report(…)` : les cases du haut (`<div class="tile">…`), le graphique des 30 jours,
  les tableaux « Par type », « Trésors obtenus », « Historique des parties ».

Exemple : une case « Coffres rares » parmi les cases du haut :

```csharp
<div class="tile"><div class="v">{L.N(matches.Sum(m => m.RareChests))}</div><div class="l">{L.T("coffres rares", "rare chests", "cofres raros")}</div></div>
```

Le rapport est régénéré après chaque import et au changement de compte (`ReportWriter.WriteAll`).

## 10. Ligne de commande

Les options sont lues dans `Program.Main`, dans le `switch` sur `args` :

```csharp
case "--demos" when i + 1 < args.Length: replays = Path.GetFullPath(args[++i]); break;
```

Pour ajouter une option : un `case` de plus, une variable, puis son utilisation plus bas dans `Main`. Ajoute-la au
texte de `--help` et à [Ligne de commande](ligne-de-commande.md).

## 11. Add-ons

Le fonctionnement complet est dans [Fonctionnement et sécurité](add-ons.md). Voici les modifications courantes.

### Ajouter une `metric` (une statistique pour les compteurs)

Exemple : `rares`, le nombre de coffres rares (§ 6).

| Étape | Fichier | Modification |
|---|---|---|
| 1 | `src/ChestCounter/Addons.cs` | ajouter `"rares"` dans `AddonRules.Metrics` |
| 2 | `src/ChestCounter/AddonStore.cs` | dans `AddonRuntime.Compute` : `"rares" => matches.Sum(m => m.RareChests),` |
| 3 | `addons-registry/scripts/Validate-Addon.ps1` | ajouter `'rares'` dans `$metrics` |
| 4 | `addons-registry/site-src/addon-rules.js` | ajouter `'rares'` dans `METRICS` |
| 5 | `addons-registry/site-src/creer.html` | ajouter `rares: 'Coffres rares'` dans `METRIC_TEXT` |
| 6 | `addons-registry/site-src/addon.schema.json` | ajouter `"rares"` dans l'`enum` de `metric` |
| 7 | `docs/creer-un-addon.md` | ajouter la ligne au tableau des `metric` |

Puis recompile TrackChest et republie le catalogue (`addon.ps1 publier`). Les anciennes versions de TrackChest
refuseront les add-ons qui utilisent `rares` : attends que les joueurs aient la nouvelle version avant d'en publier.

### Donner plus d'informations aux scripts

Les données reçues par `overlay(view, data)` sont construites par `AddonScript.DataJson` (`AddonScript.cs`). Ajoute un
champ à l'objet `Match(…)` ou à l'objet racine :

```csharp
rares = m.RareChests,
```

Ce que le script peut renvoyer est lu par `AddonScript.ViewFromJson` : chaque champ a une longueur maximale. Documente
tout ajout dans [Créer un add-on → Scripts](creer-un-addon.md).

### Les limites du bac à sable

Dans `AddonScript.NewEngine` :

```csharp
.TimeoutInterval(TimeSpan.FromMilliseconds(1000))   // temps maximal par mise à jour
.MaxStatements(2_000_000)                           // nombre d'instructions
.LimitMemory(64_000_000)                            // mémoire (octets)
.LimitRecursion(64)                                 // profondeur d'appels
```

{% hint style="danger" %}
N'active jamais l'accès .NET de Jint (`AllowClr`) et ne passe jamais d'objet .NET au script avec `SetValue` : le
script aurait alors accès aux fichiers et au réseau du joueur. Seules des chaînes JSON doivent entrer et sortir.
{% endhint %}

### Une nouvelle permission

Une permission doit rester limitée à de l'affichage ou à des données. Il faut :

1. l'ajouter à `AddonRules.AllPermissions` et la décrire dans `AddonRules.Describe` (texte de la fenêtre de
   confirmation) ;
2. l'utiliser dans `AddonRuntime` ;
3. l'ajouter à `Validate-Addon.ps1`, `addon-rules.js` (`PERMISSIONS`, `PERMISSION_TEXT`) et au schéma ;
4. la documenter.

## 12. Site du catalogue

Le site est dans `addons-registry/site-src/` (voir le [README du catalogue](../addons-registry/README.md)) :

| Fichier | Contenu |
|---|---|
| `index.html` | page Catalogue |
| `creer.html` | page Créer un add-on (formulaire) |
| `poster.html` | page Poster un add-on |
| `style.css` | style commun (variables `--bg`, `--gold`… en haut) |
| `addon-rules.js` | règles et `.zip`, partagés par les pages et la fonction d'envoi |
| `../functions/api/proposer.js` | fonction Cloudflare qui crée la proposition sur GitHub |

Pour tester une page, ouvre-la avec `npx wrangler pages dev site` après un `Build-Index.ps1` (dans le dossier du
catalogue), puis publie avec `.\scripts\addon.ps1 publier -Exe …\TrackChest.exe`.

## 13. Publier une nouvelle version

1. `.\Construire.ps1` : nouvel exe dans `dist\`.
2. Teste avec un dossier de données de test, puis avec ton vrai historique (fais un **Exporter** avant).
3. Commit et push de `RetryProd/TrackChest` : la documentation GitBook se met à jour toute seule.
4. Si les règles des add-ons ont changé : commit et push de `RetryProd/trackchest-addons`, puis
   `.\scripts\addon.ps1 publier`.
5. Distribue `dist\TrackChest.exe`. Le code contient une partie sous licence GPL 3 (OozSharp) : quand tu distribues
   l'exe, le code source doit être accessible aux personnes qui le reçoivent (dépôt public, ou code fourni avec
   l'exe). Voir [Crédits et licences](../CREDITS.md).

Avant chaque version, la checklist de [Contribuer](contribuer.md) reste valable.

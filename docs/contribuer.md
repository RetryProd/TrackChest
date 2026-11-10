# Contribuer

## Conventions du code

- **Textes visibles par le joueur** : chaque texte passe par `L.T("français", "English", "español", "Deutsch",
  "русский", "日本語")` (`Lang.cs`), avec les six traductions.
- **Commentaires et noms** : en anglais, comme le reste du code.
- **Données enregistrées** : ne renomme pas un champ de `MatchRecord`, `Config` ou `TrackerData` sans prévoir la lecture
  des anciens fichiers. Les historiques des joueurs doivent rester lisibles d'une version à l'autre.
- **Écritures de fichiers** : passe par `Store` (écriture atomique) plutôt que d'écrire directement.
- **Interface** : réutilise les contrôles de `CounterWindow.Layout.cs` (`RoundPanel`, `PillButton`, `PillLabel`,
  `GlowPanel`) et la palette définie en haut de ce fichier, pour garder un style cohérent.
- **Threads** : ne modifie pas l'historique pendant un scan (`_scanning`). L'overlay se met à jour avec
  `NativeOverlay.Update`, jamais en touchant sa fenêtre depuis un autre thread.

## Changer l'analyse des replays

1. Modifie `ReplayAnalyzer.cs` (ou `lib/`, en l'ajoutant dans `lib/PATCHES.md`).
2. Augmente `ReplayAnalyzer.CurrentVersion` : les replays encore présents seront réanalysés au lancement suivant.
3. Teste avec `--once` sur un dossier de replays connus et compare le résumé et le rapport avec la version précédente.
   La variable `CHESTCOUNTER_DEBUG` aide à comprendre ce que l'analyse voit ([compilation.md](compilation.md)).

Garde quelques replays de test de côté : Fortnite supprime les anciens replays de son dossier.

## Avant de proposer une modification

- [ ] `.\Construire.ps1` compile sans erreur.
- [ ] La fenêtre s'ouvre avec un dossier de données de test (`--data`), pas ton vrai historique.
- [ ] Si l'overlay change : vérifie-le avec `--overlay-preview`.
- [ ] Si le format des données change : un ancien `data.json` se charge encore.
- [ ] Si les règles des add-ons changent : `Addons.cs`, `Validate-Addon.ps1` et le guide sont à jour
      ([add-ons.md](add-ons.md)).
- [ ] La documentation de `docs/` correspond au comportement.

## Ce qui ne doit jamais être versionné

- `dist/`, `bin/`, `obj/` : produits de compilation.
- Des fichiers `.replay` : ils contiennent les parties et les pseudos d'autres joueurs.
- Un historique (`data.json`) ou une configuration de joueur.
- **La clé privée du catalogue** (`addon-private.key`). Seule `assets/addon-public.key` est publique.

Le `.gitignore` du projet couvre ces cas, mais vérifie `git status` avant chaque commit.

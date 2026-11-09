# TrackChest

<img src="assets/Chest.png" alt="" width="96" align="right">

**Compteur de coffres Fortnite pour Windows.** TrackChest lit les replays que Fortnite enregistre sur ton PC, compte
les coffres que tu as ouverts ou cassés et les canons à butin que tu as tirés, puis affiche le résultat dans un
overlay par-dessus le jeu. L'historique est gardé par compte Epic, partie par partie.

- **Overlay en jeu** : total de coffres, dernière partie, session, trésors et chance cumulée d'obtenir le jeton pirate.
- **Historique détaillé** : une page locale qui déroule chaque partie (type et moment de chaque coffre, cartes Wanted,
  trésors).
- **Plusieurs comptes** : chaque compte Epic qui a enregistré des replays sur le PC a son propre profil.
- **Sauvegarde** : export et import de tout l'historique dans un fichier `.zip`, pour changer de PC.
- **Add-ons** : des compteurs et des thèmes pour l'overlay, installés depuis un catalogue signé. Un add-on ne contient
  aucun code.

Aucune donnée ne quitte le PC, sauf le téléchargement du catalogue d'add-ons quand tu ouvres la fenêtre Add-on.

## Utilisation

1. Dans Fortnite, active **Enregistrer les replays** et choisis le mode d'affichage **Plein écran fenêtré** (sinon
   l'overlay ne peut pas s'afficher par-dessus le jeu).
2. Lance `dist\TrackChest.exe` (ou `Lancer-TrackChest.cmd`) et confirme le dossier des replays, en général
   `%LOCALAPPDATA%\FortniteGame\Saved\Demos`.
3. Laisse TrackChest ouvert ou réduit. Il importe les replays existants, puis vérifie les nouveaux fichiers toutes les
   quatre secondes. Les chiffres d'une partie arrivent quand Fortnite finalise son replay, à la fin de la partie.

| Raccourci | Effet |
|---|---|
| **Ctrl+Maj+O** | afficher ou masquer l'overlay |
| **Ctrl+Maj+L** | déverrouiller l'overlay pour le déplacer (verrouillé, il laisse passer les clics) |

Le bouton **Paramètres** règle la taille et l'opacité de l'overlay, le dossier des replays et l'ouverture automatique
avec Fortnite.

Fortnite ne garde qu'un nombre limité de replays et supprime les plus anciens. Un replay importé reste compté dans
TrackChest, même après sa suppression par Fortnite.

## Compiler

Il faut Windows x64 et le SDK .NET 10. Depuis PowerShell :

```powershell
.\Construire.ps1
```

L'exécutable autonome est produit dans `dist\`. Les détails sont dans [docs/compilation.md](docs/compilation.md).

## Documentation

| Document | Contenu |
|---|---|
| [docs/architecture.md](docs/architecture.md) | organisation du code, rôle de chaque fichier, chemin d'un replay jusqu'à l'overlay |
| [docs/modifier-trackchest.md](docs/modifier-trackchest.md) | guide pour tout modifier : textes, couleurs, fenêtres, overlay, statistiques, analyse, add-ons, site |
| [docs/compilation.md](docs/compilation.md) | prérequis, compilation, icône, ressources intégrées, débogage |
| [docs/donnees.md](docs/donnees.md) | fichiers de données, format de l'historique, sauvegarde, migration |
| [docs/ligne-de-commande.md](docs/ligne-de-commande.md) | options de l'exécutable et scripts PowerShell |
| [docs/creer-un-addon.md](docs/creer-un-addon.md) | guide pour écrire et proposer un add-on |
| [docs/add-ons.md](docs/add-ons.md) | système d'add-ons : format, sécurité, catalogue signé, publication |
| [docs/contribuer.md](docs/contribuer.md) | conventions du code et vérifications avant une modification |
| [addons-registry/README.md](addons-registry/README.md) | modèle du dépôt public du catalogue d'add-ons |

## Licence et crédits

TrackChest s'appuie sur [Fortnite-ChestCounter](https://github.com/LED-N/Fortnite-ChestCounter) (MIT),
[FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) (MIT) et OozSharp (GPL 3). Le
programme combiné est distribué sous **GPL version 3 ou ultérieure**. Le détail est dans [CREDITS.md](CREDITS.md).

TrackChest n'est ni affilié à Epic Games ni approuvé par Epic Games. Fortnite est une marque d'Epic Games, Inc.

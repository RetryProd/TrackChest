# Données

## Où sont les fichiers

Tout est dans `%APPDATA%\TrackChest` (par exemple `C:\Users\toi\AppData\Roaming\TrackChest`). L'option `--data`
choisit un autre dossier.

| Fichier | Contenu |
|---|---|
| `config.json` | réglages (voir plus bas) |
| `data.json` | l'historique : toutes les parties analysées |
| `report.html` | la page Historique, régénérée après chaque changement |
| `treasures.json` | noms des trésors, modifiables à la main |
| `errors.log` | replays illisibles, avec la raison |
| `addons/` | add-ons installés (voir [add-ons.md](add-ons.md)) |

Les fichiers JSON sont écrits de façon atomique : TrackChest écrit d'abord un fichier `.tmp`, puis remplace l'ancien.
Si un fichier est illisible au chargement, une copie `….corrupt-AAAAMMJJ-HHMMSS` est gardée et TrackChest repart d'un
fichier vide, sans écraser l'original.

### Migration depuis ChestOpen

L'application s'appelait ChestOpen. Au premier lancement, si `%APPDATA%\TrackChest` n'existe pas et que
`%APPDATA%\ChestOpen` existe, le dossier est renommé : historique et réglages sont conservés. Si le renommage est
impossible (dossier ouvert par une autre application), TrackChest continue d'utiliser l'ancien dossier.

## config.json

| Clé | Rôle |
|---|---|
| `DemosFolder` | dossier des replays Fortnite |
| `Profile` | compte affiché (identifiant Epic ou nom). Vide = le compte joué le plus récemment |
| `OverlayX`, `OverlayY` | position de l'overlay |
| `OverlayLocked` | `true` = l'overlay laisse passer les clics et ne se déplace pas |
| `OverlaySize` | taille de l'overlay en %, de 70 à 175 |
| `OverlayOpacity` | opacité en %, de 30 à 100 |
| `AddonCatalogUrl` | adresse d'un autre catalogue d'add-ons (facultatif). Seul un catalogue signé par la clé intégrée à l'exe est accepté |

Les réglages se changent depuis la fenêtre Paramètres ; modifier le fichier à la main n'est utile que pour
`AddonCatalogUrl`. Ferme TrackChest avant, sinon il réécrit le fichier.

## data.json

```jsonc
{
  "Version": 1,
  "AnalyzerVersion": 12,          // version de l'analyse qui a produit l'historique
  "CreatedUtc": "…",
  "Matches": [ /* une entrée par partie, voir ci-dessous */ ],
  "ProcessedFiles": {             // replays déjà traités : "chemin|taille|date" → statut
    "C:\\…\\UnsavedReplay-2026.11.09-15.12.40.replay|1234567|638…": "lu"
  }
}
```

Statuts de `ProcessedFiles` : `lu` (analysé), `incomplet` (replay sans clé de chiffrement, ou resté marqué « en cours »), `erreur` (illisible, voir
`errors.log`), `autre mode` (pas une partie Battle Royale). Le bouton **Actualiser** réessaie les replays en erreur.

### Une partie (`MatchRecord`)

| Champ | Contenu |
|---|---|
| `SessionId` | identifiant de la partie Fortnite. Avec le compte, il évite de compter une partie deux fois |
| `ReplayFile` | nom du fichier replay |
| `MatchStartUtc`, `DurationSeconds` | début et durée de la partie |
| `Playlist`, `GameVersion` | mode de jeu et version de Fortnite |
| `PlayerName`, `EpicId` | compte suivi |
| `Placement`, `TotalPlayers`, `Eliminations` | classement et éliminations |
| `ChestsOpened` | coffres ouverts |
| `Chests` | chaque coffre : moment (secondes), type, blueprint, `Destroyed` (cassé), objets sortis |
| `OtherInteractions` | autres conteneurs par type : canons à butin, caisses de munitions, de nourriture… |
| `WantedMaps` | moments où une carte Wanted a été ramassée (absent pour les anciennes analyses) |
| `TreasureFinds` | trésors obtenus, avec leur source et leur moment |
| `AnalyzerVersion` | version de l'analyse de cette partie |

Les valeurs calculées (`ChestsTotal`, `ChestsDestroyed`, `SpriteChests`, `LootCannons`) ne sont pas enregistrées :
elles sont recalculées à partir des champs ci-dessus.

Les types de coffres sont enregistrés en français (« Coffre », « Coffre rare », « Canon à butin »…) pour rester
compatibles avec les anciens historiques.

## treasures.json

Associe l'identifiant d'un trésor envoyé par le serveur à un nom lisible :

```json
{
  "DelilahPrize:dpid_item4": "No Trespassing (piste musicale)",
  "Unknown:2026-11-09_15.12@7m16s": ""
}
```

Les identifiants inconnus sont ajoutés automatiquement avec un nom vide : complète-les et le rapport les affichera.

## Sauvegarde et restauration

Le bouton **Exporter** crée un `.zip` qui contient `data.json` et `treasures.json`. Le bouton **Importer** fusionne une
sauvegarde dans l'historique en place :

- une partie déjà présente (même `SessionId`, même compte) n'est pas ajoutée une seconde fois ;
- une partie présente dans les deux est remplacée seulement si la sauvegarde contient une analyse plus récente ;
- les noms de trésors vides sont complétés, les noms existants sont gardés ;
- si la sauvegarde vient d'une autre version de l'analyse, les replays encore présents sur le PC sont réanalysés au
  scan suivant.

Les réglages (`config.json`) ne sont pas dans la sauvegarde : ils dépendent du PC (dossier, position de l'overlay).
Pendant un export ou un import, la surveillance du dossier est mise en pause.

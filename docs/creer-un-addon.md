# Créer un add-on

Un add-on ajoute des **compteurs** et/ou un **thème** à l'overlay de TrackChest. C'est un fichier de données, jamais du
code : il n'y a rien à programmer, seulement un fichier JSON à remplir.

TrackChest valide lui-même chaque add-on. Un add-on ne peut ni lancer un programme, ni lire des fichiers, ni utiliser
Internet : il ne peut faire que ce qu'il déclare dans ses `permissions`, que le joueur voit avant d'installer.

## 1. Le fichier manifest.json

Dans le dépôt du catalogue, crée un dossier `addons/mon-addon/` avec un seul fichier, `manifest.json`, enregistré en
UTF-8 **sans BOM**. Le nom du dossier est identique à l'`id`.

### Exemple : un compteur

```json
{
  "schema": 1,
  "id": "mon-compteur",
  "name": "Mon compteur",
  "version": "1.0.0",
  "author": "TonPseudo",
  "description": "Affiche mes coffres cassés et mes canons à butin.",
  "permissions": ["stats.read", "overlay.rows"],
  "counters": [
    { "label": "Cassés", "metric": "destroyed", "scope": "total", "showLast": true },
    { "label": "Canons", "metric": "cannons", "scope": "session" }
  ]
}
```

### Exemple : un thème

```json
{
  "schema": 1,
  "id": "mon-theme",
  "name": "Mon thème",
  "version": "1.0.0",
  "author": "TonPseudo",
  "description": "Un overlay rouge sombre.",
  "permissions": ["overlay.theme"],
  "theme": {
    "background": "#14080A",
    "accent": "#FF5A5F",
    "text": "#FFF1F1"
  }
}
```

## 2. Référence

### Champs généraux

| Champ | Règle |
|---|---|
| `schema` | toujours `1` |
| `id` | 3 à 40 caractères : minuscules, chiffres et tirets (`mon-addon`). Unique dans le catalogue |
| `name` | 1 à 40 caractères |
| `version` | format `1.0.0`. Augmente-la à chaque modification, sinon les joueurs ne voient pas de mise à jour |
| `author` | 1 à 40 caractères |
| `description` | 200 caractères maximum |
| `permissions` | la liste de ce que l'add-on utilise (voir ci-dessous). Une permission inutilisée est refusée |
| `counters` | jusqu'à 8 compteurs (facultatif) |
| `theme` | des couleurs (facultatif). Il faut au moins un compteur ou un thème |

Toute autre clé est refusée.

### Permissions

| Permission | Ce qu'elle permet |
|---|---|
| `stats.read` | lire les statistiques (coffres, parties, trésors) pour calculer un compteur |
| `overlay.rows` | ajouter des lignes à l'overlay |
| `overlay.theme` | changer les couleurs de l'overlay |

Les compteurs demandent `stats.read` et `overlay.rows`. Un thème demande `overlay.theme`.

### Compteurs

| Champ | Valeurs |
|---|---|
| `label` | le texte de la ligne, 24 caractères maximum |
| `metric` | ce qu'on compte, voir le tableau suivant |
| `scope` | `total` (toutes les parties), `session` (depuis le lancement de TrackChest), `last` (la dernière partie) |
| `showLast` | `true` ajoute « · n dernière partie » après le total (avec `scope` = `total`) |

| `metric` | Ce que ça compte |
|---|---|
| `chests` | coffres ouverts ou cassés, plus les canons à butin (le total de TrackChest) |
| `opened` | coffres ouverts |
| `destroyed` | coffres cassés |
| `sprites` | coffres d'esprit |
| `wanted` | cartes Wanted ramassées |
| `cannons` | canons à butin tirés |
| `treasures` | trésors trouvés |
| `matches` | nombre de parties |

L'overlay affiche au plus 10 lignes d'add-ons au total, tous add-ons confondus.

### Thème

Toutes les couleurs sont facultatives, au format `#RRGGBB`. Une couleur absente garde l'aspect d'origine.

| Clé | Couleur de… |
|---|---|
| `background` | le fond de l'overlay |
| `border` | la bordure arrondie |
| `line` | les traits de séparation |
| `muted` | les textes secondaires (légendes, noms des lignes) |
| `text` | le texte principal (valeurs) |
| `accent` | le total de coffres et le badge « partie en cours » |
| `treasure` | la ligne Trésors |

Un seul thème est actif à la fois : en activer un désactive l'autre.

## 3. Vérifier son add-on

Dans le dépôt du catalogue :

```powershell
.\scripts\Validate-Addon.ps1 -Id mon-addon
```

Le script affiche toutes les erreurs : clé inconnue, couleur invalide, fichier en trop (un add-on ne contient que
`manifest.json`), etc. TrackChest refait les mêmes contrôles à l'installation.

Pour vérifier un paquet déjà construit :

```powershell
.\TrackChest.exe --addon-check site\mon-addon-1.0.0.zip
```

Dans VS Code, le schéma `addon.schema.json` (publié avec le catalogue, source dans
`addons-registry/site-src/`) donne l'autocomplétion et souligne les erreurs pendant que tu écris. Ajoute dans tes
réglages :

```json
"json.schemas": [
  { "fileMatch": ["**/addons/*/manifest.json"], "url": "./site-src/addon.schema.json" }
]
```

Pour essayer ton add-on avant de le proposer, construis le catalogue en local
(`.\scripts\Build-Index.ps1 -BaseUrl https://exemple.invalid`), puis installe le `.zip` produit dans `site\` avec
**Add-on → Ajouter un fichier…**. Il apparaît comme « Local, non vérifié ».

## 4. Proposer son add-on

1. Copie (« fork ») le dépôt GitHub du catalogue.
2. Ajoute ton dossier `addons/mon-addon/manifest.json`.
3. Ouvre une **pull request**. Les contrôles automatiques se lancent et indiquent ce qui ne va pas.
4. Le mainteneur relit ton manifeste, l'accepte et publie le catalogue. Les joueurs le voient ensuite dans
   **Add-on → Actualiser**.

## 5. Niveaux de confiance

| Niveau | Signification |
|---|---|
| ✓ Vérifié | relu par le mainteneur, catalogue signé |
| Communauté | contrôles automatiques seulement : TrackChest affiche un avertissement |
| Local | fichier ajouté à la main par un joueur, non vérifié |

Le catalogue est signé : tout fichier modifié après publication est refusé par TrackChest, et un add-on dangereux peut
être révoqué à distance. Le fonctionnement complet est dans [Fonctionnement et sécurité](add-ons.md).

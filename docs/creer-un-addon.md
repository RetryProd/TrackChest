# Créer un add-on

Un add-on change l'overlay de TrackChest. Il peut :

- ajouter des **compteurs** (un fichier JSON à remplir, sans programmation) ;
- changer les couleurs avec un **thème** (même principe) ;
- tout modifier, ajouter ou supprimer avec un **script** JavaScript (voir [Scripts](#6-scripts--tout-modifier)).

TrackChest valide lui-même chaque add-on. Un add-on ne peut ni lancer un programme, ni lire des fichiers, ni utiliser
Internet : il ne peut faire que ce qu'il déclare dans ses `permissions`, que le joueur voit avant d'installer. Les
scripts tournent dans un bac à sable.

## Le plus rapide

Il y a deux façons de créer un add-on :

- **Le site** : <https://trackchest-addons.pages.dev/creer.html>. Tu remplis un formulaire, l'add-on se vérifie en
  direct, puis tu le télécharges en `.zip` ou tu le postes directement (voir
  [Proposer son add-on](#4-proposer-son-add-on)). Aucun outil à installer, aucun compte.
- **La commande `addon.ps1`**, pour développer sur ton PC (voir [Développer avec la commande
  addon.ps1](#3-développer-avec-la-commande-addonps1)) :

  ```powershell
  .\scripts\addon.ps1 nouveau mon-addon      # crée le fichier à remplir
  .\scripts\addon.ps1 verifier mon-addon     # affiche les erreurs
  .\scripts\addon.ps1 empaqueter mon-addon   # crée un .zip à essayer dans TrackChest
  ```

## 1. Le fichier manifest.json

Dans le dépôt du catalogue, crée un dossier `addons/mon-addon/` avec `manifest.json`, enregistré en UTF-8 **sans BOM**.
Le nom du dossier est identique à l'`id`. Tu peux aussi y placer un script `script.js` (voir
[Scripts](#6-scripts--tout-modifier)) et des icônes `.png` (32 Ko maximum chacune, 10 fichiers au maximum dans tout le
paquet).

### Exemple : un compteur

```json
{
  "schema": 1,
  "id": "mon-compteur",
  "name": "Mon compteur",
  "version": "1.0.0",
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
| `description` | 200 caractères maximum |
| `permissions` | la liste de ce que l'add-on utilise (voir ci-dessous). Une permission inutilisée est refusée |
| `counters` | jusqu'à 8 compteurs (facultatif) |
| `theme` | des couleurs (facultatif). Il faut au moins un compteur, un thème ou un script (`script.js`) |

Toute autre clé est refusée.

### Permissions

| Permission | Ce qu'elle permet |
|---|---|
| `stats.read` | lire les statistiques (coffres, parties, trésors) pour calculer un compteur |
| `overlay.rows` | ajouter des lignes à l'overlay |
| `overlay.theme` | changer les couleurs de l'overlay |
| `overlay.script` | exécuter `script.js`, qui peut modifier, ajouter ou supprimer tout ce qu'affiche l'overlay |

Les compteurs demandent `stats.read` et `overlay.rows`. Un thème demande `overlay.theme`. Un script demande
`stats.read` et `overlay.script`.

### Compteurs

| Champ | Valeurs |
|---|---|
| `label` | le texte de la ligne, 24 caractères maximum |
| `metric` | ce qu'on compte, voir le tableau suivant |
| `scope` | `total` (toutes les parties), `session` (depuis le lancement de TrackChest), `last` (la dernière partie) |
| `showLast` | `true` ajoute « · n dernière partie » après le total (avec `scope` = `total`) |
| `icon` | facultatif : nom d'une icône intégrée ou d'un fichier `.png` placé dans le dossier de l'add-on (lettres, chiffres, `_` et `-`, 32 caractères max avant `.png`) |

| `metric` | Ce que ça compte | Icône par défaut (si `icon` est absent) |
|---|---|---|
| `chests` | coffres ouverts ou cassés, plus les canons à butin (le total de TrackChest) | `Chest.png` |
| `opened` | coffres ouverts | `Chest.png` |
| `destroyed` | coffres cassés | `break_chest.png` |
| `buried` | coffres enterrés | `Chest.png` |
| `sprites` | coffres d'esprit | `Esprit_Chest.png` |
| `wanted` | cartes Wanted ramassées | `Wanted_Card.png` |
| `cannons` | canons à butin tirés | `Loot_Cannons.png` |
| `treasures` | trésors trouvés | `Treasures.png` |
| `matches` | nombre de parties | aucune |

Icônes intégrées disponibles sans rien ajouter au `.zip` : `Chest.png`, `Treasures.png`, `Pirate_Token.png`,
`break_chest.png`, `Esprit_Chest.png`, `Wanted_Card.png`, `Loot_Cannons.png`. Pour une icône personnalisée, place
simplement ton fichier (par exemple `mon_icone.png`, ≤ 32 Ko) à côté de `manifest.json` et écris
`"icon": "mon_icone.png"`.

L'overlay affiche au plus 10 lignes de compteurs d'add-ons au total, tous add-ons confondus. Depuis la fenêtre
**Add-on**, le bouton **Configurer** sur un add-on à compteurs permet au joueur d'afficher ou masquer chaque ligne
individuellement.

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

## 3. Développer avec la commande addon.ps1

Copie le dépôt du catalogue sur ton PC, puis, dans son dossier :

| Commande | Effet |
|---|---|
| `.\scripts\addon.ps1 nouveau mon-addon` | crée `addons\mon-addon\manifest.json` à remplir (`-Type theme` pour un thème, `-Type script` pour un script avec `script.js`) |
| `.\scripts\addon.ps1 verifier [mon-addon]` | affiche toutes les erreurs d'un add-on (ou de tous) : clé inconnue, couleur invalide, fichier en trop… |
| `.\scripts\addon.ps1 empaqueter mon-addon` | crée `dist\mon-addon-1.0.0.zip` ; avec `-Exe chemin\TrackChest.exe`, applique aussi le contrôle de TrackChest |
| `.\scripts\addon.ps1 aide` | liste des commandes |

TrackChest refait les mêmes contrôles à l'installation.

La commande `publier` est réservée au mainteneur : elle construit, signe et publie le catalogue.

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

Pour **essayer** ton add-on avant de le proposer, installe le `.zip` créé par `empaqueter` avec **Add-on → Ajouter un
fichier…** dans TrackChest. Il apparaît comme « Local, non vérifié ».

## 4. Proposer son add-on

Tout se fait sur le site du catalogue, **sans compte** : <https://trackchest-addons.pages.dev/>.

| Page | Rôle |
|---|---|
| **Catalogue** (`/`) | tous les add-ons publiés, avec recherche et téléchargement du `.zip` |
| **Créer un add-on** (`/creer.html`) | le formulaire : compteurs, thème, script ; puis **Télécharger le .zip** ou **Poster cet add-on** |
| **Poster un add-on** (`/poster.html`) | glisse un `.zip` (fait avec le formulaire ou `addon.ps1 empaqueter`) puis **Poster l'add-on** |

1. Poste ton add-on : le site vérifie le `.zip` (mêmes règles que TrackChest) avant de l'envoyer.
2. Le site crée lui-même une **proposition** (pull request) sur le dépôt GitHub du catalogue. Tu reçois son lien.
3. Les contrôles automatiques se lancent sur la proposition.
4. Le mainteneur la relit, l'accepte et publie le catalogue. Les joueurs voient ensuite l'add-on dans
   **Add-on → Actualiser**.

Pour mettre à jour un add-on, poste-le de nouveau avec une `version` plus grande et le même `id`.

## 5. Niveaux de confiance

| Niveau | Signification |
|---|---|
| ✓ Vérifié | relu par le mainteneur, catalogue signé |
| Communauté | contrôles automatiques seulement : TrackChest affiche un avertissement |
| Local | fichier ajouté à la main par un joueur, non vérifié |

Le catalogue est signé : tout fichier modifié après publication est refusé par TrackChest, et un add-on dangereux peut
être révoqué à distance. Le fonctionnement complet est dans [Fonctionnement et sécurité](add-ons.md).

## 6. Scripts : tout modifier

Un add-on à script contient deux fichiers : `manifest.json` (avec les permissions `stats.read` et `overlay.script`) et
`script.js`. Le script définit une fonction `overlay(view, data)`. TrackChest l'appelle à chaque mise à jour de
l'overlay : elle reçoit l'overlay tel qu'il va s'afficher, le modifie comme elle veut, puis le renvoie.

```powershell
.\scripts\addon.ps1 nouveau mon-script -Type script   # crée manifest.json et un script.js d'exemple
```

```javascript
// Retire la ligne « Jeton pirate », renomme l'en-tête et ajoute le record de coffres en une partie.
function overlay(view, data) {
  view.header = "MES COFFRES";
  view.rows = view.rows.filter(function (row) { return row.label !== "Jeton pirate"; });

  var record = 0;
  data.matches.forEach(function (m) { if (m.chests > record) record = m.chests; });
  view.rows.push({
    label: "Record",
    value: String(record),
    detail: "· en une partie",
    color: "#FFC860",
    icon: "Chest.png",
    separator: true
  });

  return view;
}
```

Si plusieurs scripts sont actifs, chacun reçoit le résultat du précédent, dans l'ordre d'installation. Les compteurs et
le thème d'autres add-ons sont appliqués avant les scripts.

### `view` : ce que le script peut changer

| Champ | Contenu | Limite |
|---|---|---|
| `header` | texte de l'en-tête (« COFFRES ») | 24 caractères |
| `total` | grand chiffre de gauche | 16 caractères |
| `totalCaption` | légende sous le total (« total ») | 24 caractères |
| `current` | grand chiffre de droite (dernière partie ou partie en cours), `null` = « — » | 16 caractères |
| `currentCaption` | légende sous ce chiffre | 24 caractères |
| `badge` | badge en haut à droite (« ● PARTIE EN COURS »), `null` = aucun | 32 caractères |
| `rows` | lignes de l'overlay, dans l'ordre d'affichage | 16 lignes au maximum |

Chaque ligne de `rows` : `label` (24 caractères), `value` (32), `detail` (48, texte gris après la valeur, `null` =
aucun), `color` (couleur de la valeur, `#RRGGBB`, facultatif), `treasure` (`true` = couleur de la ligne Trésors),
`icon` (nom d'icône intégrée ou `.png` du paquet, `null` = aucune) et `separator` (`true` = dessine un trait horizontal
au-dessus de la ligne).

Pour **supprimer** une ligne, retire-la de `rows` ; pour l'**ajouter**, ajoute un objet ; pour la **modifier**, change
ses champs ; pour **réordonner**, change l'ordre du tableau. Un champ absent du résultat garde sa valeur. Les textes
trop longs sont coupés, les caractères de contrôle retirés.

### `data` : les statistiques

| Champ | Contenu |
|---|---|
| `player` | nom du compte suivi |
| `inProgress` | `true` pendant une partie |
| `language` | langue de l'interface (`fr`) |
| `total` | `{ matches, chests, opened }` sur tout l'historique du compte |
| `session` | `{ matches, chests }` depuis le lancement de TrackChest |
| `matches` | les parties, **la plus récente en premier** (500 au maximum) |

Chaque partie de `matches` : `start` (date ISO), `durationSeconds`, `playlist`, `placement`, `totalPlayers`,
`eliminations`, `chests` (total TrackChest), `opened`, `destroyed`, `sprites`, `buried`, `cannons`, `wanted` (`null`
si inconnu), `treasures`, `inSession`, et `chestList` : chaque coffre avec `time` (secondes), `kind` (type) et
`destroyed`.

### Bac à sable et limites

Le script tourne dans un interpréteur JavaScript intégré à TrackChest, sans aucun accès à Windows : pas de fichiers,
pas d'Internet (`fetch`, `require` et consorts n'existent pas), pas de programme. Seules des données JSON entrent et
sortent.

| Limite | Valeur |
|---|---|
| taille de `script.js` | 32 Ko, UTF-8 sans BOM |
| temps par mise à jour | 1 seconde |
| instructions | 2 millions |
| mémoire | 64 Mo |
| profondeur de récursion | 64 |

Si le script dépasse une limite, plante ou renvoie un résultat invalide (couleur mal formée, plus de 16 lignes…),
l'overlay s'affiche sans lui. Après trois échecs de suite, il est mis de côté jusqu'au prochain redémarrage de
TrackChest ou à sa réactivation. L'erreur s'affiche dans la fenêtre **Add-on**, sous le nom de l'add-on.

### Tester et proposer un script

1. `.\scripts\addon.ps1 empaqueter mon-script -Exe chemin\TrackChest.exe` : TrackChest compile le script et vérifie
   qu'il définit `overlay()`.
2. Dans TrackChest : **Add-on → Ajouter un fichier…**, choisis le `.zip`, puis regarde l'overlay. Pour un aperçu sans
   lancer Fortnite : `TrackChest.exe --overlay-preview apercu.png` (voir [Compilation](compilation.md)).
3. Pour le proposer : envoie le `.zip` sur la page **Poster un add-on** du site, ou, sur **Créer un add-on**, coche
   **Ajouter un script**, colle ton code et clique sur **Poster cet add-on**.

Un script est relu avec plus d'attention qu'un compteur : écris un code clair et commente ce qu'il fait.

# Add-ons

Cette page décrit le système d'add-ons du point de vue du code de TrackChest. Pour **écrire** un add-on, voir
[Créer un add-on](creer-un-addon.md) (le générateur de manifeste est publié avec le catalogue :
<https://trackchest-addons.pages.dev/>, c'est là qu'on propose un add-on). Pour **publier** le catalogue, voir
[addons-registry/README.md](../addons-registry/README.md).

## Principe : des données, jamais du code

Un add-on est un paquet `.zip` qui contient **un seul fichier**, `manifest.json`. Il peut déclarer :

- des **compteurs** : des lignes ajoutées à l'overlay, calculées par TrackChest à partir de l'historique ;
- un **thème** : les couleurs de l'overlay.

TrackChest lit le manifeste et fait lui-même les calculs et le dessin. Un add-on ne peut donc rien exécuter, ni lire de
fichier, ni utiliser le réseau. C'est la principale garantie de sécurité ; la signature et la relecture viennent en
plus.

```json
{
  "schema": 1,
  "id": "stats-detail",
  "name": "Statistiques détaillées",
  "version": "1.0.0",
  "description": "Ajoute à l'overlay les coffres cassés, les coffres d'esprit, les cartes Wanted et les canons à butin.",
  "permissions": ["stats.read", "overlay.rows"],
  "counters": [
    { "label": "Cassés", "metric": "destroyed", "scope": "total", "showLast": true }
  ]
}
```

La référence complète des champs est dans le guide développeur et dans le schéma
`addons-registry/site-src/addon.schema.json`.

## Validation (`Addons.cs`)

`AddonPackage.Read` refuse le paquet dès qu'une règle n'est pas respectée :

| Contrôle | Règle |
|---|---|
| Taille | paquet ≤ 64 Ko, manifeste ≤ 16 Ko (la lecture s'arrête au-delà, contre les bombes zip) |
| Contenu | exactement une entrée, nommée `manifest.json` : pas de dossier, pas de `../`, pas d'autre fichier |
| JSON | lecture stricte : une clé inconnue est une erreur (`UnmappedMemberHandling.Disallow`), pas de BOM |
| Champs | `id` en minuscules, chiffres et tirets ; version `x.y.z` ; longueurs maximales ; pas de caractères de contrôle |
| Valeurs | `metric`, `scope`, permissions et couleurs prises dans des listes fixes |
| Cohérence | chaque fonction demande sa permission, et chaque permission doit servir |

Les mêmes règles sont appliquées par `addons-registry/scripts/Validate-Addon.ps1` (dépôt du catalogue), par
`TrackChest.exe --addon-check` et par le générateur de manifeste de la page du site
(`addons-registry/site-src/index.html`, fonction `validate`). Si une règle change, modifie les quatre, ainsi que
`addon.schema.json`.

### Permissions

| Permission | Effet dans le code |
|---|---|
| `stats.read` | autorise le calcul des compteurs à partir des parties |
| `overlay.rows` | `AddonRuntime.CounterRows` ajoute les lignes de l'add-on à l'overlay |
| `overlay.theme` | `AddonRuntime.Load` passe le thème à `NativeOverlay.SetTheme` |

Au plus 10 lignes d'add-ons sont affichées en tout. Un seul thème est actif : en activer un désactive les autres.

## Confiance

### Niveaux

| Niveau | Origine | Affichage |
|---|---|---|
| Vérifié | catalogue signé, add-on listé dans `registry.json → verified` (relu) | sans avertissement |
| Communauté | catalogue signé, non relu | avertissement à l'installation |
| Local | fichier choisi par le joueur (**Ajouter un fichier…**) | avertissement fort |

Avant toute installation, une fenêtre affiche le nom, la version, le niveau de confiance et les permissions.

### Catalogue signé (`AddonStore.cs`)

Le catalogue est un site statique : `index.json`, sa signature `index.json.sig`, et un `.zip` par add-on.

1. `AddonCatalog.FetchIndexAsync` télécharge `index.json` (≤ 256 Ko) et `index.json.sig`, en **HTTPS uniquement** (une
   redirection vers HTTP est refusée).
2. `AddonTrust.Verify` vérifie la signature **ECDSA P-256 / SHA-256** avec la clé publique intégrée à l'exe
   (`assets/addon-public.key`). Si elle ne correspond pas, rien du catalogue n'est utilisé.
3. Les entrées invalides (adresse non HTTPS, empreinte mal formée, taille hors limite…) sont écartées.
4. À l'installation, `AddonCatalog.DownloadAsync` télécharge le paquet, vérifie sa **taille** et son **SHA-256** contre
   le catalogue, le valide, puis vérifie que l'identifiant, la version et les **permissions** du manifeste sont
   exactement ceux annoncés par le catalogue (ceux que le joueur a vus).

La clé privée ne quitte jamais le PC du mainteneur : la signature se fait en local avec `--addon-sign`. Un attaquant qui
modifie l'hébergement ne peut pas produire une signature valide.

Sans `assets/addon-public.key` à la compilation, `AddonTrust.IsConfigured` est faux : le catalogue est désactivé et seuls
les fichiers locaux peuvent être installés.

### Révocation

`index.json` contient une liste `revoked` (`"id"` ou `"id@version"`). À chaque lecture du catalogue,
`AddonStore.ApplyRevocations` désactive et marque les add-ons installés concernés ; ils ne peuvent plus être réactivés.

### Intégrité sur le disque

Les add-ons installés sont dans `%APPDATA%\TrackChest\addons\<id>\package.zip`, avec `addons\state.json` (version,
niveau, activé, révoqué, SHA-256, date). À chaque chargement, le paquet est revalidé et son SHA-256 comparé à celui de
l'installation : un paquet modifié après coup est ignoré.

## Ajouter une possibilité aux add-ons

Par exemple une nouvelle `metric` :

1. `Addons.cs` : ajoute la valeur dans `AddonRules.Metrics`.
2. `AddonStore.cs` : calcule-la dans `AddonRuntime.Compute`.
3. `addons-registry/scripts/Validate-Addon.ps1` : ajoute-la dans `$metrics`.
4. `addons-registry/site-src/addon.schema.json` et `index.html` : documente-la.
5. Republie le catalogue pour mettre en ligne le guide à jour.

Une nouvelle **permission** demande plus de prudence : elle doit rester limitée à des données ou à de l'affichage, et
être décrite dans `AddonRules.Describe` pour la fenêtre de confirmation. Ne permets jamais à un add-on de fournir du code,
un chemin de fichier ou une adresse réseau.

## Publier le catalogue

Résumé (procédure complète dans [addons-registry/README.md](../addons-registry/README.md)) :

```powershell
# une seule fois : clés, puis clé publique et adresse dans assets\, puis recompilation
.\dist\TrackChest.exe --addon-keygen "$env:USERPROFILE\TrackChest-cles"

# à chaque mise à jour, dans le dépôt du catalogue : contrôle, construction, signature, publication
.\scripts\addon.ps1 publier -Exe <chemin>\TrackChest.exe
```

`addon.ps1 publier` enchaîne `Build-Index.ps1`, `TrackChest.exe --addon-sign` et `wrangler pages deploy`.

Si la clé privée est perdue, il faut créer une nouvelle paire, mettre la nouvelle clé publique dans `assets\`, publier
une nouvelle version de TrackChest et signer le catalogue avec la nouvelle clé. Si elle fuit, il faut faire la même
chose au plus vite : avec l'ancienne clé, n'importe qui peut signer un faux catalogue.

# Add-ons

Cette page décrit le système d'add-ons du point de vue du code de TrackChest. Pour **écrire** un add-on, voir
[Créer un add-on](creer-un-addon.md) (le générateur de manifeste est publié avec le catalogue :
<https://trackchest-addons.pages.dev/>, c'est là qu'on propose un add-on). Pour **publier** le catalogue, voir
[addons-registry/README.md](../addons-registry/README.md).

## Principe : des données, et des scripts dans un bac à sable

Un add-on est un paquet `.zip` qui contient `manifest.json`, et `script.js` pour un add-on à script. Il peut déclarer :

- des **compteurs** : des lignes ajoutées à l'overlay, calculées par TrackChest à partir de l'historique ;
- un **thème** : les couleurs de l'overlay ;
- un **script** (`script.js`) : une fonction `overlay(view, data)` qui peut modifier, ajouter ou supprimer tout ce
  qu'affiche l'overlay.

Pour les compteurs et le thème, TrackChest lit le manifeste et fait lui-même les calculs et le dessin. Les scripts
tournent dans un bac à sable (voir [Scripts](#scripts-addonscriptcs)) : ils ne peuvent ni lire de fichier, ni utiliser
le réseau, ni lancer un programme. La signature du catalogue et la relecture viennent en plus.

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
| Taille | paquet ≤ 64 Ko, manifeste ≤ 16 Ko, `script.js` ≤ 32 Ko (la lecture s'arrête au-delà, contre les bombes zip) |
| Contenu | `manifest.json`, et `script.js` seulement avec la permission `overlay.script` : pas de dossier, pas de `../`, pas d'autre fichier |
| Script | UTF-8 sans BOM ; compilé et exécuté une fois dans le bac à sable ; doit définir `overlay()` |
| JSON | lecture stricte : une clé inconnue est une erreur (`UnmappedMemberHandling.Disallow`), pas de BOM |
| Champs | `id` en minuscules, chiffres et tirets ; version `x.y.z` ; longueurs maximales ; pas de caractères de contrôle |
| Valeurs | `metric`, `scope`, permissions et couleurs prises dans des listes fixes |
| Cohérence | chaque fonction demande sa permission, et chaque permission doit servir |

Les mêmes règles sont appliquées par `addons-registry/scripts/Validate-Addon.ps1` (dépôt du catalogue), par
`TrackChest.exe --addon-check`, et par le site et sa fonction d'envoi (`addons-registry/site-src/addon-rules.js`,
fonction `validateManifest`). Si une règle change, modifie les quatre, ainsi que `addon.schema.json`.

### Permissions

| Permission | Effet dans le code |
|---|---|
| `stats.read` | autorise le calcul des compteurs à partir des parties |
| `overlay.rows` | `AddonRuntime.CounterRows` ajoute les lignes de l'add-on à l'overlay |
| `overlay.theme` | `AddonRuntime.Load` passe le thème à `NativeOverlay.SetTheme` |
| `overlay.script` | `AddonRuntime.ApplyScripts` passe la vue construite par `OverlayModel` à chaque script, dans l'ordre d'installation |

Au plus 10 lignes de compteurs d'add-ons sont ajoutées en tout. Un seul thème est actif : en activer un désactive les
autres. Les compteurs sont ajoutés avant les scripts, qui peuvent donc aussi les modifier.

## Scripts (`AddonScript.cs`)

Le script est exécuté par [Jint](https://github.com/sebastienros/jint) (licence MIT), un interpréteur JavaScript écrit
en .NET, référencé dans `TrackChest.csproj`.

**Ce qui le rend sûr**

- Aucun accès à .NET : l'interopérabilité CLR de Jint est désactivée (option par défaut). Le script n'a ni fichiers,
  ni réseau, ni registre, ni processus : `fetch`, `require`, `XMLHttpRequest` n'existent pas.
- Seul du JSON traverse la frontière : la vue (`view`) et les statistiques (`data`) entrent sous forme de texte JSON,
  le résultat ressort sous forme de texte JSON, puis `ViewFromJson` ne garde que les champs connus, coupe les textes
  trop longs, retire les caractères de contrôle, limite les lignes à 16 et refuse une couleur mal formée.
- Un nouveau moteur est créé à chaque exécution : rien ne persiste d'une mise à jour à l'autre.
- Limites : 1 seconde, 2 millions d'instructions, 64 Mo, récursion 64. À l'installation, le script est compilé et son
  code de premier niveau exécuté une fois sous les mêmes limites (`AddonScript.Check`).

**En cas d'échec** (exception, limite atteinte, résultat invalide), l'overlay garde la vue d'avant ce script. Après
trois échecs de suite, le script est ignoré jusqu'au prochain `AddonRuntime.Load` (démarrage ou changement
d'add-on). La dernière erreur est affichée dans la fenêtre Add-on (`AddonRuntime.ScriptError`).

**Ce que le bac à sable n'empêche pas** : un script peut afficher des chiffres faux ou un texte trompeur dans l'overlay.
C'est pourquoi un add-on à script doit être relu entièrement avant d'être marqué « Vérifié », et la fenêtre de
confirmation l'indique au joueur.

Les statistiques passées au script (`AddonScript.DataJson`) et le format de `view` sont décrits dans
[Créer un add-on → Scripts](creer-un-addon.md#6-scripts--tout-modifier). Si tu ajoutes un champ, mets aussi à jour cette
page.

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

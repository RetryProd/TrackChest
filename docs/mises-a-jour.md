# Mises à jour et installateur

TrackChest se met à jour depuis les **GitHub Releases** du dépôt [RetryProd/TrackChest](https://github.com/RetryProd/TrackChest/releases).
Chaque version publie quatre fichiers :

| Fichier | Rôle |
|---|---|
| `TrackChest-x.y.z.msi` | installateur (pour l'utilisateur courant, sans droits administrateur) |
| `TrackChest-x.y.z-portable.zip` | `TrackChest.exe`, les licences et `LISEZMOI.txt` (le fonctionnement) : à décompresser n'importe où |
| `update.json` | l'annonce : version, taille et SHA-256 du `.msi`, notes |
| `update.json.sig` | signature ECDSA P-256 de `update.json` |

## Côté joueur

1. Au démarrage (désactivable dans **Paramètres → Mises à jour**), `Updater.CheckAsync` télécharge `update.json` et sa
   signature depuis `releases/latest/download/`, en HTTPS uniquement.
2. La signature est vérifiée avec la clé publique intégrée à l'exe (`assets/addon-public.key`, la même que pour le
   catalogue d'add-ons). Une annonce non signée, ou signée par une autre clé, est ignorée.
3. Si la version est plus grande que celle de l'exe, TrackChest **demande** : rien n'est installé sans accord.
4. Le `.msi` est téléchargé à l'adresse construite par l'application à partir de la version signée (jamais lue dans un
   fichier), puis sa **taille** et son **SHA-256** sont comparés à l'annonce signée. En cas de différence, il est refusé.
5. `msiexec /i … /passive` remplace les fichiers, puis TrackChest se rouvre. Les données (`%APPDATA%\TrackChest`) ne sont
   pas touchées.

Le bouton **Rechercher** des Paramètres lance la même vérification à la demande et affiche aussi les erreurs. La version
portable (`.zip`) ne se met pas à jour toute seule en place : la mise à jour installe le `.msi`.

## Côté développeur : publier une version

Prérequis : le SDK .NET 10 (voir [compilation](compilation.md)), la clé privée `%USERPROFILE%\TrackChest-cles\addon-private.key`,
`gh` connecté, et WiX 5 (libre, licence MS-RL) :

```powershell
dotnet tool install wix --version 5.0.2 --tool-path "$env:LOCALAPPDATA\TrackChest\wix"
```

> N'installe pas WiX 7 : il demande d'accepter une licence de maintenance payante pour un usage commercial.

```powershell
.\Publier-Version.ps1 -Version 1.1.0 -Notes "Ce qui change" -Publier
```

Sans `-Publier`, les fichiers sont seulement fabriqués dans `dist\release\`. Le script :

1. écrit la version dans `TrackChest.csproj` (`<Version>`), la source de vérité du numéro ;
2. compile dans `dist\release\build` (pas dans `dist\`, qui peut contenir l'exe en cours d'utilisation) ;
3. fabrique le `.msi` avec `installer\TrackChest.wxs` et le `.zip` avec `installer\LISEZMOI.txt` ;
4. écrit `update.json`, le signe avec `TrackChest.exe --addon-sign`, vérifie la signature avec `assets\addon-public.key` ;
5. avec `-Publier`, crée la release `vx.y.z` avec les quatre fichiers.

Pense à commiter et pousser le changement de version avant de publier.

### Règles à respecter

- **`UpgradeCode`** dans `installer/TrackChest.wxs` ne change jamais : c'est lui qui permet à un `.msi` de remplacer le précédent.
- Les mises à jour sont signées avec la clé du catalogue : si elle est perdue ou change, les anciennes versions
  n'accepteront plus aucune annonce, et il faut distribuer à la main une version avec la nouvelle clé publique.
- Les notes sont limitées à 600 caractères (l'application les coupe au-delà).
- L'application refuse une version plus petite ou égale à la sienne : une mise à jour ne peut pas faire revenir en arrière.

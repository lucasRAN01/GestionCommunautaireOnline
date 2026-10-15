# Mise en ligne (Supabase + GitHub Pages)

Ce guide met l'application en ligne gratuitement, en une heure environ, sans
écrire de code et sans carte bancaire. Tout se fait depuis deux sites :
**Supabase** (comptes, données, fichiers) et **GitHub** (le site web,
la surveillance et les sauvegardes automatiques).

| Service | Rôle | Offre gratuite |
|---|---|---|
| Supabase | Comptes, base de données, photos et documents | 500 Mo de base, 1 Go de fichiers, 5 Go de transfert par mois |
| GitHub Pages | Héberge le site | 100 Go de transfert par mois, certificat HTTPS inclus (dépôt public) |
| GitHub Actions | Publie le site, surveille Supabase, sauvegarde | Gratuit (dépôt public) ou 2 000 min/mois (dépôt privé) |

---

## 1. Créer le projet Supabase

1. Créez un compte sur <https://supabase.com> puis **New project**.
   - Choisissez la région la plus proche des membres (par ex. *West EU (Paris)*
     ou *Central EU (Frankfurt)*).
   - Notez le **mot de passe de la base** : il servira pour les sauvegardes.
2. Ouvrez **SQL Editor > New query**, collez tout le contenu de
   [`supabase/migrations/0001_initialisation.sql`](../supabase/migrations/0001_initialisation.sql)
   puis cliquez **Run**. Le script peut être relancé sans danger (après une
   mise à jour de l'application par exemple).
3. **Désactivez la confirmation par e-mail** (la validation est faite par
   l'administrateur, et l'offre gratuite n'envoie que quelques e-mails par
   heure) : **Authentication > Sign In / Providers > Email**, décochez
   **Confirm email**, puis **Save**.
4. Récupérez les informations de connexion :
   - **Project Settings > API** (ou *API Keys*) :
     - l'**URL du projet** (`https://xxxx.supabase.co`) ;
     - la clé **anon** (publique, elle ira dans le site) ;
     - la clé **service_role** (secrète, elle ne sert qu'à GitHub).

     Si votre projet propose les nouvelles clés, utilisez l'onglet
     *Legacy API keys* pour obtenir `anon` et `service_role`.
   - **Connect** (en haut de la page) **> Connection string > Session pooler** :
     copiez l'adresse `postgresql://postgres.xxxx:[YOUR-PASSWORD]@…pooler.supabase.com:5432/postgres`
     et remplacez `[YOUR-PASSWORD]` par le mot de passe noté à l'étape 1.
     Prenez bien le *Session pooler* : la connexion directe ne fonctionne pas
     depuis GitHub.

> La clé **service_role** et l'adresse de la base donnent un accès complet :
> ne les mettez jamais dans le code ni dans un message, uniquement dans les
> secrets GitHub (étape 3).

## 2. Activer GitHub Pages

1. Dans le dépôt GitHub : **Settings > Pages**.
2. Sous **Build and deployment**, choisissez **Source : GitHub Actions**.
   Il n'y a rien d'autre à régler.
3. L'adresse du site sera
   `https://lucasran01.github.io/GestionCommunautaireOnline/` (affichée en
   haut de cette même page après la première publication). Un nom de domaine
   personnel peut être ajouté plus tard dans **Custom domain**, gratuitement.

GitHub Pages est gratuit pour un dépôt public et ne demande pas de carte
bancaire. Le code du site est donc visible de tous, ce qui ne pose pas de
problème : les données des membres restent dans Supabase, protégées par les
comptes et les droits.

## 3. Renseigner GitHub

Dans le dépôt GitHub : **Settings > Secrets and variables > Actions**.

Onglet **Variables** :

| Nom | Valeur |
|---|---|
| `SUPABASE_URL` | URL du projet Supabase |
| `SUPABASE_ANON_KEY` | clé `anon` |

Onglet **Secrets** :

| Nom | Valeur |
|---|---|
| `SUPABASE_SERVICE_ROLE_KEY` | clé `service_role` |
| `SUPABASE_DB_URL` | adresse *Session pooler* avec le mot de passe |
| `SAUVEGARDE_MOT_DE_PASSE` | un mot de passe de votre choix (12 caractères minimum) pour chiffrer les sauvegardes |

Le dépôt est public : sans `SAUVEGARDE_MOT_DE_PASSE`, la tâche **Sauvegarde**
refuse de s'exécuter plutôt que de publier des données lisibles par tous.
**Notez ce mot de passe en lieu sûr** : sans lui, les sauvegardes ne peuvent
pas être ouvertes, et personne (pas même GitHub) ne peut le retrouver.

Tant que `SUPABASE_URL` n'est pas renseignée, les tâches automatiques
attendent sans erreur.

## 4. Publier

- Le site est publié automatiquement à chaque modification de la branche
  `main` (tâche **Déploiement du site**). Pour le publier tout de suite :
  **Actions > Déploiement du site > Run workflow**.
- Lancez aussi une fois à la main **Actions > Veille Supabase** et
  **Actions > Sauvegarde** pour vérifier que les secrets sont bons.

## 5. Premier administrateur

1. Ouvrez le site et choisissez **Créer un compte**.
2. **Le tout premier compte devient automatiquement Administrateur** et
   actif. Créez-le vous-même, immédiatement après la mise en ligne.
3. Les inscriptions suivantes restent **en attente** : elles apparaissent dans
   **Comptes en ligne** (avec un bandeau et un badge). L'administrateur choisit
   le rôle puis **Valider** ou **Refuser**.
4. Ensuite, le rôle d'un membre ou la désactivation de son compte se gèrent
   depuis sa fiche dans **Utilisateurs**, et les droits dans
   **Rôles et permissions** : ces droits sont aussi appliqués par le serveur.
5. Mot de passe oublié : il n'y a pas d'envoi d'e-mail. Un administrateur
   ouvre **Comptes en ligne > Nouveau mot de passe**, définit un mot de passe
   provisoire et le transmet ; le membre le change ensuite depuis son menu
   (**Changer mon mot de passe**).

---

## Alerte de saturation Supabase

L'offre gratuite est limitée (500 Mo de base, 1 Go de fichiers). L'application
surveille cet espace et prévient les administrateurs bien avant la limite.

- **Mesure quotidienne** par la tâche GitHub *Veille Supabase* (6 h 17 UTC),
  plus une mesure à chaque ouverture de la page **Capacité du serveur**.
- **Deux seuils réglables** : *attention* (70 % par défaut) et *critique*
  (90 %), modifiables dans la page Capacité.
- **Où l'administrateur est prévenu :**
  - un bandeau en haut de chaque page, orange (attention) ou rouge
    (critique), avec un bouton vers la page Capacité ;
  - un badge sur **Capacité du serveur** dans le menu ;
  - un ticket GitHub « Alerte capacité Supabase » (GitHub envoie un e-mail
    aux personnes qui suivent le dépôt), complété à chaque nouvelle mesure
    tant qu'il reste ouvert.
- Une alerte n'est créée qu'une fois par niveau : tant qu'elle n'est pas
  marquée **Traitée**, aucune alerte en double n'est ajoutée. Un passage de
  *attention* à *critique* crée une nouvelle alerte.
- **Page Capacité du serveur** (administrateurs) :
  - jauges base, fichiers et comptes, avec les seuils ;
  - estimation du nombre de jours avant la limite (d'après les 30 derniers
    jours) ;
  - historique sur 90 jours ;
  - détail de la place prise par chaque partie de l'application ;
  - **Libérer de l'espace** : recherche et suppression des fichiers qui ne
    sont plus utilisés par aucune fiche ;
  - lien vers le tableau de bord Supabase pour le transfert mensuel, que
    l'application ne peut pas mesurer elle-même.

### Que faire en cas d'alerte

1. Ouvrir **Capacité du serveur** et regarder ce qui grossit.
2. **Libérer de l'espace** : supprimer les fichiers inutilisés.
3. Archiver ou supprimer les anciennes données (anciennes activités,
   messages, documents périmés).
4. Une fois l'espace libéré, cliquer **Traitée** sur l'alerte et fermer le
   ticket GitHub.
5. Si la communauté a durablement besoin de plus, l'offre payante Supabase
   (25 $/mois) multiplie les limites ; rien n'est à changer dans le code.

## Les autres parades de l'offre gratuite

| Risque | Parade mise en place |
|---|---|
| Projet Supabase mis en pause après 7 jours sans activité | La veille quotidienne GitHub interroge Supabase chaque jour. |
| Aucune sauvegarde automatique dans l'offre gratuite | Tâche **Sauvegarde** chaque dimanche : copie complète de la base (comptes compris, conservée 90 jours) et des fichiers (conservés 14 jours), chiffrées avec votre mot de passe et téléchargeables dans **Actions > Sauvegarde**. |
| Tâches programmées suspendues par GitHub après 60 jours sans activité du dépôt | La veille modifie elle-même un petit fichier (`.github/derniere-activite.txt`) si rien n'a changé depuis 50 jours. |
| Photos trop lourdes | Compression dans le navigateur avant l'envoi (1600 px maximum, JPEG). |
| Base gonflée par les fichiers | Photos et documents rangés dans le stockage de fichiers, pas dans la base ; un même fichier n'est stocké qu'une fois. |
| Transfert mensuel (5 Go) | Cache dans le navigateur : seules les parties modifiées sont retéléchargées, et un fichier n'est téléchargé qu'une fois. |
| Fichiers orphelins | Recherche et suppression depuis la page Capacité. |
| Une donnée trop grosse | Le serveur refuse tout enregistrement de plus de 5 Mo, avec un message clair. |

> **Si GitHub suspend quand même les tâches** (il prévient par e-mail) :
> ouvrez **Actions**, choisissez *Veille Supabase* puis **Enable workflow**,
> et faites de même pour *Sauvegarde*. Sans la veille, Supabase mettra le
> projet en pause au bout de 7 jours ; il se relance alors depuis le tableau
> de bord Supabase (**Restore project**), sans perte de données.

### Restaurer une sauvegarde

1. Téléchargez l'archive `sauvegarde-base` dans **Actions > Sauvegarde**
   (choisir une exécution, rubrique *Artifacts*). Elle contient un fichier
   `.7z` : ouvrez-le avec [7-Zip](https://www.7-zip.org) (gratuit) et
   tapez le mot de passe `SAUVEGARDE_MOT_DE_PASSE`.
2. Dans un nouveau projet Supabase, restaurez-la avec
   `psql "<adresse Session pooler>" -f base-AAAA-MM-JJ.sql`.
3. Les fichiers de `sauvegarde-fichiers` (même mot de passe) se renvoient dans le compartiment
   `fichiers`, dossier `f/`, depuis **Storage** dans Supabase.

En cas de doute, faites la première restauration avec de l'aide : c'est
l'opération la plus délicate.

---

## Limites connues de cette première version

- **Données partagées par blocs.** Chaque partie de l'application (membres,
  calendrier, finances…) est enregistrée d'un seul bloc. Si deux personnes
  modifient la même partie au même moment, la seconde est prévenue que les
  données viennent de changer : elles sont rechargées et elle refait sa
  dernière modification. Rien n'est écrasé sans avertissement.
- **Parties collaboratives.** Le journal d'activité, la messagerie et les
  votes peuvent être modifiés par tout membre qui y a accès. Le serveur
  contrôle le droit d'écrire dans une partie, pas chaque ligne à l'intérieur.
- **Paramètres communs.** Les réglages (nom de l'application, thème, couleur, logo…) sont ceux de la
  communauté, pas de chaque membre.
- **Mise à jour toutes les 60 secondes.** Les modifications des autres
  membres apparaissent au plus tard une minute après.
- **Export automatique mensuel désactivé** dans la version en ligne : il est
  remplacé par les sauvegardes du serveur. L'export manuel reste disponible.
- **Transfert mensuel non mesuré** par l'application : il se consulte dans
  le tableau de bord Supabase (lien sur la page Capacité).
- **Pas d'e-mail** : ni confirmation d'inscription, ni réinitialisation du mot
  de passe par lien (voir *Premier administrateur*, point 5).
- L'application mobile hors ligne (dépôt *GestionUtilisateurs2*) reste
  indépendante : ses données ne sont pas synchronisées avec la version en
  ligne.

## Lancer le site sur un ordinateur (développement)

1. Installez le SDK .NET 9.
2. Renseignez `Url` et `AnonKey` dans
   `src/GestionUtilisateurs.Online.Web/wwwroot/appsettings.json` (ne publiez
   pas ce changement : le déploiement remplit ce fichier lui-même).
3. Lancez `dotnet run --project src/GestionUtilisateurs.Online.Web` et ouvrez
   l'adresse affichée.

Sans configuration, le site affiche un écran expliquant que Supabase n'est pas
encore relié.

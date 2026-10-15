# Gestion communautaire — version en ligne

Version web de l'application *Gestion des utilisateurs*, avec un compte
personnel pour chaque membre. L'application mobile hors ligne reste dans le
dépôt [GestionUtilisateurs2](https://github.com/lucasRAN01/GestionUtilisateurs2)
et n'est pas modifiée.

- **Sur ordinateur** : nouvelle mise en page web (menu latéral, contenu élargi).
- **Sur téléphone** : exactement le même design que l'application mobile.
- **Comptes** : inscription, connexion, validation manuelle par un
  administrateur, rôles et permissions appliqués aussi par le serveur.
- **Hébergement gratuit** : Supabase (comptes, données, fichiers) et Azure
  Static Web Apps (site), avec alerte de saturation pour les administrateurs,
  veille quotidienne et sauvegardes hebdomadaires.

👉 **Pour mettre en ligne : [docs/MISE-EN-LIGNE.md](docs/MISE-EN-LIGNE.md).**

## Contenu du dépôt

| Dossier | Contenu |
|---|---|
| `src/GestionUtilisateurs.Online.Web` | Le site (Blazor WebAssembly, .NET 9) |
| `src/GestionUtilisateurs.Online.Shared` | Logique métier, reprise de l'application hors ligne |
| `tests/GestionUtilisateurs.Online.Tests` | Tests automatiques |
| `supabase/migrations` | Script SQL à exécuter une fois dans Supabase |
| `.github/workflows` | Vérification, déploiement Azure, veille Supabase, sauvegarde |

## Développement

```bash
dotnet build GestionCommunautaireOnline.sln
dotnet test tests/GestionUtilisateurs.Online.Tests
dotnet run --project src/GestionUtilisateurs.Online.Web
```

Le site a besoin de l'adresse et de la clé publique Supabase dans
`src/GestionUtilisateurs.Online.Web/wwwroot/appsettings.json` (voir le guide).

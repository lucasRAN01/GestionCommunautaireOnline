using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreSyncPermissionsTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static void LoginAdmin(DataStore store)
    {
        var login = store.Login("admin", "admin123");
        Assert.True(login.Success);
    }

    private static SyncPackage ExportImport(DataStore from, DataStore to)
    {
        var json = from.BuildSyncPackageJson(new SyncOptions(), "Appareil-A");
        var (package, error) = to.ParseSyncPackage(json);
        Assert.Null(error);
        Assert.NotNull(package);
        return package!;
    }

    private static void ApplyAllIncoming(DataStore to, SyncPackage package)
    {
        var preview = to.PreviewSyncImport(package);
        var resolutions = preview.Conflicts
            .Select(c => new SyncResolution { Collection = c.Collection, Key = c.Key, UseIncoming = true })
            .ToList();
        var result = to.ApplySyncImport(package, resolutions);
        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Permissions_Defauts_PreserventComportementActuel()
    {
        var store = NewStore();

        Assert.True(store.HasPermission(Roles.Administrateur, Permissions.UsersManage));
        Assert.True(store.HasPermission(Roles.Gestionnaire, Permissions.UsersManage));
        Assert.False(store.HasPermission(Roles.Utilisateur, Permissions.UsersManage));
        Assert.True(store.HasPermission(Roles.Utilisateur, Permissions.CalendarUse));
        Assert.True(store.HasPermission(Roles.Utilisateur, Permissions.DocumentsUse));
        Assert.True(store.HasPermission(Roles.Gestionnaire, Permissions.SyncUse));
        Assert.False(store.HasPermission(Roles.Utilisateur, Permissions.SyncUse));
        Assert.True(store.HasPermission(Roles.Administrateur, Permissions.RolesManage));
        Assert.False(store.HasPermission(Roles.Gestionnaire, Permissions.RolesManage));
        Assert.True(store.HasPermission(Roles.BaseAdmin, "nimporte.quoi"));
        Assert.False(store.HasPermission(null, Permissions.CalendarUse));
    }

    [Fact]
    public void Permissions_Save_GardeAdminEtAntiBlocage()
    {
        var store = NewStore();

        // Non connecté : refusé.
        var refused = store.SaveRolePermissions(DataStore.DefaultPermissions());
        Assert.False(refused.Success);

        LoginAdmin(store);
        var sets = store.RolePermissions.ToList();
        var gestionnaire = sets.First(s => s.Role == Roles.Gestionnaire);
        gestionnaire.Allowed[Permissions.SyncUse] = false;
        Assert.True(store.SaveRolePermissions(sets).Success);
        Assert.False(store.HasPermission(Roles.Gestionnaire, Permissions.SyncUse));

        // Anti-blocage : impossible de retirer la gestion des rôles aux administrateurs.
        sets = store.RolePermissions.ToList();
        sets.First(s => s.Role == Roles.Administrateur).Allowed[Permissions.RolesManage] = false;
        var blocked = store.SaveRolePermissions(sets);
        Assert.False(blocked.Success);
        Assert.True(store.HasPermission(Roles.Administrateur, Permissions.RolesManage));
    }

    [Fact]
    public void Sync_PaquetMemeAppareil_Refuse()
    {
        var store = NewStore();
        var json = store.BuildSyncPackageJson(new SyncOptions(), "Moi");
        var (_, error) = store.ParseSyncPackage(json);
        Assert.NotNull(error);
        Assert.Contains("cet appareil", error);
    }

    [Fact]
    public void Sync_PremierEchange_FusionneTout()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        a.AddUser(new UserAccount
        {
            Prenom = "Marie", Nom = "DUPONT", Identifiant = "mdupont",
            Role = Roles.Utilisateur, Actif = true, MotDePasse = "secret123"
        });

        var package = ExportImport(a, b);
        var preview = b.PreviewSyncImport(package);
        Assert.DoesNotContain(preview.Conflicts, c => c.Key != "admin" && c.Key != "dba");
        Assert.True(preview.TotalChanges > 0);

        ApplyAllIncoming(b, package);
        Assert.NotNull(b.FindUserByIdentifier("mdupont"));
        Assert.Equal(a.FindUserByIdentifier("admin")!.Id, b.FindUserByIdentifier("admin")!.Id);

        // Réimport du même paquet : stable, aucun changement.
        var again = b.PreviewSyncImport(package);
        Assert.Equal(0, again.TotalChanges);
    }

    [Fact]
    public void Sync_ModificationUnilaterale_SansConflit()
    {
        var a = NewStore();
        var b = NewStore();
        ApplyAllIncoming(b, ExportImport(a, b));

        LoginAdmin(a);
        var admin = a.FindUserByIdentifier("admin")!;
        var draft = new UserAccount
        {
            Id = admin.Id, Prenom = admin.Prenom, Nom = admin.Nom, Identifiant = admin.Identifiant,
            Email = "admin@exemple.fr", Telephone = admin.Telephone, Role = admin.Role,
            Actif = admin.Actif, Notes = admin.Notes, Champs = admin.Champs, Photo = admin.Photo
        };
        Assert.True(a.UpdateUser(admin.Id, draft, false).Success);

        var preview = b.PreviewSyncImport(ExportImport(a, b));
        Assert.Empty(preview.Conflicts);
        Assert.True(preview.UpdatedFromPeer.Values.Sum() >= 1);
        Assert.True(b.ApplySyncImport(ExportImport(a, b), new List<SyncResolution>()).Success);
        Assert.Equal("admin@exemple.fr", b.FindUserByIdentifier("admin")!.Email);
    }

    [Fact]
    public void Sync_Conflit_ResolutionLocalePuisRejetMemorise()
    {
        var a = NewStore();
        var b = NewStore();
        ApplyAllIncoming(b, ExportImport(a, b));

        LoginAdmin(a);
        LoginAdmin(b);
        SetEmail(a, "a@exemple.fr");
        SetEmail(b, "b@exemple.fr");

        var package = ExportImport(a, b);
        var preview = b.PreviewSyncImport(package);
        var conflict = Assert.Single(preview.Conflicts, c => c.Collection == "users");
        Assert.Equal("admin", conflict.Key);

        // Conserver le local.
        var kept = b.ApplySyncImport(package, new List<SyncResolution>());
        Assert.True(kept.Success, kept.Error);
        Assert.Equal("b@exemple.fr", b.FindUserByIdentifier("admin")!.Email);

        // Le même paquet ne redemande plus rien (rejet mémorisé).
        var again = b.PreviewSyncImport(package);
        Assert.Equal(0, again.TotalChanges);

        // …une nouvelle modification distante devient une simple mise à jour (base = version locale conservée).
        SetEmail(a, "a2@exemple.fr");
        var newer = b.PreviewSyncImport(ExportImport(a, b));
        Assert.Empty(newer.Conflicts);
        Assert.True(newer.UpdatedFromPeer.Values.Sum() >= 1);
        Assert.True(b.ApplySyncImport(ExportImport(a, b), new List<SyncResolution>()).Success);
        Assert.Equal("a2@exemple.fr", b.FindUserByIdentifier("admin")!.Email);
    }

    [Fact]
    public void Sync_SuppressionDistante_Propagee()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        a.AddEvent(new CalendarEvent
        {
            Type = CalendarEventTypes.Reunion, Titre = "Point hebdo",
            Date = "2026-09-20", HeureDebut = "09:00"
        });
        ApplyAllIncoming(b, ExportImport(a, b));
        Assert.Single(b.Events);

        Assert.True(a.DeleteEvent(a.Events[0].Id).Success);
        var preview = b.PreviewSyncImport(ExportImport(a, b));
        Assert.Empty(preview.Conflicts);
        Assert.True(b.ApplySyncImport(ExportImport(a, b), new List<SyncResolution>()).Success);
        Assert.Empty(b.Events);
    }

    [Fact]
    public void Sync_Permissions_Synchronisees()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        var sets = a.RolePermissions.ToList();
        sets.First(s => s.Role == Roles.Gestionnaire).Allowed[Permissions.SyncUse] = false;
        Assert.True(a.SaveRolePermissions(sets).Success);

        ApplyAllIncoming(b, ExportImport(a, b));
        Assert.False(b.HasPermission(Roles.Gestionnaire, Permissions.SyncUse));
    }

    private static void SetEmail(DataStore store, string email)
    {
        var admin = store.FindUserByIdentifier("admin")!;
        var draft = new UserAccount
        {
            Id = admin.Id, Prenom = admin.Prenom, Nom = admin.Nom, Identifiant = admin.Identifiant,
            Email = email, Telephone = admin.Telephone, Role = admin.Role,
            Actif = admin.Actif, Notes = admin.Notes, Champs = admin.Champs, Photo = admin.Photo
        };
        Assert.True(store.UpdateUser(admin.Id, draft, false).Success);
    }
}

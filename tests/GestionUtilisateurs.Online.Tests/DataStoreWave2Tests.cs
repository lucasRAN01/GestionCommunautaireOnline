using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreWave2Tests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static void LoginAdmin(DataStore store)
    {
        Assert.True(store.Login("admin", "admin123").Success);
    }

    [Fact]
    public void Messagerie_Annonce_VisibleParTous()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.SendMessage(new List<string>(), "Assemblée samedi !").Success);

        var convs = store.GetConversations("dba");
        Assert.Contains(convs, c => c.ConversationId == DataStore.ConversationAnnonces);
        Assert.Equal(1, store.UnreadCount("dba"));

        store.MarkConversationRead(DataStore.ConversationAnnonces, "dba");
        Assert.Equal(0, store.UnreadCount("dba"));
    }

    [Fact]
    public void Messagerie_DestinataireInconnu_Refuse()
    {
        var store = NewStore();
        LoginAdmin(store);
        var res = store.SendMessage(new List<string> { "fantome" }, "Bonjour ?");
        Assert.False(res.Success);
        Assert.Empty(store.Messages);
    }

    [Fact]
    public void Messagerie_Suppression_ExpediteurOuAdmin()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.SendMessage(new List<string>(), "À effacer").Success);
        var id = store.Messages[0].Id;

        // Sans session : refusé (ni expéditeur ni admin).
        var loggedOut = NewStore();
        Assert.False(loggedOut.DeleteMessage(id).Success);

        Assert.True(store.DeleteMessage(id).Success);
        // Suppression douce : le journal reste visible dans la conversation.
        var deleted = Assert.Single(store.Messages);
        Assert.True(deleted.Supprime);
        Assert.Equal("admin", deleted.SupprimePar);
        Assert.Equal(0, store.UnreadCount("dba"));
    }

    [Fact]
    public void Messagerie_Modification_LimiteeA5Min()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.SendMessage(new List<string>(), "Bonjour").Success);
        var id = store.Messages[0].Id;

        Assert.True(store.CanEditMessage(id));
        Assert.True(store.UpdateMessage(id, "Bonjour !").Success);
        Assert.Equal("Bonjour !", store.Messages[0].Texte);
        Assert.False(string.IsNullOrWhiteSpace(store.Messages[0].DateModification));

        // Après 6 minutes : refusé.
        store.Messages[0].Date = DateTimeOffset.Now.AddMinutes(-6).ToString("O");
        Assert.False(store.CanEditMessage(id));
        Assert.False(store.UpdateMessage(id, "Trop tard").Success);

        // Message supprimé : ni modification ni re-suppression.
        store.Messages[0].Date = DateTimeOffset.Now.ToString("O");
        Assert.True(store.DeleteMessage(id).Success);
        Assert.False(store.UpdateMessage(id, "Encore").Success);
        Assert.False(store.DeleteMessage(id).Success);
    }

    [Fact]
    public void Taches_CycleDeSuivi()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddTache(new Tache
        {
            Titre = "Préparer la salle",
            Assignes = new List<string> { "admin" },
            Priorite = CalendarEventTypes.PrioriteImportante,
            Echeance = "2026-12-31"
        }).Success);

        var tache = Assert.Single(store.Taches);
        Assert.Equal(TacheStatuts.AFaire, tache.Statut);
        Assert.Single(store.GetTachesForUser("admin", false));

        Assert.True(store.SetTacheStatut(tache.Id, TacheStatuts.EnCours).Success);
        Assert.True(store.SetTacheStatut(tache.Id, TacheStatuts.Terminee).Success);
        Assert.Equal(TacheStatuts.Terminee, store.FindTache(tache.Id)!.Statut);
        Assert.False(string.IsNullOrWhiteSpace(store.FindTache(tache.Id)!.TermineLe));

        Assert.False(store.SetTacheStatut(tache.Id, "inconnu").Success);
        Assert.True(store.DeleteTache(tache.Id).Success);
        Assert.Empty(store.Taches);
    }

    [Fact]
    public void Taches_EcheanceInvalide_Refusee()
    {
        var store = NewStore();
        var res = store.AddTache(new Tache { Titre = "X", Echeance = "31/13/2026" });
        Assert.False(res.Success);
    }

    [Fact]
    public void Anniversaire_LienFicheCalendrier()
    {
        var store = NewStore();
        LoginAdmin(store);
        var draft = new UserAccount
        {
            Prenom = "Jean", Nom = "PAUL", Identifiant = "jpaul",
            Role = Roles.Utilisateur, Actif = true, MotDePasse = "secret123",
            DateNaissance = "1990-05-17"
        };
        Assert.True(store.AddUser(draft).Success);
        var user = store.FindUserByIdentifier("jpaul")!;
        Assert.Equal("1990-05-17", user.DateNaissance);

        // Création automatique à l'ajout du membre (anniversaire -> calendrier).
        var auto = store.GetBirthdayEventsForUser(user.Identifiant, user.NomComplet);
        var evt = Assert.Single(auto);
        Assert.Equal(CalendarEventTypes.Anniversaire, evt.Type);
        Assert.Contains(user.Identifiant, evt.PersonnesConcernees);

        // Une seule fois : doublon refusé (manuel ou auto).
        Assert.False(store.EnsureBirthdayEvent(user.Id).Success);
        var linked = store.GetBirthdayEventsForUser(user.Identifiant, user.NomComplet);
        Assert.Single(linked);
    }

    [Fact]
    public void Anniversaire_Excel_ColonneNaissance()
    {
        var store = NewStore();
        LoginAdmin(store);
        var draft = new UserAccount
        {
            Prenom = "A", Nom = "B", Identifiant = "ab",
            Role = Roles.Utilisateur, Actif = true, MotDePasse = "secret123",
            DateNaissance = "2000-01-02"
        };
        Assert.True(store.AddUser(draft).Success);

        var bytes = store.ExportUsersExcel();
        var fresh = NewStore();
        var parsed = fresh.ParseExcelImport(bytes);
        Assert.True(parsed.Success, parsed.Error);
        var imported = Assert.Single(parsed.Users!, u => u.Identifiant == "ab");
        Assert.Equal("2000-01-02", imported.DateNaissance);
    }

    [Fact]
    public void Sync_MessagesEtTaches_Fusionnes()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        Assert.True(a.SendMessage(new List<string>(), "Bonjour à tous").Success);
        Assert.True(a.AddTache(new Tache { Titre = "Task A" }).Success);

        var json = a.BuildSyncPackageJson(new SyncOptions(), "A");
        var (package, error) = b.ParseSyncPackage(json);
        Assert.Null(error);
        var preview = b.PreviewSyncImport(package!);
        var resolutions = preview.Conflicts
            .Select(c => new SyncResolution { Collection = c.Collection, Key = c.Key, UseIncoming = true })
            .ToList();
        Assert.True(b.ApplySyncImport(package!, resolutions).Success);
        Assert.Single(b.Messages);
        Assert.Single(b.Taches);

        // Réimport stable.
        Assert.Equal(0, b.PreviewSyncImport(package!).TotalChanges);
    }
}

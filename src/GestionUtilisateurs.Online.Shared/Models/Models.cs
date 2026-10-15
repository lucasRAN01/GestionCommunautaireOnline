namespace GestionUtilisateurs.Shared.Models;

public enum FieldType
{
    Texte,
    TexteMultiligne,
    Nombre,
    Liste,
    Date,
    Email,
    Booleen,
    PieceJointe,
    Image,
    ChoixMultiple
}

public static class FieldTypeInfo
{
    public static string Label(FieldType t) => t switch
    {
        FieldType.Texte => "Texte libre",
        FieldType.TexteMultiligne => "Zone de texte",
        FieldType.Nombre => "Nombre",
        FieldType.Liste => "Liste",
        FieldType.Date => "Date",
        FieldType.Email => "E-mail",
        FieldType.Booleen => "Oui / Non",
        FieldType.PieceJointe => "Pièce jointe",
        FieldType.Image => "Image",
        FieldType.ChoixMultiple => "Choix par cochage",
        _ => t.ToString()
    };

    public static string Icon(FieldType t) => t switch
    {
        FieldType.Texte => "Aa",
        FieldType.TexteMultiligne => "¶",
        FieldType.Nombre => "123",
        FieldType.Liste => "☰",
        FieldType.Date => "◷",
        FieldType.Email => "@",
        FieldType.Booleen => "◉",
        FieldType.PieceJointe => "📎",
        FieldType.Image => "🖼",
        FieldType.ChoixMultiple => "☑",
        _ => "?"
    };

    public static bool SupportsOptions(FieldType t) => t == FieldType.Liste || t == FieldType.ChoixMultiple;
}

public class FieldDefinition
{
    /// <summary>Identifiant du champ système lié au login du compte.</summary>
    public const string FIELD_IDENTIFIANT = "__identifiant";

    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public FieldType Type { get; set; } = FieldType.Texte;
    public bool Requis { get; set; }
    public List<string> Options { get; set; } = new();
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;
    /// <summary>Champ lié au compte (identifiant) : ni supprimable, ni de type modifiable.</summary>
    public bool SystemField { get; set; }
}

public class FileRef
{
    public string Key { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Taille { get; set; }
    /// <summary>Contenu du fichier en data URL, stocké en local (photo/pièce jointe choisie dans le formulaire).</summary>
    public string? Data { get; set; }
}

public class FieldValue
{
    public string Texte { get; set; } = "";
    public double? Nombre { get; set; }
    public bool Booleen { get; set; }
    public FileRef? Fichier { get; set; }
}

public class UserAccount
{
    public string Id { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Identifiant { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telephone { get; set; } = "";
    public string Role { get; set; } = "Utilisateur";
    public bool Actif { get; set; } = true;
    public string MotDePasse { get; set; } = "";
    public string Notes { get; set; } = "";
    public string DateCreation { get; set; } = "";
    public string? DerniereConnexion { get; set; }
    /// <summary>Date de naissance au format yyyy-MM-dd (vide si inconnue), affichée JJ/MM/AAAA.</summary>
    public string DateNaissance { get; set; } = "";
    /// <summary>Matricule unique du membre (vide si non attribué), affiché sur la carte de membre.</summary>
    public string Matricule { get; set; } = "";
    public string? Photo { get; set; }
    public Dictionary<string, FieldValue> Champs { get; set; } = new();

    public string NomComplet => Nom + " " + Prenom;
    public string Statut => Actif ? "Actif" : "Inactif";

    public string? GetChampTexte(string fieldId)
        => Champs.TryGetValue(fieldId, out var v) ? v.Texte : null;

    public FieldValue? GetChamp(string fieldId)
        => Champs.TryGetValue(fieldId, out var v) ? v : null;
}

public class ActivityEntry
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public string User { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class AppSettings
{
    public string AppName { get; set; } = "Gestion des utilisateurs";
    public string Theme { get; set; } = "light";
    public string Color { get; set; } = "#4f46e5";
    public int PageSize { get; set; } = 10;
    /// <summary>Logo personnalisé (data URL) affiché sur l'écran de connexion.</summary>
    public string Logo { get; set; } = "";
    /// <summary>Durée de session avant déconnexion automatique (minutes).</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;
    /// <summary>Export automatique mensuel de toutes les données (activé par défaut).</summary>
    public bool ExportAutoMensuel { get; set; } = true;
    /// <summary>Dernier mois exporté automatiquement (format yyyy-MM).</summary>
    public string DernierExportAuto { get; set; } = "";
}

/// <summary>Histoire interactive de la communauté (chapitres modifiables).</summary>
public class CommunityChapter
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Texte { get; set; } = "";
    public string Date { get; set; } = "";
    public string? Image { get; set; }
}

public class CommunityStory
{
    public string Titre { get; set; } = "Histoire de notre communauté";
    public string SousTitre { get; set; } = "";
    public List<CommunityChapter> Chapitres { get; set; } = new();
}

/// <summary>Message interne : boîte locale partagée, synchronisée comme le reste.</summary>
public class Message
{
    public string Id { get; set; } = "";
    /// <summary>Identifiant de conversation : paire triée pour un duo, liste triée pour un groupe, « annonces » pour le canal général.</summary>
    public string ConversationId { get; set; } = "";
    public string Expediteur { get; set; } = "";
    /// <summary>Destinataires (identifiants). Vide = canal « Annonces » (tout le monde).</summary>
    public List<string> Destinataires { get; set; } = new();
    public string Texte { get; set; } = "";
    public string Date { get; set; } = "";
    /// <summary>Identifiants ayant lu le message.</summary>
    public List<string> LuPar { get; set; } = new();
}

/// <summary>Tâche avec suivi (statut, priorité, échéance, assignés).</summary>
public class Tache
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Assignés (identifiants de connexion).</summary>
    public List<string> Assignes { get; set; } = new();
    public string Statut { get; set; } = TacheStatuts.AFaire;
    public string Priorite { get; set; } = CalendarEventTypes.PrioriteNormale;
    /// <summary>Échéance au format yyyy-MM-dd (vide si aucune).</summary>
    public string Echeance { get; set; } = "";
    public string DateCreation { get; set; } = "";
    public string? TermineLe { get; set; }
    public string CreatedBy { get; set; } = "";
}

public static class TacheStatuts
{
    public const string AFaire = "a_faire";
    public const string EnCours = "en_cours";
    public const string Terminee = "terminee";

    public static string Label(string statut) => statut switch
    {
        EnCours => "En cours",
        Terminee => "Terminée",
        _ => "À faire"
    };

    public static string Icon(string statut) => statut switch
    {
        EnCours => "🔄",
        Terminee => "✅",
        _ => "☑"
    };
}

/// <summary>Cotisation d'un membre (montant en Ariary).</summary>
public class Cotisation
{
    public string Id { get; set; } = "";
    public string MembreIdentifiant { get; set; } = "";
    public double Montant { get; set; }
    public string Motif { get; set; } = "";
    /// <summary>Période (ex. « 2026 »).</summary>
    public string Periode { get; set; } = "";
    /// <summary>Date de paiement au format yyyy-MM-dd.</summary>
    public string Date { get; set; } = "";
    public string Statut { get; set; } = CotisationStatuts.Payee;
    public string CreatedBy { get; set; } = "";
}

public static class CotisationStatuts
{
    public const string Payee = "payee";
    public const string EnAttente = "en_attente";

    public static string Label(string statut) => statut switch
    {
        EnAttente => "En attente",
        _ => "Payée"
    };
}

/// <summary>Mouvement de trésorerie (entrée ou sortie, montants en Ariary).</summary>
public class TransactionTresorerie
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = TresorerieTypes.Entree;
    public double Montant { get; set; }
    public string Motif { get; set; } = "";
    /// <summary>Date au format yyyy-MM-dd.</summary>
    public string Date { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public static class TresorerieTypes
{
    public const string Entree = "entree";
    public const string Sortie = "sortie";

    public static string Label(string type) => type switch
    {
        Sortie => "Sortie",
        _ => "Entrée"
    };

    public static string Icon(string type) => type switch
    {
        Sortie => "↗",
        _ => "↙"
    };
}

/// <summary>Matériel inventorié (quantité totale, disponibilité calculée des prêts actifs).</summary>
public class Materiel
{
    public string Id { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Description { get; set; } = "";
    public string Categorie { get; set; } = "";
    public int QuantiteTotale { get; set; } = 1;
    public string Etat { get; set; } = MaterielEtats.Bon;
    public string Responsable { get; set; } = "";
}

public static class MaterielEtats
{
    public const string Bon = "bon";
    public const string Moyen = "moyen";
    public const string HorsService = "hors_service";

    public static string Label(string etat) => etat switch
    {
        Moyen => "Moyen",
        HorsService => "Hors service",
        _ => "Bon"
    };
}

/// <summary>Prêt de matériel (actif tant que DateRetour est vide).</summary>
public class PretMateriel
{
    public string Id { get; set; } = "";
    public string MaterielId { get; set; } = "";
    public string MembreIdentifiant { get; set; } = "";
    public int Quantite { get; set; } = 1;
    /// <summary>Date du prêt au format yyyy-MM-dd.</summary>
    public string DatePret { get; set; } = "";
    /// <summary>Date de retour au format yyyy-MM-dd (vide = prêt actif).</summary>
    public string DateRetour { get; set; } = "";
}

/// <summary>Réunion avec participants et feuille de présence.</summary>
public class Reunion
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    /// <summary>Date au format yyyy-MM-dd.</summary>
    public string Date { get; set; } = "";
    public string? HeureDebut { get; set; }
    public string? HeureFin { get; set; }
    public string Lieu { get; set; } = "";
    public List<string> Participants { get; set; } = new();
    /// <summary>Présence par identifiant : present / absent / excuse.</summary>
    public Dictionary<string, string> Presences { get; set; } = new();
    public string CompteRendu { get; set; } = "";
    public string DateCreation { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public static class PresenceStatuts
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Excuse = "excuse";

    public static string Label(string statut) => statut switch
    {
        Absent => "Absent",
        Excuse => "Excusé",
        _ => "Présent"
    };

    public static string Icon(string statut) => statut switch
    {
        Absent => "✗",
        Excuse => "◐",
        _ => "✓"
    };
}

/// <summary>Situation particulière d'un membre (visite, accompagnement, événement important…).</summary>
public class Situation
{
    public string Id { get; set; } = "";
    public string MembreIdentifiant { get; set; } = "";
    public string Type { get; set; } = SituationTypes.Visite;
    public string Titre { get; set; } = "";
    public string Description { get; set; } = "";
    public string Statut { get; set; } = SituationStatuts.Ouvert;
    public string Priorite { get; set; } = CalendarEventTypes.PrioriteNormale;
    public string DateCreation { get; set; } = "";
    public string? DateResolution { get; set; }
    public string CreatedBy { get; set; } = "";
}

public static class SituationTypes
{
    public const string Visite = "visite";
    public const string Accompagnement = "accompagnement";
    public const string EvenementImportant = "evenement_important";
    public const string Autre = "autre";

    public static string Label(string type) => type switch
    {
        Accompagnement => "Accompagnement",
        EvenementImportant => "Événement important",
        Autre => "Autre",
        _ => "Visite nécessaire"
    };

    public static string Icon(string type) => type switch
    {
        Accompagnement => "🤝",
        EvenementImportant => "⭐",
        Autre => "📌",
        _ => "🏠"
    };
}

public static class SituationStatuts
{
    public const string Ouvert = "ouvert";
    public const string EnCours = "en_cours";
    public const string Resolu = "resolu";

    public static string Label(string statut) => statut switch
    {
        EnCours => "En cours",
        Resolu => "Résolu",
        _ => "Ouvert"
    };

    public static string Icon(string statut) => statut switch
    {
        EnCours => "🔄",
        Resolu => "✅",
        _ => "📬"
    };
}

/// <summary>Vote interne : options, bulletins (un par membre, modifiable tant qu'ouvert).</summary>
public class Vote
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string Statut { get; set; } = VoteStatuts.Ouvert;
    /// <summary>Date de clôture au format yyyy-MM-dd (vide = manuelle).</summary>
    public string DateCloture { get; set; } = "";
    public List<Bulletin> Bulletins { get; set; } = new();
    public string DateCreation { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public class Bulletin
{
    public string VotantIdentifiant { get; set; } = "";
    public string Option { get; set; } = "";
    public string Date { get; set; } = "";
}

public static class VoteStatuts
{
    public const string Ouvert = "ouvert";
    public const string Clos = "clos";

    public static string Label(string statut) => statut switch
    {
        Clos => "Clos",
        _ => "Ouvert"
    };
}

/// <summary>Élément du journal multimédia (photo, vidéo, audio — 100 % local).</summary>
public class MediaItem
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = MediaTypes.Photo;
    public string Mime { get; set; } = "";
    public long Taille { get; set; }
    /// <summary>Contenu en data URL.</summary>
    public string Data { get; set; } = "";
    /// <summary>Date au format yyyy-MM-dd.</summary>
    public string Date { get; set; } = "";
    public string DateCreation { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public static class MediaTypes
{
    public const string Photo = "photo";
    public const string Video = "video";
    public const string Audio = "audio";

    public static string Label(string type) => type switch
    {
        Video => "Vidéo",
        Audio => "Audio",
        _ => "Photo"
    };

    public static string Icon(string type) => type switch
    {
        Video => "🎬",
        Audio => "🎙",
        _ => "🖼"
    };
}

/// <summary>Rappel intelligent calculé (tâches, anniversaires, événements, cotisations).</summary>
public class Rappel
{
    public string Id { get; set; } = "";
    public string Categorie { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>normale / importante / urgente.</summary>
    public string Priorite { get; set; } = CalendarEventTypes.PrioriteNormale;
    /// <summary>Page de destination : taches, users, calendar, finances.</summary>
    public string PageCible { get; set; } = "";
}

public class SessionInfo
{
    public string UserId { get; set; } = "";
    public long ExpiryUtcTicks { get; set; }
}

public static class Roles
{
    public const string BaseAdmin = "Administrateur de la base";
    public const string Administrateur = "Administrateur";
    public const string Gestionnaire = "Gestionnaire";
    public const string Utilisateur = "Utilisateur";

    public static bool IsAdmin(string? role) => role == Administrateur || role == BaseAdmin;

    /// <summary>Rôles pouvant apparaître dans la matrice des permissions (le compte technique a tout, verrouillé).</summary>
    public static IReadOnlyList<string> MatrixRoles { get; } = new[]
    {
        Administrateur, Gestionnaire, Utilisateur
    };
}

/// <summary>
/// Clés de permissions vérifiées via <c>DataStore.Can()</c>.
/// Les clés futures (finances, matériel…) sont déclarées dès maintenant
/// pour que la vague 2 s'y branche sans migration.
/// </summary>
public static class Permissions
{
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string FieldsManage = "fields.manage";
    public const string ActivityView = "activity.view";
    public const string CalendarUse = "calendar.use";
    public const string DocumentsUse = "documents.use";
    public const string ArchiveView = "archive.view";
    public const string SettingsManage = "settings.manage";
    public const string CommunityUse = "community.use";
    public const string SyncUse = "sync.use";
    public const string RolesManage = "roles.manage";
    public const string ExportData = "export.data";
    public const string ImportData = "import.data";
    // Vague 2 (modules du menu « … »)
    public const string MessagerieUse = "messagerie.use";
    public const string TachesUse = "taches.use";
    public const string FinancesManage = "finances.manage";
    public const string MaterielManage = "materiel.manage";
    public const string PresenceManage = "presence.manage";
    public const string RappelsUse = "rappels.use";
    public const string SituationsManage = "situations.manage";
    public const string CarteMembreView = "carte_membre.view";
    public const string VotesManage = "votes.manage";
    public const string JournalMediaUse = "journal_media.use";

    public sealed record Definition(string Key, string Groupe, string Label);

    public static IReadOnlyList<Definition> Toutes { get; } = new[]
    {
        new Definition(UsersView, "Utilisateurs", "Voir la liste des utilisateurs"),
        new Definition(UsersManage, "Utilisateurs", "Ajouter / modifier / supprimer des utilisateurs"),
        new Definition(FieldsManage, "Utilisateurs", "Gérer les champs personnalisés"),
        new Definition(ArchiveView, "Utilisateurs", "Voir et restaurer les archives"),
        new Definition(ActivityView, "Suivi", "Voir le journal d'activité"),
        new Definition(CalendarUse, "Suivi", "Utiliser le calendrier"),
        new Definition(TachesUse, "Suivi", "Utiliser les tâches et leur suivi"),
        new Definition(DocumentsUse, "Contenus", "Utiliser la gestion documentaire"),
        new Definition(CommunityUse, "Contenus", "Voir l'histoire de la communauté"),
        new Definition(JournalMediaUse, "Contenus", "Utiliser le journal multimédia"),
        new Definition(SettingsManage, "Application", "Gérer les paramètres"),
        new Definition(SyncUse, "Application", "Synchroniser avec un autre appareil"),
        new Definition(ExportData, "Application", "Exporter les données"),
        new Definition(ImportData, "Application", "Importer des données"),
        new Definition(RolesManage, "Application", "Gérer les rôles et permissions"),
        new Definition(MessagerieUse, "Échanges", "Utiliser la messagerie interne"),
        new Definition(RappelsUse, "Échanges", "Utiliser les rappels intelligents"),
        new Definition(CarteMembreView, "Échanges", "Voir la carte de membre numérique"),
        new Definition(VotesManage, "Décisions", "Gérer les votes et décisions internes"),
        new Definition(FinancesManage, "Gestion", "Gérer les finances (cotisations, trésorerie)"),
        new Definition(MaterielManage, "Gestion", "Gérer le matériel"),
        new Definition(PresenceManage, "Gestion", "Gérer présence et réunions"),
        new Definition(SituationsManage, "Gestion", "Suivre les situations particulières"),
    };
}

/// <summary>Permissions accordées à un rôle (matrice modifiable, sauf compte technique).</summary>
public class RolePermissionSet
{
    public string Role { get; set; } = "";
    public Dictionary<string, bool> Allowed { get; set; } = new();
}

// ---------- Synchronisation locale (fichier d'échange .gusync.json) ----------

/// <summary>Contenu sélectionné pour un paquet d'échange entre appareils.</summary>
public class SyncOptions
{
    public bool Users { get; set; } = true;
    public bool Fields { get; set; } = true;
    public bool Events { get; set; } = true;
    public bool Documents { get; set; } = true;
    /// <summary>Inclure le contenu binaire des documents (sinon métadonnées seules).</summary>
    public bool DocumentFiles { get; set; } = true;
    public bool Folders { get; set; } = true;
    public bool Community { get; set; } = true;
    public bool Archives { get; set; } = true;
    public bool Activity { get; set; } = false;
    public bool Settings { get; set; } = true;
    public bool Permissions { get; set; } = true;
    public bool Messages { get; set; } = true;
    public bool Taches { get; set; } = true;
    public bool Cotisations { get; set; } = true;
    public bool Tresorerie { get; set; } = true;
    public bool Materiels { get; set; } = true;
    public bool Reunions { get; set; } = true;
    public bool Situations { get; set; } = true;
    public bool Votes { get; set; } = true;
    public bool JournalMedia { get; set; } = true;
    /// <summary>Inclure le contenu binaire du journal (sinon métadonnées seules).</summary>
    public bool JournalMediaFiles { get; set; } = true;
}

/// <summary>Paquet d'échange : état d'un appareil à un instant T, fusionné à l'import.</summary>
public class SyncPackage
{
    public int Version { get; set; } = 1;
    public string ExportedAt { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public SyncOptions Options { get; set; } = new();
    public List<UserAccount> Users { get; set; } = new();
    public List<FieldDefinition> Fields { get; set; } = new();
    public List<CalendarEvent> Events { get; set; } = new();
    public List<DocumentItem> Documents { get; set; } = new();
    public List<DocumentFolder> Folders { get; set; } = new();
    public CommunityStory Community { get; set; } = new();
    public List<ArchivedUser> Archives { get; set; } = new();
    public List<ActivityEntry> Activity { get; set; } = new();
    public AppSettings? Settings { get; set; }
    public List<RolePermissionSet> Permissions { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
    public List<Tache> Taches { get; set; } = new();
    public List<Cotisation> Cotisations { get; set; } = new();
    public List<TransactionTresorerie> Tresorerie { get; set; } = new();
    public List<Materiel> Materiels { get; set; } = new();
    public List<PretMateriel> Prets { get; set; } = new();
    public List<Reunion> Reunions { get; set; } = new();
    public List<Situation> Situations { get; set; } = new();
    public List<Vote> Votes { get; set; } = new();
    public List<MediaItem> JournalMedia { get; set; } = new();
}

/// <summary>Un enregistrement modifié des deux côtés depuis la dernière synchro.</summary>
public class SyncConflict
{
    public string Collection { get; set; } = "";
    public string Key { get; set; } = "";
    /// <summary>Libellé lisible (nom d'utilisateur, titre…).</summary>
    public string Titre { get; set; } = "";
    public string DetailLocal { get; set; } = "";
    public string DetailDistant { get; set; } = "";
    /// <summary>Cas : modified (les deux), deletedHere, deletedPeer.</summary>
    public string Cas { get; set; } = "modified";
}

/// <summary>Résultat de l'analyse d'un paquet avant application.</summary>
public class SyncPreview
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string DeviceName { get; set; } = "";
    public string ExportedAt { get; set; } = "";
    public Dictionary<string, int> NewFromPeer { get; set; } = new();
    public Dictionary<string, int> UpdatedFromPeer { get; set; } = new();
    public Dictionary<string, int> DeletedOnPeer { get; set; } = new();
    public string? Warning { get; set; }
    public List<SyncConflict> Conflicts { get; set; } = new();
    public int TotalChanges => NewFromPeer.Values.Sum() + UpdatedFromPeer.Values.Sum()
        + DeletedOnPeer.Values.Sum() + Conflicts.Count;
}

/// <summary>Choix utilisateur pour un conflit : conserver le local ou prendre l'entrant.</summary>
public class SyncResolution
{
    public string Collection { get; set; } = "";
    public string Key { get; set; } = "";
    public bool UseIncoming { get; set; }
}

/// <summary>État de la dernière synchro (base commune pour la détection de conflits).</summary>
public class SyncSnapshot
{
    public string DeviceId { get; set; } = "";
    public string? LastSyncAt { get; set; }
    public string? LastPeerName { get; set; }
    public Dictionary<string, string> Hashes { get; set; } = new();
    /// <summary>Conflits tranchés « conserver le local » : clé → hash distant écarté (ne plus redemander).</summary>
    public Dictionary<string, string> Dismissed { get; set; } = new();
}

public class ArchivedUser
{
    public UserAccount User { get; set; } = new();
    public string DeletedAt { get; set; } = "";
    public string DeletedBy { get; set; } = "";
}

public static class ActivityTypes
{
    public const string Login = "login";
    public const string Logout = "logout";
    public const string Create = "create";
    public const string Update = "update";
    public const string Activate = "activate";
    public const string Deactivate = "deactivate";
    public const string Delete = "delete";
    public const string Import = "import";
    public const string Export = "export";
    public const string PasswordReset = "password_reset";
    public const string FieldsChange = "fields_change";
    public const string CalendarCreate = "calendar_create";
    public const string CalendarUpdate = "calendar_update";
    public const string CalendarDelete = "calendar_delete";
    public const string DossierCreate = "folder_create";
    public const string DossierUpdate = "folder_update";
    public const string DossierDelete = "folder_delete";
    public const string DocumentCreate = "document_create";
    public const string DocumentUpdate = "document_update";
    public const string DocumentDelete = "document_delete";
    public const string DocumentRestore = "document_restore";
    public const string DocumentShare = "document_share";
    public const string SyncExport = "sync_export";
    public const string SyncImport = "sync_import";
    public const string RolesChange = "roles_change";
    public const string MessageSend = "message_send";
    public const string MessageDelete = "message_delete";
    public const string TacheCreate = "tache_create";
    public const string TacheUpdate = "tache_update";
    public const string TacheDelete = "tache_delete";
    public const string FinanceCotisation = "finance_cotisation";
    public const string FinanceTresorerie = "finance_tresorerie";
    public const string MaterielChange = "materiel_change";
    public const string PretChange = "pret_change";
    public const string ReunionChange = "reunion_change";
    public const string PresenceMark = "presence_mark";
    public const string SituationChange = "situation_change";
    public const string VoteChange = "vote_change";
    public const string VoteCast = "vote_cast";
    public const string MediaChange = "media_change";

    public static string Label(string type) => type switch
    {
        Login => "Connexion",
        Logout => "Déconnexion",
        Create => "Création",
        Update => "Modification",
        Activate => "Activation",
        Deactivate => "Désactivation",
        Delete => "Suppression",
        Import => "Importation",
        Export => "Exportation",
        PasswordReset => "Réinitialisation MDP",
        FieldsChange => "Champs du formulaire",
        CalendarCreate => "Ajout d'événement",
        CalendarUpdate => "Modification d'événement",
        CalendarDelete => "Suppression d'événement",
        DossierCreate => "Création de dossier",
        DossierUpdate => "Modification de dossier",
        DossierDelete => "Suppression de dossier",
        DocumentCreate => "Ajout de document",
        DocumentUpdate => "Modification de document",
        DocumentDelete => "Suppression de document",
        DocumentRestore => "Restauration de document",
        DocumentShare => "Partage de document",
        SyncExport => "Export de synchronisation",
        SyncImport => "Import de synchronisation",
        RolesChange => "Rôles et permissions",
        MessageSend => "Envoi de message",
        MessageDelete => "Suppression de message",
        TacheCreate => "Création de tâche",
        TacheUpdate => "Suivi de tâche",
        TacheDelete => "Suppression de tâche",
        FinanceCotisation => "Cotisation",
        FinanceTresorerie => "Trésorerie",
        MaterielChange => "Matériel",
        PretChange => "Prêt de matériel",
        ReunionChange => "Réunion",
        PresenceMark => "Feuille de présence",
        SituationChange => "Situation particulière",
        VoteChange => "Vote interne",
        VoteCast => "Vote exprimé",
        MediaChange => "Journal multimédia",
        _ => type
    };
}

public class OperationResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";

    public static OperationResult Ok() => new() { Success = true };
    public static OperationResult Fail(string error) => new() { Success = false, Error = error };
}

public class UserOperationResult : OperationResult
{
    public string? UserId { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<UserAccount>? Users { get; set; }
    public int Count => Users?.Count ?? 0;
}
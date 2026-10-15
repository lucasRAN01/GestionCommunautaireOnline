using System.Collections.Generic;
using System.Linq;

namespace GestionUtilisateurs.Shared.Models;

/// <summary>Une rubrique d'aide (titre + points clés).</summary>
public sealed record HelpSection(string Title, string[] Points);

/// <summary>Fiche d'aide d'une fonctionnalité (utilisée par le ⓘ d'aide et par le guide utilisateur).</summary>
public sealed record HelpEntry(string Key, string Title, string Icon, string Summary, HelpSection[] Sections);

/// <summary>
/// Catalogue central des fiches d'aide de l'application.
/// Utilisé pour : la bannière contextuelle au premier usage, le bouton ⓘ
/// de la barre du haut et la page « Guide utilisateur » complète.
/// </summary>
public static class HelpCatalog
{
    public static IReadOnlyList<HelpEntry> Entries { get; } = Build();

    private static readonly Dictionary<string, HelpEntry> ByKey = Entries.ToDictionary(e => e.Key, System.StringComparer.OrdinalIgnoreCase);

    public static HelpEntry? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return ByKey.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>La fiche correspondant au code de page de l'application.</summary>
    public static HelpEntry? ForPage(string? currentPage) => Find(currentPage);

    private static List<HelpEntry> Build() => new()
    {
        new("dashboard", "Accueil", "🏠",
            "Votre tableau de bord personnel : les chiffres clés, les dernières arrivées et les modules que vous ouvrez le plus.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Le haut de page résume : membres actifs, nouvelles inscriptions et rappels urgents.",
                    "La courbe « Répartition par rôle » s'appuie sur les statuts définis dans « Rôles et permissions ».",
                    "« Derniers utilisateurs ajoutés » liste les comptes créés le plus récemment.",
                    "Finances, tâches et matériel sont repliés par défaut : touchez le titre pour les déplier."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Le logo (en haut à gauche) ouvre l'histoire de la communauté.",
                    "Les graphes sont volontairement compacts pour rester lisibles sur petit écran."
                })
            }),
        new("users", "Utilisateurs", "👥",
            "La liste des membres de la communauté : recherchez, ajoutez, modifiez ou consultez chaque compte.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Recherchez immédiatement par nom, identifiant ou e-mail depuis le champ en haut.",
                    "Touchez un utilisateur pour ouvrir sa fiche détaillée (photo, signature, documents, cotisations…).",
                    "« ＋ Ajouter » crée un nouveau compte : identifiant et rôle, avec mot de passe par défaut si laissé vide.",
                    "Le bouton ⋮ d'une fiche permet de modifier, désactiver ou archiver le compte."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Un compte inactif ne peut plus se connecter mais reste consultable.",
                    "Archiver range le compte dans « Archives des utilisateurs ». Supprimer est définitif."
                })
            }),
        new("profile", "Mes informations", "👤",
            "Votre propre fiche : modifiez vos coordonnées, votre photo, votre signature et votre mot de passe.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Touchez le bouton en haut à droite (votre avatar) puis « Mes informations ».",
                    "La photo de profil peut venir de la galerie ou d'une photo prise sur le moment.",
                    "La signature se remplit via « Ouvrir » : écrivez ou dessinez, puis validez.",
                    "Changez votre mot de passe depuis l'onglet dédié."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Vos informations sont stockées uniquement sur cet appareil.",
                    "La photo et la signature apparaissent sur votre carte de membre."
                })
            }),
        new("activity", "Journal d'activité", "📝",
            "Trace de tout ce qui se passe dans l'application : connexions, créations, modifications et suppressions.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Chaque évènement indique qui, quoi et quand.",
                    "Passez d'une vue à l'autre selon le type d'activité (connexions, utilisateurs, documents…).",
                    "Les exports Excel, HTML ou CSV permettent une relecture hors application."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Le journal est local ; il n'est jamais envoyé à un serveur.",
                    "Une activité conservée trop longtemps peut être vidée depuis la gestion des données."
                })
            }),
        new("calendar", "Calendrier", "📅",
            "Tous les événements de la communauté au même endroit : réunions, anniversaires, échéances.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Naviguez entre les mois avec les flèches ◀ ▶.",
                    "Touchez un jour pour voir les événements et en créer un.",
                    "« ＋ Ajouter un événement » : intitulé, date, heure et description.",
                    "La pastille sur l'icône Calendrier (en bas) indique les événements du jour."
                })
            }),
        new("fields", "Champs personnalisés", "🧩",
            "Personnalisez les fiches utilisateurs avec vos propres champs (matricule, section, blouse…)",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Ajouter un champ » définit le libellé, le type (texte, choix, nombre…) et si le champ est obligatoire.",
                    "Les champs créés apparaissent dans la fiche et le formulaire de chaque utilisateur.",
                    "Un champ personnalisé peut être réordonné ou masqué."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Les valeurs saisies restent liées au champ : supprimer le champ efface aussi les valeurs.",
                    "Champ « Identifiant » est un champ système non supprimable."
                })
            }),
        new("settings", "Paramètres", "⚙️",
            "Généralités de l'application : nom, thème, carte de membre, ordre du menu et guide.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "« Apparence » choisit le thème clair/sombre et la couleur principale.",
                    "« Configuration carte de membre » ajuste slogan et citation imprimés sur la carte.",
                    "« Stockage » surveille l'espace utilisé et ajuste le quota d'alerte.",
                    "« Export automatique » sauvegarde chaque mois toutes les données en local.",
                    "« Ordre du menu » réorganise les cartes du menu « … »."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Le bouton ⓘ en haut de chaque écran ouvre l'aide du module courant.",
                    "« Guide utilisateur » redirige vers le guide complet de l'application."
                })
            }),
        new("documents", "Gestion documentaire", "📄",
            "Classez et retrouvez tous vos fichiers : comptes rendus, justificatifs, plans, photos…",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Nouveau dossier » organise vos documents par thème.",
                    "« ＋ Ajouter un document » choisit un fichier (généralement compressé automatiquement) et l'associe éventuellement à un utilisateur.",
                    "Touchez un document pour le visualiser, le renommer ou le déplacer vers un autre dossier.",
                    "La corbeille récupère les documents supprimés avant suppression définitive."
                })
            }),
        new("archive", "Archives des utilisateurs", "🗃",
            "Les comptes archivés : consultez-les, restaurez-les ou supprimez-les définitivement.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Restaurer un compte le renvoie dans la liste des utilisateurs, avec ses données.",
                    "Supprimer définitivement efface la fiche et ses pièces associées : action irréversible."
                })
            }),
        new("community", "Histoire de la communauté", "📖",
            "Racontez et faites défiler l'histoire de votre groupe : titre, sous-titre et chapitres.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Touchez le logo en haut de l'écran pour ouvrir l'histoire depuis n'importe où.",
                    "Dans l'écran Communauté, glissez un doigt horizontalement pour changer de chapitre.",
                    "« ＋ Chapitre » ajoute une étape datée ; le titre et le sous-titre se modifient en haut."
                })
            }),
        new("comptes", "Comptes en ligne", "👤",
            "Acceptez ou refusez les inscriptions et gérez l'accès de chaque membre.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Chaque nouvelle inscription apparaît en haut de la liste avec le statut « en attente ».",
                    "« Accepter » ouvre l'accès en choisissant le rôle ; « Refuser » le bloque.",
                    "Un compte accepté peut être désactivé à tout moment, puis réactivé."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Un e-mail GitHub prévient l'administrateur quand des comptes attendent une validation.",
                    "Le premier compte créé est automatiquement administrateur."
                })
            }),
        new("roles", "Rôles et permissions", "🛡",
            "Contrôlez ce que chaque rôle peut voir et faire dans l'application.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Chaque rôle possède une liste de permissions activables/désactivables.",
                    "Les changements s'appliquent immédiatement aux comptes du rôle concerné.",
                    "Un compte peut cumuler un rôle applicatif et des permissions particulières."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Des verrous empêchent de bloquer l'accès des administrateurs.",
                    "Le compte « dba » gère la base ; le compte « admin » administre l'application."
                })
            }),
        new("messagerie", "Messagerie", "✉",
            "Échangez des messages entre membres, visibles par les destinataires sur tous leurs appareils.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Nouveau message » choisit un ou plusieurs destinataires.",
                    "La liste indique les messages en attente de votre réponse, en cours et clos.",
                    "Touchez un message pour répondre, marquer comme traité ou le relancer.",
                    "Le compteur sur le menu « … » signale les conversations non lues."
                })
            }),
        new("taches", "Tâches et suivi", "☑",
            "Planifiez les choses à faire et suivez qui en est responsable.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Nouvelle tâche » : intitulé, assigné, échéance, priorité.",
                    "Dépliez les sections « À faire », « En cours », « Terminées ».",
                    "Une tâche peut être commentée puis archivée une fois close."
                })
            }),
        new("finances", "Finances", "💰",
            "Cotisations et trésorerie : suivez les impayés et l'argent de la communauté.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Trois cartes : Solde disponible, Cotisations en cours et Impayés.",
                    "« Cotisation » enregistre le règlement annuel d'un membre (montant, date, échéance).",
                    "« Trésorerie » saisit chaque mouvement de caisse ; le solde en découle.",
                    "Les impayés se régularisent depuis la fiche du membre concerné."
                })
            }),
        new("materiel", "Matériel", "🧰",
            "Inventaire du matériel de la communauté et suivi des prêts.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Ajouter un matériel » : nom, référence, état.",
                    "Un prêt associe un matériel à un membre et demande une date de retour.",
                    "Les prêts en cours apparaissent avec le membre concerné ; le compteur du menu « … » les signale."
                })
            }),
        new("reunions", "Présence et réunions", "👥",
            "Organisez des réunions, notez la présence et les participants.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Réunion » saisit la date, l'objet et les participants.",
                    "Un membre peut confirmer sa présence depuis sa fiche ou depuis la réunion.",
                    "L'historique des présences aide à préparer les prochaines rencontres."
                })
            }),
        new("rappels", "Rappels intelligents", "⏰",
            "Ne manquez plus une échéance : rappels de tâches, de cotisations, d'événements.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "L'application regroupe les échéances proches : tâches, prêts, cotisations, événements.",
                    "Marquer un rappel « traité » le retire de la liste.",
                    "Les ajustements de rappel dépendent des dates saisies dans chaque module."
                })
            }),
        new("situations", "Situations particulières", "🤝",
            "Suivez discrètement les situations sensibles nécessitant attention (santé, logement, aide…).",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Chaque situation est privée et visible uniquement par ce que les permissions autorisent.",
                    "Un suivi peut être marqué « résolu » et reste alors consultable en historique."
                })
            }),
        new("carte", "Carte de membre", "🪪",
            "La carte d'adhérent : photo, QR code, signature, export PDF et HTML.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "La carte se compose d'une face avant (photo, identité, QR) et d'un dos (signature, citation).",
                    "Touchez le QR code pour l'agrandir et le faire scanner facilement.",
                    "« Exporter en PDF » ou « Exporter en HTML » produit une carte utilisable hors application.",
                    "Slogan et citation du dos se personnalisent dans les Paramètres."
                })
            }),
        new("votes", "Votes et décisions", "🗳",
            "Soumettez une question à la communauté et recueillez les bulletins.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Créer un vote » : question, choix multiples, date de clôture.",
                    "Chaque membre disposant de la permission vote une seule fois ; les résultats s'actualisent.",
                    "Un vote clôturé conserve son résultat et peut être archivé."
                })
            }),
        new("journal", "Journal multimédia", "🎞",
            "Les souvenirs en photos, vidéos et audio, classés par date.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "« ＋ Ajouter un média » importe une photo, une vidéo ou un son avec date et description.",
                    "La lecture s'ouvre dans l'application, ou dans un nouvel onglet du navigateur.",
                    "Les médias sont compressés automatiquement pour économiser le stockage."
                })
            }),
        new("data", "Gestion de données", "🗂",
            "Réservé au gestionnaire de la base : consulter et modifier les données de la communauté sans JSON.",
            new[]
            {
                new HelpSection("Comprendre", new[]
                {
                    "Choisissez une collection (utilisateurs, événements, documents…) via les onglets.",
                    "Recherchez, ajoutez, modifiez ou supprimez des enregistrements directement.",
                    "Chaque modification est enregistrée instantanément et protégée contre les suppressions accidentelles."
                }),
                new HelpSection("Bon à savoir", new[]
                {
                    "Les droits de ce module sont réservés au compte « dba ».",
                    "La réinitialisation vide les données de la communauté et ne garde que votre compte."
                })
            }),
        new("capacite", "Capacité du serveur", "📊",
            "Suivez l'espace utilisé sur le serveur gratuit et libérez de la place avant qu'il soit plein.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "La page affiche l'espace de la base de données et celui des fichiers, en pourcentage de la limite gratuite.",
                    "Au-delà de 70 %, une alerte s'affiche et un e-mail GitHub prévient l'administrateur.",
                    "Videz la corbeille des documents et supprimez les médias inutiles pour libérer de l'espace."
                })
            }),
        new("guide", "Guide utilisateur", "🧭",
            "Tout le fonctionnement de l'application expliqué pas à pas, avec captures d'écran.",
            new[]
            {
                new HelpSection("Comment faire", new[]
                {
                    "Le sommaire en haut permet d'aller directement au module recherché.",
                    "Chaque fiche explique le rôle du module puis les actions principales.",
                    "Les captures d'écran illustrent les écrans clés."
                })
            })
    };
}
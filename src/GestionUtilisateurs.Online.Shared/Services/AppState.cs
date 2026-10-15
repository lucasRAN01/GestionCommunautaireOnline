namespace GestionUtilisateurs.Shared.Services;

/// <summary>Navigation interne (pas de routeur) et notifications toast.</summary>
public class AppState
{
    private string _currentPage = "dashboard";
    private readonly List<string> _history = new();

    public string CurrentPage => _currentPage;

    /// <summary>Indique si une page précédente existe (affiche le bouton retour).</summary>
    public bool HasHistory => _history.Count > 0;

    public event Action? PageChanged;
    public event Action<string, string>? Toast;
    public event Action? SearchChanged;

    public void Navigate(string page)
    {
        if (_currentPage == page) return;
        _history.Add(_currentPage);
        _currentPage = page;
        PageChanged?.Invoke();
    }

    /// <summary>Revient à la page précédente (geste de balayage vers la gauche ou la droite).</summary>
    public void GoBack()
    {
        if (_history.Count == 0) return;
        _currentPage = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        PageChanged?.Invoke();
    }

    /// <summary>Efface l'historique de navigation (connexion/déconnexion).</summary>
    public void ResetNavigation()
    {
        _history.Clear();
        _currentPage = "dashboard";
        PageChanged?.Invoke();
    }

    /// <summary>Démarre sur la page d'accueil de la session sans empiler l'écran de connexion.</summary>
    public void GoToInitial(string page)
    {
        _history.Clear();
        _currentPage = page;
        PageChanged?.Invoke();
    }

    private string? _pendingUserId;

    /// <summary>Ouvre directement la fiche d'un utilisateur (depuis le tableau de bord).</summary>
    public void NavigateToUser(string id)
    {
        _pendingUserId = id;
        Navigate("users");
    }

    public string? PendingUserId => _pendingUserId;

    public void ConsumePendingUser() => _pendingUserId = null;

    private string _globalSearch = "";
    public string GlobalSearch => _globalSearch;

    public void SetGlobalSearch(string query)
    {
        _globalSearch = query;
        SearchChanged?.Invoke();
    }

    private string? _usersStatusFilter;
    private string? _usersRoleFilter;

    public string? UsersStatusFilter => _usersStatusFilter;
    public string? UsersRoleFilter => _usersRoleFilter;

    /// <summary>Navigue vers la liste des utilisateurs avec des filtres (statut et/ou rôle).</summary>
    public void NavigateWithUsersFilter(string? status, string? role)
    {
        _usersStatusFilter = status;
        _usersRoleFilter = role;
        Navigate("users");
    }

    /// <summary>Consomme puis efface les filtres transmis par le tableau de bord.</summary>
    public void ConsumeUsersFilter()
    {
        _usersStatusFilter = null;
        _usersRoleFilter = null;
    }

    public void Notify(string message, string type)
        => Toast?.Invoke(message, type);

    public void NotifySuccess(string message) => Toast?.Invoke(message, "success");
    public void NotifyError(string message) => Toast?.Invoke(message, "error");
    public void NotifyWarning(string message) => Toast?.Invoke(message, "warning");
    public void NotifyInfo(string message) => Toast?.Invoke(message, "info");
}
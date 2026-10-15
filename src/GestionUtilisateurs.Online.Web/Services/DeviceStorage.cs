namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>
/// Équivalent Web de DeviceStorage (MAUI) : espace disque réel indisponible
/// dans le navigateur (sandbox). Retourne 0 = inconnu ; l'interface utilise
/// alors un plafond large sans bloquer (voir SettingsPage.QuotaMaxMb).
/// </summary>
public static class DeviceStorage
{
    public static long GetFreeBytes() => 0;

    public static long GetTotalBytes() => 0;
}

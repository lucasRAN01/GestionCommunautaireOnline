namespace GestionUtilisateurs.Shared.Services;

public interface IStorageProvider
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
}
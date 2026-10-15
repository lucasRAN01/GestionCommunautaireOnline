using System.Collections.Generic;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class MemoryStorageProvider : IStorageProvider
{
    private readonly Dictionary<string, string> _store = new();

    public string? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value) => _store[key] = value;

    public void Remove(string key) => _store.Remove(key);

    public int Count() => _store.Count;
}
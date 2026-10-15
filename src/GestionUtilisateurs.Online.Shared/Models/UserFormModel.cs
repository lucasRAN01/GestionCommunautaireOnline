namespace GestionUtilisateurs.Shared.Models;

/// <summary>Modèle UI du formulaire utilisateur (champs fixes + champs flexibles enrichis).</summary>
public class UserFormModel
{
    public string Id { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Identifiant { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telephone { get; set; } = "";
    public string Role { get; set; } = "Utilisateur";
    public bool Actif { get; set; } = true;
    public string Notes { get; set; } = "";
    /// <summary>Date de naissance (yyyy-MM-dd) liée au calendrier des anniversaires.</summary>
    public string DateNaissance { get; set; } = "";
    /// <summary>Matricule unique du membre.</summary>
    public string Matricule { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public bool ChangePasswordOnEdit { get; set; }

    public Dictionary<string, FieldValue> Custom { get; set; } = new();
    public Dictionary<string, string> CustomNumberText { get; set; } = new();
    public Dictionary<string, PendingUpload> PendingFiles { get; set; } = new();
    public string Photo { get; set; } = "";
    public string Signature { get; set; } = "";

    public static UserFormModel FromUser(UserAccount u)
    {
        var m = new UserFormModel
        {
            Id = u.Id,
            Prenom = u.Prenom,
            Nom = u.Nom,
            Identifiant = u.Identifiant,
            Email = u.Email,
            Telephone = u.Telephone,
            Role = u.Role,
            Actif = u.Actif,
            Notes = u.Notes,
            Photo = u.Photo ?? "",
            Signature = u.Signature ?? "",
            DateNaissance = u.DateNaissance ?? "",
            Matricule = u.Matricule ?? "",
            ChangePasswordOnEdit = false
        };
        m.Custom = u.Champs.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var (id, val) in u.Champs)
        {
            if (val.Nombre != null) m.CustomNumberText[id] = val.Nombre.Value.ToString();
        }
        return m;
    }

    public string FieldText(string fieldId)
        => Custom.TryGetValue(fieldId, out var v) ? v.Texte : "";

    public void SetFieldText(string fieldId, string text)
        => GetOrCreate(fieldId).Texte = text;

    public void SetFieldNumber(string fieldId, string rawText)
    {
        CustomNumberText[fieldId] = rawText;
        var v = GetOrCreate(fieldId);
        if (double.TryParse(rawText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n))
            v.Nombre = n;
        else if (string.IsNullOrWhiteSpace(rawText)) v.Nombre = null;
    }

    public void SetFieldBool(string fieldId, bool value)
        => GetOrCreate(fieldId).Booleen = value;

    public string BoolSelection(string fieldId)
        => Custom.TryGetValue(fieldId, out var v) ? (v.Booleen ? "oui" : "non") : "";

    public void SetBoolSelection(string fieldId, string value)
        => GetOrCreate(fieldId).Booleen = value == "oui";

    public bool HasChoice(string fieldId, string option)
    {
        if (!Custom.TryGetValue(fieldId, out var v)) return false;
        return SplitChoices(v.Texte).Contains(option, StringComparer.Ordinal);
    }

    public void SetChoice(string fieldId, string option, bool selected)
    {
        var list = SplitChoices(FieldText(fieldId)).ToList();
        if (selected)
        {
            if (!list.Contains(option, StringComparer.Ordinal)) list.Add(option);
        }
        else
        {
            list.RemoveAll(x => x == option);
        }
        GetOrCreate(fieldId).Texte = string.Join(", ", list);
    }

    private static IEnumerable<string> SplitChoices(string? text)
        => (text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public void SetFieldFile(string fieldId, FileRef fichier)
        => GetOrCreate(fieldId).Fichier = fichier;

    public void ClearFieldFile(string fieldId)
    {
        if (Custom.TryGetValue(fieldId, out var v)) v.Fichier = null;
    }

    private FieldValue GetOrCreate(string fieldId)
    {
        if (!Custom.TryGetValue(fieldId, out var v)) { v = new FieldValue(); Custom[fieldId] = v; }
        return v;
    }

    /// <summary>Construit le brouillon UserAccount à valider puis persister.</summary>
    public UserAccount ToDraft() => new()
    {
        Id = Id,
        Prenom = Prenom,
        Nom = Nom,
        Identifiant = Identifiant,
        Email = Email,
        Telephone = Telephone,
        Role = Role,
        Actif = Actif,
        Notes = Notes,
        MotDePasse = Password,
        DateNaissance = DateNaissance,
        Matricule = Matricule,
        Photo = string.IsNullOrWhiteSpace(Photo) ? null : Photo,
        Signature = string.IsNullOrWhiteSpace(Signature) ? null : Signature,
        DateCreation = "",
        Champs = Custom
    };
}

public class PendingUpload
{
    public string Key { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Taille { get; set; }
    public string Base64 { get; set; } = "";
    public string DataUrl { get; set; } = "";
}
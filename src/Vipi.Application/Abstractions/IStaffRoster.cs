namespace Vipi.Application.Abstractions;

/// <summary>Riga del roster staff per il picker degli AOD/DIR (solo staffisti IT attivi).</summary>
public sealed record StaffRosterEntry(
    int UserId,
    string? DisplayName,
    string? AtcRating,
    IReadOnlyList<string> StaffPositions,
    DateTime LastLoginUtc);

/// <summary>
/// Persistenza del roster degli staffisti IT (popolato ai login, ri-verificato via API). Impl. EF.
/// </summary>
public interface IStaffRosterRepository
{
    /// <summary>Crea/aggiorna lo staffista al login: nome + posizioni + LastLoginUtc (riattiva se disattivato).</summary>
    Task UpsertLoginAsync(int UserId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default);

    /// <summary>Staffisti attualmente attivi, ordinati per UserId.</summary>
    Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>UserId di tutti gli staffisti nel roster (per la verifica periodica).</summary>
    Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default);

    /// <summary>Aggiorna i dati verificati via API (rating/posizioni) e marca LastVerifiedUtc.
    /// <para><paramref name="displayName"/> è un RIPIEGO: riempie il nome solo se manca. Il nome buono lo
    /// porta il login (nome e cognome veri); qui arriva il solo nickname del token applicativo, e
    /// sovrascrivere farebbe oscillare l'elenco a ogni ri-verifica.</para></summary>
    Task UpdateVerifiedAsync(int UserId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default);

    /// <summary>Disattiva uno staffista che non è più staff IT (IsActive=false, LastVerifiedUtc aggiornato).</summary>
    Task DeactivateAsync(int UserId, CancellationToken ct = default);

    /// <summary>Risolve VID → DisplayName per un insieme di UserId (batch). Include anche gli ex-staff disattivati;
    /// i VID senza nome noto (o non nel roster) non compaiono nel dizionario.</summary>
    Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default);
}

/// <summary>Dati staff di un utente dalla sorgente esterna (oggi IVAO <c>/v2/users/{UserId}</c>, leggibili col token app).</summary>
public sealed record SourceUserStaff(
    int UserId,
    string? Nickname,
    string? AtcRating,
    string? DivisionId,
    bool IsStaff,
    IReadOnlyList<string> StaffPositionCodes);

/// <summary>Porta verso il singolo utente della sorgente esterna (oggi IVAO <c>/v2/users/{UserId}</c>).</summary>
public interface IUserDirectory
{
    /// <summary>Profilo (staff) dell'utente, o null se non risolvibile/errore.</summary>
    Task<SourceUserStaff?> GetUserAsync(int UserId, CancellationToken ct = default);
}

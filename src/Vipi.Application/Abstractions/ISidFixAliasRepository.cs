namespace Vipi.Application.Abstractions;

/// <summary>Alias prefisso-troncato → fix reale (es. "SIV" → "SOSIV") per i casi irregolari del sectorfile.</summary>
public sealed record SidFixAliasRow(int Id, string Prefix, string FixName);

/// <summary>Persistenza degli alias fix (globali). Impl. EF.</summary>
public interface ISidFixAliasRepository
{
    Task<IReadOnlyList<SidFixAliasRow>> ListAsync(CancellationToken ct = default);
    /// <summary>Mappa prefisso→fix (case-insensitive) per il parser.</summary>
    Task<IReadOnlyDictionary<string, string>> GetMapAsync(CancellationToken ct = default);
    /// <summary>Crea o aggiorna l'alias per il prefisso indicato.</summary>
    Task UpsertAsync(string prefix, string fixName, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

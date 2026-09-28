namespace Vipi.Application.Abstractions;

/// <summary>Alias prefisso-troncato → fix reale (es. "SIV" → "SOSIV") per i casi irregolari del sectorfile.</summary>
/// <param name="Icao">Lo scalo per cui vale; <c>null</c> = tutti gli scali (gli alias nati prima del 27 settembre
/// 2026, quando non c'era scelta).</param>
public sealed record SidFixAliasRow(int Id, string? Icao, string Prefix, string FixName);

/// <summary>
/// Persistenza degli alias fix. Impl. EF.
///
/// <para>🔴 <b>Un alias vale per lo scalo da cui nasce</b> (U-031, revisione totale 3). Era globale: la radice
/// risolta in uno scalo riscriveva, senza il segno «da verificare», il punto delle procedure di un altro — LUMA è
/// LUMAR a LIBD e LUMAV a LIPE, a 400 km — e lo creava qualunque Editor, anche di un altro ACC. Gli alias vecchi,
/// senza scalo, restano validi per tutti: sono stati scritti così, e toglierli spetta alla pagina Sorgenti.</para>
/// </summary>
public interface ISidFixAliasRepository
{
    Task<IReadOnlyList<SidFixAliasRow>> ListAsync(CancellationToken ct = default);

    /// <summary>Mappa prefisso→fix (case-insensitive) per il parser di UNO scalo: i suoi alias più quelli di tutti;
    /// a parità di prefisso vince il suo.</summary>
    Task<IReadOnlyDictionary<string, string>> GetMapAsync(string icao, CancellationToken ct = default);

    /// <summary>Crea o aggiorna l'alias per il prefisso indicato, per quello scalo.</summary>
    Task UpsertAsync(string icao, string prefix, string fixName, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}

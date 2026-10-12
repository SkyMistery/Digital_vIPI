using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Un NOTAM come lo dà la sorgente, già ripulito: codici maiuscoli, date in UTC, righe con <c>\n</c>.</summary>
/// <param name="InizioUtc"><c>null</c> = la sorgente non dice da quando: vale da sempre.</param>
/// <param name="FineUtc"><c>null</c> = senza scadenza.</param>
public sealed record NotamDaSorgente(
    int IvaoId,
    string Nome,
    DateTime? InizioUtc,
    DateTime? FineUtc,
    string Testo,
    bool Militare,
    IReadOnlyList<string> Aeroporti,
    IReadOnlyList<string> Centri,
    DateTime? AggiornatoUtc);

/// <summary>
/// Porta sulla sorgente dei NOTAM della divisione (carta <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>).
/// </summary>
public interface INotamSource
{
    /// <summary>
    /// <b>Tutti</b> i NOTAM della divisione, scaduti compresi. ⚠️ Lancia se l'elenco non si è letto (non-2xx, corpo
    /// che non è un elenco, voce senza id o senza numero): un elenco vuoto per sbaglio toglierebbe i NOTAM da ogni
    /// documento, e un giro fallito invece si ritenta.
    /// </summary>
    Task<IReadOnlyList<NotamDaSorgente>> GetDivisionNotamsAsync(CancellationToken ct = default);
}

/// <summary>Quel che un giro ha cambiato in archivio.</summary>
public sealed record NotamImportResult(int Creati, int Aggiornati, int Tolti);

/// <summary>L'archivio dei NOTAM: lo specchio dell'elenco della sorgente.</summary>
public interface INotamStore
{
    /// <summary>Tutte le righe, senza tracciamento.</summary>
    Task<IReadOnlyList<Notam>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Porta l'archivio a coincidere con <paramref name="arrivati"/>: crea chi manca, aggiorna chi è cambiato,
    /// toglie chi la sorgente non elenca più (cancellato dallo staff: deve sparire anche dai documenti).
    /// </summary>
    Task<NotamImportResult> SincronizzaAsync(IReadOnlyList<NotamDaSorgente> arrivati, DateTime adessoUtc,
                                             CancellationToken ct = default);
}

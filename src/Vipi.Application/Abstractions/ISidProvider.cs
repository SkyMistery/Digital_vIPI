using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Procedura prelevata dalla sorgente (sectorfile), già estratta e col punto completato. DTO neutro (ADR-0006).</summary>
public sealed record SourceProcedure(
    string Icao,
    string? Runway,
    string Fix,               // punto della procedura completato (o prefisso grezzo se NeedsFixReview)
    string Name,              // codice grezzo (es. "ALAX7G", "SOS5A-ESI8H", "ELKA3A")
    string? Transition,       // fix di transition (pieno), se presente
    string? Type,             // "RNAV" / "CONV"
    string StableKey,         // identità stabile (ICAO|fix|lettera|transition|pista), esclusa la revisione numerica
    bool NeedsFixReview,      // fix non risolto automaticamente → da completare a mano
    ProcedureKind Kind = ProcedureKind.Sid);   // ⚠️ in coda e col default: le SID esistenti si costruiscono com'erano

/// <summary>Porta neutra: fornisce le SID di un aeroporto dalla sorgente esterna (impl. GitHub/Aurora in Infrastructure).</summary>
public interface ISidProvider
{
    /// <summary>SID dell'aeroporto dalla sorgente. Vuoto se la sorgente non ha il file o non è raggiungibile.</summary>
    Task<IReadOnlyList<SourceProcedure>> GetSidsAsync(string icao, CancellationToken ct = default);
}

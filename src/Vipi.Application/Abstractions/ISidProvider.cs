namespace Vipi.Application.Abstractions;

/// <summary>SID prelevata dalla sorgente (sectorfile), già estratta e col fix completato. DTO neutro (ADR-0006).</summary>
public sealed record SourceSid(
    string Icao,
    string? Runway,
    string Fix,               // fix di partenza completato (o prefisso grezzo se NeedsFixReview)
    string Name,              // codice SID grezzo (es. "ALAX7G", "SOS5A-ESI8H")
    string? Transition,       // fix di transition (pieno), se presente
    string? Type,             // "RNAV" / "CONV"
    string StableKey,         // identità stabile (ICAO|fix|lettera|transition|pista), esclusa la revisione numerica
    bool NeedsFixReview);     // fix non risolto automaticamente → da completare a mano

/// <summary>Porta neutra: fornisce le SID di un aeroporto dalla sorgente esterna (impl. GitHub/Aurora in Infrastructure).</summary>
public interface ISidProvider
{
    /// <summary>SID dell'aeroporto dalla sorgente. Vuoto se la sorgente non ha il file o non è raggiungibile.</summary>
    Task<IReadOnlyList<SourceSid>> GetSidsAsync(string icao, CancellationToken ct = default);
}

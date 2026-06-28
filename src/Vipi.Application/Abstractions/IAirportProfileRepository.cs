using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Persistenza del profilo strutturato dell'aeroporto (TL, piste, regole, SID, link-frequenze) e
/// rigenerazione del documento vIPI aeroporto da esse. Le scritture per-area sostituiscono l'intera
/// lista per l'aeroporto (l'editor invia la lista completa); il merge da IVAO è invece mirato.
/// </summary>
public interface IAirportProfileRepository
{
    /// <summary>Carica il profilo completo (entità + frequenze proprie dai settori + link risolti). null = ICAO non assegnato.</summary>
    Task<AirportProfileData?> LoadAsync(string icao, CancellationToken ct = default);

    /// <summary>Codice ACC dell'aeroporto (per la guardia di autorizzazione). null = ICAO inesistente.</summary>
    Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default);

    /// <summary>Tutte le frequenze nel DB (per il picker di link), con ICAO/callsign del settore sorgente.</summary>
    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default);
    Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default);
    Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default);
    Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default);
    Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default);

    /// <summary>
    /// Merge da IVAO: imposta TA/ATIS, upsert piste per ident (sovrascrive Length/Bearing, preserva le colonne
    /// editoriali), e se non ci sono TL le inizializza con la tabella standard. Non tocca regole/SID/link.
    /// </summary>
    Task MergeFromSourceAsync(string icao, int? transitionAltitude, string? atisFrequency,
        IReadOnlyList<(string Ident, int? LengthM, int? Bearing)> runways, CancellationToken ct = default);

    /// <summary>Rigenera in-place le sezioni gestite del documento dell'aeroporto dalle entità, preservando le altre. Ritorna l'id documento.</summary>
    Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default);
}

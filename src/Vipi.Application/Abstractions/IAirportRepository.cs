using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Persistenza del profilo strutturato dell'aeroporto (TL, piste, regole, SID, link-frequenze) e
/// rigenerazione del documento vIPI aeroporto da esse. Le scritture per-area sostituiscono l'intera
/// lista per l'aeroporto (l'editor invia la lista completa); il merge da IVAO è invece mirato.
/// </summary>
public interface IAirportRepository
{
    /// <summary>Carica il profilo completo (entità + frequenze proprie dai settori + link risolti). null = ICAO non assegnato.</summary>
    Task<AirportData?> LoadAsync(string icao, CancellationToken ct = default);

    /// <summary>Codice ACC dell'aeroporto (per la guardia di autorizzazione). null = ICAO inesistente.</summary>
    Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default);

    /// <summary>Tutti i settori con frequenza nel DB (per il picker di link), con ICAO/callsign.</summary>
    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default);
    Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default);
    Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default);
    Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default);
    /// <summary>Salva le sole SID MANUALI dell'aeroporto (IsImported=false): sostituisce l'intera lista manuale, non tocca le importate.</summary>
    Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default);

    /// <summary>Merge SID importate: rimuove le sole righe importate precedenti e inserisce le nuove, riapplicando
    /// Priority e ForcePublished per StableKey. Le righe manuali restano intatte.</summary>
    Task ReplaceImportedSidsAsync(string icao, IReadOnlyList<ImportedSid> rows, string airacCycle, CancellationToken ct = default);

    /// <summary>Aggiorna i campi editabili di UNA riga SID importata (priorità, forzatura pubblicazione, fix risolto a mano).</summary>
    Task UpdateImportedSidAsync(int sidId, int? priority, bool forcePublished, string? resolvedFix, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
    Task SaveExtraSectionsAsync(string icao, IReadOnlyList<ExtraSectionRow> rows, CancellationToken ct = default);

    /// <summary>
    /// Merge da IVAO: imposta TA, upsert piste per ident (sovrascrive Length/Bearing, preserva le colonne
    /// editoriali), e se non ci sono TL le inizializza con la tabella standard. Non tocca regole/SID/link.
    /// L'ATIS non è più qui: è una frequenza del catalogo AirportSector.
    /// </summary>
    Task MergeFromSourceAsync(string icao, int? transitionAltitude,
        IReadOnlyList<(string Ident, int? LengthM, int? Bearing)> runways, CancellationToken ct = default);

    /// <summary>Rigenera in-place le sezioni gestite del documento dell'aeroporto dalle entità, preservando le altre. Ritorna l'id documento.</summary>
    Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default);

    /// <summary>RenderMode della sezione SID nel documento corrente (doc 10 §S4c). Default <see cref="RenderMode.Live"/>
    /// se il documento/sezione non esistono ancora.</summary>
    Task<RenderMode> GetSidsRenderModeAsync(string icao, CancellationToken ct = default);

    /// <summary>Imposta il RenderMode della sezione SID del documento corrente (doc 10 §S4c). Preservato dai rebuild.
    /// No-op se il documento/sezione non esistono ancora (la sezione nasce al primo rebuild).</summary>
    Task SetSidsRenderModeAsync(string icao, RenderMode mode, CancellationToken ct = default);
}

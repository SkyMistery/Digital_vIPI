using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// La sola LETTURA del profilo strutturato dell'aeroporto. Esiste separata da <see cref="IAirportRepository"/>
/// perché è tutto ciò che serve a chi ne <b>deriva una vista</b> (la pagina, la cattura di release): quei due non
/// devono poter scrivere, e non devono conoscere le altre sedici operazioni del repository.
/// <para>Non è un secondo modello: l'implementazione resta una sola, ed è il repository stesso.</para>
/// </summary>
public interface IAirportProfileReader
{
    /// <summary>Carica il profilo completo (entità + frequenze proprie dai settori + link risolti). null = ICAO non assegnato.</summary>
    Task<AirportData?> LoadAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Piste e regole-pista di PIÙ aeroporti insieme: quel poco che serve a dire quale pista è in uso, e
    /// nient'altro.
    ///
    /// <para><b>Perché esiste.</b> L'elenco degli aeroporti di una ACC mostra la pista consigliata accanto
    /// a ogni scalo, e per calcolarla chiamava <see cref="LoadAsync"/> una volta per aeroporto — cioè
    /// caricava il profilo INTERO (livelli di transizione, SID, link-frequenze, che quell'elenco non
    /// guarda) con <b>otto query a testa, in fila</b>. Contate il 27 agosto 2026 su un ACC con tre
    /// aeroporti pubblicati: 36 query per una pagina che ne mostra tre righe. Su una ACC con quindici
    /// diventavano centoventi andate e ritorno, una dietro l'altra.</para>
    ///
    /// <para>⚠️ Restituisce <b>solo</b> piste e regole. Chi avesse bisogno d'altro non allarghi questa: ne
    /// faccia un'altra, o torni a <see cref="LoadAsync"/>. Il valore di questo metodo è tutto in quello
    /// che NON legge.</para>
    /// </summary>
    /// <returns>ICAO → piste e regole. Gli ICAO senza profilo semplicemente non compaiono.</returns>
    Task<IReadOnlyDictionary<string, PisteDiAeroporto>> ListRunwayDataAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default);
}

/// <summary>Le due liste che bastano a scegliere la pista in uso. Vedi <see cref="IAirportProfileReader.ListRunwayDataAsync"/>.</summary>
public sealed record PisteDiAeroporto(IReadOnlyList<RunwayRow> Runways, IReadOnlyList<RunwayRuleRow> Rules);

/// <summary>
/// Persistenza del profilo strutturato dell'aeroporto (TL, piste, regole, SID, link-frequenze) e nascita del
/// documento vIPI d'aeroporto. Le scritture per-area sostituiscono l'intera lista per l'aeroporto (l'editor invia
/// la lista completa); il merge da IVAO è invece mirato.
/// </summary>
public interface IAirportRepository : IAirportProfileReader
{
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

    /// <summary>Aggiorna i campi editabili di UNA riga SID importata: priorità, forzatura pubblicazione, fix risolto a
    /// mano e gli arricchimenti editoriali (initial climb, CAT, WTC, condition) sovrapposti alla riga di sorgente.</summary>
    Task UpdateImportedSidAsync(int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);

    /// <summary>
    /// Merge da IVAO: imposta TA, upsert piste per ident (sovrascrive Length/Bearing, preserva le colonne
    /// editoriali), e se non ci sono TL le inizializza con la tabella standard. Non tocca regole/SID/link.
    /// L'ATIS non è più qui: è una frequenza del catalogo AirportSector.
    /// </summary>
    Task MergeFromSourceAsync(string icao, int? transitionAltitude,
        IReadOnlyList<(string Ident, int? LengthM, int? Bearing)> runways, CancellationToken ct = default);

    /// <summary>
    /// Idempotente: garantisce che l'aeroporto abbia il suo documento (<c>Airport.DocumentId</c>) con le sezioni del
    /// profilo <see cref="SectionProfile.Airport"/>, e riallinea i settori dello scalo a quel documento. Ritorna
    /// l'id documento.
    /// <para>
    /// ⚠️ Non «rigenera» più niente: fino alla carta 2026-08-26 questo metodo <b>cuoceva</b> le sezioni — le
    /// cancellava riconoscendole per titolo e le riscriveva come tabelle Markdown. Era il motivo per cui l'ordine,
    /// il «nascondi» e le sotto-sezioni dell'aeroporto non sopravvivevano: quello stato sta sulla sezione, e la
    /// sezione veniva distrutta. Ora il corpo delle sezioni fisse si deriva a view-time dalle tabelle del profilo.
    /// </para>
    /// </summary>
    Task<int> EnsureDocumentAsync(string icao, CancellationToken ct = default);

    /// <summary>Id del Document proiettato dell'aeroporto (via settori d'aeroporto con <c>DocumentId</c>), o null se non ancora generato.</summary>
    Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default);
}

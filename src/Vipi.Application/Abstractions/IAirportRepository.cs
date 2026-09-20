using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

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

    /// <summary>
    /// Tutti i settori col loro nominativo radio, <b>frequenza o no</b>: è la domanda dei riferimenti
    /// <c>[[ATC …]]</c>. Vedi <see cref="EnteRow"/> per perché non è <see cref="ListLinkableFrequenciesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EnteRow>> ListSectorCallsignsAsync(CancellationToken ct = default);

    Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default);

    /// <summary>Scrive la stazione METAR di riferimento dello scalo; <c>null</c> = il suo ICAO.</summary>
    Task SetMetarStationAsync(string icao, string? station, CancellationToken ct = default);
    Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default);
    Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default);
    Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default);

    /// <summary>Scrive (o cancella, con <c>null</c>) i minimi LVP dello scalo: al massimo una riga.</summary>
    Task SaveLvpAsync(string icao, LvpRow? row, CancellationToken ct = default);
    /// <summary>Salva le sole procedure MANUALI dello scalo <b>di quel verso</b> (IsImported=false):
    /// sostituisce l'intera lista manuale di quel verso, non tocca le importate né l'altro verso.</summary>
    Task SaveSidsAsync(string icao, ProcedureKind kind, IReadOnlyList<SidRow> rows, CancellationToken ct = default);

    /// <summary>Merge delle procedure importate <b>di quel verso</b>: rimuove le sole righe importate precedenti
    /// dello stesso <paramref name="kind"/> e inserisce le nuove, riapplicando Priority e ForcePublished per
    /// StableKey. Le righe manuali restano intatte, e le procedure dell'altro verso non si toccano — importare
    /// gli arrivi non deve poter cancellare le partenze.</summary>
    Task ReplaceImportedProceduresAsync(string icao, ProcedureKind kind, IReadOnlyList<ImportedProcedure> rows,
        string airacCycle, CancellationToken ct = default);

    /// <summary>Aggiorna i campi editabili di UNA riga SID importata: priorità, forzatura pubblicazione, fix risolto a
    /// mano e gli arricchimenti editoriali (initial climb, CAT, WTC, condition) sovrapposti alla riga di sorgente.</summary>
    Task UpdateImportedSidAsync(int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default);

    /// <summary>Nasconde (o rimostra) al pubblico le SID IMPORTATE indicate, dello scalo indicato. Le manuali
    /// passano da <see cref="SaveSidsAsync"/>, che le riscrive tutte. Ritorna quante righe ha toccato.</summary>
    Task<int> SetImportedSidsHiddenAsync(string icao, IReadOnlyCollection<int> sidIds, bool hidden, CancellationToken ct = default);

    /// <summary>Punto e transition corretti a mano su UNA SID importata. Null o vuoto = torna a valere la sorgente.</summary>
    Task SetImportedSidOverridesAsync(string icao, int sidId, string? fixOverride, string? transitionOverride, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);

    /// <summary>
    /// Merge da IVAO: imposta TA, riconcilia le piste per ident (sovrascrive Length/Bearing/soglia, preserva
    /// le colonne editoriali), e se non ci sono TL le inizializza con la tabella standard. Non tocca
    /// regole/SID/link. L'ATIS non è più qui: è una frequenza del catalogo AirportSector.
    ///
    /// <para><b>Riconciliare, non solo aggiungere.</b> Fino al 4 settembre 2026 questo era un add-or-update:
    /// le piste che la sorgente smetteva di nominare restavano in archivio per sempre. Quando IVAO ha
    /// ri-denominato Rimini (13/31 → 12/30, deriva magnetica) LIPR si è ritrovato QUATTRO piste, le due
    /// morte davanti alle due vive. Ora le orfane senza lavoro editoriale si tolgono; quelle con TORA/LDA
    /// restano e tornano indietro in <see cref="RunwayMergeOutcome.OrphansWithData"/>.</para>
    ///
    /// <para>⚠️ <paramref name="runways"/> <b>vuoto vuol dire «nessun cambio»</b>, mai «l'aeroporto non ha
    /// più piste»: è così che <c>SourceMergeInputs</c> esprime la categoria esclusa dalla policy, ed è anche
    /// ciò che resta di una fetch andata a vuoto (l'import piste è best-effort silenzioso: IVAO 4xx → lista
    /// vuota, nessun errore). Con la lista vuota le piste non si toccano.</para>
    /// </summary>
    Task<RunwayMergeOutcome> MergeFromSourceAsync(string icao, int? transitionAltitude,
        IReadOnlyList<SourceRunway> runways, CancellationToken ct = default);

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

    /// <summary>
    /// Anagrafica militare dello scalo e i due legami documentali. null = ICAO inesistente.
    /// <para>⚠️ Legge il DATABASE e non la cache delle stazioni (<c>IStationResolver.Airport</c>): quella è
    /// <c>scoped</c>, cioè vive quanto il CIRCUITO, e una guardia che decide se un documento può nascere non
    /// può rispondere su un'anagrafica vecchia di ore. La cache resta buona per le testate, che se sbagliano
    /// mostrano un'etichetta di troppo.</para>
    /// </summary>
    Task<AirportMilitaryState?> GetMilitaryStateAsync(string icao, CancellationToken ct = default);
}

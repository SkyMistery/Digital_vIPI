using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una riga dell'elenco dei vSOP militari.
/// </summary>
/// <param name="Icao">Il campo.</param>
/// <param name="Name">Il suo nome.</param>
/// <param name="AccCode">L'ACC che lo governa: l'elenco si raggruppa per questo.</param>
/// <param name="SoloMilitare">Nessun traffico civile (Aviano, Ghedi, Decimomannu…). ⚠️ È il giudizio di un
/// amministratore, non il campo <c>military</c> della sorgente — che è vero anche su Linate e Ciampino.</param>
/// <param name="DocumentId">Il documento militare, se esiste. null = c'è solo il candidato.</param>
/// <param name="Pubblicato">Se ha una release effettiva: è il gate dell'elenco pubblico.</param>
/// <param name="HaCivile">La vIPI CIVILE dello scalo esiste (anche solo in bozza). ⚠️ Sui campi MISTI è il
/// prerequisito del vSOP militare (carta §5-bis): senza, l'elenco offrirebbe un tasto «Crea» che il servizio
/// rifiuta — e un tasto che fallisce sempre è peggio di un tasto che non c'è.</param>
public sealed record MilAirportRow(
    string Icao, string Name, string? AccCode, bool SoloMilitare, int? DocumentId, bool Pubblicato,
    bool HaCivile = false);

/// <summary>
/// L'elenco dei campi con un vSOP militare, e la creazione del primo (carta
/// <c>docs/feature/2026-08-27-vsop-militari.md</c> §5).
///
/// <para>
/// ⚠️ <b>Il catch-22 dell'ingresso.</b> L'elenco pubblico mostra solo ciò che ha una release effettiva,
/// quindi il <b>primo</b> documento non sarebbe raggiungibile da nessuna parte: non c'è, e per farlo
/// esistere bisognerebbe già poterci arrivare. È successo davvero con l'elenco APP. La risposta è la
/// stessa: allo <b>staff</b> l'elenco mostra anche i campi <i>senza</i> documento, con il tasto che lo crea.
/// </para>
/// </summary>
public interface IMilitaryDocumentService
{
    /// <summary>
    /// I campi da mostrare. Con <paramref name="perStaff"/> falso, solo quelli con un vSOP <b>pubblicato</b>;
    /// con vero, anche i candidati senza documento — che è ciò che permette di crearne uno.
    /// </summary>
    Task<IReadOnlyList<MilAirportRow>> ListAsync(bool perStaff, CancellationToken ct = default);

    /// <summary>
    /// Crea il vSOP militare di questo campo, se non c'è già; ritorna l'id del documento.
    ///
    /// <para>
    /// ⚠️ Nasce in <b>italiano</b> (carta §1d): la lingua sorgente è quella in cui si <i>redige</i>, non
    /// quella dei PDF di partenza. Un lettore inglese lo ottiene tradotto come qualunque altro documento.
    /// </para>
    /// <para>⚠️ Nasce in <b>bozza</b>, come le altre tre famiglie: un campo appena marcato non è ancora
    /// pubblico, e a pubblicarlo sarà una persona.</para>
    /// </summary>
    Task<int> CreaAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// L'id del vSOP militare di questo campo, o null se non c'è. ⚠️ Non lo crea: serve a chi apre la
    /// pagina in sola lettura, che un documento non deve poterlo far nascere passando di lì.
    /// </summary>
    Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Vero se questo campo ha un vSOP militare <b>pubblicato</b>: la domanda che si fa la pagina civile
    /// per decidere se mostrare il ponte verso l'altra edizione.
    /// <para>⚠️ Esiste per non far passare <see cref="ListAsync"/> di lì: quella legge tutti i campi
    /// militari e le loro release: trentaquattro righe più una query, su una delle pagine più aperte del
    /// sito, per rispondere a una domanda su UN aeroporto.</para>
    /// </summary>
    Task<bool> HasPublishedAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Vero se questo campo ha una vIPI <b>civile</b> pubblicata: la stessa domanda al contrario, e serve al
    /// ponte militare → civile.
    /// <para>
    /// ⚠️ <b>Il ponte va gated nei DUE versi.</b> Fino al 29 agosto 2026 lo era solo in uno: la pagina civile
    /// mostrava il collegamento al militare solo se esisteva, ma quella militare mandava al civile
    /// <b>sempre</b> — e sui campi solo militari (Aviano, Ghedi, Decimomannu, Rivolto), che sono proprio
    /// quelli con più probabilità di avere un vSOP, il civile non c'è: il lettore finiva su «documento non
    /// disponibile».
    /// </para>
    /// </summary>
    Task<bool> HasPublishedCivilAsync(string icao, CancellationToken ct = default);

    /// <summary>Le aree di lavoro scelte per questo campo (sezione <c>regulated</c>).</summary>
    Task<RegulatedSelection> GetRegulatedAsync(string icao, CancellationToken ct = default);

    /// <summary>Salva la scelta delle aree. ACC-gated come ogni scrittura sul documento.</summary>
    Task SaveRegulatedAsync(string icao, RegulatedSelection selection, CancellationToken ct = default);

    /// <summary>Le aree che l'ACC del campo elenca — il pool del picker.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string icao, CancellationToken ct = default);

    /// <summary>Le aree di ALTRI ACC: un campo militare vola anche fuori dal proprio centro.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Le aree scelte, risolte in vista con shape e descrizioni. ⚠️ È lo <b>stesso motore</b> della vIPI ACC
    /// e dell'APP: la selezione non sa da quale documento arriva, e riusarlo è ciò che rende «la mappa
    /// dell'AoR riusata» un fatto e non una dichiarazione.
    /// </summary>
    Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(
        RegulatedSelection selection, CancellationToken ct = default);
}

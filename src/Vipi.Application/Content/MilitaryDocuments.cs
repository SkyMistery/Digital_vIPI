using Vipi.Application.Abstractions;
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
/// <summary>
/// Lo stato dell'edizione <b>civile</b> di uno scalo, visto dal documento militare.
///
/// <para>⚠️ Le tre risposte si danno <b>insieme</b> perché insieme si decide che cosa mostrare, e chiederle
/// separatamente vorrebbe dire poterle vedere in tre istanti diversi: se il ponte verso il civile si
/// accende, e se invece manca qualcosa che <b>dovrebbe</b> esserci.</para>
/// </summary>
/// <param name="Esiste">La vIPI civile c'è, anche solo in <b>bozza</b>.</param>
/// <param name="Pubblicata">Ha una release effettiva: è il gate del ponte per il <b>pubblico</b>.</param>
/// <param name="SoloMilitare">Il campo non ha traffico civile: allora la vIPI civile <b>non deve</b>
/// esistere, e la sua assenza non è un difetto ma la regola (carta vSOP militari §5-bis).</param>
public sealed record CivilEdition(bool Esiste, bool Pubblicata, bool SoloMilitare);

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
    /// Lo stato della vIPI <b>civile</b> gemella: la stessa domanda al contrario, e serve al ponte
    /// militare → civile.
    /// <para>
    /// ⚠️ <b>Il ponte va gated nei DUE versi.</b> Fino al 29 agosto 2026 lo era solo in uno: la pagina civile
    /// mostrava il collegamento al militare solo se esisteva, ma quella militare mandava al civile
    /// <b>sempre</b> — e sui campi solo militari (Aviano, Ghedi, Decimomannu, Rivolto), che sono proprio
    /// quelli con più probabilità di avere un vSOP, il civile non c'è: il lettore finiva su «documento non
    /// disponibile».
    /// </para>
    /// <para>
    /// ⚠️ <b>Torna tre cose e non un sì/no</b>, perché «pubblicata» da sola non basta più: allo <b>staff</b>
    /// il ponte si accende anche su una <b>bozza</b> (il civile può essere appena nato), e su un campo
    /// <b>misto</b> l'assenza del civile è un <b>difetto da dire</b> — la guardia impedisce di crearne di
    /// nuovi, ma i vSOP nati prima del 29 agosto 2026 possono benissimo essere lì senza gemello.
    /// </para>
    /// </summary>
    Task<CivilEdition> GetCivilEditionAsync(string icao, CancellationToken ct = default);

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

    /// <summary>
    /// Le radioassistenze citate dalla sezione «Radioassistenze», <b>nell'ordine del documento</b> e con i
    /// valori presi dall'anagrafica di divisione. È la lettura dell'EDITOR: legge la versione di lavoro.
    /// </summary>
    Task<IReadOnlyList<NavaidRow>> GetNavaidsAsync(string icao, CancellationToken ct = default);

    /// <summary>Salva quali radioassistenze cita il documento, e in che ordine. ⚠️ Qui <b>non</b> si salvano
    /// i loro valori: quelli stanno nell'anagrafica, e ci si scrive con <see cref="INavaidCatalog"/> — se li
    /// copiassimo nel documento, la stessa radioassistenza direbbe due cose in due SOP.</summary>
    Task SaveNavaidsAsync(string icao, IReadOnlyList<NavaidKey> righe, CancellationToken ct = default);

    /// <summary>
    /// Le righe citate da un documento <b>mostrato</b> (pubblico, bozza o anteprima release), risolte
    /// sull'anagrafica. Gemella di <see cref="ResolveRegulatedAreasAsync"/> e per la stessa ragione: le
    /// identità le porta il documento, i valori i cataloghi correnti.
    /// </summary>
    Task<IReadOnlyList<NavaidRow>> ResolveNavaidsAsync(
        IReadOnlyList<NavaidKey> righe, CancellationToken ct = default);

    /// <summary>
    /// Come sopra, ma per una vista che può essere <b>congelata</b>: con <paramref name="useFrozen"/> si legge
    /// prima la fotografia della release, e solo in sua assenza si risolve dal vivo.
    /// <para>⚠️ È la differenza fra un documento e una pagina: una frequenza corretta oggi <b>non</b> deve
    /// cambiare un SOP pubblicato al ciclo scorso. Vale solo per il pubblico e per le anteprime di release —
    /// in bozza si guarda il dato di adesso, che è quel che si sta scrivendo.</para>
    /// </summary>
    Task<IReadOnlyList<NavaidRow>> ResolveNavaidsForViewAsync(
        string icao, IReadOnlyList<NavaidKey> righe, bool useFrozen, CancellationToken ct = default);

    /// <summary>Gli aeroporti alternati citati dal documento, risolti su archivio e anagrafica. Lettura
    /// dell'EDITOR: legge la versione di lavoro.</summary>
    Task<IReadOnlyList<MilDiversionView>> GetDiversionsAsync(string icao, CancellationToken ct = default);

    /// <summary>Salva le righe degli alternati: scali, radioassistenze citate, rilevamento e distanza.</summary>
    Task SaveDiversionsAsync(string icao, IReadOnlyList<MilDiversionPayload.Riga> righe,
        CancellationToken ct = default);

    /// <summary>Le righe di un documento <b>mostrato</b>, con la fotografia della release se c'è.</summary>
    Task<IReadOnlyList<MilDiversionView>> ResolveDiversionsForViewAsync(
        string icao, IReadOnlyList<MilDiversionPayload.Riga> righe, bool useFrozen,
        CancellationToken ct = default);
}

using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// L'unico motore di <b>eliminazione</b> del sistema: settori, aeroporti, ACC e documenti passano di qui.
///
/// <para><b>Due mosse e una promessa.</b> <see cref="AnteprimaAsync"/> dice cosa succederebbe;
/// <see cref="EliminaAsync"/> lo fa. La seconda <b>ricalcola</b> la prima e si ferma se nel frattempo
/// qualcosa è cambiato: fra lo schermo e il clic passa del tempo, e un altro amministratore può aver
/// aggiunto un accordo o pubblicato un documento. Un'anteprima che non viene riverificata è una promessa
/// fatta sul passato.</para>
///
/// <para><b>Perché uno solo.</b> Fino al 26 agosto 2026 l'eliminazione viveva in tre posti che si
/// somigliavano: due guardie nel repository della struttura, la rimozione degli orfani in Struttura, il
/// cestino degli aeroporti che chiedeva conferma senza sapere se l'operazione fosse possibile. Tre motori
/// che si somigliano sono tre racconti che iniziano a divergere — la regola §1 del FEATURE-PROCESS dice di
/// estendere o sostituire, mai affiancare.</para>
///
/// <para>Le politiche stanno in <see cref="DeletionRules"/>, senza IO e senza database. Qui c'è solo il
/// giro: autorizzazione, lettura dei fatti, il metro delle due chiamate, l'esecuzione, e la segnalazione
/// dei documenti che restano da rileggere.</para>
/// </summary>
public interface IDeletionService
{
    /// <summary>Cosa succederebbe. Lancia <see cref="ValidationException"/> se il bersaglio non esiste.</summary>
    Task<DeletionPlan> AnteprimaAsync(DeletionTarget bersaglio, CancellationToken ct = default);

    /// <summary>
    /// Chiede alla sorgente, <b>adesso</b>, se il bersaglio esiste ancora, e ricalcola il piano col verdetto.
    ///
    /// <para>Serve quando a trattenere è D8 — «la sorgente la manda ancora», o «non c'è ancora abbastanza
    /// storia». Sono due affermazioni sul <i>silenzio</i>, e il silenzio si può interrompere chiedendo. Il
    /// verdetto <c>Presente</c> non è un fallimento: è la risposta che oggi si aspetta due giri per avere.</para>
    /// </summary>
    Task<DeletionProbeOutcome> VerificaAllaSorgenteAsync(DeletionTarget bersaglio, CancellationToken ct = default);

    /// <summary>
    /// Esegue, in una transazione. Rifiuta con <see cref="ValidationException"/> se il piano ricalcolato ha
    /// anche un solo blocco: l'elenco dei blocchi è il messaggio.
    /// </summary>
    /// <param name="conVerificaAllaSorgente">
    /// Rifai la domanda alla sorgente <b>qui dentro</b>, e applica il verdetto al piano.
    ///
    /// <para>⚠️ È un <i>ordine di chiedere</i>, non una risposta: chi chiama non può passare un verdetto già
    /// preso. È la stessa ragione per cui il piano si ricalcola invece di fidarsi di quello mostrato — fra lo
    /// schermo e il clic passa del tempo, e una prova di dieci minuti fa è una promessa fatta sul passato.
    /// Al momento del <c>DELETE</c> la prova è di quell'istante, o non c'è.</para>
    /// </param>
    Task<DeletionPlan> EliminaAsync(DeletionTarget bersaglio, bool conVerificaAllaSorgente = false,
        CancellationToken ct = default);
}

/// <summary>Il verdetto della sorgente e il piano che ne è uscito: la finestra li mostra insieme.</summary>
public sealed record DeletionProbeOutcome(SourceProbeResult Prova, DeletionPlan Piano);

/// <inheritdoc cref="IDeletionService"/>
public sealed class DeletionService : IDeletionService
{
    private readonly IDeletionRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IImportStateStore _stati;
    private readonly IDocumentImpactService _impatti;
    private readonly IDocumentAdminService _documenti;
    private readonly IEditorTaskService _incarichi;
    private readonly ISourcePresenceProbe _sorgente;

    public DeletionService(IDeletionRepository repo, IEditAuthorizationService authz, IImportStateStore stati,
        IDocumentImpactService impatti, IDocumentAdminService documenti, IEditorTaskService incarichi,
        ISourcePresenceProbe sorgente)
    {
        _repo = repo;
        _authz = authz;
        _stati = stati;
        _impatti = impatti;
        _documenti = documenti;
        _incarichi = incarichi;
        _sorgente = sorgente;
    }

    public async Task<DeletionPlan> AnteprimaAsync(DeletionTarget bersaglio, CancellationToken ct = default)
    {
        // Eliminare è un atto d'archivio, non di redazione: lo fa un amministratore. È la stessa riga che
        // separa «rimuovi» da «riaggancia» nella casella degli impatti.
        _authz.EnsureAtLeast(VipiRole.Editor);
        return await PianoAsync(bersaglio, provaDiAssenza: false, ct);
    }

    public async Task<DeletionProbeOutcome> VerificaAllaSorgenteAsync(DeletionTarget bersaglio,
        CancellationToken ct = default)
    {
        // Interrogare la sorgente costa una chiamata di rete a ogni clic: la fa chi può anche eliminare.
        _authz.EnsureAtLeast(VipiRole.Editor);

        var prova = await ChiediAllaSorgenteAsync(bersaglio, ct);
        return new DeletionProbeOutcome(prova, await PianoAsync(bersaglio, prova.ProvaLAssenza, ct));
    }

    public async Task<DeletionPlan> EliminaAsync(DeletionTarget bersaglio, bool conVerificaAllaSorgente = false,
        CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        // La prova si rifà QUI. Quella mostrata nella finestra ha autorizzato il tasto, non il DELETE: fra le
        // due c'è il tempo che l'utente ha impiegato a leggere, e in quel tempo un import può aver rimesso in
        // archivio ciò che la sorgente aveva appena smesso di mandare.
        var prova = conVerificaAllaSorgente ? await ChiediAllaSorgenteAsync(bersaglio, ct) : null;

        var piano = await PianoAsync(bersaglio, prova?.ProvaLAssenza ?? false, ct);
        if (!piano.Eliminabile)
            throw new ValidationException(
                Lingua("Non si può eliminare: ", "It cannot be deleted: ")
                + string.Join("; ", piano.Blocca.Select(b => b.Testo)) + ".");

        // Il documento ha già il suo percorso: toglie anche le release (che non hanno FK e non cascadano),
        // controlla il lock di editing e scrive l'audit col titolo dentro. Riscriverlo qui vorrebbe dire
        // avere due modi di cancellare un documento, e uno dei due sarebbe sbagliato.
        if (piano.Azioni.DocumentoDaEliminare is int docId)
        {
            var doc = (await _documenti.ListAsync(ct)).FirstOrDefault(d => d.DocumentId == docId);
            if (doc is not null && !string.IsNullOrWhiteSpace(doc.ReleaseKey))
            {
                await _documenti.DeleteAsync(new ManagedDocRef(doc.Kind, doc.ReleaseKey, doc.DocumentId), ct);
                return piano;
            }

            // ⚠️ Senza chiave. Non è un errore da rifiutare: è il documento che ha più bisogno di questo
            // tasto, ed è l'unico che la via dei gestiti NON sa cancellare — la sua autorizzazione parte da
            // «di quale ACC è questo documento?», e per un documento senza chiave la risposta non c'è:
            // `EnsureCanEditAsync` risponde «Documento inesistente» a un documento che esiste eccome.
            //
            // Come ci si finisce, misurato sul vipi.db vero: una «vIPI Roma» scritta il 10 luglio su
            // LIRR_ES_CTR, che allora era una radice. Un import l'ha infilato sotto LIRR_SU_CTR, la vIPI di
            // ACC vuole un CTR RADICE, e il documento è scivolato nel catch-all dell'aeroporto — senza un
            // aeroporto, quindi con la chiave vuota. Da quel giorno: in elenco senza nome di scalo, non
            // pubblicabile, nascosto alla tendina degli incarichi, e non cancellabile.
            //
            // Qui l'autorizzazione l'ha già data EliminaAsync con EnsureAdmin: è un atto d'archivio.
            await _repo.DeleteUnmanagedDocumentAsync(docId, _authz.CurrentUserId ?? 0, ct);
            return piano;
        }

        // Le tracce della prova finiscono nell'audit insieme all'atto: senza, il registro direbbe che il 26
        // agosto qualcuno ha cancellato un settore che la regola dei due giri proteggeva, e non perché.
        await _repo.ApplyAsync(piano.Azioni, _authz.CurrentUserId ?? 0,
            prova?.ProvaLAssenza == true ? prova.Tracce : null, ct);

        // I documenti che restano a raccontare qualcosa che non c'è più. La segnalazione parte DOPO
        // l'eliminazione ma con gli Id raccolti PRIMA: un istante dopo il DELETE nessun reverse-lookup
        // troverebbe più il legame appena reciso.
        if (piano.Azioni.DocumentiDaMarcare.Count > 0)
            await _impatti.RaiseForDocumentsAsync(ImpactKind.SectorDetached, piano.Azioni.DocumentiDaMarcare,
                sourceKey: piano.Titolo, args: new[] { piano.Titolo }, ct);

        // Un'area eliminata a mano lascia gli stessi documenti scoperti che lascerebbe una potata
        // dall'import: stesso rilievo, stessa frase — «AreaGone» — e non un secondo modo di dirlo.
        // ⚠️ Il reverse-lookup gira ADESSO, e l'area non c'è più: gli argomenti li porta il piano, che li
        // ha raccolti quando ancora esisteva.
        if (piano.Azioni.AreaDaEliminare is not null)
            await _impatti.RaiseForAreaAsync(ImpactKind.AreaGone, piano.Azioni.AreaDaEliminare, piano.Titolo, ct);

        return piano;
    }

    /// <summary>
    /// Traduce il bersaglio nell'indirizzo che la <b>sorgente</b> conosce, e chiede. I bersagli che nessuna
    /// sorgente rivendica — un documento, un candidato confinante, un'area — non hanno niente da chiedere:
    /// rispondono «non si sa», che per loro non toglie niente perché D8 non li tocca.
    /// </summary>
    private async Task<SourceProbeResult> ChiediAllaSorgenteAsync(DeletionTarget b, CancellationToken ct)
    {
        switch (b.Kind)
        {
            case DeletionTargetKind.Sector:
            {
                var id = b.Id > 0
                    ? b.Id
                    : await _repo.SectorIdByCallsignAsync(b.Code ?? "", ct) ?? throw Inesistente("Settore");
                var f = await _repo.SectorFactsAsync(id, ct) ?? throw Inesistente("Settore");

                // Una riga aggiunta a mano la sorgente non l'ha mai mandata: chiederle se c'è ancora è una
                // domanda senza senso, e la risposta «non c'è» non proverebbe niente. D8 già non la tocca.
                if (!f.IsProjected || f.CatalogoManuale)
                    return SourceProbeResult.NonSiSa(
                        Lingua($"{f.Callsign} è stato aggiunto a mano: nessuna sorgente lo rivendica",
                               $"{f.Callsign} was added by hand: no source claims it"),
                        Lingua("nessuna chiamata: riga di catalogo manuale",
                               "no call: manual catalogue row"));

                return await _sorgente.ChiediAsync(f.Kind == SectorKind.Airport
                    ? new SourceProbeTarget(SourceProbeKind.AirportSector, f.Callsign, f.AirportIcao)
                    : new SourceProbeTarget(SourceProbeKind.AccSector, f.Callsign, f.AccCode), ct);
            }

            case DeletionTargetKind.Airport:
            {
                var f = await _repo.AirportFactsAsync(b.Id, ct) ?? throw Inesistente("Aeroporto");
                return await _sorgente.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, f.Icao), ct);
            }

            case DeletionTargetKind.Acc:
            {
                var f = await _repo.AccFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("ACC");
                return await _sorgente.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Acc, f.Code), ct);
            }

            default:
                return SourceProbeResult.NonSiSa(
                    Lingua("non è la sorgente a decidere di questo: è roba nostra",
                           "the source does not decide about this: it is ours"),
                    Lingua("nessuna chiamata: bersaglio senza sorgente",
                           "no call: target without a source"));
        }
    }

    private async Task<DeletionPlan> PianoAsync(DeletionTarget b, bool provaDiAssenza, CancellationToken ct)
    {
        switch (b.Kind)
        {
            case DeletionTargetKind.Sector:
            {
                // Il bersaglio può arrivare per Id o per callsign: l'albero della Struttura conosce i
                // callsign (le sue righe sono di catalogo), le altre pagine gli Id dei settori.
                var id = b.Id > 0
                    ? b.Id
                    : await _repo.SectorIdByCallsignAsync(b.Code ?? "", ct) ?? throw Inesistente("Settore");
                var f = await _repo.SectorFactsAsync(id, ct) ?? throw Inesistente("Settore");
                // Il penultimo giro che conta è quello della sorgente GIUSTA: gli aeroporti per una
                // postazione di scalo, le ACC altrimenti. Sono due giri con due cadenze, e col timbro
                // sbagliato si vieta un'eliminazione lecita o se ne permette una prematura.
                var categoria = f.Kind == SectorKind.Airport
                    ? ImportCategories.AirportSector
                    : ImportCategories.Acc;
                return DeletionRules.PerSettore(f, await _stati.GetPrevSuccessAsync(categoria, ct), provaDiAssenza);
            }

            case DeletionTargetKind.Airport:
                return DeletionRules.PerAeroporto(
                    await _repo.AirportFactsAsync(b.Id, ct) ?? throw Inesistente("Aeroporto"),
                    await _stati.GetPrevSuccessAsync(ImportCategories.AirportDirectory, ct),
                    await _stati.GetPrevSuccessAsync(ImportCategories.AirportSector, ct),
                    provaDiAssenza);

            case DeletionTargetKind.Neighbour:
                return DeletionRules.PerConfinante(
                    await _repo.NeighbourFactsAsync(b.Id, ct) ?? throw Inesistente("Candidato confinante"));

            case DeletionTargetKind.Area:
                return DeletionRules.PerArea(
                    await _repo.AreaFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("Area regolamentata"));

            case DeletionTargetKind.Acc:
                return DeletionRules.PerAcc(
                    await _repo.AccFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("ACC"),
                    await _stati.GetPrevSuccessAsync(ImportCategories.Acc, ct),
                    provaDiAssenza);

            default:
            {
                var f = await _repo.DocumentFactsAsync(b.Id, ct) ?? throw Inesistente("Documento");
                // Le pubblicazioni si contano dal bersaglio di release, non dal documento: le DocRelease
                // stanno sotto (tipo, chiave) e il documento non le conosce.
                var gestito = (await _documenti.ListAsync(ct)).FirstOrDefault(d => d.DocumentId == b.Id);
                if (gestito is not null)
                    f = f with
                    {
                        Release = await _repo.ReleaseCountAsync(gestito.ReleaseTarget, gestito.ReleaseKey, ct),
                        // Gli incarichi puntano al documento per (tipo, chiave), come le release: si
                        // trovano dalla stessa coppia, e per la stessa ragione non cascadano da soli.
                        Incarichi = (await _incarichi.ListAllAsync(ct))
                            .Where(t => t.TargetType == gestito.ReleaseTarget
                                        && string.Equals(t.TargetKey, gestito.ReleaseKey, StringComparison.OrdinalIgnoreCase))
                            .Select(t => t.Title)
                            .ToList(),
                    };
                return DeletionRules.PerDocumento(f);
            }
        }
    }

    /// <summary>
    /// «X inesistente», dove X è il TIPO di bersaglio. ⚠️ Il tipo arriva come chiave neutra
    /// (<c>"Settore"</c>, <c>"Aeroporto"</c>, <c>"ACC"</c>) e si traduce QUI: era il nome già scritto in
    /// italiano, e in inglese avrebbe dato «Settore does not exist».
    /// </summary>
    private static ValidationException Inesistente(string cosa) => new(Lingua(
        $"{Tipo(cosa, inglese: false)} inesistente.",
        $"{Tipo(cosa, inglese: true)} does not exist."));

    private static string Tipo(string chiave, bool inglese) => chiave switch
    {
        "Settore" => inglese ? "Sector" : "Settore",
        "Aeroporto" => inglese ? "Airport" : "Aeroporto",
        _ => chiave,   // «ACC» è uguale in tutte e due
    };
}

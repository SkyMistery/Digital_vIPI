using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

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
    /// Esegue, in una transazione. Rifiuta con <see cref="ValidationException"/> se il piano ricalcolato ha
    /// anche un solo blocco: l'elenco dei blocchi è il messaggio.
    /// </summary>
    Task<DeletionPlan> EliminaAsync(DeletionTarget bersaglio, CancellationToken ct = default);
}

/// <inheritdoc cref="IDeletionService"/>
public sealed class DeletionService : IDeletionService
{
    private readonly IDeletionRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IImportStateStore _stati;
    private readonly IDocumentImpactService _impatti;
    private readonly IDocumentAdminService _documenti;
    private readonly IEditorTaskService _incarichi;

    public DeletionService(IDeletionRepository repo, IEditAuthorizationService authz, IImportStateStore stati,
        IDocumentImpactService impatti, IDocumentAdminService documenti, IEditorTaskService incarichi)
    {
        _repo = repo;
        _authz = authz;
        _stati = stati;
        _impatti = impatti;
        _documenti = documenti;
        _incarichi = incarichi;
    }

    public async Task<DeletionPlan> AnteprimaAsync(DeletionTarget bersaglio, CancellationToken ct = default)
    {
        // Eliminare è un atto d'archivio, non di redazione: lo fa un amministratore. È la stessa riga che
        // separa «rimuovi» da «riaggancia» nella casella degli impatti.
        _authz.EnsureAdmin();
        return await PianoAsync(bersaglio, ct);
    }

    public async Task<DeletionPlan> EliminaAsync(DeletionTarget bersaglio, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();

        var piano = await PianoAsync(bersaglio, ct);
        if (!piano.Eliminabile)
            throw new ValidationException(
                "Non si può eliminare: " + string.Join("; ", piano.Blocca.Select(b => b.Testo)) + ".");

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

        await _repo.ApplyAsync(piano.Azioni, _authz.CurrentUserId ?? 0, ct);

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

    private async Task<DeletionPlan> PianoAsync(DeletionTarget b, CancellationToken ct)
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
                return DeletionRules.PerSettore(f, await _stati.GetPrevSuccessAsync(categoria, ct));
            }

            case DeletionTargetKind.Airport:
                return DeletionRules.PerAeroporto(
                    await _repo.AirportFactsAsync(b.Id, ct) ?? throw Inesistente("Aeroporto"),
                    await _stati.GetPrevSuccessAsync(ImportCategories.AirportDirectory, ct),
                    await _stati.GetPrevSuccessAsync(ImportCategories.AirportSector, ct));

            case DeletionTargetKind.Neighbour:
                return DeletionRules.PerConfinante(
                    await _repo.NeighbourFactsAsync(b.Id, ct) ?? throw Inesistente("Candidato confinante"));

            case DeletionTargetKind.Area:
                return DeletionRules.PerArea(
                    await _repo.AreaFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("Area regolamentata"));

            case DeletionTargetKind.Acc:
                return DeletionRules.PerAcc(
                    await _repo.AccFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("ACC"),
                    await _stati.GetPrevSuccessAsync(ImportCategories.Acc, ct));

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

    private static ValidationException Inesistente(string cosa) => new($"{cosa} inesistente.");
}

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

    public DeletionService(IDeletionRepository repo, IEditAuthorizationService authz, IImportStateStore stati,
        IDocumentImpactService impatti, IDocumentAdminService documenti)
    {
        _repo = repo;
        _authz = authz;
        _stati = stati;
        _impatti = impatti;
        _documenti = documenti;
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
            var doc = (await _documenti.ListAsync(ct)).FirstOrDefault(d => d.DocumentId == docId)
                      ?? throw new ValidationException("Documento non gestito: non risulta fra quelli eliminabili.");
            await _documenti.DeleteAsync(new ManagedDocRef(doc.Kind, doc.ReleaseKey, doc.DocumentId), ct);
            return piano;
        }

        await _repo.ApplyAsync(piano.Azioni, _authz.CurrentUserId ?? 0, ct);

        // I documenti che restano a raccontare qualcosa che non c'è più. La segnalazione parte DOPO
        // l'eliminazione ma con gli Id raccolti PRIMA: un istante dopo il DELETE nessun reverse-lookup
        // troverebbe più il legame appena reciso.
        if (piano.Azioni.DocumentiDaMarcare.Count > 0)
            await _impatti.RaiseForDocumentsAsync(ImpactKind.SectorDetached, piano.Azioni.DocumentiDaMarcare,
                sourceKey: piano.Titolo, args: new[] { piano.Titolo }, ct);

        return piano;
    }

    private async Task<DeletionPlan> PianoAsync(DeletionTarget b, CancellationToken ct)
    {
        switch (b.Kind)
        {
            case DeletionTargetKind.Sector:
            {
                var f = await _repo.SectorFactsAsync(b.Id, ct) ?? throw Inesistente("Settore");
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

            case DeletionTargetKind.Acc:
                return DeletionRules.PerAcc(
                    await _repo.AccFactsAsync(b.Code ?? "", ct) ?? throw Inesistente("ACC"),
                    await _stati.GetPrevSuccessAsync(ImportCategories.Acc, ct));

            default:
                return DeletionRules.PerDocumento(
                    await _repo.DocumentFactsAsync(b.Id, ct) ?? throw Inesistente("Documento"));
        }
    }

    private static ValidationException Inesistente(string cosa) => new($"{cosa} inesistente.");
}

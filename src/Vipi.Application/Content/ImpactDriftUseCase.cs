using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Esito di un giro di deriva, per il log e per la diagnostica.</summary>
/// <param name="Ripuntate">Release rimesse sotto la chiave viva del loro bersaglio (C6). Non è un conteggio
/// decorativo: è l'unica traccia, nel log del giro, di una SCRITTURA su documenti pubblicati.</param>
public sealed record ImpactDriftResult(int Esaminati, int Aperti, int Chiusi, int Potati, int Stantii = 0,
    int Ripuntate = 0);

/// <summary>
/// Il <b>rivelatore calcolato</b> della casella: confronta quel che la copia pubblicata dice con quel che
/// direbbe oggi, e apre <see cref="ImpactKind.ReleaseDrift"/> dove i due divergono. Carta
/// <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c> §5-B.
///
/// <para><b>E guarda anche AVANTI</b> (carta <c>2026-09-02-il-ciclo-entrante.md</c> §AW1): dove la copia
/// pubblicata dice ancora il vero <i>adesso</i> ma non lo dirà al <b>ciclo entrante</b>, apre
/// <see cref="ImpactKind.ReleaseDriftNextCycle"/>. Serviva perché le derivate che dipendono dal ciclo — le
/// SID d'aeroporto, le shape dei settori — <b>nascondono</b> quel che entra dopo: chiedendo solo «com'è
/// oggi», il giro non poteva vedere quel che stava per cambiare, e l'avviso arrivava sempre <b>il giorno
/// dopo il rollover</b>, a ciclo già in vigore. Ora arriva mentre c'è ancora il tempo di programmare la
/// release a quel ciclo — che è il gesto che rende il fatto falso.</para>
///
/// <para><b>Perché serve, se già ci sono gli eventi.</b> Gli eventi dipendono da chi si ricorda di
/// agganciarli: è la trappola del «gate per chiamante», che in questo progetto è già costata due volte.
/// Questo giro non ha trigger da ricordare — guarda il risultato invece della causa — e quindi copre anche i
/// cambi che nessun evento ha emesso: una regola pista riscritta, un blocco tolto, una TA aggiornata
/// dall'import.</para>
///
/// <para><b>Riconcilia</b> invece di accumulare: le righe la cui deriva è sparita — perché qualcuno ha
/// ripubblicato — si chiudono da sole. È la regola «chi calcola, riconcilia» (§2 della carta), e senza di
/// essa la casella si riempirebbe di segnalazioni che nessuno può più togliere.</para>
/// </summary>
public interface IImpactDriftUseCase
{
    Task<ImpactDriftResult> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IImpactDriftUseCase"/>
public sealed class ImpactDriftUseCase : IImpactDriftUseCase
{
    private readonly IDocumentAdminRepository _admin;
    private readonly IReleaseService _releases;
    private readonly IReleaseRepository _releaseRepo;
    private readonly IDocumentImpactService _impacts;
    private readonly IAiracService _airac;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IOrphanSectorRepository? _cataloghi;
    private readonly IImportStateStore? _stati;

    /// <param name="cataloghi">Porta di lettura delle righe di catalogo stantìe. Opzionale: senza, il giro fa
    /// solo la deriva delle pubblicazioni.</param>
    /// <param name="stati">Ultimo giro riuscito degli import: è il metro contro cui un timbro è «vecchio».</param>
    public ImpactDriftUseCase(IDocumentAdminRepository admin, IReleaseService releases,
        IReleaseRepository releaseRepo, IDocumentImpactService impacts, IAiracService airac,
        IReleaseTargetRegistry targets, IOrphanSectorRepository? cataloghi = null,
        IImportStateStore? stati = null)
    {
        _admin = admin;
        _releases = releases;
        _releaseRepo = releaseRepo;
        _impacts = impacts;
        _airac = airac;
        _targets = targets;
        _cataloghi = cataloghi;
        _stati = stati;
    }

    /// <summary>
    /// Quanto margine si dà a un timbro prima di chiamarlo vecchio. Un giorno: gli import girano ogni 24 ore e
    /// un giro può slittare (retry, riavvio, sorgente lenta) — senza margine la prima notte storta produrrebbe
    /// una segnalazione per ogni riga di catalogo.
    /// </summary>
    private static readonly TimeSpan MargineDelTimbro = TimeSpan.FromDays(1);

    /// <summary>Per quanti cicli AIRAC si tengono le righe già chiuse. Due: il tempo di accorgersi che una
    /// cosa era stata segnalata, non tanto da far diventare la tabella un archivio storico.</summary>
    private const int CicliDiRitenzione = 2;

    public async Task<ImpactDriftResult> RunAsync(CancellationToken ct = default)
    {
        var gestiti = await _admin.ListAsync(ct);

        // Solo quelli che il pubblico legge, o che dovrebbe leggere: su una bozza «la copia pubblicata è
        // indietro» non vuol dire niente, e su un documento nascosto non lo legge nessuno. Sono decine di
        // righe, non migliaia.
        // ⚠️ Il cancello è `VaTenutoAggiornato` e NON `IsPublished`: una release programmata non promuove
        // la bozza, quindi un documento pubblicato solo per schedulazione resta Status=Draft per sempre pur
        // essendo in vigore — e questo giro non lo guardava affatto. Misurato dal vivo, due su diciassette.
        var candidati = gestiti
            .Where(d => d.VaTenutoAggiornato && d.DocumentId is not null)
            .ToList();

        var attuali = new List<RaiseImpactInput>();
        var entranti = new List<RaiseImpactInput>();
        var chiaviSpostate = new List<RaiseImpactInput>();
        var bersagliRotti = new List<RaiseImpactInput>();
        var ripuntate = 0;

        // Il ciclo entrante si chiede UNA volta per giro e non per documento: è lo stesso per tutti, e
        // ricalcolarlo per riga significherebbe due documenti valutati contro cicli diversi se il giro
        // scavalcasse la mezzanotte del rollover.
        var entrante = _releases.NextCycle();

        foreach (var d in candidati)
        {
            ct.ThrowIfCancellationRequested();
            var docId = d.DocumentId!.Value;

            // 1) La chiave del bersaglio è ancora quella sotto cui sono scritte le sue release?
            //    ⚠️ La chiave di una vIPI ACC è «{acc}|{callsign del primario}» e quella di un APP È il
            //    callsign: le sposta un settore riparentato o una rinomina in sorgente. Quando succede, le
            //    release restano scritte sotto la vecchia e il pubblico non le trova più: il documento è
            //    pubblicato e la sua pagina è muta. Vedi lavori-aperti C6.
            // 0) Il bersaglio risolve ancora a QUESTO documento? È il caso del §0 della carta: cancella
            //    l'aeroporto e la sua vIPI resta in archivio senza che nessuna pagina la raggiunga più.
            //    ⚠️ Si confronta con l'Id, non con «non null»: un bersaglio che risolve a un ALTRO documento
            //    è rotto quanto uno che non risolve, e in più è silenzioso — la pagina mostra qualcosa.
            var risolto = await _targets.For(d.ReleaseTarget).ResolveDocumentIdAsync(d.ReleaseKey, ct);
            if (risolto != docId)
            {
                bersagliRotti.Add(new RaiseImpactInput(
                    docId, ImpactKind.BrokenTarget, d.ReleaseKey,
                    DocumentImpactService.Reasons.BrokenTarget));
                continue;
            }

            var effettiva = await _releaseRepo.GetEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, DateTime.UtcNow, ct);
            if (effettiva is null)
            {
                if (await ChiaveSpostataAsync(d, docId, ct) is string vecchia)
                {
                    // Si RIPARA, non si segnala soltanto: la chiave è un puntatore, e finché resta indietro
                    // la pagina pubblica di un documento pubblicato è muta. Il ripuntamento è lecito solo
                    // quando è inequivocabile — stesso documento, chiave nuova senza release — e il
                    // repository rifiuta gli altri casi ritornando 0.
                    if (await _releaseRepo.RepointKeyAsync(d.ReleaseTarget, vecchia, d.ReleaseKey, ct) > 0)
                    {
                        ripuntate++;
                        // La deriva di questo documento si guarda al giro prossimo: la copia pubblicata ora
                        // si trova, e confrontarla adesso vorrebbe dire rileggere quel che si è appena scritto.
                        continue;
                    }

                    chiaviSpostate.Add(new RaiseImpactInput(
                        docId, ImpactKind.ReleaseKeyMoved, vecchia,
                        DocumentImpactService.Reasons.ReleaseKeyMoved, new[] { vecchia }));
                }
                continue;
            }

            // 2) La deriva vera e propria.
            var righe = await _releases.DriftFromEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, ct: ct);
            if (righe.Count > 0)
            {
                attuali.Add(new RaiseImpactInput(
                    docId, ImpactKind.ReleaseDrift, d.ReleaseKey,
                    DocumentImpactService.Reasons.ReleaseDrift, new[] { Riassunto(righe) }));
                // ⚠️ E qui si FERMA: un documento già indietro adesso ha già la sua riga «da ripubblicare»,
                // e una seconda che dice «e sarà indietro anche al ciclo entrante» sarebbe rumore su una
                // lista che vive di essere corta. Le due righe non compaiono mai insieme (carta §AW1).
                continue;
            }

            // 3) La deriva al CICLO ENTRANTE. È la riga che mancava: le derivate che dipendono dal ciclo —
            //    le SID d'aeroporto, le shape dei settori — nascondono quel che entra dopo, quindi guardando
            //    solo a oggi il giro non poteva vedere quel che sta per cambiare e l'avviso arrivava sempre
            //    IL GIORNO DOPO il rollover, a ciclo già in vigore. Adesso arriva mentre c'è ancora il tempo
            //    di programmare la release a quel ciclo — che è il gesto che rende il fatto falso.
            var prossime = await _releases.DriftFromEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, entrante.Cycle, ct);
            if (prossime.Count == 0) continue;

            entranti.Add(new RaiseImpactInput(
                docId, ImpactKind.ReleaseDriftNextCycle, d.ReleaseKey,
                DocumentImpactService.Reasons.ReleaseDriftNextCycle,
                new[] { entrante.Cycle, Riassunto(prossime) }));
        }

        var (apertiDeriva, chiusiDeriva) = await _impacts.ReconcileAsync(ImpactKind.ReleaseDrift, attuali, ct);
        var (apertiEntranti, chiusiEntranti) =
            await _impacts.ReconcileAsync(ImpactKind.ReleaseDriftNextCycle, entranti, ct);
        var (apertiChiavi, chiusiChiavi) = await _impacts.ReconcileAsync(ImpactKind.ReleaseKeyMoved, chiaviSpostate, ct);
        var (apertiRotti, chiusiRotti) = await _impacts.ReconcileAsync(ImpactKind.BrokenTarget, bersagliRotti, ct);

        // 3) I cataloghi che la sorgente non manda più: è il caso della RINOMINA, che nessun altro vede.
        var (apertiStantii, chiusiStantii, quantiStantii) = await CataloghiStantiiAsync(ct);

        var potati = await _impacts.PruneClearedBeforeAsync(SogliaRitenzione(), ct);

        return new ImpactDriftResult(
            candidati.Count,
            apertiDeriva + apertiEntranti + apertiChiavi + apertiRotti + apertiStantii,
            chiusiDeriva + chiusiEntranti + chiusiChiavi + chiusiRotti + chiusiStantii,
            potati,
            quantiStantii,
            ripuntate);
    }


    /// <summary>
    /// Le righe di catalogo che la sorgente ha smesso di elencare, riconosciute dal <b>timbro</b>
    /// <c>ImportedAtUtc</c> rimasto indietro rispetto all'ultimo giro riuscito.
    ///
    /// <para><b>Perché serve un rivelatore apposta.</b> I cataloghi non potano mai, quindi quando la sorgente
    /// rinomina una posizione — <c>LIRN_US0_APP</c> → <c>LIRN_US1_APP</c> — <b>non sparisce niente</b>: la
    /// riga vecchia resta, il settore resta attivo, e la proiezione non ha nulla da segnalare. Il fantasma
    /// continua a rivendicare la sua area, a portarsi dietro il documento e a comparire nelle mappe, mentre
    /// chi controlla davvero si connette col nome nuovo. Misurato sull'archivio reale: <c>LIED_G_APP</c> era
    /// fermo al 5 agosto contro il 24 delle altre tre posizioni dello stesso scalo.</para>
    ///
    /// <para>⚠️ <b>Due guardie.</b> Se manca l'ultimo giro riuscito di una delle due famiglie non si dice
    /// niente — «non lo sappiamo» non è «sono spariti tutti», la stessa regola dell'avvio a freddo. E le
    /// righe <b>aggiunte a mano</b> sono escluse a monte: la sorgente non le ha mai mandate, quindi il loro
    /// timbro è vecchio per costruzione.</para>
    /// </summary>
    private async Task<(int Aperti, int Chiusi, int Quanti)> CataloghiStantiiAsync(CancellationToken ct)
    {
        if (_cataloghi is null || _stati is null) return (0, 0, 0);

        var aeroporti = await _stati.GetLastSuccessAsync(ImportCategories.AirportSector, ct);
        var acc = await _stati.GetLastSuccessAsync(ImportCategories.Acc, ct);
        if (aeroporti is null || acc is null) return (0, 0, 0);

        // Il metro è il giro più VECCHIO fra i due: usare il più recente segnalerebbe le righe dell'altra
        // famiglia solo perché il suo giro è slittato di qualche ora.
        var soglia = (aeroporti < acc ? aeroporti.Value : acc.Value) - MargineDelTimbro;

        var stantie = await _cataloghi.ListStaleCatalogRowsAsync(soglia, ct);

        // ⚠️ Guardia di massa: vedi SogliaTimbro.TroppiPerEssereVeri. Si riconcilia comunque con l'insieme
        // VUOTO, così le righe aperte ieri si richiudono invece di restare appese a un sospetto.
        if (SogliaTimbro.TroppiPerEssereVeri(stantie.Count, await _cataloghi.CountCatalogRowsAsync(ct)))
        {
            var (_, richiusi) = await _impacts.ReconcileAsync(
                ImpactKind.SectorStale, Array.Empty<RaiseImpactInput>(), ct);
            return (0, richiusi, 0);
        }

        var attuali = new List<RaiseImpactInput>();
        var adesso = DateTime.UtcNow;

        foreach (var r in stantie)
        {
            ct.ThrowIfCancellationRequested();
            var giorni = Math.Max(1, r.GiorniDiSilenzio(adesso));
            attuali.AddRange(await _impacts.PrepareForSectorAsync(
                ImpactKind.SectorStale, r.Callsign, r.AccCode,
                new[] { r.Callsign, giorni.ToString() }, ct));
        }

        var (aperti, chiusi) = await _impacts.ReconcileAsync(ImpactKind.SectorStale, attuali, ct);
        return (aperti, chiusi, stantie.Count);
    }

    /// <summary>
    /// Il documento non ha release sotto la chiave di oggi: ce ne sono sotto un'ALTRA chiave dello stesso
    /// tipo che risolve a lui? Allora la chiave si è spostata, e il pubblico non trova più niente. Ritorna la
    /// chiave vecchia, o null se semplicemente non è mai stato pubblicato sotto nessuna chiave.
    /// </summary>
    private async Task<string?> ChiaveSpostataAsync(ManagedDoc d, int docId, CancellationToken ct)
    {
        var descrittore = _targets.For(d.ReleaseTarget);
        foreach (var chiave in await _releaseRepo.ListKeysWithReleasesAsync(d.ReleaseTarget, ct))
        {
            if (string.Equals(chiave, d.ReleaseKey, StringComparison.OrdinalIgnoreCase)) continue;
            if (await descrittore.ResolveDocumentIdAsync(chiave, ct) == docId) return chiave;
        }
        return null;
    }

    /// <summary>Le sezioni toccate, non il conteggio: «AoR, Frequenze» dice all'editore dove guardare, «3
    /// differenze» no.</summary>
    private static string Riassunto(IReadOnlyList<ReleaseDiffRow> righe)
    {
        const int quante = 3;
        var nomi = righe.Select(r => r.Label).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return nomi.Count <= quante
            ? string.Join(", ", nomi)
            : string.Join(", ", nomi.Take(quante)) + $" (+{nomi.Count - quante})";
    }

    private DateTime SogliaRitenzione() =>
        _airac.EffectiveUtcForCycle(_airac.GetCycle(DateTime.UtcNow)).AddDays(-28 * CicliDiRitenzione);
}

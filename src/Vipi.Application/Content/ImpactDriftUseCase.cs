using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Esito di un giro di deriva, per il log e per la diagnostica.</summary>
public sealed record ImpactDriftResult(int Esaminati, int Aperti, int Chiusi, int Potati);

/// <summary>
/// Il <b>rivelatore calcolato</b> della casella: confronta quel che la copia pubblicata dice con quel che
/// direbbe oggi, e apre <see cref="ImpactKind.ReleaseDrift"/> dove i due divergono. Carta
/// <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c> §5-B.
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

    public ImpactDriftUseCase(IDocumentAdminRepository admin, IReleaseService releases,
        IReleaseRepository releaseRepo, IDocumentImpactService impacts, IAiracService airac,
        IReleaseTargetRegistry targets)
    {
        _admin = admin;
        _releases = releases;
        _releaseRepo = releaseRepo;
        _impacts = impacts;
        _airac = airac;
        _targets = targets;
    }

    /// <summary>Per quanti cicli AIRAC si tengono le righe già chiuse. Due: il tempo di accorgersi che una
    /// cosa era stata segnalata, non tanto da far diventare la tabella un archivio storico.</summary>
    private const int CicliDiRitenzione = 2;

    public async Task<ImpactDriftResult> RunAsync(CancellationToken ct = default)
    {
        var gestiti = await _admin.ListAsync(ct);

        // Solo i PUBBLICATI e non nascosti: su una bozza «la copia pubblicata è indietro» non vuol dire
        // niente, e su un documento nascosto non lo legge nessuno. Sono decine di righe, non migliaia.
        var candidati = gestiti
            .Where(d => d.IsPublished && !d.IsHidden && d.DocumentId is not null)
            .ToList();

        var attuali = new List<RaiseImpactInput>();
        var chiaviSpostate = new List<RaiseImpactInput>();
        var bersagliRotti = new List<RaiseImpactInput>();

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
                    chiaviSpostate.Add(new RaiseImpactInput(
                        docId, ImpactKind.ReleaseKeyMoved, vecchia,
                        DocumentImpactService.Reasons.ReleaseKeyMoved, new[] { vecchia }));
                continue;
            }

            // 2) La deriva vera e propria.
            var righe = await _releases.DriftFromEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, ct);
            if (righe.Count == 0) continue;

            attuali.Add(new RaiseImpactInput(
                docId, ImpactKind.ReleaseDrift, d.ReleaseKey,
                DocumentImpactService.Reasons.ReleaseDrift, new[] { Riassunto(righe) }));
        }

        var (apertiDeriva, chiusiDeriva) = await _impacts.ReconcileAsync(ImpactKind.ReleaseDrift, attuali, ct);
        var (apertiChiavi, chiusiChiavi) = await _impacts.ReconcileAsync(ImpactKind.ReleaseKeyMoved, chiaviSpostate, ct);
        var (apertiRotti, chiusiRotti) = await _impacts.ReconcileAsync(ImpactKind.BrokenTarget, bersagliRotti, ct);

        var potati = await _impacts.PruneClearedBeforeAsync(SogliaRitenzione(), ct);

        return new ImpactDriftResult(
            candidati.Count,
            apertiDeriva + apertiChiavi + apertiRotti,
            chiusiDeriva + chiusiChiavi + chiusiRotti,
            potati);
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

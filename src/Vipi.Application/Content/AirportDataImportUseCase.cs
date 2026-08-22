using System.Runtime.ExceptionServices;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Esito del giro su TA e piste: quanti aeroporti sono stati toccati e quali sono falliti (il chiamante
/// logga). <paramref name="Skipped"/> è vero quando la policy esclude <b>entrambe</b> le categorie: non è un
/// fallimento, è la scelta dell'amministratore — e vale la pena distinguerlo da «zero aeroporti».
/// </summary>
public sealed record AirportDataImportResult(int Airports, IReadOnlyList<AirportImportFailure> Failures, bool Skipped)
{
    public static AirportDataImportResult SkippedByPolicy { get; } =
        new(0, Array.Empty<AirportImportFailure>(), true);
}

/// <summary>
/// Il giro periodico di <b>Transition Altitude</b> e <b>Piste</b>: le due categorie che fino al 22 agosto
/// 2026 arrivavano solo quando qualcuno premeva un bottone (reimport nell'editor aeroporto, massivo su
/// <c>/services/vsop/admin/airports</c>, «Genera documenti»). Senza un giro, una TA cambiata in AIRAC
/// restava vecchia a tempo indefinito e nessuna riga della pagina Sorgenti lo segnalava: la pill diceva
/// «su richiesta», che è vero e inutile — non dice quanto è vecchio il dato.
/// </summary>
public interface IAirportDataImportUseCase
{
    /// <summary>Rilegge TA e piste dalla sorgente per <b>tutti</b> gli aeroporti in archivio.</summary>
    Task<AirportDataImportResult> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportDataImportUseCase"/>
public sealed class AirportDataImportUseCase : IAirportDataImportUseCase
{
    private readonly IAirportSectorRepository _airports;
    private readonly IAirportRepository _repo;
    private readonly IAirportDirectory _directory;
    private readonly IAirportDetailProvider _details;
    private readonly IImportPolicyStore _policy;

    public AirportDataImportUseCase(IAirportSectorRepository airports, IAirportRepository repo,
        IAirportDirectory directory, IAirportDetailProvider details, IImportPolicyStore policy)
    {
        _airports = airports;
        _repo = repo;
        _directory = directory;
        _details = details;
        _policy = policy;
    }

    /// <summary>
    /// ⚠️ Passa per lo <b>stesso</b> <see cref="SourceMergeInputs.ReadAsync"/> +
    /// <c>MergeFromSourceAsync</c> del bottone: nessun secondo percorso, quindi nessun modo per il giro
    /// automatico e quello manuale di divergere sulla policy. È la lezione del 22 agosto («un gate per
    /// categoria, non uno per chiamante») applicata al motore invece che alla toppa.
    ///
    /// <para>Con entrambe le categorie escluse si esce <b>prima</b> della fetch, come fanno
    /// <see cref="SpecialAreaImportUseCase"/> e <see cref="AirportSectorImporter"/>: la sorgente non si
    /// interroga per un dato che non si può scrivere.</para>
    /// </summary>
    public async Task<AirportDataImportResult> RunAsync(CancellationToken ct = default)
    {
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.TransitionAltitude) && !policy.IsImported(ImportCategory.Runways))
            return AirportDataImportResult.SkippedByPolicy;

        var icaos = await _airports.ListAirportIcaosAsync(ct);
        var failures = new List<AirportImportFailure>();
        var toccati = 0;

        foreach (var icao in icaos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Un aeroporto per volta, e il salvataggio dentro il merge: un giro su 92 aeroporti che
                // accumulasse tutto e salvasse alla fine perderebbe l'intero giro per un 404 sull'ultimo.
                var (ta, runways) = await SourceMergeInputs.ReadAsync(policy, icao, _directory, _details, ct);
                await _repo.MergeFromSourceAsync(icao, ta, runways, ct);
                toccati++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Per-aeroporto si annota e si prosegue; il chiamante logga a Warning (a Debug un import
                // rotto sugli scali principali è rimasto invisibile per cicli interi — vedi import SID).
                // Un guasto GLOBALE (credenziali assenti, sorgente giù) fallisce invece su tutti gli
                // aeroporti e viene rilanciato in coda: lì il loop deve scrivere l'errore e ritentare.
                failures.Add(new AirportImportFailure(icao, ex));
            }
        }

        // Tutti falliti con almeno un aeroporto in archivio: non è una collezione di guasti locali, è la
        // sorgente che non risponde. Rilanciare è ciò che fa scrivere `LastError` sulla riga di stato —
        // altrimenti la pagina Sorgenti mostrerebbe verde e data di oggi per un giro che non ha importato
        // niente, che è esattamente il «verde regalato» chiuso il 22 agosto.
        // ⚠️ `ExceptionDispatchInfo` e non `throw failures[0].Error`: il TIPO deve sopravvivere, perché è
        // così che il chiamante distingue «credenziali sorgente assenti» (InvalidOperationException, si
        // salta in silenzio) da un guasto vero da ritentare. Un `throw ex` azzererebbe anche lo stack.
        if (failures.Count > 0 && failures.Count == icaos.Count)
            ExceptionDispatchInfo.Capture(failures[0].Error).Throw();

        return new AirportDataImportResult(toccati, failures, Skipped: false);
    }
}

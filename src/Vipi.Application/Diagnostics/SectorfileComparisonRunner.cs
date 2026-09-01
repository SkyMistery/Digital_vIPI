using Vipi.Application.Abstractions;

namespace Vipi.Application.Diagnostics;

/// <summary>
/// Esegue <b>un</b> confronto col sectorfile e lascia la fotografia in <see cref="ISectorfileComparisonReport"/>.
///
/// <para><b>Perché un servizio e non il corpo del giro periodico.</b> Il confronto si fa in due momenti — il
/// giro delle 24 ore e il tasto «confronta adesso» dello staff — e sono lo stesso confronto. Scritto due
/// volte, il giorno in cui una tolleranza cambia ne cambia una sola, e i due numeri divergono senza che
/// nessuno lo veda.</para>
///
/// <para>⚠️ Il tasto serve davvero: chi corregge il sectorfile vuole sapere <i>adesso</i> se è tornato a
/// posto, e aspettare fino a domani vorrebbe dire non riguardarlo mai più. È lo stesso ragionamento del
/// «chiedi alla sorgente adesso» delle sorgenti d'import.</para>
/// </summary>
public interface ISectorfileComparisonRunner
{
    /// <summary>True se il confronto è riuscito. Un guasto non lancia: resta scritto nella fotografia.</summary>
    Task<bool> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class SectorfileComparisonRunner : ISectorfileComparisonRunner
{
    private readonly ISectorfileFactsSource _sorgente;
    private readonly ISectorfileComparisonRepository _repo;
    private readonly ISectorfileComparisonReport _report;

    public SectorfileComparisonRunner(ISectorfileFactsSource sorgente,
        ISectorfileComparisonRepository repo, ISectorfileComparisonReport report)
    {
        _sorgente = sorgente;
        _repo = repo;
        _report = report;
    }

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var facts = await _sorgente.GetFactsAsync(ct);
            if (facts is null)
            {
                // ⚠️ Non si scrive una fotografia vuota: «il sectorfile non ha risposto» e «il sectorfile
                // non ha divergenze» sono due cose opposte, e la seconda sarebbe una bugia rassicurante.
                _report.SetError("Il sectorfile non ha risposto: confronto saltato.");
                return false;
            }

            _report.Set(SectorfileComparison.Analyze(facts, await _repo.LoadAsync(ct)), DateTime.UtcNow);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _report.SetError($"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}

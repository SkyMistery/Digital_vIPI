using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Un documento pubblicato, guardato dal punto di vista del ciclo entrante.</summary>
/// <param name="GiaProgrammato">Ha già una release programmata <b>a quel ciclo</b>. ⚠️ Non «ne ha una
/// programmata»: una release schedulata a un ciclo ancora più in là non copre quello entrante.</param>
public sealed record DocumentoAlCicloEntrante(
    ReleaseTargetType Tipo, string Chiave, string Titolo, string? AccCode, bool GiaProgrammato);

/// <summary>
/// Il quadro del <b>ciclo entrante</b>: quale ciclo sta per entrare, quando, e a che punto sono i documenti
/// pubblicati. Carta <c>docs/feature/2026-09-02-il-ciclo-entrante.md</c> §AW3.
/// </summary>
/// <param name="GiorniAllEntrata">Quanti giorni mancano, arrotondati per eccesso. Zero = entra oggi.</param>
public sealed record QuadroCicloEntrante(
    string CicloCorrente, string CicloEntrante, DateTime EfficaceUtc, int GiorniAllEntrata,
    IReadOnlyList<DocumentoAlCicloEntrante> Documenti)
{
    public int Programmati => Documenti.Count(d => d.GiaProgrammato);
    public int DaProgrammare => Documenti.Count - Programmati;
}

/// <summary>Esito di una programmazione in blocco.</summary>
/// <param name="Saltati">Chi non è passato e <b>perché</b>, titolo per titolo. ⚠️ Non è decorazione: un giro
/// che riesce a metà in silenzio è peggio di uno che fallisce, perché chi l'ha premuto crede di aver finito.</param>
public sealed record EsitoProgrammazione(int Programmati, IReadOnlyList<(string Titolo, string Motivo)> Saltati);

/// <summary>
/// Che cosa manca al <b>ciclo AIRAC entrante</b>, e il gesto per metterlo a posto in blocco.
///
/// <para><b>Perché non è una lista nuova.</b> Le righe di lavoro — «questo documento al ciclo entrante non
/// dirà più il vero» — le porta già la lista unica (<see cref="ImpactKind.ReleaseDriftNextCycle"/>, §AW1), e
/// la §1 del <c>FEATURE-PROCESS</c> dice di estendere e mai affiancare. Qui c'è solo il <b>quadro di
/// insieme</b> con il gesto in blocco, e vive come sezione di <c>/services/vsop/versions</c> — la pagina che
/// già mostra il ciclo corrente e già inietta le release.</para>
/// </summary>
public interface IProssimoAiracService
{
    /// <summary>Il quadro. Solo documenti <b>pubblicati e non nascosti</b>: su una bozza «programmare una
    /// release» non vuol dire niente, e su un documento nascosto non lo legge nessuno.</summary>
    Task<QuadroCicloEntrante> LeggiAsync(CancellationToken ct = default);

    /// <summary>
    /// Programma una release al ciclo entrante per ogni documento che non ce l'ha.
    ///
    /// <para>⚠️ <b>Passa dallo stesso <c>PublishAsync</c> di un pubblica singolo</b>, uno per uno: è già il
    /// posto dove si chiedono i permessi sull'ACC e si controlla il lock di un altro editor. Rifarle qui
    /// sarebbe una seconda definizione delle stesse due regole, cioè il modo in cui due racconti divergono.
    /// Chi non passa finisce fra i <see cref="EsitoProgrammazione.Saltati"/> col suo motivo, e il giro
    /// prosegue: un documento lockato non deve fermare gli altri ventinove.</para>
    /// </summary>
    Task<EsitoProgrammazione> ProgrammaMancantiAsync(string? nota = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IProssimoAiracService"/>
public sealed class ProssimoAiracService : IProssimoAiracService
{
    private readonly IDocumentAdminRepository _admin;
    private readonly IReleaseService _releases;
    private readonly IAiracService _airac;

    public ProssimoAiracService(IDocumentAdminRepository admin, IReleaseService releases, IAiracService airac)
    {
        _admin = admin;
        _releases = releases;
        _airac = airac;
    }

    public async Task<QuadroCicloEntrante> LeggiAsync(CancellationToken ct = default)
    {
        var entrante = _releases.NextCycle();
        var adesso = DateTime.UtcNow;

        // ⚠️ `NextScheduledCycle` arriva dalla STESSA query dell'elenco (ManagedDoc): niente N+1, e niente
        // seconda lettura che potrebbe raccontare un'altra storia.
        var documenti = (await _admin.ListAsync(ct))
            .Where(d => d.IsPublished && !d.IsHidden && d.DocumentId is not null)
            .Select(d => new DocumentoAlCicloEntrante(
                d.ReleaseTarget, d.ReleaseKey, d.Title, d.AccCode,
                GiaProgrammato: string.Equals(d.NextScheduledCycle, entrante.Cycle, StringComparison.Ordinal)))
            .OrderBy(d => d.GiaProgrammato)                             // prima chi manca: è quello da guardare
            .ThenBy(d => d.AccCode ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Titolo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Per eccesso: a mezza giornata dall'entrata «mancano 0 giorni» direbbe che è già dentro.
        var giorni = (int)Math.Ceiling((entrante.EffectiveUtc - adesso).TotalDays);

        return new QuadroCicloEntrante(
            _airac.GetCycle(adesso), entrante.Cycle, entrante.EffectiveUtc, Math.Max(0, giorni), documenti);
    }

    public async Task<EsitoProgrammazione> ProgrammaMancantiAsync(string? nota = null, CancellationToken ct = default)
    {
        var quadro = await LeggiAsync(ct);
        var saltati = new List<(string, string)>();
        var fatti = 0;

        foreach (var d in quadro.Documenti.Where(x => !x.GiaProgrammato))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _releases.PublishAsync(d.Tipo, d.Chiave, quadro.CicloEntrante, nota, ct);
                fatti++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Permesso negato, lock di un altro editor, documento senza contenuto: sono i casi normali di
                // un giro su decine di documenti, e vanno DETTI. L'eccezione non si rilancia, o il primo
                // documento lockato fermerebbe tutti quelli dopo.
                saltati.Add((d.Titolo, ex.Message));
            }
        }

        return new EsitoProgrammazione(fatti, saltati);
    }
}

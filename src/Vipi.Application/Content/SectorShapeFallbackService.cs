using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Un settore di catalogo, con lo stato della sua shape: quel che serve al ripiego per decidere.</summary>
/// <param name="Catalog">Da quale dei due cataloghi viene: serve a scriverci sopra.</param>
/// <param name="HasUsableShape">Ha già una shape che si disegna.</param>
/// <param name="Shape">Corrente, in vigore, ciclo di entrata, provenienza, forzatura.</param>
public sealed record SectorShapeRow(
    SourceCatalog Catalog, int Id, string Callsign, string? Position, bool HasUsableShape, ShapeState Shape);

/// <summary>Cosa scrivere su una riga: la geometria nuova, quella che resta in vigore, e da quando.</summary>
public sealed record ShapeWrite(
    SourceCatalog Catalog, int Id, string PolygonJson, string? InForce, string? FromCycle);

/// <summary>Persistenza per il ripiego shape: i settori, dove scriverla, e la promozione dei differiti.</summary>
public interface ISectorShapeRepository
{
    /// <summary>
    /// I settori dei due cataloghi che possono avere un'area — CTR/FSS dai subcenter, APP/DEP dalle posizioni
    /// d'aeroporto — con lo stato della shape che hanno adesso.
    /// <para>⚠️ Le <b>TWR</b> restano fuori: hanno il loro ripiego (<c>GithubTowerShapeService</c> e il cerchio),
    /// e due strade che scrivono la stessa colonna con regole diverse sono due racconti che divergono.</para>
    /// </summary>
    Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default);

    /// <summary>Scrive la shape (provenienza <see cref="ShapeSource.Sectorfile"/>) e il suo differimento.</summary>
    Task ApplyShapeAsync(ShapeWrite write, CancellationToken ct = default);

    /// <summary>Chiude i differimenti il cui ciclo è arrivato. Ritorna quanti ne ha promossi.</summary>
    Task<int> PromoteDueShapesAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <inheritdoc cref="SectorShapeFallbackService"/>
public interface ISectorShapeFallbackService
{
    /// <summary>Applica le shape del sectorfile e chiude i differimenti maturati.</summary>
    Task<SectorShapeFallbackResult> ApplyAsync(CancellationToken ct = default);
}

/// <param name="Applied">Shape scritte per la prima volta (settori che non ne avevano).</param>
/// <param name="Updated">Shape aggiornate perché il sectorfile le ha cambiate: differite al ciclo successivo.</param>
/// <param name="Promoted">Differimenti chiusi perché il ciclo è arrivato.</param>
/// <param name="StillWithout">Quanti settori restano senza area anche dopo il ripiego.</param>
/// <param name="UnresolvedPoints">I punti che il catalogo navaid non conosce: ognuno vale uno o più settori
/// senza area, e non saperlo vorrebbe dire cercarne la causa a schermo.</param>
public sealed record SectorShapeFallbackResult(
    int Applied, int Updated, int Promoted, int StillWithout,
    IReadOnlyList<(string Point, string Callsigns)> UnresolvedPoints)
{
    public static readonly SectorShapeFallbackResult Nothing =
        new(0, 0, 0, 0, Array.Empty<(string, string)>());
}

/// <summary>
/// Dà un'area ai settori (CTR/APP/MIL/FSS) che dall'anagrafica IVAO non ne hanno ricevuta, prendendola dal
/// sectorfile Aurora. È il gemello di <c>GithubTowerShapeService</c>, che fa lo stesso per le TWR.
///
/// <para><b>È un ripiego, e si comporta come tale</b>: non sovrascrive mai una shape dell'anagrafica. Se IVAO
/// ricomincia a dare i poligoni, il giro successivo li scrive e questo servizio smette da sé di avere lavoro
/// da fare — senza che si tocchi niente.</para>
///
/// <para><b>Ma aggiorna quel che ha scritto lui.</b> ⚠️ La prima stesura riempiva soltanto i vuoti, e così il
/// sectorfile sarebbe stato una sorgente <i>write-once</i>: un confine ridisegnato non sarebbe mai arrivato.
/// Ora una geometria cambiata entra — <b>differita al ciclo successivo</b>, perché il sectorfile lo scriviamo
/// in anticipo (<see cref="ShapeAiracGate"/>).</para>
///
/// <para>Idempotente: quel che ha appena scritto non è più un bersaglio.</para>
/// </summary>
public sealed class SectorShapeFallbackService : ISectorShapeFallbackService
{
    private readonly ISectorShapeRepository _repo;
    private readonly ISectorShapeSource _source;
    private readonly IAiracService _airac;
    private readonly TimeProvider _clock;

    public SectorShapeFallbackService(
        ISectorShapeRepository repo, ISectorShapeSource source, IAiracService airac, TimeProvider? clock = null)
    {
        _repo = repo;
        _source = source;
        _airac = airac;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<SectorShapeFallbackResult> ApplyAsync(CancellationToken ct = default)
    {
        var adesso = _clock.GetUtcNow().UtcDateTime;

        // Prima si chiudono i differimenti maturati: una shape entrata in vigore stanotte dev'essere già
        // «corrente e basta» quando si guarda se il sectorfile l'ha cambiata di nuovo.
        var promossi = await _repo.PromoteDueShapesAsync(adesso, ct);

        var candidati = await _repo.ListShapeCandidatesAsync(ct);
        var daFare = candidati.Where(RiguardaIlRipiego).ToList();
        var senzaArea = candidati.Count(c => !c.HasUsableShape);
        if (daFare.Count == 0)
            return SectorShapeFallbackResult.Nothing with { Promoted = promossi, StillWithout = senzaArea };

        var shapes = await _source.GetSectorPolygonsAsync(ct);
        if (shapes.PolygonsByCallsign.Count == 0)
            return SectorShapeFallbackResult.Nothing with
            {
                Promoted = promossi, StillWithout = senzaArea, UnresolvedPoints = shapes.UnresolvedPoints,
            };

        var cicloProssimo = ProssimoCiclo(adesso);
        int nuove = 0, aggiornate = 0;

        foreach (var s in daFare)
        {
            ct.ThrowIfCancellationRequested();
            if (!shapes.PolygonsByCallsign.TryGetValue(s.Callsign, out var json)) continue;

            // ⚠️ Si controlla che si DISEGNI prima di scriverla. Un anello degenere passerebbe il controllo di
            // «non è vuota» e finirebbe in colonna come una shape vera, togliendo il settore dai bersagli del
            // ripiego per sempre: resterebbe senza area, ma senza più nessuno che ci riprovi.
            if (AorPolygonProjector.Project(json) is null) continue;

            if (!s.HasUsableShape)
            {
                // Primo riempimento: in vigore subito. Differirlo vorrebbe dire nessuna area fino a 28 giorni.
                await _repo.ApplyShapeAsync(new ShapeWrite(s.Catalog, s.Id, json, InForce: null, FromCycle: null), ct);
                nuove++;
                continue;
            }

            // Ha già una shape NOSTRA e il sectorfile l'ha cambiata: entra, ma dal ciclo successivo, e quella
            // di adesso resta da parte per chi pubblica nel frattempo.
            if (string.Equals(s.Shape.Current, json, StringComparison.Ordinal)) continue;   // identica: niente da fare
            await _repo.ApplyShapeAsync(
                new ShapeWrite(s.Catalog, s.Id, json, InForce: InVigoreOra(s.Shape), FromCycle: cicloProssimo), ct);
            aggiornate++;
        }

        return new SectorShapeFallbackResult(
            nuove, aggiornate, promossi, senzaArea - nuove, shapes.UnresolvedPoints);
    }

    /// <summary>
    /// Di chi si occupa il ripiego: chi non ha un'area, e chi ce l'ha <b>messa lui</b>. Mai una shape
    /// dell'anagrafica — quella comanda — né un cerchio sintetico, che è roba delle TWR.
    /// </summary>
    private static bool RiguardaIlRipiego(SectorShapeRow r) =>
        !r.HasUsableShape || r.Shape.Source == ShapeSource.Sectorfile;

    /// <summary>Quella che resta in vigore mentre la nuova aspetta: se un differimento era già aperto, la
    /// precedente è ancora lei — non la corrente, che non è mai entrata in vigore.</summary>
    private static string? InVigoreOra(ShapeState s) => s.InForce ?? s.Current;

    /// <summary>Il ciclo successivo a quello corrente: è da lì che la geometria nuova entra in vigore.</summary>
    private string ProssimoCiclo(DateTime nowUtc)
    {
        var prossimi = _airac.NextCycles(nowUtc, 2);
        return prossimi.Count > 1 ? prossimi[1].Cycle : _airac.GetCycle(nowUtc);
    }
}

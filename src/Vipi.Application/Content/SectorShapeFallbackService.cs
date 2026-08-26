using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un settore di catalogo che potrebbe volere una shape dal ripiego.</summary>
/// <param name="Catalog">Da quale dei due cataloghi viene: serve a scriverci sopra.</param>
/// <param name="HasUsableShape">Ha già una shape che si disegna. Se sì il ripiego non lo tocca.</param>
public sealed record SectorShapeRow(
    SourceCatalog Catalog, int Id, string Callsign, string? Position, bool HasUsableShape);

/// <summary>Persistenza per il ripiego shape: i settori che ne hanno bisogno, e dove scriverla.</summary>
public interface ISectorShapeRepository
{
    /// <summary>
    /// I settori dei due cataloghi che possono avere un'area — CTR/FSS dai subcenter, APP/DEP dalle posizioni
    /// d'aeroporto — con lo stato della shape che hanno adesso.
    /// <para>⚠️ Le <b>TWR</b> restano fuori: hanno il loro ripiego (<c>GithubTowerShapeService</c> e il cerchio),
    /// e due strade che scrivono la stessa colonna con regole diverse sono due racconti che divergono.</para>
    /// </summary>
    Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default);

    /// <summary>Scrive la shape sul settore indicato. Non tocca nient'altro della riga.</summary>
    Task SetShapeAsync(SourceCatalog catalog, int id, string polygonJson, CancellationToken ct = default);
}

/// <inheritdoc cref="SectorShapeFallbackService"/>
public interface ISectorShapeFallbackService
{
    /// <summary>Applica le shape del sectorfile ai settori che non ne hanno una utilizzabile.</summary>
    Task<SectorShapeFallbackResult> ApplyAsync(CancellationToken ct = default);
}

/// <param name="Applied">Quante shape sono state scritte.</param>
/// <param name="StillWithout">Quanti settori restano senza area anche dopo il ripiego.</param>
/// <param name="UnresolvedPoints">I punti che il catalogo navaid non conosce: ognuno vale uno o più settori
/// senza area, e non saperlo vorrebbe dire cercarne la causa a schermo.</param>
public sealed record SectorShapeFallbackResult(
    int Applied, int StillWithout, IReadOnlyList<(string Point, string Callsigns)> UnresolvedPoints);

/// <summary>
/// Dà un'area ai settori (CTR/APP/MIL/FSS) che dall'anagrafica IVAO non ne hanno ricevuta, prendendola dal
/// sectorfile Aurora. È il gemello di <c>GithubTowerShapeService</c>, che fa lo stesso per le TWR.
///
/// <para><b>È un ripiego, e si comporta come tale</b>: tocca solo chi non ha una shape che si disegna, e non
/// sovrascrive mai quella dell'anagrafica. Se IVAO ricomincia a dare i poligoni, il giro successivo li scrive
/// e questo servizio smette da sé di avere lavoro da fare — senza che si tocchi niente.</para>
///
/// <para>Idempotente: quel che ha appena scritto non è più un bersaglio.</para>
/// </summary>
public sealed class SectorShapeFallbackService : ISectorShapeFallbackService
{
    private readonly ISectorShapeRepository _repo;
    private readonly ISectorShapeSource _source;

    public SectorShapeFallbackService(ISectorShapeRepository repo, ISectorShapeSource source)
    {
        _repo = repo;
        _source = source;
    }

    public async Task<SectorShapeFallbackResult> ApplyAsync(CancellationToken ct = default)
    {
        var candidati = await _repo.ListShapeCandidatesAsync(ct);
        var senza = candidati.Where(c => !c.HasUsableShape).ToList();
        if (senza.Count == 0)
            return new SectorShapeFallbackResult(0, 0, Array.Empty<(string, string)>());

        var shapes = await _source.GetSectorPolygonsAsync(ct);
        if (shapes.PolygonsByCallsign.Count == 0)
            return new SectorShapeFallbackResult(0, senza.Count, shapes.UnresolvedPoints);

        var applicate = 0;
        foreach (var s in senza)
        {
            ct.ThrowIfCancellationRequested();
            if (!shapes.PolygonsByCallsign.TryGetValue(s.Callsign, out var json)) continue;

            // ⚠️ Si controlla che si DISEGNI prima di scriverla. Un anello degenere passerebbe il controllo di
            // «non è vuota» e finirebbe in colonna come una shape vera, togliendo il settore dai bersagli del
            // ripiego per sempre: resterebbe senza area, ma senza più nessuno che ci riprovi.
            if (AorPolygonProjector.Project(json) is null) continue;

            await _repo.SetShapeAsync(s.Catalog, s.Id, json, ct);
            applicate++;
        }

        return new SectorShapeFallbackResult(applicate, senza.Count - applicate, shapes.UnresolvedPoints);
    }
}

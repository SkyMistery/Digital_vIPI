using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Caratterizzazione del cuore deterministico dei confinanti (doc refactor 05 §4.2): filtro confine CTR/FSS,
/// adiacenza domestici×esteri, aggregazione per coppia, catalogo estero confinante. Poligoni in formato IVAO [lng,lat].
/// </summary>
public class NeighbourAdjacencyComputerTests
{
    // Quadrati ~1°×1°. A = lon 10..11 / lat 43..44 (domestico).
    private const string SquareA = "[[10,43],[11,43],[11,44],[10,44]]";
    private const string SquareB_Touching = "[[11,43],[12,43],[12,44],[11,44]]";   // condivide il bordo est di A
    private const string SquareD_Far = "[[20,43],[21,43],[21,44],[20,44]]";        // lontano

    private readonly NeighbourAdjacencyComputer _sut = new();

    [Theory]
    [InlineData("LIRR_CTR", true)]
    [InlineData("LIBB_ES_CTR", true)]
    [InlineData("LIRR_FSS", true)]
    [InlineData("LIRN_TWR", false)]
    [InlineData("LIRN_APP", false)]
    [InlineData("LIRN_I_TWR", false)]
    [InlineData("LIRN_GND", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAccBoundaryPosition_only_CTR_and_FSS(string? callsign, bool expected) =>
        Assert.Equal(expected, NeighbourAdjacencyComputer.IsAccBoundaryPosition(callsign));

    [Fact]
    public void ComputeImport_adjacent_foreign_CTR_produces_one_candidate()
    {
        var domestic = new[] { new DomesticSectorPoly("LIRR", "LIRR_CTR", SquareA) };
        var foreign = new[]
        {
            new ForeignAccData("LFFF", "France", "FR", new[]
            {
                Sub("LFFF_CTR", SquareB_Touching),   // adiacente → hit
                Sub("LFFF_APP", SquareB_Touching),   // APP: escluso dal filtro confine
                Sub("LFFF_N_CTR", SquareD_Far),      // lontano: nessuna adiacenza
                Sub("LFFF_S_CTR", null),             // senza poligono: saltato
            }),
        };

        var r = _sut.ComputeImport(domestic, foreign, thresholdNm: 8.0);

        var cand = Assert.Single(r.Candidates);
        Assert.Equal("LIRR", cand.HomeAccCode);
        Assert.Equal("LFFF", cand.ForeignAccCode);
        Assert.Equal("LFFF_CTR", cand.ForeignRootCallsign);
        Assert.Equal(1, cand.AdjacentSectorCount);                       // solo LFFF_CTR
        Assert.Equal(new[] { "LFFF_CTR" }, cand.AdjacentForeignCallsigns);
        Assert.Equal(new[] { "LIRR_CTR" }, cand.AdjacentHomeCallsigns);
        Assert.NotNull(cand.MinDistanceNm);
        Assert.True(cand.MinDistanceNm < 0.1);                            // bordi combacianti
        Assert.Single(r.Hits);
    }

    [Fact]
    public void ComputeImport_foreign_catalog_holds_only_confining_subcenters()
    {
        var domestic = new[] { new DomesticSectorPoly("LIRR", "LIRR_CTR", SquareA) };
        var foreign = new[]
        {
            new ForeignAccData("LFFF", "France", "FR", new[]
            {
                Sub("LFFF_CTR", SquareB_Touching),   // confina → nel catalogo
                Sub("LFFF_N_CTR", SquareD_Far),      // non confina → NON nel catalogo
            }),
        };

        var r = _sut.ComputeImport(domestic, foreign, thresholdNm: 8.0);

        var acc = Assert.Single(r.ForeignCatalog);
        Assert.Equal("LFFF", acc.Code);
        var sub = Assert.Single(acc.Subcenters);
        Assert.Equal("LFFF_CTR", sub.ComposePosition);
    }

    [Fact]
    public void ComputeImport_no_adjacency_yields_no_candidates_no_catalog()
    {
        var domestic = new[] { new DomesticSectorPoly("LIRR", "LIRR_CTR", SquareA) };
        var foreign = new[] { new ForeignAccData("LFFF", "France", "FR", new[] { Sub("LFFF_CTR", SquareD_Far) }) };

        var r = _sut.ComputeImport(domestic, foreign, thresholdNm: 8.0);

        Assert.Empty(r.Candidates);
        Assert.Empty(r.ForeignCatalog);
        Assert.Empty(r.Hits);
    }

    [Fact]
    public void ComputePairDetail_warns_when_foreign_has_no_boundary_polygon()
    {
        var domestic = new[] { new DomesticSectorPoly("LIRR", "LIRR_CTR", SquareA) };
        var foreignSubs = new[] { Sub("LFFF_APP", SquareB_Touching) };   // solo APP: nessun confine

        var detail = _sut.ComputePairDetail("LIRR", "LFFF", domestic, foreignSubs, thresholdNm: 8.0);

        Assert.Empty(detail.Adjacencies);
        Assert.NotEmpty(detail.Warnings);
    }

    [Fact]
    public void ComputePairDetail_reports_adjacency_for_touching_boundary()
    {
        var domestic = new[] { new DomesticSectorPoly("LIRR", "LIRR_CTR", SquareA) };
        var foreignSubs = new[] { Sub("LFFF_CTR", SquareB_Touching) };

        var detail = _sut.ComputePairDetail("LIRR", "LFFF", domestic, foreignSubs, thresholdNm: 8.0);

        var a = Assert.Single(detail.Adjacencies);
        Assert.Equal("LIRR_CTR", a.HomeSector);
        Assert.Equal("LFFF_CTR", a.ForeignSector);
    }

    private static SourceSubcenter Sub(string compose, string? polygon) =>
        new(compose, compose.Split('_')[0], null, null, null, polygon);
}

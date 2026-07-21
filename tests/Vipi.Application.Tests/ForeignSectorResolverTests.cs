using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione della risoluzione sorgente del settore estero aggiunto a mano: dispatch aeroporto vs
/// center, not-found → null, mapping dei campi (freq/poligono dal dettaglio; CenterId = ACC estero scelto).</summary>
public class ForeignSectorResolverTests
{
    [Fact]
    public async Task Airport_position_found_maps_frequency_and_polygon_from_detail()
    {
        var details = new FakeDetails
        {
            Positions = { ["LGKR"] = new() { new SourceAtcPosition("LGKR_APP", "119.100", Position: "APP", AtcCallsign: "Kerkyra Approach") } },
            Detail = { ["LGKR_APP"] = new SourceAtcPosition("LGKR_APP", "119.100", Position: "APP", RegionMapPolygon: "[[19.9,39.6]]", LowerLimit: 0, UpperLimit: 19500) },
        };
        var sut = new ForeignSectorResolver(new FakeDir(), details);

        var r = await sut.ResolveAsync(ForeignSectorCallsign.Parse("LGKR_APP"), centerId: "LGGG");

        Assert.NotNull(r);
        Assert.Equal("LGKR_APP", r!.ComposePosition);
        Assert.Equal("LGGG", r.CenterId);           // agganciato all'ACC estero della coppia, non al FIR reale
        Assert.Equal("APP", r.Position);
        Assert.Equal("119.100", r.Frequency);
        Assert.Equal("[[19.9,39.6]]", r.RegionMapPolygon);
        Assert.Equal("Kerkyra Approach", r.AtcCallsign);
        Assert.Equal(19500, r.UpperLimit);
    }

    [Fact]
    public async Task Airport_position_absent_returns_null()
    {
        var details = new FakeDetails { Positions = { ["LGKR"] = new() { new SourceAtcPosition("LGKR_TWR", "118.7", Position: "TWR") } } };
        var sut = new ForeignSectorResolver(new FakeDir(), details);

        var r = await sut.ResolveAsync(ForeignSectorCallsign.Parse("LGKR_APP"), centerId: "LGGG");

        Assert.Null(r);   // il callsign chiesto non è tra le postazioni pubblicate → non trovato
    }

    [Fact]
    public async Task Center_subcenter_found_uses_directory()
    {
        var dir = new FakeDir { Subs = { ["LGGG"] = new() {
            new SourceSubcenter("LGGG_N_CTR", "LGGG", "CTR", null, "133.000", "[[1,2]]", "Athinai Control") } } };
        var sut = new ForeignSectorResolver(dir, new FakeDetails());

        var r = await sut.ResolveAsync(ForeignSectorCallsign.Parse("LGGG_N_CTR"), centerId: "LGGG");

        Assert.NotNull(r);
        Assert.Equal("LGGG_N_CTR", r!.ComposePosition);
        Assert.Equal("CTR", r.Position);
        Assert.Equal("133.000", r.Frequency);
        Assert.Equal("[[1,2]]", r.RegionMapPolygon);
    }

    [Fact]
    public async Task Center_subcenter_absent_returns_null()
    {
        var sut = new ForeignSectorResolver(new FakeDir(), new FakeDetails());
        var r = await sut.ResolveAsync(ForeignSectorCallsign.Parse("LGGG_N_CTR"), centerId: "LGGG");
        Assert.Null(r);
    }

    private sealed class FakeDir : IAccDirectory
    {
        public Dictionary<string, List<SourceSubcenter>> Subs = new(StringComparer.OrdinalIgnoreCase);
        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceSubcenter>>(Subs.TryGetValue(accIcao, out var s) ? s : new());
        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeDetails : IAirportDetailProvider
    {
        public Dictionary<string, List<SourceAtcPosition>> Positions = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SourceAtcPosition> Detail = new(StringComparer.OrdinalIgnoreCase);
        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAtcPosition>>(Positions.TryGetValue(icao, out var p) ? p : new());
        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default) =>
            Task.FromResult(Detail.TryGetValue(composePosition, out var d) ? d : null);
        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default) => throw new NotImplementedException();
    }
}

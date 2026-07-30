using Vipi.Application.Aor;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione del parsing puro del callsign estero aggiunto a mano (confinanti).</summary>
public class ForeignSectorCallsignTests
{
    [Theory]
    [InlineData("LGKR_APP", "LGKR", "APP", ForeignSectorKind.Airport)]
    [InlineData("lgkr_dep", "LGKR", "DEP", ForeignSectorKind.Airport)]   // normalizza a maiuscolo
    [InlineData("LGKR_TWR", "LGKR", "TWR", ForeignSectorKind.Airport)]
    [InlineData("LGGG_N_CTR", "LGGG", "CTR", ForeignSectorKind.Center)]  // 3 pezzi: prende primo e ultimo
    [InlineData("LGGG_FSS", "LGGG", "FSS", ForeignSectorKind.Center)]
    [InlineData("  LGKR_APP  ", "LGKR", "APP", ForeignSectorKind.Airport)] // trim
    public void Parse_extracts_icao_suffix_and_kind(string raw, string icao, string suffix, ForeignSectorKind kind)
    {
        var r = ForeignSectorCallsign.Parse(raw);
        Assert.Equal(raw.Trim().ToUpperInvariant(), r.Callsign);
        Assert.Equal(icao, r.Icao);
        Assert.Equal(suffix, r.Suffix);
        Assert.Equal(kind, r.Kind);
    }

    [Theory]
    [InlineData("")]              // vuoto
    [InlineData("   ")]           // solo spazi
    [InlineData("LGKR")]          // manca il suffisso
    [InlineData("LG_APP")]        // ICAO troppo corto
    [InlineData("LGKRX_APP")]     // ICAO troppo lungo
    [InlineData("LGKR_XYZ")]      // suffisso ignoto
    public void Parse_rejects_malformed(string raw)
    {
        Assert.Throws<ValidationException>(() => ForeignSectorCallsign.Parse(raw));
    }
}

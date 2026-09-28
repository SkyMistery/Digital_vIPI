using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.Shared.Tests;

/// <summary>I nomi che un <c>.geo</c> usa senza definirli (manuale IVAO del sector), lotto «Subito» slice 4.</summary>
public sealed class NomiDeiColoriDelGeoTests
{
    [Theory]
    [InlineData("TAXI_CENTER", "TAXIWAYCENTER")]
    [InlineData("taxi_center", "TAXIWAYCENTER")]
    [InlineData("STOPLINE", "STOPBAR")]
    [InlineData("APPRON", "APRON")]
    [InlineData("PROHIBIT", "PROHIBITED")]
    [InlineData("RESTRICT", "RESTRICTED")]
    [InlineData(" COAST ", "COAST")]
    public void OgniNomePortaAllaSuaChiaveDelloSchema(string nome, string chiave)
        => Assert.Equal(chiave, NomiDeiColoriDelGeo.Chiave(nome));

    [Theory]
    [InlineData("GRASS")]      // di colors.def, non dello schema
    [InlineData("#406230")]
    [InlineData("")]
    [InlineData(null)]
    public void GliAltriNonSonoPredefiniti(string? nome)
        => Assert.Null(NomiDeiColoriDelGeo.Chiave(nome));
}

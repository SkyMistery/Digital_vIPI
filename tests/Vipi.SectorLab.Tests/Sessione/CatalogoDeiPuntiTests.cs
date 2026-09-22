using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>
/// Il catalogo dei punti, uno per <c>.isc</c> (carta F3, slice 3): quello che risolve un vertice scritto per nome
/// (<c>AMSOR;AMSOR;</c>) quando la mappa lo deve disegnare.
/// </summary>
public sealed class CatalogoDeiPuntiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi()
        => CatalogoDeiPunti.PerOgniIsc(SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!));

    [Fact]
    public void UnCatalogoPerOgniMaster()
    {
        var cataloghi = Cataloghi();
        Assert.Equal(["ITALY.isc", "LIRR.isc"], cataloghi.Keys.Order(StringComparer.Ordinal));
        Assert.True(cataloghi["ITALY.isc"].Punti > 100, $"punti {cataloghi["ITALY.isc"].Punti}");
    }

    [Fact]
    public void UnVorSiTrovaColSuoNomeEColleCoordinateDelFile()
    {
        var punto = Cataloghi()["ITALY.isc"].Cerca("GRO");

        Assert.NotNull(punto);
        Assert.Equal("vor", punto.Value.Catalogo);
        Assert.Equal("SectorFiles/Include/IT/NAVAIDS/itvor.vor", punto.Value.File);
        Assert.Equal(42.76, punto.Value.Posizione.LatitudeDeg, 2);
    }

    [Fact]
    public void LeMaiuscoleNonContano()
        => Assert.True(Cataloghi()["ITALY.isc"].Risolve("gro"));

    [Fact]
    public void UnMasterRisolveSoloQuelCheCARICA()
    {
        var cataloghi = Cataloghi();

        // LIRR.isc carica APT.fix e basta: i VOR non li vede, e i suoi nomi non si risolvono.
        Assert.True(cataloghi["ITALY.isc"].Risolve("GRO"));
        Assert.False(cataloghi["LIRR.isc"].Risolve("GRO"));
        Assert.True(cataloghi["LIRR.isc"].Risolve("BC404"));
    }

    [Fact]
    public void GliScaliDiUnFileDiAirportEntranoColLoroIcao()
    {
        var punto = Cataloghi()["ITALY.isc"].Cerca("LIRF");

        Assert.NotNull(punto);
        Assert.Equal("scalo", punto.Value.Catalogo);
    }

    [Fact]
    public void ITreNomiCheNonSiRISOLVONOSonoQuelliCheDiceIlValidatore()
    {
        // Sul master del 22 settembre 2026 questi tre sono errori veri del sector (carta F2 §10): un refuso, un nome
        // che non c'è, e un VOR con le coordinate rotte che nessun catalogo può contenere.
        var italy = Cataloghi()["ITALY.isc"];
        Assert.False(italy.Risolve("ALPHA SUOTH"));
        Assert.False(italy.Risolve("MAFRE"));
        Assert.False(italy.Risolve("KPT"));
    }

    [Fact]
    public void IFileCaricatiSonoQuelliCitatiEQuelliPerIcao()
    {
        var italy = Cataloghi()["ITALY.isc"];

        Assert.Contains("SectorFiles/Include/IT/NAVAIDS/itvor.vor", italy.FileCaricati);
        // Per ICAO, senza che nessuno lo citi: itap.ap dichiara LIRF, e lirf.sid entra da sé.
        Assert.Contains("SectorFiles/Include/IT/lirf.sid", italy.FileCaricati);
        Assert.DoesNotContain("SectorFiles/Include/IT/LEGGIMI.md", italy.FileCaricati);
    }
}

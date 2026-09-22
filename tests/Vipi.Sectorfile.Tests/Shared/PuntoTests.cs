using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Tests.Shared;

/// <summary>
/// Carta F2, slice 4: il tipo unico del punto — coordinate o nome. I nomi sono quelli veri del sector (master
/// <c>7e761aa</c>): con gli spazi, i trattini, il punto in coda, e i due nomi diversi dei refusi.
/// </summary>
public sealed class PuntoTests
{
    [Fact]
    public void LeCoordinateSonoUnPuntoPerCoordinate()
    {
        var punto = Punto.Leggi("N041.13.55.000", "E014.47.19.000");
        Assert.False(punto.PerNome);
        Assert.Equal(CoordinateConverter.ParsePair("N041.13.55.000", "E014.47.19.000"), punto.Posizione);
    }

    [Theory]
    [InlineData("AMSOR")]          // DYNAMIC_SEC/libb_es_ctr.tfl
    [InlineData("NILTO")]          // comincia con N, ma non con N e una cifra
    [InlineData("CAPO FERRATO")]   // lied.sid
    [InlineData("IAF2-14")]
    [InlineData("NOVEMBER.")]
    public void UnNomeEUnPuntoPerNome(string nome)
    {
        var punto = Punto.Leggi(nome, nome);
        Assert.True(punto.PerNome);
        Assert.Equal(nome, punto.Nome);
        Assert.Equal(nome, punto.NomeLongitudine);
        Assert.Equal(nome + ";" + nome + ";", punto.Riga());
    }

    // Aurora prende la latitudine dal primo nome e la longitudine dal secondo: riscriverne uno solo sposterebbe
    // il punto. Il refuso resta com'è; dirlo è compito del validatore.
    [Fact]
    public void DueNomiDiversiRestanoDue()
    {
        var punto = Punto.Leggi("ALPHA SOUTH", "ALPHA SUOTH");
        Assert.Equal("ALPHA SOUTH;ALPHA SUOTH;", punto.Riga());
    }

    [Fact]
    public void DueNomiSiRisolvonoComeInAurora()
    {
        var catalogo = new Catalogo { ["MC905"] = new Coordinate(40, 9), ["MC904"] = new Coordinate(41, 10) };
        Assert.True(Punto.Leggi("MC905", "MC904").TryRisolvi(catalogo, out var posizione));
        Assert.Equal(new Coordinate(40, 10), posizione);
    }

    [Fact]
    public void UnNomeFuoriDalCatalogoNonSiRisolve()
    {
        Assert.False(Punto.Nominato("UTENO").TryRisolvi(new Catalogo(), out _));
        Assert.False(Punto.Nominato("UTENO").TryRisolvi(null, out _));
    }

    // Una coordinata che non si legge resta una coordinata sbagliata: non diventa un punto chiamato così.
    [Theory]
    [InlineData("N047.44.75.000", "E011.00.00.000")]   // NAVAIDS/itvor.vor: minuti 75
    [InlineData("N047.42.27.000", "E017.04.60.000")]   // DYNAMIC_SEC/lovv.tfl:48: secondi 60
    [InlineData("N041.00.00.000", "AMSOR")]            // coordinata e nome mescolati
    [InlineData("41.00850773", "E016.00.00.000")]       // decimale e DMS mescolati
    [InlineData("", "AMSOR")]
    [InlineData("//AMSOR", "AMSOR")]
    public void NonEUnPunto(string lat, string lon)
    {
        Assert.Throws<CoordinateParseException>(() => Punto.Leggi(lat, lon));
        Assert.False(Punto.TryLeggi(lat, lon, out _));
    }

    [Fact]
    public void UnaCoordinataDiventaUnPunto()
    {
        Punto punto = new Coordinate(41, 12);
        Assert.Equal("N041.00.00.000;E012.00.00.000;", punto.Riga());
    }

    private sealed class Catalogo : Dictionary<string, Coordinate>, IFixResolver
    {
        public bool TryResolve(string ident, out Coordinate position) => TryGetValue(ident, out position);
    }
}

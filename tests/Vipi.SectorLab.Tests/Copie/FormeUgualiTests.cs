using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Le famiglie di forme (lotto «Subito» slice 8a, «file per file» D5, I2): la stessa forma in più record, confrontata
/// come ANELLO — inizio e verso qualsiasi, coordinate scritte in qualunque forma.
/// </summary>
public sealed class FormeUgualiTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";
    private const string Confine = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/zzzz.str";
    private const string Geo = "SectorFiles/Include/IT/GEO/prova.geo";
    private const string Pol = "SectorFiles/Include/IT/GND_LAYOUT/prova.pol";

    private readonly AlberoDiProva _albero = new();

    public FormeUgualiTests()
    {
        // Un quadrato: A B C D.
        _albero.Scrivi(Settore, """
            LZZZ_APP;APP;1;APP;1;
            N041.00.00.000;E012.00.00.000;
            N041.10.00.000;E012.00.00.000;
            N041.10.00.000;E012.10.00.000;
            N041.00.00.000;E012.10.00.000;
            """.ReplaceLineEndings("\r\n"));
        // Lo stesso quadrato partendo da C e girando all'indietro (C B A D), e chiuso ripetendo C.
        _albero.Scrivi(Confine, """
            T;ZZ CONF;N041.10.00.000;E012.10.00.000;
            T;ZZ CONF;N041.10.00.000;E012.00.00.000;
            T;ZZ CONF;N041.00.00.000;E012.00.00.000;
            T;ZZ CONF;N041.00.00.000;E012.10.00.000;
            T;ZZ CONF;N041.10.00.000;E012.10.00.000;
            """.ReplaceLineEndings("\r\n"));
        _albero.Scrivi(Pol, """
            STATIC;GRASS;1;GRASS;
            N045.00.00.000;E009.00.00.000;
            N045.00.10.000;E009.00.00.000;
            N045.00.10.000;E009.00.10.000;
            N045.00.00.000;E009.00.10.000;
            """.ReplaceLineEndings("\r\n"));
        // Il bordo dello stesso poligono, a segmenti: quattro record, una linea.
        _albero.Scrivi(Geo, """
            N045.00.00.000;E009.00.00.000;N045.00.10.000;E009.00.00.000;BUILDING;
            N045.00.10.000;E009.00.00.000;N045.00.10.000;E009.00.10.000;BUILDING;
            N045.00.10.000;E009.00.10.000;N045.00.00.000;E009.00.10.000;BUILDING;
            N045.00.00.000;E009.00.10.000;N045.00.00.000;E009.00.00.000;BUILDING;
            """.ReplaceLineEndings("\r\n"));
    }

    public void Dispose() => _albero.Dispose();

    private FormeUguali Indice()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return FormeUguali.Di(StratiDellaMappa.DiSessione(sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]));
    }

    [Fact]
    public void LoStessoAnelloDaUnAltroVerticeEAllIndietroEUguale()
    {
        var (parte, copie) = Assert.Single(Indice().Di(Settore, 0));

        Assert.Equal((Settore, 0, 0), (parte.File, parte.Record, parte.Parte));
        var copia = Assert.Single(copie);
        Assert.Equal(Confine, copia.Dove.File);
        Assert.True(copia.Uguale);
        Assert.Equal((0, 0), (copia.SoloQui, copia.SoloLa));
    }

    [Fact]
    public void LeCoordinateCompatteEQuelleColPuntoSonoLoStessoVertice()
    {
        _albero.Scrivi(Confine, """
            T;ZZ CONF;N0411000000;E0121000000;
            T;ZZ CONF;N0411000000;E0120000000;
            T;ZZ CONF;N0410000000;E0120000000;
            T;ZZ CONF;N0410000000;E0121000000;
            """.ReplaceLineEndings("\r\n"));

        Assert.True(Assert.Single(Assert.Single(Indice().Di(Settore, 0)).Copie).Uguale);
    }

    [Fact]
    public void UnVerticeSpostatoFaUnaCopiaSimileNonUguale()
    {
        // Dieci vertici; nell'ATZ del MAPS uno è spostato: 9 su 10 in comune, la soglia.
        string Anello(int spostato) => string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"N041.{i:00}.00.000;E012.{(i % 2 == 0 ? 0 : 5) + (i == spostato ? 1 : 0):00}.00.000;\r\n"));
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\n" + Anello(-1));
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + Anello(4));

        var copia = Assert.Single(Assert.Single(Indice().Di(Settore, 0)).Copie, c => c.Dove.File == Mappe);

        Assert.False(copia.Uguale);
        Assert.Equal((1, 1, 10), (copia.SoloQui, copia.SoloLa, copia.Vertici));
    }

    [Fact]
    public void SottoINoveDecimiNonSonoParenti()
    {
        string Anello(int spostati) => string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"N041.{i:00}.00.000;E012.{(i % 2 == 0 ? 0 : 5) + (i < spostati ? 1 : 0):00}.00.000;\r\n"));
        _albero.Scrivi(Settore, "LZZZ_APP;APP;1;APP;1;\r\n" + Anello(0));
        _albero.Scrivi(Mappe, "ZZZZ;MAPS;ZZZZ CTR;;;;;1;\r\n" + Anello(2));

        Assert.DoesNotContain(Indice().Di(Settore, 0).SelectMany(p => p.Copie), c => c.Dove.File == Mappe);
    }

    [Fact]
    public void IlBordoDelGeoEIlRiempimentoDelPolSonoLaStessaForma()
    {
        var indice = Indice();

        Assert.True(Assert.Single(Assert.Single(indice.Di(Pol, 0)).Copie).Uguale);
        // Ogni segmento del .geo vale per la sua linea: la scheda del terzo trova il poligono come quella del primo.
        var dalTerzo = Assert.Single(indice.Di(Geo, 2));
        Assert.Equal(0, dalTerzo.Parte.Record);
        Assert.Equal(Pol, Assert.Single(dalTerzo.Copie).Dove.File);
    }

    [Fact]
    public void LeFamiglieRaccolgonoLeFormeUguali()
    {
        // I campioni dell'albero di prova hanno le loro: qui contano le due scritte sopra.
        var famiglie = Indice().Famiglie();

        Assert.Contains(famiglie, f => f.Select(p => p.File).Order().SequenceEqual([Settore, Confine]));
        Assert.Contains(famiglie, f => f.Select(p => p.File).Order().SequenceEqual([Geo, Pol]));
    }

    [Fact]
    public void UnaLineaDiDuePuntiNonEUnaForma()
    {
        _albero.Scrivi(Geo, "N045.00.00.000;E009.00.00.000;N045.00.10.000;E009.00.00.000;BUILDING;\r\n");

        Assert.Empty(Indice().Di(Geo, 0));
    }
}

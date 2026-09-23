using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// Da record a forme per la mappa (carta F3, slice 3): punti, linee, aree, coi nomi risolti nel catalogo del master.
/// </summary>
public sealed class GeometriaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, CatalogoDeiPunti Catalogo) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]);
    }

    private IReadOnlyList<FormaDellaMappa> Forme(string relativo)
    {
        var (sessione, catalogo) = Apri();
        return Geometria.DelFile(sessione.File["SectorFiles/Include/IT/" + relativo], catalogo);
    }

    // F3-bis (committente, 23 settembre): in lirn.str la STAR 06(ALL) ricollegava ogni STAR alla successiva. Il <br> di
    // una mappa per nome, o mista, spezza la linea come quello delle zone a coordinate.
    [Theory]
    [InlineData("BC404;BC404;<br>", "BC406;BC406;1A;", "BC408;BC408;<br>", "BC408;BC408;2A;", "BC404;BC404;")]
    [InlineData("BC404;BC404;", "BC406;BC406;", "N039.00.00.000;E017.00.00.000;<br>", "N039.10.00.000;E017.10.00.000;")]
    public void IlBrDiUnaMappaPerNomeSpezzaLaLinea(params string[] corpo)
    {
        _albero.Scrivi("SectorFiles/Include/IT/zzzz.str", string.Join("\r\n", ["ZZZZ;MAPS;STAR (ALL);;;;;1;", .. corpo]) + "\r\n");

        var forma = Assert.Single(Forme("zzzz.str"));

        Assert.Equal(TipoDiForma.Linea, forma.Tipo);
        Assert.Equal(2, forma.Tratti.Count);
        Assert.Empty(forma.NomiNonRisolti);
    }

    [Fact]
    public void UnFixEUnPunto()
    {
        var forma = Forme("NAVAIDS/APT.fix").First(f => f.Etichetta == "BC404");

        Assert.Equal(TipoDiForma.Punto, forma.Tipo);
        Assert.Equal(1, forma.Punti);
        Assert.Empty(forma.NomiNonRisolti);
    }

    [Fact]
    public void ISegmentiDiUnGeoSiCUCIONOInPolilinee()
    {
        // Tre segmenti attaccati (la fine dell'uno è l'inizio dell'altro) e uno staccato: due polilinee, non quattro.
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo", """
            N041.00.00.000;E012.00.00.000;N041.01.00.000;E012.00.00.000;COAST;
            N041.01.00.000;E012.00.00.000;N041.02.00.000;E012.00.00.000;COAST;
            N041.02.00.000;E012.00.00.000;N041.03.00.000;E012.00.00.000;COAST;
            N042.00.00.000;E013.00.00.000;N042.01.00.000;E013.00.00.000;COAST;
            """.ReplaceLineEndings("\r\n"));

        var forme = Forme("GEO/prova.geo");

        Assert.Equal(2, forme.Count);
        Assert.Equal(4, forme[0].Punti);
        Assert.Equal(2, forme[1].Punti);
        Assert.All(forme, f => Assert.Equal(TipoDiForma.Linea, f.Tipo));
    }

    [Fact]
    public void UnSettoreEUnAreaEIVerticiPerNomeSiRisolvono()
    {
        _albero.Scrivi("SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl", """
            PROVA_CTR;CTR;COLOR_Sector;1;COLOR_Line;0;
            N041.00.00.000;E012.00.00.000;
            GRO;GRO;
            N042.00.00.000;E013.00.00.000;
            """.ReplaceLineEndings("\r\n"));

        var forma = Assert.Single(Forme("DYNAMIC_SEC/prova.tfl"));

        Assert.Equal(TipoDiForma.Area, forma.Tipo);
        Assert.Equal(3, forma.Punti);
        Assert.Empty(forma.NomiNonRisolti);
        // Il vertice di mezzo è il VOR di Grosseto, preso dal catalogo.
        Assert.Equal(42.76, forma.Tratti[0][1].LatitudeDeg, 2);
    }

    [Fact]
    public void UnaRigaVuotaNelTracciatoDiUnaSidSpezzaIlTratto()
    {
        // Come `NORTH DEP16` di lied.sid (campione vero): una riga vuota FRA due punti spezza il tratto e non chiude
        // la SID (carta F2, slice 4). Qui il caso è esatto: tre punti, poi una riga vuota, poi due.
        _albero.Scrivi("SectorFiles/Include/IT/prova.sid", """
            LIRF;16;PROVA DEP16; ; ;1;
            N041.00.00.000;E012.00.00.000;
            N041.01.00.000;E012.00.00.000;
            N041.02.00.000;E012.00.00.000;

            N042.00.00.000;E013.00.00.000;
            N042.01.00.000;E013.00.00.000;
            """.ReplaceLineEndings("\r\n"));

        var forma = Assert.Single(Forme("prova.sid"));

        Assert.Equal(2, forma.Tratti.Count);
        Assert.Equal(3, forma.Tratti[0].Count);
        Assert.Equal(2, forma.Tratti[1].Count);
    }

    [Fact]
    public void ILiedSidVeriHannoTracciatiSpezzati()
    {
        // Il campione vero, a conferma che il caso costruito sopra esiste davvero nel sector.
        Assert.Contains(Forme("lied.sid"), f => f.Tratti.Count > 1);
    }

    [Fact]
    public void UnNomeCheNonSiRisolveLASCIALaFormaEDiceIlNome()
    {
        // 🔴 Il difetto della slice 3b: la forma senza punti spariva, e con lei il motivo. Caso vero: in limn.str
        // «IAF HITAC35» cita un fix che nessun catalogo conosce, ed è un errore del sector (carta F2 §10).
        _albero.Scrivi("SectorFiles/Include/IT/prova.str", """
            LIRF;35;IAF-INESISTENTE; ; ;3;
            NONCESONO;NONCESONO;
            """.ReplaceLineEndings("\r\n"));

        var forma = Assert.Single(Forme("prova.str"));

        Assert.Empty(forma.Tratti);
        Assert.Equal(0, forma.Punti);
        Assert.Equal(["NONCESONO", "NONCESONO"], forma.NomiNonRisolti);
    }

    [Fact]
    public void UnAttesaSenzaIlSuoFixNonFiniscePerErroreANordDellAfrica()
    {
        // Senza il fix il punto non si disegna: una coordinata «vuota» sarebbe 0,0, nel golfo di Guinea.
        _albero.Scrivi("SectorFiles/Include/IT/prova.hold", "HLD-XXXXX;NONCESONO;NONCESONO;XXXXX/225R-9000;\r\n");

        var forma = Assert.Single(Forme("prova.hold"));

        Assert.Empty(forma.Tratti);
        Assert.Contains("NONCESONO", forma.NomiNonRisolti);
    }

    [Fact]
    public void UnaPistaEUnaLineaFraLeDueSoglie()
    {
        var forma = Forme("OTHER/lirr.rw").First();

        Assert.Equal(TipoDiForma.Linea, forma.Tipo);
        Assert.Equal(2, forma.Punti);
    }

    [Fact]
    public void IFileCheIlMotoreNonInterpretaNonHannoGeometria()
    {
        var (sessione, catalogo) = Apri();
        Assert.Empty(Geometria.DelFile(sessione.File["SectorFiles/Include/IT/LEGGIMI.md"], catalogo));
    }

    [Fact]
    public void SenzaCatalogoIPuntiPerCoordinateSiDisegnanoLoSTESSO()
    {
        var (sessione, _) = Apri();
        var forme = Geometria.DelFile(sessione.File["SectorFiles/Include/IT/NAVAIDS/itvor.vor"], catalogo: null);

        Assert.NotEmpty(forme);
        Assert.All(forme, f => Assert.Equal(TipoDiForma.Punto, f.Tipo));
    }
}

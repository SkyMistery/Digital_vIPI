using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Le piste (lotto «Subito» slice 7d, R-1): chi usa un verso — le SID, le voci dei .str, le mappe «RWY16L», i tag, i
/// PAR dei .cpr e i commenti dei disegni — e la rinomina, che riscrive le prime e ELENCA le altre (da cambiare a mano).
/// </summary>
public sealed class RinominaDellePisteTests : IDisposable
{
    private const string Rw = "SectorFiles/Include/IT/OTHER/prova.rw";
    private const string Copia = "SectorFiles/Include/IT/OTHER/copia.rw";
    private const string Sid = "SectorFiles/Include/IT/lxxx.sid";
    private const string Str = "SectorFiles/Include/IT/lxxx.str";
    private const string Cpr = "SectorFiles/Include/IT/PREFS/LXXX.cpr";
    private const string Geo = "SectorFiles/Include/IT/GEO/lxxx.geo";

    private readonly AlberoDiProva _albero = new();

    public RinominaDellePisteTests()
    {
        const string Pista = "LXXX;16L;34R;14;6;158.7;338.7;N041.50.45.490;E012.15.41.380;N041.48.44.800;E012.16.31.890;\r\n";
        // Il lettore del .rw legge le piste solo sotto l'intestazione //PISTE (con le barre che vuole).
        _albero.Scrivi(Rw, "//PISTE\r\n" + Pista + "LXXX;16R;34L;7;8;158.7;338.7;N041.48.55.860;E012.13.34.910;N041.46.55.180;E012.14.25.450;\r\n");
        _albero.Scrivi(Copia, "///////PISTE\r\n" + Pista);
        _albero.Scrivi(Sid, "LXXX;16L;ABC1A;;;;;1;\r\nLXXX;34R;ABC1B;;;;;1;\r\nLXXX;16R;ABC1C;;;;;1;\r\n");
        _albero.Scrivi(Str, "LXXX;16L:16R;ELKA3A;;;;;1;\r\nELKAP;ELKAP;3A;\r\nBIBEK;BIBEK;\r\n\r\n"
                            + "LXXX;MAPS;RWY16L;;;4;\r\nN041.51.42.708;E012.15.17.318;\r\nN041.52.39.925;E012.14.53.244;\r\n");
        _albero.Scrivi(Cpr, "PAR_VERTICAL_SCAN=30\r\nINS1PAR_CAPTION=LXXX RWY16L/3.0°\r\nINS2PAR_CAPTION=LXXX RWY16R/3.0°\r\n");
        _albero.Scrivi(Geo, "//Runway 16L designator\r\nN041.00.00.000;E012.00.00.000;N041.00.01.000;E012.00.01.000;RUNWAY;\r\n");
    }

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, ChiLoUsa Indice, IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, ChiLoUsa.Di(sessione), CatalogoDeiPunti.PerOgniIsc(sessione));
    }

    [Fact]
    public void UnaPistaLaCitanoProcedureMappePareDisegni()
    {
        var (sessione, indice, cataloghi) = Apri();

        var usi = indice.Di(sessione, cataloghi, Rw, 0, _ => [])!;

        Assert.Equal("pista", usi.Catalogo);
        Assert.Equal(["16L", "34R"], usi.Nomi);
        // La SID di 16R no (è l'altra pista); la STAR di 16L:16R sì; la mappa RWY16L; il PAR e il commento da vedere.
        Assert.Equal([(Geo, 1, Piste.DaVedere), (Cpr, 2, Piste.DaVedere), (Sid, 1, "procedura"), (Sid, 2, "procedura"),
                      (Str, 1, "procedura"), (Str, 5, "mappa")],
            usi.Citazioni.Select(c => (c.File, c.Riga, c.Come)));
    }

    [Fact]
    public void LaRinominaRiscriveIlVersoEElencaQuelloDaFareAMano()
    {
        var (sessione, indice, cataloghi) = Apri();
        var modifiche = new ModificheInSospeso();

        var pronta = Assert.IsType<RinominaPronta>(
            Rinomina.Prepara(sessione, indice, cataloghi, Rw, 0, "16L", "16C", null, modifiche.SporchiDi));
        Assert.IsType<ModificaDelTesto>(modifiche.CambiaInPiuFile([.. pronta.PerFile.Select(f => (sessione.File[f.File], f.Righe))], "rinomina"));

        IReadOnlyList<string> Righe(string file) => ((IFileConRecord)sessione.File[file]).RigheDelFile(modifiche.SporchiDi(file));
        Assert.StartsWith("LXXX;16C;34R;", Righe(Rw)[1], StringComparison.Ordinal);
        Assert.StartsWith("LXXX;16R;34L;", Righe(Rw)[2], StringComparison.Ordinal);
        Assert.StartsWith("LXXX;16C;34R;", Righe(Copia)[1], StringComparison.Ordinal);
        Assert.Equal(["LXXX;16C;ABC1A;;;;;1;", "LXXX;34R;ABC1B;;;;;1;", "LXXX;16R;ABC1C;;;;;1;"], Righe(Sid));
        Assert.Equal("LXXX;16C:16R;ELKA3A;;;;;1;", Righe(Str)[0]);
        Assert.Equal("LXXX;MAPS;RWY16C;;;4;", Righe(Str)[4]);
        // Il PAR di 16L e il commento del disegno: da cambiare a mano (non quello di 16R).
        Assert.Equal([(Geo, 1), (Cpr, 2)], pronta.AMano.Select(c => (c.File, c.Riga)));
        Assert.Equal(1, modifiche.Quante);
    }

    [Theory]
    [InlineData("16R", "ha già una pista")]
    [InlineData("37", "due cifre")]
    [InlineData("16X", "due cifre")]
    public void UnVersoCheNonVaSiRifiuta(string nuovo, string perche)
    {
        var (sessione, indice, cataloghi) = Apri();

        var rifiuto = Assert.IsType<ModificaRifiutata>(Rinomina.Prepara(sessione, indice, cataloghi, Rw, 0, "16L", nuovo, null, _ => []));

        Assert.Contains(perche, rifiuto.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void NeiTagCambiaIlNomeDelRecordELeChiaviDelVerso()
        => Assert.Equal("//@\"LXXX 16C/34R\" 16C.tora=3900 34R.tora=3900",
            Piste.Rinomina("//@\"LXXX 16L/34R\" 16L.tora=3900 34R.tora=3900", "LXXX", "16L", "16C"));

    [Fact]
    public async Task DallaSchedaLaRinominaDiceCosaResta()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(lab.RinominaIlPunto(Rw, 0, "16L", "16C"));

        Assert.Equal(2, lab.DaCambiareAMano.Count);
        Assert.Contains("LXXX;16C:16R;ELKA3A;;;;;1;", lab.RigheDiAdesso(Str));

        // Una pista usata non si toglie.
        Assert.False(lab.TogliRecord(Rw, 1));
        Assert.Contains("È usato", lab.Rifiuto, StringComparison.Ordinal);
    }
}

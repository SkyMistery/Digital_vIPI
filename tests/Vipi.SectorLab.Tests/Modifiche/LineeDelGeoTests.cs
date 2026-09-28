using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// La vista a linea dei <c>.geo</c> (lotto «Subito», slice 5d, «file per file» G1 e R-3): i segmenti attaccati di fila sono
/// una linea di punti; un punto spostato riscrive i due segmenti che lo toccano; la riga vuota spezza e riunisce.
/// </summary>
public sealed class LineeDelGeoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private FileAperto Liap()
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/GEO/liap.geo"];

    private IReadOnlyList<string> Righe(FileAperto file)
        => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    [Fact]
    public void ISegmentiAttaccatiSonoUnaLineaDiPunti()
    {
        // liap.geo, «//fence»: segmenti BUILDING uno dopo l'altro, la fine di ognuno è l'inizio del prossimo.
        var file = Liap();

        var linea = _modifiche.LineaDi(file, 0)!;

        Assert.True(linea.Record.Count > 3);
        Assert.Equal(linea.Record.Count + 1, linea.Punti.Count);
        var segmenti = linea.Record.Select(r => (Line)((IFileConRecord)file).RecordDelModello[r]).ToList();
        Assert.Equal(segmenti[0].Start, linea.Punti[0]);
        Assert.All(Enumerable.Range(0, segmenti.Count), k => Assert.Equal(segmenti[k].End, linea.Punti[k + 1]));
        Assert.Equal(linea.Record, _modifiche.LineaDi(file, linea.Record[2])!.Record);   // da ogni segmento, la stessa linea
    }

    [Fact]
    public void SpostareUnPuntoInMezzoRiscriveIDueSegmentiCheLoToccano()
    {
        var file = Liap();
        var prima = _modifiche.LineaDi(file, 0)!;

        _modifiche.CambiaPuntoDellaLinea(file, 0, 2, "N042.22.20.000 E013.18.44.000");

        var diff = _modifiche.DiffDi(file);
        Assert.Equal(2, diff.Tolte);
        Assert.Equal(2, diff.Aggiunte);
        var dopo = _modifiche.LineaDi(file, 0)!;
        Assert.Equal(prima.Record, dopo.Record);   // la catena non si è rotta
        Assert.Equal("N042.22.20.000 E013.18.44.000", LineeDelGeo.Scrivi(dopo.Punti[2]));
    }

    [Fact]
    public void IlPrimoPuntoToccaUnSegmentoSolo()
    {
        var file = Liap();

        _modifiche.CambiaPuntoDellaLinea(file, 0, 0, "N042.22.24.000 E013.18.27.000");

        Assert.Equal(1, _modifiche.DiffDi(file).Tolte);
    }

    [Fact]
    public void UnaCoordinataIllegibileSiRifiutaSenzaToccareNiente()
    {
        var file = Liap();

        Assert.IsType<ModificaRifiutata>(_modifiche.CambiaPuntoDellaLinea(file, 0, 2, "PIPPO"));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void SpezzareMetteLaRigaVuotaEUnireLaToglie()
    {
        var file = Liap();
        var linea = _modifiche.LineaDi(file, 0)!;
        var prima = Righe(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.SpezzaLaLinea(file, 0, 3));

        var spezzata = _modifiche.LineaDi(file, 0)!;
        Assert.Equal(3, spezzata.Record.Count);
        Assert.Equal(linea.Record[3], spezzata.Dopo);

        Assert.IsType<ModificaDelTesto>(_modifiche.UnisciLaLinea(file, 0, dopo: true));
        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void NonSiSpezzaAllEstremita()
    {
        var file = Liap();
        var linea = _modifiche.LineaDi(file, 0)!;

        Assert.IsType<ModificaRifiutata>(_modifiche.SpezzaLaLinea(file, 0, 0));
        Assert.IsType<ModificaRifiutata>(_modifiche.SpezzaLaLinea(file, 0, linea.Record.Count));
    }

    [Fact]
    public void UnRecordCheNonEUnSegmentoNonHaLinea()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

        Assert.Null(_modifiche.LineaDi(sessione.File["SectorFiles/Include/IT/lied.sid"], 0));
    }
}

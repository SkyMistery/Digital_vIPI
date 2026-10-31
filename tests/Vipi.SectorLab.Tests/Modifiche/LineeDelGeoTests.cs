using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

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
    public void Lo_stesso_editor_dei_vertici_aggiunge_e_toglie_un_segmento()
    {
        // D5 della carta del riordino: «+» mette una copia del punto (un segmento in più, da spostare), «−» lo toglie (i due
        // segmenti che lo toccavano diventano uno), e la catena non si rompe.
        var file = Liap();
        var prima = _modifiche.LineaDi(file, 0)!;
        var righe = Righe(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Aggiungi, 2));
        var dopo = _modifiche.LineaDi(file, 0)!;
        Assert.Equal(prima.Punti.Count + 1, dopo.Punti.Count);
        Assert.Equal(prima.Punti[2], dopo.Punti[2]);
        Assert.Equal(prima.Punti[2], dopo.Punti[3]);
        // I segmenti che non cambiano restano le loro righe: le due prima del punto sono quelle di prima.
        Assert.Equal(righe.Take(2), Righe(file).Take(2));

        Assert.IsNotType<ModificaRifiutata>(_modifiche.CambiaPuntoDellaLinea(file, 0, 3, "N042.22.20.000 E013.18.44.000"));
        Assert.Equal("N042.22.20.000 E013.18.44.000", LineeDelGeo.Scrivi(_modifiche.LineaDi(file, 0)!.Punti[3]));

        Assert.IsType<ModificaDelTesto>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Togli, 3));
        Assert.Equal(prima.Punti, _modifiche.LineaDi(file, 0)!.Punti);
        Assert.Equal(prima.Record, _modifiche.LineaDi(file, 0)!.Record);
        Assert.Equal(righe.Count, Righe(file).Count);
    }

    [Fact]
    public void Inverti_e_incolla_riscrivono_la_linea_e_una_linea_tiene_due_punti()
    {
        var file = Liap();
        var prima = _modifiche.LineaDi(file, 0)!;

        Assert.IsType<ModificaDelTesto>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Inverti));
        Assert.Equal(prima.Punti.Reverse(), _modifiche.LineaDi(file, 0)!.Punti);
        Assert.Equal(prima.Record.Count, _modifiche.LineaDi(file, 0)!.Record.Count);

        Coordinate a = new(42.37, 13.31), b = new(42.38, 13.32), c = new(42.39, 13.31);
        Assert.IsType<ModificaDelTesto>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Incolla, incollati: [a, b, c]));
        var incollata = _modifiche.LineaDi(file, 0)!;
        Assert.Equal(2, incollata.Record.Count);
        Assert.Equal(new[] { a, b, c }.Select(LineeDelGeo.Scrivi), incollata.Punti.Select(LineeDelGeo.Scrivi));

        Assert.IsType<ModificaDelTesto>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Togli, 1));
        var rifiuto = Assert.IsType<ModificaRifiutata>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Togli, 0));
        Assert.Contains("due punti", rifiuto.Motivo, StringComparison.Ordinal);
        Assert.IsType<ModificaRifiutata>(_modifiche.GestoSullaLinea(file, 0, GestoSullaLinea.Incolla, incollati: [a]));
    }

    [Fact]
    public void UnRecordCheNonEUnSegmentoNonHaLinea()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

        Assert.Null(_modifiche.LineaDi(sessione.File["SectorFiles/Include/IT/lied.sid"], 0));
    }
}

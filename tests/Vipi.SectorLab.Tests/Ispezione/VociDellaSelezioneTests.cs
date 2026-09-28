using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// Le voci come la finestra di selezione di Aurora (lotto «Subito», slice 6, «file per file» A3, J1, B1, E1, H3): una
/// voce per nome, anche in più pezzi, e le parti col nome dal commento sopra.
/// </summary>
public sealed class VociDellaSelezioneTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private (FileAperto File, IReadOnlyList<VoceDellaSelezione> Voci) Voci(string relativo)
    {
        var file = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/" + relativo];
        var conRecord = (IFileConRecord)file;
        return (file, VociDellaSelezione.Di(file, conRecord.RigheDelFile([]), conRecord.PostiDeiRecord([]))!);
    }

    [Fact]
    public void FraArtccHaISeiGruppiDellaAccSelectionPiuLeEtichette()
    {
        var (_, voci) = Voci("ACC/FRA.artcc");

        Assert.Equal(["FRA BDRY", "LIMITROFI", "NPZ", "AOCC MM", "AOCC PP", "AOCC RR", "Etichette (L)"], voci.Select(v => v.Nome));
    }

    [Fact]
    public void LePartiPrendonoIlNomeDalCommentoSopra()
    {
        var (_, voci) = Voci("ACC/FRA.artcc");

        var fra = voci.Single(v => v.Nome == "FRA BDRY");
        Assert.Equal(["FRA IT - Zona A", "FRA IT - Zona B", "PADOVA TUTTA da sud a nord", "MILANO-ROMA NE", "confine ROMA-BRINDISI"],
            fra.Parti.Select(p => p.Nome));
        // Fra più commenti di fila, l'ultimo corto che non è un titolo: il nome, non la descrizione sotto.
        Assert.Equal(["LINPZ1 VEKEN", "LINPZ2 FIRENZE"], voci.Single(v => v.Nome == "NPZ").Parti.Select(p => p.Nome));
    }

    [Fact]
    public void IlNomeInCodaAlSeparatoreEIlNomeDellaParteDopo()
    {
        // FRA.artcc: `T;DUMMY;N041.24.56.000;E018.18.07.000; //brindisi/tirana` e sotto il pezzo verso Tirana.
        var (_, voci) = Voci("ACC/FRA.artcc");

        Assert.Contains("brindisi/tirana", voci.Single(v => v.Nome == "LIMITROFI").Parti.Select(p => p.Nome));
    }

    [Fact]
    public void LaRigaDelNomeEIlCommentoNelFile()
    {
        var (file, voci) = Voci("ACC/FRA.artcc");
        var righe = ((IFileConRecord)file).RigheDelFile([]);

        var zonaA = voci.Single(v => v.Nome == "FRA BDRY").Parti[0];

        Assert.Equal("//FRA IT - Zona A", righe[zonaA.RigaDelNome!.Value - 1]);
        Assert.Equal($"{zonaA.Record}.0", zonaA.Chiave);
    }

    [Fact]
    public void UnaVoceInPiuPezziEUnaSola()
    {
        // lirr.hartcc: 13 voci come la finestra di Aurora; RR CONF2 in due poligoni coi loro nomi.
        var (_, voci) = Voci("HI_AIRSPACE/lirr.hartcc");

        Assert.Equal(13, voci.Count);
        Assert.Equal(2, voci.Single(v => v.Nome == "RR CONF2").Parti.Count);
    }

    [Fact]
    public void UnAeroviaEUnaVoceCoiSuoiPezziSenzaIBreak()
    {
        var (_, voci) = Voci("AIRWAY/itawlow.lairway");

        Assert.DoesNotContain(voci, v => v.Nome == "BREAK");
        Assert.True(voci.Single(v => v.Nome == "L615").Record.Count >= 2);
    }

    [Fact]
    public void NeiGeoLaVoceEIlGruppoSottoUnCommento()
    {
        var (_, voci) = Voci("GEO/liap.geo");

        Assert.Equal("fence", voci[0].Nome);
        Assert.All(voci, v => Assert.Empty(v.Parti));
        Assert.Equal(((IFileConRecord)Voci("GEO/liap.geo").File).RecordDelModello.Count, voci.Sum(v => v.Record.Count));
    }

    [Theory]
    [InlineData("Percorso senza titolo", true)]
    [InlineData("Poligono senza titolo", true)]
    [InlineData(null, true)]
    [InlineData("fence", false)]
    public void SenzaTitoloEUnNomeMancante(string? nome, bool mancante)
        => Assert.Equal(mancante, VociDellaSelezione.SenzaTitolo(nome));

    [Fact]
    public void NelleAreePRDLaVoceEIlNomeDellArea()
    {
        var (_, voci) = Voci("GEO/italy.danger");

        Assert.All(voci, v => Assert.False(v.NomeMancante));
        Assert.Equal(voci.Count, voci.Select(v => v.Nome).Distinct().Count());
    }

    [Fact]
    public void UnFileSenzaFinestraDiSelezioneNonHaVoci()
    {
        var file = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/lied.sid"];

        Assert.Null(VociDellaSelezione.Di(file, ((IFileConRecord)file).RigheDelFile([]), ((IFileConRecord)file).PostiDeiRecord([])));
    }

    [Fact]
    public void LaSchedaDiceQuantiPuntiHannoIPoligoniNonIlNomeDelTipo()
    {
        // Visto a schermo (slice 6): «Poligoni» si leggeva «Vipi.Sectorfile.Models.StaticBoundaryPolygon, …».
        var (file, voci) = Voci("ACC/FRA.artcc");
        int limitrofi = voci.Single(v => v.Nome == "LIMITROFI").Record[0];

        var campo = Ispettore.Scheda(file, limitrofi, null)!.Campi.Single(c => c.Nome == "Polygons");

        Assert.DoesNotContain("Vipi.", campo.Valore, StringComparison.Ordinal);
        Assert.Contains(" punti", campo.Valore, StringComparison.Ordinal);
    }

    [Fact]
    public void OgniTrattoDiUnConfineSaDiQualePoligonoE()
    {
        var file = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/HI_AIRSPACE/lirr.hartcc"];

        var conf2 = Geometria.DelFile(file, null).Single(f => f.Etichetta == "RR CONF2");

        Assert.Equal([0, 1], conf2.Parti);
    }
}

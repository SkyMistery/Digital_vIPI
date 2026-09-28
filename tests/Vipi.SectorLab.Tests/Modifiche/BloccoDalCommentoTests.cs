using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// La regola commento/blocco di §M (lotto «Subito», slice 6c): un pezzo senza nome nelle righe di dati (un gruppo di un
/// <c>.geo</c>) che riceve il primo metadato passa da commento a blocco, col nome del commento, e il commento resta sopra.
/// </summary>
public sealed class BloccoDalCommentoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private FileAperto Apri(string relativo)
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/" + relativo];

    private IReadOnlyList<string> Righe(FileAperto file)
        => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    private static VoceDellaSelezione Fence(FileAperto file)
        => VociDellaSelezione.Di(file, ((IFileConRecord)file).RigheDelFile([]), ((IFileConRecord)file).PostiDeiRecord([]))![0];

    [Fact]
    public void IlPrimoMetadatoScriveIlBloccoColNomeDelCommento()
    {
        var file = Apri("GEO/liap.geo");
        var fence = Fence(file);
        int record = fence.Record[2];

        var fatta = _modifiche.CambiaIlMetadato(file, record, "note", "recinzione");

        Assert.IsType<ModificaDelMetadato>(fatta);
        var righe = Righe(file).ToList();
        int commento = righe.IndexOf("//fence");
        Assert.Equal(["//fence", "//@\"fence\" note=recinzione", "//@START"], righe.Skip(commento).Take(3));
        Assert.Contains("//@END \"fence\"", righe);
        var conRecord = (IFileConRecord)file;
        Assert.Null(conRecord.TagRotti());
        // Il blocco tiene tutto il gruppo: anche il primo e l'ultimo segmento hanno la nota.
        Assert.Equal("recinzione", conRecord.ChiaviDi(fence.Record[0])!["note"]);
        Assert.Equal("recinzione", conRecord.ChiaviDi(fence.Record[^1])!["note"]);
    }

    [Fact]
    public void ToltoLUltimoMetadatoIlFileTornaComEra()
    {
        var file = Apri("GEO/liap.geo");
        int record = Fence(file).Record[0];
        var prima = Righe(file);

        _modifiche.CambiaIlMetadato(file, record, "note", "recinzione");
        _modifiche.CambiaIlMetadato(file, record, "note", null);

        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void NellaSchedaIMetadatiDelSegmentoOraSiScrivono()
    {
        var file = Apri("GEO/liap.geo");

        var metadati = MetadatiDellaScheda.Di(file, Fence(file).Record[0]);

        Assert.Contains(metadati, m => m.Chiave == "note" && m.SiScrive);
    }

    [Fact]
    public void UnGruppoSenzaTitoloNonDaIlNomeAlBlocco()
    {
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo", string.Join("\r\n",
            "//Percorso senza titolo",
            "N042.22.24.448;E013.18.27.278;N042.22.17.909;E013.18.42.313;BUILDING;",
            "N042.22.17.909;E013.18.42.313;N042.22.18.000;E013.18.43.785;BUILDING;",
            ""));
        var file = Apri("GEO/prova.geo");

        var esito = _modifiche.CambiaIlMetadato(file, 0, "note", "prova");

        Assert.Contains("nome vero", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
        Assert.Contains(MetadatiDellaScheda.Di(file, 0), m => m.Chiave == "note" && !m.SiScrive);
    }
}

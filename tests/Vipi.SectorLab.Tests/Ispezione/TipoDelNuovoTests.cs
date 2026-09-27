using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>«+ Nuovo record» chiede il tipo fisso prima di tutto (lotto «Subito», slice 3e; voce A1).</summary>
public sealed class TipoDelNuovoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();
    private readonly SessioneAperta _sessione;

    public TipoDelNuovoTests() => _sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    public void Dispose() => _albero.Dispose();

    private FileAperto File(string relativo) => _sessione.File["SectorFiles/Include/IT/" + relativo];

    private static IReadOnlyList<object> Record(FileAperto file) => ((IFileConRecord)file).RecordDelModello;

    [Fact]
    public void UnArtccChiedeEtichettaOTracciaColSignificato()
    {
        var scelta = TipoDelNuovo.Di(File("ACC/FRA.artcc"))!;

        Assert.Null(scelta.Proprieta);
        Assert.Equal(["L", "T"], scelta.Valori.Select(v => v.Valore));
        Assert.All(scelta.Valori, v => Assert.Contains("·", v.Voce, StringComparison.Ordinal));
    }

    [Fact]
    public void UnFixChiedeIlSuoTipoSenzaIlVuoto()
    {
        var scelta = TipoDelNuovo.Di(File("NAVAIDS/APT.fix"))!;

        Assert.Equal(nameof(Fix.DisplayType), scelta.Proprieta);
        Assert.Equal(["0", "1", "2", "3"], scelta.Valori.Select(v => v.Valore));
    }

    [Fact]
    public void UnoScaloNonProponeIlTipoFuoriManualeEUnNdbNonChiedeNiente()
    {
        Assert.DoesNotContain(TipoDelNuovo.Di(File("OTHER/itap.ap"))!.Valori, v => v.Valore == nameof(InstallationType.Custom));
        Assert.Null(TipoDelNuovo.Di(File("NAVAIDS/itndb.ndb")));
    }

    [Fact]
    public void UnEtichettaNuovaDaUnaTracciaSiCopiaDaUnEtichettaEVaSottoLaTraccia()
    {
        var file = File("ACC/FRA.artcc");
        int traccia = Record(file).Select((r, i) => (r, i)).First(v => v.r is StaticBoundaryGroup).i;

        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, traccia, tipo: "L"));

        Assert.Equal(traccia + 1, _modifiche.UltimoAggiunto);
        Assert.IsType<LabelPoint>(Record(file)[traccia + 1]);
        Assert.IsType<StaticBoundaryGroup>(Record(file)[traccia]);
    }

    [Fact]
    public void UnFixNuovoPrendeIlTipoSceltoEIlSuoNome()
    {
        var file = File("NAVAIDS/APT.fix");
        int bc404 = Ispettore.Etichette(file, null).ToList().IndexOf("BC404");

        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(file, bc404, nome: "BC405", tipo: "1"));

        var nuovo = Assert.IsType<Fix>(Record(file)[_modifiche.UltimoAggiunto!.Value]);
        Assert.Equal(("BC405", (int?)1), (nuovo.Name, nuovo.DisplayType));
        Assert.Contains(((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)),
            r => r.StartsWith("BC405;N039.05.11.290;E017.03.27.750;1;", StringComparison.Ordinal));
    }

    [Fact]
    public void UnSegmentoNuovoPrendeIlTipoScelto()
    {
        var file = File("GEO/liap.geo");

        _modifiche.AggiungiRecord(file, 0, tipo: "TAXI_CENTER");

        Assert.Equal("TAXI_CENTER", Assert.IsType<Line>(Record(file)[_modifiche.UltimoAggiunto!.Value]).Color);
    }

    [Fact]
    public void NellElencoUnEtichettaLSiChiamaColSuoFixENonColNomeDellaClasse()
    {
        var etichette = Ispettore.Etichette(File("ACC/FRA.artcc"), null);

        Assert.DoesNotContain("LabelPoint", etichette);
        Assert.Equal("ABDAB", etichette[0]);
    }

    [Fact]
    public void UnTipoCheIlFileNonHaSiRifiuta()
    {
        var file = File("NAVAIDS/APT.fix");

        Assert.IsType<ModificaRifiutata>(_modifiche.AggiungiRecord(file, 0, nome: "BC405", tipo: "9"));
        Assert.IsType<ModificaRifiutata>(_modifiche.AggiungiRecord(File("NAVAIDS/itndb.ndb"), 0, tipo: "1"));
        Assert.False(_modifiche.CEQualcosa);
    }
}

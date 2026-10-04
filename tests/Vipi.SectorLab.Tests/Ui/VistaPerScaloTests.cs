using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Lotto «Subito» slice 12d («file per file» I6, O1): la vista per scalo. Scelto uno scalo, tutto quello che lo riguarda
/// da qualunque file — la riga dell'.ap, le piste, le posizioni, i file col suo nome, i disegni e i riempimenti (anche
/// quelli che non ne portano il nome: si riconoscono da dove stanno) e le marcature per pista. In una linguetta sua.
/// </summary>
public sealed class VistaPerScaloTests : IDisposable
{
    private const string It = "SectorFiles/Include/IT/";

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public VistaPerScaloTests()
    {
        _albero.Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nN039.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" +
            "[AIRPORT]\r\nF;OTHER\\prova.ap\r\n[RUNWAY]\r\nF;OTHER\\prova.rw\r\n[ATC]\r\nF;OTHER\\prova.frq\r\n");
        _albero.Scrivi(It + "OTHER/prova.ap", "LIXA;14;6000;N039.48.01.000;E012.14.20.000;PROVA A;\r\nLIXB;48;6000;N038.25.54.000;E014.11.13.000;PROVA B;\r\n");
        _albero.Scrivi(It + "OTHER/prova.rw", "//PISTE\r\n" +
            "LIXA;16L;34R;14;6;159;339;N039.50.45.490;E012.15.41.380;N039.48.44.800;E012.16.31.890;\r\n" +
            "LIXB;04;22;48;40;040;220;N038.25.30.000;E014.10.50.000;N038.26.20.000;E014.11.40.000;\r\n");
        _albero.Scrivi(It + "OTHER/prova.frq", "LIXA_TWR;118.700;LIXA;;;0;\r\nLIXB_TWR;119.100;LIXB;;;0;\r\nLIXA_GND;121.900;LIXA;;;0;\r\n");
        _albero.Scrivi(It + "lixa.gts", "101;LIXA;N039.48.16.944;E012.14.18.365;\r\n");
        _albero.Scrivi(It + "lixa.sid", "LIXA;16L;PROVA1A;;;;;\r\nPROVA;PROVA;\r\n");
        // Il disegno dello scalo, col suo nome: dentro, una parte di marcature che nomina la pista.
        _albero.Scrivi(It + "GEO/lixa.geo",
            "//Taxiway A\r\nN039.48.00.000;E012.14.00.000;N039.48.00.000;E012.15.00.000;TAXI_CENTER;\r\n" +
            "//threshold marks rw 34R\r\nN039.48.06.000;E012.14.00.000;N039.48.06.000;E012.15.00.000;RUNWAY;\r\n");
        // Riempimenti e marcature senza il nome dello scalo: sul sedime di LIXA.
        _albero.Scrivi(It + "GND_LAYOUT/xa_ad_gnd.pol",
            "STATIC;GRASS;1;GRASS;\r\nN039.48.00.000;E012.14.00.000;\r\nN039.48.00.000;E012.15.00.000;\r\nN039.49.00.000;E012.15.00.000;\r\n");
        _albero.Scrivi(It + "RW_MARKINGS/xa_mark.geo",
            "//designator rw 16L\r\nN039.48.06.000;E012.14.00.000;N039.48.06.000;E012.15.00.000;RUNWAY;\r\n" +
            "//Runway stripes (1)\r\nN039.48.07.000;E012.14.00.000;N039.48.07.000;E012.15.00.000;RUNWAY;\r\n" +
            "//designator rw 34R\r\nN039.48.08.000;E012.14.00.000;N039.48.08.000;E012.15.00.000;RUNWAY;\r\n");
        // E quelli di LIXB, che in LIXA non devono comparire.
        _albero.Scrivi(It + "RW_MARKINGS/xb_mark.geo",
            "//designator rw 04\r\nN038.25.50.000;E014.11.00.000;N038.25.50.000;E014.11.30.000;RUNWAY;\r\n");

        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    private IReadOnlyList<VoceDelloScalo> Voci(string sezione) => _lab.VistaDelloScalo().SingleOrDefault(s => s.Id == sezione)?.Voci ?? [];

    [Fact]
    public async Task GliScaliSonoQuelliDegliAp_UnaVoltaCiascuno()
    {
        await Apri();
        var scali = _lab.ScaliDellaVista();

        Assert.Contains(new ScaloDellaVista("LIXA", "PROVA A"), scali);
        Assert.Contains(new ScaloDellaVista("LIXB", "PROVA B"), scali);
        Assert.Equal(scali.Count, scali.Select(s => s.Icao).Distinct().Count());
        Assert.Empty(_lab.VistaDelloScalo());
    }

    [Fact]
    public async Task ScegliendoUnoScaloSiVedeTuttoQuelCheLoRiguarda_ESoloIlSuo()
    {
        await Apri();
        _lab.ScegliLoScalo("lixa");

        Assert.Equal(new VoceDelloScalo("LIXA PROVA A", It + "OTHER/prova.ap", 0, "prova.ap"), Assert.Single(Voci("scalo")));
        Assert.Equal(new VoceDelloScalo("16L/34R", It + "OTHER/prova.rw", 0, "prova.rw"), Assert.Single(Voci("piste")));
        Assert.Equal(["LIXA_TWR", "LIXA_GND"], Voci("posizioni").Select(v => v.Titolo));
        Assert.Equal([0, 2], Voci("posizioni").Select(v => v.Record!.Value));
        Assert.Equal(["lixa.gts", "lixa.sid"], Voci("file").Select(v => v.Titolo));
        Assert.All(Voci("file"), v => Assert.Null(v.Record));

        // Il suo .geo per nome, i riempimenti e le marcature per posizione; quelli di LIXB no.
        Assert.Equal(["lixa.geo", "xa_ad_gnd.pol", "xa_mark.geo"], Voci("terra").Select(v => v.Titolo));
    }

    [Fact]
    public async Task LeMarcatureSonoPerPistaEPerParte_EUnaParteSenzaPistaEDellaPistaDiPrima()
    {
        await Apri();
        _lab.ScegliLoScalo("LIXA");

        // 16L: il numero e le strisce (che non nominano la pista: sono della 16L, nominata prima).
        var della16 = Voci("marcature:16L");
        Assert.Equal(["designator rw 16L", "Runway stripes (1)"], della16.Select(v => v.Titolo));
        Assert.Equal([0, 1], della16.Select(v => v.Record!.Value));
        Assert.All(della16, v => Assert.EndsWith("xa_mark.geo", v.File, StringComparison.Ordinal));

        // 34R: dal file delle marcature e dal .geo dello scalo; «Taxiway A» non è una marcatura.
        var della34 = Voci("marcature:34R");
        Assert.Equal(2, della34.Count);
        Assert.Contains(della34, v => v.File.EndsWith("lixa.geo", StringComparison.Ordinal) && v.Record == 1 && v.Nota == "lixa.geo:3");
        Assert.DoesNotContain(_lab.VistaDelloScalo(), s => s.Voci.Any(v => v.Titolo.Contains("Taxiway", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LaVistaSegueLeModifiche()
    {
        await Apri();
        _lab.ScegliLoScalo("LIXA");
        Assert.Equal(2, Voci("posizioni").Count);

        Assert.True(_lab.CambiaCampo(It + "OTHER/prova.frq", 1, "Code", "LIXA_APP"));

        Assert.Equal(["LIXA_TWR", "LIXA_APP", "LIXA_GND"], Voci("posizioni").Select(v => v.Titolo));
    }

    [Fact]
    public async Task ASchermo_LaLinguettaScali_UnRecordSiSceglieEUnFileSiApreInSfoglia()
    {
        await Apri();
        var colonna = _contesto.RenderComponent<ColonnaDiSinistra>();

        colonna.Find("[data-linguetta='scali']").Click();
        colonna.Find("[data-campo='scalo']").Change("LIXA");
        colonna.WaitForAssertion(() => Assert.NotEmpty(colonna.FindAll("[data-sezione-scalo='marcature:16L']")));
        Assert.NotEmpty(colonna.FindAll("[data-sezione-scalo='piste']"));

        // Un record: si sceglie, e la colonna resta sugli scali.
        colonna.Find($"[data-voce-scalo='{It}OTHER/prova.rw#0']").Click();
        Assert.Equal((It + "OTHER/prova.rw", 0), _lab.Scelta);
        Assert.NotEmpty(colonna.FindAll("[data-campo='scalo']"));

        // Un file: si apre in Sfoglia, dove stanno i suoi record.
        colonna.Find($"[data-voce-scalo='{It}lixa.gts']").Click();
        colonna.WaitForAssertion(() => Assert.Contains("lab-linguetta-scelta", colonna.Find("[data-linguetta='sfoglia']").ClassName ?? "", StringComparison.Ordinal));
        Assert.Equal(It + "lixa.gts", _lab.FileScelto);
    }
}

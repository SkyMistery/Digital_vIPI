using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Dalle prove del committente del 23 settembre (sera): (b)+7 una riga scritta a mano — `BC;518;…` salvata in APT.fix
/// non era più un record e non si correggeva dal Lab —, 6 un fix nuovo in ordine alfabetico e dal file, (a) la vista.
/// </summary>
public sealed class RigaAManoOrdineEVistaTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";
    private const string Apt = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public RigaAManoOrdineEVistaTests()
    {
        // Il caso vero del committente: una riga col «;» di troppo nel nome, che il lettore salta.
        _albero.Scrivi(Prova, "//LIBC\r\nBC404;N039.05.11.290;E017.03.27.750;3;\r\nBC;518;N039.05.09.890;E017.12.02.940;3;\r\nBC621;N038.47.00.430;E016.54.47.900;3;\r\n//@BC621 nota=si");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    private async Task Apri() => Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

    private int Record(string file) => _lab.Sessione!.File[file].Record;

    [Fact]
    public async Task UnaRigaIllegibileCorrettaAManoDiventaUnRecord()
    {
        await Apri();
        Assert.Equal(2, Record(Prova));

        Assert.True(_lab.CambiaRigaAMano(Prova, 3, "BC518;N039.05.09.890;E017.12.02.940;3;"));

        Assert.Equal(3, Record(Prova));
        Assert.Equal((Prova, 1), _lab.Scelta);
        Assert.Equal("BC518", _lab.Scheda()!.Campi.Single(c => c.Nome == "Name").Valore);
        var voce = Assert.IsType<ModificaDelTesto>(Assert.Single(_lab.Modifiche.Voci));
        Assert.Contains("riga 3", voce.Descrizione);
        var diff = _lab.DiffDi(Prova);
        Assert.Contains(diff.Pezzi.SelectMany(p => p.Righe), r => r.Testo.StartsWith("BC;518", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnnullareLaRigaAManoRimetteIlFileDellApertura()
    {
        await Apri();
        Assert.True(_lab.CambiaRigaAMano(Prova, 3, "BC518;N039.05.09.890;E017.12.02.940;3;"));

        _lab.AnnullaModifica(_lab.Modifiche.Voci.Single());

        Assert.Equal(2, Record(Prova));
        Assert.Empty(_lab.DiffDi(Prova).Pezzi);
        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task CtrlZAnnullaAncheLaRigaAMano()
    {
        await Apri();
        Assert.True(_lab.CambiaRigaAMano(Prova, 3, "BC518;N039.05.09.890;E017.12.02.940;3;"));

        _lab.Annulla();

        Assert.Equal(2, Record(Prova));
        Assert.Equal(0, _lab.Modifiche.Quante);

        _lab.Ripeti();

        Assert.Equal(3, Record(Prova));
    }

    [Theory]
    [InlineData(5, "//@BC621 nota=no")]
    [InlineData(2, "//@BC404 nota=si")]
    public async Task ITagNonSiScrivonoAMano(int riga, string testo)
    {
        await Apri();

        Assert.False(_lab.CambiaRigaAMano(Prova, riga, testo));

        Assert.Contains("//@", _lab.Rifiuto);
        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task LeModificheCheCeranoEntranoNelTesto()
    {
        await Apri();
        Assert.True(_lab.CambiaCampo(Prova, 0, "Position", "N041.00.00.000 E012.00.00.000"));

        Assert.True(_lab.CambiaRigaAMano(Prova, 3, "BC518;N039.05.09.890;E017.12.02.940;3;"));

        // Una voce sola, ma il diff ha tutt'e due le righe.
        Assert.IsType<ModificaDelTesto>(Assert.Single(_lab.Modifiche.Voci));
        var aggiunte = _lab.DiffDi(Prova).Pezzi.SelectMany(p => p.Righe).Where(r => r.Segno == SegnoDelDiff.Aggiunta).Select(r => r.Testo).ToList();
        Assert.Contains(aggiunte, r => r.StartsWith("BC404;N041.00.00.000;E012.00.00.000", StringComparison.Ordinal));
        Assert.Contains(aggiunte, r => r.StartsWith("BC518;", StringComparison.Ordinal));

        // Annullando torna tutto com'era sul disco, anche il valore del campo nel modello.
        _lab.AnnullaModifica(_lab.Modifiche.Voci.Single());
        _lab.Scegli(Prova, 0);
        Assert.Equal("N039.05.11.290 E017.03.27.750", _lab.Scheda()!.Campi.Single(c => c.Nome == "Position").Valore);
        Assert.Empty(_lab.DiffDi(Prova).Pezzi);
    }

    [Fact]
    public async Task SalvataLaRigaAManoVaSulDisco()
    {
        await Apri();
        Assert.True(_lab.CambiaRigaAMano(Prova, 3, "BC518;N039.05.09.890;E017.12.02.940;3;"));

        await _lab.SalvaAsync(confermato: true);

        string scritto = File.ReadAllText(Path.Combine(_albero.Radice, Prova.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("\r\nBC518;N039.05.09.890;E017.12.02.940;3;\r\n", scritto);
        Assert.DoesNotContain("BC;518", scritto);
        Assert.Equal(3, Record(Prova));
    }

    [Fact]
    public async Task UnFixNuovoVaInOrdineAlfabeticoNellaSuaSezione()
    {
        await Apri();

        Assert.True(_lab.AggiungiAlFile(Apt, "BD430"));

        var etichette = _lab.EtichetteDi(Apt);
        int nuovo = etichette.ToList().IndexOf("BD430");
        Assert.Equal((Apt, nuovo), _lab.Scelta);
        Assert.True(string.CompareOrdinal(etichette[nuovo - 1], "BD430") < 0);
        Assert.True(string.CompareOrdinal(etichette[nuovo + 1], "BD430") > 0);
        Assert.StartsWith("BD", etichette[nuovo - 1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrimoDellaSezioneStaSottoLIntestazione()
    {
        await Apri();
        int bd423 = _lab.EtichetteDi(Apt).ToList().IndexOf("BD423");

        Assert.True(_lab.AggiungiRecord(Apt, bd423, "BD100"));

        var righe = _lab.RigheDiAdesso(Apt).ToList();
        int intestazione = righe.IndexOf("//LIBD");
        Assert.StartsWith("BD100;", righe[intestazione + 1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BC;999")]
    [InlineData(" BC999")]
    public async Task UnNomeCheNonVaSiRifiuta(string nome)
    {
        await Apri();
        int quanti = Record(Apt);

        Assert.False(_lab.AggiungiAlFile(Apt, nome));

        Assert.Equal(quanti, Record(Apt));
        Assert.NotNull(_lab.Rifiuto);
    }

    [Fact]
    public async Task LaVistaMetteEToglieEAccendeLoStrato()
    {
        await Apri();
        Assert.DoesNotContain("punti", _lab.Accesi);

        _lab.CambiaLaVista(Apt, 3);
        _lab.CambiaLaVista(Prova, null);

        Assert.Equal([SessioneDelLab.ChiaveDellaVista(Apt, 3), Prova + "#*"], _lab.InVista);
        Assert.Contains("punti", _lab.Accesi);
        Assert.Equal("prova.fix (tutto)", _lab.NomeInVista(Prova + "#*"));

        _lab.CambiaLaVista(Apt, 3);
        Assert.Single(_lab.InVista);
        _lab.SvuotaLaVista();
        Assert.Empty(_lab.InVista);
    }
}

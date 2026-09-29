using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// Lotto «Subito» slice 11d («file per file» N1): un profilo <c>.cpr</c> si apre come record, un'impostazione per riga
/// con la sua sezione; il valore si scrive e tocca solo la sua riga; la chiave si sceglie fra quelle di un profilo
/// completo di Aurora per quella sezione.
/// </summary>
public sealed class ProfiliNelLabTests : IDisposable
{
    private const string Lipi = "SectorFiles/Include/IT/PREFS/LIPI.cpr";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public ProfiliNelLabTests()
    {
        _albero.Scrivi(Lipi, "PAR_VERTICAL_SCAN=30\r\n[INSET1]\r\nINS1PAR_CAPTION=LIPI RWY06/2.6°\r\nINS1PAR_Radial=55\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    [Fact]
    public async Task LImpostazioneHaLaSuaSchedaEIlValoreSiScrive()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(["PAR_VERTICAL_SCAN", "INS1PAR_CAPTION", "INS1PAR_Radial"], _lab.EtichetteDi(Lipi));

        var scheda = Ispettore.Scheda(_lab.Sessione!.File[Lipi], 2, null)!;
        Assert.Equal("Impostazione del profilo", scheda.NomeDelTipo);
        Assert.Equal("INSET1", scheda.Campi.Single(c => c.Nome == "Sezione").Scritto);
        Assert.Contains("Sovrascrive", scheda.Campi.Single(c => c.Nome == "Valore").Descrizione!.Significato, StringComparison.Ordinal);
        Assert.DoesNotContain(scheda.Campi, c => c.Sconosciuto);

        Assert.True(_lab.CambiaCampo(Lipi, 2, "Valore", "56"));
        Assert.Equal(["PAR_VERTICAL_SCAN=30", "[INSET1]", "INS1PAR_CAPTION=LIPI RWY06/2.6°", "INS1PAR_Radial=56"], _lab.RigheDiAdesso(Lipi));
    }

    [Fact]
    public async Task LeChiaviSiScelgonoFraQuelleDiAuroraPerLaSezione()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var voci = _lab.Elenchi()!;

        Assert.Contains("INS1PAR_Radial", voci.Voci(FonteDellElenco.ChiaviDelProfilo, "INSET1"));
        Assert.Contains("INS1Par_Elevation", voci.Voci(FonteDellElenco.ChiaviDelProfilo, "INSET1"));
        Assert.DoesNotContain("INS2PAR_Radial", voci.Voci(FonteDellElenco.ChiaviDelProfilo, "INSET1"));
        Assert.Contains("AircraftHorizontal", voci.Voci(FonteDellElenco.ChiaviDelProfilo, "PREFS"));
        // Anche quelle scritte nei .cpr dell'albero (prima della prima sezione: PAR_VERTICAL_SCAN).
        Assert.Contains("PAR_VERTICAL_SCAN", voci.Voci(FonteDellElenco.ChiaviDelProfilo, ""));
    }
}

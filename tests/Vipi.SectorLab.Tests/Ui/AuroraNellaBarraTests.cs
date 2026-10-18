using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta F5, slice 1: la vetrina nella barra — «Porta in Aurora» mette il sector aperto nella cartella che Aurora legge
/// come secondo sector; la barra dice cosa c'è dentro e se Aurora la guarda; i sector recenti a un clic. Le impostazioni
/// di Aurora sono finte, nel temporaneo: il test non legge mai quelle del PC.
/// </summary>
public sealed class AuroraNellaBarraTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly string _aurora;

    public AuroraNellaBarraTests()
    {
        _aurora = Path.Combine(_albero.Radice, "dati-del-lab", "aurora");
        Directory.CreateDirectory(Path.Combine(_aurora, "Settings"));
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"))
        {
            Aurora = (Path.Combine(_aurora, "Settings", "profile.ini"), Path.Combine(_aurora, "SectorFiles")),
        };
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private string Vetrina => Path.Combine(_albero.Radice, "dati-del-lab", "vetrina");

    private void AuroraLegge(string secondo)
        => File.WriteAllText(_lab.Aurora.ProfileIni, $"[CREDS]\r\npassword=NON-SI-LEGGE\r\n\r\n[SECTOR]\r\nSecondSector={secondo}\r\n");

    [Fact]
    public async Task PortaInAurora_LaBarraDiceCosaCe_ESeAuroraLaGuarda()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.Equal("senza vetrina", pagina.Find("[data-in-vetrina]").TextContent.Trim()), TimeSpan.FromSeconds(10));

        // La vetrina nel sector pubblicato di Aurora no; in una cartella nuova sì.
        Assert.False(_lab.CambiaLaVetrina(_lab.Aurora.Pubblicata));
        Assert.True(_lab.CambiaLaVetrina(Vetrina));
        AuroraLegge(@"D:\IVAO\it-aurora-sector\SectorFiles");

        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);

        Assert.True(File.Exists(Path.Combine(Vetrina, "SectorFiles", "ITALY.isc")));
        Assert.Equal(Path.GetFullPath(_albero.Radice), _lab.InVetrina!.Sorgente);
        Assert.False(_lab.AuroraGuardaLaVetrina);
        pagina.WaitForAssertion(() => Assert.Contains("Aurora guarda altrove", pagina.Find("[data-in-vetrina]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));

        // L'AOD punta Aurora alla vetrina, una volta.
        AuroraLegge(Path.Combine(Vetrina, "SectorFiles"));
        Assert.True(_lab.AuroraGuardaLaVetrina);

        // Le modifiche non salvate in Aurora no.
        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", "PIPPO"));
        Assert.False(await _lab.PortaInAuroraAsync());
        Assert.Contains("non salvate", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ISectorApertiSiRicordano()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));

        Assert.Equal([_albero.Radice], _lab.SectorRecenti());
        Assert.Equal([_albero.Radice], new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).SectorRecenti());
    }
}

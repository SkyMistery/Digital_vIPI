using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Disco;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il salvataggio a schermo (carta F3 §2.4, slice 9): il tasto, l'esito, la conferma degli errori nuovi, e il file
/// cambiato sul disco che si ricarica lasciando leggibile il diff perso.
/// </summary>
public sealed class SalvataggioAschermoTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    /// <summary>
    /// Salvare valida, scrive, rilegge e rifà cataloghi e strati: sul runner della CI ci vuole più del secondo che
    /// bUnit aspetta di base (i tre test sono caduti così alla prima corsa, «Check count: 4»; con 20 ms cadono anche in locale).
    /// </summary>
    private static readonly TimeSpan Attesa = TimeSpan.FromSeconds(15);

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public SalvataggioAschermoTests()
    {
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

    private byte[] SulDisco() => File.ReadAllBytes(_albero.Percorso(Fix));

    private async Task<IRenderedComponent<Home>> ConUnFixCambiato(string campo, string valore)
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        await pagina.InvokeAsync(() => _lab.Scegli(forma.File, forma.Record));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll($"[data-scrivi='{campo}']")));
        pagina.Find($"[data-scrivi='{campo}']").Change(valore);
        pagina.WaitForAssertion(() => Assert.Equal(1, _lab.Modifiche.Quante));
        return pagina;
    }

    [Fact]
    public async Task SalvaScriveIlFile_DiceDoveStaIlBackup_ELeModificheSpariscono()
    {
        var pagina = await ConUnFixCambiato("Position", "N041.00.00.000 E012.00.00.000");
        byte[] prima = SulDisco();

        pagina.Find("[data-tasto='salva']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            var esito = pagina.Find("[data-salvataggio]");
            Assert.Equal(nameof(StatoDelSalvataggio.Salvato), esito.GetAttribute("data-salvataggio"));
            Assert.Contains("1 file salvato", esito.TextContent, StringComparison.Ordinal);
            Assert.Contains(Path.Combine(_albero.Radice, "dati-del-lab", "backup"), pagina.Find("[data-backup]").TextContent, StringComparison.Ordinal);
        });
        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Empty(pagina.FindAll("[data-diff]"));
        Assert.NotEqual(prima, SulDisco());
        Assert.Contains("N041.00.00.000", System.Text.Encoding.UTF8.GetString(SulDisco()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnErroreNuovo_SiMostra_ESiSalvaSoloConfermando()
    {
        var pagina = await ConUnFixCambiato("Name", "BC;404");
        byte[] prima = SulDisco();

        pagina.Find("[data-tasto='salva']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            Assert.Equal(nameof(StatoDelSalvataggio.DaConfermare), pagina.Find("[data-salvataggio]").GetAttribute("data-salvataggio"));
            Assert.NotEmpty(pagina.FindAll("[data-problema]"));
        });
        Assert.Equal(prima, SulDisco());

        pagina.Find("[data-tasto='salva-lo-stesso']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
            Assert.Equal(nameof(StatoDelSalvataggio.Salvato), pagina.Find("[data-salvataggio]").GetAttribute("data-salvataggio")));
        Assert.Contains("BC;404", System.Text.Encoding.UTF8.GetString(SulDisco()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnFileCambiatoSulDisco_SiFerma_ERicaricandoLoIlDiffPersoRestaLeggibile()
    {
        var pagina = await ConUnFixCambiato("Position", "N041.00.00.000 E012.00.00.000");
        File.AppendAllText(_albero.Percorso(Fix), "\r\n// da un collega\r\n");
        byte[] delCollega = SulDisco();

        pagina.Find("[data-tasto='salva']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () => Assert.NotEmpty(pagina.FindAll($"[data-conflitto='{Fix}']")));
        Assert.Equal(delCollega, SulDisco());

        pagina.Find($"[data-ricarica='{Fix}']").Click();

        pagina.WaitForAssertion(timeout: Attesa, assertion: () =>
        {
            Assert.False(_lab.Modifiche.CEQualcosa);
            // L'esito fermo se ne va (il conflitto non c'è più), il diff perso resta.
            Assert.Empty(pagina.FindAll("[data-salvataggio]"));
            Assert.Contains("N041.00.00.000", pagina.Find($"[data-perse='{Fix}']").TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(delCollega, SulDisco());
        Assert.Empty(_lab.Sessione!.CambiatiSulDisco());
    }

    [Fact]
    public async Task RiaprireLaCartella_NonSiPortaDietroLeModificheDiPrima()
    {
        await ConUnFixCambiato("Position", "N041.00.00.000 E012.00.00.000");

        Assert.True(await _lab.ApriAsync(_albero.Radice));

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Null(_lab.UltimoSalvataggio);
    }
}

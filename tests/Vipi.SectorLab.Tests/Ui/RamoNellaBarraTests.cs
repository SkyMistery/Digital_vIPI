using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta F4, slice 1: il ramo git nella barra del Lab — il nome e lo stato in parole, che cambia dopo un salvataggio;
/// fuori da git il Lab lo dice e lavora lo stesso.
/// </summary>
public sealed class RamoNellaBarraTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public RamoNellaBarraTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        foreach (string file in Directory.EnumerateFiles(_albero.Radice, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        _albero.Dispose();
    }

    private void Git(params string[] argomenti)
    {
        var esito = _lab.Git.EseguiAsync(_albero.Radice, argomenti).GetAwaiter().GetResult();
        Assert.True(esito.Riuscito, esito.Errore);
    }

    /// <summary>L'albero di prova diventa un clone col suo primo commit (i dati del Lab fuori da git).</summary>
    private void FanneUnRepo()
    {
        Git("init", "--quiet", "--initial-branch=master");
        Git("config", "user.name", "Prova del Lab");
        Git("config", "user.email", "prova@example.invalid");
        Git("config", "core.autocrlf", "false");
        File.WriteAllText(_albero.Percorso(".gitignore"), "dati-del-lab/\n");
        Git("add", "--all");
        Git("commit", "--quiet", "-m", "primo");
    }

    [Fact]
    public async Task IlRamoSiVedeEDopoUnSalvataggioCeUnFileDaRegistrare()
    {
        FanneUnRepo();
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() => Assert.Equal("master", pagina.Find("[data-ramo]").GetAttribute("data-ramo")), TimeSpan.FromSeconds(10));
        // Un clone senza server: niente da mandare né da prendere, ma il ramo non è mai stato mandato.
        Assert.Equal("mai mandato", pagina.Find("[data-stato-del-ramo]").TextContent.Trim());

        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", "PIPPO"));
        await _lab.SalvaAsync();

        pagina.WaitForAssertion(() => Assert.StartsWith("1 file da registrare", pagina.Find("[data-stato-del-ramo]").TextContent.Trim(), StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        Assert.Contains("APT.fix (cambiato)", pagina.Find("[data-ramo]").GetAttribute("title"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrendiIlSector_LoScaricaELoApre()
    {
        // Carta F4, slice 2: l'albero di prova fa da repo del server; il Lab ne prende una copia parziale e la apre.
        FanneUnRepo();
        string dove = Path.Combine(_albero.Radice, "dati-del-lab", "preso", "it-aurora-sector");
        _lab.CambiaLIndirizzoDelSector(_albero.Radice);

        Assert.True(await _lab.PrendiIlSectorAsync(dove));

        Assert.Equal(StatoDelLab.Aperta, _lab.Stato);
        Assert.True(File.Exists(Path.Combine(dove, "SectorFiles", "ITALY.isc")));
        await _lab.RileggiIlRamoAsync();
        Assert.Equal(("master", "pulito"), (_lab.Ramo!.Ramo, _lab.Ramo.InParole()));
        // Una seconda volta nella stessa cartella no: non è più vuota.
        Assert.False(await _lab.PrendiIlSectorAsync(dove));
        Assert.Contains("non è vuota", _lab.Errore, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnLavoroNuovo_IlCicloCheNasce_ENienteCambioDiRamoConFileNonRegistrati()
    {
        // Carta F4, slice 3. Il server è l'albero di prova; il Lab lavora sulla sua copia.
        FanneUnRepo();
        string dove = Path.Combine(_albero.Radice, "dati-del-lab", "preso", "it-aurora-sector");
        _lab.CambiaLIndirizzoDelSector(_albero.Radice);
        Assert.True(await _lab.PrendiIlSectorAsync(dove));
        await _lab.RileggiIlRamoAsync();

        // Senza VID no: il nome del ramo lo chiede.
        Assert.False(await _lab.NuovoLavoroAsync("prova", Vipi.SectorLab.Core.Git.BaseDelLavoro.Immediato));
        Assert.Contains("VID", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.True(_lab.CambiaIlVid("704798"));

        Assert.True(await _lab.NuovoLavoroAsync("Prova delle SID", Vipi.SectorLab.Core.Git.BaseDelLavoro.Immediato));
        await _lab.RileggiIlRamoAsync();
        Assert.Equal(("aod/704798/prova-delle-sid", "master"), (_lab.Ramo!.Ramo, _lab.BaseDelRamo));
        Assert.Equal(StatoDelLab.Aperta, _lab.Stato);

        // Il ciclo dopo non c'è: un lavoro per il ciclo no, finché non nasce.
        Assert.False(_lab.CicloDopoSulServer);
        Assert.False(await _lab.NuovoLavoroAsync("ciclo", Vipi.SectorLab.Core.Git.BaseDelLavoro.CicloDopo));
        Assert.True(await _lab.FaiNascereIlCicloAsync());
        Assert.True(_lab.CicloDopoSulServer);

        // Un file salvato e non registrato: niente cambio di ramo, e il perché.
        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", "PIPPO"));
        Assert.False(await _lab.PassaAlRamoAsync("master"));
        Assert.Contains("non salvate", _lab.Rifiuto, StringComparison.Ordinal);
        await _lab.SalvaAsync();
        await _lab.RileggiIlRamoAsync();
        Assert.False(await _lab.NuovoLavoroAsync("ciclo", Vipi.SectorLab.Core.Git.BaseDelLavoro.CicloDopo));
        Assert.Contains("non registrati", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal("aod/704798/prova-delle-sid", _lab.Ramo!.Ramo);
    }

    [Fact]
    public async Task SalvatoERegistrato_ColMessaggioDaiGesti()
    {
        // Carta F4, slice 4.
        FanneUnRepo();
        string dove = Path.Combine(_albero.Radice, "dati-del-lab", "preso", "it-aurora-sector");
        _lab.CambiaLIndirizzoDelSector(_albero.Radice);
        Assert.True(await _lab.PrendiIlSectorAsync(dove));
        Assert.True(_lab.CambiaIlVid("704798"));
        await _lab.RileggiIlRamoAsync();

        // Su master il Lab non registra.
        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", "PIPPO"));
        await _lab.SalvaAsync();
        Assert.False(await _lab.RegistraAsync("su master"));
        Assert.Contains("non registra su master", _lab.Rifiuto, StringComparison.Ordinal);

        // Il file salvato non si porta in un altro ramo: lo si rimette com'era, poi un lavoro nuovo.
        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", _primoNome));
        await _lab.SalvaAsync();
        await _lab.RileggiIlRamoAsync();
        Assert.True(await _lab.NuovoLavoroAsync("prova", Vipi.SectorLab.Core.Git.BaseDelLavoro.Immediato));
        Assert.Empty(_lab.GestiDaRegistrare);   // un'altra apertura: i gesti di prima non valgono

        Assert.True(_lab.CambiaCampo("SectorFiles/Include/IT/NAVAIDS/APT.fix", 0, "Name", "PIPPO"));
        await _lab.SalvaAsync();
        await _lab.RileggiIlRamoAsync();
        string proposto = _lab.MessaggioProposto!;
        Assert.StartsWith("Nome di ", proposto, StringComparison.Ordinal);
        Assert.EndsWith("File: APT.fix", proposto, StringComparison.Ordinal);

        Assert.True(await _lab.RegistraAsync(proposto));

        Assert.Equal("mai mandato", _lab.Ramo!.InParole());
        Assert.Null(_lab.MessaggioProposto);
        Assert.Empty(_lab.GestiDaRegistrare);
    }

    private string _primoNome => File.ReadLines(_albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix")).First(r => !r.StartsWith("//", StringComparison.Ordinal) && r.Contains(';', StringComparison.Ordinal)).Split(';')[0];

    [Fact]
    public void IlVidSiRicordaESoloCifre()
    {
        Assert.Null(_lab.Vid);
        Assert.False(_lab.CambiaIlVid("SkyMistery"));
        Assert.True(_lab.CambiaIlVid(" 704798 "));

        var dopo = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.Equal("704798", dopo.Vid);
        Assert.Equal(Vipi.SectorLab.Core.Git.PresaDelSector.IndirizzoDiBase, dopo.IndirizzoDelSector);
    }

    [Fact]
    public async Task FuoriDaGitLoDiceELavoraLoStesso()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-ramo-assente]")), TimeSpan.FromSeconds(10));
        Assert.Empty(pagina.FindAll("[data-ramo]"));
        Assert.Null(_lab.Ramo);
    }
}

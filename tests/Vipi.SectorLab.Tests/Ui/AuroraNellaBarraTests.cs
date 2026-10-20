using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components;
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
        // Riordino delle pagine (R1): la vetrina di Aurora sta nella pagina Consegna.
        _lab.ScegliLaPagina(PaginaDelLab.Consegna);
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

    /// <summary>Carta F5, slice 2 — «finita quando»: tre salvataggi → «N modifiche», riaperto il Lab sono ancora lì.</summary>
    [Fact]
    public async Task IlLotto_TreSalvataggi_RestanoDopoLaRiapertura()
    {
        const string fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLaVetrina(Vetrina));
        AuroraLegge(Path.Combine(Vetrina, "SectorFiles"));
        var pagina = _contesto.RenderComponent<Home>();

        foreach (string nome in new[] { "PIPPO", "PLUTO", "PAPER" })
        {
            Assert.True(_lab.CambiaCampo(fix, 0, "Name", nome));
            await _lab.SalvaAsync(confermato: true);
            Assert.False(_lab.Modifiche.CEQualcosa);
        }

        Assert.Equal(3, _lab.Lotto!.DaPortare);
        Assert.All(_lab.Lotto.Voci, v => Assert.Equal([fix], v.File));
        pagina.WaitForAssertion(() => Assert.Equal("3 da portare", pagina.Find("[data-conto-lotto]").TextContent.Trim()), TimeSpan.FromSeconds(10));

        // In vetrina per la prima volta: le tre voci e una per tutto il sector.
        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);
        pagina.WaitForAssertion(() => Assert.Equal("4 modifiche da vedere", pagina.Find("[data-conto-lotto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.Contains("i punti sulla mappa (NAVAIDS: FIX)", pagina.Find("[data-lotto]").TextContent, StringComparison.Ordinal);

        // Il Lab riaperto: il lotto c'è ancora.
        var riaperto = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Aurora = _lab.Aurora };
        Assert.True(await riaperto.ApriAsync(_albero.Radice));
        Assert.Equal(4, riaperto.Lotto!.DaVedere);
        Assert.True(riaperto.InVetrinaCeQuesto);

        // «Visto in Aurora» dalla barra: il lotto si svuota, e resta vuoto.
        pagina.Find("[data-tasto=visto-in-aurora]").Click();
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-conto-lotto]")), TimeSpan.FromSeconds(10));
        Assert.Empty(Lotto(_albero.Radice));
    }

    /// <summary>Il lotto di quella cartella com'è sul disco.</summary>
    private IReadOnlyList<Vipi.SectorLab.Core.Aurora.VoceDelLotto> Lotto(string sector)
        => Vipi.SectorLab.Core.Aurora.Lotto.Leggi(Path.Combine(_albero.Radice, "dati-del-lab"), sector).Voci;

    /// <summary>
    /// Carta F5, slice 3 — «finita quando»: una ❌ si vede nella scheda del record e nei problemi, e se ne va solo quando
    /// l'AOD la toglie. Il record è stato rinominato dal gesto stesso: la nota lo ritrova col nome nuovo.
    /// </summary>
    [Fact]
    public async Task UnNonVa_RestaSulRecord_FinoAQuandoLoTogli()
    {
        const string fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLaVetrina(Vetrina));
        Assert.True(_lab.CambiaCampo(fix, 0, "Name", "PIPPO"));
        await _lab.SalvaAsync(confermato: true);
        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);
        var barra = _contesto.RenderComponent<AuroraNellaBarra>();

        // ❌ sulla voce del fix, con due parole; ✅ su «tutto il sector» (le voci in ordine: prima il gesto sul fix).
        var fixNelLotto = barra.FindAll("[data-voce-lotto=da-vedere]").First();
        Assert.Contains("i punti sulla mappa", fixNelLotto.TextContent, StringComparison.Ordinal);
        fixNelLotto.QuerySelector("[data-tasto=esito-non-va]")!.Click();
        barra.WaitForAssertion(() => barra.Find("[data-campo=nota-esito]").Change("l'etichetta copre il fix"), TimeSpan.FromSeconds(10));
        barra.FindAll("[data-tasto=esito-come]").Last().Click();
        barra.Find("[data-tasto=visto-in-aurora]").Click();
        barra.WaitForAssertion(() => Assert.Empty(barra.FindAll("[data-voce-lotto]")), TimeSpan.FromSeconds(10));

        // Nella scheda del record e in cima ai problemi.
        _lab.Scegli(fix, 0);
        var scheda = _contesto.RenderComponent<Scheda>();
        Assert.Contains("l'etichetta copre il fix", scheda.Find("[data-note-da-aurora]").TextContent, StringComparison.Ordinal);
        var problemi = _contesto.RenderComponent<PannelloDeiProblemi>();
        Assert.Contains("l'etichetta copre il fix", problemi.Find("[data-note-da-aurora]").TextContent, StringComparison.Ordinal);
        // Un altro record non ce l'ha.
        Assert.Empty(_lab.NoteDaAuroraDi(fix, 1));

        // Riaperto il Lab, c'è ancora; il clic porta al record.
        var riaperto = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Aurora = _lab.Aurora };
        Assert.True(await riaperto.ApriAsync(_albero.Radice));
        var nota = Assert.Single(riaperto.NoteDaAurora);
        riaperto.VaiAllaNota(nota);
        Assert.Equal((fix, 0), riaperto.Scelta);

        // Se ne va solo con «Togli».
        problemi.Find("[data-tasto=togli-nota]").Click();
        problemi.WaitForAssertion(() => Assert.Empty(problemi.FindAll("[data-note-da-aurora]")), TimeSpan.FromSeconds(10));
        scheda.WaitForAssertion(() => Assert.Empty(scheda.FindAll("[data-note-da-aurora]")), TimeSpan.FromSeconds(10));
        Assert.Empty(Vipi.SectorLab.Core.Aurora.Lotto.Leggi(Path.Combine(_albero.Radice, "dati-del-lab"), _albero.Radice).Note);
    }

    [Fact]
    public async Task IlLotto_CambiatoSector_TuttoDaRivedere()
    {
        using var altro = new AlberoDiProva();
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        Assert.True(_lab.CambiaLaVetrina(Vetrina));
        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);
        // La seconda volta, stesso sector e stesso ramo: niente voce nuova.
        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);
        Assert.Contains("prima volta", Assert.Single(_lab.Lotto!.Voci).Cosa, StringComparison.Ordinal);

        // Un altro sector in vetrina: il suo lotto dice che va rivisto tutto, e quello di prima non è più «da vedere».
        Assert.True(await _lab.ApriAsync(altro.Radice));
        Assert.True(await _lab.PortaInAuroraAsync(), _lab.Rifiuto);
        Assert.Contains("prima c'era", Assert.Single(_lab.Lotto!.Voci).Cosa, StringComparison.Ordinal);
        Assert.True(await _lab.ApriAsync(_albero.Radice));
        Assert.False(_lab.InVetrinaCeQuesto);
    }

    [Fact]
    public async Task ISectorApertiSiRicordano()
    {
        Assert.True(await _lab.ApriAsync(_albero.Radice));

        Assert.Equal([_albero.Radice], _lab.SectorRecenti());
        Assert.Equal([_albero.Radice], new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).SectorRecenti());
    }
}

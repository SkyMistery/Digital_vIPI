using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Components;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Testo;

/// <summary>
/// La pagina Testo (riordino, D12, D13, D21): il file com'è adesso, «Applica» che fa una modifica in sospeso, le righe dei
/// metadati bloccate finché non si sbloccano, il file che il motore non interpreta in sola lettura.
/// </summary>
public sealed class PaginaDelTestoTests : IDisposable
{
    private const string Apt = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public PaginaDelTestoTests() => _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));

    public void Dispose() => _albero.Dispose();

    // Due punti con i metadati a blocco, uno senza: la forma di APT.fix sul sector di prova.
    private static readonly string[] Righe =
    [
        "//prova",
        "//@\"AAAAA\" note=\"uno\"", "//@START", "AAAAA;N045.00.00.000;E009.00.00.000;3;", "//@END \"AAAAA\"",
        "//@\"BBBBB\" note=\"due\"", "//@START", "BBBBB;N045.00.00.000;E009.30.00.000;3;", "//@END \"BBBBB\"",
        "CCCCC;N045.00.00.000;E010.00.00.000;3;",
    ];

    private async Task ApriAsync()
    {
        _albero.Scrivi(Apt, string.Join("\r\n", Righe) + "\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
    }

    private string Testo(params (int Riga, string Testo)[] cambi)
    {
        var righe = _lab.RigheDiAdesso(Apt).ToList();
        foreach (var (riga, testo) in cambi.OrderByDescending(c => c.Riga))
            if (riga < 0)
                righe.Insert(-riga, testo);   // negativo: una riga nuova al posto (da 0) -riga
            else
                righe[riga - 1] = testo;
        return string.Join("\n", righe);
    }

    [Fact]
    public async Task Applica_UnaModificaInSospeso_ELeRigheToccateSoltanto()
    {
        await ApriAsync();
        var prima = _lab.RigheDiAdesso(Apt);
        Assert.Equal(Righe, prima.Take(Righe.Length));

        Assert.True(_lab.ApplicaIlTesto(Apt, Testo((10, "CCCCC;N045.00.00.000;E010.30.00.000;3;")), metadatiSbloccati: false), _lab.Rifiuto);

        Assert.Contains(_lab.Modifiche.Voci, v => v.File == Apt);
        Assert.Equal("CCCCC;N045.00.00.000;E010.30.00.000;3;", _lab.RigheDiAdesso(Apt)[9]);
        Assert.StartsWith("APT.fix scritto nel Testo", _lab.DaAnnullare, StringComparison.Ordinal);
        Assert.Contains(Apt, _lab.FileConModifiche);

        // Lo stesso testo di nuovo: niente da applicare.
        Assert.False(_lab.ApplicaIlTesto(Apt, string.Join("\n", _lab.RigheDiAdesso(Apt)), false));
        _lab.Annulla();
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task IMetadati_Bloccati_UnaRigaFraDueBlocchiSi_UnTagCambiatoNo()
    {
        await ApriAsync();

        // Fra «//@END "AAAAA"» e «//@"BBBBB"»: i tag restano quelli, passa.
        Assert.True(_lab.ApplicaIlTesto(Apt, Testo((-5, "//fra i due blocchi")), false), _lab.Rifiuto);
        Assert.Equal("//fra i due blocchi", _lab.RigheDiAdesso(Apt)[5]);

        // Un tag cambiato, bloccati: no.
        Assert.False(_lab.ApplicaIlTesto(Apt, Testo((2, "//@\"AAAAA\" note=\"altro\"")), false));
        Assert.Contains("Sblocca i metadati", _lab.Rifiuto, StringComparison.Ordinal);

        // Sbloccati sì; ma un tag che non si rilegge no.
        Assert.True(_lab.ApplicaIlTesto(Apt, Testo((2, "//@\"AAAAA\" note=\"altro\"")), metadatiSbloccati: true), _lab.Rifiuto);
        Assert.Equal("//@\"AAAAA\" note=\"altro\"", _lab.RigheDiAdesso(Apt)[1]);
        Assert.False(_lab.ApplicaIlTesto(Apt, Testo((2, "//@\"AAAAA\" note=\"rotto")), metadatiSbloccati: true));
        Assert.Contains("i metadati //@ non tornano", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellareUnRecordCoiMetadati_ChiedePrima_ELiToglieConLui()
    {
        // T2, D13: senza la riga di AAAAA il suo blocco di tag resterebbe senza record.
        await ApriAsync();
        var senza = string.Join("\n", _lab.RigheDiAdesso(Apt).Where(r => !r.StartsWith("AAAAA;", StringComparison.Ordinal)));
        Assert.Equal(["AAAAA"], _lab.RecordConMetadatiTolti(Apt, senza));

        Assert.False(_lab.ApplicaIlTesto(Apt, senza, metadatiSbloccati: false));
        Assert.Contains("AAAAA: ha i metadati", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.Modifiche.CEQualcosa);

        Assert.True(_lab.ApplicaIlTesto(Apt, senza, metadatiSbloccati: false, togliAncheIMetadati: true), _lab.Rifiuto);
        var righe = _lab.RigheDiAdesso(Apt);
        Assert.DoesNotContain(righe, r => r.Contains("AAAAA", StringComparison.Ordinal));
        Assert.Equal(["//prova", "//@\"BBBBB\" note=\"due\"", "//@START"], righe.Take(3));
    }

    [Fact]
    public async Task LaDefinizioneDiUnNome_EIProblemiDelFile()
    {
        await ApriAsync();
        // CCCCC sta in APT.fix, che ITALY.isc carica: alla riga 10.
        Assert.Equal((Apt, 10), _lab.DefinizioneDi("ccccc"));
        Assert.Null(_lab.DefinizioneDi("NESSUNO"));

        // Senza modifiche i problemi dell'albero (coi numeri del disco); con le modifiche quelli nuovi.
        var problemi = _lab.ProblemiDelTesto(Apt, out var soloNuovi);
        Assert.False(soloNuovi);
        Assert.Equal(_lab.ProblemiDellAlbero.Count(p => p.File == Apt && p.Problema.Riga > 0), problemi.Count);
        Assert.True(_lab.ApplicaIlTesto(Apt, Testo((10, "CCCCC;N045.00.00.000;E010.30.00.000;3;")), false), _lab.Rifiuto);
        _lab.ProblemiDelTesto(Apt, out soloNuovi);
        Assert.True(soloNuovi);
    }

    [Fact]
    public async Task ILegami_LaRigaDelRecord_SullaMappa_ELApertura()
    {
        // T3 (D21).
        await ApriAsync();
        // La riga del record CCCCC (il terzo record di APT.fix, dopo AAAAA e BBBBB): la 10.
        int ccccc = Enumerable.Range(0, 3).First(i => _lab.RigaDelRecord(Apt, i) == 10);
        Assert.Null(_lab.RigaDelRecord(Apt, 99));

        // Dalla Scheda: il Testo su quel file, alla sua riga, presa una volta sola.
        _lab.ApriNelTesto(Apt, _lab.RigaDelRecord(Apt, ccccc));
        Assert.Equal((PaginaDelLab.Testo, Apt), (_lab.Pagina, _lab.FileDelTesto));
        Assert.Equal(10, _lab.PrendiLaRigaDelTesto());
        Assert.Null(_lab.PrendiLaRigaDelTesto());

        // Dal Testo alla mappa: la riga 10 è di CCCCC; la 1 è un commento.
        Assert.True(_lab.MostraSullaMappa(Apt, 10), _lab.Rifiuto);
        Assert.Equal(PaginaDelLab.Sector, _lab.Pagina);
        Assert.Equal((Apt, ccccc), (_lab.Scelta?.File, _lab.Scelta?.Record));
        Assert.False(_lab.MostraSullaMappa(Apt, 1));
        Assert.Contains("non è di un record", _lab.Rifiuto, StringComparison.Ordinal);

        // Il tasto della Scheda.
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.Services.AddSingleton(_lab);
        var scheda = contesto.RenderComponent<Scheda>();
        scheda.Find("[data-tasto='apri-nel-testo']").Click();
        Assert.Equal((PaginaDelLab.Testo, Apt, 10), (_lab.Pagina, _lab.FileDelTesto, _lab.RigaDelTesto));
    }

    [Fact]
    public async Task LaPagina_SullaMappa_ELaBozzaCheResta()
    {
        await ApriAsync();
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.JSInterop.Setup<bool>("sectorlabTesto.apri", _ => true).SetResult(true);
        contesto.JSInterop.Setup<int?>("sectorlabTesto.riga", _ => true).SetResult(10);
        var scritto = Testo((10, "CCCCC;N045.00.00.000;E010.30.00.000;3;"));
        contesto.JSInterop.Setup<string?>("sectorlabTesto.testo", _ => true).SetResult(scritto);
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<PaginaDelTesto>();
        pagina.Find($"[data-file-del-testo='{Apt}']").Click();
        pagina.WaitForAssertion(() => Assert.Single(contesto.JSInterop.Invocations, i => i.Identifier == "sectorlabTesto.apri"));

        // «Sulla mappa» col testo applicato: il record della riga del cursore.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='testo-sulla-mappa']").Click());
        Assert.Equal(PaginaDelLab.Sector, _lab.Pagina);
        Assert.Equal(Apt, _lab.Scelta?.File);

        // Col testo scritto no; e lasciando la pagina il testo resta come bozza.
        _lab.ApriNelTesto(Apt);
        await pagina.InvokeAsync(() => pagina.Instance.Cambiato(true));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='testo-sulla-mappa']").Click());
        Assert.Contains("Prima «Applica» o «Scarta»", pagina.Find("[data-testo-rifiutato]").TextContent, StringComparison.Ordinal);
        await pagina.Instance.DisposeAsync();
        Assert.Equal(new SessioneDelLab.BozzaDelTesto(Apt, string.Join("\n", _lab.RigheDiAdesso(Apt)), scritto), _lab.Bozza);

        // Riaperta, la pagina riprende la bozza: l'editor riceve il testo scritto, e «Applica» lo scrive.
        var di_nuovo = contesto.RenderComponent<PaginaDelTesto>();
        di_nuovo.WaitForAssertion(() => Assert.Equal(2, contesto.JSInterop.Invocations.Count(i => i.Identifier == "sectorlabTesto.apri")));
        Assert.Equal(scritto, contesto.JSInterop.Invocations.Last(i => i.Identifier == "sectorlabTesto.apri").Arguments[1]);
        Assert.Null(_lab.Bozza);
        Assert.Contains("Ripreso il testo", di_nuovo.Find("[data-testo-rifiutato]").TextContent, StringComparison.Ordinal);
        await di_nuovo.InvokeAsync(() => di_nuovo.Find("[data-tasto='applica-testo']").Click());
        Assert.Contains(_lab.Modifiche.Voci, v => v.File == Apt);
    }

    [Fact]
    public async Task UnFileCheIlMotoreNonInterpreta_SiGuardaSoltanto()
    {
        await ApriAsync();
        // Il sector di prova ne ha (i campioni che il motore non legge come record).
        var non =_lab.FileDelSector.FirstOrDefault(f => _lab.TestoDelFile(f, out var m, out _) is not null && !m);
        Assert.NotNull(non);
        Assert.NotNull(_lab.TestoDelFile(non, out var modificabile, out var perche));
        Assert.False(modificabile);
        Assert.Contains("si guarda soltanto", perche, StringComparison.Ordinal);
        Assert.False(_lab.ApplicaIlTesto(non, "x", false));
    }

    [Fact]
    public async Task LaPagina_ApreIlFile_EApplicaQuelCheLeditoreHa()
    {
        await ApriAsync();
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.JSInterop.Setup<bool>("sectorlabTesto.apri", _ => true).SetResult(true);
        var scritto = Testo((10, "CCCCC;N045.00.00.000;E010.30.00.000;3;"));
        contesto.JSInterop.Setup<string?>("sectorlabTesto.testo", _ => true).SetResult(scritto);
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<PaginaDelTesto>();
        Assert.NotNull(pagina.Find("[data-testo-vuoto]"));

        pagina.Find($"[data-file-del-testo='{Apt}']").Click();

        // L'editor riceve il file com'è adesso, modificabile.
        pagina.WaitForAssertion(() => Assert.Single(contesto.JSInterop.Invocations, i => i.Identifier == "sectorlabTesto.apri"));
        var apri = contesto.JSInterop.Invocations.Single(i => i.Identifier == "sectorlabTesto.apri");
        Assert.Equal(string.Join("\n", _lab.RigheDiAdesso(Apt)), apri.Arguments[1]);
        Assert.Equal(PaginaDelLab.Testo, _lab.Pagina);
        Assert.True(pagina.Find("[data-tasto='applica-testo']").HasAttribute("disabled"));   // niente di scritto

        // L'editor dice che è cambiato: «Applica» si accende e scrive.
        await pagina.InvokeAsync(() => pagina.Instance.Cambiato(true));
        Assert.False(pagina.Find("[data-tasto='applica-testo']").HasAttribute("disabled"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='applica-testo']").Click());

        Assert.Contains(_lab.Modifiche.Voci, v => v.File == Apt);
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll($"[data-file-del-testo='{Apt}'] .lab-testo-toccato")));
        Assert.Empty(pagina.FindAll("[data-testo-rifiutato]"));
    }

    [Fact]
    public async Task LaPagina_NomiDelCatalogo_CtrlClic_ELaDomandaSuiMetadati()
    {
        await ApriAsync();
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.JSInterop.Setup<bool>("sectorlabTesto.apri", _ => true).SetResult(true);
        var senza = string.Join("\n", _lab.RigheDiAdesso(Apt).Where(r => !r.StartsWith("AAAAA;", StringComparison.Ordinal)));
        contesto.JSInterop.Setup<string?>("sectorlabTesto.testo", _ => true).SetResult(senza);
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<PaginaDelTesto>();
        pagina.Find($"[data-file-del-testo='{Apt}']").Click();
        pagina.WaitForAssertion(() => Assert.Single(contesto.JSInterop.Invocations, i => i.Identifier == "sectorlabTesto.apri"));

        // I nomi del catalogo mentre si scrive.
        Assert.Contains(pagina.Instance.Suggerisci("CCC"), n => n.Nome == "CCCCC" && n.Dove.Contains("APT.fix", StringComparison.Ordinal));

        // Ctrl+clic su un nome dello stesso file: si va alla riga.
        await pagina.InvokeAsync(() => pagina.Instance.VaiAllaDefinizione("CCCCC"));
        Assert.Contains(contesto.JSInterop.Invocations, i => i.Identifier == "sectorlabTesto.vaiAllaRiga" && Equals(i.Arguments[1], 10));
        await pagina.InvokeAsync(() => pagina.Instance.VaiAllaDefinizione("NESSUNO"));
        Assert.Contains("non è nel catalogo", pagina.Find("[data-testo-rifiutato]").TextContent, StringComparison.Ordinal);

        // Con del testo scritto non si cambia file.
        await pagina.InvokeAsync(() => pagina.Instance.Cambiato(true));
        var altro = _lab.FileDelSector.First(f => f != Apt);
        pagina.Find($"[data-file-del-testo='{altro}']").Click();
        Assert.Equal(Apt, _lab.FileDelTesto);
        Assert.Contains("prima «Applica» o «Scarta»", pagina.Find("[data-testo-rifiutato]").TextContent, StringComparison.Ordinal);

        // «Applica» senza la riga di AAAAA: prima la domanda, poi «Togli anche i metadati».
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='applica-testo']").Click());
        Assert.Contains("AAAAA ha i metadati", pagina.Find("[data-domanda-metadati]").TextContent, StringComparison.Ordinal);
        Assert.False(_lab.Modifiche.CEQualcosa);
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='togli-anche-i-metadati']").Click());
        Assert.True(_lab.Modifiche.CEQualcosa);
        Assert.DoesNotContain(_lab.RigheDiAdesso(Apt), r => r.Contains("AAAAA", StringComparison.Ordinal));
        Assert.Empty(pagina.FindAll("[data-domanda-metadati]"));
    }
}

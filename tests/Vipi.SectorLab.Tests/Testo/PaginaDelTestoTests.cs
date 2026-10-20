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
}

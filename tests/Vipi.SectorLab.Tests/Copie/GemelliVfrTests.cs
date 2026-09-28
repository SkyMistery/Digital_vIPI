using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Il gemello di un punto VFR (lotto «Subito» slice 8e, «file per file» F2): il punto del <c>.vfi</c> e il suo fix
/// nascosto in <c>NAVAIDS/VFR_NASCOSTI.fix</c>, col codice come chiave. Spostare l'uno sposta l'altro, un tasto crea il
/// gemello che manca, togliere il punto propone di togliere il gemello.
/// </summary>
public sealed class GemelliVfrTests : IDisposable
{
    private const string Vfi = "SectorFiles/Include/IT/lzzz.vfi";
    private const string Nascosti = "SectorFiles/Include/IT/NAVAIDS/VFR_NASCOSTI.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public GemelliVfrTests()
    {
        // COLOMBO ha il gemello (scritto compatto, come nel fork); ALTRO no; QUOTA non ha un codice nel 2° campo.
        _albero.Scrivi(Vfi, "COLOMBO;ZZS3;N041.42.47.000;E012.21.56.000;\r\nALTRO;ZZN1;N041.50.00.000;E012.20.00.000;\r\n"
                            + "QUOTA;2500;N041.55.00.000;E012.25.00.000;\r\n");
        _albero.Scrivi(Nascosti, "AAA1;N0400000000;E0100000000;3;\r\nZZS3;N0414247000;E0122156000;3;\r\nZZZ9;N0450000000;E0100000000;3;\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    private string[] Righe(string file) => File.ReadAllText(Path.Combine(_albero.Radice, file)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task IlPuntoEIlSuoFixNascostoSonoGemelliAncheScrittiInFormeDiverse()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        var gemello = _lab.GemelloDi(Vfi, 0)!;
        Assert.Equal("ZZS3", gemello.Codice);
        Assert.Equal((Nascosti, 1), (Assert.Single(gemello.Gemelli).File, gemello.Gemelli[0].Indice));
        Assert.True(gemello.Uguale);
        // E dall'altra parte: il fix vede il punto.
        Assert.Equal(Vfi, Assert.Single(_lab.GemelloDi(Nascosti, 1)!.Gemelli).File);
        // Un 2° campo che non è un codice non chiede un gemello; un fix senza punto lo dice.
        Assert.Null(_lab.GemelloDi(Vfi, 2));
        Assert.Empty(_lab.GemelloDi(Nascosti, 0)!.Gemelli);
    }

    [Fact]
    public async Task SpostareIlPuntoSpostaIlGemelloERinominarloNo()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.CambiaCampo(Vfi, 0, "Position", "N041.43.00.000 E012.22.00.000"));
        Assert.True(_lab.CambiaCampo(Vfi, 0, "Name", "COLOMBA"));

        // Il nome del punto non è il nome del fix: il gemello non lo riceve, e non è nemmeno una «copia non cambiata».
        Assert.All(_lab.Modifiche.Voci.OfType<Vipi.SectorLab.Core.Modifiche.ModificaDiCampo>(), m => Assert.Empty(m.NonToccate));
        Assert.True(_lab.GemelloDi(Vfi, 0)!.Uguale);
        await _lab.SalvaAsync();
        Assert.Equal("ZZS3;N0414300000;E0122200000;3;", Righe(Nascosti)[1]);
        Assert.Equal("COLOMBA;ZZS3;N041.43.00.000;E012.22.00.000;", Righe(Vfi)[0]);
    }

    [Fact]
    public async Task SpostareIlFixNascostoSpostaIlPunto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.CambiaCampo(Nascosti, 1, "Position", "N041.43.00.000 E012.22.00.000"));

        Assert.True(_lab.GemelloDi(Vfi, 0)!.Uguale);
        Assert.Contains(Vfi, _lab.Modifiche.FileToccati);
    }

    [Fact]
    public async Task IlGemelloCheMancaSiCreaAlSuoPostoInOrdineAlfabetico()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        _lab.Scegli(Vfi, 1);
        Assert.Empty(_lab.GemelloDi(Vfi, 1)!.Gemelli);

        Assert.True(_lab.CreaIlGemello(Vfi, 1));

        // La scheda resta sul punto: il gemello nasce dall'altra parte.
        Assert.Equal((Vfi, 1), _lab.Scelta);
        var gemello = _lab.GemelloDi(Vfi, 1)!;
        Assert.True(gemello.Uguale);
        await _lab.SalvaAsync();
        Assert.Equal(["AAA1;N0400000000;E0100000000;3;", "ZZN1;N0415000000;E0122000000;3;", "ZZS3;N0414247000;E0122156000;3;",
                      "ZZZ9;N0450000000;E0100000000;3;"], Righe(Nascosti));

        // Ora il gemello c'è: il tasto non ne crea un secondo.
        Assert.False(_lab.CreaIlGemello(Vfi, 1));
    }

    [Fact]
    public async Task IlGemelloCreatoSiAnnullaConUnGestoSolo()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CreaIlGemello(Vfi, 1));

        _lab.Annulla();

        Assert.False(_lab.Modifiche.CEQualcosa);
        Assert.Empty(_lab.GemelloDi(Vfi, 1)!.Gemelli);
    }

    [Fact]
    public async Task TogliereIlPuntoProponeDiTogliereIlGemello()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.TogliRecord(Vfi, 0));
        var domanda = _lab.GemelloDaTogliere!;
        Assert.Equal(("COLOMBO", "ZZS3"), (domanda.Punto, domanda.Codice));
        // Il gemello non se ne va da solo.
        Assert.DoesNotContain(Nascosti, _lab.Modifiche.FileToccati);

        Assert.True(_lab.RispondiSulGemello(true));

        Assert.Null(_lab.GemelloDaTogliere);
        await _lab.SalvaAsync();
        Assert.DoesNotContain(Righe(Nascosti), r => r.StartsWith("ZZS3;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LasciatoIlGemelloLaDomandaSparisceEIlFixResta()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.TogliRecord(Vfi, 0));

        Assert.False(_lab.RispondiSulGemello(false));

        Assert.Null(_lab.GemelloDaTogliere);
        Assert.Equal([Vfi], _lab.Modifiche.FileToccati);
    }

    [Fact]
    public async Task UnCodiceRipetutoNonSiPropaga()
    {
        _albero.Scrivi(Nascosti, "ZZS3;N0414247000;E0122156000;3;\r\nZZS3;N0414247000;E0122156000;3;\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(2, _lab.GemelloDi(Vfi, 0)!.Gemelli.Count);

        Assert.True(_lab.CambiaCampo(Vfi, 0, "Position", "N041.43.00.000 E012.22.00.000"));

        Assert.Equal([Vfi], _lab.Modifiche.FileToccati);
    }
}

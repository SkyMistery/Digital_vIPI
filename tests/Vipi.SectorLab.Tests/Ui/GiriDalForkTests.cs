using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Git;
using Vipi.SectorLab.Tests.Git;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// I giri di prova dal fork (carta del 3 novembre 2026): il formato e le sue regole (P3), i file di <c>giri/</c> letti da
/// git sul ramo predefinito (P1-P2) con la copia nei dati, la scelta del giro e il «nuovo» (P4), gli esiti per giro e il
/// passaggio dal file di prima (P5), «Ricomincia» (P7), «Primi passi» come ripiego (P8).
/// </summary>
public sealed class GiriDalForkTests : IDisposable
{
    private const string Ground = """
        {
          "id": "ground", "titolo": "Il ground dello scalo", "versione": 1, "perChi": "Chi ritocca piazzali e stand",
          "nuovoCampo": "si ignora",
          "compiti": [
            { "id": "1", "titolo": "Lavorare su uno scalo", "pagina": "Sector", "fai": "Lavora su LIRF.", "dovresti": "Il sedime." },
            { "id": "2", "titolo": "Una pagina che non c'è", "pagina": "Cucina", "fai": "Niente." },
            { "id": "9", "titolo": "Che cosa ti è mancato?", "fai": "Scrivilo.", "soloNota": true },
          ]
        }
        """;

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private string Dati => Path.Combine(_albero.Radice, "dati-del-lab");

    public GiriDalForkTests() => _contesto.JSInterop.Mode = JSRuntimeMode.Loose;

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    [Fact]
    public void UnGiroSiLeggeColleSueRegole()
    {
        var giro = GiroDiProva.Leggi("02-ground.json", Ground, out string? perche);

        Assert.Null(perche);
        Assert.NotNull(giro);
        Assert.Equal(("ground", "Il ground dello scalo", 1, "02-ground.json"), (giro.Id, giro.Titolo, giro.Versione, giro.File));
        Assert.Equal(["1", "2", "9"], giro.Compiti.Select(c => c.Id));
        Assert.Equal(PaginaDelLab.Sector, giro.Compiti[0].Pagina);
        Assert.Equal("Il sedime.", giro.Compiti[0].DovrestiVedere);
        Assert.Null(giro.Compiti[1].Pagina);
        Assert.Equal("", giro.Compiti[1].DovrestiVedere);
        Assert.True(giro.Compiti[2].SoloNota);
    }

    [Theory]
    [InlineData("{ non è json", "non è un JSON valido")]
    [InlineData("[]", "non è un oggetto")]
    [InlineData("""{ "id": "Ground", "titolo": "x", "compiti": [ { "id": "1", "titolo": "a", "fai": "b" } ] }""", "manca «id»")]
    [InlineData("""{ "id": "ground", "compiti": [ { "id": "1", "titolo": "a", "fai": "b" } ] }""", "manca «titolo»")]
    [InlineData("""{ "id": "ground", "titolo": "x", "compiti": [] }""", "manca «compiti»")]
    [InlineData("""{ "id": "ground", "titolo": "x", "compiti": [ { "id": "1", "titolo": "a" } ] }""", "non ha «titolo» o «fai»")]
    [InlineData("""{ "id": "ground", "titolo": "x", "compiti": [ { "id": "1", "titolo": "a", "fai": "b" }, { "id": "1", "titolo": "c", "fai": "d" } ] }""", "due compiti con l'id «1»")]
    [InlineData("""{ "id": "ground", "titolo": "x", "versione": "due", "compiti": [ 3 ] }""", "il compito 1 non è un oggetto")]
    public void UnGiroRottoSiSaltaColPerche(string testo, string perche)
    {
        Assert.Null(GiroDiProva.Leggi("rotto.json", testo, out string? detto));
        Assert.Contains(perche, detto, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimiPassiStaInTestaEUnGiroRottoSiDice()
    {
        var lab = new SessioneDelLab(Dati);

        lab.UsaIGiri([new("02-ground.json", Ground), new("03-rotto.json", "{")]);

        Assert.Equal(["primi-passi", "ground"], lab.Giri.Select(g => g.Id));
        Assert.Same(GiroDiProva.PrimiPassi, lab.Giri[0]);
        Assert.StartsWith("03-rotto.json: non è un JSON valido", Assert.Single(lab.GiriNonLetti), StringComparison.Ordinal);

        // Un primi-passi.json sul fork prende il posto di quello dentro il Lab, dove sta fra i file.
        lab.UsaIGiri([new("01-primi-passi.json", """{ "id": "primi-passi", "titolo": "Primi passi", "versione": 2, "compiti": [ { "id": "G1", "titolo": "a", "fai": "b" } ] }"""),
            new("02-ground.json", Ground)]);
        Assert.Equal((2, "01-primi-passi.json"), (lab.Giri[0].Versione, lab.Giri[0].File));
    }

    [Fact]
    public void GliEsitiSonoPerGiroEUnGiroCambiatoTornaNuovo()
    {
        var lab = new SessioneDelLab(Dati);
        lab.AccendiIlGiro();
        lab.DaiLEsito("G1", EsitoDelCompito.Va, null);
        lab.UsaIGiri([new("02-ground.json", Ground)]);
        var ground = lab.Giri[1];

        Assert.True(lab.GiroNuovo(ground));
        Assert.False(lab.GiroNuovo(lab.Giri[0]));
        Assert.Equal(1, lab.GiriNuovi);

        lab.ScegliIlGiro("ground");
        Assert.False(lab.GiroNuovo(ground));
        Assert.Equal(0, lab.CompitoAperto);
        lab.DaiLEsito("1", EsitoDelCompito.NonVa, "non trovo LIRF");
        Assert.Equal(1, lab.CompitiFatti);
        Assert.Equal(1, lab.FattiDi(lab.Giri[0]));

        // La versione 2 cambia il compito 2 e ne aggiunge uno: torna nuovo, e l'esito del compito 1 resta.
        lab.UsaIGiri([new("02-ground.json", Ground.Replace("\"versione\": 1", "\"versione\": 2", StringComparison.Ordinal))]);
        Assert.True(lab.GiroNuovo(lab.Giri[1]));
        Assert.Equal(EsitoDelCompito.NonVa, lab.EsitiDi(lab.Giri[1])["1"].Esito);

        // Riaperto il Lab: il giro aperto, gli esiti dei due giri, e la copia dei giri del fork non c'è (solo UsaIGiri).
        var dopo = new SessioneDelLab(Dati);
        Assert.Equal(["primi-passi"], dopo.Giri.Select(g => g.Id));
        Assert.Equal(EsitoDelCompito.Va, dopo.EsitiDi(GiroDiProva.PrimiPassi)["G1"].Esito);
    }

    [Fact]
    public void IlFileDiPrimaDiventaPrimiPassi()
    {
        Directory.CreateDirectory(Dati);
        File.WriteAllText(Path.Combine(Dati, "giro-di-prova.json"),
            """{"Acceso":true,"Esiti":[{"Compito":"G1","Esito":"NonHoCapito","Nota":"il VID dov'è?","Quando":"2026-11-03T08:55:52+01:00"}]}""");

        var lab = new SessioneDelLab(Dati);

        Assert.True(lab.GiroAcceso);
        Assert.Equal("primi-passi", lab.GiroScelto.Id);
        Assert.Equal("il VID dov'è?", lab.EsitiDelGiro["G1"].Nota);
        Assert.Equal(1, lab.CompitoAperto);

        // Riscritto alla prima scelta, col formato nuovo e senza gli «Esiti» in cima.
        lab.ScegliIlGiro("primi-passi");
        string scritto = File.ReadAllText(Path.Combine(Dati, "giro-di-prova.json"));
        Assert.Contains("\"Giri\":{\"primi-passi\":", scritto, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Esiti\":null", scritto, StringComparison.Ordinal);
        Assert.Equal("il VID dov'è?", new SessioneDelLab(Dati).EsitiDelGiro["G1"].Nota);
    }

    [Fact]
    public async Task RicominciaChiedePrimaEDiceSeNonSonoMandati()
    {
        var lab = new SessioneDelLab(Dati) { ApriNelBrowser = _ => { } };
        lab.AccendiIlGiro();
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        lab.DaiLEsito("G1", EsitoDelCompito.Va, "ok");
        _contesto.Services.AddSingleton(lab);
        var pagina = _contesto.RenderComponent<Home>();

        pagina.Find("[data-tasto='giro-ricomincia']").Click();
        Assert.Contains("Non li hai ancora mandati", pagina.Find("[data-conferma='giro-ricomincia']").TextContent, StringComparison.Ordinal);
        pagina.Find("[data-tasto='giro-ricomincia-no']").Click();
        Assert.Empty(pagina.FindAll("[data-conferma='giro-ricomincia']"));
        Assert.Equal(1, lab.CompitiFatti);

        Assert.True(lab.CambiaIlVid("123456"));
        Assert.True(lab.MandaGliEsiti());
        Assert.False(lab.EsitiNonMandati);
        pagina.WaitForAssertion(() => pagina.Find("[data-tasto='giro-ricomincia']").Click());
        Assert.Contains("Quelli mandati restano nella issue", pagina.Find("[data-conferma='giro-ricomincia']").TextContent, StringComparison.Ordinal);
        pagina.Find("[data-tasto='giro-ricomincia-si']").Click();

        pagina.WaitForAssertion(() => Assert.Equal(0, lab.CompitiFatti));
        Assert.Equal("G1", pagina.Find("[data-compito]").GetAttribute("data-compito"));
        Assert.Equal("nessuno", pagina.Find("[data-pallino='G1']").GetAttribute("data-esito"));
    }

    [Fact]
    public async Task IGiriArrivanoDalForkERestaLaCopia()
    {
        // Sul «server» il sector dell'albero di prova e la cartella giri/ (un file che non è .json si ignora).
        using var repo = new RepoDiProva();
        string sectorFiles = Path.Combine(_albero.Radice, "SectorFiles");
        foreach (string file in Directory.EnumerateFiles(sectorFiles, "*", SearchOption.AllDirectories))
            repo.Scrivi(Path.GetRelativePath(_albero.Radice, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(file));
        repo.Scrivi("giri/02-ground.json", Ground);
        repo.Scrivi("giri/LEGGIMI.md", "i giri del Lab");
        repo.Esegui(repo.Lavoro, "add", "--", "SectorFiles", "giri");
        repo.Esegui(repo.Lavoro, "commit", "--quiet", "-m", "sector e giri");
        repo.Esegui(repo.Lavoro, "push", "--quiet");

        var lab = new SessioneDelLab(Dati);
        lab.AccendiIlGiro();
        Assert.True(await lab.ApriEValidaAsync(repo.Lavoro));
        await lab.CercaIGiriAsync();

        Assert.Null(lab.PercheDeiGiri);
        Assert.Equal(["primi-passi", "ground"], lab.Giri.Select(g => g.Id));
        Assert.Equal(["02-ground.json"], Directory.EnumerateFiles(Path.Combine(Dati, "giri")).Select(Path.GetFileName));

        // Riaperto senza rete (la copia nei dati) i giri ci sono subito; tolto dal fork, si toglie anche dalla copia.
        Assert.Equal(["primi-passi", "ground"], new SessioneDelLab(Dati).Giri.Select(g => g.Id));
        repo.Esegui(repo.Lavoro, "rm", "--quiet", "--", "giri/02-ground.json");
        repo.Esegui(repo.Lavoro, "commit", "--quiet", "-m", "via il ground");
        repo.Esegui(repo.Lavoro, "push", "--quiet");
        await lab.CercaIGiriAsync();
        Assert.Equal(["primi-passi"], lab.Giri.Select(g => g.Id));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(Dati, "giri")));
    }

    [Fact]
    public async Task UnRamoSenzaGiriTornaVuotoEUnRamoCheNonCeLoDice()
    {
        using var repo = new RepoDiProva();

        var (giri, perche) = await GiriDalServer.LeggiAsync(repo.Git, repo.Lavoro, "master", prendi: true);
        Assert.Empty(giri);
        Assert.Null(perche);

        (giri, perche) = await GiriDalServer.LeggiAsync(repo.Git, repo.Lavoro, "non-ce", prendi: false);
        Assert.Empty(giri);
        Assert.Contains("origin/non-ce", perche, StringComparison.Ordinal);
    }
}

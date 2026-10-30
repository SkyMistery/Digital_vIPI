using Aip;
using Vipi.SectorLab.Core.Git;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Servizi;
using GitDelLab = Vipi.SectorLab.Core.Git.Git;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 0c: il repo dell'AIP espanso nel Lab — «Prendi l'AIP espanso», la lettura sul ramo del ciclo
/// (<c>aip/&lt;ciclo&gt;</c>), «Registra», «Manda». Il «server» è un repo nudo nel temporaneo, col ramo <c>main</c> e il
/// README come quello di prova del committente: niente rete, uguale in CI e sul PC.
/// </summary>
public sealed class RepoDellAipTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "repo-aip-" + Guid.NewGuid().ToString("N"));
    private readonly GitDelLab _git = new();

    public RepoDellAipTests()
    {
        Directory.CreateDirectory(_radice);
        Esegui(_radice, "init", "--quiet", "--bare", "--initial-branch=main", Server);
        var primo = Path.Combine(_radice, "primo");
        Esegui(_radice, "clone", "--quiet", Server, primo);
        Configura(primo);
        File.WriteAllText(Path.Combine(primo, "README.md"), "# it-aip-espanso-test\n");
        Esegui(primo, "add", "README.md");
        Esegui(primo, "commit", "--quiet", "-m", "Add initial README");
        Esegui(primo, "push", "--quiet", "-u", "origin", "main");
    }

    private string Server => Path.Combine(_radice, "server.git");

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_radice, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_radice, recursive: true);
    }

    private string Esegui(string cartella, params string[] argomenti)
    {
        var esito = _git.EseguiAsync(cartella, argomenti).GetAwaiter().GetResult();
        Assert.True(esito.Riuscito, $"git {string.Join(' ', argomenti)}: {esito.Errore}");
        return esito.Uscita;
    }

    private void Configura(string clone)
    {
        Esegui(clone, "config", "user.name", "Prova del Lab");
        Esegui(clone, "config", "user.email", "prova@example.invalid");
    }

    /// <summary>La cartella dei PDF con una cartella per ciclo (dal 22 ottobre il Lab legge un ciclo dalla sua cartella).</summary>
    private string CartellaDeiPdf(params string[] cicli)
    {
        var pdf = Directory.CreateDirectory(Path.Combine(_radice, "AIP")).FullName;
        foreach (var ciclo in cicli.Length == 0 ? ["2611"] : cicli)
        {
            var cartella = Directory.CreateDirectory(EmendamentoDellAip.CartellaDelCiclo(pdf, ciclo)).FullName;
            File.WriteAllBytes(Path.Combine(cartella, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno", "LI R902 - Prova Due"));
        }
        return pdf;
    }

    [Theory]
    [InlineData(".gitignore", true)]
    [InlineData("2611/li.json", true)]
    [InlineData("2611/LF.JSON", true)]
    [InlineData("2611/LI-ENR 5.1.2.pdf", false)]
    [InlineData("README.md", false)]
    [InlineData("li.json", false)]
    [InlineData("26/li.json", false)]
    [InlineData("2611/sotto/li.json", false)]
    [InlineData("correzioni/li.json", true)]
    [InlineData("correzioni/note.md", false)]
    [InlineData("correzioni/sotto/li.json", false)]
    public void IConfiniDelRepo(string percorso, bool dentro) => Assert.Equal(dentro, RepoDellAip.DentroIConfini(percorso));

    [Fact]
    public void IlMessaggioPropostoNominaIPdf_ODaSetteInSuLeSezioni()
    {
        static EsitoDellAipEspanso Esito(params string[] letti) => new("2610", "x", letti.Length, letti, [], 870, [], [], []);

        Assert.Equal("AIP 2610: 870 aree da 2 PDF (LI-ENR 5.1.1, LI-ENR 5.1.2)",
            RepoDellAip.MessaggioProposto(Esito("LI-ENR 5.1.1.pdf", "LI-ENR 5.1.2.pdf")));
        Assert.Equal("AIP 2610: 870 aree da 7 PDF (ENR 2, ENR 5)",
            RepoDellAip.MessaggioProposto(Esito("LI-ENR 2.1.1.1.pdf", "LI-ENR 2.1.2 1 Alghero CTR.pdf", "LI-ENR 2.2.pdf",
                "LI-ENR 5.1.1.pdf", "LI-ENR 5.1.2.pdf", "LI-ENR 5.1.3.pdf", "LI-ENR 5.1.4.pdf")));
        // Le voci di ENR 3 e 4 dove ci sono, quelle a zero no.
        Assert.Equal("AIP 2610: 870 aree, 29 rotte, 55 radioassistenze, 1254 punti da 1 PDF (LI-ENR 4.4)",
            RepoDellAip.MessaggioProposto(Esito("LI-ENR 4.4.pdf") with { Rotte = 29, Radioassistenze = 55, Punti = 1254 }));
    }

    [Fact]
    public void IlGitIgnoreTieneFuoriIPdf_ESiCompletaSenzaDoppioni()
    {
        var cartella = Directory.CreateDirectory(Path.Combine(_radice, "g")).FullName;
        File.WriteAllText(Path.Combine(cartella, ".gitignore"), "*.tmp\n*.pdf\n");

        Assert.True(RepoDellAip.MettiIlGitIgnore(cartella));
        Assert.False(RepoDellAip.MettiIlGitIgnore(cartella));
        var righe = File.ReadAllLines(Path.Combine(cartella, ".gitignore"));
        Assert.Equal(["*.tmp", "*.pdf", "*.PDF", "*.scrivendo"], righe);
    }

    [Fact]
    public async Task SiPrendeSoloInUnaCartellaNuovaOVuota()
    {
        var piena = Directory.CreateDirectory(Path.Combine(_radice, "piena")).FullName;
        File.WriteAllText(Path.Combine(piena, "x.txt"), "x");

        Assert.Contains("non è vuota", await RepoDellAip.PrendiAsync(_git, Server, piena), StringComparison.Ordinal);
        Assert.Contains("per intero", await RepoDellAip.PrendiAsync(_git, Server, "relativa"), StringComparison.Ordinal);
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, Path.Combine(_radice, "nuova")));
        Assert.True(RepoDellAip.EUnRepo(Path.Combine(_radice, "nuova")));
        Assert.True(File.Exists(Path.Combine(_radice, "nuova", "README.md")));
    }

    [Fact]
    public async Task IlGiroDalLab_PrendiLeggiRegistraManda_EMainNonSiTocca()
    {
        var aperte = new List<Uri>();
        var lab = new SessioneDelLab(Path.Combine(_radice, "dati-del-lab")) { ApriNelBrowser = aperte.Add };
        lab.CambiaLIndirizzoDellAipEspanso(Server);
        Assert.True(lab.CambiaLaCartellaDeiPdf(CartellaDeiPdf()), lab.Rifiuto);
        var espanso = Path.Combine(_radice, "aip-espanso");

        Assert.True(await lab.PrendiLAipEspansoAsync(espanso), lab.Rifiuto);
        Configura(espanso);
        Assert.Equal(espanso, lab.CartellaDellAipEspanso);
        Assert.Equal("main", lab.StatoDellAip!.Ramo);

        // Si legge sul ramo del ciclo, nato da origin/main, col .gitignore; un PDF lasciato lì non entra.
        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);
        File.WriteAllText(Path.Combine(espanso, "2611", "LI-ENR 5.1.2.pdf"), "non va nel repo");
        await lab.RileggiIlRepoDellAipAsync();
        Assert.Equal("aip/2611", lab.StatoDellAip!.Ramo);
        Assert.Equal([".gitignore", "2611/li.json", "2611/pagine.json"], lab.StatoDellAip.Cambiati.Select(c => c.Percorso).Order(StringComparer.Ordinal));
        Assert.Equal("AIP 2611: 2 aree da 1 PDF (LI-ENR 5.1.2)", lab.MessaggioDellAip);

        Assert.True(await lab.RegistraLAipAsync(lab.MessaggioDellAip), lab.Rifiuto);
        Assert.Empty(lab.StatoDellAip!.Cambiati);
        Assert.Equal("AIP 2611: 2 aree da 1 PDF (LI-ENR 5.1.2)", Esegui(espanso, "log", "-1", "--format=%s").Trim());
        Assert.Equal(".gitignore\n2611/li.json\n2611/pagine.json", Esegui(espanso, "show", "--name-only", "--format=", "HEAD").Trim().Replace("\r", ""));

        Assert.True(await lab.MandaLAipAsync(), lab.Rifiuto);
        Assert.Contains("refs/heads/aip/2611", Esegui(Server, "for-each-ref", "--format=%(refname)"), StringComparison.Ordinal);
        // main del server com'era: il README e basta. La PR la fonde il committente.
        Assert.Equal("Add initial README", Esegui(Server, "log", "-1", "--format=%s", "main").Trim());
        // Un server che non è GitHub non ha la pagina della PR.
        Assert.Null(lab.PaginaDellaPrDellAip);
        Assert.Empty(aperte);

        // Riletto uguale: niente da registrare, e lo dice.
        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);
        Assert.False(await lab.RegistraLAipAsync("di nuovo"));
        Assert.Contains("niente da registrare", lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal(2, AipJson.Deserialize(File.ReadAllText(Path.Combine(espanso, "2611", "li.json"))).Airspaces.Count);
    }

    // La domanda aperta del 27 ottobre: con file non registrati la lettura del ciclo nuovo si rifiuta (A40), ma lo si scopriva
    // dopo i tre minuti dell'emendamento. Ora lo dicono la pagina accanto al tasto e «Applica», prima di scrivere.
    [Fact]
    public async Task LEmendamentoSiFermaPrimaConFileNonRegistrati()
    {
        var lab = new SessioneDelLab(Path.Combine(_radice, "dati-del-lab"));
        lab.CambiaLIndirizzoDellAipEspanso(Server);
        var pdf = CartellaDeiPdf();
        Assert.True(lab.CambiaLaCartellaDeiPdf(pdf), lab.Rifiuto);
        var espanso = Path.Combine(_radice, "aip-espanso");
        Assert.True(await lab.PrendiLAipEspansoAsync(espanso), lab.Rifiuto);
        Configura(espanso);
        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);
        var amdt = Path.Combine(_radice, "AMDT.pdf");
        File.WriteAllBytes(amdt, PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno"));

        Assert.Equal("L'AIP espanso ha 3 file non registrati su aip/2611: dopo l'emendamento la lettura del ciclo nuovo non partirebbe. "
                     + "Registra l'AIP prima di applicarlo.", lab.EmendamentoBloccato);
        Assert.False(await lab.ApplicaLEmendamentoAsync(amdt));
        Assert.EndsWith("Registra l'AIP prima di applicarlo. Non ho scritto niente.", lab.Rifiuto, StringComparison.Ordinal);
        Assert.Null(lab.UltimoEmendamento);
        Assert.Equal(["AIP 2611"], Directory.EnumerateDirectories(pdf).Select(Path.GetFileName));

        // Registrato, il tasto si riaccende e «Applica» va avanti: questo PDF non è un emendamento, e lo dice l'emendamento.
        Assert.True(await lab.RegistraLAipAsync("AIP 2611"), lab.Rifiuto);
        Assert.Null(lab.EmendamentoBloccato);
        Assert.False(await lab.ApplicaLEmendamentoAsync(amdt));
        Assert.DoesNotContain("non registrati", lab.Rifiuto, StringComparison.Ordinal);
        Assert.NotNull(lab.UltimoEmendamento);
    }

    [Fact]
    public async Task IlRamoDelCicloSiPrendeDalServerSeUnAltroAodLHaGiaCominciato_ESenzaLasciareLavoroIndietro()
    {
        var altro = Path.Combine(_radice, "altro");
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, altro));
        Configura(altro);
        Assert.Null(await RepoDellAip.SulRamoDelCicloAsync(_git, altro, "2611"));
        File.WriteAllText(Path.Combine(altro, "2611.txt"), "x");
        Directory.CreateDirectory(Path.Combine(altro, "2611"));
        File.WriteAllText(Path.Combine(altro, "2611", "li.json"), "{}");
        Esegui(altro, "add", "2611/li.json");
        Esegui(altro, "commit", "--quiet", "-m", "dall'altro AOD");
        Esegui(altro, "push", "--quiet", "-u", "origin", "aip/2611");

        var mio = Path.Combine(_radice, "mio");
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, mio));
        Configura(mio);
        File.WriteAllText(Path.Combine(mio, "README.md"), "cambiato a mano");

        Assert.Contains("non registrati su main", await RepoDellAip.SulRamoDelCicloAsync(_git, mio, "2611"), StringComparison.Ordinal);
        Esegui(mio, "checkout", "--quiet", "--", "README.md");
        Assert.Null(await RepoDellAip.SulRamoDelCicloAsync(_git, mio, "2611"));
        Assert.Equal("dall'altro AOD", Esegui(mio, "log", "-1", "--format=%s").Trim());
        Assert.Equal("origin/aip/2611", (await RepoDelSector.LeggiAsync(_git, mio)).Stato!.Monte);
    }

    [Fact]
    public async Task IlCicloNuovoNasceDalRamoDelCicloPrima_EVedeAncheQuello()
    {
        var lab = new SessioneDelLab(Path.Combine(_radice, "dati-del-lab"));
        lab.CambiaLIndirizzoDellAipEspanso(Server);
        Assert.True(lab.CambiaLaCartellaDeiPdf(CartellaDeiPdf("2611", "2612")), lab.Rifiuto);
        var espanso = Path.Combine(_radice, "aip-espanso");
        Assert.True(await lab.PrendiLAipEspansoAsync(espanso), lab.Rifiuto);
        Configura(espanso);
        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);
        Assert.True(await lab.RegistraLAipAsync("AIP 2611"), lab.Rifiuto);

        // Prima nasceva da origin/main: sul ramo del 2612 il 2611 non c'era (22 ottobre, preparando le prove a mano).
        Assert.True(await lab.LeggiLAipAsync("2612"), lab.Rifiuto);

        Assert.Equal("aip/2612", lab.StatoDellAip!.Ramo);
        Assert.True(File.Exists(Path.Combine(espanso, "2611", "li.json")));
        Assert.Equal(["2611", "2612"], lab.CicliConIlModello);
        Assert.Equal("2611", lab.UltimoAipEspanso!.Cambiate?.Ciclo);
        Assert.Contains("AIP 2611", Esegui(espanso, "log", "--format=%s", "aip/2612"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnCicloPrimaGiaFusoNonFaDaBase_SiParteDaMain()
    {
        var altro = Path.Combine(_radice, "altro");
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, altro));
        Configura(altro);
        Assert.Null(await RepoDellAip.SulRamoDelCicloAsync(_git, altro, "2611"));
        Directory.CreateDirectory(Path.Combine(altro, "2611"));
        File.WriteAllText(Path.Combine(altro, "2611", "li.json"), "{}");
        Esegui(altro, "add", "2611/li.json");
        Esegui(altro, "commit", "--quiet", "-m", "AIP 2611");
        Esegui(altro, "push", "--quiet", "-u", "origin", "aip/2611");
        // Il committente fonde il 2611, poi main va avanti.
        var primo = Path.Combine(_radice, "primo");
        Esegui(primo, "fetch", "--quiet");
        Esegui(primo, "merge", "--quiet", "--no-ff", "-m", "fonde il 2611", "origin/aip/2611");
        File.WriteAllText(Path.Combine(primo, "README.md"), "# dopo la fusione\n");
        Esegui(primo, "commit", "--quiet", "-am", "README dopo la fusione");
        Esegui(primo, "push", "--quiet", "origin", "main");

        var mio = Path.Combine(_radice, "mio");
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, mio));
        Configura(mio);
        Assert.Null(await RepoDellAip.SulRamoDelCicloAsync(_git, mio, "2611"));
        Assert.Null(await RepoDellAip.SulRamoDelCicloAsync(_git, mio, "2612"));

        Assert.Equal("README dopo la fusione", Esegui(mio, "log", "-1", "--format=%s").Trim());
        Assert.True(File.Exists(Path.Combine(mio, "2611", "li.json")));
    }

    [Fact]
    public async Task SuMainNonSiRegistraNeSiManda()
    {
        var clone = Path.Combine(_radice, "su-main");
        Assert.Null(await RepoDellAip.PrendiAsync(_git, Server, clone));
        Configura(clone);
        Directory.CreateDirectory(Path.Combine(clone, "2611"));
        File.WriteAllText(Path.Combine(clone, "2611", "li.json"), "{}");
        var stato = (await RepoDelSector.LeggiAsync(_git, clone)).Stato!;

        Assert.Contains("mai su main", await RepoDellAip.RegistraAsync(_git, stato, "a mano"), StringComparison.Ordinal);
        Esegui(clone, "add", "2611/li.json");
        Esegui(clone, "commit", "--quiet", "-m", "a mano su main");
        stato = (await RepoDelSector.LeggiAsync(_git, clone)).Stato!;
        Assert.Contains("mai main", await RepoDellAip.MandaAsync(_git, stato), StringComparison.Ordinal);
        Assert.Equal("Add initial README", Esegui(Server, "log", "-1", "--format=%s", "main").Trim());
    }

    [Fact]
    public async Task UnaCartellaDentroUnAltroRepoNonEIlRepoDellAip()
    {
        var dentro = Directory.CreateDirectory(Path.Combine(_radice, "primo", "sotto")).FullName;
        var lab = new SessioneDelLab(Path.Combine(_radice, "dati-del-lab"));
        Assert.True(lab.CambiaLAipEspanso(dentro), lab.Rifiuto);

        await lab.RileggiIlRepoDellAipAsync();

        Assert.False(RepoDellAip.EUnRepo(dentro));
        Assert.Null(lab.StatoDellAip);
    }
}

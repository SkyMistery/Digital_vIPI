using Vipi.SectorLab.Core.Aurora;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Carta F5, slice 4: le prove in Aurora come lotti — un ramo <c>prova/…</c> con la variante e le domande nel lotto; il
/// ramo si butta e non si manda. La prima prova: l'ordine a blocchi delle aerovie («file per file» B9).
/// </summary>
public sealed class ProveTests : IDisposable
{
    /// <summary>Un <c>itawlow.lairway</c> in piccolo, con la forma del vero: intestazione di KY139, tracciati, etichette, lista in fondo.</summary>
    private const string Aerovie =
        "//////////\r\n" +
        "//KY139 inserita manualmente. NON CANCELLARE\r\n" +
        "T;KY139;TOP;TOP;\r\n" +
        "T;KY139;CHI;CHI;\r\n" +
        "\r\n" +
        "L;KY139;N045.00.39.623;E011.31.41.858;\r\n" +
        "//////////\r\n" +
        "\r\n" +
        "//Airway tracks\r\n" +
        "T;A1;BANKO;BANKO;\r\n" +
        "T;A1;WEBCA;WEBCA;\r\n" +
        "T;L613;PAPIZ;PAPIZ;\r\n" +
        "T;L613;RIVAM;RIVAM;\r\n" +
        "\r\n" +
        "T;BREAK;RIVAM;RIVAM;\r\n" +
        "T;L613;LAPAB;LAPAB;\r\n" +
        "T;L613;VIC;VIC;\r\n" +
        "T;L615;VIC;VIC;\r\n" +
        "T;L615;BORMI;BORMI;\r\n" +
        "T;BREAK;BORMI;BORMI; //discontinuity (creates a break)\r\n" +
        "\r\n" +
        "//Airway labels\r\n" +
        "L;A1;N044.00.00.000;E010.00.00.000;\r\n" +
        "L;L615-L613;N045.30.00.000;E011.30.00.000;\r\n" +
        "L;L615;N045.40.00.000;E011.40.00.000;\r\n" +
        "\r\n" +
        "//Waypoint references\r\n" +
        "//ABADI;N040.45.19.000;E018.38.30.000;\r\n";

    private readonly AlberoDiProva _albero = new();

    /// <summary>La vetrina fuori dal clone: il Lab non specchia mai dentro un clone git.</summary>
    private readonly string _vetrina = Path.Combine(Path.GetTempPath(), "vetrina-prova-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_vetrina))
            Directory.Delete(_vetrina, recursive: true);
        foreach (string file in Directory.EnumerateFiles(_albero.Radice, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        _albero.Dispose();
    }

    private static List<string> Dati(string testo)
        => [.. testo.Split("\r\n").Where(r => r.TrimStart().StartsWith("T;", StringComparison.Ordinal) || r.TrimStart().StartsWith("L;", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    [Fact]
    public void ABlocchi_LeRigheSiSpostanoNonCambiano()
    {
        var esito = OrdineABlocchiDelleAerovie.Riordina(Aerovie, out string? perche);

        Assert.Null(perche);
        var (testo, conti) = esito!.Value;
        Assert.Equal(Dati(Aerovie), Dati(testo));
        Assert.Equal((4, 10, 4, 2, 7, "L613", "L615-L613"), (conti.Aerovie, conti.Tracciati, conti.Etichette, conti.Break, conti.Tolte, conti.ConBreak, conti.Condivisa));
        Assert.Equal(["RIVAM e LAPAB"], conti.BreakFra);
        Assert.EndsWith("\r\n", testo, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", testo.Replace("\r\n", "", StringComparison.Ordinal), StringComparison.Ordinal);

        var righe = testo.Split("\r\n");
        // L'intestazione prima del primo dato e la lista in fondo restano com'erano.
        Assert.Equal(["//////////", "//KY139 inserita manualmente. NON CANCELLARE", "//@\"KY139\"", "//@START", "L;KY139;N045.00.39.623;E011.31.41.858;"], righe[..5]);
        Assert.Equal(["//Waypoint references", "//ABADI;N040.45.19.000;E018.38.30.000;", ""], righe[^3..]);
        // L613: la condivisa con L615 (prima in ordine alfabetico), poi il tracciato col BREAK dov'era.
        int l613 = Array.IndexOf(righe, "//@\"L613\"");
        Assert.Equal(
        [
            "//@\"L613\"", "//@START", "L;L615-L613;N045.30.00.000;E011.30.00.000;",
            "T;L613;PAPIZ;PAPIZ;", "T;L613;RIVAM;RIVAM;", "T;BREAK;RIVAM;RIVAM;", "T;L613;LAPAB;LAPAB;", "T;L613;VIC;VIC;",
            "//@END \"L613\"",
        ], righe[l613..(l613 + 9)]);
        // Il BREAK in coda a L615 resta in coda a lei; i commenti di sezione se ne vanno.
        int l615 = Array.IndexOf(righe, "//@END \"L615\"");
        Assert.Equal("T;BREAK;BORMI;BORMI; //discontinuity (creates a break)", righe[l615 - 1]);
        Assert.DoesNotContain("//Airway tracks", righe);
        Assert.DoesNotContain("//Airway labels", righe);
    }

    [Fact]
    public void ABlocchi_UnaDichiarazioneCheCeGiaResta_EUnBloccoVecchioSiRifa()
    {
        string conBlocco = Aerovie.Replace("T;A1;BANKO;BANKO;\r\n", "//@\"A1\" locked=si\r\n//@START\r\n//@@\"BANKO\" dir=both\r\nT;A1;BANKO;BANKO;\r\n", StringComparison.Ordinal)
            .Replace("T;A1;WEBCA;WEBCA;\r\n", "T;A1;WEBCA;WEBCA;\r\n//@END \"A1\"\r\n", StringComparison.Ordinal);

        var righe = OrdineABlocchiDelleAerovie.Riordina(conBlocco, out _)!.Value.Testo.Split("\r\n");

        int a1 = Array.IndexOf(righe, "//@\"A1\" locked=si");
        Assert.Equal(["//@\"A1\" locked=si", "//@START", "L;A1;N044.00.00.000;E010.00.00.000;", "//@@\"BANKO\" dir=both", "T;A1;BANKO;BANKO;", "T;A1;WEBCA;WEBCA;", "//@END \"A1\""],
            righe[a1..(a1 + 7)]);
        // Il //@START e il //@END che c'erano non si ripetono: uno per blocco.
        Assert.Equal(4, righe.Count(r => r == "//@START"));
        Assert.Single(righe, r => r == "//@END \"A1\"");
    }

    /// <summary>In Aurora un'aerovia non si cerca per nome (committente, 18 ottobre): le domande 3 e 4 dicono i fix.</summary>
    [Fact]
    public void ABlocchi_LeDomandeDiconoIFixDoveGuardare()
    {
        string comune = Aerovie.Replace("T;L615;VIC;VIC;\r\n", "T;L615;LAPAB;LAPAB;\r\nT;L615;VIC;VIC;\r\n", StringComparison.Ordinal);

        var conti = OrdineABlocchiDelleAerovie.Riordina(comune, out _)!.Value.Conti;

        Assert.Equal(["RIVAM e LAPAB"], conti.BreakFra);
        Assert.Equal(["LAPAB", "VIC"], conti.CondivisaFra);
    }

    [Fact]
    public void ABlocchi_UnAeroviaCheTornaDopoAltre_NonSiRiordina()
    {
        string sparsa = Aerovie.Replace("T;L615;BORMI;BORMI;\r\n", "T;L615;BORMI;BORMI;\r\nT;A1;ZZZZZ;ZZZZZ;\r\n", StringComparison.Ordinal);

        Assert.Null(OrdineABlocchiDelleAerovie.Riordina(sparsa, out string? perche));
        Assert.Contains("A1 torna più avanti", perche, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlocchi_DomandeDalFile()
    {
        _albero.Scrivi(OrdineABlocchiDelleAerovie.File, Aerovie);

        var preparata = new OrdineABlocchiDelleAerovie().Prepara(_albero.Radice, out string? perche);

        Assert.Null(perche);
        Assert.Equal(OrdineABlocchiDelleAerovie.File, Assert.Single(preparata!.File).Percorso);
        Assert.Contains(preparata.Domande, d => d.Testo.Contains("4 aerovie", StringComparison.Ordinal));
        Assert.Contains(preparata.Domande, d => d.Testo.StartsWith("L613 resta spezzata", StringComparison.Ordinal) && d.Dove.StartsWith("fra RIVAM e LAPAB", StringComparison.Ordinal));
        Assert.Contains(preparata.Domande, d => d.Testo.Contains("(L615-L613)", StringComparison.Ordinal) && d.Dove.Contains("L615 e L613", StringComparison.Ordinal));
    }

    /// <summary>
    /// Il giro intero — «finita quando» della slice: prepara (ramo prova/…, variante registrata, domande nel lotto),
    /// porta, rispondi, «visto» (le risposte restano come esiti, non come note), butta (torna al ramo di prima, il ramo
    /// prova/… non c'è più). Il ramo di prova non si manda mai.
    /// </summary>
    [Fact]
    public async Task LaProva_PreparataGuardataButtata()
    {
        _albero.Scrivi(OrdineABlocchiDelleAerovie.File, Aerovie);
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"))
        {
            Aurora = (Path.Combine(_albero.Radice, "dati-del-lab", "aurora", "profile.ini"), Path.Combine(_albero.Radice, "dati-del-lab", "aurora", "SectorFiles")),
        };
        FanneUnRepo(lab);
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.RileggiIlRamoAsync();
        Assert.True(lab.CambiaLaVetrina(_vetrina), lab.Rifiuto);

        Assert.True(await lab.PreparaLaProvaAsync("aerovie-a-blocchi"), lab.Rifiuto);

        Assert.Equal("prova/aerovie-a-blocchi", lab.Ramo!.Ramo);
        Assert.Equal("master", lab.BaseDelRamo);
        Assert.NotNull(lab.ProvaInCorso);
        Assert.Contains("//@END \"L613\"", File.ReadAllText(_albero.Percorso(OrdineABlocchiDelleAerovie.File)), StringComparison.Ordinal);
        Assert.Empty(lab.Ramo.Cambiati);
        Assert.Contains("Prova in Aurora", Git(lab, "log", "-1", "--format=%s"), StringComparison.Ordinal);
        Assert.NotNull(Vipi.SectorLab.Core.Git.Invio.PercheNonSiManda(lab.Ramo));
        var domande = lab.Lotto!.Voci.Where(v => v.Prova == "aerovie-a-blocchi").ToList();
        Assert.Equal(5, domande.Count);
        Assert.All(domande, d => Assert.Null(d.Portata));

        // In vetrina: niente «cambiato ramo», le domande dicono già cosa guardare.
        Assert.True(await lab.PortaInAuroraAsync(), lab.Rifiuto);
        Assert.Contains("prima volta", lab.Lotto.Voci[^1].Cosa, StringComparison.Ordinal);
        Assert.Equal(6, lab.Lotto.DaVedere);
        Assert.True(lab.SegnaNelLotto(0, EsitoInAurora.Come));
        Assert.True(lab.SegnaNelLotto(2, EsitoInAurora.NonVa, "L613 unita"));
        lab.VistoInAurora();

        Assert.Empty(lab.NoteDaAurora);
        Assert.Equal(5, lab.Lotto.EsitiDelleProve.Count);
        Assert.Equal("L613 unita", lab.Lotto.EsitiDelleProve.Single(e => e.Esito == EsitoInAurora.NonVa).Nota);

        // Butta: si torna a master, il ramo di prova non c'è più, le risposte restano.
        Assert.True(await lab.ButtaLaProvaAsync(), lab.Rifiuto);
        Assert.Equal("master", lab.Ramo!.Ramo);
        Assert.Null(lab.ProvaInCorso);
        Assert.DoesNotContain("prova/aerovie-a-blocchi", lab.RamiLocali);
        Assert.Contains("//Airway tracks", File.ReadAllText(_albero.Percorso(OrdineABlocchiDelleAerovie.File)), StringComparison.Ordinal);
        Assert.Equal(5, Lotto.Leggi(Path.Combine(_albero.Radice, "dati-del-lab"), _albero.Radice).EsitiDelleProve.Count);
    }

    [Fact]
    public async Task LaProva_NonSiPreparaSopraUnAltra_NeFuoriDaGit()
    {
        _albero.Scrivi(OrdineABlocchiDelleAerovie.File, Aerovie);
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.RileggiIlRamoAsync();
        Assert.False(await lab.PreparaLaProvaAsync("aerovie-a-blocchi"));
        Assert.False(await lab.PreparaLaProvaAsync("non-esiste"));
        Assert.Contains("non conosce", lab.Rifiuto, StringComparison.Ordinal);

        FanneUnRepo(lab);
        await lab.RileggiIlRamoAsync();
        Assert.True(await lab.PreparaLaProvaAsync("aerovie-a-blocchi"), lab.Rifiuto);
        Assert.False(await lab.PreparaLaProvaAsync("aerovie-a-blocchi"));
        Assert.Contains("già sulla prova", lab.Rifiuto, StringComparison.Ordinal);
    }

    private string Git(SessioneDelLab lab, params string[] argomenti)
    {
        var esito = lab.Git.EseguiAsync(_albero.Radice, argomenti).GetAwaiter().GetResult();
        Assert.True(esito.Riuscito, esito.Errore);
        return esito.Uscita;
    }

    /// <summary>L'albero di prova diventa un clone col suo primo commit (i dati del Lab fuori da git).</summary>
    private void FanneUnRepo(SessioneDelLab lab)
    {
        Git(lab, "init", "--quiet", "--initial-branch=master");
        Git(lab, "config", "user.name", "Prova del Lab");
        Git(lab, "config", "user.email", "prova@example.invalid");
        Git(lab, "config", "core.autocrlf", "false");
        File.WriteAllText(_albero.Percorso(".gitignore"), "dati-del-lab/\n");
        Git(lab, "add", "--all");
        Git(lab, "commit", "--quiet", "-m", "primo");
    }
}

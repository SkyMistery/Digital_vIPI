using Vipi.SectorLab.Core.Aurora;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Le prove di organizzazione dei file («file per file» I7, K2, O4): la cartella per scalo (C), la cartella per tipo
/// (B), un file per ACC per gli spazi bassi (K2). Le righe si spostano, non cambiano; gli <c>.isc</c> perdono solo le
/// righe dei file spostati e guadagnano la cartella nella riga 6 di <c>[INFO]</c>.
/// </summary>
public sealed class ProveDiOrganizzazioneTests : IDisposable
{
    private const string Isc = "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n[GEO]\r\nF;GEO\\lirf.geo\r\nF;GEO\\liml.geo\r\n\r\n[ARTCC LOW]\r\nF;LOW_AIRSPACE\\limm_es0_app.lartcc\r\nF;LOW_AIRSPACE\\limm_tma.lartcc\r\nF;LOW_AIRSPACE\\lirf_star.lartcc\r\n\r\n[FILLCOLOR]\r\nF;GND_LAYOUT\\rf_ad_gnd.pol\r\n";

    private const string Geo = "//LIRF\r\nN041.0;E012.0;N041.1;E012.1;RUNWAY;\r\n//AD_BOUNDARY\r\nN041.2;E012.2;N041.3;E012.3;BUILDING;\r\nN041.4;E012.4;N041.5;E012.5;TAXI_CENTER;\r\nN041.6;E012.6;N041.7;E012.7;RUNWAY;\r\n";

    private readonly string _radice = Path.Combine(Path.GetTempPath(), "organizzazione-" + Guid.NewGuid().ToString("N"));

    public ProveDiOrganizzazioneTests()
    {
        Scrivi("SectorFiles/ITALY.isc", Isc);
        Scrivi("SectorFiles/Include/IT/GEO/lirf.geo", Geo);
        Scrivi("SectorFiles/Include/IT/GND_LAYOUT/rf_ad_gnd.pol", "STATIC;GRASS;1;GRASS;\r\nN041.0;E012.0;\r\n");
        Scrivi("SectorFiles/Include/IT/lirf.sid", "LIRF;16L;ELKA5A;\r\n");
        Scrivi("SectorFiles/Include/IT/lirf.gts", "101;LIRF;N041.48.16.944;E012.16.18.365;\r\n");
        Scrivi("SectorFiles/Include/IT/liml.sid", "LIML;35R;\r\n");
        Scrivi("SectorFiles/Include/IT/LOW_AIRSPACE/limm_es0_app.lartcc", "T;MM ES0;N045.0;E009.0;\r\n");
        Scrivi("SectorFiles/Include/IT/LOW_AIRSPACE/limm_tma.lartcc", "T;MM TMA;N045.5;E009.5;\r\n");
        Scrivi("SectorFiles/Include/IT/LOW_AIRSPACE/lirf_star.lartcc", "T;RF STAR;N041.0;E012.0;\r\n");
    }

    public void Dispose()
    {
        // git lascia i suoi oggetti in sola lettura: senza toglierla, su Windows la cancellazione fallisce.
        foreach (string file in Directory.EnumerateFiles(_radice, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_radice, recursive: true);
    }

    private void Scrivi(string relativo, string testo)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo);
    }

    private const string IscSenzaLirf = "\r\n\r\n[GEO]\r\nF;GEO\\liml.geo\r\n\r\n[ARTCC LOW]\r\nF;LOW_AIRSPACE\\limm_es0_app.lartcc\r\nF;LOW_AIRSPACE\\limm_tma.lartcc\r\nF;LOW_AIRSPACE\\lirf_star.lartcc\r\n\r\n[FILLCOLOR]\r\n";

    [Fact]
    public void C_TuttoLoScaloInUnaCartella()
    {
        var file = new CartellaPerScalo().Prepara(_radice, out string? perche)!.File.ToDictionary(f => f.Percorso, f => f.Testo);

        Assert.Null(perche);
        Assert.Equal("LIRF;16L;ELKA5A;\r\n", file["SectorFiles/Include/IT/AD/LIRF/lirf.sid"]);
        Assert.Equal(Geo, file["SectorFiles/Include/IT/AD/LIRF/lirf.geo"]);
        Assert.Equal("STATIC;GRASS;1;GRASS;\r\nN041.0;E012.0;\r\n", file["SectorFiles/Include/IT/AD/LIRF/lirf.tfl"]);
        foreach (string tolto in new[] { "SectorFiles/Include/IT/lirf.sid", "SectorFiles/Include/IT/lirf.gts", "SectorFiles/Include/IT/GEO/lirf.geo", "SectorFiles/Include/IT/GND_LAYOUT/rf_ad_gnd.pol" })
            Assert.Null(file[tolto]);
        // Gli altri scali non si toccano; la cartella entra nella riga 6.
        Assert.DoesNotContain("SectorFiles/Include/IT/liml.sid", file.Keys);
        Assert.Equal("[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT;IT\\AD\\LIRF" + IscSenzaLirf, file["SectorFiles/ITALY.isc"]);
    }

    [Fact]
    public void B_LaTerraDivisaPerTipo_ICommentiColLoroDato()
    {
        var file = new CartellaPerTipo().Prepara(_radice, out string? perche)!.File.ToDictionary(f => f.Percorso, f => f.Testo);

        Assert.Null(perche);
        Assert.Equal("//LIRF\r\nN041.0;E012.0;N041.1;E012.1;RUNWAY;\r\nN041.6;E012.6;N041.7;E012.7;RUNWAY;\r\n", file["SectorFiles/Include/IT/GND/lirf.rwy"]);
        Assert.Equal("//AD_BOUNDARY\r\nN041.2;E012.2;N041.3;E012.3;BUILDING;\r\n", file["SectorFiles/Include/IT/GND/lirf.bld"]);
        Assert.Equal("N041.4;E012.4;N041.5;E012.5;TAXI_CENTER;\r\n", file["SectorFiles/Include/IT/GND/lirf.txc"]);
        Assert.NotNull(file["SectorFiles/Include/IT/GND/lirf.tfl"]);
        Assert.Null(file["SectorFiles/Include/IT/GEO/lirf.geo"]);
        // Le SID stanno dove stavano: in B si muove solo la terra.
        Assert.DoesNotContain("SectorFiles/Include/IT/lirf.sid", file.Keys);
        Assert.Equal("[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT;IT\\GND" + IscSenzaLirf, file["SectorFiles/ITALY.isc"]);
    }

    [Fact]
    public void B_UnColoreSenzaFilePerTipo_FermaLaProva()
    {
        Scrivi("SectorFiles/Include/IT/GEO/lirf.geo", Geo + "N041.8;E012.8;N041.9;E012.9;COAST;\r\n");

        Assert.Null(new CartellaPerTipo().Prepara(_radice, out string? perche));
        Assert.Contains("COAST", perche, StringComparison.Ordinal);
    }

    /// <summary>Il giro git di una prova che toglie file e ne crea in cartelle nuove: tutto registrato, niente in sospeso.</summary>
    [Fact]
    public async Task C_NelRamoDiProva_FileTolti_ECartelleNuove()
    {
        var git = new Vipi.SectorLab.Core.Git.Git();
        async Task<string> G(params string[] a)
        {
            var e = await git.EseguiAsync(_radice, a);
            Assert.True(e.Riuscito, e.Errore);
            return e.Uscita;
        }

        await G("init", "--quiet", "--initial-branch=master");
        await G("config", "user.name", "Prova del Lab");
        await G("config", "user.email", "prova@example.invalid");
        await G("config", "core.autocrlf", "false");
        await G("add", "--all");
        await G("commit", "--quiet", "-m", "primo");

        var (preparata, perche) = await ProveInAurora.PreparaAsync(git, _radice, new CartellaPerScalo());

        Assert.Null(perche);
        Assert.NotNull(preparata);
        Assert.Equal("", (await G("status", "--porcelain")).Trim());
        Assert.False(File.Exists(Path.Combine(_radice, "SectorFiles", "Include", "IT", "lirf.sid")));
        Assert.True(File.Exists(Path.Combine(_radice, "SectorFiles", "Include", "IT", "AD", "LIRF", "lirf.sid")));
        Assert.Contains("SectorFiles/Include/IT/AD/LIRF/lirf.geo", await G("show", "--name-only", "--format=", "HEAD"), StringComparison.Ordinal);
    }

    [Fact]
    public void K2_UnFilePerAcc_LeStarDiScaloRestano()
    {
        var file = new UnFilePerAcc().Prepara(_radice, out string? perche)!.File.ToDictionary(f => f.Percorso, f => f.Testo);

        Assert.Null(perche);
        Assert.Equal("//limm_es0_app.lartcc\r\nT;MM ES0;N045.0;E009.0;\r\n\r\n//limm_tma.lartcc\r\nT;MM TMA;N045.5;E009.5;\r\n", file["SectorFiles/Include/IT/LOW_AIRSPACE/limm.lartcc"]);
        Assert.Null(file["SectorFiles/Include/IT/LOW_AIRSPACE/limm_tma.lartcc"]);
        Assert.DoesNotContain("SectorFiles/Include/IT/LOW_AIRSPACE/lirf_star.lartcc", file.Keys);
        Assert.Contains("[ARTCC LOW]\r\nF;LOW_AIRSPACE\\limm.lartcc\r\nF;LOW_AIRSPACE\\lirf_star.lartcc\r\n", file["SectorFiles/ITALY.isc"], StringComparison.Ordinal);
    }
}

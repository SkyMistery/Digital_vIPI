using Vipi.SectorLab.Core.Aurora;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Le prove in Aurora dopo la B9 («file per file»): F5 i VFR della FIR sotto <c>[VFRFIX]</c>, A8 le etichette dei fix di
/// confine per riferimento, R4 i punti di startup col tasto HOLD. Ogni prova cambia solo le righe che deve.
/// </summary>
public sealed class AltreProveTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "altre-prove-" + Guid.NewGuid().ToString("N"));

    public AltreProveTests() => Directory.CreateDirectory(_radice);

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private void Scrivi(string relativo, string testo)
    {
        string percorso = Path.Combine(_radice, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo);
    }

    [Fact]
    public void F5_SoloIFileDellaFir_SottoVfrfix()
    {
        Scrivi("SectorFiles/ITALY.isc", "[INFO]\r\nIT\r\n\r\n[VFRENR]\r\nF;ENRVFI\\limm.vfi\r\nF;ENRVFI\\lirr.vfi\r\n\r\n[ARTCC]\r\nF;ACC\\FRA.artcc\r\n");
        Scrivi("SectorFiles/LIMM.isc", "[VFRENR]\r\nF;ENRVFI\\limm.vfi\r\nF;ALTRO\\x.vfi\r\n[GEO]\r\n");
        Scrivi("SectorFiles/LIBB.isc", "[GEO]\r\nF;GEO\\itgeo.geo\r\n");
        Scrivi("SectorFiles/Include/IT/ENRVFI/limm.vfi", "LAVENO;MMN1;N0455440000;E0083700000;\r\n");

        var preparata = new VfrDiFirComeFix().Prepara(_radice, out string? perche);

        Assert.Null(perche);
        var file = preparata!.File.ToDictionary(f => f.Percorso, f => f.Testo);
        Assert.Equal(["SectorFiles/ITALY.isc", "SectorFiles/LIMM.isc"], file.Keys.Order(StringComparer.Ordinal));
        // Solo i file della FIR: il titolo cambia, il resto resta.
        Assert.Equal("[INFO]\r\nIT\r\n\r\n[VFRFIX]\r\nF;ENRVFI\\limm.vfi\r\nF;ENRVFI\\lirr.vfi\r\n\r\n[ARTCC]\r\nF;ACC\\FRA.artcc\r\n", file["SectorFiles/ITALY.isc"]);
        // Con un altro file nella sezione: quello della FIR va in una [VFRFIX] nuova, subito dopo.
        Assert.Equal("[VFRENR]\r\nF;ALTRO\\x.vfi\r\n[VFRFIX]\r\nF;ENRVFI\\limm.vfi\r\n[GEO]\r\n", file["SectorFiles/LIMM.isc"]);
        Assert.Contains(preparata.Domande, d => d.Dove.Contains("LAVENO (limm.vfi)", StringComparison.Ordinal));
    }

    [Fact]
    public void A8_LeEtichetteNellaStessaPosizione_PerRiferimento_EUnFixSiSposta()
    {
        Scrivi(EtichettePerRiferimento.File,
            "//FRA\r\nT;FRA BDRY;ABDAB;ABDAB;\r\nL;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\nL;ABESI;N046.09.35.000;E009.02.34.000;8;\r\nL;ZZZZZ;N040.00.00.000;E010.00.00.000;8;\r\n");
        Scrivi("SectorFiles/Include/IT/NAVAIDS/itfix.fix", "ABDAB;N037.53.21.000;E010.37.43.000;0;1;\r\nABESI;N046.09.35.000;E009.02.34.000;0;1;\r\nZZZZZ;N041.00.00.000;E010.00.00.000;0;\r\n");
        Scrivi("SectorFiles/Include/IT/NAVAIDS/ESTERNI.fix", "ABDAB;N037.53.21.000;E010.37.43.000;3;\r\n");

        var preparata = new EtichettePerRiferimento().Prepara(_radice, out string? perche);

        Assert.Null(perche);
        var file = preparata!.File.ToDictionary(f => f.Percorso, f => f.Testo);
        // ZZZZZ: il fix sta altrove, l'etichetta resta com'era.
        Assert.Equal("//FRA\r\nT;FRA BDRY;ABDAB;ABDAB;\r\nL;ABDAB;ABDAB;ABDAB;8;\r\nL;ABESI;ABESI;ABESI;8;\r\nL;ZZZZZ;N040.00.00.000;E010.00.00.000;8;\r\n", file[EtichettePerRiferimento.File]);
        // Si sposta il primo fix che sta in un file solo: ABESI (ABDAB è in due file).
        Assert.Equal("ABDAB;N037.53.21.000;E010.37.43.000;0;1;\r\nABESI;N046.12.35.000;E009.02.34.000;0;1;\r\nZZZZZ;N041.00.00.000;E010.00.00.000;0;\r\n", file["SectorFiles/Include/IT/NAVAIDS/itfix.fix"]);
        Assert.Contains(preparata.Domande, d => d.Testo.StartsWith("L'etichetta di ABESI si è spostata", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("N046.02.37.000", 3, "N046.05.37.000")]
    [InlineData("N046.58.37.120", 3, "N047.01.37.120")]
    [InlineData("S000.01.00.000", 3, "N000.02.00.000")]
    public void A8_UnaLatitudinePiuANord(string lat, int primi, string attesa) => Assert.Equal(attesa, EtichettePerRiferimento.PiuANord(lat, primi));

    [Fact]
    public void R4_LaMappaStartupDopoIlMaps_DueTrattini()
    {
        Scrivi("SectorFiles/Include/IT/lirf.str",
            "//MENU MAPS\r\nLIRF;MAPS;LIRF CTR; ; ;1;\r\nN042.02.22.000;E012.21.18.000;\r\n\r\nLIRF;07:16L:16R;HLD-CMP; ; ;2;\r\nCMP;CMP;\r\n");
        Scrivi("SectorFiles/Include/IT/lirf.gts", "101;LIRF;N041.48.16.944;E012.16.18.365;\r\n102;LIRF;N041.48.16.209;E012.16.15.712;\r\n");

        var preparata = new PuntiDiStartup().Prepara(_radice, out string? perche);

        Assert.Null(perche);
        Assert.Equal(
            "//MENU MAPS\r\nLIRF;MAPS;LIRF CTR; ; ;1;\r\nN042.02.22.000;E012.21.18.000;\r\n\r\n" +
            "LIRF;MAPS;STARTUP; ; ;2;\r\nN041.48.16.944;E012.16.17.865;\r\nN041.48.16.944;E012.16.18.865;S1;\r\n" +
            "N041.48.16.209;E012.16.15.212;<br>\r\nN041.48.16.209;E012.16.16.212;S2;\r\n\r\n" +
            "LIRF;07:16L:16R;HLD-CMP; ; ;2;\r\nCMP;CMP;\r\n",
            Assert.Single(preparata!.File).Testo);
    }

    [Theory]
    [InlineData("E012.16.18.365", -0.5, "E012.16.17.865")]
    [InlineData("E012.16.59.800", 0.5, "E012.17.00.300")]
    [InlineData("E000.00.00.200", -0.5, "W000.00.00.300")]
    public void R4_UnaLongitudineSpostata(string lon, double secondi, string attesa) => Assert.Equal(attesa, PuntiDiStartup.SpostaLaLongitudine(lon, secondi));

    [Fact]
    public void LeProveSenzaIlLoroFile_DiconoPerche()
    {
        foreach (var prova in new ProvaInAurora[] { new VfrDiFirComeFix(), new EtichettePerRiferimento(), new PuntiDiStartup() })
        {
            Assert.Null(prova.Prepara(_radice, out string? perche));
            Assert.False(string.IsNullOrWhiteSpace(perche));
        }
    }
}

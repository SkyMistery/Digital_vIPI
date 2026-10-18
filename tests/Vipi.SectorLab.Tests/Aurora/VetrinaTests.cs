using Vipi.SectorLab.Core.Aurora;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Carta F5, slice 1: la vetrina — la cartella fissa che Aurora legge come «secondo sector», riempita dal Lab come
/// specchio della <c>SectorFiles</c> del sector aperto; e la sola riga <c>SecondSector</c> delle impostazioni di Aurora.
/// </summary>
public sealed class VetrinaTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "vetrina-" + Guid.NewGuid().ToString("N"));

    public VetrinaTests() => Directory.CreateDirectory(_radice);

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private string P(params string[] parti) => Path.Combine([_radice, .. parti]);

    private void Scrivi(string percorso, string testo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo);
    }

    [Fact]
    public void PortaInAurora_ESpecchio_EDiceCosaCe()
    {
        Scrivi(P("ciclo", "SectorFiles", "ITALY.isc"), "[INFO]\r\n");
        Scrivi(P("ciclo", "SectorFiles", "Include", "IT", "lirf.sid"), "LIRF;16L;EKLO8R;\r\n");
        Scrivi(P("prova", "SectorFiles", "ITALY.isc"), "[INFO]\r\n");
        Scrivi(P("prova", "SectorFiles", "Include", "IT", "symbols.sym"), "//APT\r\n");
        string vetrina = P("vetrina");
        var quando = new DateTimeOffset(2026, 10, 18, 9, 30, 0, TimeSpan.Zero);

        var prima = Vetrina.Porta(P("ciclo"), vetrina, P("aurora", "SectorFiles"), "airac/2611", "abc1234", quando, out string? perche);

        Assert.Null(perche);
        Assert.Equal(new EsitoDellaVetrina(2, 0, 0), prima);
        Assert.True(File.Exists(P("vetrina", "SectorFiles", "Include", "IT", "lirf.sid")));
        Assert.Equal(new InVetrina(P("ciclo"), "airac/2611", "abc1234", quando), Vetrina.Contenuto(vetrina));

        // L'altro sector a turno: ITALY.isc uguale resta, lirf.sid se ne va (e la sua cartella vuota), symbols.sym arriva.
        var dopo = Vetrina.Porta(P("prova"), vetrina, P("aurora", "SectorFiles"), "prova/t4", null, quando.AddMinutes(5), out perche);

        Assert.Null(perche);
        Assert.Equal(new EsitoDellaVetrina(1, 1, 1), dopo);
        Assert.False(File.Exists(P("vetrina", "SectorFiles", "Include", "IT", "lirf.sid")));
        Assert.True(File.Exists(P("vetrina", "SectorFiles", "Include", "IT", "symbols.sym")));
        Assert.Equal("prova/t4", Vetrina.Contenuto(vetrina)!.Ramo);
    }

    [Fact]
    public void LaVetrinaNonEMaiUnaCartellaCheNonESua()
    {
        Scrivi(P("piena", "mio.txt"), "non toccare");
        Directory.CreateDirectory(P("clone", ".git"));
        Directory.CreateDirectory(P("clone", "sotto"));

        Assert.Contains("non è vuota", Vetrina.PercheNonVa(P("piena"), null), StringComparison.Ordinal);
        Assert.Contains("clone git", Vetrina.PercheNonVa(P("clone", "sotto"), null), StringComparison.Ordinal);
        Assert.Contains("pubblicato", Vetrina.PercheNonVa(P("aurora"), P("aurora", "SectorFiles")), StringComparison.Ordinal);
        Assert.Contains("per intero", Vetrina.PercheNonVa("vetrina", null), StringComparison.Ordinal);
        Assert.Null(Vetrina.PercheNonVa(P("nuova"), P("aurora", "SectorFiles")));
        // Una cartella col segno è una vetrina del Lab anche se è piena.
        Scrivi(P("piena", Vetrina.Segno), "");
        Assert.Null(Vetrina.PercheNonVa(P("piena"), null));
    }

    [Fact]
    public void DelleImpostazioniDiAuroraSiLeggeSoloIlSecondoSector()
    {
        string ini = P("profile.ini");
        string secondo = Vetrina.SectorFilesDi(P("vetrina"));
        Scrivi(ini, $"[SESSION]\r\naccess_token=SEGRETO\r\n\r\n[CREDS]\r\nVID=1\r\npassword=SEGRETA\r\n\r\n[SECTOR]\r\nSecondSector={secondo}\r\n");

        Assert.Equal(secondo, Vetrina.SecondoSector(ini));
        Assert.True(Vetrina.AuroraGuarda(Vetrina.SecondoSector(ini), P("vetrina")));
        Assert.False(Vetrina.AuroraGuarda(Vetrina.SecondoSector(ini), P("altra")));
        Assert.Null(Vetrina.SecondoSector(P("non-ce.ini")));
    }
}

using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 15b (carta «file per file» E3, E5, S3): i controlli delle MVA di ACC e di scalo — l'etichetta
/// fuori da ogni zona, la zona senza etichetta, la quota che non è una quota o non è in centinaia, il gruppo che manca
/// (anche sui separatori), il nome che non è quello dello scalo.
/// </summary>
public sealed class ControlloDelleMvaTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "mva-" + Guid.NewGuid().ToString("N"));

    private const string Dummy = "T;DUMMY;N000.00.00.000;E000.00.00.000;";

    public ControlloDelleMvaTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "ENRMVA"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", ""));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    // Un quadrato di un grado: il vertice in basso a sinistra, col nome e col gruppo nel 5° campo (di ACC) o senza.
    private static string[] Quadrato(string nome, int lat, int lon, string? gruppo, bool chiuso = true)
    {
        string coda = gruppo is null ? ";" : $";{gruppo};";
        string[] punti =
        [
            $"N0{lat}.00.00.000;E0{lon}.00.00.000", $"N0{lat + 1}.00.00.000;E0{lon}.00.00.000",
            $"N0{lat + 1}.00.00.000;E0{lon + 1}.00.00.000", $"N0{lat}.00.00.000;E0{lon + 1}.00.00.000",
        ];
        return [.. (chiuso ? punti.Append(punti[0]) : punti).Select(p => $"T;{nome};{p}{coda}")];
    }

    private void ScriviDiAcc(params string[] righe) => Scrivi(@"Include\IT\ENRMVA\lizz.mva", Righe(righe));

    [Fact]
    public void UnEtichettaFuoriDaOgniZonaEUnaZonaChiusaSenzaEtichetta()
    {
        ScriviDiAcc([
            "L;LIZZ;N041.30.00.000;E012.30.00.000;100;8;", .. Quadrato("LIZZ", 41, 12, "LIZZ"), "T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;", "",
            // Un'etichetta in mezzo al mare, lontana da tutto.
            "L;LIZZ;N035.30.00.000;E012.30.00.000;60;8;", "",
            // Una zona chiusa senza la sua etichetta; una linea aperta non è una zona.
            .. Quadrato("LIZZ", 43, 12, "LIZZ"), "T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;", "",
            .. Quadrato("LIZZ", 45, 12, "LIZZ", chiuso: false), "T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;"]);

        var fuori = Assert.Single(Di(Regola.EtichettaFuoriDallaZona));
        Assert.Equal(9, fuori.Riga);
        Assert.Contains("60", fuori.Dettaglio, StringComparison.Ordinal);
        Assert.Equal(Gravita.Avviso, fuori.Gravita);

        var senza = Assert.Single(Di(Regola.ZonaSenzaEtichetta));
        Assert.Equal(11, senza.Riga);
    }

    [Fact]
    public void UnEtichettaInGradiDecimaliEUnVerticePerNomeSiLeggonoComeGliAltri()
    {
        // lipx.mva riga 14: `L;MM ES0;45.55756591;10.27902575;60;8;`. ENRMVA/lirr.mva: `T;LIRR;UTENO;UTENO;LIRR;`.
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "NAVAIDS"));
        Scrivi(@"Include\IT\NAVAIDS\ENR.fix", Righe("ANGOL;N041.00.00.000;E012.00.00.000;0;"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", "[FIXES]", @"F;NAVAIDS\ENR.fix"));
        string[] quadrato = Quadrato("LIZZ", 41, 12, "LIZZ");
        quadrato[0] = "T;LIZZ;ANGOL;ANGOL;LIZZ;";
        ScriviDiAcc(["L;LIZZ;41.5;12.5;100;8;", .. quadrato, "T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;"]);

        Assert.Empty(Di(Regola.EtichettaFuoriDallaZona));
        Assert.Empty(Di(Regola.ZonaSenzaEtichetta));
    }

    [Theory]
    [InlineData("100", null)]
    [InlineData("TRL", null)]
    [InlineData("NO MINIMA", null)]
    [InlineData("70/TRL", null)]
    [InlineData("*30/40", null)]
    [InlineData("ALTA", Regola.QuotaNonValida)]
    [InlineData("", Regola.QuotaNonValida)]
    [InlineData("2500", Regola.QuotaNonInCentinaia)]
    [InlineData("FL85", Regola.QuotaNonInCentinaia)]
    public void LaQuotaEInCentinaiaOUnValoreSpeciale(string quota, Regola? attesa)
    {
        ScriviDiAcc(["L;LIZZ;N041.30.00.000;E012.30.00.000;" + quota + ";8;", .. Quadrato("LIZZ", 41, 12, "LIZZ"), "T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;"]);

        var problemi = Validatore.ValidaLAlbero(_radice).Where(p => p.Regola is Regola.QuotaNonValida or Regola.QuotaNonInCentinaia).ToList();

        Assert.Equal(attesa is null ? [] : new[] { attesa.Value }, problemi.Select(p => p.Regola));
    }

    [Fact]
    public void UnaQuotaPienaHaLaRigaInCentinaia()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT"));
        Scrivi(@"Include\IT\lizz.mva", Righe(["L;LIZZ;N041.30.00.000;E012.30.00.000;2500;8;", "L;LIZZ;N041.40.00.000;E012.30.00.000;FL85;8;", .. Quadrato("LIZZ", 41, 12, null)]));

        var problemi = Di(Regola.QuotaNonInCentinaia);

        Assert.Equal(["L;LIZZ;N041.30.00.000;E012.30.00.000;25;8;", "L;LIZZ;N041.40.00.000;E012.30.00.000;85;8;"], problemi.Select(p => p.Proposta));
    }

    [Fact]
    public void InUnaMvaDiAccIlGruppoStaSuOgniRigaAncheSuiSeparatori()
    {
        ScriviDiAcc([
            "L;LIZZ;N041.30.00.000;E012.30.00.000;100;8;",
            "T;LIZZ;N041.00.00.000;E012.00.00.000;LIZZ;",
            // Senza gruppo, e con un altro gruppo.
            "T;LIZZ;N042.00.00.000;E012.00.00.000;",
            "T;LIZZ;N042.00.00.000;E013.00.00.000;LIRR;",
            "T;LIZZ;N041.00.00.000;E013.00.00.000;LIZZ;",
            "T;LIZZ;N041.00.00.000;E012.00.00.000;LIZZ;",
            // Il separatore senza gruppo fa comparire la voce DUMMY nella MVA Selection di Aurora.
            Dummy]);

        var problemi = Di(Regola.GruppoMancanteNellaMva);

        Assert.Equal([3, 4, 7], problemi.Select(p => p.Riga));
        Assert.Equal("T;LIZZ;N042.00.00.000;E012.00.00.000;LIZZ;", problemi[0].Proposta);
        Assert.Equal("T;LIZZ;N042.00.00.000;E013.00.00.000;LIZZ;", problemi[1].Proposta);
        Assert.Equal("T;DUMMY;N000.00.00.000;E000.00.00.000;LIZZ;", problemi[2].Proposta);
        Assert.Contains("DUMMY", problemi[2].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaMvaDiScaloSiChiamaComeLoScalo_UnAvvisoPerFile()
    {
        // Lo stile delle ACC col nome del settore; un nome per zona; e quella già giusta.
        Scrivi(@"Include\IT\lirn.mva", Righe(["L;RR US0;N041.30.00.000;E012.30.00.000;110;8;", .. Quadrato("RR US0", 41, 12, null), Dummy]));
        Scrivi(@"Include\IT\liba.mva", Righe([.. Quadrato("ZONA1", 41, 12, null), "", .. Quadrato("ZONA2", 43, 12, null), "",
            "L;60;N041.30.00.000;E012.30.00.000;60;7;", "L;90;N043.30.00.000;E012.30.00.000;90;7;"]));
        Scrivi(@"Include\IT\lizz.mva", Righe(["L;LIZZ;N041.30.00.000;E012.30.00.000;110;8;", .. Quadrato("LIZZ", 41, 12, null), Dummy]));

        var problemi = Di(Regola.MvaNonDelloScalo);

        Assert.Equal(["liba.mva", "lirn.mva"], problemi.Select(p => Path.GetFileName(p.File)));
        Assert.All(problemi, p => Assert.Equal(1, p.Riga));
        Assert.Contains("LIBA", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("4 nomi", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("«RR US0»", problemi[1].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("LIRN", problemi[1].Dettaglio, StringComparison.Ordinal);
        // Di scalo il gruppo nel 5° campo non c'è (ancora): non è un avviso riga per riga.
        Assert.Empty(Di(Regola.GruppoMancanteNellaMva));
        // E nei file «un nome per zona» le zone non si ricavano dai poligoni: niente «zona senza etichetta».
        Assert.Empty(Di(Regola.ZonaSenzaEtichetta));
    }
}

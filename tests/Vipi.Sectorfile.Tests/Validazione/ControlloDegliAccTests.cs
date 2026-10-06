using System.Globalization;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 14a (carta «file per file» A4): gli avvisi degli <c>.artcc</c> — il cerchio di un gate non
/// chiuso, col centro fuori dal confine, la stanghetta di un'AOCC che non è di 15 NM, l'etichetta lontana dal fix che
/// porta il suo nome. 🔴 Non «gate ≠ 10 NM» né «etichetta fuori centro»: quelle eccezioni sono vere.
/// </summary>
public sealed class ControlloDegliAccTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "acc-" + Guid.NewGuid().ToString("N"));

    // Il confine di prova: lungo il parallelo 45°, da E010 a E011.
    private const double Parallelo = 45.0;

    public ControlloDegliAccTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "NAVAIDS"));
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "ACC"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[FIXES]", @"F;NAVAIDS\itfix.fix", "[ARTCC]", @"F;ACC\FRA.artcc", @"F;ACC\FRA-gates.artcc"));
        Scrivi(@"Include\IT\NAVAIDS\itfix.fix", Righe(
            "ABDAB;N045.00.00.000;E010.00.00.000;0;1;",
            "LUSIL;N045.00.00.000;E011.00.00.000;0;1;"));
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

    private static string Punto(double lat, double lon)
        => CoordinateConverter.LatitudeToDottedDms(lat) + ";" + CoordinateConverter.LongitudeToDottedDms(lon);

    // Un punto a nord e a est di un altro, in miglia.
    private static (double Lat, double Lon) Da((double Lat, double Lon) p, double nordNm, double estNm)
        => (p.Lat + (nordNm / 60), p.Lon + (estNm / (60 * Math.Cos(p.Lat * Math.PI / 180))));

    // Un cerchio di mezzo miglio, un punto ogni 10°: chiuso ripete il primo punto in fondo, aperto no.
    private static IEnumerable<string> Cerchio((double Lat, double Lon) centro, bool chiuso)
    {
        for (int g = 0; g < (chiuso ? 37 : 36); g++)
        {
            double a = g * 10 * Math.PI / 180;
            var p = Da(centro, 0.5 * Math.Cos(a), 0.5 * Math.Sin(a));
            yield return "T;COPs;" + Punto(p.Lat, p.Lon) + ";";
        }
    }

    private const string Dummy = "T;DUMMY;N000.00.00.000;E000.00.00.000;";

    private void ScriviIlConfine(params string[] dopo)
        => Scrivi(@"Include\IT\ACC\FRA.artcc", Righe([
            "//confine",
            "T;FRA BDRY;ABDAB;ABDAB;",
            "T;FRA BDRY;LUSIL;LUSIL;",
            Dummy,
            .. dopo]));

    [Fact]
    public void UnCerchioCuiMancaLUltimoPuntoNonEChiuso()
    {
        ScriviIlConfine();
        Scrivi(@"Include\IT\ACC\FRA-gates.artcc", Righe([
            "//X01-X02", .. Cerchio((Parallelo, 10.2), chiuso: true), Dummy,
            "//X07-X08", .. Cerchio((Parallelo, 10.4), chiuso: false), Dummy]));

        var p = Assert.Single(Di(Regola.CerchioNonChiuso));

        Assert.EndsWith("FRA-gates.artcc", p.File, StringComparison.Ordinal);
        Assert.Equal(41, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("X07-X08", p.Dettaglio, StringComparison.Ordinal);
        Assert.Empty(Di(Regola.CentroFuoriDalConfine));
    }

    [Fact]
    public void UnCerchioColCentroLontanoDalConfineEFuoriPosto()
    {
        ScriviIlConfine();
        var fuori = Da((Parallelo, 10.6), 2, 0);
        Scrivi(@"Include\IT\ACC\FRA-gates.artcc", Righe([
            "//Y01-Y02", .. Cerchio((Parallelo, 10.2), chiuso: true), Dummy,
            "//Y02-Y03", .. Cerchio(fuori, chiuso: true), Dummy]));

        var p = Assert.Single(Di(Regola.CentroFuoriDalConfine));

        Assert.Equal(41, p.Riga);
        Assert.Contains("Y02-Y03", p.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("2", p.Dettaglio, StringComparison.Ordinal);
        Assert.Empty(Di(Regola.CerchioNonChiuso));
    }

    [Fact]
    public void LaStanghettaDiUnAoccEDi15Miglia()
    {
        // Due AOCC: il gambo parte dal confine e scende di 10 NM; in fondo la stanghetta, di traverso. La prima è di
        // 15 NM, la seconda di 12.
        var piede1 = Da((Parallelo, 10.3), -10, 0);
        var piede2 = Da((Parallelo, 10.7), -10, 0);
        string[] Aocc((double Lat, double Lon) cima, (double Lat, double Lon) piede, double lunga) =>
        [
            "T;AOCC XX;" + Punto(cima.Lat, cima.Lon) + ";",
            "T;AOCC XX;" + Punto(piede.Lat, piede.Lon) + ";",
            Dummy,
            "T;AOCC XX;" + Punto(Da(piede, 0, -lunga / 2).Lat, Da(piede, 0, -lunga / 2).Lon) + ";",
            "T;AOCC XX;" + Punto(Da(piede, 0, lunga / 2).Lat, Da(piede, 0, lunga / 2).Lon) + ";",
            Dummy,
        ];
        ScriviIlConfine(["//AOCC buona", .. Aocc((Parallelo, 10.3), piede1, 15), "//AOCC corta", .. Aocc((Parallelo, 10.7), piede2, 12)]);

        var p = Assert.Single(Di(Regola.StanghettaDellAocc));

        // La stanghetta della seconda: 4 righe del confine, 1 + 6 della prima AOCC, 1 commento, 3 del gambo.
        Assert.Equal(16, p.Riga);
        Assert.Contains("12", p.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("15", p.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnEtichettaLontanaDalFixCheHaIlSuoNome()
    {
        var spostata = Da((Parallelo, 11.0), 3, 0);
        ScriviIlConfine(
            "L;ABDAB;N045.00.00.000;E010.00.00.000;8;",
            "L;LUSIL;" + Punto(spostata.Lat, spostata.Lon) + ";8;",
            // Il nome di un gate non è un fix: niente da confrontare.
            "L;X07;N045.30.00.000;E010.30.00.000;8;");

        var p = Assert.Single(Di(Regola.EtichettaLontanaDalFix));

        Assert.Equal(6, p.Riga);
        Assert.Contains("LUSIL", p.Dettaglio, StringComparison.Ordinal);
        Assert.Contains(string.Format(CultureInfo.InvariantCulture, "{0:0.#}", 3.0), p.Dettaglio, StringComparison.Ordinal);
        // La riga giusta: le coordinate del fix, il resto com'è.
        Assert.Equal("L;LUSIL;N045.00.00.000;E011.00.00.000;8;", p.Proposta);
    }
}

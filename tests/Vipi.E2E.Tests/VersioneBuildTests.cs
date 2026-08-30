using System.Reflection;
using System.Text.RegularExpressions;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// «Che versione del sito è online?» — la domanda che il 24 agosto 2026 non aveva risposta, perché
/// <c>AssemblyVersion</c> è <c>1.0.0</c> in ogni pacchetto e la data in <c>avvio-diagnostica.txt</c> dice
/// quando è ripartito, non che cosa.
///
/// <para>Dal 30 agosto 2026 il nome della build è un <b>numero</b> e non più una lettera («e», «f», … «j»),
/// e sta in <c>Directory.Build.props</c> insieme alle tre regole che glielo fanno significare qualcosa.</para>
/// </summary>
public sealed class VersioneBuildTests
{
    private static readonly DateTime Avvio = new(2026, 8, 24, 15, 17, 0, DateTimeKind.Utc);

    [Fact]
    public void Col_timbro_completo_la_barra_dice_versione_e_commit()
    {
        var (etichetta, dettaglio) = VersioneBuild.Componi("1.0.0", "17a6060", "2026-08-24", Avvio);

        Assert.Equal("1.0.0 · 17a6060", etichetta);
        Assert.Equal("Versione 1.0.0 · commit 17a6060 del 2026-08-24 · in servizio dal 2026-08-24 15:17 UTC", dettaglio);
    }

    /// <summary>Build dal repo senza numero di versione: il commit basta a dire quale codice gira.</summary>
    [Fact]
    public void Senza_numero_di_versione_resta_il_commit()
    {
        var (etichetta, dettaglio) = VersioneBuild.Componi(null, "17a6060", "2026-08-24", Avvio);

        Assert.Equal("17a6060", etichetta);
        Assert.StartsWith("Commit 17a6060", dettaglio);
    }

    /// <summary>
    /// Senza timbro — build da uno zip, macchina senza git — si scrive «sviluppo». ⚠️ Non si inventa un
    /// numero: una versione finta è peggio di nessuna versione, perché a una versione si crede.
    /// </summary>
    [Fact]
    public void Senza_timbro_si_dice_sviluppo()
    {
        var (etichetta, dettaglio) = VersioneBuild.Componi(null, null, null, Avvio);

        Assert.Equal("sviluppo", etichetta);
        Assert.Contains("senza timbro", dettaglio);
    }

    /// <summary>L'ora di avvio c'è sempre: con Passenger che riavvia da solo, è metà della risposta.</summary>
    [Theory]
    [InlineData("1.0.0", "17a6060", "2026-08-24")]
    [InlineData(null, null, null)]
    public void L_ora_di_avvio_c_e_sempre(string? versione, string? commit, string? data)
    {
        var (_, dettaglio) = VersioneBuild.Componi(versione, commit, data, Avvio);

        Assert.Contains("in servizio dal 2026-08-24 15:17 UTC", dettaglio);
    }

    /// <summary>Spazi e a capo dell'output di git non devono arrivare in barra.</summary>
    [Fact]
    public void Il_timbro_arriva_da_git_e_va_ripulito()
    {
        var (etichetta, _) = VersioneBuild.Componi(" 1.0.0 \n", "17a6060\r\n", "  ", Avvio);

        Assert.Equal("1.0.0 · 17a6060", etichetta);
    }

    /// <summary>
    /// ⚠️ <b>Il numero da solo non basta, e questo test è il presidio di quella regola.</b> Il commit è
    /// l'unica cosa che identifica il codice senza ambiguità: il numero è il nome che gli diamo noi, e due
    /// build dello stesso numero possono essere due codici diversi. Se qualcuno lo togliesse dall'etichetta
    /// per farla più corta, si tornerebbe esattamente al 24 agosto 2026.
    /// </summary>
    [Fact]
    public void Il_numero_non_sostituisce_il_commit_in_barra()
    {
        var (etichetta, _) = VersioneBuild.Componi("1.0.0", "17a6060", "2026-08-24", Avvio);

        Assert.Contains("17a6060", etichetta);
    }

    /// <summary>
    /// La forma del numero, letta dal <b>binario compilato</b> e non dal file di build: è l'unico modo di
    /// verificare che <c>VipiVersione</c> sia arrivato fino all'assembly, che è dove il sito lo legge.
    ///
    /// <para>Tre numeri separati da punti: niente <c>v</c> davanti, niente suffissi, niente <c>1.0</c>. Non
    /// è pedanteria — un numero che promette una regola deve almeno avere la forma che la regola descrive,
    /// e <c>PATCH</c>/<c>MINOR</c>/<c>MAJOR</c> senza tre posizioni non si distinguono.</para>
    ///
    /// <para>⚠️ Se un giorno la build gira dove <c>Directory.Build.props</c> non si applica, il metadato
    /// manca del tutto: il test lo dice, invece di passare guardando una stringa vuota.</para>
    /// </summary>
    [Fact]
    public void Il_numero_timbrato_nell_assembly_ha_la_forma_giusta()
    {
        var timbro = typeof(VersioneBuild).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "VipiVersione")?.Value;

        Assert.False(string.IsNullOrWhiteSpace(timbro),
            "VipiVersione non è arrivato nell'assembly: il sito direbbe solo il commit. " +
            "Sta in Directory.Build.props, insieme alle tre regole che gli danno un significato.");

        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), timbro!.Trim());
    }
}

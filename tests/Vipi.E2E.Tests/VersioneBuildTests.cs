using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// «Che versione del sito è online?» — la domanda che il 24 agosto 2026 non aveva risposta, perché
/// <c>AssemblyVersion</c> è <c>1.0.0</c> in ogni pacchetto e la data in <c>avvio-diagnostica.txt</c> dice
/// quando è ripartito, non che cosa.
/// </summary>
public sealed class VersioneBuildTests
{
    private static readonly DateTime Avvio = new(2026, 8, 24, 15, 17, 0, DateTimeKind.Utc);

    [Fact]
    public void Col_timbro_completo_la_barra_dice_pacchetto_e_commit()
    {
        var (etichetta, dettaglio) = VersioneBuild.Componi("g", "17a6060", "2026-08-24", Avvio);

        Assert.Equal("g · 17a6060", etichetta);
        Assert.Equal("Pacchetto «g» · commit 17a6060 del 2026-08-24 · in servizio dal 2026-08-24 15:17 UTC", dettaglio);
    }

    /// <summary>Build dal repo senza lettera di pacchetto: il commit basta a dire quale codice gira.</summary>
    [Fact]
    public void Senza_lettera_di_pacchetto_resta_il_commit()
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
    [InlineData("g", "17a6060", "2026-08-24")]
    [InlineData(null, null, null)]
    public void L_ora_di_avvio_c_e_sempre(string? pacchetto, string? commit, string? data)
    {
        var (_, dettaglio) = VersioneBuild.Componi(pacchetto, commit, data, Avvio);

        Assert.Contains("in servizio dal 2026-08-24 15:17 UTC", dettaglio);
    }

    /// <summary>Spazi e a capo dell'output di git non devono arrivare in barra.</summary>
    [Fact]
    public void Il_timbro_arriva_da_git_e_va_ripulito()
    {
        var (etichetta, _) = VersioneBuild.Componi("  g \n", "17a6060\r\n", "  ", Avvio);

        Assert.Equal("g · 17a6060", etichetta);
    }
}

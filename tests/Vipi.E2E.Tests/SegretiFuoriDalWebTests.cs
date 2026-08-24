using Microsoft.Extensions.Configuration;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// I segreti devono poter stare in un file che <b>non ha un nome indovinabile</b>, perché sul server vero la
/// cartella dell'applicazione è il document root e <c>/appsettings.Production.json</c> risponde 200
/// (misurato il 24 agosto 2026, <c>docs/lavori-aperti.md</c> §A13).
/// </summary>
public sealed class SegretiFuoriDalWebTests
{
    [Fact]
    public void Un_file_nella_cartella_vince_su_appsettings()
    {
        using var _ = new CartellaSegretiFinta("nome-che-nessuno-indovina-9f3a.json", """
            { "ConnectionStrings": { "Vipi": "Server=localhost;Password=quella-vera" } }
            """);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Quello che resterebbe nel file scaricabile: nessun segreto.
                ["ConnectionStrings:Vipi"] = $"Server=localhost;Password={SegretiFuoriDalWeb.Segnaposto}",
            });

        var letti = SegretiFuoriDalWeb.Carica(cfg);

        Assert.Equal(1, letti);
        Assert.Equal("Server=localhost;Password=quella-vera", cfg.Build().GetConnectionString("Vipi"));
    }

    [Fact]
    public void Senza_la_cartella_non_succede_niente()
    {
        var cfg = new ConfigurationBuilder();
        Assert.Equal(0, SegretiFuoriDalWeb.Carica(cfg));   // sviluppo e test: è il caso normale
    }

    /// <summary>
    /// Partire senza password significherebbe ripiegare su uno SQLite vuoto: il sito torna su con l'aria di
    /// aver perso tutti i dati. Meglio non partire — e dirlo in <c>diagnostica/avvio-errore.txt</c>.
    /// </summary>
    [Theory]
    [InlineData("MySql", null, true)]
    [InlineData("MySql", "", true)]
    [InlineData("MySql", "Server=localhost;Password=METTI-QUI-LA-PASSWORD", true)]
    [InlineData("MySql", "Server=localhost;Database=itivao_atc;Password=vera", false)]
    // Una password che non si riconosce NON ferma l'avvio: fra socket unix, plugin e `pwd`, una guardia
    // troppo sveglia qui non protegge un dato — spegne il sito.
    [InlineData("MySql", "Server=/var/run/mysqld/mysqld.sock;User Id=itivao_atc", false)]
    // Provider diverso: la guardia non è affare suo.
    [InlineData("Sqlite", "Data Source=vipi.db", false)]
    [InlineData(null, null, false)]
    public void La_guardia_ferma_solo_i_casi_inequivocabili(string? provider, string? connectionString, bool deveFermare)
    {
        var errore = SegretiFuoriDalWeb.ValidaConnessione(provider, connectionString);

        Assert.Equal(deveFermare, errore is not null);
        if (deveFermare) Assert.Contains(SegretiFuoriDalWeb.Cartella, errore);
    }

    /// <summary>Crea e rimuove la cartella accanto all'assembly dei test, che è ciò che <c>Carica</c> guarda.</summary>
    private sealed class CartellaSegretiFinta : IDisposable
    {
        private readonly string _cartella = Path.Combine(AppContext.BaseDirectory, SegretiFuoriDalWeb.Cartella);

        public CartellaSegretiFinta(string nomeFile, string contenuto)
        {
            Directory.CreateDirectory(_cartella);
            File.WriteAllText(Path.Combine(_cartella, nomeFile), contenuto);
        }

        public void Dispose()
        {
            try { Directory.Delete(_cartella, recursive: true); } catch { /* best-effort */ }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Translation;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La prova d'uso <b>reale</b>: il giro di riempimento su una COPIA del <c>vipi.db</c> vero, col motore
/// vero, dall'inizio alla fine.
///
/// <para>
/// ⚠️ <b>Si salta a meno che non lo si chieda.</b> Tocca la rete e consuma quota di un servizio a pagamento:
/// in CI e nella suite di tutti i giorni non deve girare mai. Lo si accende con le variabili d'ambiente,
/// che sono anche il modo di NON scrivere una chiave in un sorgente:
/// </para>
/// <code>
/// $env:VIPI_TRADUZIONE_LIVE="1"
/// $env:VIPI_AZURE_KEY="..."      # da: dotnet user-secrets list --project src/Vipi.Host
/// $env:VIPI_AZURE_REGION="italynorth"
/// dotnet test tests/Vipi.Infrastructure.Tests -f net10.0 --filter "FullyQualifiedName~DalVivo"
/// </code>
///
/// <para>
/// ⚠️ <b>Su una COPIA, mai sull'originale.</b> Il <c>vipi.db</c> di sviluppo è l'unica istanza con dentro i
/// documenti veri: un giro che scrive va fatto su una copia, o la prima prova andata storta se la porta via.
/// </para>
/// </summary>
public class TraduzioneDalVivoTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _out;
    private string _copia = "";

    public TraduzioneDalVivoTests(ITestOutputHelper output) => _out = output;

    private static bool Acceso => Environment.GetEnvironmentVariable("VIPI_TRADUZIONE_LIVE") == "1";
    private static string? Chiave => Environment.GetEnvironmentVariable("VIPI_AZURE_KEY");
    private static string Regione => Environment.GetEnvironmentVariable("VIPI_AZURE_REGION") ?? "";

    /// <summary>Il database di sviluppo, se c'è. Si risale dalla cartella dell'assembly fino alla radice.</summary>
    private static string? DatabaseVero()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "src", "Vipi.Host", "vipi.db");
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }
        return null;
    }

    public Task InitializeAsync()
    {
        if (!Acceso) return Task.CompletedTask;
        var vero = DatabaseVero();
        if (vero is null) return Task.CompletedTask;

        _copia = Path.Combine(Path.GetTempPath(), $"vipi-traduzione-{Guid.NewGuid():N}.db");
        File.Copy(vero, _copia, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // ⚠️ SQLite tiene le connessioni in un POOL: senza svuotarlo il file resta aperto e la copia non si
        // cancella («being used by another process»), lasciando spazzatura nella cartella temporanea a ogni
        // giro. Non basta chiudere il DbContext.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (_copia.Length > 0 && File.Exists(_copia)) File.Delete(_copia);
        return Task.CompletedTask;
    }

    private sealed class UnaFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { Timeout = TimeSpan.FromSeconds(60) };
    }

    [Fact]
    public async Task Il_giro_riempie_la_memoria_dal_corpus_vero()
    {
        if (!Acceso || string.IsNullOrWhiteSpace(Chiave))
        {
            _out.WriteLine("SALTATO: manca VIPI_TRADUZIONE_LIVE=1 o VIPI_AZURE_KEY.");
            return;
        }
        Assert.True(File.Exists(_copia), "copia del vipi.db non creata: il database di sviluppo non e' stato trovato");

        await using var db = new VipiDbContext(
            new DbContextOptionsBuilder<VipiDbContext>().UseSqlite($"Data Source={_copia}").Options);

        // ⚠️ Il vipi.db di sviluppo e' fermo a prima di questa funzione: la tabella della memoria non c'e'
        // ancora. La copia si porta al passo PRIMA di usarla — ed e' anche il promemoria che il database
        // vero, quando lo si aggiornera', vuole lo stesso trattamento.
        await db.Database.MigrateAsync();

        var opzioni = Options.Create(new TranslationOptions
        {
            Enabled = true,
            Order = new[] { "azure" },
            Azure = new AzureOptions { ApiKey = Chiave, Region = Regione },
        });

        var motore = new AzureTranslationEngine(new UnaFactory(), opzioni);
        var memoria = new EfTranslationMemory(db);
        var corpus = new EfTranslatableCorpus(db);

        // I nomi dello staff vero: e' il roster che il protettore deve proteggere.
        var roster = await db.StaffMembers.AsNoTracking()
            .Where(s => s.DisplayName != null).Select(s => s.DisplayName!).ToListAsync();

        var giro = new TranslationFillUseCase(corpus, memoria, new[] { motore },
                                              new TextProtector(roster), opzioni.Value);

        // La vLOA nasce in inglese: per lei l'italiano e' il bersaglio. E' il verso piu' rappresentato nel
        // corpus di sviluppo, quindi e' quello che prova qualcosa.
        var rapporto = await giro.EseguiAsync("en", "it");

        _out.WriteLine($"segmenti          : {rapporto.Segmenti}");
        _out.WriteLine($"gia' in memoria   : {rapporto.GiaInMemoria}");
        _out.WriteLine($"tradotti ora      : {rapporto.Tradotti}");
        _out.WriteLine($"da tradurre a mano: {rapporto.DaTradurreAMano}");
        _out.WriteLine($"scartati          : {rapporto.Scartati}");
        _out.WriteLine($"esito             : {rapporto.Esito} ({rapporto.Dettaglio})");
        _out.WriteLine($"motore            : {rapporto.Motore}");

        Assert.Equal(TranslationOutcome.Ok, rapporto.Esito);
        Assert.True(rapporto.Segmenti > 0, "il corpus non ha prodotto segmenti");
        // ⚠️ Qualche scarto E' NORMALE e non e' un guasto: e' il controllo sul contenuto dei tag che ferma
        // le traduzioni in cui il motore ha CAMBIATO un identificatore. Misurato sul corpus vero: Azure ha
        // invertito «RWY 07/25» in «RWY 25/07», e accettarlo avrebbe scritto una pista sbagliata in un
        // documento operativo. Quel che non deve succedere e' che ne scarti la maggioranza.
        Assert.True(rapporto.Scartati * 2 < rapporto.Segmenti,
                    $"troppi scarti: {rapporto.Scartati} su {rapporto.Segmenti}");

        // Un secondo giro non deve spendere NIENTE: e' il dedup, ed e' il cuore del costo.
        var secondo = await giro.EseguiAsync("en", "it");
        _out.WriteLine($"\nsecondo giro -> tradotti {secondo.Tradotti}, gia' in memoria {secondo.GiaInMemoria}");
        Assert.Equal(0, secondo.Tradotti);
        Assert.Equal(rapporto.Tradotti, secondo.GiaInMemoria);

        // ⚠️ Cio' che e' stato scartato NON entra in memoria, quindi il giro dopo ci riprova — e con un
        // segmento su cui il motore sbaglia sempre allo stesso modo, ci riprovera' per sempre, spendendo
        // ogni volta. Su un segmento e' rumore; se un giorno fossero cento, varrebbe la pena ricordarsi
        // quali falliscono invece di ritentarli alla cieca. Scritto qui perche' non si scopra dal conto.
        Assert.Equal(rapporto.Scartati, secondo.Scartati);

        // Un assaggio di quel che e' uscito, per leggerlo con gli occhi.
        var esempi = await db.TranslationUnits.AsNoTracking().OrderBy(u => u.Id).Take(8).ToListAsync();
        _out.WriteLine("\n---- assaggio ----");
        foreach (var u in esempi)
            _out.WriteLine($"  EN: {u.SourceText}\n  IT: {u.TargetText}\n");
    }
}

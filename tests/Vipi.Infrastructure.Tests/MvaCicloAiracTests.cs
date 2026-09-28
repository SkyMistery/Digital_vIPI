using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-037 (revisione totale 3): le carte MRVA si leggevano dal sectorfile al momento, e una carta rivista per il
/// ciclo prossimo — il sectorfile lo scriviamo in anticipo — entrava subito anche nelle release del ciclo in corso.
/// Ora il testo si ricorda: una carta cambiata entra dal ciclo SUCCESSIVO, e fino ad allora una release congela
/// quella in vigore. Fuori dalla cattura si vede la corrente, come per le aree di settore.
/// </summary>
public class MvaCicloAiracTests : IAsyncLifetime
{
    private const string CartaA = "L;110;N044.13.15.000;E010.53.34.000;110;7;";
    private const string CartaB = "L;120;N044.13.15.000;E010.53.34.000;120;7;";
    private const string Percorso = "ENRMVA/lirr.mva";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private readonly Orologio _orologio = new();
    private readonly ShapeReleaseContext _cattura = new();

    /// <summary>Il 28 settembre 2026: ciclo corrente 2609, successivo 2610 (dal 1° ottobre).</summary>
    private sealed class Orologio : TimeProvider
    {
        public DateTimeOffset Adesso { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Adesso;
    }

    /// <summary>Il sectorfile: risponde col testo di adesso, che il test cambia.</summary>
    private sealed class Sectorfile : HttpMessageHandler
    {
        public string Testo { get; set; } = CartaA;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Testo) });
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EfMvaChartStates Stati() => new(_db, new AiracService(), _orologio);

    /// <summary>Un provider per giro: la cache è quella di un processo nuovo, come dopo il giro che la svuota.</summary>
    private AuroraMvaProvider Provider(Sectorfile sf) =>
        new(new HttpClient(sf, disposeHandler: false),
            Options.Create(new SectorfileOptions { RawBaseUrl = "https://example.test/" }),
            new SectorfileCache(), NullLogger<AuroraMvaProvider>.Instance, Stati(), _cattura);

    private static string Etichetta(Vipi.Application.Abstractions.MvaChart c) => c.Labels.Single().Text;

    private async Task<string> AlCiclo(AuroraMvaProvider p, string? ciclo)
    {
        if (ciclo is null) return Etichetta(await p.GetAccChartAsync("LIRR"));
        using (_cattura.Capturing(ciclo))
            return Etichetta(await p.GetAccChartAsync("LIRR"));
    }

    [Fact]
    public async Task La_prima_carta_vale_subito()
    {
        var sf = new Sectorfile();

        Assert.Equal("110", await AlCiclo(Provider(sf), "2609"));
        Assert.Null((await _db.MvaChartStates.AsNoTracking().SingleAsync()).AiracCycle);
    }

    [Fact]
    public async Task Una_carta_rivista_entra_dal_ciclo_successivo()
    {
        var sf = new Sectorfile();
        await AlCiclo(Provider(sf), null);            // il primo giro la vede com'è

        sf.Testo = CartaB;                           // la divisione la rivede per il ciclo prossimo
        var p = Provider(sf);

        Assert.Equal("110", await AlCiclo(p, "2609")); // la release di adesso congela quella in vigore
        Assert.Equal("120", await AlCiclo(p, "2610")); // quella del prossimo la nuova
        Assert.Equal("120", await AlCiclo(p, null));   // fuori dalla cattura si vede la corrente
        Assert.Equal("2610", (await _db.MvaChartStates.AsNoTracking().SingleAsync()).AiracCycle);
    }

    [Fact]
    public async Task Arrivato_il_ciclo_il_differimento_si_chiude()
    {
        var sf = new Sectorfile();
        await AlCiclo(Provider(sf), null);
        sf.Testo = CartaB;
        await AlCiclo(Provider(sf), null);

        _orologio.Adesso = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("120", await AlCiclo(Provider(sf), "2610"));

        var stato = await _db.MvaChartStates.AsNoTracking().SingleAsync();
        Assert.Null(stato.AiracCycle);
        Assert.Null(stato.TextInForce);
    }

    [Fact]
    public async Task Due_revisioni_prima_del_ciclo_tengono_in_vigore_la_prima()
    {
        await Stati().RiconciliaAsync(Percorso, CartaA);
        await Stati().RiconciliaAsync(Percorso, CartaB);
        await Stati().RiconciliaAsync(Percorso, "L;130;N044.13.15.000;E010.53.34.000;130;7;");

        Assert.Equal(CartaA, await Stati().TestoInVigoreAsync(Percorso, "2609"));
    }
}

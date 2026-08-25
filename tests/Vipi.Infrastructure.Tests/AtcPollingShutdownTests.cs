using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Lo spegnimento del poller ATC deve **scrivere**, non arrendersi.
///
/// <para>Il salvataggio finale esiste per una ragione sola: fra un checkpoint e l'arresto restano in memoria
/// fino a dieci minuti di traffico per ogni sessione in corso, e a ogni deploy andrebbero persi. ⚠️ Passargli
/// il gettone di arresto lo rendeva inutile proprio nel caso che conta — un arresto brusco, un secondo
/// Ctrl+C — perché la scrittura moriva sull'apertura della connessione con una `TaskCanceledException`.
/// E il danno non era «non scritto»: `FlushAsync` chiama `TakeAll`, che **svuota** il registro prima di
/// salvare, quindi quei minuti sparivano anche dalla RAM.</para>
/// </summary>
public class AtcPollingShutdownTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Un solo settore, un quadratone attorno all'Italia centrale, senza tetto.</summary>
    private sealed class CatalogoFinto : ISectorVolumeCatalog
    {
        public Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorVolumeRow>>(new[]
            {
                new SectorVolumeRow("LIRR_NE1_CTR", null, SectorType.Ctr, null,
                    "[[10,40],[14,40],[14,44],[10,44]]", 0, null),
            });
    }

    /// <summary>Archivio che non scrive niente ma ricorda <b>com'era il gettone</b> quando l'hanno chiamato.</summary>
    private sealed class ArchivioSpia : IAtcTrafficStore
    {
        public int Salvataggi { get; private set; }
        public bool? GettoneAnnullato { get; private set; }

        /// <summary>Acceso DOPO il giro di preparazione: `RecordAsync` salva già di suo al primo giro.</summary>
        public bool Esplode { get; set; }

        public Task<int> SaveAsync(TrafficFlush flush, CancellationToken ct = default)
        {
            Salvataggi++;
            GettoneAnnullato = ct.IsCancellationRequested;
            if (Esplode) throw new InvalidOperationException("database irraggiungibile");
            ct.ThrowIfCancellationRequested();          // com'è morta la vera: sull'apertura della connessione
            return Task.FromResult(1);
        }

        public Task<IReadOnlyDictionary<long, (IReadOnlyList<TrafficLegRow> Legs, int TrafficMinutes)>> GetLegsAsync(
            IReadOnlyCollection<long> sessionIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, (IReadOnlyList<TrafficLegRow>, int)>>(
                new Dictionary<long, (IReadOnlyList<TrafficLegRow>, int)>());

        public Task<(IReadOnlyList<AirportSessionWindow> ToFill, IReadOnlyList<AirportSessionWindow> Concurrent)>
            GetAirportSessionsToFillAsync(DateTimeOffset notBefore, int max, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<AirportSessionWindow>, IReadOnlyList<AirportSessionWindow>)>(
                (Array.Empty<AirportSessionWindow>(), Array.Empty<AirportSessionWindow>()));

        public Task<int> FillAirportMovementsAsync(long sessionId, IReadOnlyList<SourceAirportMovement> movements,
            DateTimeOffset filledAtUtc, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class AmbienteFinto : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>Un giro di attribuzione, così che nel registro ci sia davvero qualcosa da salvare.</summary>
    private static async Task<AtcTrafficRecorder> RecorderConTrafficoInMemoria(IAtcTrafficStore archivio)
    {
        var recorder = new AtcTrafficRecorder(new CatalogoFinto());
        var snapshot = new NetworkSnapshot
        {
            AsOf = T0,
            Atc = new[] { new SourceAtcConnection(100, 704798, "LIRR_NE1_CTR", "CTR", "118.700", 4, T0, 600) },
            Pilots = new[]
            {
                new SourcePilotFix(63001, 785031, "AZA123", 42.0, 12.0, 35_000, 420, false, "En Route",
                    50, 900, "LIRF", "LIRN", "B38M"),
            },
        };

        var esito = await recorder.RecordAsync(snapshot, archivio);
        Assert.Equal(1, esito.Attributed);      // se non attribuisce, il resto del test non prova niente
        return recorder;
    }

    private static AtcPollingHostedService Poller(AtcTrafficRecorder recorder, IAtcTrafficStore archivio)
    {
        var servizi = new ServiceCollection();
        servizi.AddSingleton(archivio);
        var provider = servizi.BuildServiceProvider();

        return new AtcPollingHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            recorder,
            new OnlineAtcCache(),
            Options.Create(new IvaoOptions()),
            new AmbienteFinto(),
            NullLogger<AtcPollingHostedService>.Instance);
    }

    /// <summary>
    /// ⚠️ Il caso che il difetto rendeva inutile: il gettone di arresto arriva <b>già annullato</b>. Il
    /// salvataggio finale deve partire lo stesso, e con un gettone suo.
    /// </summary>
    [Fact]
    public async Task Lo_spegnimento_salva_anche_col_gettone_di_arresto_gia_annullato()
    {
        var archivio = new ArchivioSpia();
        var recorder = await RecorderConTrafficoInMemoria(archivio);
        var salvataggiPrima = archivio.Salvataggi;

        var poller = Poller(recorder, archivio);
        await poller.StopAsync(new CancellationToken(canceled: true));

        Assert.Equal(salvataggiPrima + 1, archivio.Salvataggi);
        Assert.False(archivio.GettoneAnnullato, "il salvataggio finale ha ricevuto il gettone di «fermati»");
    }

    /// <summary>Un guasto vero nel salvataggio non deve impedire allo spegnimento di andare avanti.</summary>
    [Fact]
    public async Task Un_salvataggio_finale_che_esplode_non_blocca_lo_spegnimento()
    {
        var archivio = new ArchivioSpia();
        var recorder = await RecorderConTrafficoInMemoria(archivio);
        archivio.Esplode = true;

        var poller = Poller(recorder, archivio);
        await poller.StopAsync(CancellationToken.None);   // niente eccezione fuori: è tutto quel che serve
    }
}

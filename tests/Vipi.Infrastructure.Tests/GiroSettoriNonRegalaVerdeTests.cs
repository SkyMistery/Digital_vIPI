using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro notturno dei settori d'aeroporto timbra «riuscito» solo quando ha davvero riletto la sorgente.
///
/// <para>Il timbro non è cosmetico: <c>DeletionService</c> legge il penultimo giro riuscito per decidere la D8
/// («la sorgente la manda ancora»). Un settore con <c>ImportedAtUtc</c> più vecchio del penultimo giro è dato per
/// sparito, e da lì si può eliminare con legami e blocchi di documento. Un giro verde che non ha letto niente
/// fa quindi cadere la D8 su tutti i settori d'aeroporto dopo due notti.</para>
///
/// <para>🔴 <b>U-024</b> (revisione totale 3): con «Settori» esclusa in Sorgenti l'importatore non fa nulla per
/// scelta, ma il giro ritornava <c>true</c> e <c>GatedImportLoop</c> timbrava. 🔴 <b>U-002</b>, la metà del giro:
/// un aeroporto che non si legge fermava tutti quelli dopo di lui (il ciclo non aveva un catch per scalo).</para>
/// </summary>
public class GiroSettoriNonRegalaVerdeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var s = new EfStructureEditingRepository(_db);
        await s.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await s.CreateAirportAsync("LIRR", "LIRA", "Roma Ciampino");
        await s.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Con_i_settori_esclusi_il_giro_non_timbra_e_non_chiama_la_sorgente()
    {
        await new EfImportPolicyStore(_db).SaveAsync(new ImportPolicySnapshot(true, true, Sectors: false), 1);
        var importer = new Importatore();
        var proiezione = new Proiezione();

        var timbra = await Giro().ImportOnceAsync(Servizi(importer, proiezione), CancellationToken.None);

        Assert.False(timbra);
        Assert.Empty(importer.Chiesti);
    }

    [Fact]
    public async Task Uno_scalo_che_non_si_legge_non_ferma_gli_altri_ma_il_giro_non_e_riuscito()
    {
        var importer = new Importatore { Rotto = "LIRA" };
        var proiezione = new Proiezione();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            Giro().ImportOnceAsync(Servizi(importer, proiezione), CancellationToken.None));

        // LIRA viene prima in ordine alfabetico: LIRF si legge lo stesso, e la proiezione si rifà.
        Assert.Equal(new[] { "LIRA", "LIRF" }, importer.Chiesti);
        Assert.Equal(1, proiezione.Volte);
        // Il messaggio dice QUALE scalo: è quello che finisce in Sorgenti come ultimo errore.
        Assert.Contains("LIRA", ex.Message);
    }

    [Fact]
    public async Task Tutti_letti_il_giro_timbra()
    {
        var importer = new Importatore();

        Assert.True(await Giro().ImportOnceAsync(Servizi(importer, new Proiezione()), CancellationToken.None));
        Assert.Equal(new[] { "LIRA", "LIRF" }, importer.Chiesti);
    }

    [Fact]
    public async Task Senza_credenziali_non_timbra_come_prima()
    {
        var importer = new Importatore { Rotto = "*", Errore = new SorgenteNonConfigurataException("Credenziali IVAO non configurate") };

        Assert.False(await Giro().ImportOnceAsync(Servizi(importer, new Proiezione()), CancellationToken.None));
    }

    private static AirportSectorImportHostedService Giro() =>
        new(null!, Options.Create(new IvaoOptions()), NullLogger<AirportSectorImportHostedService>.Instance);

    private IServiceProvider Servizi(IAirportSectorImporter importer, ISectorProjectionService proiezione) =>
        new ServiceCollection()
            .AddSingleton<IAirportSectorRepository>(new EfAirportSectorRepository(_db))
            .AddSingleton<IImportPolicyStore>(new EfImportPolicyStore(_db))
            .AddSingleton(importer)
            .AddSingleton(proiezione)
            .BuildServiceProvider();

    private sealed class Importatore : IAirportSectorImporter
    {
        public List<string> Chiesti { get; } = new();
        public string? Rotto { get; init; }
        public Exception? Errore { get; init; }

        public Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default)
        {
            Chiesti.Add(icao);
            if (Rotto == "*" || Rotto == icao)
                throw Errore ?? new HttpRequestException($"IVAO 403 su /v2/airports/{icao}/ATCPositions", null,
                    System.Net.HttpStatusCode.Forbidden);
            return Task.FromResult((1, 0));
        }
    }

    private sealed class Proiezione : ISectorProjectionService
    {
        public int Volte;
        public Task<int> SyncFromCatalogsAsync(CancellationToken ct = default) { Volte++; return Task.FromResult(0); }
    }
}

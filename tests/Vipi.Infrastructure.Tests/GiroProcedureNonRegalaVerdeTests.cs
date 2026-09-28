using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro notturno delle procedure timbra «riuscito» solo quando ha davvero letto la sorgente.
///
/// <para>🔴 <b>U-038</b> (revisione totale 3): prendeva ogni eccezione per scalo e tornava sempre <c>true</c>.
/// Con GitHub irraggiungibile all'ora del giro — 100 scali falliti su 100 — Sorgenti restava verde, il giro dopo
/// era fra 24 ore invece che fra un'ora, e il gradino 3 di <c>SidStampCycle</c> si ancorava a un giro che non
/// aveva letto niente. Il gemello dei settori è <see cref="GiroSettoriNonRegalaVerdeTests"/>.</para>
///
/// <para>La soglia è diversa da quella dei settori, e di proposito: un file <c>.sid</c> rotto è di UNO scalo e
/// può restarlo per settimane; se bastasse lui a non timbrare, il giro ritenterebbe ogni ora per sempre e il
/// timbro non avanzerebbe più per nessuno. Non timbra quando fallisce almeno metà degli scali, o quando non
/// arriva nemmeno una procedura.</para>
/// </summary>
public class GiroProcedureNonRegalaVerdeTests : IAsyncLifetime
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
        await s.CreateAirportAsync("LIRR", "LIRN", "Napoli");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Tutti_gli_scali_falliti_il_giro_non_timbra_e_dice_perche()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            Giro().RunOnceAsync(Servizi(new Importatore { Rotti = { "LIRA", "LIRF", "LIRN" } }), CancellationToken.None));

        Assert.Contains("3", ex.Message);
        Assert.Contains("LIRA", ex.Message);
    }

    [Fact]
    public async Task La_sorgente_che_non_da_niente_non_e_un_giro_riuscito()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Giro().RunOnceAsync(Servizi(new Importatore { Righe = 0 }), CancellationToken.None));
    }

    [Fact]
    public async Task Con_le_SID_escluse_il_giro_non_timbra_e_non_chiama_la_sorgente()
    {
        await new EfImportPolicyStore(_db).SaveAsync(new ImportPolicySnapshot(true, true, true, Sids: false), 1);
        var importer = new Importatore();

        Assert.False(await Giro().RunOnceAsync(Servizi(importer), CancellationToken.None));
        Assert.Empty(importer.Chiesti);
    }

    [Fact]
    public async Task Uno_scalo_rotto_su_tre_non_ferma_il_timbro()
    {
        var importer = new Importatore { Rotti = { "LIRA" } };

        Assert.True(await Giro().RunOnceAsync(Servizi(importer), CancellationToken.None));
        Assert.Equal(new[] { "LIRA", "LIRF", "LIRN" }, importer.Chiesti);
    }

    private static ProcedureImportHostedService Giro() =>
        new(null!, Options.Create(new SectorfileOptions()), NullLogger<ProcedureImportHostedService>.Instance);

    private IServiceProvider Servizi(IProcedureImporter importer) =>
        new ServiceCollection()
            .AddSingleton<IAirportSectorRepository>(new EfAirportSectorRepository(_db))
            .AddSingleton<IImportPolicyStore>(new EfImportPolicyStore(_db))
            .AddSingleton(importer)
            .AddSingleton(new SectorfileCache())
            .BuildServiceProvider();

    private sealed class Importatore : IProcedureImporter
    {
        public List<string> Chiesti { get; } = new();
        public HashSet<string> Rotti { get; } = new();
        public int Righe { get; init; } = 4;

        public Task<int> ImportAsync(string icao, CancellationToken ct = default)
        {
            Chiesti.Add(icao);
            if (Rotti.Contains(icao))
                throw new HttpRequestException($"GitHub raw irraggiungibile per {icao.ToLowerInvariant()}.sid");
            return Task.FromResult(Righe);
        }

        public Task<int> ImportForCurrentUserAsync(string icao, CancellationToken ct = default) => ImportAsync(icao, ct);
    }
}

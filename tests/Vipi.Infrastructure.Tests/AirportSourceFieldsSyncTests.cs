using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il riallineamento dei campi <b>anagrafici</b> degli aeroporti già in archivio
/// (<see cref="EfStructureEditingRepository.SyncAirportSourceFieldsAsync"/>), dal 25 agosto 2026.
///
/// <para>Esiste per una trappola misurata: l'assegnazione è additiva — salta gli ICAO già presenti — quindi un
/// campo nuovo nascerebbe al suo default sui 93 aeroporti in archivio e nessun giro lo riempirebbe mai. Questi
/// test presidiano le tre cose che il riallineamento non deve sbagliare: <b>scrivere ciò che dice la
/// sorgente</b> sugli aeroporti esistenti, <b>non toccare</b> ciò che ha deciso una persona, e <b>tenere
/// coerente</b> «solo militare» con la presenza militare che lo rende possibile.</para>
/// </summary>
public class AirportSourceFieldsSyncTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfStructureEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfStructureEditingRepository(_db);
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIPA", "Aviano");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Task<Vipi.Domain.Entities.Airport> LoadAsync(string icao) =>
        _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == icao);

    [Fact]
    public async Task Un_aeroporto_gia_in_archivio_riceve_i_campi_della_sorgente()
    {
        var changed = await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", "Aviano", 7000,
                HasMilitaryPresence: true, Iata: "AVB", ElevationFt: 413, MagneticVariation: 2.5),
        });

        Assert.Equal(1, changed);
        var apt = await LoadAsync("LIPA");
        Assert.True(apt.HasMilitaryPresence);
        Assert.Equal("AVB", apt.Iata);
        Assert.Equal(413, apt.ElevationFt);
        Assert.Equal(2.5, apt.MagneticVariation);
    }

    [Fact]
    public async Task Un_secondo_giro_senza_novita_non_conta_niente_come_cambiato()
    {
        var source = new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", "Aviano", 7000,
                HasMilitaryPresence: true, Iata: "AVB", ElevationFt: 413, MagneticVariation: 2),
        };
        await _repo.SyncAirportSourceFieldsAsync(source);

        Assert.Equal(0, await _repo.SyncAirportSourceFieldsAsync(source));
    }

    [Fact]
    public async Task Il_giro_non_tocca_ne_il_nome_ne_la_scelta_dell_amministratore()
    {
        await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", null, null, HasMilitaryPresence: true),
        });
        // «Solo militare» lo decide una persona, non la sorgente.
        (await _db.Airports.SingleAsync(a => a.Icao == "LIPA")).IsMilitaryOnly = true;
        await _db.SaveChangesAsync();

        await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            // La sorgente arriva con un nome diverso: non deve vincere sull'archivio.
            new SourceAirport("LIPA", "AVIANO AB", "LIPP", null, null, HasMilitaryPresence: true),
        });

        var apt = await LoadAsync("LIPA");
        Assert.True(apt.IsMilitaryOnly);
        Assert.Equal("Aviano", apt.Name);
    }

    [Fact]
    public async Task Tolta_la_presenza_militare_cade_anche_il_solo_militare()
    {
        await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", null, null, HasMilitaryPresence: true),
        });
        (await _db.Airports.SingleAsync(a => a.Icao == "LIPA")).IsMilitaryOnly = true;
        await _db.SaveChangesAsync();

        await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", null, null, HasMilitaryPresence: false),
        });

        var apt = await LoadAsync("LIPA");
        Assert.False(apt.HasMilitaryPresence);
        Assert.False(apt.IsMilitaryOnly);
    }

    [Fact]
    public async Task Un_aeroporto_che_la_sorgente_non_nomina_resta_comera()
    {
        (await _db.Airports.SingleAsync(a => a.Icao == "LIRF")).Iata = "FCO";
        await _db.SaveChangesAsync();

        // La sorgente parla solo di LIPA: LIRF è fuori dal paese configurato o semplicemente assente da questa
        // pagina, e «non lo so» non deve diventare «non ce l'ha».
        await _repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIPA", "Aviano", "LIPP", null, null, HasMilitaryPresence: true),
        });

        Assert.Equal("FCO", (await LoadAsync("LIRF")).Iata);
    }
}

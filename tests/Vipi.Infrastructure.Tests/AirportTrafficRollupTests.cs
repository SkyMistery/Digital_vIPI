using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il consolidamento del traffico d'aeroporto contro un database vero: che cosa chiede alla sorgente, che
/// cosa scrive, e che cosa legge poi la pagina.
///
/// <para>⚠️ Il caso che vale più di tutti è l'ultimo: un aeroporto <b>senza nessun controllore</b> deve
/// comparire con il suo traffico e zero coperti. È il motivo per cui questa tabella esiste — se sparisse
/// dall'elenco, la pagina direbbe che copriamo tutto quello che si vede.</para>
/// </summary>
public class AirportTrafficRollupTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    /// <summary>Un lunedì a mezzanotte: la finestra dei giorni si legge senza fare conti a mente.</summary>
    private static readonly DateTimeOffset G0 = new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc });
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = acc });
        _db.Airports.Add(new Airport { Icao = "LIRZ", Name = "Perugia", Acc = acc, IsHidden = true });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task Sessione(long id, string callsign, double daOre, double aOre)
    {
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = 704798, Callsign = callsign,
            StartUtc = G0.AddHours(daOre).UtcDateTime, EndUtc = G0.AddHours(aOre).UtcDateTime,
            DurationSeconds = (int)((aOre - daOre) * 3600), Source = AtcSessionSource.Backfill, ShiftKey = id,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Sorgente finta: risponde per aeroporto, e annota che cosa le è stato chiesto.</summary>
    private sealed class SorgenteFinta : IAirportTrafficSource
    {
        private readonly Dictionary<string, SourceAirportMovement[]> _per;
        public List<(string Icao, DateTimeOffset From, DateTimeOffset To)> Chieste { get; } = new();

        public SorgenteFinta(Dictionary<string, SourceAirportMovement[]> per) => _per = per;

        public Task<IReadOnlyList<SourceAirportMovement>> GetMovementsAsync(
            string icao, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        {
            Chieste.Add((icao, from, to));
            return Task.FromResult<IReadOnlyList<SourceAirportMovement>>(
                _per.TryGetValue(icao, out var m) ? m : Array.Empty<SourceAirportMovement>());
        }
    }

    private static SourceAirportMovement Arrivo(string icao, double ora) =>
        new(AirportMovementKind.Inbound, $"IN{ora}", 1, (long)(ora * 100), "LIML", icao, "A320",
            G0.AddHours(ora - 1), G0.AddHours(ora));

    private static SourceAirportMovement Partenza(string icao, double ora) =>
        new(AirportMovementKind.Outbound, $"OUT{ora}", 1, (long)(ora * 100) + 1, icao, "LIML", "A320",
            G0.AddHours(ora), G0.AddHours(ora + 2));

    private (AirportTrafficRollupUseCase Uc, SorgenteFinta Sorgente) Caso(
        Dictionary<string, SourceAirportMovement[]> movimenti)
    {
        var sorgente = new SorgenteFinta(movimenti);
        return (new AirportTrafficRollupUseCase(
            sorgente, new EfAirportTrafficRollupStore(_db), new EfImportPolicyStore(_db)), sorgente);
    }

    [Fact]
    public async Task Consolida_il_traffico_e_quanto_ha_trovato_acceso()
    {
        // Torre aperta dalle 10 alle 12 del primo giorno.
        await Sessione(1, "LIRF_TWR", 10, 12);

        var (uc, sorgente) = Caso(new()
        {
            ["LIRF"] = new[] { Arrivo("LIRF", 11), Arrivo("LIRF", 15), Partenza("LIRF", 10.5) },
        });

        var esito = await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        Assert.Equal(2, sorgente.Chieste.Count);                 // un blocco per aeroporto visibile
        Assert.DoesNotContain(sorgente.Chieste, c => c.Icao == "LIRZ");   // quello nascosto no

        var riga = await _db.AirportDayTraffic.SingleAsync(r => r.Icao == "LIRF");
        Assert.Equal(2, riga.Inbound);
        Assert.Equal(1, riga.Outbound);
        Assert.Equal(2, riga.CoveredMovements);                  // l'arrivo delle 15 era fuori orario
        Assert.Equal(120, riga.AtcMinutes);
        Assert.Equal(3, esito.Movements);
    }

    /// <summary>
    /// ⚠️ Il campo lo dice il callsign, e un CTR non ne dichiara nessuno: <c>LIRR_NE1_CTR</c> comincia per
    /// un codice di FIR, e prenderlo per un aeroporto darebbe a Fiumicino ore di apertura mai esistite.
    /// </summary>
    [Fact]
    public async Task Un_settore_d_area_non_apre_nessun_aeroporto()
    {
        await Sessione(1, "LIRR_NE1_CTR", 8, 20);

        var (uc, _) = Caso(new() { ["LIRF"] = new[] { Arrivo("LIRF", 11) } });
        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        var riga = await _db.AirportDayTraffic.SingleAsync(r => r.Icao == "LIRF");
        Assert.Equal(1, riga.Movements());
        Assert.Equal(0, riga.CoveredMovements);
        Assert.Equal(0, riga.AtcMinutes);
    }

    /// <summary>Torre e terra insieme sono UN'apertura: i minuti non si sommano due volte.</summary>
    [Fact]
    public async Task Due_posizioni_insieme_fanno_un_apertura_sola()
    {
        await Sessione(1, "LIRF_TWR", 10, 12);
        await Sessione(2, "LIRF_GND", 10.5, 12.5);

        var (uc, _) = Caso(new() { ["LIRF"] = Array.Empty<SourceAirportMovement>() });
        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        var riga = await _db.AirportDayTraffic.SingleAsync(r => r.Icao == "LIRF");
        Assert.Equal(150, riga.AtcMinutes);      // 10:00→12:30, non 240
    }

    [Fact]
    public async Task Con_le_statistiche_escluse_dalla_policy_non_si_chiede_niente()
    {
        await new EfImportPolicyStore(_db).SaveAsync(
            ImportPolicySnapshot.AllImported with { AtcSessions = false }, updatedByUserId: 704798);
        _db.ChangeTracker.Clear();

        var (uc, sorgente) = Caso(new() { ["LIRF"] = new[] { Arrivo("LIRF", 11) } });
        var esito = await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        Assert.Equal(0, esito.Chunks);
        Assert.Empty(sorgente.Chieste);
    }

    /// <summary>Un secondo giro non richiede quel che è già definitivo: l'arretrato deve scendere.</summary>
    [Fact]
    public async Task Il_secondo_giro_non_richiede_i_giorni_gia_presi()
    {
        var (uc, sorgente) = Caso(new() { ["LIRF"] = new[] { Arrivo("LIRF", 11) } });

        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));
        var quante = sorgente.Chieste.Count;

        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));
        Assert.Equal(quante, sorgente.Chieste.Count);
    }

    /// <summary>
    /// ⚠️ Il caso per cui la tabella esiste: un campo dove non è mai stato aperto niente deve comparire
    /// <b>con il suo traffico</b> e zero coperti. Se sparisse, la pagina direbbe che copriamo tutto quello
    /// che si vede.
    /// </summary>
    [Fact]
    public async Task Un_campo_senza_nessun_controllore_resta_in_elenco_con_zero_coperti()
    {
        var (uc, _) = Caso(new()
        {
            ["LIRA"] = new[] { Arrivo("LIRA", 9), Arrivo("LIRA", 14), Partenza("LIRA", 16) },
        });
        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        var esito = await new EfAirportCoverageQueries(_db).ByAirportAsync(G0, G0);

        var lira = esito.Rows.Single(r => r.Icao == "LIRA");
        Assert.Equal(3, lira.Movements);
        Assert.Equal(0, lira.Covered);
        Assert.Equal(0, lira.CoveredPercent);

        // E il campo senza nemmeno una riga consolidata resta comunque in elenco, a zero.
        Assert.Contains(esito.Rows, r => r.Icao == "LIRF");
        // Quello nascosto non c'è mai.
        Assert.DoesNotContain(esito.Rows, r => r.Icao == "LIRZ");
    }

    [Fact]
    public async Task La_lettura_dice_quanti_giorni_sono_davvero_consolidati()
    {
        var (uc, _) = Caso(new() { ["LIRF"] = new[] { Arrivo("LIRF", 11) } });
        await uc.RunAsync(G0, G0, max: 10, now: G0.AddDays(2));

        // Si chiedono tre giorni, ne è stato consolidato uno: la pagina deve poterlo dire.
        var esito = await new EfAirportCoverageQueries(_db).ByAirportAsync(G0, G0.AddDays(2));

        Assert.Equal(1, esito.DaysCovered);
        Assert.Equal(3, esito.DaysAsked);
    }

    [Fact]
    public async Task Il_gruppo_restringe_agli_aeroporti_di_quell_acc()
    {
        var altro = new Acc { Code = "LIMM", Name = "Milano" };
        _db.Accs.Add(altro);
        _db.Airports.Add(new Airport { Icao = "LIMC", Name = "Malpensa", Acc = altro });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var q = new EfAirportCoverageQueries(_db);

        Assert.All((await q.ByAirportAsync(G0, G0, "LIMM")).Rows, r => Assert.Equal("LIMM", r.AccCode));
        Assert.Contains(await q.GroupsAsync(), g => g.Code == "LIRR" && g.Airports == 2);
    }
}

/// <summary>Comodità di lettura per i test: i movimenti veri di una riga consolidata.</summary>
internal static class AirportDayTrafficTestExtensions
{
    public static int Movements(this AirportDayTraffic r) => r.Inbound + r.Outbound;
}

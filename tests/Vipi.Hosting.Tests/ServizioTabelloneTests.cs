using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Tabellone;
using Vipi.Hosting;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Il servizio dietro <c>GET /api/tabellone/{ICAO}</c> (carta docs/feature/2026-10-02-tabellone-partenze-arrivi.md):
/// una risposta per scalo ogni 15 s uguale per tutti, il booking al più una volta al minuto con l'ultima lettura buona
/// tenuta, lo stand dal documento del ponte, il JSON nel formato concordato e senza VID. Fonti finte, niente rete.
/// </summary>
public class ServizioTabelloneTests
{
    private static readonly DateTimeOffset Ora = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private const string BookingSalvato = """
    [ { "callsign": "AZA1", "origin_icao": "LIRN", "destination_icao": "LIRF", "aircraft_icao": "A320",
        "eobt": "2026-10-03 11:30:00", "eat": "2026-10-03 12:30:00", "gate": "509", "booked_by": 100001 },
      { "callsign": "RYR3", "origin_icao": "LIRF", "destination_icao": "EGSS", "aircraft_icao": "B738",
        "eobt": "2026-10-03 12:30:00", "eat": null, "gate": "TBD", "booked_by": 100002 },
      { "callsign": "EZY5", "origin_icao": "LIRF", "destination_icao": "LFPG", "aircraft_icao": "A319",
        "eobt": "2026-10-03 12:40:00", "eat": null, "gate": "512", "booked_by": null } ]
    """;

    private const string Ponte = """{ "board": { "arr:AZA1": "507", "dep:RYR3": "510" }, "boardAt": "2026-10-03T11:58:00Z" }""";

    [Fact]
    public async Task Uno_scalo_senza_tabellone_configurato_non_ha_risposta()
    {
        var (servizio, _, _, _, _) = Servizio();
        Assert.Null(await servizio.LeggiAsync("LIRN"));
    }

    [Fact]
    public async Task Una_risposta_ogni_15_secondi_uguale_per_tutti_e_il_booking_al_piu_ogni_minuto()
    {
        var (servizio, orologio, booking, _, _) = Servizio();

        var prima = await servizio.LeggiAsync("LIRF");
        orologio.Avanti(TimeSpan.FromSeconds(14));
        Assert.Same(prima, await servizio.LeggiAsync("LIRF"));
        Assert.Equal(1, booking.Letture);

        orologio.Avanti(TimeSpan.FromSeconds(2));
        var seconda = await servizio.LeggiAsync("LIRF");
        Assert.NotSame(prima, seconda);
        Assert.Equal(1, booking.Letture);                 // 16 s: il booking non si rilegge

        orologio.Avanti(TimeSpan.FromSeconds(45));
        await servizio.LeggiAsync("LIRF");
        Assert.Equal(2, booking.Letture);                 // 61 s
    }

    [Fact]
    public async Task Se_il_booking_tace_restano_le_righe_dell_ultima_lettura_buona()
    {
        var (servizio, orologio, booking, _, _) = Servizio();
        await servizio.LeggiAsync("LIRF");

        booking.Guasto = true;
        orologio.Avanti(TimeSpan.FromSeconds(61));
        var r = (await servizio.LeggiAsync("LIRF"))!.Risposta;
        Assert.True(r.Fonti.Booking.Ok);                  // l'ultima buona ha un minuto
        Assert.Contains(r.Arrivi, x => x.Id == "AZA1");

        orologio.Avanti(TimeSpan.FromMinutes(3));
        r = (await servizio.LeggiAsync("LIRF"))!.Risposta;
        Assert.False(r.Fonti.Booking.Ok);
        Assert.Contains(r.Arrivi, x => x.Id == "AZA1");
    }

    [Fact]
    public async Task Il_JSON_e_nel_formato_concordato_e_senza_VID()
    {
        var (servizio, _, _, _, _) = Servizio();
        var pronto = (await servizio.LeggiAsync("LIRF"))!;
        var testo = Encoding.UTF8.GetString(pronto.Json);

        Assert.DoesNotContain("10000", testo);
        Assert.DoesNotContain("booked", testo, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(testo);
        var radice = doc.RootElement;
        Assert.Equal(1, radice.GetProperty("versione").GetInt32());
        Assert.Equal("2026-10-03T12:00:00Z", radice.GetProperty("aggiornato").GetString());
        Assert.Equal("ROMA FIUMICINO", radice.GetProperty("scalo").GetProperty("nome").GetString());
        Assert.Equal("Europe/Rome", radice.GetProperty("scalo").GetProperty("fusoOrario").GetString());
        Assert.Equal("ROMA RFE", radice.GetProperty("evento").GetProperty("nome").GetString());
        Assert.Equal("tutti", radice.GetProperty("voli").GetString());
        Assert.True(radice.GetProperty("fonti").GetProperty("gateManager").GetProperty("ok").GetBoolean());
        Assert.Equal("2026-10-03T11:58:00Z", radice.GetProperty("fonti").GetProperty("gateManager").GetProperty("letto").GetString());

        var aza = radice.GetProperty("arrivi")[0];
        Assert.Equal("AZA1", aza.GetProperty("id").GetString());
        Assert.Equal("2026-10-03T12:30:00Z", aza.GetProperty("programmato").GetString());
        Assert.Equal(JsonValueKind.Null, aza.GetProperty("stimato").ValueKind);
        Assert.Equal("507", aza.GetProperty("gate").GetString());
        Assert.True(aza.GetProperty("gateCambiato").GetBoolean());
        Assert.Equal("SCHEDULED", aza.GetProperty("stato").GetString());
        Assert.Equal("NAPLES", aza.GetProperty("scalo").GetProperty("citta").GetString());
        Assert.Equal("NAP", aza.GetProperty("scalo").GetProperty("iata").GetString());

        // EZY5 non l'ha prenotato nessuno: non compare.
        Assert.Equal(new[] { "RYR3" }, radice.GetProperty("partenze").EnumerateArray().Select(x => x.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task L_etichetta_non_cambia_se_cambia_solo_l_ora_del_calcolo()
    {
        var (servizio, orologio, _, ponte, _) = Servizio();
        var prima = (await servizio.LeggiAsync("LIRF"))!;

        orologio.Avanti(TimeSpan.FromSeconds(20));
        var stessa = (await servizio.LeggiAsync("LIRF"))!;
        Assert.NotEqual(prima.Risposta.Aggiornato, stessa.Risposta.Aggiornato);
        Assert.Equal(prima.ETag, stessa.ETag);

        ponte.Data = Ponte.Replace("\"507\"", "\"508\"");
        orologio.Avanti(TimeSpan.FromSeconds(20));
        Assert.NotEqual(prima.ETag, (await servizio.LeggiAsync("LIRF"))!.ETag);
    }

    [Fact]
    public async Task L_anagrafica_si_chiede_una_volta_per_scalo_anche_quando_non_lo_conosce()
    {
        var (servizio, orologio, _, _, anagrafica) = Servizio();

        await servizio.LeggiAsync("LIRF");
        orologio.Avanti(TimeSpan.FromSeconds(20));
        await servizio.LeggiAsync("LIRF");

        Assert.Equal(1, anagrafica.Chieste.Count(i => i == "EGSS"));   // sconosciuto: non si richiede a ogni giro
        Assert.Equal(1, anagrafica.Chieste.Count(i => i == "LIRN"));
    }

    // ---------------------------------------------------------------- finti

    private static (ServizioTabellone, OrologioFinto, BookingFinto, PonteFinto, AnagraficaFinta) Servizio()
    {
        var anagrafica = new AnagraficaFinta();
        var orologio = new OrologioFinto(Ora);
        var booking = new BookingFinto();
        var ponte = new PonteFinto { Data = Ponte };
        var sp = new ServiceCollection()
            .AddSingleton<IRfoSharedStateStore>(ponte)
            .AddSingleton<IAirportDirectory>(anagrafica)
            .BuildServiceProvider();
        var opzioni = new TabelloneOptions();
        opzioni.Scali["LIRF"] = new TabelloneScaloOptions { Nome = "Roma Fiumicino", EventoRfo = "lirf-20261003", NomeEvento = "Roma RFE" };

        var piloti = new FotografiaPiloti();
        piloti.Pubblica(Array.Empty<SourcePilotFix>(), Ora.AddSeconds(-20));

        var servizio = new ServizioTabellone(sp.GetRequiredService<IServiceScopeFactory>(), booking, piloti,
            new Monitor(opzioni), NullLogger<ServizioTabellone>.Instance, orologio);
        return (servizio, orologio, booking, ponte, anagrafica);
    }

    private sealed class OrologioFinto(DateTimeOffset inizio) : TimeProvider
    {
        private DateTimeOffset _ora = inizio;
        public void Avanti(TimeSpan t) => _ora += t;
        public override DateTimeOffset GetUtcNow() => _ora;
    }

    private sealed class BookingFinto : IBookingSource
    {
        public int Letture;
        public bool Guasto;
        public Task<string> LeggiAsync(CancellationToken ct = default)
        {
            Letture++;
            return Guasto ? throw new HttpRequestException("Booking: HTTP 500.") : Task.FromResult(BookingSalvato);
        }
    }

    private sealed class PonteFinto : IRfoSharedStateStore
    {
        public string Data = "{}";
        public Task<RfoStateRow?> LoadAsync(string eventId, CancellationToken ct = default) =>
            Task.FromResult<RfoStateRow?>(eventId == "lirf-20261003" ? new RfoStateRow(7, Data, "LIRF_DEL", DateTime.UtcNow) : null);
        public Task<RfoWriteResult> WriteAsync(string eventId, long expectedVersion, string data, string? updatedBy, CancellationToken ct = default) =>
            throw new NotSupportedException("Il tabellone non scrive sul ponte.");
        public Task<int> PotaStoriaAsync(DateTime primaDi, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AnagraficaFinta : IAirportDirectory
    {
        public readonly List<string> Chieste = new();

        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAirport>>(Array.Empty<SourceAirport>());

        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default)
        {
            Chieste.Add(icao);
            return Task.FromResult(icao switch
            {
                "LIRN" => new SourceAirport("LIRN", "Napoli Capodichino", "LIRR", "Naples", Iata: "NAP"),
                "LIRF" => new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", "Rome", Iata: "FCO"),
                _ => null,
            });
        }
    }

    private sealed class Monitor(TabelloneOptions valore) : IOptionsMonitor<TabelloneOptions>
    {
        public TabelloneOptions CurrentValue => valore;
        public TabelloneOptions Get(string? name) => valore;
        public IDisposable? OnChange(Action<TabelloneOptions, string?> listener) => null;
    }
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Tabellone;

namespace Vipi.Hosting;

/// <summary>
/// Il tabellone partenze/arrivi: <c>GET /api/tabellone/{ICAO}</c>, pubblico, senza chiave, solo lettura (committente,
/// 2 ottobre 2026: un'eccezione voluta alla regola «le API non sono mai anonime», perché lo legge uno schermo e i
/// dati sono quelli che chiunque vede già su Whazzup e sul sito del booking, senza i VID). Carta
/// <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c>; formato <c>Dep_arr_board/FORMATO-DATI.md</c>.
///
/// <list type="bullet">
/// <item>Una risposta per scalo calcolata al più ogni <see cref="ServizioTabellone.Ricalcolo"/>, uguale per tutti:
///   mille schermi costano un calcolo ogni 15 secondi.</item>
/// <item>ETag sulla risposta: lo schermo che rimanda <c>If-None-Match</c> riceve un 304 senza corpo, che non entra
///   nel registro delle richieste.</item>
/// <item>CORS aperto (<c>*</c>, senza credenziali): la pagina del tabellone può stare altrove.</item>
/// <item>404 per uno scalo che non ha un tabellone configurato (<c>Tabellone:Scali</c>).</item>
/// </list>
/// </summary>
public static class Tabellone
{
    public const string Rotta = "/api/tabellone/{icao}";
    public const string PrefissoRotta = "/api/tabellone/";

    private static readonly Regex IcaoValido = new("^[A-Za-z0-9]{4}$", RegexOptions.CultureInvariant);

    public static IServiceCollection AddVipiTabellone(this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<TabelloneOptions>(configuration.GetSection(TabelloneOptions.SectionName));
        services.AddHttpClient(Vipi.Infrastructure.Booking.BookingItClient.NomeClient, c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<IBookingSource, Vipi.Infrastructure.Booking.BookingItClient>();
        // La registra anche AddVipiIvao (la riempie il poller): qui c'è perché il tabellone si monta anche senza.
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .TryAddSingleton<FotografiaPiloti>(services);
        services.AddSingleton<ServizioTabellone>();
        return services;
    }

    public static IEndpointRouteBuilder MapTabellone(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Rotta, async (string icao, HttpContext ctx, ServizioTabellone servizio, CancellationToken ct) =>
        {
            var risposta = ctx.Response;
            risposta.Headers.AccessControlAllowOrigin = "*";
            if (!IcaoValido.IsMatch(icao)) return Results.StatusCode(StatusCodes.Status404NotFound);

            var pronta = await servizio.LeggiAsync(icao.ToUpperInvariant(), ct);
            if (pronta is null) return Results.StatusCode(StatusCodes.Status404NotFound);

            risposta.Headers.ETag = pronta.ETag;
            // Pubblica e uguale per tutti: un proxy può tenerla quanto il ricalcolo.
            risposta.Headers.CacheControl = "public, max-age=" + ((int)ServizioTabellone.Ricalcolo.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            if (ctx.Request.Headers.IfNoneMatch.ToString().Split(',').Any(e => Etichetta(e) == pronta.ETag))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Bytes(pronta.Json, "application/json; charset=utf-8");
        });

        // Il preflight non serve a un GET semplice, ma uno schermo che manda If-None-Match da fetch() lo fa partire.
        endpoints.MapMethods(Rotta, new[] { HttpMethods.Options }, (HttpContext ctx) =>
        {
            ctx.Response.Headers.AccessControlAllowOrigin = "*";
            ctx.Response.Headers.AccessControlAllowMethods = "GET";
            ctx.Response.Headers.AccessControlAllowHeaders = "If-None-Match";
            ctx.Response.Headers.AccessControlMaxAge = "86400";
            return Results.NoContent();
        });
        return endpoints;
    }

    /// <summary><c>W/"x"</c> e <c>"x"</c> sono la stessa etichetta: un proxy che comprime aggiunge la W.</summary>
    private static string Etichetta(string e)
    {
        var v = e.Trim();
        return v.StartsWith("W/", StringComparison.Ordinal) ? v[2..] : v;
    }
}

/// <summary>Una risposta già scritta, con la sua etichetta.</summary>
public sealed record TabellonePronto(byte[] Json, string ETag, RispostaTabellone Risposta);

/// <summary>
/// Raccoglie le fonti e calcola il tabellone di uno scalo, al più ogni <see cref="Ricalcolo"/>. Singleton.
///
/// <list type="bullet">
/// <item><b>Whazzup</b>: i piloti che il poller IVAO pubblica in <see cref="FotografiaPiloti"/> dalla lettura che
///   fa già. Nessuna chiamata in più.</item>
/// <item><b>Booking</b>: letto al più ogni <see cref="GiroBooking"/>, uno per tutti gli scali (il booking restituisce
///   l'evento in corso, non ha uno scalo nel percorso). Se tace si tiene l'ultima lettura buona.</item>
/// <item><b>Stand del Gate Manager</b>: la voce <c>board</c> del documento del ponte RFO dell'evento configurato per
///   lo scalo, letta dal database a ogni calcolo.</item>
/// <item><b>Città e IATA</b> degli altri scali: anagrafica aeroporti di vIPI. Gli scali che non conosce si cercano
///   prima del calcolo, al più <see cref="RicercheAlGiro"/> per giro; uno che non trova si riprova fra sei ore.</item>
/// </list>
/// </summary>
public sealed class ServizioTabellone
{
    public static readonly TimeSpan Ricalcolo = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan GiroBooking = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RiprovaAnagrafica = TimeSpan.FromHours(6);
    private const int RicercheAlGiro = 10;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new OrarioUtc() },
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IBookingSource _booking;
    private readonly FotografiaPiloti _piloti;
    private readonly IOptionsMonitor<TabelloneOptions> _opzioni;
    private readonly TimeProvider _orologio;
    private readonly ILogger<ServizioTabellone> _log;

    private readonly ConcurrentDictionary<string, StatoScalo> _scali = new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _cancelloBooking = new(1, 1);
    private LetturaBooking? _ultimoBooking;
    private DateTimeOffset? _bookingLetto;
    private DateTimeOffset _bookingProvato = DateTimeOffset.MinValue;

    private readonly ConcurrentDictionary<string, ((string? Citta, string? Iata)? Dati, DateTimeOffset Al)> _anagrafica =
        new(StringComparer.OrdinalIgnoreCase);

    public ServizioTabellone(IServiceScopeFactory scopes, IBookingSource booking, FotografiaPiloti piloti,
        IOptionsMonitor<TabelloneOptions> opzioni, ILogger<ServizioTabellone> log, TimeProvider? orologio = null)
    {
        _scopes = scopes;
        _booking = booking;
        _piloti = piloti;
        _opzioni = opzioni;
        _log = log;
        _orologio = orologio ?? TimeProvider.System;
    }

    private sealed record Calcolo(TabellonePronto Pronto, DateTimeOffset Al);

    private sealed class StatoScalo
    {
        public readonly SemaphoreSlim Cancello = new(1, 1);
        public readonly MemoriaTabellone Memoria = new();
        // Risposta e ora del calcolo in UN riferimento: letti fuori dal cancello, non devono essere di due giri diversi.
        public Calcolo? Ultimo;
        public ScaloTabellone? Scalo;
        public BoardDelPonte.Board? UltimaBoard;
    }

    /// <summary>Il tabellone dello scalo, o null se lo scalo non ha un tabellone configurato.</summary>
    public async Task<TabellonePronto?> LeggiAsync(string icao, CancellationToken ct = default)
    {
        if (!_opzioni.CurrentValue.Scali.TryGetValue(icao, out var config)) return null;

        var stato = _scali.GetOrAdd(icao, _ => new StatoScalo());
        if (Volatile.Read(ref stato.Ultimo) is { } fresco && _orologio.GetUtcNow() - fresco.Al < Ricalcolo) return fresco.Pronto;

        await stato.Cancello.WaitAsync(ct);
        try
        {
            // Calcolato da chi è entrato prima: lo stesso per tutti.
            if (stato.Ultimo is { } appena && _orologio.GetUtcNow() - appena.Al < Ricalcolo) return appena.Pronto;

            var pronto = await CalcolaAsync(icao, config, stato, ct);
            Volatile.Write(ref stato.Ultimo, new Calcolo(pronto, _orologio.GetUtcNow()));
            return pronto;
        }
        finally
        {
            stato.Cancello.Release();
        }
    }

    private async Task<TabellonePronto> CalcolaAsync(string icao, TabelloneScaloOptions config, StatoScalo stato, CancellationToken ct)
    {
        await AggiornaBookingAsync(ct);
        var board = await LeggiBoardAsync(config.EventoRfo, stato, ct);
        stato.Scalo = await ScaloAsync(icao, config, ct);

        var booking = Volatile.Read(ref _ultimoBooking);
        var piloti = _piloti.Corrente;

        // Gli altri scali delle righe possibili, cercati PRIMA del calcolo (al più RicercheAlGiro per giro: gli altri
        // al giro dopo, intanto la città è l'ICAO).
        var altri = (booking?.Voli ?? Array.Empty<VoloPrenotato>())
            .SelectMany(v => Uguale(v.Origine, icao) ? new[] { v.Destinazione } : Uguale(v.Destinazione, icao) ? new[] { v.Origine } : [])
            .Concat(piloti.Piloti.SelectMany(p =>
                Uguale(p.DepIcao, icao) ? new[] { p.ArrIcao } : Uguale(p.ArrIcao, icao) ? new[] { p.DepIcao } : []))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i!.Trim().ToUpperInvariant())
            .Distinct();
        await CercaAnagraficaAsync(altri, ct);

        var ingressi = new IngressiTabellone(
            stato.Scalo, config.NomeEvento, config.Voli,
            booking, _bookingLetto,
            piloti,
            board?.Stand, board?.Al, !string.IsNullOrWhiteSpace(config.EventoRfo),
            altro => _anagrafica.TryGetValue(altro, out var v) ? v.Dati : null);
        var risposta = RegoleTabellone.Calcola(ingressi, stato.Memoria, _orologio.GetUtcNow());

        var json = JsonSerializer.SerializeToUtf8Bytes(risposta, Json);
        // L'etichetta è il contenuto SENZA l'ora del calcolo: se non è cambiato niente lo schermo riceve un 304.
        var senzaOra = JsonSerializer.SerializeToUtf8Bytes(risposta with { Aggiornato = default }, Json);
        var etag = "\"" + Convert.ToHexString(SHA256.HashData(senzaOra), 0, 12).ToLowerInvariant() + "\"";
        return new TabellonePronto(json, etag, risposta);
    }

    private async Task AggiornaBookingAsync(CancellationToken ct)
    {
        if (_orologio.GetUtcNow() - _bookingProvato < GiroBooking) return;
        if (!await _cancelloBooking.WaitAsync(0, ct)) return;   // lo sta già leggendo un altro scalo
        try
        {
            _bookingProvato = _orologio.GetUtcNow();
            var lettura = BookingParser.Leggi(await _booking.LeggiAsync(ct));
            if (lettura.Ok)
            {
                Volatile.Write(ref _ultimoBooking, lettura);
                _bookingLetto = _orologio.GetUtcNow();
            }
            else _log.LogWarning("Tabellone: booking illeggibile ({Errore}); tengo l'ultima lettura buona.", lettura.Errore);
        }
        catch (SorgenteNonConfigurataException ex)
        {
            _log.LogDebug("Tabellone: {Motivo}", ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning("Tabellone: booking non letto ({Tipo}: {Messaggio}); tengo l'ultima lettura buona.",
                ex.GetType().Name, ex.Message);
        }
        finally
        {
            _cancelloBooking.Release();
        }
    }

    private async Task<BoardDelPonte.Board?> LeggiBoardAsync(string? evento, StatoScalo stato, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evento)) return null;
        try
        {
            using var scope = _scopes.CreateScope();
            var riga = await scope.ServiceProvider.GetRequiredService<IRfoSharedStateStore>().LoadAsync(evento.Trim(), ct);
            stato.UltimaBoard = BoardDelPonte.Leggi(riga?.Data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Si tiene quella di prima: la sua età (boardAt) decide comunque se vale ancora.
            _log.LogWarning("Tabellone: documento del ponte {Evento} non letto ({Tipo}).", evento, ex.GetType().Name);
        }
        return stato.UltimaBoard;
    }

    private async Task<ScaloTabellone> ScaloAsync(string icao, TabelloneScaloOptions config, CancellationToken ct)
    {
        await CercaAnagraficaAsync(new[] { icao }, ct);
        var anag = _anagrafica.TryGetValue(icao, out var v) ? v.Dati : null;
        var nome = RegoleTabellone.Palette(config.Nome ?? anag?.Citta ?? icao, 32);
        return new ScaloTabellone(icao, anag?.Iata, nome, string.IsNullOrWhiteSpace(config.FusoOrario) ? "Europe/Rome" : config.FusoOrario.Trim());
    }

    private async Task CercaAnagraficaAsync(IEnumerable<string> icao, CancellationToken ct)
    {
        var adesso = _orologio.GetUtcNow();
        var daCercare = icao
            .Where(i => !_anagrafica.TryGetValue(i, out var v) || (v.Dati is null && adesso - v.Al > RiprovaAnagrafica))
            .Take(RicercheAlGiro)
            .ToList();
        if (daCercare.Count == 0) return;

        using var scope = _scopes.CreateScope();
        var anagrafica = scope.ServiceProvider.GetService<IAirportDirectory>();
        foreach (var i in daCercare)
        {
            (string? Citta, string? Iata)? dati = null;
            try
            {
                if (anagrafica is not null && await anagrafica.GetByIcaoAsync(i, ct) is { } a)
                    dati = (a.City, a.Iata);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Senza credenziali IVAO o con la sorgente giù: la città resta l'ICAO, si riprova fra sei ore.
                _log.LogDebug("Tabellone: anagrafica di {Icao} non letta ({Tipo}).", i, ex.GetType().Name);
            }
            _anagrafica[i] = (dati, adesso);
        }
    }

    private static bool Uguale(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gli orari come nel formato: ISO 8601 UTC con la Z, ai secondi.</summary>
    private sealed class OrarioUtc : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }
}

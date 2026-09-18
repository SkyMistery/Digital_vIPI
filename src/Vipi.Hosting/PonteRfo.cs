using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Hosting;

/// <summary>
/// Le chiavi del ponte RFO (sezione «Rfo»). Stanno nella configurazione del server — in pratica un file dentro
/// <c>segreti/</c> — non nel codice e non nel database. Carta <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c> §4.
///
/// <code>
/// "Rfo": { "Chiavi": {
///   "lirn-20260919": { "Chiave": "rfo_…", "Eventi": "lirn-20260919" },
///   "tutti":         { "Chiave": "rfo_…", "Eventi": "*" }
/// } }
/// </code>
///
/// <para>🔴 <b>Un dizionario per nome, non un array</b>, ed <see cref="RfoChiaveOptions.Eventi"/> è un testo con le
/// virgole, non un array: il binder <b>somma</b> gli array delle diverse sorgenti (appsettings + segreti) invece di
/// sostituirli — pagato il 17 settembre 2026 con <c>Translation:Targets</c> (§A62). Con i nomi, la stessa voce in
/// due file si sovrascrive; con gli indici si accoderebbe.</para>
/// </summary>
public sealed class RfoOptions
{
    public const string SectionName = "Rfo";

    public Dictionary<string, RfoChiaveOptions> Chiavi { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RfoChiaveOptions
{
    /// <summary>La chiave. Più corta di <see cref="PonteRfo.LunghezzaMinimaChiave"/> caratteri non apre niente:
    /// una chiave «prova» dimenticata nei segreti non deve diventare una porta.</summary>
    public string Chiave { get; set; } = "";

    /// <summary>Gli eventi che la chiave apre, separati da virgola; <c>*</c> = tutti. Vuoto = nessuno (403).</summary>
    public string Eventi { get; set; } = "";
}

/// <summary>
/// Il ponte fra le copie di «RFO Gate Manager» delle postazioni ATC di un evento RFO: un documento JSON per evento,
/// con una versione. Il contratto è quello del programma (<c>docs/SYNC-API.md</c> della repo
/// SkyMistery/RFO-Stand-Manager) e si segue alla lettera; la carta del sito è
/// <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c>.
///
/// <list type="bullet">
/// <item><c>GET</c>: 404 se mai scritto, 304 se <c>If-None-Match</c> è la versione di oggi, altrimenti 200 con la busta.</item>
/// <item><c>PUT</c>: 428 senza <c>If-Match</c>; 404 se il documento non c'è e <c>If-Match</c> non è <c>"0"</c>;
///   409 <b>con la busta attuale</b> se qualcuno ha scritto prima; 200 con la busta nuova.</item>
/// <item>401 senza corpo se la chiave manca o è sbagliata; 403 se è buona ma per un altro evento.</item>
/// </list>
///
/// <para>⚠️ <b>Fuori da <c>/services</c> di proposito</b>: la cache delle letture anonime
/// (<c>CacheDelleLettureAnonime</c>) vale solo lì, e una copia tenuta sessanta secondi qui vorrebbe dire postazioni
/// che non si vedono fra loro. Niente CORS: chi chiama è un programma desktop, non un browser.</para>
///
/// <para>⚠️ <b>ETag deboli.</b> Un proxy che comprime (Cloudflare, nginx) può riscrivere <c>"12"</c> in <c>W/"12"</c>,
/// e il client rimanda quello che ha ricevuto: <c>If-None-Match</c> e <c>If-Match</c> accettano tutte e due le forme,
/// o il 304 non scatterebbe mai e ogni postazione riscaricherebbe il documento ogni tre secondi.</para>
/// </summary>
public static class PonteRfo
{
    public const string Rotta = "/api/rfo/events/{eventId}/state";
    public const string PrefissoRotta = "/api/rfo/";
    public const int LunghezzaMinimaChiave = 32;

    private static readonly Regex EventoValido = new("^[a-z0-9-]{1,64}$", RegexOptions.CultureInvariant);

    private static readonly JsonDocumentOptions Lettura = new() { MaxDepth = 256 };

    public static IEndpointRouteBuilder MapPonteRfo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Rotta, async (string eventId, HttpContext ctx, IRfoSharedStateStore store,
            IOptionsMonitor<RfoOptions> opzioni, CancellationToken ct) =>
        {
            if (!EventoValido.IsMatch(eventId)) return Vuota(StatusCodes.Status400BadRequest);
            if (Porta(ctx, eventId, opzioni.CurrentValue) is { } rifiuto) return rifiuto;

            var attuale = await store.LoadAsync(eventId, ct);
            if (attuale is null) return Vuota(StatusCodes.Status404NotFound);

            if (VersioneDa(ctx.Request.Headers.IfNoneMatch.ToString()) == attuale.Version)
            {
                ctx.Response.Headers.ETag = Etichetta(attuale.Version);
                return Vuota(StatusCodes.Status304NotModified);
            }

            return new Busta(attuale, StatusCodes.Status200OK);
        });

        endpoints.MapPut(Rotta, async (string eventId, HttpContext ctx, IRfoSharedStateStore store,
            IOptionsMonitor<RfoOptions> opzioni, CancellationToken ct) =>
        {
            if (!EventoValido.IsMatch(eventId)) return Vuota(StatusCodes.Status400BadRequest);
            if (Porta(ctx, eventId, opzioni.CurrentValue) is { } rifiuto) return rifiuto;

            // Senza If-Match una postazione sovrascriverebbe alla cieca quello che hanno deciso le altre.
            var ifMatch = ctx.Request.Headers.IfMatch.ToString();
            if (string.IsNullOrWhiteSpace(ifMatch)) return Vuota(StatusCodes.Status428PreconditionRequired);
            if (VersioneDa(ifMatch) is not { } attesa) return Vuota(StatusCodes.Status400BadRequest);

            if (ctx.Request.ContentLength > RfoLimits.CorpoMassimo) return Vuota(StatusCodes.Status413PayloadTooLarge);

            // Il tetto si conta LEGGENDO, non solo dall'intestazione: un corpo a blocchi non dichiara la lunghezza.
            var corpo = await LeggiAsync(ctx.Request.Body, ct);
            if (corpo is null) return Vuota(StatusCodes.Status413PayloadTooLarge);

            string data;
            string? da;
            try
            {
                using var doc = JsonDocument.Parse(corpo, Lettura);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("data", out var dati)
                    || dati.ValueKind != JsonValueKind.Object)
                    return Vuota(StatusCodes.Status422UnprocessableEntity);

                // Il testo ESATTO che la postazione ha mandato: il sito non lo apre, non lo riordina, non lo
                // riscrive. Nuove funzioni del programma non devono chiedere un pacchetto del sito.
                data = dati.GetRawText();

                if (!doc.RootElement.TryGetProperty("updatedBy", out var chi) || chi.ValueKind == JsonValueKind.Null)
                    da = null;
                else if (chi.ValueKind == JsonValueKind.String)
                    da = Tronca(chi.GetString()!, RfoLimits.UpdatedBy);
                else
                    return Vuota(StatusCodes.Status422UnprocessableEntity);
            }
            catch (JsonException)
            {
                return Vuota(StatusCodes.Status400BadRequest);
            }

            var esito = await store.WriteAsync(eventId, attesa, data, da, ct);
            return esito.Outcome switch
            {
                RfoWriteOutcome.Scritto => new Busta(esito.Current!, StatusCodes.Status200OK),
                // Il 409 porta la busta di oggi: la postazione riapplica la sua modifica su quella e riprova,
                // senza una lettura in più. È questo che fa SOMMARE due scritture nello stesso secondo.
                RfoWriteOutcome.Conflitto => new Busta(esito.Current!, StatusCodes.Status409Conflict),
                _ => Vuota(StatusCodes.Status404NotFound),
            };
        });

        return endpoints;
    }

    /// <summary>
    /// Null se la chiave apre questo evento; altrimenti 401 (mancante o sconosciuta) o 403 (buona, altro evento),
    /// senza corpo. Il confronto è a tempo costante e percorre SEMPRE tutte le chiavi: quanto ci mette la risposta
    /// non deve dire quanto era vicina la chiave provata, né quale.
    /// </summary>
    internal static IResult? Porta(HttpContext ctx, string eventId, RfoOptions opzioni)
    {
        var presentata = ctx.Request.Headers["x-api-key"].ToString().Trim();
        var esito = Verifica(presentata, eventId, opzioni);
        if (esito == EsitoChiaveRfo.Valida) return null;

        var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(PortaDelleApi.CategoriaLog);
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
        if (esito == EsitoChiaveRfo.AltroEvento)
        {
            log.LogWarning("Ponte RFO {Evento}: chiave valida ma non per questo evento, da {Ip}", eventId, ip);
            return Vuota(StatusCodes.Status403Forbidden);
        }

        log.LogInformation("Ponte RFO {Evento}: chiave {Stato}, da {Ip}", eventId,
            presentata.Length == 0 ? "mancante" : "sconosciuta", ip);
        return Vuota(StatusCodes.Status401Unauthorized);
    }

    internal static EsitoChiaveRfo Verifica(string? presentata, string eventId, RfoOptions opzioni)
    {
        if (string.IsNullOrEmpty(presentata)) return EsitoChiaveRfo.Sconosciuta;

        var impronta = SHA256.HashData(Encoding.UTF8.GetBytes(presentata));
        var esito = EsitoChiaveRfo.Sconosciuta;
        foreach (var voce in opzioni.Chiavi.Values)
        {
            var chiave = voce?.Chiave?.Trim() ?? "";
            if (chiave.Length < LunghezzaMinimaChiave) continue;

            var uguale = CryptographicOperations.FixedTimeEquals(impronta, SHA256.HashData(Encoding.UTF8.GetBytes(chiave)));
            if (!uguale) continue;

            var apre = ApreEvento(voce!.Eventi, eventId);
            if (apre) esito = EsitoChiaveRfo.Valida;
            else if (esito != EsitoChiaveRfo.Valida) esito = EsitoChiaveRfo.AltroEvento;
        }
        return esito;
    }

    private static bool ApreEvento(string? eventi, string eventId)
    {
        foreach (var voce in (eventi ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (voce == "*" || string.Equals(voce, eventId, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// La versione da <c>If-Match</c> o <c>If-None-Match</c>: <c>"12"</c>, <c>W/"12"</c> o <c>12</c>. Null se
    /// l'intestazione manca o non è UNA versione (un elenco, <c>*</c>, un numero negativo).
    /// </summary>
    internal static long? VersioneDa(string? intestazione)
    {
        var v = intestazione?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.StartsWith("W/", StringComparison.Ordinal)) v = v[2..];
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v[1..^1];
        return long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    internal static string Etichetta(long versione) => $"\"{versione.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Il corpo intero, o null se supera <see cref="RfoLimits.CorpoMassimo"/>.</summary>
    private static async Task<byte[]?> LeggiAsync(Stream corpo, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var pezzo = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int letti;
            while ((letti = await corpo.ReadAsync(pezzo.AsMemory(), ct)) > 0)
            {
                if (buffer.Length + letti > RfoLimits.CorpoMassimo) return null;
                buffer.Write(pezzo, 0, letti);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pezzo);
        }
        return buffer.ToArray();
    }

    private static string Tronca(string s, int massimo)
    {
        if (s.Length <= massimo) return s;
        // Non si spezza una coppia surrogata: mezzo carattere diventerebbe un «?» nel database.
        var n = char.IsHighSurrogate(s[massimo - 1]) ? massimo - 1 : massimo;
        return s[..n];
    }

    private static IResult Vuota(int stato) => Results.StatusCode(stato);

    /// <summary>
    /// La busta del contratto, con <see cref="RfoStateRow.Data"/> scritto <b>così com'è</b> (<c>WriteRawValue</c>):
    /// passarlo da un <c>JsonNode</c> lo riscriverebbe (ordine, spazi, escape dei caratteri non ASCII).
    /// </summary>
    private sealed class Busta : IResult
    {
        private readonly RfoStateRow _riga;
        private readonly int _stato;

        public Busta(RfoStateRow riga, int stato)
        {
            _riga = riga;
            _stato = stato;
        }

        public async Task ExecuteAsync(HttpContext ctx)
        {
            var buffer = new ArrayBufferWriter<byte>(_riga.Data.Length + 256);
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                w.WriteNumber("version", _riga.Version);
                w.WriteString("updatedAt", DateTime.SpecifyKind(_riga.UpdatedAtUtc, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                if (_riga.UpdatedBy is null) w.WriteNull("updatedBy");
                else w.WriteString("updatedBy", _riga.UpdatedBy);
                w.WritePropertyName("data");
                w.WriteRawValue(_riga.Data, skipInputValidation: true);
                w.WriteEndObject();
            }

            var risposta = ctx.Response;
            risposta.StatusCode = _stato;
            risposta.ContentType = "application/json; charset=utf-8";
            risposta.Headers.ETag = Etichetta(_riga.Version);
            // Nessuna copia senza chiedere: un proxy che tenesse la busta farebbe vedere a una postazione il
            // documento di un minuto fa. Con l'ETag la domanda costa un 304.
            risposta.Headers.CacheControl = "private, no-cache";
            risposta.ContentLength = buffer.WrittenCount;
            await risposta.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
        }
    }
}

internal enum EsitoChiaveRfo
{
    Sconosciuta,
    AltroEvento,
    Valida,
}

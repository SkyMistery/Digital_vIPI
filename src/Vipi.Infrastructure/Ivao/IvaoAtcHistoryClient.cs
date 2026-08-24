using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO dello storico connessioni (<c>/v2/tracker/sessions</c>), paginato.
///
/// <para>Misurato il 24 agosto 2026 col token app: risponde <b>200</b> (a differenza dell'elenco membri di
/// divisione, che dà 500), il filtro <c>callsign</c> è un <b>prefisso di almeno tre caratteri</b> e la
/// retention è di circa 366 giorni. Costo del backfill di dodici mesi sull'Italia: 21 231 sessioni,
/// ~220 pagine da 100.</para>
///
/// <para>⚠️ La lista <b>non</b> porta <c>atcSession</c>: posizione e frequenza stanno solo sul dettaglio
/// per-sessione. Si evita una chiamata per sessione ricavando la posizione dal callsign.</para>
/// </summary>
public sealed class IvaoAtcHistoryClient : IAtcHistorySource
{
    /// <summary>Tetto di pagine per prefisso: 200 pagine sono 20 000 sessioni, molto oltre il caso reale.</summary>
    private const int MaxPages = 200;

    private const int PerPage = 100;

    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;

    public IvaoAtcHistoryClient(IvaoHttp http, IOptions<IvaoOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public async Task<IReadOnlyList<SourceAtcSessionHistory>> GetAtcSessionsAsync(
        string callsignPrefix, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var esito = new List<SourceAtcSessionHistory>();
        var pagina = 1;

        while (pagina <= MaxPages && !ct.IsCancellationRequested)
        {
            var path = $"{_opt.AtcSessionsPath}?connectionType=ATC&callsign={Uri.EscapeDataString(callsignPrefix)}" +
                       $"&perPage={PerPage}&page={pagina}" +
                       $"&from={Iso(from)}&to={Iso(to)}";

            using var res = await _http.SendGetAsync(path, ct);
            // ⚠️ Un errore NON è «zero sessioni»: se lo si ingoiasse, il backfill lascerebbe buchi silenziosi
            // nello storico. I 503 transitori della sorgente li assorbe già TransientRetryHandler.
            res.EnsureSuccessStatusCode();

            var d = await res.Content.ReadFromJsonAsync<SessionsPageDto>(cancellationToken: ct);
            var items = d?.Items ?? new List<SessionDto>();
            if (items.Count == 0) break;

            esito.AddRange(items
                .Where(i => !string.IsNullOrWhiteSpace(i.Callsign))
                .Select(i => new SourceAtcSessionHistory(
                    SessionId: i.Id,
                    UserId: i.UserId,
                    Callsign: i.Callsign!,
                    Rating: i.Rating,
                    StartUtc: i.CreatedAt,
                    EndUtc: i.CompletedAt,
                    ConnectedSeconds: i.Time)));

            if (d?.Pages is { } totale && pagina >= totale) break;
            pagina++;
        }

        return esito;
    }

    private static string Iso(DateTimeOffset t) =>
        t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    // DTO permissivi: forma verificata sulla risposta reale del 24 agosto 2026.
    private sealed record SessionsPageDto(
        [property: JsonPropertyName("totalItems")] int TotalItems,
        [property: JsonPropertyName("pages")] int Pages,
        [property: JsonPropertyName("items")] List<SessionDto>? Items);

    private sealed record SessionDto(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("callsign")] string? Callsign,
        [property: JsonPropertyName("rating")] int Rating,
        [property: JsonPropertyName("time")] int Time,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt);
}

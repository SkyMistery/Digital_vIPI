using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Tabellone;

namespace Vipi.Infrastructure.Booking;

/// <summary>
/// Il booking di IVAO Italia per il tabellone partenze/arrivi: <c>GET {Tabellone:BookingUrl}</c> con l'header
/// <c>x-api-key</c>. Carta <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c> §3.
///
/// <para>🔴 La chiave sta nei segreti del server (<c>Tabellone:BookingApiKey</c>), mai nel repo, che è pubblico; e
/// non si scrive nei log, nemmeno nei messaggi d'errore.</para>
/// </summary>
public sealed class BookingItClient : IBookingSource
{
    public const string NomeClient = "booking-it";

    /// <summary>Il booking è JSON di qualche centinaio di voli: un tetto largo ferma una risposta impazzita.</summary>
    private const int CorpoMassimo = 4 * 1024 * 1024;

    private readonly IHttpClientFactory _fabbrica;
    private readonly IOptionsMonitor<TabelloneOptions> _opzioni;

    public BookingItClient(IHttpClientFactory fabbrica, IOptionsMonitor<TabelloneOptions> opzioni)
    {
        _fabbrica = fabbrica;
        _opzioni = opzioni;
    }

    public async Task<string> LeggiAsync(CancellationToken ct = default)
    {
        var o = _opzioni.CurrentValue;
        var chiave = o.BookingApiKey?.Trim() ?? "";
        if (chiave.Length == 0)
            throw new SorgenteNonConfigurataException("Tabellone:BookingApiKey vuota: il tabellone va col solo Whazzup.");
        if (!Uri.TryCreate(o.BookingUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            throw new SorgenteNonConfigurataException("Tabellone:BookingUrl non è un indirizzo https.");

        using var richiesta = new HttpRequestMessage(HttpMethod.Get, url);
        richiesta.Headers.TryAddWithoutValidation("x-api-key", chiave);
        richiesta.Headers.Accept.ParseAdd("application/json");

        var http = _fabbrica.CreateClient(NomeClient);
        using var risposta = await http.SendAsync(richiesta, HttpCompletionOption.ResponseHeadersRead, ct);
        // 401 a corpo vuoto con la chiave sbagliata. Il messaggio dice lo stato, non la chiave.
        if (!risposta.IsSuccessStatusCode)
            throw new HttpRequestException($"Booking: HTTP {(int)risposta.StatusCode}.", null, risposta.StatusCode);
        if (risposta.Content.Headers.ContentLength > CorpoMassimo)
            throw new InvalidDataException("Booking: risposta troppo grande.");

        var testo = await risposta.Content.ReadAsStringAsync(ct);
        if (testo.Length > CorpoMassimo) throw new InvalidDataException("Booking: risposta troppo grande.");
        return testo;
    }
}

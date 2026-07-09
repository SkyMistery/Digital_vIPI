using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per l'anagrafica aeroporti (paginata, per country). Cache di processo condivisa
/// (<see cref="IvaoAirportCache"/>). Implementa la porta <see cref="IAirportDirectory"/>. Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoAirportClient : IAirportDirectory
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly IvaoAirportCache _airportCache;

    public IvaoAirportClient(IvaoHttp http, IOptions<IvaoOptions> opt, IvaoAirportCache airportCache)
    {
        _http = http;
        _opt = opt.Value;
        _airportCache = airportCache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
    {
        if (_airportCache.IsFresh) return _airportCache.Items!;
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'anagrafica aeroporti.");

        await _airportCache.Gate.WaitAsync(ct);
        try
        {
            if (_airportCache.IsFresh) return _airportCache.Items!;

            var all = new List<SourceAirport>();
            for (int page = 1; ; page++)
            {
                var path = $"{_opt.AirportsPath}?page={page}&countryId={Uri.EscapeDataString(_opt.AirportsCountryId)}";
                using var res = await _http.SendGetAsync(path, ct);
                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync(ct);
                    var snippet = string.IsNullOrWhiteSpace(body) ? "" : $" — {body[..Math.Min(body.Length, 200)]}";
                    throw new InvalidOperationException(
                        $"IVAO {(int)res.StatusCode} {res.StatusCode} su {_opt.AirportsPath} (scope: {_opt.Scopes}).{snippet}");
                }

                var pageDto = await res.Content.ReadFromJsonAsync<AirportsPageDto>(cancellationToken: ct);
                foreach (var a in pageDto?.Items ?? new List<AirportDto>())
                    if (!string.IsNullOrWhiteSpace(a.Icao))
                        all.Add(new SourceAirport(a.Icao!, a.Name ?? a.Icao!, a.CenterId, a.City, a.TransitionAltitude,
                            a.Latitude, a.Longitude));

                if (pageDto is null || page >= pageDto.Pages) break;
            }

            var result = all.OrderBy(a => a.Icao, StringComparer.OrdinalIgnoreCase).ToList();
            _airportCache.Items = result;
            _airportCache.ExpiresAt = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _opt.AirportsCacheHours));
            return result;
        }
        finally { _airportCache.Gate.Release(); }
    }
}

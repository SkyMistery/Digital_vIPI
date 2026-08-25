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
    public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
    {
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'anagrafica aeroporti.");

        var ttl = TimeSpan.FromHours(Math.Max(1, _opt.AirportsCacheHours));
        return _airportCache.GetOrLoadAsync(FetchAllPagesAsync, ttl, ct);
    }

    private async Task<IReadOnlyList<SourceAirport>> FetchAllPagesAsync(CancellationToken ct)
    {
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
                    all.Add(ToSource(a));

            if (pageDto is null || page >= pageDto.Pages) break;
        }

        return all.OrderBy(a => a.Icao, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public async Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        if (icao.Length == 0) return null;
        if (_airportCache.TryGetSingle(icao, out var cached)) return cached;
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile cercare l'aeroporto.");

        // /v2/airports/{ICAO}: dettaglio singolo (anche estero). 404/altro → null (best-effort, non blocca l'editing).
        var dto = await _http.GetJsonAsync<AirportDto>($"{_opt.AirportsPath}/{Uri.EscapeDataString(icao)}", ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Icao)) return null;

        var result = ToSource(dto);
        _airportCache.PutSingle(icao, result);
        return result;
    }

    /// <summary>
    /// Traduzione unica DTO -> porta: la lista paginata e il dettaglio per ICAO portano gli stessi campi
    /// (verificato sul filo il 25 agosto 2026), quindi la conversione sta in un posto solo — due copie
    /// divergerebbero al primo campo aggiunto da una parte sola.
    /// </summary>
    private static SourceAirport ToSource(AirportDto dto) => new(
        dto.Icao!, dto.Name ?? dto.Icao!, dto.CenterId, dto.City, dto.TransitionAltitude,
        HasMilitaryPresence: dto.Military ?? false,
        // La sorgente manda stringa vuota, non null, per gli aeroporti senza IATA (73 su 221): senza questa
        // normalizzazione in archivio finirebbero due modi diversi di dire «non ce l'ha».
        Iata: string.IsNullOrWhiteSpace(dto.Iata) ? null : dto.Iata.Trim().ToUpperInvariant(),
        ElevationFt: dto.Elevation,
        MagneticVariation: dto.Magnetic);
}

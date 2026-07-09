using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per l'elenco membri della divisione (controllori). Richiede credenziali (endpoint protetto).
/// Implementa la porta <see cref="IDivisionMembersProvider"/>. Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoDivisionClient : IDivisionMembersProvider
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;

    public IvaoDivisionClient(IvaoHttp http, IOptions<IvaoOptions> opt, IOptions<DivisionOptions> div)
    {
        _http = http;
        _opt = opt.Value;
        _div = div.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DivisionMember>> GetDivisionControllersAsync(CancellationToken ct = default)
    {
        var path = string.Format(_opt.DivisionMembersPathFormat, _div.Code);

        // Senza credenziali non c'è Bearer: l'endpoint membri è protetto → fallisce con 401. Messaggio chiaro.
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'elenco membri.");

        using var res = await _http.SendGetAsync(path, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            var snippet = string.IsNullOrWhiteSpace(body) ? "" : $" — {body[..Math.Min(body.Length, 200)]}";
            throw new InvalidOperationException(
                $"IVAO {(int)res.StatusCode} {res.StatusCode} su {path} (scope: {_opt.Scopes}). " +
                $"Verificare endpoint/scope/credenziali.{snippet}");
        }

        var raw = await res.Content.ReadFromJsonAsync<DivisionMembersDto>(cancellationToken: ct);
        return (raw?.Items ?? new List<DivisionMemberDto>())
            .Select(m => new DivisionMember(m.UserId, m.Name ?? $"UserId {m.UserId}", m.AtcRating ?? "—"))
            .ToList();
    }
}

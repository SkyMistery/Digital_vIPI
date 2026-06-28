using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Hosting;

/// <summary>
/// Adapter d'identità per gli scenari "embedded" (A/B dell'ADR-0002): legge il <c>ClaimsPrincipal</c>
/// già autenticato dal sito ospitante (es. OIDC IVAO) e lo proietta sul modello neutro
/// <see cref="CurrentUser"/>. Nessun nuovo login, nessun nuovo endpoint. La mappa dei claim è
/// configurabile via <see cref="HostIdentityOptions"/> per agganciarsi a host diversi senza ricompilare.
/// </summary>
public sealed class HostIdentityCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _http;
    private readonly HostIdentityOptions _opt;

    public HostIdentityCurrentUserProvider(IHttpContextAccessor http, IOptions<HostIdentityOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public CurrentUser? Get()
    {
        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return null;

        var vidRaw = principal.FindFirst(_opt.UserIdClaim)?.Value
                     ?? principal.FindFirst("sub")?.Value;
        if (!int.TryParse(vidRaw, out var UserId) || UserId <= 0) return null;

        var name = _opt.NameClaims
            .Select(c => principal.FindFirst(c)?.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? $"UserId {UserId}";

        var fir = principal.FindFirst(_opt.FirClaim)?.Value;

        var positions = ExtractStaffPositions(principal.FindAll(_opt.StaffPositionsClaim).Select(c => c.Value));

        return new CurrentUser(UserId, name, string.IsNullOrWhiteSpace(fir) ? null : fir, positions)
        {
            CanEdit = positions.Count > 0,
        };
    }

    /// <summary>
    /// Estrae i codici posizione (es. "IT-DIR") da claim che possono essere: codici semplici ripetuti,
    /// un unico claim con array JSON di stringhe, o un array JSON di oggetti con campo <c>id</c>/<c>connectAs</c>.
    /// </summary>
    private static IReadOnlyCollection<string> ExtractStaffPositions(IEnumerable<string> claimValues)
    {
        var result = new List<string>();
        foreach (var value in claimValues)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            var trimmed = value.TrimStart();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(value);
                    AddFromJson(doc.RootElement, result);
                    continue;
                }
                catch (JsonException) { /* non era JSON: trattalo come codice semplice */ }
            }

            result.Add(value.Trim());
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddFromJson(JsonElement el, List<string> into)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) AddFromJson(item, into);
                break;
            case JsonValueKind.String:
                if (el.GetString() is { Length: > 0 } s) into.Add(s);
                break;
            case JsonValueKind.Object:
                foreach (var key in new[] { "id", "connectAs", "code" })
                    if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String &&
                        p.GetString() is { Length: > 0 } code)
                    {
                        into.Add(code);
                        break;
                    }
                break;
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Plumbing HTTP condiviso dai client IVAO (typed HttpClient). Tiene il token e la base URL; espone GET
/// autorizzati (raw / string / JSON best-effort) e i parser JSON tolleranti. I client per porta lo iniettano
/// via ctor (composizione, non eredità) — doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoHttp
{
    private readonly HttpClient _http;
    private readonly IvaoTokenProvider _token;
    private readonly IvaoOptions _opt;

    public IvaoHttp(HttpClient http, IvaoTokenProvider token, IOptions<IvaoOptions> opt)
    {
        _http = http;
        _token = token;
        _opt = opt.Value;
    }

    /// <summary>Le credenziali IVAO (ClientId/ClientSecret) sono configurate.</summary>
    public bool IsConfigured => _token.IsConfigured;

    /// <summary>Options IVAO (path/scope) per i client.</summary>
    public IvaoOptions Options => _opt;

    public string Combine(string path) => $"{_opt.BaseUrl.TrimEnd('/')}{path}";

    public async Task AuthorizeAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var token = await _token.GetTokenAsync(ct);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>GET autorizzato grezzo: il chiamante ispeziona status/body e dispone la risposta.</summary>
    public async Task<HttpResponseMessage> SendGetAsync(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
        await AuthorizeAsync(req, ct);
        return await _http.SendAsync(req, ct);
    }

    /// <summary>GET autorizzato che ritorna il body come stringa (null su 4xx/5xx). Best-effort.</summary>
    public async Task<string?> GetStringAsync(string path, CancellationToken ct)
    {
        using var res = await SendGetAsync(path, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    /// <summary>GET autorizzato deserializzato (null su 4xx/5xx). Best-effort.</summary>
    public async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var res = await SendGetAsync(path, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    // ---- Parser JSON tolleranti (i campi non noti/assenti diventano null/false). ----

    public static string? JsonStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool JsonBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static double? JsonNum(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>
    /// Legge un id <b>numerico</b>, e solo quello: null se il campo manca, è una stringa, o non entra in un int.
    /// <para>La severità è voluta. Lo stesso nome <c>id</c> porta cose diverse a seconda dell'endpoint: sui
    /// subcenter e sulle postazioni è il numero che identifica la riga (1174, 3954), su <c>/v2/centers</c> è il
    /// codice ACC come stringa ("LIRR"). Un parser tollerante come <see cref="JsonId"/> li appiattirebbe
    /// entrambi in una stringa, e "LIRR" finirebbe a fare da identità dove serve un numero.</para>
    /// </summary>
    public static int? JsonIntId(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i : null;

    // Legge un id come stringa (numero o stringa).
    public static string JsonId(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.TryGetInt64(out var l) ? l.ToString() : v.GetRawText(),
            _ => "",
        };
    }

    // La frequenza può arrivare come MHz (118.7) o, in alcune risposte, come Hz/kHz interi: normalizziamo a "118.700".
    public static string? FormatFrequency(double? f)
    {
        if (f is not double v || v <= 0) return null;
        if (v > 1_000_000) v /= 1_000_000;   // Hz → MHz
        else if (v > 1_000) v /= 1_000;       // kHz → MHz
        return v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }
}

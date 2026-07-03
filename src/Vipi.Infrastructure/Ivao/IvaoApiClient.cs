using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter HTTP verso le API IVAO v2 (typed HttpClient). Legge il riepilogo ATC online e l'elenco
/// membri divisione, normalizzando in modelli dell'Application. PIANO §7.1. Filtro per prefisso ICAO.
/// </summary>
public sealed class IvaoApiClient : IDivisionMembersProvider, IUserDirectory, IAirportDirectory, IAirportDetailProvider, IAccDirectory
{
    private readonly HttpClient _http;
    private readonly IvaoTokenProvider _token;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;
    private readonly IvaoAirportCache _airportCache;

    public IvaoApiClient(HttpClient http, IvaoTokenProvider token, IOptions<IvaoOptions> opt,
        IOptions<DivisionOptions> div, IvaoAirportCache airportCache)
    {
        _http = http;
        _token = token;
        _opt = opt.Value;
        _div = div.Value;
        _airportCache = airportCache;
    }

    /// <summary>ATC della divisione (prefissi ICAO configurati) attualmente online, normalizzati.</summary>
    public async Task<IReadOnlyList<OnlineAtc>> GetOnlineAtcAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(_opt.AtcSummaryPath));
        await AuthorizeAsync(req, ct);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<List<AtcSummaryDto>>(cancellationToken: ct)
                  ?? new List<AtcSummaryDto>();

        return raw
            .Where(a => !string.IsNullOrWhiteSpace(a.Callsign) && MatchesDivision(a.Callsign!))
            .Select(a => new OnlineAtc(
                Callsign: a.Callsign!,
                UserId: a.UserId,
                Name: $"UserId {a.UserId}",
                Rating: a.Rating))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DivisionMember>> GetDivisionControllersAsync(CancellationToken ct = default)
    {
        var path = string.Format(_opt.DivisionMembersPathFormat, _div.Code);
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
        await AuthorizeAsync(req, ct);

        // Senza credenziali non c'è Bearer: l'endpoint membri è protetto → fallisce con 401. Messaggio chiaro.
        if (!_token.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'elenco membri.");

        using var res = await _http.SendAsync(req, ct);
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

    /// <inheritdoc />
    public async Task<SourceUserStaff?> GetUserAsync(int UserId, CancellationToken ct = default)
    {
        if (!_token.IsConfigured) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, Combine($"/v2/users/{UserId}"));
        await AuthorizeAsync(req, ct);

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;   // 404/transitorio: il chiamante non tocca lo stato

        var u = await res.Content.ReadFromJsonAsync<UserDto>(cancellationToken: ct);
        if (u is null) return null;

        var positions = (u.UserStaffPositions ?? new List<StaffPosDto>())
            .Select(p => p.Id)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();

        return new SourceUserStaff(
            UserId: u.Id,
            Nickname: NormalizeNick(u.PublicNickname, u.Id),
            AtcRating: u.Rating?.AtcRating?.ShortName,
            DivisionId: u.DivisionId,
            IsStaff: u.IsStaff,
            StaffPositionCodes: positions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
    {
        if (_airportCache.IsFresh) return _airportCache.Items!;
        if (!_token.IsConfigured)
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
                using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
                await AuthorizeAsync(req, ct);

                using var res = await _http.SendAsync(req, ct);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default)
    {
        if (!_token.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'anagrafica ACC/center.");

        // Parsing tollerante: la risposta può essere paginata ({items,pages}) o un array nudo, e i nomi dei
        // campi variano tra endpoint. Estraiamo via JsonDocument senza vincolarci a un DTO rigido.
        var all = new List<SourceCenter>();
        int rawItems = 0;
        string lastSnippet = "";
        for (int page = 1; ; page++)
        {
            var path = $"{_opt.CentersPath}?page={page}&countryId={Uri.EscapeDataString(_opt.AirportsCountryId)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
            await AuthorizeAsync(req, ct);

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var s = string.IsNullOrWhiteSpace(body) ? "" : $" — {body[..Math.Min(body.Length, 200)]}";
                throw new InvalidOperationException(
                    $"IVAO {(int)res.StatusCode} {res.StatusCode} su {_opt.CentersPath} (scope: {_opt.Scopes}).{s}");
            }
            lastSnippet = body.Length > 300 ? body[..300] : body;

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;

            bool paginated = root.ValueKind == System.Text.Json.JsonValueKind.Object;
            int pages = 1;
            System.Text.Json.JsonElement items;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array) items = root;
            else if (!root.TryGetProperty("items", out items)) root.TryGetProperty("data", out items);
            if (paginated)
            {
                if (root.TryGetProperty("pages", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number) pages = p.GetInt32();
                else if (root.TryGetProperty("totalPages", out var tp) && tp.ValueKind == System.Text.Json.JsonValueKind.Number) pages = tp.GetInt32();
            }

            if (items.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var it in items.EnumerateArray())
                {
                    rawItems++;
                    var callsign = (JsonStr(it, "composePosition") ?? JsonStr(it, "id") ?? "").Trim().ToUpperInvariant();
                    var centerId = (JsonStr(it, "centerId") ?? (callsign.Contains('_') ? callsign.Split('_')[0] : callsign)).Trim().ToUpperInvariant();
                    if (callsign.Length == 0 && centerId.Length > 0) callsign = $"{centerId}_CTR";
                    if (callsign.Length == 0) continue;
                    var name = JsonStr(it, "atcCallsign") ?? JsonStr(it, "name") ?? callsign;
                    var military = JsonBool(it, "military") || JsonBool(it, "isMilitary");
                    all.Add(new SourceCenter(callsign, centerId, name.Trim(), military, FormatFrequency(JsonNum(it, "frequency"))));
                }
            }

            if (!paginated || page >= Math.Max(1, pages)) break;
        }

        if (all.Count == 0)
            throw new InvalidOperationException(
                $"/v2/centers: nessun ACC riconosciuto (elementi grezzi letti: {rawItems}). " +
                $"Verifica endpoint/scope o segnala lo schema. Risposta: {lastSnippet}");

        return all.OrderBy(c => c.Callsign, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default)
    {
        if (!_token.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere i subcenter.");

        accIcao = (accIcao ?? "").Trim().ToUpperInvariant();
        if (accIcao.Length == 0) return Array.Empty<SourceSubcenter>();

        // 1) Lista subcenter dell'ACC: composePosition, centerId, position, middleIdentifier.
        var listPath = string.Format(_opt.SubcentersPathFormat, Uri.EscapeDataString(accIcao));
        var listBody = await GetStringAsync(listPath, ct);
        if (listBody is null) return Array.Empty<SourceSubcenter>();

        var basics = new List<(string Compose, string Center, string? Pos, string? Mid, string? Name)>();
        using (var doc = System.Text.Json.JsonDocument.Parse(listBody))
        {
            var root = doc.RootElement;
            var items = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root
                : (root.TryGetProperty("items", out var it) ? it
                    : (root.TryGetProperty("data", out var dt) ? dt : default));
            if (items.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var s in items.EnumerateArray())
                {
                    var compose = (JsonStr(s, "composePosition") ?? JsonStr(s, "id") ?? "").Trim().ToUpperInvariant();
                    if (compose.Length == 0) continue;
                    var center = (JsonStr(s, "centerId") ?? accIcao).Trim().ToUpperInvariant();
                    basics.Add((compose, center, JsonStr(s, "position"), JsonStr(s, "middleIdentifier"), JsonStr(s, "atcCallsign")));
                }
        }

        // 2) Dettaglio per ogni subcenter: frequency + regionMapPolygon (best-effort).
        var result = new List<SourceSubcenter>(basics.Count);
        foreach (var b in basics)
        {
            string? freq = null, polygon = null;
            var detailBody = await GetStringAsync(string.Format(_opt.SubcenterDetailPathFormat, Uri.EscapeDataString(b.Compose)), ct);
            if (detailBody is not null)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(detailBody);
                var d = doc.RootElement;
                freq = FormatFrequency(JsonNum(d, "frequency"));
                if (d.TryGetProperty("regionMapPolygon", out var poly) && poly.ValueKind != System.Text.Json.JsonValueKind.Null
                    && poly.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    polygon = poly.GetRawText();
            }
            result.Add(new SourceSubcenter(b.Compose, b.Center, b.Pos, b.Mid, freq, polygon, b.Name));
        }

        return result;
    }

    public async Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, CancellationToken ct = default)
    {
        if (!_token.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere le aree speciali.");

        accIcao = (accIcao ?? "").Trim().ToUpperInvariant();
        if (accIcao.Length == 0) return Array.Empty<SourceSpecialArea>();

        // 1) Elenco paginato: id + campi anagrafici (best-effort, schema tollerante come i centers).
        var basics = new List<(string Id, string? Type, string Name, string? Desc, string? Act, int? Min, int? Max, bool Range)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listBase = string.Format(_opt.SpecialAreasPathFormat, Uri.EscapeDataString(accIcao));
        var page = 1;
        var maxPages = 1;
        do
        {
            var body = await GetStringAsync($"{listBase}?page={page}", ct);
            if (body is null)
            {
                // Prima pagina non risponde = fetch fallita (non "nessuna area"): segnala, così il prune non cancella per errore.
                if (page == 1) throw new HttpRequestException($"specialAreas: nessuna risposta per {accIcao} (pagina 1).");
                break;   // pagine successive: usa quanto raccolto
            }

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("pages", out var pg) && pg.ValueKind == System.Text.Json.JsonValueKind.Number)
                maxPages = Math.Max(maxPages, pg.GetInt32());
            else if (root.TryGetProperty("totalPages", out var tp) && tp.ValueKind == System.Text.Json.JsonValueKind.Number)
                maxPages = Math.Max(maxPages, tp.GetInt32());

            var items = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root
                : (root.TryGetProperty("items", out var it) ? it
                    : (root.TryGetProperty("data", out var dt) ? dt : default));
            if (items.ValueKind != System.Text.Json.JsonValueKind.Array) break;

            var any = false;
            foreach (var s in items.EnumerateArray())
            {
                any = true;
                var id = JsonId(s, "id");
                if (id.Length == 0 || !seen.Add(id)) continue;
                var name = JsonStr(s, "name") ?? id;
                basics.Add((id, JsonStr(s, "type"), name, JsonStr(s, "description"), JsonStr(s, "activationDetails"),
                    JsonNum(s, "minimumAlt") is double mn ? (int)Math.Round(mn) : null,
                    JsonNum(s, "maximumAlt") is double mx ? (int)Math.Round(mx) : null,
                    JsonBool(s, "range")));
            }
            if (!any) break;
            page++;
        } while (page <= maxPages && page <= 50);

        // 2) Dettaglio per id: shape (regionMapPolygon grezzo, best-effort).
        var result = new List<SourceSpecialArea>(basics.Count);
        foreach (var b in basics)
        {
            string? polygon = null;
            var detailBody = await GetStringAsync(string.Format(_opt.SpecialAreaDetailPathFormat, Uri.EscapeDataString(b.Id)), ct);
            if (detailBody is not null)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(detailBody);
                var d = doc.RootElement;
                if (d.TryGetProperty("regionMapPolygon", out var poly) && poly.ValueKind != System.Text.Json.JsonValueKind.Null
                    && poly.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    polygon = poly.GetRawText();
                else if (d.TryGetProperty("regionMap", out var rm) && rm.ValueKind != System.Text.Json.JsonValueKind.Null
                    && rm.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    polygon = rm.GetRawText();
            }
            result.Add(new SourceSpecialArea(b.Id, b.Type, b.Name, b.Desc, b.Act, b.Min, b.Max, b.Range, accIcao, polygon));
        }

        return result;
    }

    // Legge un id come stringa (numero o stringa).
    private static string JsonId(System.Text.Json.JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return "";
        return v.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => v.GetString() ?? "",
            System.Text.Json.JsonValueKind.Number => v.TryGetInt64(out var l) ? l.ToString() : v.GetRawText(),
            _ => "",
        };
    }

    // GET autorizzato che ritorna il body come stringa (null su 4xx/5xx). Best-effort per i dati di riferimento.
    private async Task<string?> GetStringAsync(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
        await AuthorizeAsync(req, ct);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    private static string? JsonStr(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
    private static bool JsonBool(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True;
    private static double? JsonNum(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetDouble() : null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var raw = await GetJsonAsync<List<AtcPositionDto>>($"/v2/airports/{Uri.EscapeDataString(icao)}/ATCPositions", ct)
                  ?? new List<AtcPositionDto>();
        return raw
            .Select(p => new SourceAtcPosition(
                Callsign: (p.ComposePosition ?? "").Trim().ToUpperInvariant(),   // es. "LIRN_GND" (NON atcCallsign, che è il nome)
                Frequency: FormatFrequency(p.Frequency),
                Position: p.Position,
                MiddleIdentifier: p.MiddleIdentifier,
                AtcCallsign: p.AtcCallsign))
            .Where(p => p.Callsign.Length > 0)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default)
    {
        var compose = (composePosition ?? "").Trim().ToUpperInvariant();
        if (compose.Length == 0) return null;

        // Dettaglio per posizione: frequency + regionMapPolygon (+ position/middleIdentifier/limiti se esposti).
        var body = await GetStringAsync(string.Format(_opt.AtcPositionDetailPathFormat, Uri.EscapeDataString(compose)), ct);
        if (body is null) return null;

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var d = doc.RootElement;
        string? polygon = null;
        if (d.TryGetProperty("regionMapPolygon", out var poly) && poly.ValueKind != System.Text.Json.JsonValueKind.Null
            && poly.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            polygon = poly.GetRawText();

        // Coordinate del riferimento aeroporto dal blocco "airport" (presente su ogni postazione dell'aeroporto).
        double? airLat = null, airLon = null;
        if (d.TryGetProperty("airport", out var ap) && ap.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            airLat = JsonNum(ap, "latitude");
            airLon = JsonNum(ap, "longitude");
        }

        return new SourceAtcPosition(
            Callsign: compose,
            Frequency: FormatFrequency(JsonNum(d, "frequency")),
            Position: JsonStr(d, "position"),
            MiddleIdentifier: JsonStr(d, "middleIdentifier"),
            RegionMapPolygon: polygon,
            LowerLimit: JsonNum(d, "lowerLimit") is double lo ? (int)Math.Round(lo) : null,
            UpperLimit: JsonNum(d, "upperLimit") is double up ? (int)Math.Round(up) : null,
            AirportLatitude: airLat,
            AirportLongitude: airLon);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var raw = await GetJsonAsync<List<RunwayDto>>($"/v2/airports/{Uri.EscapeDataString(icao)}/runways", ct)
                  ?? new List<RunwayDto>();
        return raw
            .Select(r => new SourceRunway(
                Ident: StripRunwayPrefix(r.Runway),
                LengthM: r.Length is double l and > 0 ? (int)Math.Round(l * 0.3048) : null,   // length in piedi → metri
                Bearing: r.Bearing is double b ? (int)Math.Round(b) : null))
            .Where(r => r.Ident.Length > 0)
            .ToList();
    }

    // GET autorizzato che ritorna null su 4xx/5xx (i dettagli per-aeroporto sono best-effort: in errore la
    // generazione del documento prosegue con la sezione vuota da completare a mano).
    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
        await AuthorizeAsync(req, ct);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    // "RW06" → "06"; "RW24L" → "24L". Toglie il prefisso RW dell'API runways.
    private static string StripRunwayPrefix(string? runway)
    {
        var r = (runway ?? "").Trim().ToUpperInvariant();
        return r.StartsWith("RW") ? r[2..] : r;
    }

    // La frequenza può arrivare come MHz (118.7) o, in alcune risposte, come Hz/kHz interi: normalizziamo a "118.700".
    private static string? FormatFrequency(double? f)
    {
        if (f is not double v || v <= 0) return null;
        if (v > 1_000_000) v /= 1_000_000;   // Hz → MHz
        else if (v > 1_000) v /= 1_000;       // kHz → MHz
        return v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }

    // L'API restituisce un nickname placeholder "User {UserId}" quando l'utente non ne ha impostato uno.
    private static string? NormalizeNick(string? nick, int UserId) =>
        string.IsNullOrWhiteSpace(nick) || nick.Trim().Equals($"User {UserId}", StringComparison.OrdinalIgnoreCase)
            ? null : nick.Trim();

    private bool MatchesDivision(string callsign) =>
        _div.IcaoPrefixes.Any(p => callsign.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private string Combine(string path) => $"{_opt.BaseUrl.TrimEnd('/')}{path}";

    private async Task AuthorizeAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var token = await _token.GetTokenAsync(ct);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // DTO permissivi: i campi non noti vengono ignorati.
    private sealed record AtcSummaryDto(
        [property: JsonPropertyName("callsign")] string? Callsign,
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("rating")] int Rating);

    private sealed record DivisionMembersDto(
        [property: JsonPropertyName("items")] List<DivisionMemberDto>? Items);

    private sealed record DivisionMemberDto(
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("firstName")] string? Name,
        [property: JsonPropertyName("atcRating")] string? AtcRating);

    // /v2/users/{UserId}: solo i campi utili al roster staff. Gli altri vengono ignorati.
    private sealed record UserDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("divisionId")] string? DivisionId,
        [property: JsonPropertyName("isStaff")] bool IsStaff,
        [property: JsonPropertyName("publicNickname")] string? PublicNickname,
        [property: JsonPropertyName("rating")] RatingDto? Rating,
        [property: JsonPropertyName("userStaffPositions")] List<StaffPosDto>? UserStaffPositions);

    private sealed record RatingDto(
        [property: JsonPropertyName("atcRating")] AtcRatingDto? AtcRating);

    private sealed record AtcRatingDto(
        [property: JsonPropertyName("shortName")] string? ShortName);

    private sealed record StaffPosDto(
        [property: JsonPropertyName("id")] string? Id);

    // /v2/airports?countryId=…: pagina + solo i campi utili all'editor. centerId = ACC di competenza.
    private sealed record AirportsPageDto(
        [property: JsonPropertyName("items")] List<AirportDto>? Items,
        [property: JsonPropertyName("pages")] int Pages);

    private sealed record AirportDto(
        [property: JsonPropertyName("icao")] string? Icao,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("centerId")] string? CenterId,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("transitionAltitude")] int? TransitionAltitude,
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude);

    // /v2/airports/{icao}/ATCPositions — es. { composePosition:"LIRN_GND", position:"GND", frequency:121.9 }.
    private sealed record AtcPositionDto(
        [property: JsonPropertyName("composePosition")] string? ComposePosition,
        [property: JsonPropertyName("position")] string? Position,
        [property: JsonPropertyName("middleIdentifier")] string? MiddleIdentifier,
        [property: JsonPropertyName("atcCallsign")] string? AtcCallsign,
        [property: JsonPropertyName("frequency")] double? Frequency);

    // /v2/airports/{icao}/runways — es. { runway:"RW06", length:8622 (piedi), width:45, bearing:56 }.
    private sealed record RunwayDto(
        [property: JsonPropertyName("runway")] string? Runway,
        [property: JsonPropertyName("length")] double? Length,
        [property: JsonPropertyName("width")] double? Width,
        [property: JsonPropertyName("bearing")] double? Bearing);
}

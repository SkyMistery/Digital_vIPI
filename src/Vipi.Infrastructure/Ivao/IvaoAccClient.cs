using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using static Vipi.Infrastructure.Ivao.IvaoHttp;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per l'anagrafica ACC/center: centers, subcenter e aree speciali. Parsing tollerante
/// (schema variabile). Implementa la porta <see cref="IAccDirectory"/>. Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoAccClient : IAccDirectory
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;

    public IvaoAccClient(IvaoHttp http, IOptions<IvaoOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) =>
        GetCentersByCountryAsync(_opt.AirportsCountryId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default)
    {
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere l'anagrafica ACC/center.");

        countryId = (countryId ?? "").Trim();
        if (countryId.Length == 0) return Array.Empty<SourceCenter>();

        // Parsing tollerante: la risposta può essere paginata ({items,pages}) o un array nudo, e i nomi dei
        // campi variano tra endpoint. Estraiamo via JsonDocument senza vincolarci a un DTO rigido.
        var all = new List<SourceCenter>();
        int rawItems = 0;
        string lastSnippet = "";
        for (int page = 1; ; page++)
        {
            var path = $"{_opt.CentersPath}?page={page}&countryId={Uri.EscapeDataString(countryId)}";
            using var res = await _http.SendGetAsync(path, ct);
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
        if (!_http.IsConfigured)
            throw new InvalidOperationException(
                "Credenziali IVAO non configurate (Ivao:ClientId/ClientSecret): impossibile leggere i subcenter.");

        accIcao = (accIcao ?? "").Trim().ToUpperInvariant();
        if (accIcao.Length == 0) return Array.Empty<SourceSubcenter>();

        // 1) Lista subcenter dell'ACC: composePosition, centerId, position, middleIdentifier.
        var listPath = string.Format(_opt.SubcentersPathFormat, Uri.EscapeDataString(accIcao));
        var listBody = await _http.GetStringAsync(listPath, ct);
        if (listBody is null) return Array.Empty<SourceSubcenter>();

        var basics = new List<(string Compose, string Center, string? Pos, string? Mid, string? Name, int? IvaoId)>();
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
                    // ⚠️ `id` è NUMERICO sui subcenter (es. 1174) ed è l'identità della riga; su /v2/centers è invece
                    // una STRINGA (il codice ACC, "LIRR"). JsonIntId legge solo i numeri, quindi il fallback del
                    // callsign su `id` — che serve ai center — non può inquinare l'identità qui.
                    var compose = (JsonStr(s, "composePosition") ?? JsonStr(s, "id") ?? "").Trim().ToUpperInvariant();
                    if (compose.Length == 0) continue;
                    var center = (JsonStr(s, "centerId") ?? accIcao).Trim().ToUpperInvariant();
                    basics.Add((compose, center, JsonStr(s, "position"), JsonStr(s, "middleIdentifier"),
                        JsonStr(s, "atcCallsign"), JsonIntId(s, "id")));
                }
        }

        // 2) Dettaglio per ogni subcenter: frequency + regionMapPolygon (best-effort).
        var result = new List<SourceSubcenter>(basics.Count);
        foreach (var b in basics)
        {
            string? freq = null, polygon = null;
            var ivaoId = b.IvaoId;
            var detailBody = await _http.GetStringAsync(string.Format(_opt.SubcenterDetailPathFormat, Uri.EscapeDataString(b.Compose)), ct);
            if (detailBody is not null)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(detailBody);
                var d = doc.RootElement;
                freq = FormatFrequency(JsonNum(d, "frequency"));
                if (d.TryGetProperty("regionMapPolygon", out var poly) && poly.ValueKind != System.Text.Json.JsonValueKind.Null
                    && poly.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    polygon = poly.GetRawText();
                ivaoId ??= JsonIntId(d, "id");   // il dettaglio lo ripete: rete di sicurezza se la lista non l'avesse
            }
            result.Add(new SourceSubcenter(b.Compose, b.Center, b.Pos, b.Mid, freq, polygon, b.Name, IvaoId: ivaoId));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(
        string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default)
    {
        if (!_http.IsConfigured)
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
            var body = await _http.GetStringAsync($"{listBase}?page={page}", ct);
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

        // 2) Dettaglio per id: shape (regionMapPolygon grezzo, best-effort). Saltato per le aree la cui shape è già
        //    in archivio e fresca: polygon resta null e l'upsert preserva quella salvata.
        var result = new List<SourceSpecialArea>(basics.Count);
        foreach (var b in basics)
        {
            string? polygon = null;
            var detailBody = skipDetailIds.Contains(b.Id)
                ? null
                : await _http.GetStringAsync(string.Format(_opt.SpecialAreaDetailPathFormat, Uri.EscapeDataString(b.Id)), ct);
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
}

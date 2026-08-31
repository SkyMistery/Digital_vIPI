using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Chiede a IVAO, in puntuale e adesso, se un elemento c'è ancora. Implementa
/// <see cref="ISourcePresenceProbe"/>.
///
/// <para><b>Perché non riusa i client anagrafici.</b> <c>IvaoAccClient</c>, <c>IvaoAirportClient</c> e
/// <c>IvaoAirportDetailClient</c> passano da <c>IvaoHttp.GetJsonAsync</c>/<c>GetStringAsync</c>, che
/// ritornano <c>null</c> per <b>ogni</b> risposta non-2xx: un 404 e un 401 arrivano identici. Qui si guarda
/// lo <b>status</b>, perché è l'unica differenza che conta. In più <c>IvaoAirportClient.GetByIcaoAsync</c>
/// risponde dalla cache di processo prima di uscire in rete — e una verifica che risponde dalla memoria di
/// stamattina non è una verifica.</para>
///
/// <para><b>Le due chiamate.</b> Ogni verdetto <see cref="SourcePresence.Assente"/> poggia su due risposte:
/// la <b>puntuale</b> (il dettaglio dell'elemento) e la <b>controprova</b> (l'elenco che lo conterrebbe).
/// La controprova deve rispondere <c>200</c> e nominare <b>qualcosa</b>: un elenco vuoto è esattamente
/// l'ambiguità per cui esiste la regola dei due giri («una risposta a zero elementi non è un errore»), e
/// crederle qui rifarebbe più in fretta lo stesso errore. Dove l'elenco contiene anche la risposta diretta —
/// i subcenter di un ACC, le postazioni di un aeroporto — la controprova <b>è</b> la prova: la sorgente dice
/// «ecco i sette che ho, e questo non c'è». Dove invece l'elenco è paginato e non lo si può scorrere tutto
/// (gli aeroporti di un paese), la controprova serve solo a dimostrare che la sorgente <i>sta rispondendo</i>
/// e che il token ha lo scope: allora la prova resta il 404, ma un 404 di cui si sa che è un «non l'ho»
/// e non un «sono rotto».</para>
///
/// <para>Carta: <c>docs/feature/2026-08-26-chiedere-alla-sorgente.md</c>.</para>
/// </summary>
public sealed class IvaoSourcePresenceProbe : ISourcePresenceProbe
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly IAccDirectory _acc;

    public IvaoSourcePresenceProbe(IvaoHttp http, IOptions<IvaoOptions> opt, IAccDirectory acc)
    {
        _http = http;
        _opt = opt.Value;
        _acc = acc;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ChiediAsync(SourceProbeTarget b, CancellationToken ct = default)
    {
        var chiave = (b.Key ?? "").Trim().ToUpperInvariant();
        if (chiave.Length == 0)
            return SourceProbeResult.NonSiSa(Lingua("non c'è niente da chiedere: chiave vuota", "there is nothing to ask about: empty key"));

        if (!_http.IsConfigured)
            return SourceProbeResult.NonSiSa(
                Lingua("credenziali IVAO non configurate: la sorgente non si può interrogare",
                       "IVAO credentials are not configured: the source cannot be queried"),
                "nessuna chiamata: Ivao:ClientId assente");

        try
        {
            return b.Kind switch
            {
                SourceProbeKind.AccSector => await SettoreAccAsync(chiave, b.Owner, ct),
                SourceProbeKind.AirportSector => await SettoreAeroportoAsync(chiave, b.Owner, ct),
                SourceProbeKind.Airport => await AeroportoAsync(chiave, ct),
                SourceProbeKind.Acc => await EnteAsync(chiave, ct),
                _ => SourceProbeResult.NonSiSa(Lingua($"non so come chiedere di un {b.Kind}", $"I do not know how to ask about a {b.Kind}")),
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Una rete che cade, un timeout, un JSON che non si apre: è tutto «non si sa». La porta promette
            // di non lanciare, perché chi chiama sta già mostrando una finestra — e un'eccezione lì sarebbe
            // un messaggio d'errore al posto di un verdetto.
            return SourceProbeResult.NonSiSa(
                Lingua($"la sorgente non ha risposto: {ex.Message}", $"the source did not answer: {ex.Message}"),
                $"eccezione: {ex.GetType().Name}");
        }
    }

    // ── I quattro modi di chiedere ───────────────────────────────────────────────────────────────────

    /// <summary>Un subcenter: <c>/v2/subcenters/{callsign}</c>, con i subcenter dell'ACC come controprova.</summary>
    private async Task<SourceProbeResult> SettoreAccAsync(string callsign, string? acc, CancellationToken ct)
    {
        var dettaglio = string.Format(_opt.SubcenterDetailPathFormat, Uri.EscapeDataString(callsign));
        var esito = await ChiamaAsync(dettaglio, ct);
        if (esito.Trovato) return Presente(callsign, esito);
        if (!esito.NonTrovato) return Guasto(callsign, esito);

        if (string.IsNullOrWhiteSpace(acc))
            return SourceProbeResult.NonSiSa(
                Lingua($"{callsign} non risulta, ma non so di quale ACC chiedere conferma",
                       $"{callsign} is not there, but I do not know which ACC to ask for confirmation"),
                esito.Traccia);

        var elenco = string.Format(_opt.SubcentersPathFormat, Uri.EscapeDataString(acc.ToUpperInvariant()));
        return await ControprovaDiElencoAsync(callsign, elenco, acc.ToUpperInvariant(), "settori", esito, ct);
    }

    /// <summary>Una postazione d'aeroporto: <c>/v2/ATCPositions/{callsign}</c>, con le postazioni dello scalo
    /// come controprova.</summary>
    private async Task<SourceProbeResult> SettoreAeroportoAsync(string callsign, string? icao, CancellationToken ct)
    {
        var dettaglio = string.Format(_opt.AtcPositionDetailPathFormat, Uri.EscapeDataString(callsign));
        var esito = await ChiamaAsync(dettaglio, ct);
        if (esito.Trovato) return Presente(callsign, esito);
        if (!esito.NonTrovato) return Guasto(callsign, esito);

        if (string.IsNullOrWhiteSpace(icao))
            return SourceProbeResult.NonSiSa(
                Lingua($"{callsign} non risulta, ma non so di quale aeroporto chiedere conferma",
                       $"{callsign} is not there, but I do not know which airport to ask for confirmation"),
                esito.Traccia);

        var elenco = $"{_opt.AirportsPath}/{Uri.EscapeDataString(icao.ToUpperInvariant())}/ATCPositions";
        return await ControprovaDiElencoAsync(callsign, elenco, icao.ToUpperInvariant(), "postazioni", esito, ct);
    }

    /// <summary>
    /// Un aeroporto: <c>/v2/airports/{ICAO}</c>, con la prima pagina degli aeroporti del paese come
    /// controprova.
    ///
    /// <para>⚠️ Qui la controprova è solo di <b>vitalità</b>, non di appartenenza: l'anagrafica è paginata e
    /// l'ICAO cercato potrebbe stare a pagina tre. Serve a stabilire che l'endpoint risponde e che il token
    /// ha lo scope — cioè che il 404 di prima è «questo aeroporto non ce l'ho» e non «non ti conosco».</para>
    /// </summary>
    private async Task<SourceProbeResult> AeroportoAsync(string icao, CancellationToken ct)
    {
        var esito = await ChiamaAsync($"{_opt.AirportsPath}/{Uri.EscapeDataString(icao)}", ct);
        if (esito.Trovato) return Presente(icao, esito);
        if (!esito.NonTrovato) return Guasto(icao, esito);

        var controllo = $"{_opt.AirportsPath}?page=1&countryId={Uri.EscapeDataString(_opt.AirportsCountryId)}";
        var vivo = await ChiamaAsync(controllo, ct);
        var traccia = $"{esito.Traccia}; {vivo.Traccia}";

        if (!vivo.Trovato)
            return SourceProbeResult.NonSiSa(
                Lingua($"{icao} non risulta, ma nemmeno l'anagrafica risponde: non si può concludere niente",
                       $"{icao} is not there, but the directory does not answer either: nothing can be concluded"),
                traccia);

        if (vivo.Elementi == 0)
            return SourceProbeResult.NonSiSa(
                Lingua($"{icao} non risulta, ma l'anagrafica ha risposto vuota: è la risposta ambigua che la regola dei due giri esiste per non credere",
                       $"{icao} is not there, but the directory answered empty: this is the ambiguous answer the two-pass rule exists not to believe"),
                traccia);

        return SourceProbeResult.Assente(
            Lingua($"{icao} non c'è più: la sorgente lo dà per introvabile e l'anagrafica del paese risponde regolarmente ({vivo.Elementi} aeroporti nella prima pagina)",
                   $"{icao} is gone: the source reports it as not found and the country directory answers normally ({vivo.Elementi} airports on the first page)"),
            traccia);
    }

    /// <summary>
    /// Un ente ACC. Non ha un dettaglio per codice: l'elenco dei center del paese è insieme la domanda e la
    /// controprova, e <c>IAccDirectory</c> lo scorre già tutto — lanciando su errore e su elenco vuoto, che
    /// è esattamente la distinzione che serve qui.
    /// </summary>
    private async Task<SourceProbeResult> EnteAsync(string code, CancellationToken ct)
    {
        IReadOnlyList<SourceCenter> centers;
        try
        {
            centers = await _acc.GetCentersByCountryAsync(_opt.AirportsCountryId, ct);
        }
        catch (Exception ex)
        {
            return SourceProbeResult.NonSiSa(
                Lingua($"l'anagrafica degli enti non ha risposto: {ex.Message}",
                       $"the units directory did not answer: {ex.Message}"),
                $"GET {_opt.CentersPath}?countryId={_opt.AirportsCountryId} → {ex.GetType().Name}");
        }

        var traccia = $"GET {_opt.CentersPath}?countryId={_opt.AirportsCountryId} → 200, {centers.Count} center";
        return centers.Any(c => string.Equals(c.CenterId, code, StringComparison.OrdinalIgnoreCase))
            ? SourceProbeResult.Presente(
                Lingua($"{code} c'è ancora: la sorgente lo elenca fra i center del paese",
                       $"{code} is still there: the source lists it among the country's centers"),
                traccia)
            : SourceProbeResult.Assente(
                Lingua($"{code} non c'è più: la sorgente elenca {centers.Count} center del paese e questo non è fra loro",
                       $"{code} is gone: the source lists {centers.Count} centers for the country and this is not one of them"),
                traccia);
    }

    // ── La controprova, e le tre frasi ───────────────────────────────────────────────────────────────

    /// <summary>
    /// L'elenco del contenitore, che qui è <b>anche</b> la prova diretta: se risponde e nomina altri, il
    /// «non c'è» è una constatazione. Se risponde vuoto è l'ambiguità dei due giri; se non risponde, niente.
    ///
    /// <para>⚠️ Se l'elenco lo nomina mentre il dettaglio ha risposto 404, vince l'elenco e il verdetto è
    /// <b>presente</b>: due risposte in disaccordo non sono una prova d'assenza, e davanti a un dubbio non si
    /// cancella.</para>
    /// </summary>
    private async Task<SourceProbeResult> ControprovaDiElencoAsync(string callsign, string path, string owner,
        string cosaElenca, Esito puntuale, CancellationToken ct)
    {
        var elenco = await ChiamaAsync(path, ct, callsign);
        var traccia = $"{puntuale.Traccia}; {elenco.Traccia}";

        if (!elenco.Trovato)
            return SourceProbeResult.NonSiSa(
                Lingua($"{callsign} non risulta, ma nemmeno l'elenco di {owner} risponde: non si può concludere niente",
                       $"{callsign} is not there, but the list of {owner} does not answer either: nothing can be concluded"),
                traccia);

        if (elenco.Contiene)
            return SourceProbeResult.Presente(
                Lingua($"{callsign} c'è ancora: l'elenco di {owner} lo nomina (anche se il dettaglio non l'ha trovato)",
                       $"{callsign} is still there: the list of {owner} names it (even though the detail call did not find it)"),
                traccia);

        if (elenco.Elementi == 0)
            return SourceProbeResult.NonSiSa(
                Lingua($"{callsign} non risulta, ma l'elenco di {owner} è vuoto: è la risposta ambigua che la regola dei due giri esiste per non credere",
                       $"{callsign} is not there, but the list of {owner} is empty: this is the ambiguous answer the two-pass rule exists not to believe"),
                traccia);

        return SourceProbeResult.Assente(
            Lingua($"{callsign} non c'è più: {owner} ne elenca {elenco.Elementi} e questo non è fra loro",
                   $"{callsign} is gone: {owner} lists {elenco.Elementi} of them and this is not one of them"),
            traccia);
    }

    private static SourceProbeResult Presente(string chiave, Esito e) =>
        SourceProbeResult.Presente(
            Lingua($"{chiave} c'è ancora: la sorgente lo manda", $"{chiave} is still there: the source sends it"),
            e.Traccia);

    private static SourceProbeResult Guasto(string chiave, Esito e) =>
        SourceProbeResult.NonSiSa(
            Lingua($"non si sa: la sorgente ha risposto {(int)e.Status} {e.Status} — non è «non c'è», è «non lo dice»",
                   $"unknown: the source answered {(int)e.Status} {e.Status} — that is not «it is gone», it is «it does not say»"),
            e.Traccia);

    // ── La chiamata, con lo status davanti ───────────────────────────────────────────────────────────

    /// <param name="Elementi">Quanti elementi porta la risposta: −1 se non è un elenco.</param>
    /// <param name="Contiene">L'elenco nomina il callsign cercato.</param>
    private readonly record struct Esito(HttpStatusCode Status, int Elementi, bool Contiene, string Traccia)
    {
        public bool Trovato => (int)Status is >= 200 and < 300;
        public bool NonTrovato => Status == HttpStatusCode.NotFound;
    }

    /// <summary>
    /// Un GET autorizzato di cui si tiene lo <b>status</b>, e — se il corpo è un elenco — quanti elementi
    /// porta e se nomina <paramref name="cerca"/>. È il pezzo che i client anagrafici non hanno.
    /// </summary>
    private async Task<Esito> ChiamaAsync(string path, CancellationToken ct, string? cerca = null)
    {
        using var res = await _http.SendGetAsync(path, ct);
        if (!res.IsSuccessStatusCode)
            return new Esito(res.StatusCode, -1, false, $"GET {path} → {(int)res.StatusCode}");

        var body = await res.Content.ReadAsStringAsync(ct);
        var (elementi, contiene) = LeggiElenco(body, cerca);
        var quanti = elementi >= 0 ? $", {elementi} elementi" : "";
        return new Esito(res.StatusCode, elementi, contiene, $"GET {path} → 200{quanti}");
    }

    /// <summary>
    /// Conta gli elementi di una risposta e cerca un callsign fra loro. Parsing tollerante come il resto
    /// degli adapter: la risposta può essere un array nudo o un involucro <c>{items}</c>/<c>{data}</c>.
    ///
    /// <para>⚠️ La chiave cercata è confrontata su <c>composePosition</c> <b>o</b> <c>id</c>, gli stessi due
    /// campi che legge <c>IvaoAccClient</c>: se l'elenco cambiasse nome al campo, questo tornerebbe «non lo
    /// nomina» e autorizzerebbe una cancellazione. Per questo il conteggio è separato dalla ricerca — un
    /// elenco che ha elementi ma di cui non si riconosce nemmeno uno è trattato come vuoto, cioè ambiguo.</para>
    /// </summary>
    private static (int Elementi, bool Contiene) LeggiElenco(string body, string? cerca)
    {
        if (string.IsNullOrWhiteSpace(body)) return (-1, false);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { return (-1, false); }

        using (doc)
        {
            var root = doc.RootElement;
            JsonElement items;
            if (root.ValueKind == JsonValueKind.Array) items = root;
            else if (root.ValueKind != JsonValueKind.Object) return (-1, false);
            else if (!root.TryGetProperty("items", out items) && !root.TryGetProperty("data", out items))
                return (-1, false);   // un oggetto che non è un elenco: è il dettaglio di un elemento

            if (items.ValueKind != JsonValueKind.Array) return (-1, false);

            int riconosciuti = 0;
            bool contiene = false;
            foreach (var it in items.EnumerateArray())
            {
                if (it.ValueKind != JsonValueKind.Object) continue;
                var cs = (IvaoHttp.JsonStr(it, "composePosition") ?? IvaoHttp.JsonStr(it, "id") ?? "").Trim();
                if (cs.Length == 0) continue;
                riconosciuti++;
                if (cerca is not null && string.Equals(cs, cerca, StringComparison.OrdinalIgnoreCase))
                    contiene = true;
            }

            // Per gli elenchi in cui non si cerca nessuno (la vitalità dell'anagrafica aeroporti) basta che
            // ci sia qualcosa dentro: lì i campi si chiamano altrimenti e riconoscerli non serve.
            if (riconosciuti == 0 && cerca is null)
                return (items.GetArrayLength(), false);

            return (riconosciuti, contiene);
        }
    }
}

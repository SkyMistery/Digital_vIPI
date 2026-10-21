using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Hosting;

/// <summary>
/// Le API dei settori, per gli altri programmi della divisione (S119, 21 ottobre 2026, committente): la gerarchia dei
/// settori di un centro e i coordinamenti fra settori. Carta <c>docs/feature/2026-10-21-api-settori.md</c>.
///
/// <list type="bullet">
/// <item><c>GET /vsop/api/v1/accs</c>: i centri;</item>
/// <item><c>GET /vsop/api/v1/accs/{acc}/sectors</c>: i settori col padre, la banda e la catena di ripiego;</item>
/// <item><c>GET /vsop/api/v1/accs/{acc}/coordinations[?online=A,B]</c>: le righe degli accordi come sono scritte, o
/// risolte su chi è online.</item>
/// </list>
///
/// <para>🔴 <b>È la STRUTTURA VIVA, non i documenti pubblicati</b> (committente, 21 ottobre 2026): quello che leggono
/// la pagina Struttura, Trasferimenti e il banco di prova, aggiornato al primo salvataggio. Al contrario dell'API degli
/// aeroporti, che legge la vista pubblica del documento.</para>
///
/// <para>⚠️ Con <c>?online=</c> gli enti online sono <b>solo quelli dati</b>, come nella vista live: tutti gli altri
/// sono chiusi. Non come nel banco di prova, dove chi sta fuori dallo scenario conta aperto — un programma che chiede
/// «chi passa a chi» con l'elenco di chi è connesso intende quello e basta.</para>
///
/// <para>⚠️ I servizi usati sono quelli che non chiedono permessi (<see cref="IAgreementService.ListFlowsByAccAsync"/>,
/// <see cref="IAgreementService.ResolveForAccAsync"/>, <see cref="ITopologyProvider"/>): il banco di prova
/// (<c>ICoverageBenchService</c>) vuole un Editor, e qui chi chiama ha una chiave, non un ruolo.</para>
/// </summary>
public static class ApiSettori
{
    public const string Radice = ApiRotte.Acc;

    /// <summary>Tetti per chiave, come gli aeroporti: una richiesta di coordinamenti risolti costruisce la topologia
    /// di tutta la divisione, e chi integra la chiede quando cambiano gli online, non a ogni secondo.</summary>
    private const int RichiesteAlMinutoPerChiave = 60;
    private const int RichiesteAlMinutoTotali = 600;
    private const int ChiaviTracciate = 5000;

    /// <summary>Quanti enti online si accettano in una domanda: più di quelli che la divisione abbia mai avuto.</summary>
    public const int OnlineMassimi = 200;

    public static IEndpointRouteBuilder MapApiSettori(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Radice, async (HttpContext ctx, ISettoriPerApi settori, RequestRateLimiter limiter, CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;

            var centri = await settori.CentriAsync(ct);
            return Results.Json(Proiezione.Centri(centri));
        });

        endpoints.MapGet(Radice + "/{acc}/sectors", async (
            string acc, HttpContext ctx, ISettoriPerApi settori, ITopologyProvider topologia,
            RequestRateLimiter limiter, CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;
            if (await settori.CentroAsync(acc, ct) is not { } centro) return Sconosciuto(acc);

            var righe = await settori.SettoriAsync(centro.Code, ct);
            // ⚠️ Globale: ripieghi e bande possono portare il cielo a un altro centro, e la topologia di un centro
            // solo scarta i padri fuori da lui. Le righe dei settori le dà già la porta, coi padri giusti.
            var topo = await topologia.BuildGlobalAsync(ct);
            return Results.Json(Proiezione.Settori(centro, righe, topo));
        });

        endpoints.MapGet(Radice + "/{acc}/coordinations", async (
            string acc, string? online, HttpContext ctx, ISettoriPerApi settori, IAgreementService accordi,
            RequestRateLimiter limiter, CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;
            if (await settori.CentroAsync(acc, ct) is not { } centro) return Sconosciuto(acc);

            if (online is null)
                return Results.Json(Proiezione.Scritti(centro, await accordi.ListFlowsByAccAsync(centro.Code, ct)));

            var aperti = Online(online);
            if (aperti.Count > OnlineMassimi)
                return Results.BadRequest(new { error = $"online: al massimo {OnlineMassimi} enti" });
            var risolti = await accordi.ResolveForAccAsync(centro.Code, aperti, ct);
            return Results.Json(Proiezione.Risolti(centro, aperti, risolti));
        });

        return endpoints;
    }

    /// <summary>L'elenco <c>?online=</c>: separato da virgole, spazi e maiuscole non contano, i vuoti si scartano.
    /// Vuoto = nessuno online, e allora tutto va a UNICOM: è una domanda legittima.</summary>
    public static IReadOnlySet<string> Online(string? elenco) =>
        (elenco ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static Task<IResult?> PortaAsync(HttpContext ctx, RequestRateLimiter limiter, CancellationToken ct) =>
        PortaDelleApi.ControllaAsync(ctx, ApiEndpoints.Settori, chiaveObbligatoria: true, limiter,
            RichiesteAlMinutoPerChiave, RichiesteAlMinutoTotali, ChiaviTracciate, ct);

    private static IResult Sconosciuto(string acc) =>
        Results.NotFound(new { error = $"{(acc ?? "").Trim().ToUpperInvariant()}: centro sconosciuto" });

    /// <summary>
    /// Dai modelli al JSON. <b>Pura</b>, per i test. Le enumerazioni escono come parole (<c>arrival</c>,
    /// <c>atOrAbove</c>), non come numeri: un numero cambierebbe significato al primo valore aggiunto in mezzo.
    /// </summary>
    public static class Proiezione
    {
        public static object Centri(IReadOnlyList<ApiAccRow> centri) => new
        {
            count = centri.Count,
            accs = centri.Select(c => new { code = c.Code, name = c.Name, military = c.IsMilitary }),
        };

        public static object Settori(ApiAccRow centro, IReadOnlyList<ApiSectorRow> righe, Topology topo) => new
        {
            acc = centro.Code,
            count = righe.Count,
            sectors = righe.Select(r => new
            {
                callsign = r.Callsign,
                name = r.Name,
                // Il tipo come lo scrive il nominativo (CTR, APP, TWR…): è la parola che chi integra conosce.
                type = r.Type.ToString().ToUpperInvariant(),
                kind = Parola(r.Kind),
                airport = r.AirportIcao,
                frequency = r.Frequency,
                parent = r.ParentCallsign,
                coverageOrder = r.CoverageOrder,
                band = topo.Bands.TryGetValue(r.Callsign, out var b) ? new { baseFt = b.BaseFeet, topFt = b.TopFeet } : null,
                // Le righe DICHIARATE, nell'ordine: stanno davanti al padre quando si cerca chi riceve.
                fallbacks = (topo.Fallbacks.TryGetValue(r.Callsign, out var f) ? f : Array.Empty<FallbackRow>())
                    .Select(x => new
                    {
                        target = x.TargetCallsign, kind = Parola(x.Kind), baseFt = x.BaseFeet, topFt = x.TopFeet,
                        automatic = x.Automatica,
                    }),
            }),
        };

        public static object Scritti(ApiAccRow centro, IReadOnlyList<TransferFlowRow> flussi) => new
        {
            acc = centro.Code,
            resolved = false,
            count = flussi.Count,
            flows = flussi.Select(f => new
            {
                owner = f.OwningSectorCallsign,
                kind = Parola(f.Kind),
                airports = Scali(f),
                points = f.Points.Select(p => Punto(p)),
            }),
        };

        /// <summary>⚠️ Lo stesso flusso può uscire più volte, una per chi lo cede davvero: con il settore chiuso, la
        /// sua riga passa a chi ne tiene il cielo, e se lo tengono in due escono due righe.</summary>
        public static object Risolti(ApiAccRow centro, IReadOnlySet<string> online, IReadOnlyList<ResolvedTransferFlow> flussi) => new
        {
            acc = centro.Code,
            resolved = true,
            online = online.OrderBy(o => o, StringComparer.Ordinal),
            count = flussi.Count,
            flows = flussi.Select(r => new
            {
                owner = r.Flow.OwningSectorCallsign,
                resolvedOwner = r.ResolvedOwnerCallsign,
                ownerOnline = r.OwnerOnline,
                kind = Parola(r.Flow.Kind),
                airports = Scali(r.Flow),
                points = r.Points.Select(p => Punto(p.Point, p)),
            }),
        };

        /// <summary>Un punto di trasferimento; con <paramref name="risolto"/> anche chi lo prende adesso.</summary>
        public static object Punto(TransferPointRow p, ResolvedTransferPoint? risolto = null) => new
        {
            cop = p.Cop,
            level = new
            {
                text = Vuoto(p.LevelText),
                value = p.LevelValue,
                unit = p.LevelUnit == Vipi.Domain.LevelUnit.Fl ? "fl" : "ft",
                constraint = Parola(p.LevelConstraint),
                special = Vuoto(p.LevelSpecial),
            },
            receiver = p.NextSectorCallsign,
            handler = risolto?.ResolvedHandler,
            handlerOnline = risolto?.IsOnline,
            condition = Vuoto(p.ConditionDisplay),
            handoff = p.HasHandoff
                ? new { kind = Parola(p.HandoffKind), label = Vuoto(p.HandoffLabel), level = Vuoto(p.HandoffLevelText) }
                : null,
            speed = Vuoto(p.SpeedText),
            variantGroup = p.VariantGroup,
            variantDepth = p.VariantDepth,
        };

        /// <summary>Gli scali del flusso: tutti quelli dell'accordo quando sono più d'uno, se no quello solo.</summary>
        public static IReadOnlyList<string> Scali(TransferFlowRow f) =>
            f.AirportIcaos.Count > 0 ? f.AirportIcaos
            : f.AirportIcao is { Length: > 0 } icao ? new[] { icao }
            : Array.Empty<string>();

        public static string Parola<T>(T valore) where T : struct, Enum =>
            JsonNamingPolicy.CamelCase.ConvertName(valore.ToString());

        private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}

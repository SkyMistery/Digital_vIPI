using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Hosting;

/// <summary>
/// Le API degli aeroporti, per gli altri programmi della divisione: l'elenco degli scali, la scheda di uno scalo,
/// le sue SID e le sue STAR. Carta <c>docs/feature/2026-09-30-api-aeroporti.md</c>.
///
/// <list type="bullet">
/// <item><c>GET /vsop/api/v1/airports</c>: gli scali con un documento pubblicato;</item>
/// <item><c>GET /vsop/api/v1/airports/{icao}</c>: la scheda (TA, fasce TL, piste, frequenze);</item>
/// <item><c>GET /vsop/api/v1/airports/{icao}/sids</c> e <c>…/stars</c>: le procedure, con <c>?runway=</c>.</item>
/// </list>
///
/// <para>🔴 <b>È la VISTA PUBBLICA, non l'archivio</b> (committente, 30 settembre 2026): chi chiama legge quello che
/// legge un pilota nel documento pubblicato — la release in vigore dove la sezione è congelata, la derivazione del
/// ciclo AIRAC corrente dove è viva, senza le righe nascoste e con i punti corretti a mano. Lo stesso
/// <see cref="IAirportViewDerivationService.ResolveForViewAsync"/> della pagina, con gli stessi argomenti: una
/// seconda strada verso le SID divergerebbe dal documento alla prima correzione.</para>
///
/// <para>⚠️ Il cancello degli scali è quello del vAWOS (<see cref="AwosGate"/>): un documento con una release
/// effettiva e non nascosto. Uno scalo senza documento pubblicato è un 404, anche se in archivio ha le SID — lo
/// staff non le ha ancora date al pubblico.</para>
///
/// <para>Le API non sono mai anonime: la chiave è obbligatoria sempre, senza il periodo di passaggio
/// dell'archivio (carta 2026-09-13-chiavi-api.md): queste nascono dopo la regola.</para>
/// </summary>
public static class ApiAeroporti
{
    public const string Radice = "/vsop/api/v1/airports";

    /// <summary>Tetti per chiave: un programma che integra legge gli scali che gli servono, non tutti ogni minuto.
    /// Una richiesta costa l'elenco dei documenti più il profilo dello scalo.</summary>
    private const int RichiesteAlMinutoPerChiave = 60;
    private const int RichiesteAlMinutoTotali = 600;
    private const int ChiaviTracciate = 5000;

    public static IEndpointRouteBuilder MapApiAeroporti(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Radice, async (
            HttpContext ctx, IDocumentAdminService documenti, RequestRateLimiter limiter, CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;

            var elenco = AwosGate.Elenco(await documenti.ListAsync(ct));
            return Results.Json(new
            {
                count = elenco.Count,
                airports = elenco.Select(a => new { icao = a.Icao, name = a.Nome, vipi = a.HaVipi, vsop = a.HaVsop }),
            });
        });

        endpoints.MapGet(Radice + "/{icao}", async (
            string icao, HttpContext ctx, IDocumentAdminService documenti, IAirportEditingService scali,
            IAirportViewDerivationService viste, RequestRateLimiter limiter, CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;

            var id = Norm(icao);
            if (Edizione(await documenti.ListAsync(ct), id) is not { } edizione) return NonPubblicato(id);

            var scalo = await scali.LoadForViewAsync(id, ct);
            if (scalo is null) return NonPubblicato(id);

            var d = await viste.ResolveForViewAsync(id, useFrozen: true, edizione, ct: ct);
            return Results.Json(Proiezione.Scheda(scalo, edizione, d));
        });

        endpoints.MapGet(Radice + "/{icao}/sids", (
                string icao, string? runway, HttpContext ctx, IDocumentAdminService documenti,
                IAirportViewDerivationService viste, RequestRateLimiter limiter, CancellationToken ct) =>
            ProcedureAsync(icao, runway, ProcedureKind.Sid, ctx, documenti, viste, limiter, ct));

        endpoints.MapGet(Radice + "/{icao}/stars", (
                string icao, string? runway, HttpContext ctx, IDocumentAdminService documenti,
                IAirportViewDerivationService viste, RequestRateLimiter limiter, CancellationToken ct) =>
            ProcedureAsync(icao, runway, ProcedureKind.Star, ctx, documenti, viste, limiter, ct));

        return endpoints;
    }

    private static async Task<IResult> ProcedureAsync(
        string icao, string? runway, ProcedureKind kind, HttpContext ctx, IDocumentAdminService documenti,
        IAirportViewDerivationService viste, RequestRateLimiter limiter, CancellationToken ct)
    {
        if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;

        var id = Norm(icao);
        if (Edizione(await documenti.ListAsync(ct), id) is not { } edizione) return NonPubblicato(id);

        var d = await viste.ResolveForViewAsync(id, useFrozen: true, edizione, ct: ct);
        return Results.Json(Proiezione.Procedure(id, kind, d, runway));
    }

    /// <summary>La chiave è sempre obbligatoria: queste API nascono dopo la regola, e non hanno client di prima da
    /// non fermare.</summary>
    private static Task<IResult?> PortaAsync(HttpContext ctx, RequestRateLimiter limiter, CancellationToken ct) =>
        PortaDelleApi.ControllaAsync(ctx, ApiEndpoints.Aeroporti, chiaveObbligatoria: true, limiter,
            RichiesteAlMinutoPerChiave, RichiesteAlMinutoTotali, ChiaviTracciate, ct);

    /// <summary>
    /// Da quale documento si legge: la vIPI civile se è pubblicata, altrimenti il vSOP militare. Null = nessuno dei
    /// due è pubblico.
    /// <para>⚠️ L'edizione non è un dettaglio: dice da quale RELEASE si leggono le sezioni congelate (vedi
    /// <see cref="IAirportViewDerivationService"/>). Su un campo solo militare chiedere la civile ricadrebbe sempre
    /// sul vivo, e l'API mostrerebbe modifiche che il vSOP pubblicato non ha ancora.</para>
    /// </summary>
    public static ReleaseTargetType? Edizione(IEnumerable<ManagedDoc> documenti, string icao)
    {
        if (icao.Length != 4) return null;
        var (vipi, vsop) = AwosGate.Pubblicati(documenti, icao);
        return vipi ? ReleaseTargetType.Airport : vsop ? ReleaseTargetType.AirportMil : null;
    }

    private static IResult NonPubblicato(string icao) =>
        Results.NotFound(new { error = $"{icao}: aeroporto sconosciuto o senza documento pubblicato" });

    private static string Norm(string? icao) => (icao ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// Dalle viste del documento al JSON. <b>Pura</b>, per i test: le celle del documento hanno «—» dove sono
    /// vuote, e un programma vuole <c>null</c>.
    /// </summary>
    public static class Proiezione
    {
        private const string Trattino = "—";

        public static object Scheda(AirportData scalo, ReleaseTargetType edizione, AirportDerived d) => new
        {
            icao = scalo.Icao,
            name = scalo.Name,
            acc = scalo.AccCode,
            document = edizione == ReleaseTargetType.Airport ? "vipi" : "vsop",
            transitionAltitudeFt = d.Transition.TransitionAltitudeFt,
            transitionLevels = d.Transition.Rows.Select(r => new { qnh = r.QnhRange, level = r.Level }),
            runways = d.Runways.Rows.Select(r => new
            {
                ident = r.Ident,
                lengthM = r.LengthM,
                tora = Valore(r.Tora),
                lda = Valore(r.Lda),
                approaches = AirportRunwayLists.Tokens(Valore(r.AppProcedures)),
                patterns = Valore(r.Patterns),
                circling = Valore(r.Circling),
                threshold = Valore(r.Threshold),
                thresholdElevationFt = r.ThresholdElevationFt,
            }),
            frequencies = d.Frequencies.Rows.Select(f => new
            {
                name = f.Name, callsign = f.Callsign, frequency = f.Frequency, primary = f.IsPrimary,
            }),
        };

        /// <param name="runway">Il filtro per pista, con la regola del documento (<c>AirportSids</c>): le righe di
        /// quella pista e quelle che non ne dicono nessuna, perché valgono per tutte.</param>
        public static object Procedure(string icao, ProcedureKind kind, AirportDerived d, string? runway)
        {
            var vista = kind == ProcedureKind.Sid ? d.Sids : d.Stars;
            var pista = runway?.Trim();
            var righe = vista.Rows
                .Where(r => string.IsNullOrEmpty(pista) || Valore(r.Runway) is null
                            || string.Equals(r.Runway, pista, StringComparison.OrdinalIgnoreCase))
                .Select(r => Riga(r, kind, d.Transition.TransitionAltitudeFt))
                .ToList();

            return kind == ProcedureKind.Sid
                ? new { icao, transitionAltitudeFt = d.Transition.TransitionAltitudeFt, count = righe.Count, sids = righe }
                : (object)new { icao, transitionAltitudeFt = d.Transition.TransitionAltitudeFt, count = righe.Count, stars = righe };
        }

        public static ProceduraApi Riga(AirportSidRowView r, ProcedureKind kind, int? transitionAltitudeFt)
        {
            var (quota, conApp) = kind == ProcedureKind.Sid ? Salita(r.InitialClimb) : (null, false);
            return new ProceduraApi(
                Valore(r.Runway), Valore(r.Fix), r.Name, Valore(r.Transition),
                kind == ProcedureKind.Sid ? Valore(Vipi.Ui.Shared.AirportViewFormat.InitialClimb(r.InitialClimb, transitionAltitudeFt)) : null,
                quota, conApp,
                Valore(r.Type), Valore(r.Cat), Valore(r.Wtc), Valore(r.Condition));
        }

        /// <summary>
        /// La quota di initial climb in piedi, e se va concordata con l'APP. La cella del documento le porta insieme
        /// («5000 (to coord with APP)», o la sola nota), come le scrive <c>AirportSidDerivationService</c>.
        /// </summary>
        public static (int? Piedi, bool ConApp) Salita(string? cella)
        {
            var s = Valore(cella);
            if (s is null) return (null, false);
            var conApp = s.Contains(AirportSidDerivationService.NotaClimbApp, StringComparison.OrdinalIgnoreCase);
            var m = Quota.Match(s);
            return (m.Success && int.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var ft) ? ft : null, conApp);
        }

        private static readonly Regex Quota = new(@"^([\d,]+)", RegexOptions.Compiled);

        public static string? Valore(string? cella)
        {
            var s = (cella ?? "").Trim();
            return s.Length == 0 || s == Trattino ? null : s;
        }
    }
}

/// <summary>Una procedura (SID o STAR) come la legge chi chiama l'API. I campi vuoti del documento sono <c>null</c>.</summary>
/// <param name="InitialClimb">Come la scrive il documento: in piedi fino alla TA, in livello di volo sopra, con la nota
/// dell'APP se c'è. Solo per le SID.</param>
/// <param name="InitialClimbFt">La stessa quota in piedi, per i programmi.</param>
/// <param name="InitialClimbByApp">La quota va concordata con l'APP.</param>
public sealed record ProceduraApi(
    string? Runway, string? Fix, string Name, string? Transition,
    string? InitialClimb, int? InitialClimbFt, bool InitialClimbByApp,
    string? Type, string? Cat, string? Wtc, string? Condition);

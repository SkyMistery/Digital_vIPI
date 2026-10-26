using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components.Doc;

namespace Vipi.Hosting;

/// <summary>
/// Le API degli aeroporti, per gli altri programmi della divisione: l'elenco degli scali, la scheda di uno scalo,
/// le sue SID e le sue STAR. Carta <c>docs/feature/2026-09-30-api-aeroporti.md</c>.
///
/// <list type="bullet">
/// <item><c>GET /vsop/api/v1/airports</c>: gli scali con un documento pubblicato;</item>
/// <item><c>GET /vsop/api/v1/airports/{icao}</c>: la scheda (TA, fasce TL, piste, frequenze, torre radar, regole
/// di scelta pista, pista in uso adesso);</item>
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
    public const string Radice = ApiRotte.Aeroporti;

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
            IAirportViewDerivationService viste, IWeatherProvider meteo, RequestRateLimiter limiter,
            CancellationToken ct) =>
        {
            if (await PortaAsync(ctx, limiter, ct) is { } rifiuto) return rifiuto;

            var id = Norm(icao);
            if (AwosGate.Edizione(await documenti.ListAsync(ct), id) is not { } edizione) return NonPubblicato(id);

            var scalo = await scali.LoadForViewAsync(id, ct);
            if (scalo is null) return NonPubblicato(id);

            var d = await viste.ResolveForViewAsync(id, useFrozen: true, edizione, ct: ct);
            // Il METAR dalla stessa porta della pagina (con la stazione di riferimento): la pista in uso dell'API
            // e quella del documento si decidono sullo stesso vento.
            var wx = await meteo.GetAsync(id, ct);
            return Results.Json(Proiezione.Scheda(scalo, edizione, d, wx, DateTimeOffset.UtcNow));
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
        if (AwosGate.Edizione(await documenti.ListAsync(ct), id) is not { } edizione) return NonPubblicato(id);

        var d = await viste.ResolveForViewAsync(id, useFrozen: true, edizione, ct: ct);
        return Results.Json(Proiezione.Procedure(id, kind, d, runway));
    }

    /// <summary>La chiave è sempre obbligatoria: queste API nascono dopo la regola, e non hanno client di prima da
    /// non fermare.</summary>
    private static Task<IResult?> PortaAsync(HttpContext ctx, RequestRateLimiter limiter, CancellationToken ct) =>
        PortaDelleApi.ControllaAsync(ctx, ApiEndpoints.Aeroporti, chiaveObbligatoria: true, limiter,
            RichiesteAlMinutoPerChiave, RichiesteAlMinutoTotali, ChiaviTracciate, ct);

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

        /// <param name="meteo">Il bollettino di adesso, per la pista in uso; null = nessun METAR.</param>
        /// <param name="adesso">L'ora della risposta: serve a datare il METAR (il gruppo <c>ddhhmmZ</c> non ha mese).</param>
        public static object Scheda(AirportData scalo, ReleaseTargetType edizione, AirportDerived d,
                                    WeatherReport? meteo = null, DateTimeOffset? adesso = null) => new
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
            // ⚠️ null = «non indicato», che non è «senza radar»: il documento distingue, e così l'API.
            radarTower = d.RadarTower.Equipped,
            runwayRules = Regole(d.Rules),
            runwayInUse = InUso(scalo, d, meteo, adesso ?? DateTimeOffset.UtcNow),
        };

        /// <summary>
        /// La pista in uso <b>adesso</b> (S132, chiesta dal committente il 26 ottobre 2026): lo stesso calcolo della
        /// pagina del documento (<see cref="PistaInUso.Calcola"/>), con gli stessi argomenti — le regole che la
        /// sezione mostra, poi il vento più in prua senza le soglie «mai», misurato sulle rotte vere delle testate.
        /// <para>⚠️ Il documento, non il vAWOS: il quadro mette l'ATIS di chi è online prima delle regole, la
        /// pagina no. L'API è la vista pubblica del documento (vedi la testata) e dice la sua pista; un programma
        /// che vuole l'ATIS lo legge dal whazzup.</para>
        /// <para>⚠️ Come la pagina, nessun tetto all'età del METAR: si restituiscono il bollettino e la sua ora, e
        /// chi chiama decide se è troppo vecchio per fidarsene.</para>
        /// </summary>
        public static PistaInUsoApi InUso(AirportData scalo, AirportDerived d, WeatherReport? meteo, DateTimeOffset adesso)
        {
            var grezzo = string.IsNullOrWhiteSpace(meteo?.Metar) ? null : meteo!.Metar!.Trim();
            var metar = grezzo is null ? null : MetarParser.ParseMetar(grezzo);
            var windDir = metar?.Wind is { Calm: false, DirectionDeg: int dir } ? dir : (int?)null;
            var windKt = metar?.Wind?.SpeedKt ?? 0;

            // Sulle regole della sezione; solo una release di prima del 12 settembre 2026 non le ha, e allora
            // si decide sulle vive come fa la pagina.
            var regole = d.Rules.Regole;
            var esito = PistaInUso.Calcola(regole, d.Sids, d.Runways.Rows.Select(r => r.Ident).ToList(),
                windDir, windKt, metar, regole is null ? scalo.Rules : null,
                AirportRunwayRowView.Esclusioni(d.Runways.Rows), RunwayRow.Rotte(scalo.Runways),
                RunwaySuggestion.VentoNoto(metar?.Wind));

            RegolaInUsoApi? regola = null;
            if (esito.Regola is { } r)
            {
                // La posizione solo se il verdetto è sulle righe che `runwayRules` elenca: sulle vive sarebbe la
                // riga di un'altra lista.
                int? posizione = esito.SulleRegoleMostrate && r.RuleIndex >= 0 && r.RuleIndex < d.Rules.Rows.Count
                    ? d.Rules.Rows[r.RuleIndex].Position : null;
                regola = new RegolaInUsoApi(posizione, Valore(r.RuleName));
            }

            var fonte = regola is not null ? "rule" : esito.Dep.Count + esito.Arr.Count > 0 ? "wind" : "none";
            return new PistaInUsoApi(esito.Dep.ToList(), esito.Arr.ToList(), fonte, regola, grezzo,
                MetarParser.OraOsservazione(metar?.TimeRaw, adesso));
        }

        /// <summary>
        /// Le regole di scelta pista, nell'ordine del documento: si applica la <b>prima</b> le cui condizioni sono
        /// soddisfatte. Ogni riga porta la condizione come la scrive il documento e, accanto, le soglie una per una.
        /// <para>⚠️ Le soglie vengono da <see cref="AirportRulesView.Regole"/>, che le release scattate prima del
        /// 12 settembre 2026 non hanno: lì escono <c>null</c>, e resta il testo. Non si rileggono dall'anagrafica,
        /// o la scheda direbbe regole diverse da quelle che il documento mostra.</para>
        /// <para>⚠️ Niente nota per regola: dal 21 ottobre 2026 il testo sta nei blocchi della sezione (S123a), e la
        /// colonna se ne va con la migrazione seguente. Esporla darebbe a chi integra un campo nato per sparire.</para>
        /// </summary>
        public static IReadOnlyList<RegolaPistaApi> Regole(AirportRulesView regole)
        {
            var calcolabili = regole.Regole is { } r && r.Count == regole.Rows.Count ? r : null;
            return regole.Rows.Select((riga, i) =>
            {
                var c = calcolabili?[i];
                return new RegolaPistaApi(
                    riga.Position, c is null ? null : Valore(c.Name), Valore(riga.Condition),
                    AirportRunwayLists.Tokens(Valore(riga.Dep)), AirportRunwayLists.Tokens(Valore(riga.Arr)),
                    c?.MaxTailwindKt, c?.MaxCrosswindKt, c is null ? null : Nome(c.Surface),
                    Ora(c?.TimeFromLocalMin), Ora(c?.TimeToLocalMin), Giorni(c?.DaysOfWeekMask),
                    c is null ? null : Nome(c.DateParity),
                    GiornoDellAnno(c?.DateFromMonthDay), GiornoDellAnno(c?.DateToMonthDay));
            }).ToList();
        }

        private static string Nome<T>(T valore) where T : struct, Enum => valore.ToString().ToLowerInvariant();

        private static string? Ora(int? minuti) => minuti is int m ? $"{m / 60:00}:{m % 60:00}" : null;

        /// <summary>«MMDD» → «MM-DD».</summary>
        private static string? GiornoDellAnno(int? mmdd) => mmdd is int v ? $"{v / 100:00}-{v % 100:00}" : null;

        private static readonly string[] NomiDeiGiorni = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        /// <summary>I giorni della maschera (bit 0 = lunedì). null = nessun vincolo: maschera vuota o tutti e sette,
        /// come la condizione del documento.</summary>
        private static IReadOnlyList<string>? Giorni(int? maschera) =>
            maschera is not int m || m == 0 || m == 0b111_1111
                ? null
                : Enumerable.Range(0, 7).Where(b => (m & (1 << b)) != 0).Select(b => NomiDeiGiorni[b]).ToList();

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

/// <summary>La pista in uso adesso, come la decide il documento.</summary>
/// <param name="Departure">Le piste in partenza; vuoto = non si sa (niente METAR, vento non noto, soglie tutte «mai»).</param>
/// <param name="Source"><c>rule</c> (una regola dello scalo), <c>wind</c> (nessuna regola vale: il vento più in prua) o
/// <c>none</c>.</param>
/// <param name="Rule">La regola che vale; null se non l'ha decisa una regola.</param>
/// <param name="Metar">Il bollettino su cui si è deciso, com'è arrivato.</param>
/// <param name="MetarObservedUtc">L'ora dell'osservazione, dal gruppo <c>ddhhmmZ</c>: per giudicarne l'età.</param>
public sealed record PistaInUsoApi(
    IReadOnlyList<string> Departure, IReadOnlyList<string> Arrival, string Source, RegolaInUsoApi? Rule,
    string? Metar, DateTimeOffset? MetarObservedUtc);

/// <param name="Position">La <c>position</c> della riga in <c>runwayRules</c>; null se il verdetto viene dalle regole
/// vive perché la release (di prima del 12 settembre 2026) non porta quelle calcolabili.</param>
public sealed record RegolaInUsoApi(int? Position, string? Name);

/// <summary>Una regola di scelta pista come la legge chi chiama l'API. Le soglie sono <c>null</c> dove il documento
/// non le ha (release di prima del 12 settembre 2026) o non le pone.</summary>
/// <param name="Condition">La condizione come la scrive il documento, nome compreso.</param>
/// <param name="Surface"><c>any</c>, <c>dry</c> o <c>wet</c> (pioggia o neve nel METAR).</param>
/// <param name="TimeFromLocal">Orario «HH:mm» in ora LOCALE italiana, come in AIP; estremi inclusi, la finestra può
/// passare la mezzanotte.</param>
/// <param name="DaysOfWeek">I giorni in cui vale (<c>mon</c>…<c>sun</c>); null = tutti.</param>
/// <param name="DateParity"><c>any</c>, <c>even</c> o <c>odd</c>: la parità del giorno del mese.</param>
/// <param name="SeasonFrom">Finestra stagionale che si ripete ogni anno, «MM-DD», estremi inclusi; può passare il
/// capodanno.</param>
public sealed record RegolaPistaApi(
    int Position, string? Name, string? Condition,
    IReadOnlyList<string> DepartureRunways, IReadOnlyList<string> ArrivalRunways,
    int? MaxTailwindKt, int? MaxCrosswindKt, string? Surface,
    string? TimeFromLocal, string? TimeToLocal, IReadOnlyList<string>? DaysOfWeek, string? DateParity,
    string? SeasonFrom, string? SeasonTo);

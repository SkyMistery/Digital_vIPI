using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Application.Weather;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Una vIPI d'aeroporto <b>già caricata</b>: il documento nella lingua di lettura, le derivate risolte, il
/// meteo, e la pista in uso <i>adesso</i>.
///
/// <para>Esiste perché lo stesso documento va reso in due posti: la sua pagina, e la pagina <b>unita</b> che
/// lo mostra insieme ad altri (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>).</para>
/// </summary>
public sealed record AirportMemberDocument(
    string Icao,
    DocumentView View,
    IReadOnlyList<SectionView> Sezioni,
    PreviewMode Mode,
    string? RelCycle,
    string? Bloccata,
    TranslationCoverage Copertura,
    bool HaMarcate,
    SectionAudience? Vista,
    AirportDerived Derived,
    AirportStation? Station,
    WeatherReport? Wx,
    ParsedMetar? Metar,
    ParsedTaf? Taf,
    RunwayRuleResult? RuleResult,
    // ⚠️ HashSet e non IReadOnlySet: <AirportRunways> dichiara così i suoi parametri, e un'interfaccia qui
    // costringerebbe ogni chiamante a una conversione. Sono insiemi di sola lettura per convenzione.
    HashSet<string> DepIdents,
    HashSet<string> ArrIdents,
    int? WindDir,
    int WindKt,
    string? SidRwy)
{
    /// <summary>La release che questa vista mostra: quella dell'anteprima, o null = la effettiva adesso.</summary>
    public int? ReleaseIdShown => Mode.Kind == PreviewKind.Release ? Mode.ReleaseId : null;

    /// <summary>⚠️ Dall'ANAGRAFICA, non dalle derivate: in anteprima di release il profilo viene azzerato di
    /// proposito, e la presenza di una base militare non è un dato di release — sparire dalla testata solo
    /// perché si guarda un ciclo passato sarebbe un'informazione persa senza motivo.</summary>
    public bool MilitaryPresence => Station?.HasMilitaryPresence ?? false;
    public bool MilitaryOnly => Station?.IsMilitaryOnly ?? false;
}

/// <summary>
/// Il caricamento di una vIPI d'aeroporto per la lettura: quello che stava in
/// <c>AeroportoPage.OnParametersSetAsync</c>, spostato di peso perché non appartiene a <i>quella</i> pagina.
///
/// <para>
/// ⚠️ <b>NON si registra in DI e non si prende con <c>@inject</c>.</b> I servizi che interroga vanno presi
/// dallo scope PROPRIO della pagina che lo usa — <c>ActivatorUtilities.CreateInstance&lt;AirportMemberLoader&gt;(ScopedServices)</c>
/// — perché <c>AeroportoPage</c> è <c>OwningComponentBase</c> per un motivo misurato: alle 17:44 del
/// 24 agosto 2026 quella pagina è morta <b>sette volte</b> con «A second operation was started on this
/// context instance». Iniettarlo dal circuito rimetterebbe tutto sul <c>DbContext</c> della richiesta e
/// riaprirebbe quel guasto — che è ancora senza una causa nota, quindi la difesa serve.
/// </para>
/// <para>
/// ⚠️ <b>L'ordine dei passi è la correttezza</b>: la lingua del documento si decide <b>subito</b> (vale anche
/// per le derivazioni), la traduzione va <b>in fondo</b>, i titoli di catalogo si <b>ritraducono</b> dalla
/// stessa passata, il filtro di lettura è <b>l'ultimo</b>.
/// </para>
/// </summary>
public sealed class AirportMemberLoader
{
    private readonly IVipiViewService _viewService;
    private readonly IAirportEditingService _profile;
    private readonly IAirportViewDerivationService _airportView;
    private readonly IReleaseService _releases;
    private readonly IEditAuthorizationService _authz;
    private readonly DocumentTranslator _translator;
    private readonly IWeatherProvider _weather;
    private readonly IStationResolver _stations;
    private readonly ReadingLanguageContext _lingua;

    public AirportMemberLoader(IVipiViewService viewService, IAirportEditingService profile,
                               IAirportViewDerivationService airportView, IReleaseService releases,
                               IEditAuthorizationService authz, DocumentTranslator translator,
                               IWeatherProvider weather, IStationResolver stations,
                               ReadingLanguageContext lingua)
    {
        _viewService = viewService;
        _profile = profile;
        _airportView = airportView;
        _releases = releases;
        _authz = authz;
        _translator = translator;
        _weather = weather;
        _stations = stations;
        _lingua = lingua;
    }

    /// <param name="linguaDelCircuito">Il contesto di lingua della RICHIESTA, quando il chiamante ha uno
    /// scope proprio. ⚠️ Sono DUE istanze, e non è un di più: i servizi presi dallo scope della pagina
    /// vedono un <c>ReadingLanguageContext</c> diverso da quello iniettato nella pagina, e la lingua del
    /// documento va scritta in tutti e due o metà catena legge l'altra.</param>
    public async Task<AirportMemberDocument?> LoadAsync(string icao, PreviewMode mode, string? vista,
                                                        ReadingLanguageContext? linguaDelCircuito = null,
                                                        CancellationToken ct = default)
    {
        var code = (icao ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return null;

        DocumentView? view = null;
        string? relCycle = null;

        switch (mode.Kind)
        {
            case PreviewKind.Draft:
                if (_authz.IsEditor)
                    view = await _viewService.BuildAirportVipiAsync(code, BlockTier.Extended, live: false,
                                                                    ignoreRelease: true, preferWorking: true, ct);
                else { mode = default; view = await PubblicaAsync(code, ct); }
                break;
            case PreviewKind.Release:
                var pv = await _releases.GetPreviewAsync(mode.ReleaseId, ReleaseTargetType.Airport, code, ct);
                if (pv?.Doc is not null)
                {
                    relCycle = pv.AiracCycle;
                    view = await _viewService.BuildFromRawAsync(pv.Doc, BlockTier.Extended, ct);
                }
                else { mode = default; view = await PubblicaAsync(code, ct); }
                break;
            default:
                view = await PubblicaAsync(code, ct);
                break;
        }
        if (view is null) return null;

        // ---- In che lingua si legge QUESTO documento (carta 2026-08-31-lingua-bloccata.md §3-4) ---------
        // ⚠️ SUBITO: se è bloccato la lingua vale anche per le DERIVAZIONI, che partono qui sotto e
        // compongono prosa ed etichette. Deciderla in fondo vorrebbe dire derivare nella lingua di chi guarda
        // e tradurre il resto nell'altra — mezza schermata per lingua, e nessun errore da nessuna parte.
        var lettore = LinguaDelDocumento.Prepara(_lingua, view.LanguageLocked, view.Language, Language.It,
                                                 linguaDelCircuito);
        var bloccata = view.LanguageLocked ? lettore : null;

        var wx = await _weather.GetAsync(code, ct);
        var metar = string.IsNullOrWhiteSpace(wx?.Metar) ? null : MetarParser.ParseMetar(wx!.Metar!);
        var taf = string.IsNullOrWhiteSpace(wx?.Taf) ? null : MetarParser.ParseTaf(wx!.Taf!);

        // ⚠️ Il profilo serve solo alle regole piste, che decidono la pista IN USO ADESSO: quella si valuta
        // sempre sulle regole vive, anche mentre si guarda un ciclo passato — è la risposta a «dove atterro»,
        // non un dato di release. La tabella delle regole, invece, viene dalle derivate come tutto il resto.
        var profilo = await _profile.LoadForViewAsync(code, ct);
        var station = _stations.Airport(code);

        // ⚠️ Le derivate si risolvono INSIEME al documento e con lo stesso criterio (doc 11 §3d): frozen solo
        // in vista pubblica, live in bozza e in anteprima di release. Deciderlo sezione per sezione, o
        // lasciare che un ramo di fallback dimenticasse il flag, renderebbe il congelamento AIRAC aggirabile
        // da un `?as=` qualsiasi.
        var useFrozen = mode.Kind is not (PreviewKind.Draft or PreviewKind.Release);
        // ⚠️ In anteprima di release si guarda al CICLO DI QUELLA RELEASE, non a quello di oggi: le SID hanno
        // una regola che dipende dal ciclo, e chiedendo sempre «adesso» l'anteprima di una release programmata
        // mostrava la tabella di oggi e non quella che uscirà.
        var derived = await _airportView.ResolveForViewAsync(code, useFrozen, ReleaseTargetType.Airport, relCycle, ct);

        var runways = derived.Runways.Rows.Count > 0
            ? derived.Runways.Rows.Select(r => r.Ident).ToList()
            : PisteCotte(view);
        var windDir = metar?.Wind is { Calm: false, DirectionDeg: int d } ? d : (int?)null;
        var windKt = metar?.Wind?.SpeedKt ?? 0;
        var (ruleResult, dep, arr, sidRwy) = PistaInUso(profilo, derived, runways, windDir, windKt, metar);

        // ---- Lettura bilingue (carta 2026-08-27 §7) --------------------------------------------------
        // ⚠️ IN FONDO, non appena caricato il documento: le sezioni derivate e le piste si leggono
        // dall'originale (ids, identificatori, JSON dei blocchi), la traduzione porta la PROSA. Tradurre prima
        // significherebbe cercare «regulated» in un documento che nel frattempo dice altro.
        var tradotto = await _translator.TranslateAsync(view, Language.It, lettore, ct);
        view = tradotto.View;

        // Le sezioni da rendere si ricavano DOPO la traduzione, e si RITRADUCONO.
        //
        // ⚠️ Il secondo giro non è per scrupolo: `ForView` riporta ogni sezione di catalogo al suo titolo di
        // CATALOGO e quindi butta via il titolo appena tradotto. Visto a schermo su LIBC il 28 agosto 2026:
        // indice e testate dicevano «Regole piste» in mezzo a una pagina inglese, con la copertura che
        // dichiarava «tutto tradotto». Ripassare la sezione dalla stessa passata costa zero query.
        // ⚠️ `ForView` vuole la LINGUA: i titoli di catalogo non sono segmenti del documento, quindi la
        // passata non li conosce e ripassarli da lì non basta.
        var sezioni = AirportLegacySections.ForView(view.Sections, lettore).Select(tradotto.Pass.Sezione).ToList();

        // Il filtro di lettura, DOPO la traduzione e DOPO i titoli di catalogo: si filtra la vista che il
        // lettore vedrà davvero, e la chip si accende solo se c'è qualcosa da filtrare.
        var letturaVista = AudienceFilter.Leggi(vista);
        var haMarcate = AudienceFilter.HaSezioniMarcate(sezioni);

        return new AirportMemberDocument(
            code, view, AudienceFilter.Filtra(sezioni, letturaVista), mode, relCycle, bloccata,
            tradotto.Coverage, haMarcate, letturaVista, derived, station, wx, metar, taf,
            ruleResult, dep, arr, windDir, windKt, sidRwy);
    }

    private Task<DocumentView?> PubblicaAsync(string icao, CancellationToken ct) =>
        _viewService.BuildAirportVipiAsync(icao, BlockTier.Extended, live: false, ct: ct);

    /// <summary>
    /// Quale pista è in uso <b>adesso</b>, in partenza e in arrivo, e da quale pista parte il filtro SID.
    ///
    /// <para>⚠️ Si valuta sulle regole <b>vive</b>, non su quelle eventualmente congelate: «quale pista è in
    /// uso adesso» è una domanda sul presente, e resta tale anche mentre si sfoglia un ciclo passato.</para>
    /// </summary>
    private static (RunwayRuleResult? Rule, HashSet<string> Dep, HashSet<string> Arr, string? SidRwy)
        PistaInUso(AirportData? profilo, AirportDerived derived, List<string> runways,
                   int? windDir, int windKt, ParsedMetar? metar)
    {
        var wet = (metar?.HasRain ?? false) || (metar?.HasSnow ?? false);
        var ruleResult = profilo is { Rules.Count: > 0 }
            ? RunwaySuggestion.EvaluateRules(profilo.Rules.Select(AirportViewFormat.MapRule).ToList(),
                                             windDir, windKt, wet, DateTime.UtcNow)
            : null;
        var sugg = RunwaySuggestion.Suggest(runways, windDir, windKt);

        var dep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arr = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ruleResult is not null)
        {
            foreach (var id in Identificativi(ruleResult.Dep)) dep.Add(id);
            foreach (var id in Identificativi(ruleResult.Arr)) arr.Add(id);
        }
        else if (sugg.Best is not null)
        {
            dep.Add((sugg.DepIdent ?? sugg.Best.Ident).Trim());
            arr.Add((sugg.ArrIdent ?? sugg.Best.Ident).Trim());
        }

        // Seme del filtro SID = pista in uso in partenza (se ha SID), altrimenti la prima pista con SID.
        // ⚠️ Qui non c'è nessun «se il lettore ha già scelto»: quella scelta vive nell'isola <AirportSids>,
        // e fingere di conoscerla è esattamente ciò che teneva le chip ferme.
        var sidRwys = Components.App.AirportSids.RunwaysOf(derived.Sids);
        var sidRwy = sidRwys.FirstOrDefault(r => dep.Contains(r)) ?? sidRwys.FirstOrDefault();

        return (ruleResult, dep, arr, sidRwy);
    }

    private static IEnumerable<string> Identificativi(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Ripiego: estrae gli identificativi pista dalla tabella «Piste» <b>cotta</b> nel documento. Serve solo
    /// agli snapshot di release anteriori alla carta 2026-08-26 — un documento di lavoro le piste le ha nel
    /// profilo, e la derivazione le porta già. Per questo cerca ancora per TITOLO: quelle sezioni hanno
    /// chiavi casuali.
    /// </summary>
    private static List<string> PisteCotte(DocumentView view)
    {
        var idents = new List<string>();
        foreach (var s in view.Sections)
        {
            if (!s.Title.Contains("pist", StringComparison.OrdinalIgnoreCase)
                && !s.Title.Contains("runway", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var b in s.Blocks)
            {
                if (b.Format != BlockFormat.Table || string.IsNullOrWhiteSpace(b.BodyJson)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(b.BodyJson!);
                    if (!doc.RootElement.TryGetProperty("rows", out var rows)) continue;
                    foreach (var row in rows.EnumerateArray())
                        if (row.TryGetProperty("cells", out var cells) && cells.GetArrayLength() > 0
                            && cells[0].GetString() is { } ident && !string.IsNullOrWhiteSpace(ident))
                            idents.Add(ident.Trim());
                }
                catch (JsonException) { /* tabella non standard: ignora */ }
                // ⚠️ E l'altra: `TryGetProperty` su una radice ARRAY alza InvalidOperationException, che NON è
                // una JsonException e passava indenne il catch messo lì per il JSON malformato (29 ago 2026).
                catch (InvalidOperationException) { /* radice non-oggetto: ignora */ }
            }
        }
        return idents;
    }
}

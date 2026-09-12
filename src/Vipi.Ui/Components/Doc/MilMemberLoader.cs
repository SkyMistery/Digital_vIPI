using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Application.Weather;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Un vSOP militare d'aeroporto <b>già caricato</b>: il documento nella lingua di lettura, le derivate dello
/// scalo, il meteo e le sei tabelle che il profilo militare disegna.
///
/// <para>Esiste perché lo stesso documento va reso in due posti: la sua pagina, e la pagina <b>unita</b> che
/// lo mostra insieme ad altri (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>).</para>
/// </summary>
// ⚠️ Pubblici perché stanno nella FIRMA dei loader che `Vipi.Hosting` registra: un tipo che compare
// nella firma di un tipo pubblico è pubblico anche lui, e il compilatore lo dice (CS0050/CS0051).
// Vedi ADR-0005 D6 (revisione del 6 settembre 2026, R-009).
public sealed record MilMemberDocument(
    string Icao,
    DocumentView View,
    PreviewMode Mode,
    string? RelCycle,
    string? Bloccata,
    TranslationCoverage Copertura,
    bool HaMarcate,
    SectionAudience? Vista,
    CivilEdition Civile,
    AirportDerived Derivate,
    AirportStation? Station,
    WeatherReport? Wx,
    ParsedMetar? Metar,
    ParsedTaf? Taf,
    IReadOnlyList<AccSpecialAreaView> Aree,
    IReadOnlyList<NavaidRow> Radioassistenze,
    IReadOnlyList<MilDiversionView> Alternati,
    IReadOnlyDictionary<string, MilActivity> AttivitaAree,
    IReadOnlyDictionary<string, string> NoteAree,
    IReadOnlyList<IReadOnlyList<string>> Nominativi,
    IReadOnlyList<IReadOnlyList<string>> Parcheggi,
    // ---- «Bassa quota (BOAT)»: la stessa terna, per la sotto-sezione che ha un visualizzatore suo ----
    // ⚠️ Tre campi in più e non un secondo tipo: è lo STESSO payload sotto un'altra chiave di sezione, e
    // due record gemelli si sarebbero divisi al primo campo aggiunto a uno dei due.
    // ⚠️ Le attività qui restano vuote per costruzione: la tabella BOAT non ha la colonna (carta
    // 2026-09-09-aree-boat.md §3e). Si leggono lo stesso, perché il payload è quello e un documento
    // scritto da una versione futura che le portasse non va perso in lettura.
    IReadOnlyList<AccSpecialAreaView> AreeBoat,
    IReadOnlyDictionary<string, MilActivity> AttivitaBoat,
    IReadOnlyDictionary<string, string> NoteBoat,
    // ---- La pista in uso ADESSO (11 settembre 2026): dalle regole piste, poi dal vento del METAR ----
    // ⚠️ Lo STESSO calcolo della vIPI d'aeroporto (`PistaInUso`), perché è lo stesso campo: due pagine che
    // sullo stesso scalo dicessero due piste diverse sarebbero peggio di nessuna delle due.
    PistaInUsoAdesso InUso,
    int? WindDir,
    int WindKt,
    /// <summary>Lo stato LVP suggerito sui minimi che la sezione mostra. Stesso calcolo della vIPI civile
    /// (<see cref="AirportMemberLoader.ValutaLvp"/>): è lo stesso campo, e la risposta dev'essere una.</summary>
    LvpValutazione? Lvp = null)
{
    /// <summary>La release che questa vista mostra: quella dell'anteprima, o null = la effettiva adesso.</summary>
    public int? ReleaseIdShown => Mode.Kind == PreviewKind.Release ? Mode.ReleaseId : null;
}

/// <summary>
/// Il caricamento di un vSOP militare per la lettura: quello che stava in
/// <c>MilDocumentPage.OnParametersSetAsync</c>, spostato di peso perché non appartiene a <i>quella</i> pagina.
///
/// <para>
/// ⚠️ <b>L'edizione è <c>AirportMil</c>, non <c>Airport</c></b>, ovunque compaia. Le tabelle si derivano
/// dallo stesso scalo del documento civile, ma il congelato si legge dalla release di <b>questo</b>
/// documento: con <c>Airport</c> un campo misto (Pisa) mostrerebbe la fotografia della vIPI civile — al
/// ciclo AIRAC civile — e un campo solo militare (Rivolto) non troverebbe niente e resterebbe <b>per sempre
/// live</b>, senza che nulla protesti.
/// </para>
/// </summary>
public sealed class MilMemberLoader
{
    private readonly IVipiViewService _viewService;
    private readonly IAirportViewDerivationService _airportView;
    private readonly IMilitaryDocumentService _militari;
    private readonly IReleaseService _releases;
    private readonly IEditAuthorizationService _authz;
    private readonly DocumentTranslator _translator;
    private readonly IWeatherProvider _weather;
    private readonly IStationResolver _stations;
    private readonly ReadingLanguageContext _lingua;

    /// <summary>L'anagrafica dello scalo, per le sole regole piste <b>vive</b>: la pista in uso adesso non è un
    /// dato di release. È la stessa porta da cui la legge la vIPI d'aeroporto.</summary>
    private readonly IAirportEditingService _scalo;

    public MilMemberLoader(IVipiViewService viewService, IAirportViewDerivationService airportView,
                           IMilitaryDocumentService militari, IReleaseService releases,
                           IEditAuthorizationService authz, DocumentTranslator translator,
                           IWeatherProvider weather, IStationResolver stations,
                           ReadingLanguageContext lingua, IAirportEditingService scalo)
    {
        _viewService = viewService;
        _airportView = airportView;
        _militari = militari;
        _releases = releases;
        _authz = authz;
        _translator = translator;
        _weather = weather;
        _stations = stations;
        _lingua = lingua;
        _scalo = scalo;
    }

    public async Task<MilMemberDocument?> LoadAsync(string icao, PreviewMode mode, string? vista,
                                                    ReadingLanguageContext? linguaDelCircuito = null,
                                                    bool fissaLaPagina = true,
                                                    CancellationToken ct = default)
    {
        var code = (icao ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) return null;

        _stations.Prewarm();

        DocumentView? view = null;
        string? relCycle = null;
        var useFrozen = false;

        // ⚠️ Il degrado di un'anteprima non autorizzata torna alla PUBBLICA, derivate congelate comprese:
        // lasciarlo a metà servirebbe al pubblico le derivate live, e il congelamento AIRAC diventerebbe
        // aggirabile scrivendo un `?as=rel:` qualsiasi nell'indirizzo.
        switch (mode.Kind)
        {
            case PreviewKind.Draft:
                if (_authz.IsEditor)
                    view = await _viewService.BuildAirportMilVipiAsync(code, BlockTier.Extended, live: false,
                        ignoreRelease: true, preferWorking: true, ct);
                else { mode = default; (view, useFrozen) = await PubblicaAsync(code, ct); }
                break;
            case PreviewKind.Release:
                var pv = await _releases.GetPreviewAsync(mode.ReleaseId, ReleaseTargetType.AirportMil, code, ct);
                if (pv?.Doc is not null)
                {
                    relCycle = pv.AiracCycle;
                    view = await _viewService.BuildFromRawAsync(pv.Doc, BlockTier.Extended, ct);
                }
                else { mode = default; (view, useFrozen) = await PubblicaAsync(code, ct); }
                break;
            default:
                (view, useFrozen) = await PubblicaAsync(code, ct);
                break;
        }
        if (view is null) return null;

        // ---- In che lingua si legge QUESTO documento (carta 2026-08-31-lingua-bloccata.md §3-4) ---------
        // ⚠️ SUBITO: se è bloccato la lingua vale anche per le DERIVAZIONI che partono qui sotto, che
        // compongono prosa ed etichette.
        var lettore = LinguaDelDocumento.Prepara(_lingua, view.LanguageLocked, view.Language, Language.It,
                                                 linguaDelCircuito, fissaLaPagina);
        var bloccata = view.LanguageLocked ? lettore : null;

        var civile = await _militari.GetCivilEditionAsync(code, ct);

        // Le derivate dello STESSO scalo: meteo live più le sezioni congelate della release militare.
        var station = _stations.Airport(code);
        var wx = await _weather.GetAsync(code, ct);
        var metar = string.IsNullOrWhiteSpace(wx?.Metar) ? null : MetarParser.ParseMetar(wx!.Metar!);
        var taf = string.IsNullOrWhiteSpace(wx?.Taf) ? null : MetarParser.ParseTaf(wx!.Taf!);
        var derivate = await _airportView.ResolveForViewAsync(code, useFrozen, ReleaseTargetType.AirportMil, ct: ct);

        // La pista in uso ADESSO, come nella vIPI d'aeroporto: si decide sulle regole CHE SI STANNO MOSTRANDO
        // — congelate in pubblica, vive in bozza — perché la sezione se le porta dietro (carta 2026-09-12). Il
        // vento resta sempre quello di adesso. Solo una release scattata prima non porta le regole
        // calcolabili: lì si ricade sulle vive, e l'anagrafica si legge SOLO in quel caso.
        // ⚠️ Fino all'11 settembre 2026 il vSOP non aveva la sezione, e per questo non marcava niente: le
        // regole c'erano solo nell'anagrafica, e la vIPI civile era l'unica pagina che le leggesse.
        var windDir = metar?.Wind is { Calm: false, DirectionDeg: int d } ? d : (int?)null;
        var windKt = metar?.Wind?.SpeedKt ?? 0;
        var regole = derivate.Rules.Regole;
        var vive = regole is null ? (await _scalo.LoadForViewAsync(code, ct))?.Rules : null;
        var inUso = PistaInUso.Calcola(regole, derivate.Sids,
            derivate.Runways.Rows.Select(r => r.Ident).ToList(), windDir, windKt, metar, vive);

        // ⚠️ Gli id delle aree li porta il DOCUMENTO mostrato; shape e descrizioni vengono dai cataloghi
        // correnti — come nella vIPI ACC e nell'APP. Si legge PRIMA della traduzione: la sezione tradotta
        // porta la prosa, non il JSON del blocco.
        // ⚠️ DUE sezioni dal 9 settembre 2026, non una: «Aree di lavoro» e la sua sotto-sezione «Bassa quota
        // (BOAT)», che ha lo stesso visualizzatore su una selezione sua. Una funzione sola, chiamata due
        // volte con la chiave: due copie di queste tre righe si sarebbero divise al primo ritocco.
        var aree = await AreeDellaSezioneAsync(view, SectionKeys.Regulated, ct);
        var areeBoat = await AreeDellaSezioneAsync(view, SectionKeys.LowLevel, ct);

        // ⚠️ `useFrozen`: in pubblica e in anteprima release si legge la FOTOGRAFIA della release, non
        // l'anagrafica di adesso. In bozza no — lì si guarda quel che si sta scrivendo.
        var radioassistenze = await _militari.ResolveNavaidsForViewAsync(
            code, MilNavaidsPayload.Leggi(SectionPayload.Read(Sezione(view, "navaids"))), useFrozen, ct);

        var alternati = await _militari.ResolveDiversionsForViewAsync(
            code, MilDiversionPayload.Leggi(SectionPayload.Read(Sezione(view, "diversion"))), useFrozen, ct);

        // ⚠️ Il vSOP militare nasce in ITALIANO (carta §1d): la lingua sorgente è quella in cui si REDIGE,
        // non quella dei quindici PDF di partenza. «It» qui è solo la lingua di NASCITA della famiglia: la
        // sorgente vera la porta il documento.
        var tradotto = await _translator.TranslateAsync(view, Language.It, lettore, ct);
        view = tradotto.View;

        // ⚠️ Questi quattro si leggono dai BLOCCHI del documento mostrato e non dal servizio: il contenuto è
        // il payload, quindi in anteprima release arriva già dallo snapshot — chiederlo al servizio darebbe
        // quel che c'è adesso, cioè scavalcherebbe la release.
        //
        // ⚠️ E si leggono DOPO la traduzione, dall'8 settembre 2026. La passata riscrive il `BodyJson` di
        // ogni blocco, e da quel giorno le NOTE delle aree di lavoro sono fra i testi che riscrive: lette
        // prima, un lettore inglese trovava tutto il documento tradotto e le note ancora in italiano.
        // Attività, nominativi e parcheggi vengono dalla stessa lettura perché sono lo stesso payload letto
        // nello stesso modo — tenerne metà di qua e metà di là è il modo di scordarsene alla prossima.
        // ⚠️ Qui e non più in basso: sotto, `AudienceFilter` TOGLIE le sezioni non destinate a chi legge, e
        // da una sezione tolta questi payload tornerebbero vuoti.
        var payloadAree = SectionPayload.Read(Sezione(view, SectionKeys.Regulated));
        var payloadBoat = SectionPayload.Read(Sezione(view, SectionKeys.LowLevel));
        var attivita = MilRegulatedPayload.LeggiAttivita(payloadAree);
        var noteAree = MilRegulatedPayload.LeggiNote(payloadAree);
        var attivitaBoat = MilRegulatedPayload.LeggiAttivita(payloadBoat);
        var noteBoat = MilRegulatedPayload.LeggiNote(payloadBoat);
        var nominativi = MilTablePayload.Leggi(SectionPayload.Read(Sezione(view, "callsigns")), 4);
        var parcheggi = MilTablePayload.Leggi(SectionPayload.Read(Sezione(view, "parkings")), 3);

        // I titoli delle sezioni di CATALOGO nella lingua di lettura, DOPO la traduzione.
        // ⚠️ Non è un doppione della passata: quei titoli stanno scritti nel documento nella lingua che aveva
        // alla NASCITA. ⚠️ DOPO, e non prima: il catalogo è la resa DECISA, la memoria quella plausibile —
        // applicarlo per ultimo è quel che impedisce a «MRVA» di tornare «Minimum vectoring».
        view = SezioniDocumentali.ConSezioni(
            view, TitoliDiCatalogo.Applica(view.Sections, SectionProfile.AirportMil, lettore));

        // Il filtro DOPO la traduzione: si filtra la vista che il lettore vedrà davvero.
        var letturaVista = AudienceFilter.Leggi(vista);
        var haMarcate = AudienceFilter.HaSezioniMarcate(view.Sections);
        view = SezioniDocumentali.ConSezioni(view, AudienceFilter.Filtra(view.Sections, letturaVista));

        return new MilMemberDocument(code, view, mode, relCycle, bloccata, tradotto.Coverage, haMarcate,
                                     letturaVista, civile, derivate, station, wx, metar, taf, aree,
                                     radioassistenze, alternati, attivita, noteAree, nominativi, parcheggi,
                                     areeBoat, attivitaBoat, noteBoat, inUso, windDir, windKt,
                                     AirportMemberLoader.ValutaLvp(derivate.Lvp, metar));
    }

    /// <summary>Vista PUBBLICA: documento e derivate congelate si impostano INSIEME (doc 11 §3d).</summary>
    private async Task<(DocumentView? View, bool UseFrozen)> PubblicaAsync(string icao, CancellationToken ct) =>
        (await _viewService.BuildAirportMilVipiAsync(icao, BlockTier.Extended, live: false, ct: ct), true);

    /// <summary>
    /// Una sezione del documento mostrato, per chiave e <b>a qualunque profondità</b>.
    /// <para>⚠️ Nel profilo militare le sezioni con un payload sono FIGLIE — venti su ventisei — e cercarle
    /// fra le sole radici non ne troverebbe nessuna. È la stessa assunzione (<c>ParentSectionId == null</c> /
    /// <c>Depth == 0</c>) che ha già morso tre volte.</para>
    /// </summary>
    private static SectionView? Sezione(DocumentView view, string key) => Cerca(view.Sections, key);

    private static SectionView? Cerca(IReadOnlyList<SectionView> sezioni, string key)
    {
        foreach (var s in sezioni)
        {
            if (string.Equals(s.SectionKey, key, StringComparison.OrdinalIgnoreCase)) return s;
            if (Cerca(s.Children, key) is { } t) return t;
        }
        return null;
    }

    /// <summary>
    /// Le aree scelte in UNA sezione del documento mostrato, risolte su shape e descrizioni dei cataloghi
    /// correnti. Sezione assente ⇒ nessuna area: è il caso normale di un documento nato prima che la
    /// sotto-sezione avesse un visualizzatore, e non è un errore.
    /// <para>⚠️ Una funzione sola per tutt'e due le sezioni («Aree di lavoro» e «Bassa quota»): il motore è
    /// lo stesso della vIPI ACC e dell'APP, e la selezione non sa da quale sezione arriva.</para>
    /// </summary>
    private async Task<IReadOnlyList<AccSpecialAreaView>> AreeDellaSezioneAsync(
        DocumentView view, string sectionKey, CancellationToken ct) =>
        Sezione(view, sectionKey) is { } sez
            ? await _militari.ResolveRegulatedAreasAsync(LeggiAree(sez), ct)
            : Array.Empty<AccSpecialAreaView>();

    /// <summary>
    /// La selezione delle aree, dal payload della sezione.
    /// <para>⚠️ <c>OwnAuto</c> forzato a falso: senza JSON <c>Parse</c> risponde «automatico», che qui non
    /// esiste — il modo automatico è del solo blocco Aerovia della vIPI ACC.</para>
    /// </summary>
    private static RegulatedSelection LeggiAree(SectionView s)
    {
        var sel = RegulatedSelectionJson.Parse(SectionPayload.Read(s));
        return new RegulatedSelection { OwnAuto = false, OwnIds = sel.OwnIds, ExtraIds = sel.ExtraIds };
    }
}

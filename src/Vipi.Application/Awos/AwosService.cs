using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;

namespace Vipi.Application.Awos;

/// <summary>
/// Compone il quadro vAWOS di uno scalo: anagrafica (piste, soglie, TA/TL) + METAR + pista in uso.
///
/// <para>⚠️ <b>Non aggiunge niente all'archivio</b>: è una vista su dati che esistono già. Le tre cose che il
/// prototipo si portava dietro cablate — rotte di pista, transition level, parser METAR — qui vengono
/// dall'anagrafica IVAO, dalla tabella dell'AIP dello scalo e dal decoder di casa.</para>
/// </summary>
public interface IAwosService
{
    /// <param name="perEditor">
    /// Chi guarda può aprire anche uno scalo non pubblicato. Arriva come <b>parametro</b> e non si chiede qui
    /// dentro: questo servizio lo usa anche un endpoint fuori da ogni pagina, e un livello letto dal contesto
    /// sbaglierebbe proprio lì.
    /// </param>
    /// <param name="metarDiProva">
    /// Bollettino iniettato a mano al posto di quello vero (il «Test METAR» del quadro).
    /// <para>⚠️ Chi chiama deve già aver verificato che sia staff: qui non c'è nessuna guardia, e non deve
    /// essercene una seconda — la prima sta alla porta, dove si sa chi bussa.</para>
    /// </param>
    /// <param name="giaInVigore">
    /// Le LVP erano in vigore al giro precedente: è la memoria del QUADRO, che gliela rimanda a ogni
    /// lettura. Serve all'isteresi delle soglie di cancellazione (<see cref="LvpValutatore"/>), e sta qui
    /// invece che nel JavaScript perché la decisione dev'essere in un posto solo.
    /// </param>
    Task<AwosResult> BuildAsync(string icao, bool perEditor, string? metarDiProva = null,
                                bool giaInVigore = false, CancellationToken ct = default);

    /// <summary>
    /// Gli scali per cui il quadro si apre, in ordine di ICAO: quelli con almeno un documento pubblicato.
    ///
    /// <para>⚠️ È lo <b>stesso</b> insieme del cancello, e non è un dettaglio: un selettore che elencasse uno
    /// scalo che poi rifiuta di aprirsi sarebbe un gesto che non fa niente. Anche per un Editor l'elenco
    /// resta questo — è «che cosa vede il pubblico» — e uno scalo non pubblicato lui lo apre scrivendone
    /// l'indirizzo.</para>
    /// </summary>
    Task<IReadOnlyList<AwosAirport>> ElencoAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAwosService"/>
public sealed class AwosService : IAwosService
{
    private readonly IAirportEditingService _scali;
    private readonly IDocumentAdminService _documenti;
    private readonly IWeatherProvider _meteo;
    private readonly IOnlineAtcProvider _online;
    private readonly IFrozenSectionReader _congelate;

    /// <summary>
    /// L'elenco dei documenti, letto <b>una volta per richiesta</b>.
    ///
    /// <para>⚠️ Questo servizio è <c>Scoped</c>, quindi la memoria dura quanto la richiesta e non un minuto
    /// di più: non è una cache con un problema di freschezza, è la stessa domanda posta due volte nello
    /// stesso istante. E veniva posta due volte davvero — la pagina chiama <c>ElencoAsync</c> per la tendina
    /// e <c>BuildAsync</c> per il cancello, e ognuna si leggeva TUTTI i documenti (revisione del 12 settembre
    /// 2026, sera). Il progetto ha già pagato «otto interrogazioni per pagina» sull'elenco aeroporti.</para>
    /// </summary>
    private IReadOnlyList<ManagedDoc>? _documentiLetti;

    public AwosService(IAirportEditingService scali, IDocumentAdminService documenti, IWeatherProvider meteo,
                       IOnlineAtcProvider online, IFrozenSectionReader congelate)
    {
        _scali = scali;
        _documenti = documenti;
        _meteo = meteo;
        _online = online;
        _congelate = congelate;
    }

    public async Task<AwosResult> BuildAsync(string icao, bool perEditor, string? metarDiProva = null,
                                             bool giaInVigore = false, CancellationToken ct = default)
    {
        var id = (icao ?? "").Trim().ToUpperInvariant();
        if (id.Length != 4) return AwosResult.Ignoto;

        var scalo = await _scali.LoadForViewAsync(id, ct);
        if (scalo is null) return AwosResult.Ignoto;

        var (vipi, vsop) = AwosGate.Pubblicati(await DocumentiAsync(ct), id);
        if (!vipi && !vsop && !perEditor) return AwosResult.NonPubblicato;

        // Il METAR di prova NON passa dal provider: deve poter descrivere un tempo che non c'è, ed è tutto il
        // motivo per cui esiste. La provenienza in quel caso non si scrive: non viene da nessuna sorgente.
        WeatherReport? bollettino = null;
        string? raw = metarDiProva?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            bollettino = await _meteo.GetAsync(id, ct);
            raw = bollettino.Metar;
        }

        var metar = string.IsNullOrWhiteSpace(raw) ? null : MetarParser.ParseMetar(raw!);

        var piste = AwosComposition.Strisce(scalo.Runways);
        var identificativi = scalo.Runways
            .Select(r => (r.Ident ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var atis = AwosGate.Atis(_online.GetCurrent().Details, id);
        var (regole, minimiLvp, escluse) = await DalPubblicatoAsync(id, vipi, vsop, scalo, ct);
        var attiva = AwosComposition.PistaAttiva(regole, identificativi, metar,
            AwosGate.Piste(atis?.PistePartenza), AwosGate.Piste(atis?.PisteArrivo), atis?.Callsign, escluse);

        return new AwosResult(new AwosView(
            Icao: id,
            Nome: scalo.Name,
            AccCode: scalo.AccCode,
            HaVipi: vipi,
            HaVsop: vsop,
            MetarRaw: raw,
            MetarSource: metarDiProva is null ? bollettino?.MetarSource : null,
            MetarAsOf: metarDiProva is null ? bollettino?.AsOf : null,
            Metar: metar,
            TransitionAltitudeFt: scalo.TransitionAltitudeFt,
            TransitionLevel: AwosComposition.TransitionLevel(scalo.TransitionLevels, metar?.QnhHpa),
            Piste: piste,
            Attiva: attiva,
            Atis: atis,
            Lvp: ValutaLvp(minimiLvp, metar, giaInVigore),
            AsOf: DateTimeOffset.UtcNow,
            MetarStation: metarDiProva is null ? bollettino?.Stazione : null), AwosOutcome.Ok);
    }

    /// <summary>
    /// Regole di scelta pista, minimi LVP e soglie escluse dal ripiego su cui decidere: quelli della <b>release
    /// pubblicata</b>, non i vivi.
    ///
    /// <para>⚠️ Le esclusioni («mai in partenza» / «mai in arrivo», carta 2026-09-17-pista-mai-usare.md) vengono dalla
    /// sezione Piste congelata, con la stessa regola delle regole: una casella spuntata nell'editor e non pubblicata
    /// non deve cambiare la pista sul quadro prima che la cambi nel documento.</para>
    ///
    /// <para>🔴 Fino al 15 settembre 2026 il quadro leggeva l'anagrafica viva: una regola o un minimo scritti
    /// nell'editor e non ancora pubblicati cambiavano subito la pista in uso e lo stato LVP sul quadro pubblico.
    /// Decisione del committente: il quadro legge quel che legge il documento.</para>
    ///
    /// <para>⚠️ La STESSA regola della vIPI (<c>PistaInUso</c>, <c>AirportViewDerivationService</c>): la sezione
    /// congelata della release in vigore se c'è; altrimenti i vivi. «Altrimenti» comprende la sezione in
    /// <b>Live</b> — lo snapshot non la porta, e allora un cambiamento nell'editor arriva sul quadro subito, come
    /// nel documento — e una release di prima del 12 settembre 2026 senza le regole in forma calcolabile.
    /// Edizione: la vIPI civile se pubblicata, il vSOP militare sui campi che hanno solo quello. Nessun documento
    /// pubblicato (lo apre un Editor per provarlo): i vivi, non c'è altro.</para>
    ///
    /// <para>⚠️ Una sezione LVP congelata SENZA minimi (<c>Minimi</c> null) vale «pubblicata senza minimi», e il
    /// quadro ricade sullo standard dichiarandolo: NON si torna ai vivi, che sarebbero proprio i non pubblicati.</para>
    /// </summary>
    private async Task<(IReadOnlyList<RunwayRuleRow> Regole, LvpRow? Lvp, RunwayExclusions Escluse)> DalPubblicatoAsync(
        string icao, bool vipi, bool vsop, AirportData scalo, CancellationToken ct)
    {
        var escluseVive = RunwayRow.Esclusioni(scalo.Runways);
        if (!vipi && !vsop) return (scalo.Rules, scalo.Lvp, escluseVive);
        var edizione = vipi ? ReleaseTargetType.Airport : ReleaseTargetType.AirportMil;
        var snapshot = await _congelate.LoadAsync(edizione, icao, ct);   // una lettura per tutte
        var regole = snapshot.Get<AirportRulesView>("runwayrules")?.Regole ?? scalo.Rules;
        var lvp = snapshot.Get<AirportLvpView>("lvp") is { } congelata ? congelata.Minimi : scalo.Lvp;
        var escluse = snapshot.Get<AirportRunwaysView>("runways") is { } piste
            ? AirportRunwayRowView.Esclusioni(piste.Rows) : escluseVive;
        return (regole, lvp, escluse);
    }

    public async Task<IReadOnlyList<AwosAirport>> ElencoAsync(CancellationToken ct = default) =>
        AwosGate.Elenco(await DocumentiAsync(ct));

    /// <summary>
    /// Lo stato LVP suggerito, sui minimi <b>pubblicati</b> dello scalo (<see cref="DalPubblicatoAsync"/>) e sul METAR di adesso, e la memoria da
    /// rimandare al giro dopo. Le due regole stanno nel valutatore (T-077, T-009): qui non se ne riscrive
    /// nessuna, e il JavaScript non ne decide nessuna.
    /// </summary>
    private static AwosLvp ValutaLvp(LvpRow? minimi, ParsedMetar? metar, bool giaInVigore)
    {
        var valutazione = LvpValutatore.DaMetar(minimi, metar, giaInVigore);
        return new AwosLvp(valutazione, minimi, LvpValutatore.MemoriaDopo(valutazione.Stato, giaInVigore));
    }

    private async Task<IReadOnlyList<ManagedDoc>> DocumentiAsync(CancellationToken ct) =>
        _documentiLetti ??= await _documenti.ListAsync(ct);
}

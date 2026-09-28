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
    private readonly IPisteDalPubblicato _pubblicato;

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
                       IOnlineAtcProvider online, IPisteDalPubblicato pubblicato)
    {
        _scali = scali;
        _documenti = documenti;
        _meteo = meteo;
        _online = online;
        _pubblicato = pubblicato;
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

        // 🔴 U-092: l'età del bollettino si misura dal METAR, non dalla risposta. Oltre 90 minuti il quadro lo mostra
        // ma non ci decide sopra né LVP né pista. Il METAR di prova (staff) è per definizione «adesso».
        var adesso = DateTimeOffset.UtcNow;
        var osservato = metarDiProva is null ? MetarParser.OraOsservazione(metar?.TimeRaw, adesso) : null;
        var vecchio = AwosComposition.MetarVecchio(osservato, adesso);
        var perDecidere = vecchio ? null : metar;

        var piste = AwosComposition.Strisce(scalo.Runways);
        var identificativi = scalo.Runways
            .Select(r => (r.Ident ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var atis = AwosGate.Atis(_online.GetCurrent().Details, id);
        // Regole, minimi LVP e soglie escluse: dal documento PUBBLICATO (la porta è condivisa con vista rapida ed
        // elenco aeroporti, vedi IPisteDalPubblicato).
        var (regole, minimiLvp, escluse, transizioneCongelata) = await _pubblicato.PerScaloAsync(id, scalo.Rules, scalo.Lvp, scalo.Runways,
            await DocumentiAsync(ct), ct);
        var attiva = AwosComposition.PistaAttiva(regole, identificativi, perDecidere,
            AwosGate.Piste(atis?.PistePartenza), AwosGate.Piste(atis?.PisteArrivo), atis?.Callsign, escluse,
            RunwayRow.Rotte(scalo.Runways));   // la rotta del pannello vento, anche per regole e ripiego (U-223)

        // 🔴 U-227: TA e TL dalla sezione pubblicata, come regole e LVP; senza, la proiezione dei vivi — la stessa del
        // documento, e la stessa funzione per leggere la fascia.
        var transizione = transizioneCongelata ?? AirportSectionProjection.Transition(scalo);

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
            TransitionAltitudeFt: transizione.TransitionAltitudeFt,
            TransitionLevel: LivelloDiTransizione.Adesso(transizione, metar?.QnhHpa),
            Piste: piste,
            Attiva: attiva,
            Atis: atis,
            Lvp: ValutaLvp(minimiLvp, perDecidere, giaInVigore),
            AsOf: adesso,
            MetarStation: metarDiProva is null ? bollettino?.Stazione : null,
            MetarObservedUtc: osservato,
            MetarStale: vecchio), AwosOutcome.Ok);
    }


    public async Task<IReadOnlyList<AwosAirport>> ElencoAsync(CancellationToken ct = default) =>
        AwosGate.Elenco(await DocumentiAsync(ct));

    /// <summary>
    /// Lo stato LVP suggerito, sui minimi <b>pubblicati</b> dello scalo (<see cref="IPisteDalPubblicato"/>) e sul METAR di adesso, e la memoria da
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

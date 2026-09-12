using Vipi.Application.Content;
using Vipi.Application.Weather;

namespace Vipi.Application.Awos;

/// <summary>Perché il quadro non si apre. <see cref="Ok"/> = si apre.</summary>
public enum AwosOutcome
{
    Ok,
    /// <summary>ICAO che l'anagrafica non conosce.</summary>
    ScaloIgnoto,
    /// <summary>Lo scalo c'è, ma nessuno dei suoi documenti è pubblicato: al pubblico non esiste.</summary>
    NonPubblicato,
}

/// <summary>Esito della composizione: la vista, oppure il motivo per cui non c'è.</summary>
public sealed record AwosResult(AwosView? Vista, AwosOutcome Esito)
{
    public static AwosResult Ignoto => new(null, AwosOutcome.ScaloIgnoto);
    public static AwosResult NonPubblicato => new(null, AwosOutcome.NonPubblicato);
}

/// <summary>
/// L'ATIS in onda sullo scalo, se qualcuno lo trasmette.
/// </summary>
/// <param name="Callsign">Chi lo trasmette: è la risposta a «chi l'ha detto», e il quadro la scrive.</param>
public sealed record AwosAtis(string Callsign, string? Lettera, string? Orario, string? Testo,
                              string? PisteArrivo, string? PistePartenza);

/// <summary>
/// Lo stato LVP del quadro: quello suggerito dai minimi, e i minimi stessi per mostrarli.
///
/// <para>⚠️ Il quadro legge i minimi <b>VIVI</b>, non quelli di una release: non è un documento e non ha una
/// release — è uno strumento, come il vento. Dove il documento pubblicato e il quadro divergessero,
/// l'autorità è il documento.</para>
/// </summary>
public sealed record AwosLvp(LvpValutazione Valutazione, LvpRow? Minimi);

/// <summary>Uno scalo che il quadro sa aprire, per il selettore.</summary>
public sealed record AwosAirport(string Icao, string Nome, bool HaVipi, bool HaVsop);

/// <summary>Chi ha deciso la pista attiva. L'ordine è quello di precedenza (carta §4.5).</summary>
public enum AwosRunwaySource
{
    /// <summary>Nessuna: vento calmo o sconosciuto e nessuna regola applicabile.</summary>
    Nessuna,
    /// <summary>L'ATIS di chi presiede lo scalo — la verità operativa del momento.</summary>
    Atis,
    /// <summary>Una regola di scelta pista dello scalo.</summary>
    Regola,
    /// <summary>Il massimo vento di testa, quando non c'è nient'altro.</summary>
    Vento,
}

/// <summary>
/// Una testata di pista, con quel che serve a calcolarci sopra il vento.
/// </summary>
/// <param name="HeadingDeg">
/// La rotta <b>vera</b> della testata, dall'anagrafica IVAO.
/// <para>⚠️ Vera e non magnetica, ed è la cosa giusta proprio qui: il vento del METAR è riferito al nord
/// <b>vero</b>, e traverso e coda si calcolano fra due angoli che devono avere lo stesso nord. Il prototipo
/// usava gli heading del sectorfile, che sono magnetici, e sbagliava il traverso di tutta la declinazione.</para>
/// </param>
/// <param name="ThresholdElevationFt">Elevazione della soglia, quando la sorgente l'ha mandata: serve al QFE.</param>
public sealed record AwosEnd(string Ident, int HeadingDeg, int? ThresholdElevationFt);

/// <summary>
/// Una <b>striscia</b> del quadro: una pista fisica, cioè le sue due testate.
///
/// <para>⚠️ <see cref="Right"/> può essere null, e non è un caso di scuola: nell'anagrafica capita una testata
/// senza l'opposta (una sola pubblicata, o un ident scritto a metà). Il quadro la disegna sola invece di
/// inventarle una gemella a +180°, che sarebbe una pista che non esiste.</para>
/// </summary>
public sealed record AwosStrip(AwosEnd Left, AwosEnd? Right);

/// <summary>
/// La pista in uso adesso, e <b>chi l'ha decisa</b>.
/// <para>⚠️ <see cref="Dettaglio"/> non è decorazione: un quadro che dice la pista senza dire da dove viene è
/// la ragione per cui quello del prototipo va girato a mano.</para>
/// </summary>
public sealed record AwosActive(string? Dep, string? Arr, AwosRunwaySource Sorgente, string? Dettaglio)
{
    public static readonly AwosActive Nessuna = new(null, null, AwosRunwaySource.Nessuna, null);
}

/// <summary>
/// Tutto quel che il quadro vAWOS mostra di uno scalo, in un istante.
///
/// <para>⚠️ Attraversa il confine verso la pagina <b>serializzato in JSON</b> (l'endpoint che il modulo JS
/// rilegge ogni 60 s): niente tipi che non reggano il giro andata/ritorno.</para>
/// </summary>
/// <param name="MetarSource">Chi ha dato il METAR quando non è la sorgente principale; null = NOAA, il caso normale.</param>
/// <param name="TransitionLevel">Il TL della fascia di QNH corrente, dalla tabella dello scalo. Null = non calcolabile.</param>
/// <param name="AsOf">Quando è stata composta: è ciò che fa <b>invecchiare</b> il quadro a schermo se il server smette di rispondere.</param>
public sealed record AwosView(
    string Icao,
    string Nome,
    string AccCode,
    bool HaVipi,
    bool HaVsop,
    string? MetarRaw,
    string? MetarSource,
    DateTimeOffset? MetarAsOf,
    ParsedMetar? Metar,
    int? TransitionAltitudeFt,
    string? TransitionLevel,
    IReadOnlyList<AwosStrip> Piste,
    AwosActive Attiva,
    AwosAtis? Atis,
    AwosLvp Lvp,
    DateTimeOffset AsOf);

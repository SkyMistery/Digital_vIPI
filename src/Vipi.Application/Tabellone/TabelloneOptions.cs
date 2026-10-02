namespace Vipi.Application.Tabellone;

/// <summary>
/// Configurazione del tabellone partenze/arrivi (sezione «Tabellone»). Carta
/// <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c> §4.
///
/// <code>
/// "Tabellone": {
///   "BookingUrl": "https://booking.it.ivao.aero/api/flights",
///   "BookingApiKey": "…",                       ← SOLO nei segreti del server: il repo è pubblico
///   "Scali": {
///     "LIRF": { "Nome": "ROMA FIUMICINO", "EventoRfo": "lirf-20261003", "NomeEvento": "ROMA RFE", "Voli": "tutti" }
///   }
/// }
/// </code>
///
/// <para>🔴 <b>Un dizionario per ICAO, non un array</b>: il binder somma gli array delle diverse sorgenti
/// (appsettings + segreti) invece di sostituirli (§A62). Con i nomi la stessa voce in due file si sovrascrive.</para>
///
/// <para>Uno scalo che non è in <see cref="Scali"/> non ha tabellone: 404.</para>
/// </summary>
public sealed class TabelloneOptions
{
    public const string SectionName = "Tabellone";

    /// <summary>Il booking di IVAO Italia: restituisce l'evento in corso, senza date nel percorso.</summary>
    public string BookingUrl { get; set; } = "https://booking.it.ivao.aero/api/flights";

    /// <summary>La chiave del booking (header <c>x-api-key</c>). Vuota = niente booking: il tabellone va avanti
    /// col solo Whazzup e <c>fonti.booking.ok</c> vale false.</summary>
    public string BookingApiKey { get; set; } = "";

    public Dictionary<string, TabelloneScaloOptions> Scali { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TabelloneScaloOptions
{
    /// <summary>Il nome sul tabellone (<c>ROMA FIUMICINO</c>). Vuoto = la città dell'anagrafica, poi l'ICAO.</summary>
    public string? Nome { get; set; }

    /// <summary>Il fuso dello scalo, per l'ora locale che mostra il tabellone.</summary>
    public string FusoOrario { get; set; } = "Europe/Rome";

    /// <summary>Il documento del ponte RFO da cui leggere lo stand deciso dal Gate Manager (<c>lirf-20261003</c>).
    /// Vuoto = nessun evento RFO: <c>fonti.gateManager.ok</c> vale false e lo stand è quello del booking.</summary>
    public string? EventoRfo { get; set; }

    /// <summary>Il nome dell'evento sul tabellone (<c>ROMA RFE</c>). Il booking non lo dice.</summary>
    public string? NomeEvento { get; set; }

    /// <summary><c>tutti</c> (predefinito) o <c>prenotati</c>. Fuori dagli eventi vale sempre <c>tutti</c>.</summary>
    public string Voli { get; set; } = RegoleTabellone.VoliTutti;
}

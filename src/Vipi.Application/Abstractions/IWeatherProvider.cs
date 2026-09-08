namespace Vipi.Application.Abstractions;

/// <summary>Bollettino meteo di un aeroporto (METAR + TAF grezzi). F3 dati reali.</summary>
/// <param name="MetarSource">
/// Chi ha dato il METAR, quando non è la sorgente principale. <c>null</c> = la principale (NOAA), ed è il
/// caso normale: un'etichetta che c'è sempre non si legge più.
///
/// <para>⚠️ Serve a due cose diverse, e nessuna delle due è decorazione. A chi legge dice da dove viene il
/// dato che sta guardando — è meteo, e la provenienza è parte del dato. A chi verifica dice che la ricaduta
/// <b>ha funzionato davvero</b>: senza, una sorgente di scorta che non risponde più è indistinguibile da una
/// che non è mai servita, e nessuno se ne accorge finché non serve.</para>
/// </param>
public sealed record WeatherReport(
    string Icao, string? Metar, string? Taf, DateTimeOffset? AsOf, string? MetarSource = null)
{
    public bool HasData => !string.IsNullOrWhiteSpace(Metar) || !string.IsNullOrWhiteSpace(Taf);
    public static WeatherReport Empty(string icao) => new(icao, null, null, null);
}

/// <summary>
/// Sorgente METAR di <b>scorta</b>, interrogata solo quando la principale non dà il bollettino.
///
/// <para>⚠️ Solo METAR, e non è una semplificazione: un TAF di scorta gratuito e indipendente da NOAA non
/// esiste — provati l'8 settembre 2026 — e una porta che promette un TAF che nessuno può dare sarebbe una
/// promessa scritta nel codice.</para>
/// </summary>
public interface IMetarFallback
{
    /// <summary>Il METAR grezzo, o <c>null</c> se questa sorgente non ce l'ha. Non alza mai: chi chiama ha già
    /// un buco da riempire, e un'eccezione qui lo trasformerebbe in un guasto.</summary>
    Task<string?> GetMetarAsync(string icao, CancellationToken ct = default);

    /// <summary>Come si chiama la sorgente, per dirlo a chi legge (finisce in <see cref="WeatherReport.MetarSource"/>).</summary>
    string Nome { get; }
}

/// <summary>
/// Porta verso una sorgente METAR/TAF reale (impl. NOAA aviationweather.gov in Infrastructure).
/// Con cache TTL: il METAR aggiorna ~ogni ora, niente senso interrogare a ogni render.
/// </summary>
public interface IWeatherProvider
{
    Task<WeatherReport> GetAsync(string icao, CancellationToken ct = default);
}

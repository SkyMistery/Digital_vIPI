using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Weather;

/// <summary>
/// Il provider meteo che tutti ricevono: chiede il bollettino alla <b>stazione di riferimento</b> dello scalo, se
/// ce n'è una (15 settembre 2026, committente: LIRJ non emette un METAR suo).
///
/// <para>⚠️ Sta QUI, davanti alla porta che c'è già, e non in ognuno dei sei posti che chiedono il meteo (vIPI,
/// vSOP, vAWOS, i due pannelli, la pagina dello scalo): una regola scritta sei volte si applica in quattro, e la
/// pista in uso di un documento si deciderebbe su un vento diverso da quello che il suo riquadro mostra.</para>
///
/// <para>Il bollettino torna col NOME DELLO SCALO chiesto (<see cref="WeatherReport.Icao"/>) e la stazione in
/// <see cref="WeatherReport.Stazione"/>: chi lo mostra deve poter dire «questo è il METAR di LIRP».</para>
/// </summary>
public sealed class MeteoConStazioneDiRiferimento : IWeatherProvider
{
    private readonly NoaaWeatherClient _meteo;
    private readonly IStazioniMeteo _stazioni;

    public MeteoConStazioneDiRiferimento(NoaaWeatherClient meteo, IStazioniMeteo stazioni)
    {
        _meteo = meteo;
        _stazioni = stazioni;
    }

    public async Task<WeatherReport> GetAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao)) return await _meteo.GetAsync(icao, ct).ConfigureAwait(false);
        var scalo = icao.Trim().ToUpperInvariant();

        var rif = await _stazioni.RiferimentoDiAsync(scalo, ct).ConfigureAwait(false);
        if (rif is null || string.Equals(rif, scalo, StringComparison.OrdinalIgnoreCase))
            return await _meteo.GetAsync(scalo, ct).ConfigureAwait(false);

        var bollettino = await _meteo.GetAsync(rif, ct).ConfigureAwait(false);
        return bollettino with { Icao = scalo, Stazione = rif };
    }
}

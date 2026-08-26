namespace Vipi.Application.Content;

/// <summary>
/// Un <b>aeroporto</b> che la sorgente non nomina più. Gemello di <see cref="StaleCatalogRow"/>, e nato
/// insieme al timbro che lo rende possibile (<c>Airport.LastSeenAtUtc</c>, 26 agosto 2026).
///
/// <para>⚠️ Prima non c'era modo di accorgersene: l'anagrafica aeroporti è <b>additiva</b> — assegna gli
/// ICAO nuovi e salta quelli già in archivio — quindi uno scalo che IVAO smetteva di elencare restava lì,
/// identico a uno confermato stanotte, con la sua pagina pubblica e i suoi settori.</para>
/// </summary>
/// <param name="AirportId">Serve al tasto: è il bersaglio dell'eliminazione.</param>
/// <param name="Settori">Quanti settori porta con sé: è il prezzo dell'eliminazione, e va visto prima.</param>
/// <param name="HaDocumento">Se ha una vIPI, che va eliminata prima (e quindi letta prima).</param>
public sealed record StaleAirportRow(
    int AirportId, string Icao, string Name, string AccCode, DateTime? LastSeenUtc,
    int Settori, bool HaDocumento)
{
    /// <summary>Da quanti giorni la sorgente non lo manda più. <c>null</c> se non è mai stato timbrato.</summary>
    public int? DaGiorni => LastSeenUtc is { } t ? (int)(DateTime.UtcNow - t).TotalDays : null;
}

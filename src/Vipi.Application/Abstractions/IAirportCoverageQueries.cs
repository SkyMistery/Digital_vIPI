namespace Vipi.Application.Abstractions;

/// <summary>
/// Una riga della tabella «Aeroporti»: quanto traffico c'è stato su un campo, e quanto ha trovato acceso.
/// </summary>
/// <param name="Movements">Arrivi più partenze. I sorvoli restano fuori: non sono traffico <i>di</i> quel campo.</param>
/// <param name="Covered">Movimenti caduti in un istante in cui una posizione del campo era aperta.</param>
/// <param name="AtcMinutes">Minuti di apertura del campo, a intervalli uniti.</param>
/// <param name="WindowMinutes">Minuti del periodo di cui abbiamo davvero il dato: è il denominatore onesto.</param>
public sealed record AirportCoverageRow(
    string Icao, string Name, string AccCode,
    int Movements, int Covered, int AtcMinutes, int WindowMinutes)
{
    /// <summary>Quota di traffico che ha trovato un controllore, 0-100; <c>null</c> se non c'è stato traffico.</summary>
    public int? CoveredPercent =>
        Movements <= 0 ? null : (int)Math.Round(Covered * 100.0 / Movements);

    /// <summary>Quota di tempo in cui il campo era aperto, 0-100; <c>null</c> se il periodo è vuoto.</summary>
    public int? OpenPercent =>
        WindowMinutes <= 0 ? null : (int)Math.Round(AtcMinutes * 100.0 / WindowMinutes);
}

/// <summary>Il totale della tabella, più quel che serve a dire fin dove arriva il dato.</summary>
/// <param name="FirstDay">Primo giorno consolidato nel periodo; <c>null</c> = non c'è niente.</param>
/// <param name="DaysCovered">Quanti giorni distinti sono stati consolidati: dice se il recupero è finito.</param>
public sealed record AirportCoverageSummary(
    IReadOnlyList<AirportCoverageRow> Rows,
    int Movements, int Covered, int AtcMinutes,
    DateTimeOffset? FirstDay, int DaysCovered, int DaysAsked);

/// <summary>
/// «Quanto traffico c'è sui campi italiani, e quanto ne copriamo». Porta di sola lettura.
///
/// <para>⚠️ Legge <b>solo</b> quel che il lavoro notturno ha consolidato: un periodo non ancora recuperato
/// non è «zero traffico», è «non lo sappiamo ancora» — e chi rende questi numeri deve saper dire quale dei
/// due casi ha davanti (<c>DaysCovered</c> contro <c>DaysAsked</c>).</para>
/// </summary>
public interface IAirportCoverageQueries
{
    /// <param name="accCode">Codice ACC per restringere il gruppo (LIRR, LIMM, LIPP, LIBB); <c>null</c> = tutti.</param>
    Task<AirportCoverageSummary> ByAirportAsync(
        DateTimeOffset from, DateTimeOffset to, string? accCode = null, CancellationToken ct = default);

    /// <summary>I gruppi disponibili: codice ACC e quanti aeroporti ci stanno dentro.</summary>
    Task<IReadOnlyList<(string Code, string Name, int Airports)>> GroupsAsync(CancellationToken ct = default);
}

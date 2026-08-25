using Vipi.Application.Stats;

namespace Vipi.Application.Abstractions;

/// <summary>Un'apertura ATC su un campo: chi apre non importa, importa quando era aperto.</summary>
public sealed record AirportAtcOpening(string Icao, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>Il conto di un giorno, pronto da scrivere.</summary>
public sealed record AirportDayCount(
    string Icao, DateTimeOffset Day, int Inbound, int Outbound, int Overflight, int Covered, int AtcMinutes);

/// <summary>
/// L'archivio del traffico d'aeroporto consolidato: quel poco che serve al lavoro notturno.
///
/// <para>Volutamente <b>senza logica</b>: che cosa chiedere alla sorgente lo decide
/// <see cref="AirportRollupPlanner"/>, quanto è coperto lo decide <see cref="AirportCoverage"/>. Qui si
/// legge e si scrive, e basta — così le due decisioni restano provabili senza un database.</para>
/// </summary>
public interface IAirportTrafficRollupStore
{
    /// <summary>
    /// Gli aeroporti da consolidare: i campi <b>italiani non nascosti</b>, in ordine stabile.
    ///
    /// <para>⚠️ Tutti, non i soli che hanno visto un controllore: un aeroporto dove non è mai stato aperto
    /// niente è esattamente il caso che questa pagina deve saper mostrare.</para>
    /// </summary>
    Task<IReadOnlyList<string>> AirportsAsync(CancellationToken ct = default);

    /// <summary>I giorni già consolidati nella finestra, con l'istante in cui sono stati presi.</summary>
    Task<IReadOnlyList<KnownAirportDay>> KnownDaysAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>
    /// Le connessioni ATC <b>d'aeroporto</b> della finestra, già ridotte a (campo, inizio, fine).
    ///
    /// <para>⚠️ Il campo si legge dal <b>callsign</b> (<c>LIRF_TWR</c> → <c>LIRF</c>), e solo per le
    /// posizioni che ne dichiarano uno: <c>LIRR_NE1_CTR</c> comincia per <c>LIRR</c>, che è una FIR.</para>
    /// </summary>
    Task<IReadOnlyList<AirportAtcOpening>> AtcOpeningsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>Scrive o riscrive i giorni consolidati. Ritorna quante righe sono state toccate.</summary>
    Task<int> SaveAsync(
        IReadOnlyList<AirportDayCount> righe, DateTimeOffset now, CancellationToken ct = default);
}

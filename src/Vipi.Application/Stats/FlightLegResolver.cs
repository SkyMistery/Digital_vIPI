using System;
using System.Collections.Generic;

namespace Vipi.Application.Stats;

/// <summary>Una tratta già aperta dentro una sessione ATC: è la riga di traffico che il poller aggiorna.</summary>
/// <param name="PilotCallsign">Callsign del pilota.</param>
/// <param name="DepIcao">Aeroporto di partenza dal piano di volo (può mancare).</param>
/// <param name="ArrIcao">Aeroporto di arrivo dal piano di volo (può mancare).</param>
/// <param name="Ordinal">Progressivo della tratta per quel callsign dentro la sessione (1, 2, 3…).</param>
/// <param name="LastSeenUtc">Ultimo avvistamento: da qui si misura il buco.</param>
public sealed record OpenLeg(string PilotCallsign, string? DepIcao, string? ArrIcao, int Ordinal, DateTimeOffset LastSeenUtc);

/// <summary>
/// A quale <b>tratta</b> appartiene un avvistamento. Risponde alle due domande che rompono un conteggio fatto
/// per solo callsign:
///
/// <list type="number">
///   <item><b>Il pilota cade e rientra nello stesso volo.</b> IVAO gli dà una sessione nuova con id nuovo,
///     ma callsign e piano di volo sono gli stessi: deve restare <b>una</b> tratta, altrimenti un'ora di
///     connessione ballerina raddoppia i movimenti gestiti. Per questo la tratta NON è mai identificata
///     dall'id di sessione del pilota.</item>
///   <item><b>Il pilota fa più voli senza disconnettersi.</b> LIRF→LIRN e poi LIRN→LIRF con lo stesso
///     callsign sono <b>due</b> movimenti: se la chiave fosse il solo callsign ne conteremmo uno.</item>
/// </list>
///
/// <para>Regola: stessa tratta se callsign, partenza e arrivo coincidono <b>e</b> il buco dall'ultimo
/// avvistamento sta sotto la soglia. Il buco serve per la tratta ripetuta identica (navetta che rifà la
/// stessa rotta, circuiti di addestramento): senza, due giri sulla stessa rotta sarebbero un movimento solo.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class FlightLegResolver
{
    /// <summary>Buco oltre il quale un avvistamento apre una tratta nuova invece di continuare quella aperta.</summary>
    public static readonly TimeSpan DefaultGap = TimeSpan.FromMinutes(30);

    /// <summary>
    /// La tratta a cui attaccare l'avvistamento, o <c>null</c> se va aperta una tratta nuova
    /// (usare <see cref="NextOrdinal"/> per il progressivo).
    /// </summary>
    public static OpenLeg? Match(
        IReadOnlyList<OpenLeg> open, string pilotCallsign, string? depIcao, string? arrIcao,
        DateTimeOffset atUtc, TimeSpan? gap = null)
    {
        var soglia = gap ?? DefaultGap;
        OpenLeg? best = null;

        foreach (var leg in open)
        {
            if (!Same(leg.PilotCallsign, pilotCallsign)) continue;
            if (!Same(leg.DepIcao, depIcao) || !Same(leg.ArrIcao, arrIcao)) continue;
            if (atUtc - leg.LastSeenUtc > soglia) continue;

            // Più tratte compatibili non dovrebbero esistere; se capita vince la più recente.
            if (best is null || leg.LastSeenUtc > best.LastSeenUtc) best = leg;
        }
        return best;
    }

    /// <summary>Progressivo della prossima tratta di quel pilota dentro la sessione (parte da 1).</summary>
    public static int NextOrdinal(IReadOnlyList<OpenLeg> open, string pilotCallsign)
    {
        var max = 0;
        foreach (var leg in open)
            if (Same(leg.PilotCallsign, pilotCallsign) && leg.Ordinal > max)
                max = leg.Ordinal;
        return max + 1;
    }

    private static bool Same(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}

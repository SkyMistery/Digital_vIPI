using System;
using System.Collections.Generic;

namespace Vipi.Application.Stats;

/// <summary>Una tratta già aperta dentro una sessione ATC: è la riga di traffico che il poller aggiorna.</summary>
/// <param name="PilotCallsign">Callsign del pilota.</param>
/// <param name="FlightPlanId">Id del piano di volo IVAO, se ce n'è uno: l'identità più forte della tratta.</param>
/// <param name="DepIcao">Aeroporto di partenza dal piano di volo (può mancare).</param>
/// <param name="ArrIcao">Aeroporto di arrivo dal piano di volo (può mancare).</param>
/// <param name="Ordinal">Progressivo della tratta per quel callsign dentro la sessione (1, 2, 3…).</param>
/// <param name="LastSeenUtc">Ultimo avvistamento: da qui si misura il buco.</param>
public sealed record OpenLeg(
    string PilotCallsign, long? FlightPlanId, string? DepIcao, string? ArrIcao, int Ordinal, DateTimeOffset LastSeenUtc);

/// <summary>
/// A quale <b>tratta</b> appartiene un avvistamento. Risponde alle domande che rompono un conteggio fatto
/// per solo callsign:
///
/// <list type="number">
///   <item><b>Il pilota cade e rientra nello stesso volo.</b> IVAO gli dà una sessione nuova con id nuovo,
///     ma callsign e piano di volo sono gli stessi: deve restare <b>una</b> tratta, altrimenti un'ora di
///     connessione ballerina raddoppia i movimenti gestiti. Per questo la tratta NON è mai identificata
///     dall'id di sessione del pilota.</item>
///   <item><b>Il pilota fa più voli senza disconnettersi.</b> LIRF→LIRN e poi LIRN→LIRF con lo stesso
///     callsign sono <b>due</b> movimenti: se la chiave fosse il solo callsign ne conteremmo uno.</item>
///   <item><b>Il poller si ferma</b> (riavvio, deploy, rete): al ritorno l'aereo è ancora lì, in mezzo allo
///     stesso volo. Il buco è nostro, non suo — e non deve diventare una tratta nuova.</item>
/// </list>
///
/// <para><b>Identità, in ordine di forza.</b> Se entrambi gli avvistamenti hanno un piano di volo, decide
/// l'<b>id del piano</b>: uguale = stessa tratta (anche dopo ore di buco: è il paracadute per il poller
/// fermo), diverso = tratta nuova (anche a distanza di un minuto: si è rifilato per la gamba dopo).
/// Senza piano di volo — il VFR che non lo deposita — si ripiega su callsign + partenza/arrivo + un buco
/// massimo, che serve alla tratta ripetuta identica (navette, circuiti di addestramento).</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class FlightLegResolver
{
    /// <summary>Buco oltre il quale un avvistamento <b>senza piano di volo</b> apre una tratta nuova.</summary>
    public static readonly TimeSpan DefaultGap = TimeSpan.FromMinutes(30);

    /// <summary>
    /// La tratta a cui attaccare l'avvistamento, o <c>null</c> se va aperta una tratta nuova
    /// (usare <see cref="NextOrdinal"/> per il progressivo).
    /// </summary>
    public static OpenLeg? Match(
        IReadOnlyList<OpenLeg> open, string pilotCallsign, long? flightPlanId,
        string? depIcao, string? arrIcao, DateTimeOffset atUtc, TimeSpan? gap = null)
    {
        var soglia = gap ?? DefaultGap;
        OpenLeg? best = null;

        foreach (var leg in open)
        {
            if (!Same(leg.PilotCallsign, pilotCallsign)) continue;
            if (!IsSameLeg(leg, flightPlanId, depIcao, arrIcao, atUtc, soglia)) continue;

            // Più tratte compatibili non dovrebbero esistere; se capita vince la più recente.
            if (best is null || leg.LastSeenUtc > best.LastSeenUtc) best = leg;
        }
        return best;
    }

    private static bool IsSameLeg(
        OpenLeg leg, long? flightPlanId, string? depIcao, string? arrIcao, DateTimeOffset atUtc, TimeSpan gap)
    {
        // Il piano di volo, quando c'è da entrambe le parti, è la parola definitiva: niente soglia temporale.
        if (leg.FlightPlanId is { } aperto && flightPlanId is { } nuovo)
            return aperto == nuovo;

        if (!Same(leg.DepIcao, depIcao) || !Same(leg.ArrIcao, arrIcao)) return false;
        return atUtc - leg.LastSeenUtc <= gap;
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

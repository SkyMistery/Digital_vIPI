using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>
/// Che cosa è stato, per te, un aeroplano che ti è passato davanti. Ogni voce diventa una <b>targhetta</b>
/// nella riga del traffico gestito.
///
/// <para>⚠️ Le voci di <b>fase</b> (<see cref="TookOff"/>, <see cref="Landed"/>, <see cref="AtGate"/>…) dicono
/// quel che abbiamo <b>visto</b>, non quel che è successo: se il volo è uscito dall'area ancora in volo,
/// «atterrato» non si scrive nemmeno quando il piano di volo diceva che veniva da noi.</para>
/// </summary>
public enum TrafficTag
{
    /// <summary>Il piano di volo parte dal campo di questa postazione.</summary>
    Departure,

    /// <summary>Il piano di volo arriva al campo di questa postazione.</summary>
    Arrival,

    /// <summary>Né parte né arriva in divisione: sta attraversando.</summary>
    Overflight,

    /// <summary>Visto a terra e poi in volo: il decollo è nostro.</summary>
    TookOff,

    /// <summary>Visto in volo e poi a terra: l'atterraggio è nostro.</summary>
    Landed,

    /// <summary>Ha volato ed è finito fermo al parcheggio: arrivato ai blocchi.</summary>
    AtGate,

    /// <summary>Uscito dall'area ancora in volo, e sappiamo a chi è andato.</summary>
    HandedOff,

    /// <summary>Uscito dall'area ancora in volo, e dopo di noi non l'ha preso nessuno.</summary>
    LeftAirborne,

    /// <summary>Si è mosso ma non ha mai volato: rullaggio soltanto.</summary>
    TaxiOnly,

    /// <summary>Non si è mai mosso: presenza, non movimento.</summary>
    Parked,

    /// <summary>Nessun piano di volo mentre era da noi.</summary>
    NoFlightPlan,

    /// <summary>Seconda (o terza…) tratta dello stesso callsign nella stessa sessione.</summary>
    SecondLeg,

    /// <summary>Fra due avvistamenti c'è un buco: i minuti sono per difetto.</summary>
    Gap,

    /// <summary>Riga ricostruita dai movimenti d'aeroporto: si sa che c'è stato, non per quanto.</summary>
    Rebuilt,
}

/// <summary>Una riga di traffico ridotta a quel che serve per raccontarla. Neutra: nessun EF, nessun DTO di UI.</summary>
public sealed record TrafficFacts(
    string? DepIcao,
    string? ArrIcao,
    int LegOrdinal,
    bool SawMovement,
    bool SawAirborne,
    bool HasObservationGap,
    bool Rebuilt,
    FlightPhase? FirstPhase,
    FlightPhase? LastPhase,
    bool HasHandoffTo);

/// <summary>
/// Traduce una riga di traffico nelle sue targhette. Puro e deterministico: è qui e non nel markup perché
/// «l'ho visto atterrare?» è una regola, e una regola scritta in un <c>.razor</c> non ha test.
/// </summary>
public static class TrafficStory
{
    /// <summary>
    /// Le targhette di una riga, nell'ordine in cui vanno mostrate: prima che cosa era il volo (partenza,
    /// arrivo, sorvolo), poi che cosa ne abbiamo visto, poi gli avvisi sul dato.
    /// </summary>
    /// <param name="stationIcao">ICAO del campo della postazione, se ne ha uno (<c>LIRF_TWR</c> → <c>LIRF</c>);
    /// <c>null</c> per ACC e APP d'area, dove «parte da qui» non vuol dire niente.</param>
    /// <param name="divisionPrefixes">Prefissi ICAO della divisione: servono a riconoscere un sorvolo vero
    /// (né partenza né arrivo in casa) invece di chiamare così ogni volo che non tocca il proprio campo.</param>
    public static IReadOnlyList<TrafficTag> Tags(
        TrafficFacts f, string? stationIcao, IReadOnlyList<string>? divisionPrefixes = null)
    {
        var tags = new List<TrafficTag>();

        if (Uguale(f.DepIcao, stationIcao)) tags.Add(TrafficTag.Departure);
        else if (Uguale(f.ArrIcao, stationIcao)) tags.Add(TrafficTag.Arrival);
        else if (f.DepIcao is not null && f.ArrIcao is not null && !InDivisione(f, divisionPrefixes))
            tags.Add(TrafficTag.Overflight);

        // ⚠️ Prima le cose viste, e una sola: `Landed` e `TookOff` insieme sarebbero un tocca-e-riparti, che
        // esiste ma non è quello che il campionamento a un minuto riesce a distinguere. Vince l'ultima.
        if (f.Rebuilt)
        {
            // La riga ricostruita non ha fasi: dire «atterrato» o «decollato» sarebbe inventarlo.
        }
        else if (!f.SawMovement)
        {
            tags.Add(TrafficTag.Parked);
        }
        else if (!f.SawAirborne)
        {
            tags.Add(TrafficTag.TaxiOnly);
        }
        else if (f.LastPhase == FlightPhase.Parked)
        {
            tags.Add(TrafficTag.AtGate);
        }
        else if (f.LastPhase == FlightPhase.Ground)
        {
            tags.Add(TrafficTag.Landed);
        }
        else if (f.LastPhase == FlightPhase.Airborne)
        {
            if (f.FirstPhase is FlightPhase.Parked or FlightPhase.Ground) tags.Add(TrafficTag.TookOff);
            tags.Add(f.HasHandoffTo ? TrafficTag.HandedOff : TrafficTag.LeftAirborne);
        }

        if (f.LegOrdinal > 1) tags.Add(TrafficTag.SecondLeg);
        if (f.DepIcao is null && f.ArrIcao is null) tags.Add(TrafficTag.NoFlightPlan);
        if (f.HasObservationGap) tags.Add(TrafficTag.Gap);
        if (f.Rebuilt) tags.Add(TrafficTag.Rebuilt);

        return tags;
    }

    /// <summary>L'ICAO del campo di una postazione, se il callsign ne dichiara uno.</summary>
    /// <remarks>
    /// ⚠️ Solo <c>_TWR</c>, <c>_GND</c>, <c>_DEL</c> e <c>_APP</c>: il prefisso di <c>LIRR_NE1_CTR</c> è un
    /// codice di FIR, non un aeroporto, e prenderlo per tale farebbe nascere «arrivi a LIRR» che non esistono.
    /// </remarks>
    public static string? StationIcao(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;

        var pezzi = callsign.Split('_');
        if (pezzi.Length < 2 || pezzi[0].Length != 4) return null;

        var coda = pezzi[^1].ToUpperInvariant();
        return coda is "TWR" or "GND" or "DEL" or "APP" or "DEP" or "AFIS" ? pezzi[0].ToUpperInvariant() : null;
    }

    private static bool Uguale(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool InDivisione(TrafficFacts f, IReadOnlyList<string>? prefissi)
    {
        var p = prefissi is { Count: > 0 } ? prefissi : new[] { "LI" };
        return p.Any(x =>
            f.DepIcao!.StartsWith(x, StringComparison.OrdinalIgnoreCase) ||
            f.ArrIcao!.StartsWith(x, StringComparison.OrdinalIgnoreCase));
    }
}

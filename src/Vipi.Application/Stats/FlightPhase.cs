using System;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>
/// In che fase è un aeroplano, dal punto di vista di chi lo controlla. Tre gradini, perché tre sono le
/// posizioni che se li dividono.
/// </summary>
public enum FlightPhase
{
    /// <summary>Fermo al parcheggio con una partenza da fare: è il traffico della DEL.</summary>
    Parked,

    /// <summary>A terra e in movimento (rullaggio, decollo iniziato, appena atterrato): traffico della GND.</summary>
    Ground,

    /// <summary>In volo.</summary>
    Airborne,
}

/// <summary>
/// Ricava la fase dal tracciato IVAO (<c>lastTrack</c>) e dice quali fasi gestisce ogni tipo di posizione.
///
/// <para><b>Perché serve</b>: DEL e GND non hanno poligono — misurato sul <c>vipi.db</c> reale, 0 su 5 e 0
/// su 20 — quindi la geometria da sola non le distingue, e la sola profondità nell'albero darebbe alla DEL
/// tutto ciò che tocca l'aeroporto. Ma la DEL gestisce **solo le partenze ancora ferme**, la GND tutto ciò
/// che è a terra: la differenza sta nella fase del volo, non nello spazio.</para>
///
/// <para>Stati osservati sul whazzup reale del 24 agosto 2026 (467 piloti): <c>Boarding</c> (75, tutti fermi
/// al parcheggio di partenza), <c>On Blocks</c> (14, fermi ma <b>arrivati</b> — es. dep LFBD / arr LEMD a
/// 289 NM dalla partenza), <c>Departing</c> (14, in movimento), <c>Landed</c> (9, in rullaggio dopo
/// l'atterraggio), <c>En Route</c>, <c>Initial Climb</c>, <c>Approach</c>.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class FlightPhases
{
    /// <summary>Distanza massima dal campo di partenza perché un aereo fermo conti come «in partenza da qui».</summary>
    private const double ParkedAtDepartureNm = 3.0;

    /// <summary>
    /// Fase dell'aeroplano. <paramref name="departureDistanceNm"/> è <c>lastTrack.departureDistance</c>:
    /// serve a non scambiare per partenza un aereo fermo ai blocchi **a destinazione** (<c>On Blocks</c> a
    /// centinaia di miglia dal campo di partenza è un arrivo, non una partenza).
    /// </summary>
    public static FlightPhase Of(bool onGround, double groundSpeed, string? state, double? departureDistanceNm)
    {
        if (!onGround) return FlightPhase.Airborne;
        if (groundSpeed > 0) return FlightPhase.Ground;

        var fermoAlCampoDiPartenza = departureDistanceNm is null || departureDistanceNm <= ParkedAtDepartureNm;
        var imbarco = string.Equals(state, "Boarding", StringComparison.OrdinalIgnoreCase);

        return imbarco && fermoAlCampoDiPartenza ? FlightPhase.Parked : FlightPhase.Ground;
    }

    /// <summary>
    /// Vero se questo tipo di posizione gestisce quella fase. È una dichiarazione di competenza, non un
    /// divieto: se nessuna posizione online dichiara la fase, il traffico resta a chi copre il settore
    /// (una DEL sola in frequenza si prende anche chi rulla, perché non c'è nessun altro).
    /// Il divieto vero è <see cref="Excludes"/>.
    /// </summary>
    public static bool Handles(SectorType type, FlightPhase phase) => type switch
    {
        SectorType.Del => phase == FlightPhase.Parked,
        SectorType.Gnd => phase is FlightPhase.Parked or FlightPhase.Ground,
        SectorType.Twr or SectorType.ITwr => phase is FlightPhase.Ground or FlightPhase.Airborne,
        SectorType.App or SectorType.Ctr => phase == FlightPhase.Airborne,
        _ => false,
    };

    /// <summary>
    /// Vero se in questa fase l'aeroplano si è mosso: distingue un <b>movimento</b> vero da un aereo che è
    /// rimasto parcheggiato per tutta la sessione senza volare.
    ///
    /// <para>⚠️ Non è un filtro sull'attribuzione, ed è importante che non lo diventi. In top-down
    /// <b>APP e CTR gestiscono anche il traffico a terra quando sotto non c'è nessuno</b> (regola di
    /// divisione, committente 24-ago-2026): quell'aereo è loro e la riga si scrive. Ma i volumi ACC partono
    /// da terra e contengono ogni aereo posteggiato della FIR — misurati cinque a terra nei settori di Roma,
    /// tre fermi al gate di Fiumicino — quindi un ACC in frequenza tre ore si vedrebbe accreditare tutto il
    /// piazzale se «traffico gestito» e «aerei visti» fossero lo stesso numero.</para>
    ///
    /// <para>Da qui i due numeri distinti: <b>movimenti</b> (tratte che si sono mosse almeno una volta) e
    /// <b>presenze</b> (tutte le tratte viste). Il primo è quello che si mette in evidenza.</para>
    /// </summary>
    public static bool IsMovement(FlightPhase phase) => phase != FlightPhase.Parked;
}

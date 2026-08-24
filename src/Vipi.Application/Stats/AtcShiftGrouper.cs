using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>Una sessione ATC vista dal raggruppatore: il minimo che serve per riconoscere un turno.</summary>
/// <param name="SessionId">Id IVAO della sessione.</param>
/// <param name="UserId">VID del controllore.</param>
/// <param name="Callsign">Callsign usato.</param>
/// <param name="StartUtc">Inizio della connessione.</param>
/// <param name="EndUtc">Fine; <c>null</c> = ancora connesso.</param>
public readonly record struct ShiftInput(long SessionId, int UserId, string Callsign, DateTimeOffset StartUtc, DateTimeOffset? EndUtc);

/// <summary>
/// Raggruppa in <b>turni</b> le sessioni ATC spezzate da una caduta di linea.
///
/// <para>IVAO chiude la sessione e ne apre una nuova con id nuovo a ogni riconnessione. Chi ha controllato
/// tre ore con due disconnessioni ha tre sessioni: contarle come tre turni gonfia i numeri, e soprattutto
/// <b>lo stesso aereo compare in tutte e tre</b> — sommando i traffici per sessione lo si conta tre volte.
/// Il turno è l'unità con cui si racconta l'attività; la sessione resta l'unità con cui si scrive il dato,
/// perché è la chiave che IVAO ci dà.</para>
///
/// <para>Regola: stesso VID, stesso callsign, e ripresa entro <see cref="DefaultGap"/> dalla fine della
/// precedente. Una sessione ancora aperta (<c>EndUtc</c> null) chiude il turno: non si può sapere se ne
/// seguirà un'altra.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class AtcShiftGrouper
{
    /// <summary>Buco massimo fra due connessioni perché restino lo stesso turno.</summary>
    public static readonly TimeSpan DefaultGap = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Assegna a ogni sessione la chiave del suo turno: l'id della <b>prima</b> sessione del gruppo.
    /// Il risultato è ordinato per (VID, callsign, inizio).
    /// </summary>
    public static IReadOnlyDictionary<long, long> Group(IEnumerable<ShiftInput> sessions, TimeSpan? gap = null)
    {
        var soglia = gap ?? DefaultGap;
        var keys = new Dictionary<long, long>();

        var gruppi = sessions
            .OrderBy(s => s.UserId)
            .ThenBy(s => s.Callsign, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.StartUtc)
            .GroupBy(s => (s.UserId, s.Callsign.ToUpperInvariant()));

        foreach (var gruppo in gruppi)
        {
            long corrente = 0;
            DateTimeOffset? fine = null;

            foreach (var s in gruppo)
            {
                var continua = corrente != 0 && fine is { } f && s.StartUtc - f <= soglia && s.StartUtc >= f;
                if (!continua) corrente = s.SessionId;

                keys[s.SessionId] = corrente;
                fine = s.EndUtc;   // sessione aperta (null) → la prossima non può continuarla
            }
        }
        return keys;
    }
}

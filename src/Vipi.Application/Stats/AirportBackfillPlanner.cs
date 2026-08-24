using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Stats;

/// <summary>Una sessione d'aeroporto da riempire: quel poco che serve a decidere di chi è un movimento.</summary>
public readonly record struct AirportSessionWindow(
    long SessionId, string Callsign, string Icao, SectorType Type, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// Chi si prende un movimento d'aeroporto quando il traffico si ricostruisce <b>a posteriori</b>.
///
/// <para><b>Il problema che risolve.</b> La sorgente racconta i movimenti di un aeroporto in una finestra,
/// non a chi hanno parlato. Se in quella finestra erano in frequenza TWR e GND insieme, dare il movimento a
/// tutt'e due gonfierebbe i numeri della divisione del doppio; darlo a caso li renderebbe inaffidabili.</para>
///
/// <para><b>La regola.</b> Vince la posizione più in basso nella scaletta operativa che era in frequenza in
/// quel momento — la stessa <see cref="AirportPositionLadder"/> che governa la gerarchia altrove, non una
/// seconda classifica scritta qui. Un decollo o un atterraggio è roba da torre: fra TWR e GND vince la TWR,
/// e la GND prende i movimenti solo quando la torre non c'era.</para>
///
/// <para>⚠️ Il grado non basta da solo: la <see cref="AirportPositionLadder.Rung"/> mette la DEL più in basso
/// di tutte perché nella <i>gerarchia</i> è la foglia, ma un movimento non è mai suo se c'è qualcun altro.
/// Qui l'ordine è quello di <b>competenza sul movimento</b>: TWR, poi APP, poi GND, poi DEL.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class AirportBackfillPlanner
{
    /// <summary>Quanto vale una posizione su un movimento d'aeroporto: più alto = più titolata.</summary>
    public static int Competence(SectorType type) => type switch
    {
        SectorType.Twr or SectorType.ITwr => 40,
        SectorType.App => 30,
        SectorType.Gnd => 20,
        SectorType.Del => 10,
        _ => 0,      // CTR e FSS non prendono movimenti d'aeroporto per questa via
    };

    /// <summary>
    /// La sessione a cui attribuire i movimenti dell'aeroporto nella finestra di
    /// <paramref name="candidate"/>, o <c>null</c> se non ce n'è una titolata.
    ///
    /// <para><paramref name="concurrent"/> sono le sessioni dello stesso aeroporto che si sovrappongono nel
    /// tempo: se ce n'è una più titolata, il movimento è suo e questa sessione non lo conta.</para>
    /// </summary>
    public static long? Owner(AirportSessionWindow candidate, IReadOnlyList<AirportSessionWindow> concurrent)
    {
        if (Competence(candidate.Type) == 0) return null;

        var migliore = candidate;
        foreach (var s in concurrent)
        {
            if (s.SessionId == candidate.SessionId) continue;
            if (!string.Equals(s.Icao, candidate.Icao, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Overlaps(s, candidate)) continue;

            var suo = Competence(s.Type);
            if (suo == 0) continue;

            var mio = Competence(migliore.Type);
            // A parità (due torri sullo stesso campo) decide l'id: serve un esito stabile, non il primo che capita.
            if (suo > mio || (suo == mio && s.SessionId < migliore.SessionId)) migliore = s;
        }
        return migliore.SessionId;
    }

    /// <summary>Vero se le due finestre si toccano nel tempo.</summary>
    public static bool Overlaps(AirportSessionWindow a, AirportSessionWindow b) =>
        a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc;

    /// <summary>
    /// ICAO e posizione dal callsign: <c>LIRF_TWR</c> → (LIRF, Twr), <c>LIRN_US0_APP</c> → (LIRN, App).
    /// <c>null</c> se non è una posizione d'aeroporto riconoscibile (un CTR, un FSS, un callsign strano).
    /// </summary>
    public static (string Icao, SectorType Type)? Parse(string callsign)
    {
        var pezzi = (callsign ?? "").Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pezzi.Length < 2 || pezzi[0].Length != 4) return null;

        var tipo = pezzi[^1].ToUpperInvariant() switch
        {
            "TWR" => SectorType.Twr,
            "APP" or "DEP" => SectorType.App,
            "GND" => SectorType.Gnd,
            "DEL" => SectorType.Del,
            _ => (SectorType?)null,
        };
        if (tipo is null) return null;

        // `LIML_I_TWR` è una torre di Linate; il pezzo di mezzo è un infisso, non un altro aeroporto.
        return (pezzi[0].ToUpperInvariant(), tipo.Value);
    }
}

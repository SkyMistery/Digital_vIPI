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

    /// <summary>
    /// Il movimento avvenuto a <paramref name="istante"/> è di <paramref name="candidate"/>?
    ///
    /// <para>🔴 U-094 (revisione totale 3): prima decideva <see cref="Owner"/> per l'INTERA finestra — bastava
    /// un'intersezione anche breve con una sessione più titolata perché la candidata andasse a zero movimenti per
    /// sempre, e la vincitrice chiedeva alla sorgente solo la sua finestra: i movimenti fuori dall'intersezione non
    /// andavano a nessuno (57 sessioni, 63 ore in frequenza, nella copia del 26 settembre). Ora decide chi era in
    /// frequenza, e più titolato, in QUELL'istante.</para>
    ///
    /// <para>Se in quell'istante non c'era nessuno (o la sorgente non dà l'istante), vale la regola della finestra:
    /// così due sessioni sovrapposte non si prendono lo stesso movimento.</para>
    /// </summary>
    public static bool Tiene(AirportSessionWindow candidate, IReadOnlyList<AirportSessionWindow> concurrent,
                             DateTimeOffset? istante)
    {
        if (Competence(candidate.Type) == 0) return false;
        if (istante is not { } i) return Owner(candidate, concurrent) == candidate.SessionId;

        AirportSessionWindow? migliore = null;
        foreach (var s in concurrent.Append(candidate))
        {
            if (!string.Equals(s.Icao, candidate.Icao, StringComparison.OrdinalIgnoreCase)) continue;
            if (Competence(s.Type) == 0 || i < s.StartUtc || i >= s.EndUtc) continue;
            if (migliore is not { } m || Meglio(s, m)) migliore = s;
        }

        return migliore is { } vincitore
            ? vincitore.SessionId == candidate.SessionId
            : Owner(candidate, concurrent) == candidate.SessionId;
    }

    /// <summary>
    /// La finestra di <paramref name="candidate"/> è coperta per INTERO da sessioni più titolate dello stesso campo:
    /// ogni istante al suo interno è di un altro, e chiedere i movimenti alla sorgente non servirebbe a niente.
    /// </summary>
    public static bool CopertaDaAltri(AirportSessionWindow candidate, IReadOnlyList<AirportSessionWindow> concurrent)
    {
        var sopra = concurrent
            .Where(s => s.SessionId != candidate.SessionId
                        && string.Equals(s.Icao, candidate.Icao, StringComparison.OrdinalIgnoreCase)
                        && Competence(s.Type) > 0 && Meglio(s, candidate) && Overlaps(s, candidate))
            .OrderBy(s => s.StartUtc)
            .ToList();

        var fin = candidate.StartUtc;
        foreach (var s in sopra)
        {
            if (s.StartUtc > fin) return false;           // un buco: lì la candidata è sola
            if (s.EndUtc > fin) fin = s.EndUtc;
            if (fin >= candidate.EndUtc) return true;
        }
        return false;
    }

    // Più titolata; a parità decide l'id, per un esito stabile (stessa regola di Owner).
    private static bool Meglio(AirportSessionWindow a, AirportSessionWindow b)
    {
        int ca = Competence(a.Type), cb = Competence(b.Type);
        return ca > cb || (ca == cb && a.SessionId < b.SessionId);
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

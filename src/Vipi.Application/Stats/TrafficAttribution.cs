using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>
/// La pretesa di una sessione ATC su un settore: il volume del settore e la sua profondità nell'albero
/// proiettato (0 = radice). Una sessione ne ha una per ogni settore che copre (§4.3 della carta).
/// </summary>
/// <param name="SessionCallsign">Callsign della sessione ATC online a cui il traffico verrà attribuito.</param>
/// <param name="Volume">Volume del settore (poligono + banda).</param>
/// <param name="Depth">Profondità del settore nell'albero (<c>Sector.ParentSectorId</c>): più alta = più specifico.</param>
public readonly record struct SectorClaim(string SessionCallsign, SectorVolume Volume, int Depth);

/// <summary>
/// A quale sessione appartiene un aereo. <b>Un aereo, una sessione</b>: i settori italiani si sovrappongono
/// pesantemente e sommare tutte le sovrapposizioni gonfierebbe le statistiche.
///
/// <para>Misurato sullo snapshot whazzup reale del 24 agosto 2026 (467 piloti, 171 settori italiani con
/// poligono): un singolo volo su Roma cadeva dentro <b>sei</b> settori — <c>LIRR_NE_CTR</c>,
/// <c>LIRR_NE1_CTR</c>, <c>LIRR_OV_CTR</c>, <c>LIRR_MIL_CTR</c>, <c>LIRR_FSS</c> e l'APP di Fiumicino. Senza
/// una regola di scelta le ore «gestite» sarebbero cinque volte quelle vere.</para>
///
/// <para>Ordine di preferenza, dal più forte al più debole:</para>
/// <list type="number">
///   <item><b>Profondità</b>: il settore più in basso nell'albero è il più specifico (la TWR batte il CTR).</item>
///   <item><b>Banda verticale più stretta</b>: fra due radici sovrapposte (il caso reale <c>MIL</c>/<c>FSS</c>
///     contro i settori civili) vince chi ha il volume più contenuto.</item>
///   <item><b>Poligono più piccolo</b> (area del bounding box), stesso criterio in orizzontale.</item>
///   <item><b>Callsign in ordine alfabetico</b>: non è una preferenza, è la garanzia che due giri identici
///     diano lo stesso esito invece di dipendere dall'ordine di arrivo delle righe.</item>
/// </list>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class TrafficAttribution
{
    /// <summary>
    /// La sessione a cui attribuire l'aereo, o <c>null</c> se non è dentro nessun volume rivendicato.
    /// <paramref name="altitudeFt"/> è la quota del tracciato IVAO, in piedi.
    /// </summary>
    public static string? Attribute(
        IReadOnlyList<SectorClaim> claims, double lat, double lon, double altitudeFt)
    {
        SectorClaim? best = null;
        foreach (var c in claims)
        {
            if (!c.Volume.Contains(lat, lon, altitudeFt)) continue;
            if (best is null || IsMoreSpecific(c, best.Value)) best = c;
        }
        return best?.SessionCallsign;
    }

    /// <summary>Tutti gli aerei di un elenco, attribuiti in un colpo: callsign pilota → callsign sessione.</summary>
    public static IReadOnlyDictionary<string, string> AttributeAll(
        IReadOnlyList<SectorClaim> claims,
        IEnumerable<(string Callsign, double Lat, double Lon, double AltitudeFt)> pilots)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pilots)
        {
            var session = Attribute(claims, p.Lat, p.Lon, p.AltitudeFt);
            if (session is not null) result[p.Callsign] = session;
        }
        return result;
    }

    private static bool IsMoreSpecific(SectorClaim a, SectorClaim b)
    {
        if (a.Depth != b.Depth) return a.Depth > b.Depth;

        var (bandA, bandB) = (a.Volume.TopFl - a.Volume.BottomFl, b.Volume.TopFl - b.Volume.BottomFl);
        if (bandA != bandB) return bandA < bandB;

        var (areaA, areaB) = (BboxArea(a.Volume), BboxArea(b.Volume));
        if (Math.Abs(areaA - areaB) > 1e-9) return areaA < areaB;

        return string.CompareOrdinal(a.Volume.Callsign, b.Volume.Callsign) < 0;
    }

    /// <summary>Area del bounding box in gradi quadri: serve solo a ordinare due volumi, non a misurare.</summary>
    private static double BboxArea(SectorVolume v) =>
        (v.Ring.MaxLat - v.Ring.MinLat) * (v.Ring.MaxLon - v.Ring.MinLon);
}

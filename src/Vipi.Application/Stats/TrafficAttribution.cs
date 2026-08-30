using System;
using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>
/// La pretesa di una sessione ATC su un settore: il volume, la profondità nell'albero proiettato (0 = radice)
/// e il tipo di posizione. Una sessione ne ha una per ogni settore che copre (§4.3 della carta).
/// </summary>
/// <param name="SessionCallsign">Callsign della sessione ATC online a cui il traffico verrà attribuito.</param>
/// <param name="Volume">Volume del settore (poligono + banda).</param>
/// <param name="Depth">Profondità nell'albero (<c>Sector.ParentSectorId</c>): più alta = più specifico.</param>
/// <param name="Type">Tipo di posizione: decide quali fasi di volo la posizione dichiara di gestire.</param>
public readonly record struct SectorClaim(string SessionCallsign, SectorVolume Volume, int Depth, SectorType Type);

/// <summary>
/// A quale sessione appartiene un aereo. <b>Un aereo, una sessione</b>: i settori italiani si sovrappongono
/// pesantemente e sommare tutte le sovrapposizioni gonfierebbe le statistiche.
///
/// <para>Misurato sullo snapshot whazzup reale del 24 agosto 2026 (467 piloti, 171 settori italiani con
/// poligono): un singolo volo su Roma cadeva dentro <b>sei</b> settori — <c>LIRR_NE_CTR</c>,
/// <c>LIRR_NE1_CTR</c>, <c>LIRR_OV_CTR</c>, <c>LIRR_MIL_CTR</c>, <c>LIRR_FSS</c> e l'APP di Fiumicino.</para>
///
/// <para>Ordine di preferenza, dal più forte al più debole:</para>
/// <list type="number">
///   <item><b>La posizione dichiara la fase del volo</b> (<see cref="FlightPhases.Handles"/>): la DEL gestisce
///     solo le partenze ancora ferme, la GND tutto ciò che è a terra. Senza questo criterio una DEL online si
///     prenderebbe l'intero aeroporto solo perché è il gradino più basso della scaletta — e DEL e GND non
///     hanno poligono (0 su 5 e 0 su 20 nel <c>vipi.db</c> reale), quindi la geometria non le distingue.</item>
///   <item><b>Profondità maggiore</b>: il settore più in basso nell'albero è il più specifico.</item>
///   <item><b>Banda verticale più stretta</b> (il caso reale <c>MIL</c>/<c>FSS</c> contro i civili).</item>
///   <item><b>Poligono più piccolo</b> (area del bounding box).</item>
///   <item><b>Callsign in ordine alfabetico</b>: garantisce che due giri identici diano lo stesso esito.</item>
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
        IReadOnlyList<SectorClaim> claims, double lat, double lon, double altitudeFt, FlightPhase phase)
    {
        SectorClaim? best = null;
        var bestHandles = false;

        foreach (var c in claims)
        {
            if (!c.Volume.Contains(lat, lon, altitudeFt)) continue;

            var handles = FlightPhases.Handles(c.Type, phase);
            if (best is null || IsMoreSpecific(c, handles, best.Value, bestHandles))
            {
                best = c;
                bestHandles = handles;
            }
        }
        return best?.SessionCallsign;
    }

    /// <summary>Tutti gli aerei di un elenco, attribuiti in un colpo: callsign pilota → callsign sessione.</summary>
    public static IReadOnlyDictionary<string, string> AttributeAll(
        IReadOnlyList<SectorClaim> claims,
        IEnumerable<(string Callsign, double Lat, double Lon, double AltitudeFt, FlightPhase Phase)> pilots)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pilots)
        {
            var session = Attribute(claims, p.Lat, p.Lon, p.AltitudeFt, p.Phase);
            if (session is not null) result[p.Callsign] = session;
        }
        return result;
    }

    private static bool IsMoreSpecific(SectorClaim a, bool aHandles, SectorClaim b, bool bHandles)
    {
        if (aHandles != bHandles) return aHandles;

        if (a.Depth != b.Depth) return a.Depth > b.Depth;

        var (bandA, bandB) = (a.Volume.TopFl - a.Volume.BottomFl, b.Volume.TopFl - b.Volume.BottomFl);
        if (bandA != bandB) return bandA < bandB;

        var (areaA, areaB) = (BboxArea(a.Volume), BboxArea(b.Volume));
        if (Math.Abs(areaA - areaB) > 1e-9) return areaA < areaB;

        return string.CompareOrdinal(a.Volume.Callsign, b.Volume.Callsign) < 0;
    }

    /// <summary>Area del bounding box in gradi quadri: serve solo a ordinare due volumi, non a misurare.
    /// ⚠️ Con più pezzi è il box che li contiene TUTTI — un volume di sette zone è più grande di una sola,
    /// ed è quel che serve al confronto.</summary>
    private static double BboxArea(SectorVolume v) =>
        (v.MaxLat - v.MinLat) * (v.MaxLon - v.MinLon);
}

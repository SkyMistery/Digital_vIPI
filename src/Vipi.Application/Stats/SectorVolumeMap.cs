using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>
/// Da «chi è in frequenza» a «quali volumi rivendica ognuno»: mette insieme la discesa della copertura
/// (<see cref="CoverageResolver"/>) e i volumi dei cataloghi (<see cref="SectorVolume"/>).
///
/// <para>Puro e deterministico, nessun I/O: la mappa dei settori la porta il chiamante.</para>
/// </summary>
public static class SectorVolumeMap
{
    /// <summary>
    /// Le pretese di tutte le sessioni online: una per ogni settore coperto che abbia un volume utilizzabile.
    /// </summary>
    public static IReadOnlyList<SectorClaim> BuildClaims(
        IReadOnlyList<SectorVolumeRow> settori, IReadOnlySet<string> online)
    {
        if (settori.Count == 0 || online.Count == 0) return Array.Empty<SectorClaim>();

        var nodi = settori.Select(s => new CoverageNode(s.Callsign, s.ParentCallsign)).ToList();
        var padroni = CoverageResolver.Owners(nodi, online);
        var profondita = Depths(settori);

        var claims = new List<SectorClaim>();
        foreach (var s in settori)
        {
            if (!padroni.TryGetValue(s.Callsign, out var padrone) || padrone is null) continue;

            var volume = VolumeOf(s, settori);
            if (volume is null) continue;   // niente poligono utilizzabile: non rivendica nulla

            claims.Add(new SectorClaim(padrone, volume, profondita[s.Callsign], s.Type));
        }
        return claims;
    }

    /// <summary>
    /// Volume di un settore. ⚠️ <b>DEL e GND non hanno poligono</b> — misurato sul <c>vipi.db</c> reale:
    /// zero su 5 e zero su 20 — perché non sono volumi di spazio aereo ma posizioni a terra. Senza un
    /// ripiego non rivendicherebbero mai niente e il traffico al suolo finirebbe sempre alla TWR (o
    /// all'ACC), anche con la GND in frequenza. Il ripiego naturale è il volume della <b>torre dello stesso
    /// aeroporto</b>: è il campo, che è esattamente dove lavorano.
    /// </summary>
    private static SectorVolume? VolumeOf(SectorVolumeRow s, IReadOnlyList<SectorVolumeRow> tutti)
    {
        var proprio = SectorVolume.From(s.Callsign, s.RegionMapPolygon, s.LowerLimit, s.UpperLimit);
        if (proprio is not null) return proprio;

        if (s.Type is not (SectorType.Del or SectorType.Gnd) || string.IsNullOrWhiteSpace(s.AirportIcao))
            return null;

        var torre = tutti
            .Where(t => t.Type is SectorType.Twr or SectorType.ITwr
                        && string.Equals(t.AirportIcao, s.AirportIcao, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(t.RegionMapPolygon))
            // Fra più torri dello stesso campo vince quella col callsign senza infisso (`LIRF_TWR` prima di
            // `LIRF_E_TWR`): è la convenzione di divisione per la posizione principale.
            .OrderBy(t => t.Callsign.Count(ch => ch == '_'))
            .ThenBy(t => t.Callsign, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return torre is null
            ? null
            : SectorVolume.From(s.Callsign, torre.RegionMapPolygon, torre.LowerLimit, torre.UpperLimit);
    }

    /// <summary>
    /// Profondità di ogni settore nell'albero (0 = radice). La guardia sui nodi già visti chiude i cicli:
    /// un dato sporco in archivio deve dare un numero, non un blocco del poller.
    /// </summary>
    private static Dictionary<string, int> Depths(IReadOnlyList<SectorVolumeRow> settori)
    {
        var padri = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in settori) padri[s.Callsign] = s.ParentCallsign;

        var profondita = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in settori)
        {
            var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var d = 0;
            var corrente = s.Callsign;

            while (visti.Add(corrente)
                   && padri.TryGetValue(corrente, out var padre)
                   && padre is not null
                   && padri.ContainsKey(padre))
            {
                d++;
                corrente = padre;
            }
            profondita[s.Callsign] = d;
        }
        return profondita;
    }
}

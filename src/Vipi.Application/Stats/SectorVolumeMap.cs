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
    /// <param name="proprietarioDi">
    /// Come si collassa un settore chiuso su chi lo tiene: callsign → sessione online, o <c>null</c> se
    /// nessuno lo copre. Omesso ⇒ <see cref="CoverageResolver.Owners"/>, cioè <b>i soli padri</b>.
    ///
    /// <para>⚠️ <b>Perché è un parametro e non una costante.</b> <c>CoverageResolver</c> conosce i soli
    /// <c>ParentCallsign</c>: le righe di ripiego dichiarate <b>no</b>. Con <c>LIMM_ES5_CTR</c> chiuso a
    /// FL350 la <b>catena</b> risponde <c>WS5</c> (per la riga «FL325–UNL → WS5») e la <b>geometria</b>
    /// risponderebbe <c>ES2</c> (per il padre): due risposte diverse alla stessa domanda, cioè «due alberi»
    /// ricostruiti in un posto nuovo. Chi risolve un <b>rinvio</b> passa qui <c>FallbackChain</c> e le due
    /// tornano a coincidere.</para>
    ///
    /// <para>⚠️ Le <b>statistiche</b> continuano a non passarlo, e la differenza resta <b>dichiarata</b>: là
    /// la domanda è «di chi era quell'aereo», e la catena di ripiego non c'entra. Vedi
    /// <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2 e la carta del 10 settembre 2026.</para>
    /// </param>
    public static IReadOnlyList<SectorClaim> BuildClaims(
        IReadOnlyList<SectorVolumeRow> settori, IReadOnlySet<string> online,
        Func<string, string?>? proprietarioDi = null)
    {
        if (settori.Count == 0 || online.Count == 0) return Array.Empty<SectorClaim>();

        if (proprietarioDi is null)
        {
            var nodi = settori.Select(s => new CoverageNode(s.Callsign, s.ParentCallsign)).ToList();
            var padri = CoverageResolver.Owners(nodi, online);
            proprietarioDi = cs => padri.TryGetValue(cs, out var p) ? p : null;
        }

        var profondita = Depths(settori);

        var claims = new List<SectorClaim>();
        foreach (var s in settori)
        {
            if (proprietarioDi(s.Callsign) is not { Length: > 0 } padrone) continue;

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
        var proprio = Volume(s.Callsign, s);
        if (proprio is not null) return proprio;

        if (s.Type is not (SectorType.Del or SectorType.Gnd) || string.IsNullOrWhiteSpace(s.AirportIcao))
            return null;

        var torre = tutti
            .Where(t => t.Type is SectorType.Twr or SectorType.ITwr
                        && string.Equals(t.AirportIcao, s.AirportIcao, StringComparison.OrdinalIgnoreCase)
                        && t.Parts.Count > 0)
            // Fra più torri dello stesso campo vince quella col callsign senza infisso (`LIRF_TWR` prima di
            // `LIRF_E_TWR`): è la convenzione di divisione per la posizione principale.
            .OrderBy(t => t.Callsign.Count(ch => ch == '_'))
            .ThenBy(t => t.Callsign, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        // ⚠️ Il volume è quello della TORRE, ma il callsign resta quello della GND/DEL: è lei che rivendica.
        return torre is null ? null : Volume(s.Callsign, torre);
    }

    /// <summary>Un volume dai pezzi di una riga, col callsign che si vuole dargli.</summary>
    private static SectorVolume? Volume(string callsign, SectorVolumeRow riga) =>
        riga.Parts.Count == 0
            ? null
            : SectorVolume.From(
                callsign,
                riga.Parts.Select(p => ((string?)p.PolygonJson, p.BaseFeet, p.TopFeet)).ToList(),
                riga.Source);

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

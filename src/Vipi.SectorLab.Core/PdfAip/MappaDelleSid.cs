using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// La mappa di gruppo in testa al <c>.sid</c> (P10 della carta «file per file», carta F7 slice 7f): una voce <c>MAPS</c>
/// che disegna insieme le SID col tracciato dello scalo, come <c>STAR RNAV(ALL)</c> nello <c>.str</c>. È una mappa
/// composta (F3-bis): la dichiarazione <c>//@"SID RNAV(ALL)" compose=ALAX7G,ALAX7H</c> dice le parti, e il Lab la
/// rigenera quando una SID cambia; il corpo lo fa <see cref="MappeComposte"/>, qui si scrive solo la prima volta.
/// </summary>
public static class MappaDelleSid
{
    /// <summary>
    /// Le righe della voce nuova da mettere in testa al file: la dichiarazione, la testata, il corpo composto. Null se lo
    /// scalo ha meno di due SID col tracciato (non c'è niente da raccogliere) o se il file ha già una mappa composta di SID.
    /// Le RNAV, se ce ne sono; se no tutte.
    /// </summary>
    public static IReadOnlyList<string>? Righe(IReadOnlyList<SidProcedure> sidDelFile, string icao)
    {
        ArgumentNullException.ThrowIfNull(sidDelFile);
        if (sidDelFile.Any(s => MappeComposte.EUnaMappa(s) && s.IcaoCode.Trim().Equals(icao, StringComparison.OrdinalIgnoreCase)))
            return null;
        var conTracciato = Candidate(sidDelFile, icao);
        if (conTracciato.Count < 2)
            return null;
        bool rnav = conTracciato.All(s => s.IsRnav == true);
        string nome = rnav ? "SID RNAV(ALL)" : "SID (ALL)";

        // Il nome da solo, se nel file è unico; se no con la sua pista (come le caselle di «Composta da»).
        var procedure = sidDelFile.Where(s => !MappeComposte.EUnaMappa(s)).ToList();
        var elenco = conTracciato
            .Select(s => new ProceduraDellaComposta(procedure.Count(p => MappeComposte.Nome(p) == MappeComposte.Nome(s)) == 1 ? null : s.Runway.Trim(), MappeComposte.Nome(s)))
            .Distinct()
            .ToList();
        // La testata e il corpo li scrive il salvatore del .sid, come scriverà la mappa quando il Lab la rigenera.
        var mappa = new SidProcedure { IcaoCode = icao, Runway = "MAPS", Name = nome, IsRnav = rnav ? true : null };
        MappeComposte.Applica(mappa, MappeComposte.Componi(mappa, elenco, sidDelFile.Cast<object>().ToList()).Punti);
        return [$"//@\"{nome}\" {Metadati.Compose}={Metadati.ScriviLElenco(elenco)}", .. new SidSaver().Serialize(mappa)];
    }

    /// <summary>Quante SID col tracciato la mappa raccoglierebbe: le RNAV se ce ne sono, se no tutte.</summary>
    public static int Quante(IReadOnlyList<SidProcedure> sidDelFile, string icao) => Candidate(sidDelFile, icao).Count;

    private static List<SidProcedure> Candidate(IReadOnlyList<SidProcedure> sidDelFile, string icao)
    {
        var conTracciato = sidDelFile
            .Where(s => !MappeComposte.EUnaMappa(s) && s.Track.Count > 0 && s.IcaoCode.Trim().Equals(icao, StringComparison.OrdinalIgnoreCase)
                        && Metadati.NomeElencabile(MappeComposte.Nome(s)))
            .ToList();
        return conTracciato.Any(s => s.IsRnav == true) ? [.. conTracciato.Where(s => s.IsRnav == true)] : conTracciato;
    }
}

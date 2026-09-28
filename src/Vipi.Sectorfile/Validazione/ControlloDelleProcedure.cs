using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli delle procedure, <c>.sid</c> e <c>.str</c> (lotto «Subito» slice 9a, «file per file» P3, Q6, R-4):
/// procedura ripetuta, 6° campo di una SID fuori posto, voce di un altro scalo, pista che lo scalo non ha.
/// </summary>
/// <remarks>
/// Misure sul fork del 29 settembre: 11 SID ripetute (5 di LIMC 35R), 12 SID coi campi spostati di uno
/// (<c>VICTOR6A; ;0;VICTOR;</c>, <c>SID1; ; ;TOP;</c>), una SID e una STAR di un altro scalo (<c>limf.sid</c> <c>LIMF18</c>:
/// manca il <c>;</c>; <c>licz.str</c> <c>LICC</c>), 5 piste che lo scalo non ha (<c>lipi.str</c> <c>06:24</c>, <c>lirl.str</c>
/// <c>05:12</c>, <c>licz.str</c>). Una «pista» che non ha la forma di un verso (<c>MAPS</c>, <c>NE</c> e <c>SU</c> in
/// <c>lirr.str</c>, <c>BULL</c> e <c>AAR</c> in <c>lizz.str</c>) è un gruppo del menu, non una pista: non si controlla.
/// </remarks>
public static class ControlloDelleProcedure
{
    private static readonly Regex UnVerso = new(@"^\d{2}[LRC]?$", RegexOptions.CultureInvariant);

    // Il nome di un file di procedure di uno scalo: quattro lettere (lirf.sid). Gli altri (lirr.str, lizz.str) sono
    // raccolte, e il controllo dello scalo non vale.
    private static readonly Regex FileDiScalo = new(@"^[a-z]{4}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// I problemi delle procedure di un file. <paramref name="versi"/>: per scalo, i versi delle sue piste nei <c>.rw</c>
    /// (uno scalo che non ne ha non si controlla). <paramref name="testoDellaRiga"/>: la riga del disco, da 1.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(string relativo, IReadOnlyList<object> record,
                                                    IReadOnlyDictionary<string, IReadOnlySet<string>> versi,
                                                    Func<int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(relativo);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(versi);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        string scalo = Path.GetFileNameWithoutExtension(relativo);
        bool diScalo = FileDiScalo.IsMatch(scalo);
        var viste = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (object voce in record)
        {
            (string icao, string piste, string nome, string tipo, int riga) = voce switch
            {
                SidProcedure s => (s.IcaoCode.Trim(), s.Runway.Trim(), s.Name.Trim(), s.DefaultVisible?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", s.Source?.LineNumber ?? 0),
                StrRecord t => (t.IcaoCode.Trim(), t.RunwaySpec.Trim(), t.ProcedureId.Trim(), ((int)t.RecordType).ToString(System.Globalization.CultureInfo.InvariantCulture), t.Source?.LineNumber ?? 0),
                _ => ("", "", "", "", -1),
            };
            if (riga < 0)
                continue;
            string testo = riga > 0 ? testoDellaRiga(riga) : string.Empty;

            string chiave = $"{icao}|{piste}|{nome}|{tipo}";
            if (viste.TryGetValue(chiave, out int prima))
            {
                yield return new ProblemaDelSector(Regola.ProceduraRipetuta, relativo, riga, testo,
                    $"{icao} {piste} {nome}: la stessa procedura è già alla riga {prima}");
            }
            else
            {
                viste[chiave] = riga;
            }

            // Una SID: il 6° campo è il tipo (0 SID, 1 transizione). Con un campo in meno prima, il nome del navaid finisce
            // lì e Aurora legge la riga sbagliata (P3).
            if (voce is SidProcedure && testo.Split(';') is { Length: > 5 } campi && campi[5].Trim() is { Length: > 0 } sesto
                && sesto is not ("0" or "1"))
            {
                yield return new ProblemaDelSector(Regola.TipoFuoriPosto, relativo, riga, testo,
                    $"il 6° campo è «{sesto}», e dovrebbe essere il tipo (0 SID, 1 transizione): un campo manca prima di lui");
            }

            if (diScalo && !icao.Equals(scalo, StringComparison.OrdinalIgnoreCase))
            {
                yield return new ProblemaDelSector(Regola.VoceDiUnAltroScalo, relativo, riga, testo,
                    $"la voce è di «{icao}», ma il file è di {scalo.ToUpperInvariant()}");
            }

            if (versi.GetValueOrDefault(icao) is { Count: > 0 } suoi)
            {
                var mancanti = piste.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(p => UnVerso.IsMatch(p) && !suoi.Contains(p)).ToList();
                if (mancanti.Count > 0)
                {
                    yield return new ProblemaDelSector(Regola.PistaInesistente, relativo, riga, testo,
                        $"{icao} non ha {(mancanti.Count == 1 ? "la pista" : "le piste")} {string.Join(", ", mancanti)}: nei .rw ha {string.Join(", ", suoi.Order(StringComparer.Ordinal))}");
                }
            }
        }

        // Slice 9e (Q2c): la STAR che finisce dove nessun avvicinamento della sua pista passa.
        foreach (var (indice, punto, avvicinamenti) in LegamiDelleProcedure.StarSenzaAvvicinamento(record))
        {
            var star = (StrRecord)record[indice];
            int riga = star.Source?.LineNumber ?? 0;
            yield return new ProblemaDelSector(Regola.StarSenzaAvvicinamento, relativo, riga, riga > 0 ? testoDellaRiga(riga) : string.Empty,
                $"{star.ProcedureId.Trim()} finisce a {punto}, e nessun avvicinamento della pista {star.RunwaySpec.Trim()} ci passa ({string.Join(", ", avvicinamenti)})");
        }
    }
}

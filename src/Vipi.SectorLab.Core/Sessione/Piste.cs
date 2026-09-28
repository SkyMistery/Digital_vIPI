using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// Chi usa una pista (lotto «Subito» slice 7d, R-1): la pista è «scalo + verso», e il record del <c>.rw</c> ne dichiara
/// due (<c>LIRF;16L;34R;…</c>). La citano il 2° campo delle SID (<c>LIRF;16L;OST1E;…</c>) e delle voci dei <c>.str</c>
/// (<c>LIRF;16L:16R;ELKA3A;…</c>, versi separati da «:»), i nomi delle mappe del <c>MAPS</c> (<c>RWY16L</c>), i tag del
/// <c>.rw</c> (<c>//@"LIRF 16L/34R"</c>, le chiavi <c>16L.tora=…</c>, §M regola 7) — tutte righe che il Lab riscrive —, e
/// in più, SOLO DA VEDERE: i PAR dei profili <c>.cpr</c> (<c>INS1PAR_CAPTION=LIBN RWY14/3.0°</c>: il Lab non ha ancora
/// il lettore dei <c>.cpr</c>, slice 11) e i commenti dei disegni dello scalo (<c>//Runway 16L designator</c>: la
/// marcatura è un disegno, e le cifre nuove si ridisegnano).
/// </summary>
public static partial class Piste
{
    /// <summary>Come la cita una riga che il Lab NON riscrive: la rinomina la elenca, e va cambiata a mano.</summary>
    public const string DaVedere = "da cambiare a mano";

    /// <summary>
    /// Chi usa la pista del record <paramref name="record"/> di un <c>.rw</c>: le citazioni di tutti e due i versi, o null
    /// se il record non è una pista (la riga <c>MAPS</c> del <c>.rw</c>).
    /// </summary>
    public static UsiDelPunto? Di(SessioneAperta sessione, string file, int record, Func<string, IEnumerable<object>> sporchiDi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(sporchiDi);
        if (sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record < 0 || record >= conRecord.RecordDelModello.Count
            || conRecord.RecordDelModello[record] is not Runway pista || !EUnVerso(pista.Designator1) || !EUnVerso(pista.Designator2))
            return null;

        string icao = pista.IcaoCode.Trim();
        string[] versi = [pista.Designator1.Trim(), pista.Designator2.Trim()];
        var citazioni = new List<Citazione>();
        foreach (var (relativo, altro) in sessione.File.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            bool procedure = relativo.EndsWith(".sid", StringComparison.OrdinalIgnoreCase) || relativo.EndsWith(".str", StringComparison.OrdinalIgnoreCase);
            if (procedure && altro is IFileConRecord suo)
            {
                var righe = suo.RigheDelFile(sporchiDi(relativo));
                for (int i = 0; i < righe.Count; i++)
                {
                    if (CitaIlVerso(righe[i], icao, versi) is { } come && suo.RecordDellaRiga(i + 1) is { } k)
                        citazioni.Add(new Citazione(relativo, k, i + 1, righe[i], come, []));
                }
            }
            else if (relativo.EndsWith(".cpr", StringComparison.OrdinalIgnoreCase) || EDelDisegnoDelloScalo(relativo, icao))
            {
                // Solo da vedere: il testo dal disco (un .cpr il motore non lo legge; i disegni li riscrive il Lab, ma
                // una marcatura rinumerata è un disegno nuovo).
                string percorso = sessione.Cartella.Assoluto(relativo);
                if (!System.IO.File.Exists(percorso))
                    continue;
                string[] righe = System.IO.File.ReadAllLines(percorso);
                for (int i = 0; i < righe.Length; i++)
                {
                    if (relativo.EndsWith(".cpr", StringComparison.OrdinalIgnoreCase)
                            ? DelPar(righe[i], icao, versi)
                            : righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && versi.Any(v => Parola(v).IsMatch(righe[i])))
                        citazioni.Add(new Citazione(relativo, -1, i + 1, righe[i], DaVedere, []));
                }
            }
        }

        return new UsiDelPunto("pista", versi, [.. citazioni.OrderBy(c => c.File, StringComparer.Ordinal).ThenBy(c => c.Riga)], []);
    }

    /// <summary>Il verso di una pista: <c>07</c>, <c>16L</c>, <c>35C</c>.</summary>
    public static bool EUnVerso(string? testo) => testo is not null && Verso().IsMatch(testo.Trim());

    /// <summary>
    /// Come una riga di un <c>.sid</c>/<c>.str</c> cita un verso dello scalo: «SID/STAR» (il 2° campo), «mappa» (il
    /// nome <c>RWY16L</c> di una voce del <c>MAPS</c>), o null.
    /// </summary>
    public static string? CitaIlVerso(string riga, string icao, IReadOnlyList<string> versi)
    {
        if (riga.TrimStart().StartsWith("//", StringComparison.Ordinal))
            return null;
        string[] campi = riga.Split(';');
        if (campi.Length < 3 || !string.Equals(campi[0].Trim(), icao, StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(campi[1].Trim(), "MAPS", StringComparison.OrdinalIgnoreCase))
            return versi.Any(v => string.Equals(campi[2].Trim(), "RWY" + v, StringComparison.OrdinalIgnoreCase)) ? "mappa" : null;
        return campi[1].Split(':').Any(p => versi.Contains(p.Trim(), StringComparer.OrdinalIgnoreCase)) ? "procedura" : null;
    }

    /// <summary>Vero se una riga da vedere (un PAR, un commento di un disegno) cita QUESTO verso dello scalo.</summary>
    public static bool CitaAMano(string riga, string icao, string verso)
        => DelPar(riga, icao, [verso]) || !riga.Contains("PAR_CAPTION", StringComparison.OrdinalIgnoreCase) && Parola(verso).IsMatch(riga);

    // Il PAR di un profilo: «INS1PAR_CAPTION=LIBN RWY14/3.0°» — lo scalo è quello della didascalia, non del file.
    private static bool DelPar(string riga, string icao, IReadOnlyList<string> versi)
    {
        var m = Didascalia().Match(riga);
        return m.Success && string.Equals(m.Groups["icao"].Value, icao, StringComparison.OrdinalIgnoreCase)
               && versi.Contains(m.Groups["verso"].Value, StringComparer.OrdinalIgnoreCase);
    }

    // I disegni dello scalo: GEO\lirf.geo, GND_LAYOUT\rf_ad_gnd.pol, RW_MARKINGS\rf_mark.geo (le due lettere dopo LI).
    private static bool EDelDisegnoDelloScalo(string relativo, string icao)
    {
        string nome = relativo[(relativo.LastIndexOf('/') + 1)..].ToLowerInvariant();
        string scalo = icao.ToLowerInvariant();
        return (nome.EndsWith(".geo", StringComparison.Ordinal) || nome.EndsWith(".pol", StringComparison.Ordinal))
               && (nome.StartsWith(scalo + ".", StringComparison.Ordinal)
                   || scalo.Length == 4 && (nome.StartsWith(scalo[2..] + "_", StringComparison.Ordinal) || nome.StartsWith(scalo[2..] + "3", StringComparison.Ordinal)));
    }

    /// <summary>Il verso come parola intera: «16L» in «Runway 16L designator» e in «16L/34R», non in «116L».</summary>
    public static Regex Parola(string verso) => new($@"(?<![0-9A-Za-z]){Regex.Escape(verso)}(?![0-9A-Za-z])", RegexOptions.IgnoreCase);

    /// <summary>
    /// La riga di un <c>.rw</c>, di un <c>.sid</c>/<c>.str</c> o di un tag col verso cambiato; null se non cambia. Nel
    /// <c>.rw</c> i campi 2 e 3; nelle procedure il 2° campo (versi separati da «:») o il nome della mappa; nei tag il
    /// nome del record (<c>"LIRF 16L/34R"</c>) e le chiavi (<c>16L.tora=</c>).
    /// </summary>
    public static string? Rinomina(string riga, string icao, string vecchio, string nuovo)
    {
        ArgumentNullException.ThrowIfNull(riga);
        string testa = riga.TrimStart();
        if (testa.StartsWith("//@", StringComparison.Ordinal))
        {
            // Il nome del record della pista e le chiavi per verso (§M regola 7).
            string fatta = Regex.Replace(riga, $@"^(\s*//@""{Regex.Escape(icao)}\s+)([^""]*)("")",
                m => m.Groups[1].Value + string.Join('/', m.Groups[2].Value.Split('/').Select(v => string.Equals(v.Trim(), vecchio, StringComparison.OrdinalIgnoreCase) ? nuovo : v)) + m.Groups[3].Value,
                RegexOptions.IgnoreCase);
            fatta = Regex.Replace(fatta, $@"(?<=\s){Regex.Escape(vecchio)}(?=\.[a-z])", nuovo, RegexOptions.IgnoreCase);
            return fatta == riga ? null : fatta;
        }

        if (testa.StartsWith("//", StringComparison.Ordinal))
            return null;
        string[] campi = riga.Split(';');
        if (campi.Length < 3 || !string.Equals(campi[0].Trim(), icao, StringComparison.OrdinalIgnoreCase))
            return null;
        bool cambiata = false;
        string Cambia(string campo)
        {
            string pulito = campo.Trim();
            if (!string.Equals(pulito, vecchio, StringComparison.OrdinalIgnoreCase))
                return campo;
            cambiata = true;
            return campo.Replace(pulito, nuovo, StringComparison.Ordinal);
        }

        if (string.Equals(campi[1].Trim(), "MAPS", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(campi[2].Trim(), "RWY" + vecchio, StringComparison.OrdinalIgnoreCase))
            {
                campi[2] = campi[2].Replace(campi[2].Trim(), "RWY" + nuovo, StringComparison.Ordinal);
                cambiata = true;
            }
        }
        else if (campi[1].Contains(':', StringComparison.Ordinal))
        {
            campi[1] = string.Join(':', campi[1].Split(':').Select(Cambia));
        }
        else
        {
            // Una SID ha il verso nel 2° campo; una pista del .rw ha i due versi nel 2° e nel 3°.
            campi[1] = Cambia(campi[1]);
            if (!cambiata && EUnVerso(campi[2]))
                campi[2] = Cambia(campi[2]);
        }

        return cambiata ? string.Join(';', campi) : null;
    }

    [GeneratedRegex(@"^(0[1-9]|[12][0-9]|3[0-6])[LRC]?$", RegexOptions.IgnoreCase)]
    private static partial Regex Verso();

    [GeneratedRegex(@"PAR_CAPTION=(?<icao>[A-Z]{4})\s+RWY(?<verso>[0-9]{2}[LRC]?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Didascalia();
}

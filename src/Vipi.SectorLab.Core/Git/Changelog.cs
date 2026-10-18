using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Core.Git;

/// <summary>Una riga da scrivere nel changelog: la sezione (<c>Generale</c>, <c>LIRR</c>…) e il testo, senza l'asterisco.</summary>
/// <param name="Sezioni">Le sezioni possibili: una sola se il Lab la sa, più d'una se lo scalo sta in più FIR (sceglie l'AOD).</param>
public sealed record RigaDelChangelog(IReadOnlyList<string> Sezioni, string Testo)
{
    /// <summary>La sezione proposta: la prima delle possibili.</summary>
    public string Sezione => Sezioni[0];
}

/// <summary>Un controllo del changelog (C6) non passato: cosa, e la riga del file (da 1) se c'è.</summary>
public sealed record AvvisoDelChangelog(string Testo, int? Riga = null);

/// <summary>
/// Il <c>changelog.md</c> del sector (carta F4, slice 6; «file per file» §3, C1, C2, C6): l'intestazione del ciclo
/// (<c>**AIRAC A2610 IN VIGORE DAL 01/10/2026</c>), poi le sezioni <c>**Generale</c>, <c>**LIBB Brindisi</c>,
/// <c>**LIMM Milano</c>, <c>**LIPP Padova</c>, <c>**LIRR Roma</c>, con le righe <c>*ICAO: cosa</c> o <c>*NIL</c>. Aurora
/// lo mostra agli utenti: il Lab ci scrive righe che l'AOD ha visto e corretto, e non tocca le altre.
/// </summary>
public static partial class Changelog
{
    /// <summary>Il file, dalla radice del repo.</summary>
    public const string Percorso = "SectorFiles/Include/IT/changelog.md";

    /// <summary>Le sezioni dello schema del 2026, nell'ordine: il codice e il titolo com'è scritto.</summary>
    public static IReadOnlyList<(string Codice, string Titolo)> Sezioni { get; } =
    [
        ("Generale", "Generale"), ("LIBB", "LIBB Brindisi"), ("LIMM", "LIMM Milano"), ("LIPP", "LIPP Padova"), ("LIRR", "LIRR Roma"),
    ];

    private const string Vuota = "*NIL";

    /// <summary>Il ciclo e la data dell'intestazione (<c>2610</c>, 01/10/2026), o null se la prima riga non è nella forma.</summary>
    public static (string Ciclo, DateOnly Dal)? Intestazione(IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var trovata = righe.Count == 0 ? null : FormaDellIntestazione().Match(righe[0].Trim());
        if (trovata is not { Success: true })
            return null;
        return DateOnly.TryParseExact(trovata.Groups["dal"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dal)
            ? (trovata.Groups["ciclo"].Value, dal)
            : null;
    }

    /// <summary>
    /// I controlli (C6): l'intestazione diversa dal ciclo atteso, una sezione che manca, <c>*NIL</c> accanto ad altre righe.
    /// <paramref name="ciclo"/>: quello del ramo (<c>2611</c> per <c>airac/2611</c>, quello in vigore per <c>master</c>).
    /// </summary>
    public static IReadOnlyList<AvvisoDelChangelog> Controlla(IReadOnlyList<string> righe, string ciclo)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var avvisi = new List<AvvisoDelChangelog>();
        if (Intestazione(righe) is not { } intestazione)
            avvisi.Add(new("La prima riga non è l'intestazione del ciclo («**AIRAC A2610 IN VIGORE DAL 01/10/2026»).", 1));
        else if (intestazione.Ciclo != ciclo)
            avvisi.Add(new($"L'intestazione dice A{intestazione.Ciclo}, il ramo è del ciclo A{ciclo}.", 1));

        foreach (var (codice, titolo) in Sezioni)
        {
            if (Dove(righe, codice) is not { } sezione)
            {
                avvisi.Add(new($"Manca la sezione «**{titolo}»."));
                continue;
            }

            var voci = Voci(righe, sezione).ToList();
            if (voci.Count > 1 && voci.FirstOrDefault(v => righe[v].Trim() == Vuota) is var nil and > 0)
                avvisi.Add(new($"In «{titolo}» c'è *NIL accanto ad altre righe.", nil + 1));
            if (voci.Count == 0)
                avvisi.Add(new($"«{titolo}» è vuota: ci va *NIL.", sezione.Inizio + 1));
        }

        return avvisi;
    }

    /// <summary>
    /// Le righe del file con quelle nuove in coda alla loro sezione (C2): <c>*NIL</c> se c'era si toglie; una riga uguale
    /// a una che c'è già non si ripete. Le altre righe restano byte per byte. Una sezione che manca: la riga non si scrive,
    /// e torna fra le <paramref name="scartate"/>.
    /// </summary>
    public static List<string> Aggiungi(IReadOnlyList<string> righe, IEnumerable<(string Sezione, string Testo)> nuove, out List<string> scartate)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(nuove);
        var uscita = righe.ToList();
        scartate = [];
        foreach (var (sezione, testo) in nuove)
        {
            string riga = "*" + testo.Trim().TrimStart('*').Trim();
            if (riga == "*")
                continue;
            if (Dove(uscita, sezione) is not { } dove)
            {
                scartate.Add($"{sezione}: {testo}");
                continue;
            }

            var voci = Voci(uscita, dove).ToList();
            if (voci.Any(v => uscita[v].Trim() == riga))
                continue;
            if (voci.Count == 1 && uscita[voci[0]].Trim() == Vuota)
            {
                uscita[voci[0]] = riga;
                continue;
            }

            int dopo = voci.Count > 0 ? voci[^1] : dove.Inizio;
            uscita.Insert(dopo + 1, riga);
        }

        return uscita;
    }

    /// <summary>Le righe scritte (non <c>*NIL</c>), sezione per sezione: il codice della sezione, l'indice della riga, il testo.</summary>
    public static IReadOnlyList<(string Sezione, int Riga, string Testo)> Scritte(IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var scritte = new List<(string, int, string)>();
        foreach (var (codice, _) in Sezioni)
        {
            if (Dove(righe, codice) is not { } sezione)
                continue;
            scritte.AddRange(Voci(righe, sezione).Where(v => righe[v].Trim() != Vuota).Select(v => (codice, v, righe[v].Trim())));
        }

        return scritte;
    }

    /// <summary>
    /// Toglie la riga <paramref name="riga"/> (indice da 0) se è una voce scritta di una sezione (C2): se la sezione resta
    /// vuota, al suo posto torna <c>*NIL</c>. Le altre righe restano byte per byte. Null se quella riga non si toglie.
    /// </summary>
    public static List<string>? Togli(IReadOnlyList<string> righe, int riga)
    {
        ArgumentNullException.ThrowIfNull(righe);
        foreach (var (codice, _) in Sezioni)
        {
            if (Dove(righe, codice) is not { } sezione || riga <= sezione.Inizio || riga >= sezione.Fine)
                continue;
            var voci = Voci(righe, sezione).ToList();
            if (!voci.Contains(riga) || righe[riga].Trim() == Vuota)
                return null;
            var uscita = righe.ToList();
            if (voci.Count == 1)
                uscita[riga] = Vuota;
            else
                uscita.RemoveAt(riga);
            return uscita;
        }

        return null;
    }

    /// <summary>La sezione del codice dato: la riga del titolo e quella dopo l'ultima del suo contenuto (esclusa).</summary>
    private static (int Inizio, int Fine)? Dove(IReadOnlyList<string> righe, string codice)
    {
        for (int i = 1; i < righe.Count; i++)
        {
            string pulita = righe[i].Trim();
            if (!pulita.StartsWith("**", StringComparison.Ordinal))
                continue;
            string titolo = pulita[2..].Trim();
            if (!titolo.Equals(codice, StringComparison.OrdinalIgnoreCase) && !titolo.StartsWith(codice + " ", StringComparison.OrdinalIgnoreCase))
                continue;
            int fine = i + 1;
            while (fine < righe.Count && !righe[fine].TrimStart().StartsWith("**", StringComparison.Ordinal) && !righe[fine].TrimStart().StartsWith('#'))
                fine++;
            return (i, fine);
        }

        return null;
    }

    /// <summary>Gli indici delle righe non vuote di una sezione (le sue voci).</summary>
    private static IEnumerable<int> Voci(IReadOnlyList<string> righe, (int Inizio, int Fine) sezione)
    {
        for (int i = sezione.Inizio + 1; i < sezione.Fine; i++)
        {
            if (righe[i].Trim().Length > 0)
                yield return i;
        }
    }

    [GeneratedRegex(@"^\*\*AIRAC A(?<ciclo>\d{4}) IN VIGORE DAL (?<dal>\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex FormaDellIntestazione();
}

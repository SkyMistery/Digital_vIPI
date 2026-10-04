using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Come si scrive una chiave dei metadati nella scheda.</summary>
public enum EditorDelMetadato
{
    /// <summary>Testo libero.</summary>
    Testo,

    /// <summary><c>si</c> o niente: una casella.</summary>
    SiNo,

    /// <summary>Un punto del master scelto, coi suggerimenti (<c>fix</c>, <c>trans</c>).</summary>
    Punto,

    /// <summary>Una quota in piedi o FL, oppure <c>"COO APP"</c> con la spunta (<c>initialclimb</c>).</summary>
    SalitaIniziale,

    /// <summary>Lettere da un insieme chiuso, come tasti (<c>wtc</c> L M H S, <c>cat</c> A-E).</summary>
    Lettere,

    /// <summary>Un valore da un elenco chiuso (<c>nav</c>, <c>type</c>: slice 9c, Q2b, Q2d).</summary>
    Scelta,
}

/// <summary>
/// I valori delle chiavi dei metadati di SID e STAR come li vuole il committente (prova 68, 28 settembre): il fix intero e
/// la transizione sono punti del sector, la salita iniziale è una quota (ft o FL) o «COO APP» spuntato, le categorie di
/// scia e Vref sono tasti. Carta «file per file» P6, Q2b; §M catalogo.
/// </summary>
public static partial class ValoriDeiMetadati
{
    /// <summary>Il testo di «coordinare con l'avvicinamento», scritto sempre uguale (niente refusi: è una spunta).</summary>
    public const string CooApp = "COO APP";

    /// <summary>Le lettere ammesse, nell'ordine in cui si scrivono (e in cui il doppio clic accende «le precedenti»).</summary>
    public static IReadOnlyDictionary<string, string> Lettere { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["wtc"] = "LMHS",
        ["cat"] = "ABCDE",
    };

    /// <summary>I versi di pushback e senso unico (§M: <c>oneway=E</c> = solo verso est). Prima di <see cref="Scelte"/>, che li usa.</summary>
    private static readonly string[] Rosa = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    /// <summary>
    /// I valori chiusi (slice 9c): la specifica di navigazione (Q2b, P11) e il tipo di avvicinamento (Q2d), come li
    /// scrive la carta «file per file». Un valore fuori elenco già nel file si vede e resta.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Scelte { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["nav"] = ["RNAV1", "RNP1", "RNP APCH"],
        ["type"] = ["ILS", "LOC", "RNP", "VOR", "NDB"],
        ["role"] = ["IAF", "IF", "FAF", "MAPt"],
        // Slice 12b (R2b, R6): il codice ICAO di stand e taxiway, lo stand a contatto o remoto, i versi sulla rosa.
        ["code"] = ["A", "B", "C", "D", "E", "F"],
        ["kind"] = ["contact", "remote"],
        ["pushdir"] = Rosa,
        ["oneway"] = Rosa,
    };

    /// <summary>L'editor di una chiave (senza il numero di pista davanti).</summary>
    public static EditorDelMetadato EditorDi(string chiave, bool siNo)
        => siNo ? EditorDelMetadato.SiNo
            : chiave switch
            {
                "fix" or "trans" => EditorDelMetadato.Punto,
                "initialclimb" => EditorDelMetadato.SalitaIniziale,
                _ when Lettere.ContainsKey(chiave) => EditorDelMetadato.Lettere,
                _ when Scelte.ContainsKey(chiave) => EditorDelMetadato.Scelta,
                _ => EditorDelMetadato.Testo,
            };

    /// <summary>
    /// Il valore da scrivere per una chiave, o false col perché. Un valore vuoto toglie la chiave (null). Le chiavi che
    /// non hanno una forma loro passano come sono.
    /// </summary>
    /// <param name="risolve">Per <c>fix</c> e <c>trans</c>: il master scelto conosce questo punto?</param>
    public static bool Normalizza(string chiave, string? valore, Func<string, bool>? risolve, out string? scritto, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(chiave);
        perche = null;
        scritto = string.IsNullOrWhiteSpace(valore) ? null : valore.Trim();
        if (scritto is null)
            return true;

        switch (chiave)
        {
            case "fix" or "trans":
                scritto = scritto.ToUpperInvariant();
                if (risolve is not null && !risolve(scritto))
                {
                    perche = $"«{scritto}» non è un punto del sector (il master scelto non lo conosce): scegline uno dai suggerimenti.";
                    return false;
                }

                return true;

            case "initialclimb":
                if (string.Equals(scritto, CooApp, StringComparison.OrdinalIgnoreCase))
                {
                    scritto = CooApp;
                    return true;
                }

                return Salita(scritto, out scritto, out perche);

            case "mins":
                return Minimi(scritto, out scritto, out perche);

            case "gp":
                return Pendenza(scritto, out scritto, out perche);

            case "alt":
                return Vincolo(scritto, out scritto, out perche);

            case "spd":
                return Velocita(scritto, out scritto, out perche);

            case "use":
                return Usi(scritto, out scritto, out perche);

            case "airlines":
                return Compagnie(scritto, out scritto, out perche);

            case var _ when Scelte.TryGetValue(chiave, out var scelte):
                string cercato = string.Join(' ', scritto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (scelte.FirstOrDefault(v => string.Equals(v, cercato, StringComparison.OrdinalIgnoreCase)) is { } scelta)
                {
                    scritto = scelta;
                    return true;
                }

                perche = $"«{scritto}» non è fra {string.Join(", ", scelte)}.";
                return false;

            default:
                if (!Lettere.TryGetValue(chiave, out string? ammesse))
                    return true;
                string maiuscole = scritto.ToUpperInvariant().Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
                char fuori = maiuscole.FirstOrDefault(c => !ammesse.Contains(c, StringComparison.Ordinal));
                if (fuori != '\0')
                {
                    perche = $"«{fuori}» non è fra {string.Join(" ", ammesse.ToCharArray())}.";
                    return false;
                }

                scritto = InOrdine(ammesse, maiuscole);
                if (scritto.Length == 0)
                    scritto = null;
                return true;
        }
    }

    /// <summary>
    /// La salita iniziale: <c>FL80</c> (FL a 1-3 cifre), o piedi (<c>6000</c>, <c>6000ft</c>, <c>6000'</c>) scritti
    /// <c>6000ft</c> come negli esempi di §M.
    /// </summary>
    private static bool Salita(string testo, out string? scritto, out string? perche)
    {
        string t = testo.Replace(" ", "", StringComparison.Ordinal);
        if (Livello().Match(t) is { Success: true } livello)
        {
            int fl = int.Parse(livello.Groups[1].Value, CultureInfo.InvariantCulture);
            scritto = fl is > 0 and <= 660 ? $"FL{fl}" : null;
            perche = scritto is null ? $"«{testo}»: un FL va da 1 a 660." : null;
            return scritto is not null;
        }

        if (Piedi().Match(t) is { Success: true } piedi)
        {
            int ft = int.Parse(piedi.Groups[1].Value, CultureInfo.InvariantCulture);
            scritto = ft is > 0 and <= 66000 ? $"{ft}ft" : null;
            perche = scritto is null ? $"«{testo}»: i piedi vanno da 1 a 66 000." : null;
            return scritto is not null;
        }

        scritto = null;
        perche = $"«{testo}» non è una quota: si scrive in piedi (6000, 6000ft) o in FL (FL80), oppure si spunta {CooApp}.";
        return false;
    }

    /// <summary>
    /// I minimi per categoria (Q2d): <c>A:450,B:450,C:500,D:500</c>, in piedi, nell'ordine delle categorie. Si
    /// accettano spazi e minuscole; una categoria fuori da A-E o una quota che non è un numero si rifiutano.
    /// </summary>
    private static bool Minimi(string testo, out string? scritto, out string? perche)
    {
        var perCategoria = new SortedDictionary<char, int>();
        foreach (string pezzo in testo.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] coppia = pezzo.Split(':', StringSplitOptions.TrimEntries);
            if (coppia.Length != 2 || coppia[0].Length != 1 || !Lettere["cat"].Contains(char.ToUpperInvariant(coppia[0][0]), StringComparison.Ordinal))
            {
                scritto = null;
                perche = $"«{pezzo}»: i minimi si scrivono categoria:piedi (A:450), con le categorie A-E.";
                return false;
            }

            if (!int.TryParse(coppia[1], NumberStyles.None, CultureInfo.InvariantCulture, out int piedi) || piedi is <= 0 or > 10000)
            {
                scritto = null;
                perche = $"«{pezzo}»: la quota dei minimi è in piedi, da 1 a 10 000.";
                return false;
            }

            perCategoria[char.ToUpperInvariant(coppia[0][0])] = piedi;
        }

        perche = null;
        scritto = perCategoria.Count == 0 ? null : string.Join(',', perCategoria.Select(c => $"{c.Key}:{c.Value}"));
        return true;
    }

    /// <summary>
    /// Il vincolo di quota di un punto (Q2, §M): <c>+FL80</c> (a o sopra), <c>-5000</c> (a o sotto), <c>=4000</c> (a),
    /// <c>4000/6000</c> (fra). Una quota è in piedi o in FL; il segno davanti è obbligatorio, fuori da un «fra»: un
    /// numero da solo non dice se è un minimo, un massimo o la quota.
    /// </summary>
    private static bool Vincolo(string testo, out string? scritto, out string? perche)
    {
        string t = testo.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (t.Split('/') is [var basso, var alto])
        {
            if (Quota(basso) is { } b && Quota(alto) is { } a)
            {
                scritto = $"{b}/{a}";
                perche = null;
                return true;
            }
        }
        else if (t.Length > 1 && t[0] is '+' or '-' or '=' && Quota(t[1..]) is { } quota)
        {
            scritto = t[0] + quota;
            perche = null;
            return true;
        }

        scritto = null;
        perche = $"«{testo}»: si scrive +FL80 (a o sopra), -5000 (a o sotto), =4000 (a) o 4000/6000 (fra), in piedi o FL.";
        return false;
    }

    /// <summary>Una quota di un vincolo: <c>FL80</c>, o piedi (<c>5000</c>, <c>5000ft</c>) scritti senza unità.</summary>
    private static string? Quota(string t)
    {
        if (Livello().Match(t) is { Success: true } livello
            && int.Parse(livello.Groups[1].Value, CultureInfo.InvariantCulture) is > 0 and <= 660 and var fl)
            return $"FL{fl}";
        if (Piedi().Match(t) is { Success: true } piedi
            && int.Parse(piedi.Groups[1].Value, CultureInfo.InvariantCulture) is > 0 and <= 66000 and var ft)
            return ft.ToString(CultureInfo.InvariantCulture);
        return null;
    }

    /// <summary>Il vincolo di velocità di un punto (Q2, §M): nodi con il segno, <c>-210</c> (al più), <c>+180</c>, <c>=230</c>.</summary>
    private static bool Velocita(string testo, out string? scritto, out string? perche)
    {
        string t = testo.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant().Replace("KT", "", StringComparison.Ordinal);
        if (t.Length > 1 && t[0] is '+' or '-' or '='
            && int.TryParse(t[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int nodi) && nodi is >= 60 and <= 400)
        {
            scritto = t[0] + nodi.ToString(CultureInfo.InvariantCulture);
            perche = null;
            return true;
        }

        scritto = null;
        perche = $"«{testo}»: la velocità si scrive in nodi col segno, -210 (al più), +180 (almeno) o =230, da 60 a 400.";
        return false;
    }

    /// <summary>
    /// Gli usi di uno stand (R2b): uno o più fra quelli di <see cref="PropostaDelloStand.Usi"/>, con la virgola, nell'ordine
    /// dell'elenco (<c>schengen,cargo</c>).
    /// </summary>
    private static bool Usi(string testo, out string? scritto, out string? perche)
    {
        var scelti = testo.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => u.ToLowerInvariant()).ToList();
        if (scelti.FirstOrDefault(u => !PropostaDelloStand.Usi.ContainsKey(u)) is { } fuori)
        {
            scritto = null;
            perche = $"«{fuori}» non è un uso: {string.Join(", ", PropostaDelloStand.Usi.Keys)} (anche più d'uno, con la virgola).";
            return false;
        }

        perche = null;
        scritto = scelti.Count == 0 ? null : string.Join(',', PropostaDelloStand.Usi.Keys.Where(scelti.Contains));
        return true;
    }

    /// <summary>Le compagnie abituali di uno stand (R2b): i codici ICAO a tre lettere, con la virgola (<c>ITY,RYR</c>).</summary>
    private static bool Compagnie(string testo, out string? scritto, out string? perche)
    {
        var codici = testo.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (codici.FirstOrDefault(c => c.Length != 3 || !c.All(char.IsAsciiLetter)) is { } fuori)
        {
            scritto = null;
            perche = $"«{fuori}»: una compagnia si scrive col codice ICAO di tre lettere (ITY, RYR), più d'una con la virgola.";
            return false;
        }

        perche = null;
        scritto = codici.Count == 0 ? null : string.Join(',', codici);
        return true;
    }

    /// <summary>La pendenza del sentiero di discesa (Q2d), in gradi con un decimale: <c>3.0</c>. Da 1 a 10 gradi.</summary>
    private static bool Pendenza(string testo, out string? scritto, out string? perche)
    {
        if (decimal.TryParse(testo.Replace(',', '.').Replace("°", "", StringComparison.Ordinal), NumberStyles.Number,
                             CultureInfo.InvariantCulture, out decimal gradi) && gradi is >= 1 and <= 10)
        {
            scritto = gradi.ToString("0.0#", CultureInfo.InvariantCulture);
            perche = null;
            return true;
        }

        scritto = null;
        perche = $"«{testo}»: la pendenza è in gradi, da 1 a 10 (3.0).";
        return false;
    }

    [GeneratedRegex(@"^(?i:FL)(\d{1,3})$")]
    private static partial Regex Livello();

    [GeneratedRegex(@"^(\d{1,5})(?i:ft|')?$")]
    private static partial Regex Piedi();

    /// <summary>
    /// Un clic su un tasto delle lettere. Il primo clic accende o spegne la lettera; il secondo di un doppio clic
    /// (<paramref name="doppio"/>) porta le lettere <b>precedenti</b> allo stato di quella cliccata (committente: doppio
    /// clic su H accende o spegne anche L e M). Torna le lettere scritte, in ordine.
    /// </summary>
    public static string Clic(string chiave, string? attuale, char lettera, bool doppio)
    {
        string ammesse = Lettere[chiave];
        var accese = new HashSet<char>((attuale ?? string.Empty).ToUpperInvariant());
        int dove = ammesse.IndexOf(lettera, StringComparison.Ordinal);
        if (dove < 0)
            return InOrdine(ammesse, accese);

        if (!doppio)
        {
            if (!accese.Remove(lettera))
                accese.Add(lettera);
        }
        else
        {
            bool accesa = accese.Contains(lettera);
            foreach (char prima in ammesse[..dove])
            {
                if (accesa)
                    accese.Add(prima);
                else
                    accese.Remove(prima);
            }
        }

        return InOrdine(ammesse, accese);
    }

    private static string InOrdine(string ammesse, IEnumerable<char> accese)
    {
        var insieme = accese.ToHashSet();
        return new string([.. ammesse.Where(insieme.Contains)]);
    }
}

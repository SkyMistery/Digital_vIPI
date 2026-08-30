using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Che famiglia è un volume, e quali famiglie si possono usare. **La lista bianca sta qui, in un posto solo**:
/// il committente ha deciso il 29 agosto 2026 che le aree regolamentate (R, P, D) e le altre aree —
/// TRA, acrobazia, parchi — vengono **solo** dal database IVAO, e dal file non se ne prende nessuna.
///
/// <para>Il file però si legge e si conserva <b>intero</b>: le famiglie escluse restano in catalogo con detto
/// perché, invece di sparire in un ramo del parser dove nessuno le ritrova.</para>
///
/// <para>⚠️ È un <b>dato</b>, non una catena di <c>if</c> sparsa: chi domani vorrà mostrare le TRA cambia una
/// riga qui e non va a cercare i controlli nelle pagine.</para>
/// </summary>
public static class AirspaceFamilies
{
    /// <summary>Le famiglie che si possono agganciare a un settore e mostrare.</summary>
    private static readonly HashSet<AirspaceFamily> Utilizzabili =
    [
        AirspaceFamily.Ctr,
        AirspaceFamily.Cta,
        AirspaceFamily.Tma,
        AirspaceFamily.Atz,
        AirspaceFamily.Fir,
        AirspaceFamily.Tmz,
    ];

    /// <summary>Vero se la famiglia si può agganciare e mostrare.</summary>
    public static bool IsUsable(AirspaceFamily family) => Utilizzabili.Contains(family);

    /// <summary>Le famiglie utilizzabili, in ordine di enum: l'elenco che la UI accende.</summary>
    public static IReadOnlyList<AirspaceFamily> Usable { get; } = Utilizzabili.OrderBy(f => (int)f).ToList();

    /// <summary>
    /// Perché una famiglia non è utilizzabile. Null = lo è. Non è il testo che si mostra — quello lo traduce
    /// la UI — ma la ragione, che è una sola e vale la pena scriverla vicino alla regola.
    /// </summary>
    public static string? WhyNotUsable(AirspaceFamily family) => IsUsable(family)
        ? null
        : "le aree regolamentate e le altre aree vengono dal catalogo IVAO";

    // Le categorie che AirspaceConverter scrive per esteso: una corrispondenza secca, senza indovinare.
    private static readonly Dictionary<string, AirspaceFamily> PerCategoria = new(StringComparer.OrdinalIgnoreCase)
    {
        ["control traffic region"] = AirspaceFamily.Ctr,
        ["terminal manoeuvring area"] = AirspaceFamily.Tma,
        ["terminal maneuvering area"] = AirspaceFamily.Tma,
        ["flight information region"] = AirspaceFamily.Fir,
        ["transponder mandatory zone"] = AirspaceFamily.Tmz,
        ["danger area"] = AirspaceFamily.Danger,
        ["prohibited area"] = AirspaceFamily.Prohibited,
        ["restricted area"] = AirspaceFamily.Restricted,
        ["gliding area"] = AirspaceFamily.Gliding,
        ["over the horizon"] = AirspaceFamily.Other,
    };

    private static readonly Regex Parole = new("[A-Z]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// La famiglia di un volume, dalla categoria del file e dal suo nome.
    ///
    /// <para>⚠️ <b>Sulle classi di spazio aereo la categoria non basta, e decide il nome.</b> Misurato sul file
    /// del 15 luglio 2026: la classe D contiene 104 CTA <b>e</b> 49 fra ATZ e MATZ; la classe C contiene un
    /// solo volume, ed è <c>PISA CTR Z3</c>, cioè un CTR. Fermarsi alla categoria vorrebbe dire chiamare CTA
    /// mezzo centinaio di zone di traffico d'aeroporto — e sono proprio quelle che al punto 2 devono fare da
    /// ripiego alle torri senza poligono.</para>
    ///
    /// <para>Senza nessuna parola riconoscibile decide la classe: A/B/C/D/E è spazio controllato
    /// (<see cref="AirspaceFamily.Cta"/>), G non lo è (<see cref="AirspaceFamily.Other"/>) — che sul file vero
    /// vuol dire <c>LAMPEDUSA</c> fra le CTA e <c>FLYING CLUIB SABAUDIA(AIRWRK)</c> fra le altre, che è giusto:
    /// un campo di airwork non è una ATZ.</para>
    /// </summary>
    public static AirspaceFamily Classify(string? category, string? name)
    {
        var cat = (category ?? "").Trim();
        if (PerCategoria.TryGetValue(cat, out var diretta)) return diretta;

        if (!cat.StartsWith("airspace class", StringComparison.OrdinalIgnoreCase)) return AirspaceFamily.Other;

        var parole = Parole.Matches((name ?? "").ToUpperInvariant()).Select(m => m.Value).ToHashSet();
        if (parole.Contains("ATZ") || parole.Contains("MATZ")) return AirspaceFamily.Atz;
        if (parole.Contains("CTR")) return AirspaceFamily.Ctr;
        if (parole.Contains("CTA")) return AirspaceFamily.Cta;

        return ClassOf(cat) is "A" or "B" or "C" or "D" or "E" ? AirspaceFamily.Cta : AirspaceFamily.Other;
    }

    /// <summary>
    /// La lettera della classe di spazio aereo (<c>"Airspace class D"</c> → <c>"D"</c>), o null se la categoria
    /// non ne dichiara una. Si conserva perché è un dato dell'AIP che il lettore non deve buttare via: la
    /// famiglia dice a che cosa serve il volume, la classe dice che regole ci valgono dentro.
    /// </summary>
    public static string? ClassOf(string? category)
    {
        var cat = (category ?? "").Trim();
        if (!cat.StartsWith("airspace class", StringComparison.OrdinalIgnoreCase)) return null;

        var resto = cat["airspace class".Length..].Trim().ToUpperInvariant();
        return resto.Length == 1 && resto[0] is >= 'A' and <= 'G' ? resto : null;
    }
}

namespace Aip.Aurora;

/// <summary>Come scrivere le aree: il disegno, e come nominarle e colorarle.</summary>
public sealed record AuroraOptions
{
    public OutlineOptions Outline { get; init; } = new();

    /// <summary>
    /// Togliere le lettere dello Stato in testa all'identificativo (<c>LI P901</c> → <c>P901</c>), come fa il sector
    /// italiano nei suoi <c>italy.prohibit</c>, <c>.restrict</c>, <c>.danger</c>.
    /// </summary>
    public bool StripState { get; init; } = true;

    /// <summary>Il nome del colore (6° campo, chiave di <c>colors.def</c> o dello schema di Aurora) per tipo.</summary>
    public Func<AirspaceType, string> Color { get; init; } = DefaultColor;

    /// <summary>
    /// P, R e D coi colori che il sector usa già; gli altri col nome del tipo (<c>CTR</c>, <c>TMA</c>): quali colori
    /// abbiano nello schema di Aurora si decide con la slice 1 (ENR 2), quando si portano nel sector.
    /// </summary>
    public static string DefaultColor(AirspaceType type) => type switch
    {
        AirspaceType.Prohibited => "PROHIBIT",
        AirspaceType.Restricted => "RESTRICT",
        AirspaceType.Danger => "DANGER",
        _ => type.ToString().ToUpperInvariant(),
    };
}

/// <summary>Le righe di un file e quel che il converter ha da dire.</summary>
public sealed record AuroraResult(IReadOnlyList<string> Lines, IReadOnlyList<AuroraIssue> Issues);

/// <summary>
/// Le aree come righe <c>.geo</c> di Aurora (sezione <c>[GEO]</c>, e i file delle aree P/R/D del sector): a
/// segmenti, <c>LatInizio;LonInizio;LatFine;LonFine;Colore;Nome;</c>, l'anello chiuso sul primo vertice. Ogni area
/// è un blocco della carta «file per file» §M — la dichiarazione coi limiti e la classe, <c>//@START</c>, i
/// segmenti, <c>//@END</c> — col commento sopra (identificativo e nome come li stampa l'AIP); il ciclo AIRAC come
/// <c>//@source</c> nella prima riga.
/// <para>Attività e note dell'AIP restano nel modello (l'AIP espanso): nelle righe di Aurora non servono.</para>
/// </summary>
public static class AuroraGeo
{
    public static AuroraResult Write(IEnumerable<Airspace> areas, string state, string? airac, AuroraOptions? options = null)
    {
        var o = options ?? new AuroraOptions();
        var righe = new List<string>();
        var problemi = new List<AuroraIssue>();
        var nomi = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(airac))
            righe.Add("//@source=AIRAC" + airac.Trim());

        foreach (var area in areas)
        {
            var nome = Nome(area, state, o, nomi, problemi);
            var punti = Outline.Points(area.Geometry, o.Outline, problemi, area.Key);
            var colore = o.Color(area.Type);

            if (righe.Count > 0)
                righe.Add("");
            righe.Add("//" + area.Key);
            righe.Add(Dichiarazione(nome, area));
            righe.Add("//@START");
            for (var i = 0; i < punti.Count; i++)
            {
                var da = punti[i];
                var a = punti[(i + 1) % punti.Count];
                righe.Add($"{AuroraText.Coordinates(da)};{AuroraText.Coordinates(a)};{colore};{nome};");
            }
            righe.Add("//@END \"" + nome + "\"");
        }

        return new AuroraResult(righe, problemi);
    }

    private static string Dichiarazione(string nome, Airspace area)
    {
        var riga = $"//@\"{nome}\" lower={AuroraText.Limit(area.Lower)} upper={AuroraText.Limit(area.Upper)}";
        if (!string.IsNullOrWhiteSpace(area.Class))
            riga += " class=" + AuroraText.Value(area.Class.Trim());
        return riga;
    }

    private static string Nome(Airspace area, string state, AuroraOptions o, HashSet<string> usati, List<AuroraIssue> problemi)
    {
        var nome = area.Designator.Trim();
        var prefisso = state.Trim() + " ";
        if (o.StripState && nome.StartsWith(prefisso, StringComparison.Ordinal) && nome.Length > prefisso.Length)
            nome = nome[prefisso.Length..].Trim();

        var pulito = nome.Replace(';', ',').Replace('"', '\'');
        if (pulito != nome)
            problemi.Add(new AuroraIssue(AuroraIssueKind.NameChanged, area.Key, pulito));
        nome = pulito;

        if (!usati.Add(nome))
        {
            var altro = area.Name is { Length: > 0 } n ? $"{nome} {n.Replace(';', ',').Replace('"', '\'')}" : nome;
            for (var i = 2; !usati.Add(altro); i++)
                altro = $"{nome} ({i})";
            problemi.Add(new AuroraIssue(AuroraIssueKind.DuplicateName, area.Key, altro));
            nome = altro;
        }

        return nome;
    }
}

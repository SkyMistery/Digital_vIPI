using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// Uno strato della mappa: un gruppo di file che si accende e si spegne insieme (carta F3 §2.2 passo 4).
/// </summary>
/// <param name="Id">Il nome nell'indirizzo dell'endpoint: minuscolo, senza spazi.</param>
/// <param name="Nome">Come si legge a schermo.</param>
/// <param name="Sfondo">Le coste e i confini: sempre accesi, e disegnati sotto tutto il resto.</param>
public sealed record TipoDiStrato(string Id, string Nome, bool Sfondo = false);

/// <summary>Uno strato con dentro le forme che gli toccano, nell'ordine dei file e dei record.</summary>
public sealed record StratoDellaMappa(TipoDiStrato Tipo, IReadOnlyList<FormaDellaMappa> Forme)
{
    public string Id => Tipo.Id;

    public int Punti => Forme.Sum(f => f.Punti);
}

/// <summary>
/// Quali strati ha la mappa, e in quale finisce ogni file (carta F3, slice 4).
/// <para>La regola sta qui e non nella pagina per due motivi: l'endpoint della mappa e l'elenco degli strati devono
/// dare la <b>stessa</b> risposta (altrimenti si accende una casella che non porta niente), e la classificazione è
/// una conoscenza sul sector — <c>GEO/itgeo.geo</c> è lo sfondo di tutti, gli altri <c>.geo</c> sono disegni di un
/// aeroporto; un <c>.restrict</c> è un'area P/R/D anche se il lettore è lo stesso del <c>.geo</c>.</para>
/// <para>Un file che non finisce in nessuno strato (<c>.frq</c>, <c>.atis</c>, <c>.txt</c>) non ha geometria: non è
/// un buco, è che su una mappa non ci sta.</para>
/// </summary>
public static class StratiDellaMappa
{
    /// <summary>Le coste e i confini d'Italia: il file che fa da sfondo, l'unico acceso di suo.</summary>
    internal const string SfondoGeo = "GEO/itgeo.geo";

    public static readonly TipoDiStrato Sfondo = new("sfondo", "Coste e confini", Sfondo: true);

    /// <summary>Gli strati nell'ordine in cui si disegnano: lo sfondo sotto, i punti sopra a tutto.</summary>
    public static IReadOnlyList<TipoDiStrato> Tipi { get; } =
    [
        Sfondo,
        new("geo", "Disegni .geo"),
        new("settori", "Settori e bordi"),
        new("aree", "Aree P/R/D"),
        new("mva", "MVA"),
        new("aerovie", "Aerovie"),
        new("procedure", "SID e STAR"),
        new("vfr", "Rotte e punti VFR"),
        new("attese", "Attese"),
        new("terra", "Aeroporti a terra"),
        new("piste", "Piste"),
        new("radioassistenze", "VOR e NDB"),
        new("punti", "Punti"),
    ];

    private static readonly Dictionary<string, TipoDiStrato> PerId =
        Tipi.ToDictionary(t => t.Id, StringComparer.Ordinal);

    public static TipoDiStrato? PerNome(string id)
        => id is not null && PerId.TryGetValue(id, out var tipo) ? tipo : null;

    /// <summary>In quale strato va un file, o <c>null</c> se non si disegna. Il percorso è relativo alla radice.</summary>
    public static TipoDiStrato? DiFile(string relativo)
    {
        ArgumentNullException.ThrowIfNull(relativo);

        string dritto = relativo.Replace('\\', '/');
        if (dritto.EndsWith(SfondoGeo, StringComparison.OrdinalIgnoreCase))
            return Sfondo;

        string estensione = Path.GetExtension(dritto).TrimStart('.').ToLowerInvariant();
        return estensione switch
        {
            "geo" => PerId["geo"],
            "restrict" or "prohibit" or "danger" => PerId["aree"],
            "tfl" or "artcc" or "hartcc" or "lartcc" => PerId["settori"],
            "mva" => PerId["mva"],
            "lairway" or "hairway" => PerId["aerovie"],
            "sid" or "str" => PerId["procedure"],
            "vrt" or "vfi" => PerId["vfr"],
            "hold" => PerId["attese"],
            "pol" or "txi" or "gts" => PerId["terra"],
            "rw" => PerId["piste"],
            "vor" or "ndb" => PerId["radioassistenze"],
            "fix" or "ap" => PerId["punti"],
            _ => null,
        };
    }

    /// <summary>
    /// Tutti gli strati di una sessione, forme comprese. I punti scritti per nome li risolve il catalogo del master
    /// scelto: cambiando master cambiano i nomi risolti, ed è giusto così (slice 3a).
    /// </summary>
    public static IReadOnlyList<StratoDellaMappa> DiSessione(SessioneAperta sessione, CatalogoDeiPunti? catalogo)
    {
        ArgumentNullException.ThrowIfNull(sessione);

        var forme = new Dictionary<string, List<FormaDellaMappa>>(StringComparer.Ordinal);
        foreach (var tipo in Tipi)
            forme[tipo.Id] = [];

        // Nell'ordine dei file: due aperture della stessa cartella danno la stessa mappa, e il confronto è possibile.
        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (DiFile(file.Relativo) is not { } tipo)
                continue;
            forme[tipo.Id].AddRange(Geometria.DelFile(file, catalogo));
        }

        return [.. Tipi.Select(t => new StratoDellaMappa(t, forme[t.Id]))];
    }
}

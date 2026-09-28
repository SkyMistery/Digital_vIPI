using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>
/// L'uscita che manca a una forma (I2: «bordo» in un <c>.geo</c>, «riempimento» in un <c>.pol</c>): dove va, con che tipo,
/// e le righe da aggiungere in fondo a quel file.
/// </summary>
/// <param name="Bordo">Vero: si aggiunge il bordo (dal <c>.pol</c> al <c>.geo</c>); falso: il riempimento.</param>
/// <param name="File">Il file che la riceve.</param>
/// <param name="Tipo">Il tipo della linea del bordo, o il colore del riempimento.</param>
/// <param name="Righe">Le righe da aggiungere in fondo (una vuota, il commento col nome del gruppo, i dati).</param>
public sealed record UscitaDellaForma(bool Bordo, string File, string Tipo, IReadOnlyList<string> Righe);

/// <summary>
/// Il bordo e il riempimento di una forma (lotto «Subito» slice 8d, «file per file» I2, H10): la stessa forma in un
/// <c>.geo</c> (la linea, col suo tipo) e in un <c>.pol</c> (il poligono, col suo colore). Le due uscite sono legate come
/// famiglia (slice 8a-8c); qui c'è quella che manca, e il controllo del confine dello scalo con la sua erba.
/// </summary>
/// <remarks>
/// Le regole vengono dalla misura sul fork (28 settembre, 1 594 coppie): ogni <c>xx_ad_gnd.pol</c> ha i suoi bordi in un
/// solo <c>.geo</c>, <c>GEO/lixx.geo</c> (92 su 94; gli altri due hanno lo stesso nome: <c>limw</c>, <c>lsza</c>); il tipo
/// della linea è quello del riempimento per BUILDING, TAXIWAY, APRON, RUNWAY, e BUILDING per CONCRETE, GRASS, HOLE (quasi
/// sempre); il confine dello scalo è il gruppo <c>ad_boundary</c> (con o senza le due lettere dello scalo davanti) e la
/// sua erba <c>ad_boundary_Polygon</c>, GRASS. Nel <c>.geo</c> la linea torna al primo punto; nel <c>.pol</c> no (Aurora
/// chiude da sola: 1 673 poligoni su 1 751).
/// </remarks>
public static class UsciteDellaForma
{
    /// <summary>Il gruppo del confine dello scalo in un <c>.geo</c>: <c>ad_boundary</c>, <c>MC_ad_boundary</c>, <c>mw_boundary</c>.</summary>
    public static readonly Regex Confine = new(@"^([A-Za-z]{2}_)?(ad_)?boundary$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Il gruppo della sua erba in un <c>.pol</c>: lo stesso nome, con <c>_Polygon</c>.</summary>
    public static readonly Regex Erba = new(@"^([A-Za-z]{2}_)?(ad_)?boundary_polygon$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> TipiUguali = new(["BUILDING", "TAXIWAY", "APRON", "RUNWAY"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// L'uscita che manca alla forma del record, o null se ce le ha tutte e due, o non è un poligono di un <c>.pol</c> né
    /// una linea chiusa di un <c>.geo</c>. Il perché, se manca e non si sa dove metterla.
    /// </summary>
    public static UscitaDellaForma? Di(SessioneAperta sessione, FormeUguali forme, IReadOnlyList<FormaDellaMappa> formeDelFile,
                                       string file, int record, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);
        ArgumentNullException.ThrowIfNull(formeDelFile);
        perche = null;
        if (sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record < 0 || record >= conRecord.RecordDelModello.Count)
            return null;

        bool pol = Estensione(file) == ".pol";
        if (!pol && Estensione(file) != ".geo")
            return null;
        var parti = forme.Di(file, record);
        string altra = pol ? ".geo" : ".pol";
        if (parti.Any(p => p.Copie.Any(c => c.Uguale && Estensione(c.Dove.File) == altra)))
            return null;

        string? gruppo = VociDellaSelezione.GruppoDelRecord(sessione.File[file], record, out _)?.Nome;
        if (pol)
        {
            if (conRecord.RecordDelModello[record] is not Polygon poligono || poligono.Vertices.Count < 3)
                return null;
            if (GeoDelPol(sessione, forme, file) is not { } geo)
            {
                perche = "Non c'è un .geo dello scalo dove mettere il bordo (GEO/li…geo): crearne uno è per il futuro.";
                return null;
            }

            string tipo = TipoDelBordo(poligono.FillColor);
            string nome = gruppo is { Length: > 0 } g && g.EndsWith("_Polygon", StringComparison.OrdinalIgnoreCase) ? g[..^"_Polygon".Length] : gruppo ?? tipo;
            return new UscitaDellaForma(true, geo, tipo, RigheDelBordo(poligono.Vertices, tipo, nome));
        }

        // Una linea di un .geo: la forma della mappa che contiene il record, chiusa. Solo nei .geo degli scali (non lo
        // sfondo itgeo.geo, non le marcature di RW_MARKINGS) e non le linee RUNWAY: sul fork le 458 linee chiuse RUNWAY
        // senza riempimento sono soglie e numeri di pista disegnati a tratti, non superfici da riempire.
        if (!Regex.IsMatch(file.Replace('\\', '/'), @"/GEO/li[a-z]{2}\.geo$", RegexOptions.IgnoreCase))
            return null;
        var linea = formeDelFile.Where(f => f.Record <= record).MaxBy(f => f.Record);
        if (linea is not { Tratti.Count: 1 } || linea.Tratti[0] is not { Count: >= 4 } punti
            || FormeUguali.Chiave(punti[0]) != FormeUguali.Chiave(punti[^1])
            || string.Equals(linea.Tratto?.Trim(), "RUNWAY", StringComparison.OrdinalIgnoreCase))
            return null;
        if (PolDelGeo(sessione, forme, file) is not { } dove)
        {
            perche = "Non c'è un .pol dello scalo dove mettere il riempimento (GND_LAYOUT/…_ad_gnd.pol): crearne uno è per il futuro.";
            return null;
        }

        string riempimento = gruppo is { } nomeDelGruppo && Confine.IsMatch(nomeDelGruppo)
            ? "GRASS"
            : TipiUguali.Contains(linea.Tratto ?? "") ? linea.Tratto!.ToUpperInvariant() : "BUILDING";
        return new UscitaDellaForma(false, dove, riempimento, RigheDelRiempimento(punti.Take(punti.Count - 1).ToList(), riempimento,
            (gruppo ?? riempimento) + "_Polygon"));
    }

    /// <summary>Il tipo della linea del bordo per un riempimento: lo stesso per BUILDING, TAXIWAY, APRON, RUNWAY, se no BUILDING.</summary>
    public static string TipoDelBordo(string riempimento)
        => TipiUguali.Contains(riempimento.Trim()) ? riempimento.Trim().ToUpperInvariant() : "BUILDING";

    /// <summary>Il <c>.geo</c> dove stanno i bordi di questo <c>.pol</c>: quello delle sue coppie, se no <c>GEO/lixx.geo</c>.</summary>
    public static string? GeoDelPol(SessioneAperta sessione, FormeUguali forme, string pol)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);
        if (Accoppiati(sessione, forme, pol, ".geo") is { } giaUsato)
            return giaUsato;
        string radice = Path.GetFileNameWithoutExtension(pol);
        string it = CartellaIt(pol);
        string[] candidati = Regex.Match(radice, "^([A-Za-z]{2})_ad_gnd$", RegexOptions.IgnoreCase) is { Success: true } m
            ? [$"{it}/GEO/li{m.Groups[1].Value}.geo"]
            : [$"{it}/GEO/{Regex.Replace(radice, "_ad_gnd$", "", RegexOptions.IgnoreCase)}.geo"];
        return candidati.Select(c => Cerca(sessione, c)).FirstOrDefault(c => c is not null);
    }

    /// <summary>Il <c>.pol</c> dove sta il riempimento di questo <c>.geo</c>: quello delle sue coppie, se no <c>GND_LAYOUT/xx_ad_gnd.pol</c>.</summary>
    public static string? PolDelGeo(SessioneAperta sessione, FormeUguali forme, string geo)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);
        if (Accoppiati(sessione, forme, geo, ".pol") is { } giaUsato)
            return giaUsato;
        string radice = Path.GetFileNameWithoutExtension(geo);
        string it = CartellaIt(geo);
        var candidati = new List<string>();
        if (Regex.Match(radice, "^li([A-Za-z]{2})$", RegexOptions.IgnoreCase) is { Success: true } m)
            candidati.Add($"{it}/GND_LAYOUT/{m.Groups[1].Value.ToLowerInvariant()}_ad_gnd.pol");
        candidati.Add($"{it}/GND_LAYOUT/{radice}_ad_gnd.pol");
        candidati.Add($"{it}/GND_LAYOUT/{radice}.pol");
        return candidati.Select(c => Cerca(sessione, c)).FirstOrDefault(c => c is not null);
    }

    /// <summary>
    /// Il controllo del confine dello scalo e della sua erba (H10): un confine (<c>ad_boundary</c> in un <c>.geo</c>) senza
    /// un poligono uguale in un <c>.pol</c>, e un'erba (<c>ad_boundary_Polygon</c>) senza una linea uguale in un <c>.geo</c>.
    /// Avviso <see cref="Regola.ConfineSenzaErba"/>, sulla prima riga del record.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Problemi(SessioneAperta sessione, IEnumerable<StratoDellaMappa> strati, FormeUguali forme)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(strati);
        ArgumentNullException.ThrowIfNull(forme);
        foreach (var forma in strati.SelectMany(s => s.Forme).Where(f => Estensione(f.File) is ".geo" or ".pol"))
        {
            bool pol = Estensione(forma.File) == ".pol";
            if (!sessione.File.TryGetValue(forma.File, out var aperto) || aperto is not IFileConRecord conRecord
                || VociDellaSelezione.GruppoDelRecord(aperto, forma.Record, out _)?.Nome is not { } gruppo
                || !(pol ? Erba : Confine).IsMatch(gruppo))
                continue;
            string altra = pol ? ".geo" : ".pol";
            var copie = forme.Di(forma.File, forma.Record).SelectMany(p => p.Copie).Where(c => Estensione(c.Dove.File) == altra).ToList();
            if (copie.Any(c => c.Uguale))
                continue;

            var simile = copie.FirstOrDefault();
            string dettaglio = (pol
                                   ? $"L'erba dello scalo ({gruppo}) non ha il suo confine: nessuna linea uguale in un .geo"
                                   : $"Il confine dello scalo ({gruppo}) non ha la sua erba: nessun poligono uguale in un .pol")
                               + (simile is null ? "." : $"; in {Path.GetFileName(simile.Dove.File)} ce n'è uno simile, diverso di {simile.SoloQui + simile.SoloLa} vertici.");
            var riga = conRecord.RigheDelRecord(forma.Record, 0).FirstOrDefault(r => r.DelRecord);
            yield return new ProblemaDelSector(Regola.ConfineSenzaErba, forma.File, riga?.Numero ?? 0, riga?.Testo ?? "", dettaglio);
        }
    }

    // Il file dell'altro tipo dove stanno già le copie uguali delle forme di questo file (il più usato).
    private static string? Accoppiati(SessioneAperta sessione, FormeUguali forme, string file, string altra)
        => sessione.File.GetValueOrDefault(file) is IFileConRecord conRecord
            ? Enumerable.Range(0, conRecord.RecordDelModello.Count)
                .SelectMany(r => forme.Di(file, r)).SelectMany(p => p.Copie)
                .Where(c => c.Uguale && Estensione(c.Dove.File) == altra)
                .GroupBy(c => c.Dove.File, StringComparer.OrdinalIgnoreCase).MaxBy(g => g.Count())?.Key
            : null;

    private static string? Cerca(SessioneAperta sessione, string relativo)
        => sessione.File.Keys.FirstOrDefault(k => string.Equals(k, relativo, StringComparison.OrdinalIgnoreCase));

    // La cartella dei dati (…/Include/IT): quella sopra GND_LAYOUT o GEO.
    private static string CartellaIt(string relativo)
    {
        string cartella = Path.GetDirectoryName(relativo)!.Replace('\\', '/');
        return cartella[..cartella.LastIndexOf('/')];
    }

    private static string Estensione(string file) => Path.GetExtension(file).ToLowerInvariant();

    /// <summary>Il bordo in righe di un <c>.geo</c>: un segmento per lato, fino a tornare al primo punto; coordinate col punto.</summary>
    private static List<string> RigheDelBordo(IList<Coordinate> vertici, string tipo, string nome)
    {
        var anello = vertici.ToList();
        if (anello.Count > 1 && FormeUguali.Chiave(anello[0]) == FormeUguali.Chiave(anello[^1]))
            anello.RemoveAt(anello.Count - 1);
        var righe = new List<string> { "", "//" + nome };
        for (int i = 0; i < anello.Count; i++)
        {
            var (a, b) = (anello[i], anello[(i + 1) % anello.Count]);
            righe.Add($"{CoordinateConverter.LatitudeToDottedDms(a.LatitudeDeg)};{CoordinateConverter.LongitudeToDottedDms(a.LongitudeDeg)};"
                      + $"{CoordinateConverter.LatitudeToDottedDms(b.LatitudeDeg)};{CoordinateConverter.LongitudeToDottedDms(b.LongitudeDeg)};{tipo};");
        }

        return righe;
    }

    /// <summary>Il riempimento in righe di un <c>.pol</c>: la testa statica, poi i vertici (senza ripetere il primo).</summary>
    private static List<string> RigheDelRiempimento(IReadOnlyList<Coordinate> vertici, string colore, string nome)
    {
        var righe = new List<string> { "", "//" + nome, $"STATIC;{colore};1;{colore};" };
        righe.AddRange(vertici.Select(v => $"{CoordinateConverter.LatitudeToDottedDms(v.LatitudeDeg)};{CoordinateConverter.LongitudeToDottedDms(v.LongitudeDeg)};"));
        return righe;
    }
}

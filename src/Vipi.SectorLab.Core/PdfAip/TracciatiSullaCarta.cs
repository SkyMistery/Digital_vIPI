using System.Globalization;
using Aip;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Un punto ridisegnato sopra la carta posata (carta «Gli aeroporti nel Lab», S4): dove sta, il fix dell'AIP se il punto è un
/// fix (preso col clic sul suo simbolo o scritto per nome), e il tratto che ci arriva com'è stampato sulla carta: il path
/// terminator, la quota, la velocità, e per un arco (<c>RF</c>) il centro e il verso.
/// </summary>
public sealed record PuntoDelTracciato(
    double Lat,
    double Lon,
    string? Fix,
    string Tipo = "TF",
    string? Quota = null,
    string? Velocita = null,
    string? Centro = null,
    string? Verso = null);

/// <summary>
/// Dal tracciato alla procedura (S4): una parte nuova di una carta di avvicinamento, o dei tratti in coda a una parte che c'è
/// (il mancato che la tabella non ha). La procedura va nell'AIP espanso come correzione, con la carta per fonte: è il
/// meccanismo delle correzioni, e la carta si completa parte per parte.
/// <para>Un punto che non è un fix ha la sua posizione e <see cref="DallaCarta"/> al posto della definizione: sulla mappa si
/// disegna come i punti calcolati della prosa, e nella scheda si legge da dove viene.</para>
/// </summary>
public static class TracciatiSullaCarta
{
    /// <summary>I tratti che si tracciano su una carta: l'inizio, dritto al punto, una rotta al punto, diretto, un arco.</summary>
    public static readonly IReadOnlyList<string> Tipi = ["IF", "TF", "CF", "DF", "RF"];

    /// <summary>La definizione di un punto senza nome preso sulla carta.</summary>
    public const string DallaCarta = "dalla carta";

    /// <summary>Il perché proposto: chi legge l'AIP espanso deve sapere che il tratto non viene da una tabella.</summary>
    public const string PercheProposto = "La carta è un'immagine: il tratto è ridisegnato sopra la carta posata.";

    /// <summary>La fonte proposta: la pagina della carta con la sua data, «carta AD 2 LIRN 5-3 (25/12/2025)».</summary>
    public static string Fonte(AerodromeChart carta)
    {
        ArgumentNullException.ThrowIfNull(carta);
        return carta.Effective is { } data
            ? $"carta {carta.Page} ({data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})"
            : $"carta {carta.Page}";
    }

    /// <summary>
    /// Una parte nuova della carta: un avvicinamento con il titolo della carta per designatore, le sue piste, il segmento che
    /// l'AOD scrive («MISSED APPROACH») e la pagina della carta, così la parte sta sotto di lei. Almeno due punti: è una linea.
    /// </summary>
    public static (Procedure? Parte, string? Perche) NuovaParte(string icao, CartaDiAvvicinamento carta, string? segmento, IReadOnlyList<PuntoDelTracciato> punti)
    {
        ArgumentNullException.ThrowIfNull(carta);
        ArgumentNullException.ThrowIfNull(punti);
        var nome = segmento?.Trim();
        if (string.IsNullOrEmpty(nome))
            return (null, "Scrivi il nome della parte com'è sulla carta: «MISSED APPROACH», «INITIAL APPROACH FROM BENTO»…");
        if (punti.Count < 2)
            return (null, "Servono almeno due punti: il primo e quello dove il tratto arriva.");
        if (Problema(punti) is { } problema)
            return (null, problema);
        var parte = new Procedure
        {
            Aerodrome = icao.ToUpperInvariant(),
            Kind = ProcedureKind.Approach,
            Designator = carta.Titolo,
            Runways = carta.Piste,
            // Il titolo senza il trattino morbido che AD 2.24 stampa a volte («ILS­Z»).
            Section = string.Join(' ', carta.Carta.Title.Replace('­', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            Segment = nome,
            Legs = [.. punti.Select(Tratto)],
            Page = new AipPage(carta.Carta.Page, carta.Carta.Effective),
        };
        return (parte, null);
    }

    /// <summary>I punti in coda ai tratti di una parte che c'è: il resto della parte è com'era.</summary>
    public static (Procedure? Parte, string? Perche) InCoda(Procedure parte, IReadOnlyList<PuntoDelTracciato> punti)
    {
        ArgumentNullException.ThrowIfNull(parte);
        ArgumentNullException.ThrowIfNull(punti);
        if (punti.Count == 0)
            return (null, "Nessun punto da mettere in coda.");
        if (Problema(punti) is { } problema)
            return (null, problema);
        return (parte with { Legs = [.. parte.Legs, .. punti.Select(Tratto)] }, null);
    }

    /// <summary>Il tratto di un punto: col fix se è un fix, altrimenti con la posizione presa sulla carta.</summary>
    public static ProcedureLeg Tratto(PuntoDelTracciato punto)
    {
        ArgumentNullException.ThrowIfNull(punto);
        var fix = Pulito(punto.Fix)?.ToUpperInvariant();
        var arco = punto.Tipo == "RF";
        return new ProcedureLeg
        {
            PathTerminator = punto.Tipo,
            Fix = fix,
            Computed = fix is null ? DallaCarta : null,
            Position = fix is null ? new Position(Math.Round(punto.Lat, 6), Math.Round(punto.Lon, 6)) : null,
            Altitude = Pulito(punto.Quota),
            Speed = Pulito(punto.Velocita),
            ArcCenter = arco ? Pulito(punto.Centro)?.ToUpperInvariant() : null,
            Turn = arco ? Pulito(punto.Verso) : null,
        };
    }

    private static string? Problema(IReadOnlyList<PuntoDelTracciato> punti)
    {
        for (var i = 0; i < punti.Count; i++)
        {
            var p = punti[i];
            if (!Tipi.Contains(p.Tipo))
                return $"Il punto {i + 1} ha un tratto che non conosco: «{p.Tipo}».";
            if (p.Tipo == "RF" && (Pulito(p.Centro) is null || Pulito(p.Verso) is not ("L" or "R")))
                return $"Il punto {i + 1} chiude un arco (RF): scrivi il centro (un fix) e il verso della virata, L o R.";
        }
        return null;
    }

    private static string? Pulito(string? testo) => string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
}

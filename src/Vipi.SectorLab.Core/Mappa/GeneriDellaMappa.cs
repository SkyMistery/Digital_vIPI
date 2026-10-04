using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>Un genere di uno strato, come lo mostra l'elenco degli strati: il nome dell'AOD e quante forme ha.</summary>
/// <param name="Id">Come viaggia con le forme (<c>TAXI_CENTER</c>, <c>POL</c>): maiuscolo, senza spazi.</param>
public sealed record GenereDelloStrato(string Id, string Nome, int Forme);

/// <summary>
/// I generi dentro uno strato della mappa (lotto «Subito» slice 12c, «file per file» H1): i disegni di terra per tipo —
/// assi e bordi delle taxiway, piazzali, edifici, moli, stop, marcature delle piste — e, nello strato della terra,
/// riempimenti, etichette e stand. Ognuno si accende e si spegne da sé, come i tasti di Aurora (TAXI_CENTER e STOPLINE
/// non sono in <c>colors.def</c>, ma in Aurora si vedono e si spengono).
/// <para>Il genere di una linea <c>.geo</c> è il suo 5° campo; quello di un file di <c>RW_MARKINGS</c> è «marcature»,
/// qualunque sia il campo (lì è sempre <c>RUNWAY</c>, e spegnere le piste non deve spegnere i numeri).</para>
/// </summary>
public static class GeneriDellaMappa
{
    public const string Marcature = "MARKINGS";
    public const string SenzaTipo = "VUOTO";
    public const string Riempimenti = "POL";
    public const string Etichette = "TXI";
    public const string Stand = "GTS";

    // Nell'ordine in cui si leggono nell'elenco: prima le superfici, poi gli edifici, poi i segni.
    private static readonly (string Id, string Nome)[] Noti =
    [
        ("RUNWAY", "Piste"), ("TAXIWAY", "Bordi delle taxiway"), ("TAXI_CENTER", "Assi delle taxiway"), ("APRON", "Piazzali"),
        ("BUILDING", "Edifici"), ("PIER", "Moli"), ("STOPBAR", "Stop bar"), ("STOPLINE", "Linee d'arresto"),
        (Marcature, "Marcature delle piste"), (SenzaTipo, "Senza tipo"),
        (Riempimenti, "Riempimenti"), (Etichette, "Etichette delle taxiway"), (Stand, "Stand"),
    ];

    /// <summary>Il genere di una linea di un <c>.geo</c>; null per lo sfondo e per le aree P/R/D, che non ne hanno.</summary>
    public static string? DelGeo(string relativo, string? colore)
    {
        ArgumentNullException.ThrowIfNull(relativo);
        string dritto = relativo.Replace('\\', '/');
        if (!dritto.EndsWith(".geo", StringComparison.OrdinalIgnoreCase)
            || dritto.EndsWith(StratiDellaMappa.SfondoGeo, StringComparison.OrdinalIgnoreCase))
            return null;
        if (dritto.Contains("/RW_MARKINGS/", StringComparison.OrdinalIgnoreCase))
            return Marcature;
        string tipo = (colore ?? string.Empty).Trim().ToUpperInvariant();
        return tipo.Length == 0 ? SenzaTipo : tipo;
    }

    /// <summary>Il genere di un record di terra che non è una linea: poligono, etichetta, stand.</summary>
    public static string? DelRecord(object record) => record switch
    {
        Polygon => Riempimenti,
        TaxiwayLabel => Etichette,
        Vipi.Sectorfile.Models.Stand => Stand,
        _ => null,
    };

    /// <summary>
    /// I generi di uno strato con le loro forme, nell'ordine dell'elenco (i noti prima, gli altri per nome). Vuoto se lo
    /// strato ne ha meno di due: una casella sola non serve.
    /// </summary>
    public static IReadOnlyList<GenereDelloStrato> Di(StratoDellaMappa strato)
    {
        ArgumentNullException.ThrowIfNull(strato);
        var quante = strato.Forme.Where(f => f.Genere is not null).GroupBy(f => f.Genere!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        if (quante.Count < 2)
            return [];

        var noti = Noti.Where(n => quante.ContainsKey(n.Id)).Select(n => new GenereDelloStrato(n.Id, n.Nome, quante[n.Id]));
        var altri = quante.Keys.Where(k => !Noti.Any(n => n.Id == k)).Order(StringComparer.Ordinal)
            .Select(k => new GenereDelloStrato(k, k, quante[k]));
        return [.. noti, .. altri];
    }

    /// <summary>La chiave di un genere spento, come la conosce la mappa: <c>strato:genere</c>.</summary>
    public static string Chiave(string strato, string genere) => strato + ":" + genere;
}

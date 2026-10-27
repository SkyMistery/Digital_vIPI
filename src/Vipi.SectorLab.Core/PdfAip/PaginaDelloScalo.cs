using System.Globalization;
using System.Text.RegularExpressions;
using Aip;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Un lato di una pista (AD 2.12): la soglia, le sue distanze dichiarate (AD 2.13) e il suo ILS (AD 2.19).</summary>
public sealed record LatoDellaPista(Runway Pista, IReadOnlyList<DeclaredDistance> Distanze, IReadOnlyList<Ils> Ils);

/// <summary>Una pista con i suoi due lati: «16L/34R». Un lato che non trova l'altro sta da solo.</summary>
public sealed record PistaDelloScalo(string Nome, IReadOnlyList<LatoDellaPista> Lati);

/// <summary>
/// Una procedura nella pagina dello scalo: la sua voce nello strato (per sceglierla e accenderla), quanti tratti ha, i punti
/// che non si trovano, e se la lettura ha una segnalazione su di lei.
/// </summary>
public sealed record ProceduraDelloScalo(int Indice, string Titolo, int Tratti, int NonTrovati, bool ConSegnalazione, string? Pagina)
{
    /// <summary>Lo stato in parole, corto: «14 tratti», «solo un punto», «senza tratti»; e i punti che non si trovano.</summary>
    public string Stato => (Tratti switch
    {
        0 => "senza tratti",
        1 => "solo un punto",
        _ => $"{Tratti} tratti",
    }) + (NonTrovati switch
    {
        0 => "",
        1 => " · 1 punto non trovato",
        _ => $" · {NonTrovati} punti non trovati",
    });
}

/// <summary>Le procedure di un genere per una pista (<see cref="Pista"/> vuota: quelle che l'AIP non lega a una pista).</summary>
public sealed record GruppoDiProcedure(string Pista, string Genere, IReadOnlyList<ProceduraDelloScalo> Procedure);

/// <summary>Una carta di AD 2.24, se è di avvicinamento, e quante procedure il Lab ha letto dalla sua pagina.</summary>
public sealed record CartaDelloScalo(AerodromeChart Carta, bool Avvicinamento, int Procedure);

/// <summary>
/// La pagina di uno scalo (carta «Gli aeroporti nel Lab», S1): lo scalo come lo legge il Lab, le piste coi due lati, le
/// procedure per pista e per genere, le carte di AD 2.24 con quel che il Lab ne legge, e tutte le sue voci nello strato.
/// </summary>
public sealed record PaginaDelloScalo(
    Aerodrome Scalo,
    int Indice,
    IReadOnlyList<PistaDelloScalo> Piste,
    IReadOnlyList<GruppoDiProcedure> Procedure,
    IReadOnlyList<CartaDelloScalo> Carte,
    IReadOnlyList<int> Voci)
{
    /// <summary>Le carte di avvicinamento che danno almeno una procedura, su tutte quelle di AD 2.24.</summary>
    public (int Lette, int Tutte) CarteIap =>
        (Carte.Count(c => c.Avvicinamento && c.Procedure > 0), Carte.Count(c => c.Avvicinamento));

    /// <summary>I lati delle piste in ordine, e in fondo «» per le procedure senza pista.</summary>
    public IEnumerable<string> PisteDelleProcedure =>
        Piste.SelectMany(p => p.Lati.Select(l => l.Pista.Designator)).Append("")
            .Where(p => Procedure.Any(g => g.Pista == p));
}

/// <summary>Le pagine degli scali, dallo strato dell'AIP espanso e dal modello da cui è fatto.</summary>
public static partial class PagineDegliScali
{
    /// <summary>L'ordine dei generi nella pagina: prima chi parte, poi chi arriva.</summary>
    private static readonly string[] OrdineDeiGeneri =
        [AipSullaMappa.Sid, AipSullaMappa.SaliteIniziali, AipSullaMappa.Star, AipSullaMappa.Transizioni, AipSullaMappa.Avvicinamenti];

    /// <summary>La «pista» delle procedure che l'AIP non lega a una pista (le STAR di LIRF): un gruppo a parte, in fondo.</summary>
    private static readonly IReadOnlyList<string> SenzaPista = [""];

    /// <summary>
    /// La pagina dello scalo <paramref name="icao"/>, o null se lo strato non lo ha. <paramref name="conSegnalazione"/>: le
    /// chiavi delle procedure su cui l'ultima lettura ha una segnalazione.
    /// </summary>
    public static PaginaDelloScalo? Di(StratoDellAip strato, AipDocument modello, string icao, IReadOnlySet<string>? conSegnalazione = null)
    {
        ArgumentNullException.ThrowIfNull(strato);
        ArgumentNullException.ThrowIfNull(modello);
        if ((modello.Aerodromes ?? []).FirstOrDefault(s => string.Equals(s.Icao, icao, StringComparison.OrdinalIgnoreCase)) is not { } scalo)
            return null;
        var indice = Enumerable.Range(0, strato.Voci.Count)
            .FirstOrDefault(i => strato.Voci[i].Genere == AipSullaMappa.Scali && string.Equals(strato.Voci[i].Titolo, scalo.Icao, StringComparison.OrdinalIgnoreCase), -1);
        if (indice < 0)
            return null;

        // Le procedure sono le ultime voci dello strato, nell'ordine del modello (AipSullaMappa.Di).
        var procedure = modello.Procedures ?? [];
        var prima = strato.Voci.Count - procedure.Count;
        var delloScalo = new List<(Procedure Procedura, ProceduraDelloScalo Voce, string Genere)>();
        for (var j = 0; j < procedure.Count && prima >= 0; j++)
        {
            var p = procedure[j];
            if (!string.Equals(p.Aerodrome, scalo.Icao, StringComparison.OrdinalIgnoreCase))
                continue;
            var i = prima + j;
            var voce = strato.Voci[i];
            var forma = strato.Strato.Forme[i];
            delloScalo.Add((p, new ProceduraDelloScalo(i, voce.Titolo, p.Legs.Count, forma.NomiNonRisolti.Count,
                conSegnalazione?.Contains(p.Key) == true, p.Page?.Reference), voce.Genere));
        }

        var gruppi = delloScalo
            .SelectMany(x => (x.Procedura.Runways.Count == 0 ? SenzaPista : x.Procedura.Runways).Select(pista => (Pista: pista, x.Genere, x.Voce)))
            .GroupBy(x => (x.Pista, x.Genere))
            .OrderBy(g => g.Key.Pista.Length == 0 ? 1 : 0)
            .ThenBy(g => g.Key.Pista, StringComparer.Ordinal)
            .ThenBy(g => Array.IndexOf(OrdineDeiGeneri, g.Key.Genere) is var o and >= 0 ? o : OrdineDeiGeneri.Length)
            .Select(g => new GruppoDiProcedure(g.Key.Pista, g.Key.Genere, [.. g.Select(x => x.Voce).OrderBy(v => v.Titolo, StringComparer.Ordinal)]))
            .ToList();

        var dallaPagina = delloScalo.Where(x => x.Voce.Pagina is not null).GroupBy(x => x.Voce.Pagina!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var carte = (scalo.Charts ?? []).Select(c => new CartaDelloScalo(c, DiAvvicinamento(c.Title), dallaPagina.GetValueOrDefault(Spazi(c.Page)))).ToList();

        var voci = IndiceDellAip.Di(strato.Voci)
            .Where(p => p.Codice is "AD 2" or "AD 3")
            .SelectMany(p => p.Gruppi)
            .FirstOrDefault(g => string.Equals(g.Codice, scalo.Icao, StringComparison.OrdinalIgnoreCase))?.Indici.ToList() ?? [indice];

        return new PaginaDelloScalo(scalo, indice, Piste(scalo), gruppi, carte, voci);
    }

    /// <summary>Per ogni scalo, le carte di avvicinamento che danno almeno una procedura su tutte quelle di AD 2.24.</summary>
    public static IReadOnlyDictionary<string, (int Lette, int Tutte)> CopertureIap(AipDocument modello)
    {
        ArgumentNullException.ThrowIfNull(modello);
        var pagine = (modello.Procedures ?? []).Where(p => p.Page is not null)
            .Select(p => (p.Aerodrome.ToUpperInvariant(), p.Page!.Reference)).ToHashSet();
        return (modello.Aerodromes ?? []).ToDictionary(s => s.Icao.ToUpperInvariant(), s =>
        {
            var iap = (s.Charts ?? []).Where(c => DiAvvicinamento(c.Title)).ToList();
            return (iap.Count(c => pagine.Contains((s.Icao.ToUpperInvariant(), Spazi(c.Page)))), iap.Count);
        }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vero per una carta di avvicinamento strumentale (ICAO INSTRUMENT APPROACH CHART, anche le sue «DESCRIPTION TABLES»),
    /// non per una VAC, una carta radar o delle minime.
    /// </summary>
    public static bool DiAvvicinamento(string titolo) =>
        RxAvvicinamento().IsMatch(titolo) && !RxNonAvvicinamento().IsMatch(titolo);

    /// <summary>
    /// Le piste coi due lati: il lato opposto è quello la cui soglia è la fine di questo (AD 2.12 lo stampa così), o, senza
    /// coordinate, quello col numero a 18 di distanza e la lettera scambiata.
    /// </summary>
    internal static IReadOnlyList<PistaDelloScalo> Piste(Aerodrome scalo)
    {
        var lati = scalo.Runways.ToList();
        var usate = new HashSet<int>();
        var piste = new List<PistaDelloScalo>();
        for (var i = 0; i < lati.Count; i++)
        {
            if (!usate.Add(i))
                continue;
            var opposto = Enumerable.Range(0, lati.Count).FirstOrDefault(j => j != i && !usate.Contains(j) && Opposti(lati[i], lati[j]), -1);
            var questi = opposto < 0 ? new[] { lati[i] } : new[] { lati[i], lati[opposto] };
            if (opposto >= 0)
                usate.Add(opposto);
            questi = [.. questi.OrderBy(l => l.Designator, StringComparer.Ordinal)];
            piste.Add(new PistaDelloScalo(string.Join("/", questi.Select(l => l.Designator)), [.. questi.Select(l => new LatoDellaPista(l,
                [.. scalo.DeclaredDistances.Where(d => d.Runway == l.Designator)],
                [.. scalo.Ils.Where(x => x.Runway == l.Designator)]))]));
        }
        return piste;
    }

    private static bool Opposti(Runway a, Runway b)
    {
        if (a.End is { } fine && b.Threshold is { } soglia)
            return Math.Abs(fine.Lat - soglia.Lat) < 1e-5 && Math.Abs(fine.Lon - soglia.Lon) < 1e-5;
        return Opposto(a.Designator) == b.Designator;
    }

    /// <summary>«16L» → «34R», «07» → «25»; null se il designatore non è un numero di pista.</summary>
    internal static string? Opposto(string designatore)
    {
        if (RxPista().Match(designatore) is not { Success: true } m)
            return null;
        var numero = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
        var altro = (numero + 17) % 36 + 1;
        var lettera = m.Groups["l"].Value switch { "L" => "R", "R" => "L", var x => x };
        return altro.ToString("00", CultureInfo.InvariantCulture) + lettera;
    }

    // AD 2.24 stampa la pagina come «AD 2 LIRF 5 - 3», l'indice delle pagine e le procedure «AD 2 LIRF 5-3».
    private static string Spazi(string pagina) => RxTrattino().Replace(pagina, "-");

    [GeneratedRegex(@"INSTRUMENT APPROACH|APPROACH CHART", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxAvvicinamento();

    [GeneratedRegex(@"VISUAL|\bVAC\b|AREA CHART|RADAR|SURVEILLANCE|MINIMUM", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxNonAvvicinamento();

    [GeneratedRegex(@"^(?<n>\d{2})(?<l>[LRC]?)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPista();

    [GeneratedRegex(@"\s*-\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RxTrattino();
}

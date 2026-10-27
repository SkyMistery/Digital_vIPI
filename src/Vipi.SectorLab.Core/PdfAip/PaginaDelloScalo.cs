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

/// <summary>
/// Una carta di AD 2.24, se è di avvicinamento, e quante procedure il Lab ha letto dalla sua pagina. <see cref="Iap"/>: la
/// carta di avvicinamento a cui appartiene (sé stessa, o quella di cui è le «DESCRIPTION TABLES»).
/// </summary>
public sealed record CartaDelloScalo(AerodromeChart Carta, bool Avvicinamento, int Procedure, CartaDiAvvicinamento? Iap = null);

/// <summary>Quel che il Lab ha di una carta di avvicinamento: niente (è un disegno), una parte, o una lettura con tratti.</summary>
public enum StatoDellaCarta
{
    SoloImmagine,
    InParte,
    Letta,
}

/// <summary>
/// Una parte di una carta di avvicinamento: una procedura letta dalle sue pagine (una transizione, il finale e il mancato, la
/// tabella FIX/POINT, il blocco SBAS). <see cref="Tabella"/>: una tabella senza tratti, che non si disegna come percorso.
/// </summary>
public sealed record ParteDellaCarta(string Nome, ProceduraDelloScalo Procedura, bool Tabella);

/// <summary>
/// Una carta di avvicinamento (carta «Gli aeroporti nel Lab», S2): l'avvicinamento come lo pensa chi lo vola, «ILS (Cat.II)
/// or LOC X RWY 16L», con le sue «DESCRIPTION TABLES» e le parti che il Lab ne legge.
/// </summary>
public sealed record CartaDiAvvicinamento(
    string Titolo,
    AerodromeChart Carta,
    IReadOnlyList<AerodromeChart> Tabelle,
    IReadOnlyList<string> Piste,
    IReadOnlyList<ParteDellaCarta> Parti)
{
    /// <summary>
    /// Letta: almeno una parte con i tratti, ognuna coi tratti o una tabella, tutti i punti trovati. In parte: delle parti, ma
    /// solo tabelle, o una senza tratti, o un punto che non si trova. Solo immagine: nessuna parte.
    /// </summary>
    public StatoDellaCarta Stato =>
        Parti.Count == 0 ? StatoDellaCarta.SoloImmagine
        : Parti.Any(p => p.Procedura.Tratti > 0) && Parti.All(p => p.Tabella || p.Procedura.Tratti > 1) && Parti.All(p => p.Procedura.NonTrovati == 0)
            ? StatoDellaCarta.Letta
            : StatoDellaCarta.InParte;

    /// <summary>I tratti di tutte le parti.</summary>
    public int Tratti => Parti.Sum(p => p.Procedura.Tratti);

    /// <summary>Lo stato in parole, corto: «letta · 3 parti, 14 tratti», «in parte · solo FIX/POINT», «solo immagine».</summary>
    public string StatoInParole
    {
        get
        {
            if (Parti.Count == 0)
                return "solo immagine";
            var quante = $"{Quante(Parti.Count, "parte", "parti")}, {Quante(Tratti, "tratto", "tratti")}";
            var nonTrovati = Parti.Sum(p => p.Procedura.NonTrovati);
            var senzaTratti = Parti.Count(p => !p.Tabella && p.Procedura.Tratti == 0);
            var unPunto = Parti.Count(p => !p.Tabella && p.Procedura.Tratti == 1);
            return Stato switch
            {
                StatoDellaCarta.Letta => $"letta · {quante}",
                _ when Tratti == 0 && senzaTratti == 0 => "in parte · solo " + string.Join(", ", Parti.Select(p => p.Nome).Distinct()),
                _ => $"in parte · {quante}"
                     + (senzaTratti > 0 ? $" · {Quante(senzaTratti, "parte", "parti")} senza tratti" : "")
                     + (unPunto > 0 ? $" · {Quante(unPunto, "parte", "parti")} con un punto solo" : "")
                     + (nonTrovati > 0 ? $" · {Quante(nonTrovati, "punto non trovato", "punti non trovati")}" : ""),
            };

            static string Quante(int n, string uno, string tanti) => $"{n} {(n == 1 ? uno : tanti)}";
        }
    }
}

/// <summary>
/// La pagina di uno scalo (carta «Gli aeroporti nel Lab», S1): lo scalo come lo legge il Lab, le piste coi due lati, le
/// procedure per pista e per genere, le carte di AD 2.24 con quel che il Lab ne legge, e tutte le sue voci nello strato.
/// S2: gli avvicinamenti per carta (<see cref="Avvicinamenti"/>); in <see cref="Procedure"/> restano solo quelli che non
/// stanno sulle pagine di una carta di avvicinamento.
/// </summary>
public sealed record PaginaDelloScalo(
    Aerodrome Scalo,
    int Indice,
    IReadOnlyList<PistaDelloScalo> Piste,
    IReadOnlyList<GruppoDiProcedure> Procedure,
    IReadOnlyList<CartaDelloScalo> Carte,
    IReadOnlyList<int> Voci,
    IReadOnlyList<CartaDiAvvicinamento> Avvicinamenti)
{
    /// <summary>Le carte di avvicinamento che danno almeno una parte, su tutte (le «DESCRIPTION TABLES» sono della loro carta).</summary>
    public (int Lette, int Tutte) CarteIap =>
        (Avvicinamenti.Count(c => c.Parti.Count > 0), Avvicinamenti.Count);

    /// <summary>Le carte di avvicinamento di un lato (<paramref name="pista"/> vuota: quelle che non dicono la pista).</summary>
    public IEnumerable<CartaDiAvvicinamento> AvvicinamentiDi(string pista) =>
        Avvicinamenti.Where(c => pista.Length == 0 ? c.Piste.Count == 0 : c.Piste.Contains(pista));

    /// <summary>
    /// I lati delle piste in ordine, poi quelli che solo le procedure o le carte nominano, e in fondo «» per le procedure senza
    /// pista.
    /// </summary>
    public IEnumerable<string> PisteDelleProcedure =>
        Piste.SelectMany(p => p.Lati.Select(l => l.Pista.Designator))
            .Concat(Procedure.Select(g => g.Pista).Concat(Avvicinamenti.SelectMany(c => c.Piste)).Where(p => p.Length > 0).Order(StringComparer.Ordinal))
            .Append("").Distinct(StringComparer.Ordinal)
            .Where(p => Procedure.Any(g => g.Pista == p) || AvvicinamentiDi(p).Any());
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

        // S2: un avvicinamento che sta sulle pagine di una carta di avvicinamento è una sua parte, non una voce del gruppo.
        var avvicinamenti = delloScalo.Where(x => x.Procedura.Kind == ProcedureKind.Approach).ToList();
        var lette = CarteDi(scalo, [.. avvicinamenti.Select(x => x.Procedura)]);
        var nellaCarta = lette.SelectMany(c => c.Parti).Select(j => avvicinamenti[j].Voce.Indice).ToHashSet();
        var iap = lette.Select(c => new CartaDiAvvicinamento(c.Titolo, c.Carta, c.Tabelle, c.Piste,
            [.. c.Parti.Select(j => new ParteDellaCarta(NomeDellaParte(avvicinamenti[j].Procedura), avvicinamenti[j].Voce, Tabella(avvicinamenti[j].Procedura)))])).ToList();

        var gruppi = delloScalo
            .Where(x => !nellaCarta.Contains(x.Voce.Indice))
            .SelectMany(x => (x.Procedura.Runways.Count == 0 ? SenzaPista : x.Procedura.Runways).Select(pista => (Pista: pista, x.Genere, x.Voce)))
            .GroupBy(x => (x.Pista, x.Genere))
            .OrderBy(g => g.Key.Pista.Length == 0 ? 1 : 0)
            .ThenBy(g => g.Key.Pista, StringComparer.Ordinal)
            .ThenBy(g => Array.IndexOf(OrdineDeiGeneri, g.Key.Genere) is var o and >= 0 ? o : OrdineDeiGeneri.Length)
            .Select(g => new GruppoDiProcedure(g.Key.Pista, g.Key.Genere, [.. g.Select(x => x.Voce).OrderBy(v => v.Titolo, StringComparer.Ordinal)]))
            .ToList();

        var dallaPagina = delloScalo.Where(x => x.Voce.Pagina is not null).GroupBy(x => Spazi(x.Voce.Pagina!), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var carte = (scalo.Charts ?? []).Select(c => new CartaDelloScalo(c, DiAvvicinamento(c.Title), dallaPagina.GetValueOrDefault(Spazi(c.Page)),
            iap.FirstOrDefault(a => ReferenceEquals(a.Carta, c) || a.Tabelle.Any(t => ReferenceEquals(t, c))))).ToList();

        var voci = IndiceDellAip.Di(strato.Voci)
            .Where(p => p.Codice is "AD 2" or "AD 3")
            .SelectMany(p => p.Gruppi)
            .FirstOrDefault(g => string.Equals(g.Codice, scalo.Icao, StringComparison.OrdinalIgnoreCase))?.Indici.ToList() ?? [indice];

        return new PaginaDelloScalo(scalo, indice, Piste(scalo), gruppi, carte, voci, iap);
    }

    /// <summary>
    /// Per ogni scalo, le carte di avvicinamento che danno almeno una parte su tutte quelle di AD 2.24 (le «DESCRIPTION
    /// TABLES» contano con la loro carta).
    /// </summary>
    public static IReadOnlyDictionary<string, (int Lette, int Tutte)> CopertureIap(AipDocument modello)
    {
        ArgumentNullException.ThrowIfNull(modello);
        var perScalo = (modello.Procedures ?? []).Where(p => p.Kind == ProcedureKind.Approach)
            .ToLookup(p => p.Aerodrome, StringComparer.OrdinalIgnoreCase);
        return (modello.Aerodromes ?? []).ToDictionary(s => s.Icao.ToUpperInvariant(), s =>
        {
            var carte = CarteDi(s, [.. perScalo[s.Icao]]);
            return (carte.Count(c => c.Parti.Count > 0), carte.Count);
        }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Le carte di avvicinamento di uno scalo con le loro parti, come indici in <paramref name="avvicinamenti"/>. Una carta
    /// occupa le pagine dalla sua fino a quella prima della carta seguente dello stesso file (AD 2.24 dà solo la prima: la
    /// procedura di «ILS Z RWY 24» sta a 5-4, la carta a 5-3; il blocco SBAS di 5-9 a 5-10). Le «DESCRIPTION TABLES» si legano
    /// alla carta prima col titolo uguale senza il suffisso (LIRF 5-9 → 5-7 «RNP RWY 16L»); se non la trovano, sono una carta.
    /// </summary>
    internal static IReadOnlyList<(string Titolo, AerodromeChart Carta, IReadOnlyList<AerodromeChart> Tabelle, IReadOnlyList<string> Piste, IReadOnlyList<int> Parti)>
        CarteDi(Aerodrome scalo, IReadOnlyList<Procedure> avvicinamenti)
    {
        var tutte = (scalo.Charts ?? []).Select(c => (Carta: c, Posto: Posto(c.Page))).ToList();
        var radici = new List<(AerodromeChart Carta, List<AerodromeChart> Tabelle, List<int> Parti)>();
        var diChi = new Dictionary<AerodromeChart, int>(ReferenceEqualityComparer.Instance);
        foreach (var (carta, _) in tutte.Where(c => DiAvvicinamento(c.Carta.Title)))
        {
            if (RxTabelle().IsMatch(carta.Title)
                && radici.FindLastIndex(r => !RxTabelle().IsMatch(r.Carta.Title) && Uguali(r.Carta.Title, RxTabelle().Replace(carta.Title, ""))) is var radice and >= 0)
            {
                radici[radice].Tabelle.Add(carta);
                diChi[carta] = radice;
                continue;
            }
            diChi[carta] = radici.Count;
            radici.Add((carta, [], []));
        }

        for (var j = 0; j < avvicinamenti.Count; j++)
        {
            if (avvicinamenti[j].Page is not { } pagina || Posto(pagina.Reference) is not { } posto)
                continue;
            var carta = tutte.Where(c => c.Posto is { } p && string.Equals(p.File, posto.File, StringComparison.OrdinalIgnoreCase) && p.Numero <= posto.Numero)
                .MaxBy(c => c.Posto!.Value.Numero).Carta;
            if (carta is not null && diChi.TryGetValue(carta, out var r))
                radici[r].Parti.Add(j);
        }

        return [.. radici.Select(r =>
        {
            IReadOnlyList<string> piste = [.. RxPisteDelTitolo().Matches(r.Carta.Title).SelectMany(m => m.Groups["p"].Captures.Select(c => c.Value.ToUpperInvariant()))
                .Concat(r.Parti.SelectMany(j => avvicinamenti[j].Runways)).Distinct(StringComparer.Ordinal)];
            return (TitoloDellaCarta(r.Carta.Title), r.Carta, (IReadOnlyList<AerodromeChart>)r.Tabelle, piste, (IReadOnlyList<int>)r.Parti);
        })];
    }

    /// <summary>«ICAO INSTRUMENT APPROACH CHART ILS­Z RWY 24» → «ILS Z RWY 24»: il nome che usa chi la vola.</summary>
    internal static string TitoloDellaCarta(string titolo)
    {
        var corto = RxTestataDellaCarta().Replace(Pulito(titolo), "");
        return corto.Length == 0 ? titolo : corto;
    }

    /// <summary>
    /// Il titolo senza i difetti di stampa di AD 2.24 sul 2611: il trattino morbido («ILS­Z», U+00AD) e il «.pdf» in coda
    /// (LIBC, LICA); spazi uno solo.
    /// </summary>
    private static string Pulito(string titolo) =>
        RxSpazi().Replace(RxPdf().Replace(titolo.Replace('­', ' '), ""), " ").Trim();

    /// <summary>
    /// Il nome di una parte: il segmento stampato sopra la sua tabella («RNAV to ILS TRANSITION - APPROACH FROM BENTO»), le
    /// tabelle senza tratti per quel che sono, altrimenti il designatore (col «(2)» delle tabelle seguenti).
    /// </summary>
    private static string NomeDellaParte(Procedure p) => p.Segment switch
    {
        { } s when !RxSoloRimando().IsMatch(s) => s,
        _ when p.Legs.Count == 0 && p.Fixes is { Count: > 0 } => "FIX/POINT",
        _ when p.Legs.Count == 0 && p.DataBlock is { Count: > 0 } => "SBAS FAS DATA BLOCK",
        { } rimando => $"{p.Designator} {rimando}",
        _ => p.Designator,
    };

    private static bool Tabella(Procedure p) => p.Legs.Count == 0 && (p.Fixes is { Count: > 0 } || p.DataBlock is { Count: > 0 });

    // «ILS OR LOC­Z RWY 17L» e la sua «ILS OR LOC Z RWY 17L DESCRIPTION TABLES» (LIMC 5-27 e 5-29): uguali.
    private static bool Uguali(string a, string b) =>
        string.Equals(Pulito(a), Pulito(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>«AD 2 LIRF 5 - 9» → («AD 2 LIRF 5», 9); null se la pagina non finisce col suo numero.</summary>
    private static (string File, int Numero)? Posto(string pagina) =>
        RxPagina().Match(Spazi(pagina).Trim()) is { Success: true } m
            ? (m.Groups["f"].Value, int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture))
            : null;

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

    [GeneratedRegex(@"^(?<f>.+?)-(?<n>\d+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPagina();

    [GeneratedRegex(@"\s*\bDESCRIPTION\s+TABLES?\b\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxTabelle();

    [GeneratedRegex(@"\bRWY\s*(?<p>\d{2}[LRC]?)(?:\s*/\s*(?<p>\d{2}[LRC]?))*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPisteDelTitolo();

    [GeneratedRegex(@"^\s*(?:ICAO\s+)?INSTRUMENT\s+APPROACH\s+CHART\s*[-–—:]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxTestataDellaCarta();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"\.pdf\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxPdf();

    [GeneratedRegex(@"^\(\d+\)$", RegexOptions.CultureInvariant)]
    private static partial Regex RxSoloRimando();
}

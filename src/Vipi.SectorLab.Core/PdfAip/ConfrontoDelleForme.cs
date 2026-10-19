using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Com'è uno spazio dell'AIP rispetto al sector (carta F6 §3, «I gesti nel Lab»).</summary>
public enum EsitoDelConfronto
{
    Uguale,
    FormaDiversa,
    MancaNelSector,
    SoloNelSector,
}

/// <summary>
/// Che cosa si confronta, e con quali voci del sector (carta F6 §6, slice 1b e 1c): i CTR coi <c>MAPS</c> degli
/// <c>.str</c>; le ATZ coi <c>MAPS</c> degli <c>.str</c> o coi settori delle torri di <c>DYNAMIC_SEC/twrs.tfl</c> (il
/// sector le tiene in tutti e due, committente 18 ottobre). TMA e CTA il sector non le tiene.
/// </summary>
public enum FamigliaDelConfronto
{
    Ctr,
    Atz,
    AtzDelleTorri,

    /// <summary>Le aree di ENR 5 (P, R, D, TSA, TRA e le militari) contro <c>italy.prohibit</c>, <c>.restrict</c>, <c>.danger</c> (slice 4b).</summary>
    AreeEnr5,
}

/// <summary>
/// Una riga della lista delle differenze. <paramref name="ScartoAip"/>: quanto il contorno dell'AIP si stacca dalla linea
/// del sector, al massimo, in NM; <paramref name="ScartoSector"/>: quanto la linea del sector si stacca dai contorni
/// dell'AIP. <paramref name="Dove"/>: il punto dello scarto più grande. <paramref name="SenzaTracciato"/>: i pezzi di
/// sector lungo un confine o una costa che l'AIP nomina senza darne la forma, e che quindi non si giudicano.
/// </summary>
public sealed record DifferenzaDiForma(
    EsitoDelConfronto Esito,
    string? Aip,
    int Zone,
    string? Sector,
    string? File,
    int? Record,
    double? ScartoAip,
    double? ScartoSector,
    (double Lat, double Lon)? Dove,
    bool SenzaTracciato);

/// <summary>
/// Il confronto fra le forme dell'AIP e quelle del sector, in sola lettura (slice 1b). Il sector tiene un CTR come
/// <b>una linea</b> che passa per tutte le sue zone (la voce <c>LIPE CTR</c> del <c>MAPS</c> di <c>lipe.str</c>, coi
/// commenti «//zona 1»), l'AIP come un anello per zona: non si confrontano i vertici ma le forme, a distanza.
/// <list type="bullet">
/// <item>Le zone di un CTR dell'AIP vanno insieme: «Bologna CTR Zona/Zone '1'» … «'9'» sono «Bologna CTR».</item>
/// <item>Ogni CTR dell'AIP si abbina alla voce CTR del sector che gli passa più vicino (in media); una voce già
/// abbinata non si riusa.</item>
/// <item><b>Uguale</b> se ogni punto dei contorni dell'AIP sta entro la soglia (D4: 0,1 NM) dalla linea del sector, e
/// ogni punto della linea del sector entro la soglia da un contorno dell'AIP; altrimenti <b>forma diversa</b>, con i due
/// scarti e il punto peggiore.</item>
/// <item>I tratti di confine o di costa (l'AIP li nomina, non li disegna) non si giudicano: né i loro punti, né i pezzi
/// di sector che stanno più vicini a loro che ad altro.</item>
/// </list>
/// Distanze sul piano locale (equirettangolare attorno al CTR): a 50 NM lo scarto dalla sfera è sotto il metro.
/// </summary>
public static partial class ConfrontoDelleForme
{
    /// <summary>La soglia di base (D4, committente: 0,1 NM).</summary>
    public const double SogliaDiBase = 0.1;

    /// <summary>
    /// Oltre questo scarto (NM) la differenza è quasi certo un errore di stampa, dell'AIP o del sector, e non si sostituisce
    /// senza aver guardato: l'ATZ di Viterbo nell'ENR 2.2 del 2610 ha il centro a «017°03'44"E» (è 012°), 221 NM in
    /// Adriatico. Il CTR più lontano del 18 ottobre, Cagliari, stava a 7 NM.
    /// </summary>
    public const double ScartoDaGuardare = 25;

    /// <summary>Vero se la riga ha uno scarto da errore di stampa (<see cref="ScartoDaGuardare"/>).</summary>
    public static bool DaGuardare(DifferenzaDiForma riga) =>
        Math.Max(riga?.ScartoAip ?? 0, riga?.ScartoSector ?? 0) > ScartoDaGuardare;

    /// <summary>Oltre questa distanza media un CTR non è «quello» del sector, anche se è il più vicino.</summary>
    private const double AbbinamentoMassimo = 3.0;

    /// <summary>Ogni quanto si campiona un lato, in NM.</summary>
    private const double Passo = 0.05;

    [GeneratedRegex(@"^(?<ctr>.*?\bCTR\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex RxNomeDelCtr();

    [GeneratedRegex(@"\bCTR\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxCtr();

    [GeneratedRegex(@"\bATZ\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxAtz();

    /// <summary>«ATZ Torino/Aeritalia LIMA 3 - RMZ» → «ATZ Torino/Aeritalia LIMA»: via la RMZ e il numero della zona.</summary>
    [GeneratedRegex(@"^(?<atz>ATZ\s.*?)(?:\s+\d+)?\s*-\s*RMZ\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex RxAtzRmz();

    /// <summary>Un indicativo di località ICAO, scritto in maiuscolo (<c>LILG</c>); le città dell'AIP non lo sono.</summary>
    [GeneratedRegex(@"\b[A-Z]{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxScalo();

    /// <summary>Il file dei settori delle torri.</summary>
    public const string Torri = "twrs.tfl";

    private sealed record Lato(double Ax, double Ay, double Bx, double By, bool Giudicabile);

    /// <summary>
    /// I CTR dell'AIP contro le voci CTR dei <c>MAPS</c> del sector. <paramref name="sector"/>: le forme della mappa del
    /// Lab (i punti per nome già risolti); si guardano quelle degli <c>.str</c> col nome che dice CTR.
    /// </summary>
    public static IReadOnlyList<DifferenzaDiForma> Ctr(IEnumerable<Airspace> aip, IEnumerable<FormaDellaMappa> sector, double soglia = SogliaDiBase)
        => Di(FamigliaDelConfronto.Ctr, aip, sector, soglia);

    /// <summary>
    /// Gli spazi dell'AIP di quella famiglia contro le voci del sector che la tengono. Le ATZ di ENR 2 sono le 16 militari
    /// (ENR 2.1.3) e le «ATZ … - RMZ» di ENR 2.2, le zone di uno scalo insieme; le civili sono in AD 2, non ancora lette.
    /// </summary>
    public static IReadOnlyList<DifferenzaDiForma> Di(FamigliaDelConfronto famiglia, IEnumerable<Airspace> aip, IEnumerable<FormaDellaMappa> sector, double soglia = SogliaDiBase)
    {
        if (famiglia == FamigliaDelConfronto.AreeEnr5)
            return AreeEnr5(aip, sector, soglia);
        var gruppi = Gruppi(famiglia, aip);
        var voci = sector.Where(f => f.Tratti.Any(t => t.Count > 1) && famiglia switch
        {
            FamigliaDelConfronto.Ctr => f.File.EndsWith(".str", StringComparison.OrdinalIgnoreCase) && RxCtr().IsMatch(f.Etichetta),
            FamigliaDelConfronto.Atz => f.File.EndsWith(".str", StringComparison.OrdinalIgnoreCase) && RxAtz().IsMatch(f.Etichetta),
            _ => f.File.EndsWith("/" + Torri, StringComparison.OrdinalIgnoreCase),
        }).ToList();

        // Lo scalo, dove l'AIP lo scrive («ATZ Vergiate LILG - RMZ»), comanda sulla vicinanza: quello spazio va solo con le
        // voci del suo scalo (LILG LILG ATZ, LILG_TWR), e le voci di uno scalo che l'AIP nomina non vanno con altri. Misura
        // del 18 ottobre: Vergiate, senza settore di torre, prendeva quello di Malpensa (LIMC_TWR), a 3 NM.
        var scaloDelGruppo = gruppi.Select(g => RxScalo().Match(g.Nome) is { Success: true } m ? m.Value : null).ToList();
        var nominati = scaloDelGruppo.OfType<string>().ToHashSet(StringComparer.Ordinal);
        var scaloDellaVoce = voci.Select(f => f.Etichetta.Length >= 4 && RxScalo().IsMatch(f.Etichetta[..4]) ? f.Etichetta[..4] : null).ToList();

        // Il centro di riferimento della proiezione: uno per spazio.
        var candidati = new List<(int Gruppo, int Voce, double Media)>();
        var contorni = gruppi.Select(g => Contorni(g.Zone)).ToList();
        for (var g = 0; g < gruppi.Count; g++)
        {
            var (lat0, lon0) = Centro(contorni[g]);
            var latiAip = Proietta(contorni[g], lat0, lon0);
            for (var v = 0; v < voci.Count; v++)
            {
                if (scaloDelGruppo[g] is { } scalo ? scaloDellaVoce[v] != scalo : scaloDellaVoce[v] is { } suo && nominati.Contains(suo))
                    continue;
                var latiSector = ProiettaLaVoce(voci[v], lat0, lon0);
                // Stesso scalo: è la sua voce anche se lontana (sarà una forma diversa, non un «manca» più un «solo nel sector»).
                var stessoScalo = scaloDelGruppo[g] is not null;
                if (!stessoScalo && !Vicini(latiAip, latiSector)) continue;
                var media = Campioni(latiAip.Where(l => l.Giudicabile)).Select(p => Distanza(p, latiSector).Distanza).DefaultIfEmpty(double.MaxValue).Average();
                if (stessoScalo || media <= AbbinamentoMassimo) candidati.Add((g, v, media));
            }
        }

        var esiti = new List<DifferenzaDiForma>();
        var gruppoPreso = new HashSet<int>();
        var vocePresa = new HashSet<int>();
        foreach (var (g, v, _) in candidati.OrderBy(c => c.Media))
        {
            if (gruppoPreso.Contains(g) || vocePresa.Contains(v)) continue;
            gruppoPreso.Add(g);
            vocePresa.Add(v);
            esiti.Add(Misura(gruppi[g].Nome, gruppi[g].Zone.Count, contorni[g], voci[v], soglia));
        }
        for (var g = 0; g < gruppi.Count; g++)
            if (!gruppoPreso.Contains(g))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.MancaNelSector, gruppi[g].Nome, gruppi[g].Zone.Count, null, null, null, null, null, null, false));
        for (var v = 0; v < voci.Count; v++)
            if (!vocePresa.Contains(v))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.SoloNelSector, null, 0, voci[v].Etichetta, voci[v].File, voci[v].Record, null, null, null, false));

        return esiti
            .OrderBy(e => e.Esito switch { EsitoDelConfronto.FormaDiversa => 0, EsitoDelConfronto.MancaNelSector => 1, EsitoDelConfronto.SoloNelSector => 2, _ => 3 })
            .ThenBy(e => e.Aip ?? e.Sector, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>I file del sector con le aree di ENR 5 (P, R e D; TSA e TRA stanno fra le R).</summary>
    public static readonly IReadOnlyList<string> FileDelleAreeEnr5 = ["italy.prohibit", "italy.restrict", "italy.danger"];

    /// <summary>
    /// Le aree di ENR 5 (5.1: P, R, D, TSA, TRA, CBA; 5.2: militari) contro le voci di <see cref="FileDelleAreeEnr5"/>
    /// (carta F6, slice 4b). Diversamente dai CTR, qui il <b>nome</b> comanda (<see cref="NomeDiEnr5"/>):
    /// <list type="number">
    /// <item>Le voci del sector con lo stesso nome nello stesso file, se vicine, sono una voce sola (R108AB è in 12 pezzi).</item>
    /// <item><c>LI R4</c> va con <c>R4</c>, <c>LI D122 A</c> con <c>D122A</c>; fra più voci con quel nome, la più vicina.</item>
    /// <item>Una voce del sector può valere più aree dell'AIP (carta F6 §4): <c>R300AB</c> è R300/A più R300/B,
    /// <c>D37A/B</c>, <c>R405AC</c>; e una voce col solo nome della base (<c>D75</c>) vale le aree con le lettere che
    /// nessun'altra voce ha preso (D75/A, /B, /C). Si confronta con le aree insieme, come le zone di un CTR.</item>
    /// <item>Le aree che il nome non abbina si abbinano per vicinanza fra loro (un nome scritto in un altro modo; la riga
    /// mostra i due nomi), ma solo se sono vicine <b>nei due versi</b>: un poligono di 1 NM dentro un'area di 100 non è
    /// lei.</item>
    /// </list>
    /// Poi la stessa misura dei CTR: uguale entro la soglia nei due versi.
    /// </summary>
    public static IReadOnlyList<DifferenzaDiForma> AreeEnr5(IEnumerable<Airspace> aip, IEnumerable<FormaDellaMappa> sector, double soglia = SogliaDiBase)
    {
        var aree = aip.Where(a => a.Section.StartsWith("ENR 5", StringComparison.Ordinal)).ToList();
        // 1. Una voce per nome e file, coi pezzi vicini insieme (un'altra area con la stessa etichetta resta a parte).
        var voci = sector.Where(f => f.Tratti.Any(t => t.Count > 1) && FileDelleAreeEnr5.Any(n => f.File.EndsWith("/" + n, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(f => (f.File, Nome: NomeDiEnr5(f.Etichetta)))
            .SelectMany(g => PezziVicini(g.ToList()))
            .ToList();
        var contorni = aree.Select(a => Contorni([a])).ToList();
        var nomi = aree.Select(NomeDellAreaEnr5).ToList();

        double Media(List<(Position A, Position B, bool Giudicabile)> contorno, int v, bool versoIlSector)
        {
            var (lat0, lon0) = Centro(contorno);
            var latiAip = Proietta(contorno, lat0, lon0);
            var latiSector = ProiettaLaVoce(voci[v], lat0, lon0);
            return versoIlSector
                ? Campioni(latiAip.Where(l => l.Giudicabile)).Select(p => Distanza(p, latiSector).Distanza).DefaultIfEmpty(double.MaxValue).Average()
                : Campioni(latiSector).Select(p => Distanza(p, latiAip).Distanza).DefaultIfEmpty(double.MaxValue).Average();
        }

        var coppie = new List<(List<int> Aree, int Voce)>();
        var presa = new HashSet<int>();
        var abbinata = new HashSet<int>();
        var perNome = voci.Select((v, i) => (Nome: NomeDiEnr5(v.Etichetta), i)).GroupBy(x => x.Nome, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.i).ToList(), StringComparer.Ordinal);

        // 2. Lo stesso nome.
        for (var a = 0; a < aree.Count; a++)
        {
            if (!perNome.TryGetValue(nomi[a], out var stesse)) continue;
            var libere = stesse.Where(v => !presa.Contains(v)).ToList();
            if (libere.Count == 0) continue;
            var v = libere.Count == 1 ? libere[0] : libere.MinBy(x => Media(contorni[a], x, versoIlSector: true));
            coppie.Add(([a], v));
            presa.Add(v);
            abbinata.Add(a);
        }

        // 3. Una voce per più aree: «R300AB» → R300A e R300B, fra quelle ancora libere.
        for (var v = 0; v < voci.Count; v++)
        {
            if (presa.Contains(v) || RxPiuAree().Match(NomeDiEnr5(voci[v].Etichetta)) is not { Success: true } m) continue;
            var sue = m.Groups["lettere"].Value
                .SelectMany(c => Enumerable.Range(0, aree.Count).Where(a => !abbinata.Contains(a) && nomi[a] == m.Groups["base"].Value + c))
                .ToList();
            if (sue.Count == 0) continue;
            coppie.Add((sue, v));
            presa.Add(v);
            abbinata.UnionWith(sue);
        }

        // 4. Una voce col nome della sola base («D75», «R315») per le aree con le lettere ancora libere (D75/A, /B, /C).
        for (var v = 0; v < voci.Count; v++)
        {
            if (presa.Contains(v) || RxSoloBase().Match(NomeDiEnr5(voci[v].Etichetta)) is not { Success: true } m) continue;
            var sue = Enumerable.Range(0, aree.Count)
                .Where(a => !abbinata.Contains(a) && nomi[a].Length == m.Value.Length + 1 && nomi[a].StartsWith(m.Value, StringComparison.Ordinal) && char.IsLetter(nomi[a][^1]))
                .ToList();
            if (sue.Count == 0) continue;
            coppie.Add((sue, v));
            presa.Add(v);
            abbinata.UnionWith(sue);
        }

        // 5. Per vicinanza, nei due versi, fra quelle che il nome non ha abbinato.
        var candidati = new List<(int Area, int Voce, double Media)>();
        for (var a = 0; a < aree.Count; a++)
        {
            if (abbinata.Contains(a)) continue;
            var (lat0, lon0) = Centro(contorni[a]);
            var latiAip = Proietta(contorni[a], lat0, lon0);
            for (var v = 0; v < voci.Count; v++)
            {
                if (presa.Contains(v) || !Vicini(latiAip, ProiettaLaVoce(voci[v], lat0, lon0))) continue;
                var andata = Media(contorni[a], v, versoIlSector: true);
                if (andata > AbbinamentoMassimo) continue;
                var ritorno = Media(contorni[a], v, versoIlSector: false);
                if (ritorno <= AbbinamentoMassimo) candidati.Add((a, v, Math.Max(andata, ritorno)));
            }
        }
        foreach (var (a, v, _) in candidati.OrderBy(c => c.Media))
        {
            if (abbinata.Contains(a) || presa.Contains(v)) continue;
            coppie.Add(([a], v));
            presa.Add(v);
            abbinata.Add(a);
        }

        var esiti = coppie.Select(c => Misura(string.Join(" + ", c.Aree.Select(a => aree[a].Key)), c.Aree.Count,
            [.. c.Aree.SelectMany(a => contorni[a])], voci[c.Voce], soglia)).ToList();
        for (var a = 0; a < aree.Count; a++)
            if (!abbinata.Contains(a))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.MancaNelSector, aree[a].Key, 1, null, null, null, null, null, null, false));
        for (var v = 0; v < voci.Count; v++)
            if (!presa.Contains(v))
                esiti.Add(new DifferenzaDiForma(EsitoDelConfronto.SoloNelSector, null, 0, voci[v].Etichetta, voci[v].File, voci[v].Record, null, null, null, false));

        return esiti
            .OrderBy(e => e.Esito switch { EsitoDelConfronto.FormaDiversa => 0, EsitoDelConfronto.MancaNelSector => 1, EsitoDelConfronto.SoloNelSector => 2, _ => 3 })
            .ThenBy(e => e.Aip ?? e.Sector, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Il nome di un'area dell'AIP per abbinarla: quello del designatore e, se il nome dice la zona fra virgolette
    /// (<c>LI TSA74</c> «Gran Sasso d'Italia Zona "A"»), la sua lettera: <c>TSA74A</c>.
    /// </summary>
    public static string NomeDellAreaEnr5(Airspace a) =>
        NomeDiEnr5(a.Designator) + (a.Name is { } n && RxZona().Match(n) is { Success: true } z ? z.Groups["z"].Value.ToUpperInvariant() : "");

    /// <summary>
    /// Il nome di un'area di ENR 5 per abbinarla: senza il prefisso dello Stato (<c>LI</c>, <c>LI/LD</c>), in maiuscolo,
    /// solo lettere e cifre — <c>LI D122 A</c>, <c>D122 A</c> e <c>D122A</c> sono lo stesso nome; la «e» fra due lettere
    /// sparisce (<c>D35 B e C</c> → <c>D35BC</c>).
    /// </summary>
    public static string NomeDiEnr5(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);
        var n = RxPrefissoDelloStato().Replace(nome.Trim().ToUpperInvariant(), "");
        n = RxCongiunzione().Replace(n, "$1$2");
        return new string([.. n.Where(char.IsLetterOrDigit)]);
    }

    /// <summary>
    /// I pezzi di una voce (stesso nome, stesso file) a gruppi di vicini: le scatole a meno di 0,05° (3 NM) sono la stessa
    /// area. Nella copia del 19 ottobre R108AB è in 12 pezzi; «TSA74B» ha anche un pezzo a Belluno, 280 NM più in là.
    /// </summary>
    private static IEnumerable<FormaDellaMappa> PezziVicini(List<FormaDellaMappa> pezzi)
    {
        if (pezzi.Count == 1) return pezzi;
        static (double La0, double Lo0, double La1, double Lo1) Scatola(FormaDellaMappa f) =>
            (f.Tratti.SelectMany(t => t).Min(c => c.LatitudeDeg), f.Tratti.SelectMany(t => t).Min(c => c.LongitudeDeg),
             f.Tratti.SelectMany(t => t).Max(c => c.LatitudeDeg), f.Tratti.SelectMany(t => t).Max(c => c.LongitudeDeg));
        var scatole = pezzi.Select(Scatola).ToList();
        var gruppo = Enumerable.Range(0, pezzi.Count).ToArray();
        int Radice(int i) => gruppo[i] == i ? i : gruppo[i] = Radice(gruppo[i]);
        for (var i = 0; i < pezzi.Count; i++)
            for (var j = i + 1; j < pezzi.Count; j++)
            {
                var (a, b) = (scatole[i], scatole[j]);
                if (a.La0 <= b.La1 + 0.05 && b.La0 <= a.La1 + 0.05 && a.Lo0 <= b.Lo1 + 0.05 && b.Lo0 <= a.Lo1 + 0.05)
                    gruppo[Radice(i)] = Radice(j);
            }
        return Enumerable.Range(0, pezzi.Count).GroupBy(Radice)
            .Select(g => g.Count() == 1 ? pezzi[g.First()] : pezzi[g.First()] with { Tratti = [.. g.SelectMany(i => pezzi[i].Tratti)] });
    }

    /// <summary>Una voce del sector col solo nome della base, senza lettere: <c>D75</c>, <c>TRA504</c>.</summary>
    [GeneratedRegex(@"^[A-Z]+\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex RxSoloBase();

    /// <summary>Una voce del sector che vale più aree: la base e due o più lettere (<c>R300AB</c>, <c>R405AC</c>).</summary>
    [GeneratedRegex(@"^(?<base>[A-Z]+\d+)(?<lettere>[A-D]{2,4})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPiuAree();

    [GeneratedRegex(@"\bZON[AE](?:/ZONE)?\s*[""'“”‘’]\s*(?<z>[A-Z])\s*[""'“”‘’]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex RxZona();

    [GeneratedRegex(@"\b([A-Z])\s+E\s+([A-Z])\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxCongiunzione();

    [GeneratedRegex(@"^LI(?:/L[A-Z])?\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RxPrefissoDelloStato();

    /// <summary>Gli spazi dell'AIP di quella famiglia, a gruppi (le zone di uno stesso spazio insieme), in ordine di nome.</summary>
    public static IReadOnlyList<(string Nome, IReadOnlyList<Airspace> Zone)> Gruppi(FamigliaDelConfronto famiglia, IEnumerable<Airspace> aip) =>
        [.. aip.Select(a => (Nome: NomeDelGruppo(famiglia, a), Area: a))
            .Where(x => x.Nome is not null)
            .GroupBy(x => x.Nome!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, (IReadOnlyList<Airspace>)[.. g.Select(x => x.Area)]))
            .OrderBy(g => g.Item1, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Il nome del gruppo di uno spazio dell'AIP (<c>Bologna CTR</c>, <c>Amendola ATZ</c>, <c>ATZ Guidonia LIRG</c>), o null se non è della famiglia.</summary>
    private static string? NomeDelGruppo(FamigliaDelConfronto famiglia, Airspace a) => famiglia switch
    {
        FamigliaDelConfronto.Ctr => a.Type == AirspaceType.Ctr ? NomeDelCtr(a.Designator) : null,
        _ when a.Type == AirspaceType.Atz => a.Designator.Trim() + " ATZ",
        _ when a.Type == AirspaceType.Rmz && RxAtzRmz().Match(a.Designator) is { Success: true } m => m.Groups["atz"].Value.Trim(),
        _ => null,
    };

    /// <summary>Un punto come lo scrive l'AIP (<c>45°35'43"N 008°55'55"E</c>), per dire all'AOD dove guardare.</summary>
    public static string Dms((double Lat, double Lon) p)
    {
        static string Parte(double v, int cifre, char pos, char neg)
        {
            var s = (long)Math.Round(Math.Abs(v) * 3600);
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{(s / 3600).ToString(new string('0', cifre), System.Globalization.CultureInfo.InvariantCulture)}°{s / 60 % 60:00}'{s % 60:00}\"{(v < 0 ? neg : pos)}");
        }
        return Parte(p.Lat, 2, 'N', 'S') + " " + Parte(p.Lon, 3, 'E', 'W');
    }

    /// <summary>«Bologna CTR Zona/Zone '1'» → «Bologna CTR»; un nome senza CTR resta com'è.</summary>
    public static string NomeDelCtr(string designatore) =>
        RxNomeDelCtr().Match(designatore) is { Success: true } m ? m.Groups["ctr"].Value.Trim() : designatore.Trim();

    private static DifferenzaDiForma Misura(string nome, int zone, List<(Position A, Position B, bool Giudicabile)> contorno, FormaDellaMappa voce, double soglia)
    {
        var (lat0, lon0) = Centro(contorno);
        var latiAip = Proietta(contorno, lat0, lon0);
        var latiSector = ProiettaLaVoce(voce, lat0, lon0);

        // L'AIP contro il sector: solo i lati che l'AIP disegna.
        double scartoAip = 0;
        (double X, double Y) peggioreAip = default;
        foreach (var p in Campioni(latiAip.Where(l => l.Giudicabile)))
        {
            var d = Distanza(p, latiSector).Distanza;
            if (d > scartoAip) (scartoAip, peggioreAip) = (d, p);
        }

        // Il sector contro l'AIP: un pezzo di sector più vicino a un confine non disegnato che ad altro non si giudica.
        double scartoSector = 0;
        (double X, double Y) peggioreSector = default;
        var senzaTracciato = false;
        foreach (var p in Campioni(latiSector))
        {
            var (d, lato) = Distanza(p, latiAip);
            if (lato is { Giudicabile: false })
            {
                senzaTracciato = true;
                continue;
            }
            if (d > scartoSector) (scartoSector, peggioreSector) = (d, p);
        }

        var uguale = scartoAip <= soglia && scartoSector <= soglia;
        var peggiore = scartoAip >= scartoSector ? peggioreAip : peggioreSector;
        return new DifferenzaDiForma(
            uguale ? EsitoDelConfronto.Uguale : EsitoDelConfronto.FormaDiversa,
            nome, zone, voce.Etichetta, voce.File, voce.Record,
            Math.Round(scartoAip, 3), Math.Round(scartoSector, 3),
            uguale ? null : Indietro(peggiore, lat0, lon0),
            senzaTracciato);
    }

    /// <summary>I lati dei contorni delle zone: archi e cerchi disegnati fitti (4 punti per grado), i confini come lati non giudicabili.</summary>
    private static List<(Position A, Position B, bool Giudicabile)> Contorni(IEnumerable<Airspace> zone)
    {
        var lati = new List<(Position, Position, bool)>();
        foreach (var z in zone)
        {
            switch (z.Geometry)
            {
                case CircleGeometry c:
                    var cerchio = Outline.Circle(c, 4);
                    for (var i = 0; i < cerchio.Count; i++) lati.Add((cerchio[i], cerchio[(i + 1) % cerchio.Count], true));
                    break;
                case CorridorGeometry k:
                    var contorno = Outline.Corridor(k);
                    for (var i = 0; i < contorno.Count; i++) lati.Add((contorno[i], contorno[(i + 1) % contorno.Count], true));
                    break;
                case RingGeometry r:
                    for (var i = 0; i < r.Segments.Count; i++)
                    {
                        var s = r.Segments[i];
                        var fine = r.Segments[(i + 1) % r.Segments.Count].From;
                        switch (s)
                        {
                            case ArcSegment arco:
                                var punti = Outline.Arc(arco.From, fine, arco.Center, arco.Direction, 4).Append(fine).ToList();
                                for (var k = 0; k + 1 < punti.Count; k++) lati.Add((punti[k], punti[k + 1], true));
                                break;
                            case BorderSegment:
                                lati.Add((s.From, fine, false));
                                break;
                            default:
                                lati.Add((s.From, fine, true));
                                break;
                        }
                    }
                    break;
            }
        }
        return lati;
    }

    private static (double Lat, double Lon) Centro(List<(Position A, Position B, bool G)> lati) =>
        lati.Count == 0 ? (0, 0) : (lati.Average(l => l.A.Lat), lati.Average(l => l.A.Lon));

    private static List<Lato> Proietta(List<(Position A, Position B, bool Giudicabile)> lati, double lat0, double lon0)
    {
        var k = Math.Cos(lat0 * Math.PI / 180);
        return lati.Select(l => new Lato((l.A.Lon - lon0) * 60 * k, (l.A.Lat - lat0) * 60, (l.B.Lon - lon0) * 60 * k, (l.B.Lat - lat0) * 60, l.Giudicabile)).ToList();
    }

    private static List<Lato> ProiettaLaVoce(FormaDellaMappa voce, double lat0, double lon0)
    {
        var k = Math.Cos(lat0 * Math.PI / 180);
        var lati = new List<Lato>();
        foreach (var tratto in voce.Tratti)
        {
            // Un'area (un settore di .tfl) si chiude da sé: il lato dall'ultimo punto al primo c'è anche se non è scritto.
            var chiusa = voce.Tipo == TipoDiForma.Area && tratto.Count > 2 && !tratto[0].Equals(tratto[^1]);
            for (var i = 0; i + 1 < tratto.Count + (chiusa ? 1 : 0); i++)
            {
                var (a, b) = (tratto[i], tratto[(i + 1) % tratto.Count]);
                lati.Add(new Lato((a.LongitudeDeg - lon0) * 60 * k, (a.LatitudeDeg - lat0) * 60,
                    (b.LongitudeDeg - lon0) * 60 * k, (b.LatitudeDeg - lat0) * 60, true));
            }
        }
        return lati;
    }

    private static (double Lat, double Lon) Indietro((double X, double Y) p, double lat0, double lon0) =>
        (lat0 + p.Y / 60, lon0 + p.X / (60 * Math.Cos(lat0 * Math.PI / 180)));

    private static bool Vicini(List<Lato> a, List<Lato> b)
    {
        if (a.Count == 0 || b.Count == 0) return false;
        static (double X0, double Y0, double X1, double Y1) Scatola(List<Lato> l) =>
            (l.Min(x => Math.Min(x.Ax, x.Bx)), l.Min(x => Math.Min(x.Ay, x.By)), l.Max(x => Math.Max(x.Ax, x.Bx)), l.Max(x => Math.Max(x.Ay, x.By)));
        var (a0, a1, a2, a3) = Scatola(a);
        var (b0, b1, b2, b3) = Scatola(b);
        return a0 <= b2 + AbbinamentoMassimo && b0 <= a2 + AbbinamentoMassimo && a1 <= b3 + AbbinamentoMassimo && b1 <= a3 + AbbinamentoMassimo;
    }

    private static IEnumerable<(double X, double Y)> Campioni(IEnumerable<Lato> lati)
    {
        foreach (var l in lati)
        {
            var lunghezza = Math.Sqrt((l.Bx - l.Ax) * (l.Bx - l.Ax) + (l.By - l.Ay) * (l.By - l.Ay));
            var n = Math.Max(1, (int)Math.Ceiling(lunghezza / Passo));
            for (var i = 0; i <= n; i++)
            {
                var t = (double)i / n;
                yield return (l.Ax + (l.Bx - l.Ax) * t, l.Ay + (l.By - l.Ay) * t);
            }
        }
    }

    private static (double Distanza, Lato? Lato) Distanza((double X, double Y) p, List<Lato> lati)
    {
        var migliore = double.MaxValue;
        Lato? quale = null;
        foreach (var l in lati)
        {
            var dx = l.Bx - l.Ax;
            var dy = l.By - l.Ay;
            var len2 = dx * dx + dy * dy;
            var t = len2 == 0 ? 0 : Math.Clamp(((p.X - l.Ax) * dx + (p.Y - l.Ay) * dy) / len2, 0, 1);
            var qx = l.Ax + t * dx - p.X;
            var qy = l.Ay + t * dy - p.Y;
            var d = Math.Sqrt(qx * qx + qy * qy);
            if (d < migliore)
            {
                migliore = d;
                quale = l;
            }
        }
        return (migliore, quale);
    }
}

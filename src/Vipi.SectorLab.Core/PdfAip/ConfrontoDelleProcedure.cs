using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una riga del confronto delle procedure (carta F7, slice 7d): una SID, una STAR o una transizione dell'AIP e il sector, o
/// una voce del sector che l'AIP non ha. <paramref name="Nomi"/>: i nomi del sector che le corrispondono
/// (<c>XIBR5A</c>, <c>IRK7A-PEP2X</c>). <paramref name="File"/>/<paramref name="Record"/>: la prima voce del sector da aprire.
/// </summary>
public sealed record DifferenzaDiProcedura(EsitoDelConfronto Esito, string Scalo, ProcedureKind Genere, string Nome, IReadOnlyList<string> Piste,
    IReadOnlyList<string> Nomi, string? File, int? Record, IReadOnlyList<string> Dettagli)
{
    /// <summary>Le voci del sector di questa SID che non hanno tracciato (oggi tutte): «Scrivi i tracciati» (7e, D2) gliel'aggiunge.</summary>
    public int VociSenzaTracciato { get; init; }
}

/// <summary>
/// Le procedure di AD 2 dell'AIP espanso contro il sector, in sola lettura (carta F7, slice 7d): le SID e le transizioni coi
/// <c>.sid</c> dello scalo, le STAR con le procedure degli <c>.str</c>.
/// <para><b>Il nome</b> propone la coppia (la regola della prova della F0, carta madre §10): stessa coda — la cifra e la
/// lettera, <c>5A</c> — e il nome del sector è l'inizio di quello dell'AIP (<c>XIBR5A</c> per <c>XIBRI 5A</c>, <c>IRK7A</c>
/// per <c>IRKED 7A</c>, <c>BAGIX3R</c> intero) o, meno buono, le sue lettere nello stesso ordine con la stessa iniziale
/// (<c>NOK1K</c> per <c>NOBKE 1K</c>, <c>SIV5A</c> per <c>SOSIV 5A</c>). Un nome del sector con la transizione (<c>IRK7A-PEP2X</c>) vale per la SID
/// e per la transizione. Se il nome del sector va bene per più procedure dell'AIP dello stesso scalo, si sceglie quella col
/// nome più vicino, e se restano pari si dice.</para>
/// <para><b>Il percorso</b>: per una STAR, i punti dell'AIP in fila (i tratti senza punto no) contro quelli dello <c>.str</c>;
/// per una SID, che nel sector non ha tracciato (§1), il punto del campo 7 contro l'ultimo dell'AIP. Le piste: quelle
/// dell'AIP che nessuna voce del sector ha. I punti: quelli dell'AIP che il sector non ha, o che ha a più di 0,1 NM (D4).</para>
/// <para>Le salite iniziali non hanno una voce nel sector: si contano e basta. Le convenzionali coi tratti ricavati dalla
/// prosa (7b): i punti con nome in fila contro quelli con nome dello <c>.str</c> (il sector disegna le virate con righe di
/// coordinate, che qui non contano), e ogni punto calcolato vicino al tracciato del sector (<see cref="SogliaDeiCalcolati"/>).
/// Quelle la cui prosa non si è letta si confrontano per nome e pista.</para>
/// </summary>
public static partial class ConfrontoDelleProcedure
{
    /// <summary>Oltre questo scarto (NM) un punto dell'AIP non è quello del sector con lo stesso nome (D4).</summary>
    public const double Soglia = 0.1;

    /// <summary>
    /// Oltre questo scarto (NM) il tracciato del sector non passa per un punto calcolato della prosa: il sector arrotonda le
    /// virate (a 220 kt il raggio è più di un miglio) e il punto della prosa è lo spigolo. Misurato su LICA: DELI3E e RCA3F
    /// anticipano la virata verso l'arco DME di 1,7 e 1,1 NM; le altre stanno sotto 0,5.
    /// </summary>
    public const double SogliaDeiCalcolati = 2.0;

    [GeneratedRegex(@"^(?<b>[A-Z]+)\s?(?<c>\d{1,2}[A-Z])$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxNome();

    [GeneratedRegex(@"^\d{2}[LRC]?$", RegexOptions.CultureInvariant)]
    private static partial Regex RxPista();

    /// <summary>Una voce del sector: il file, il record, il nome intero, le sue parti (SID e transizione), le piste, il genere.</summary>
    private sealed record Voce(string File, int Record, string Nome, IReadOnlyList<string> Parti, IReadOnlyList<string> Piste, bool Star,
        string? Fix, IReadOnlyList<string> Punti, bool SenzaTracciato = false, IReadOnlyList<Punto>? Tracciato = null);

    /// <summary>
    /// Il confronto. <paramref name="puntoDelSector"/>: dove il sector ha un punto, per nome (il catalogo del master scelto);
    /// <paramref name="puntoDellAip"/>: dove l'AIP espanso ha un punto (ENR 4.4, punti di scalo, radioassistenze).
    /// </summary>
    /// <param name="abbinamenti">Se c'è, vi si scrive per ogni voce del sector (file, record) la procedura dell'AIP di ogni
    /// parte del nome (la SID e la sua transizione; null dove non se ne trova una sola): i gesti della 7e ne hanno bisogno.</param>
    public static IReadOnlyList<DifferenzaDiProcedura> Di(IEnumerable<Procedure> procedure, SessioneAperta sessione,
        Func<string, Coordinate?> puntoDelSector, Func<string, Position?> puntoDellAip,
        IDictionary<(string File, int Record), Procedure?[]>? abbinamenti = null)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(puntoDelSector);
        ArgumentNullException.ThrowIfNull(puntoDellAip);

        var esiti = new List<DifferenzaDiProcedura>();
        foreach (var scalo in procedure.Where(p => p.Kind != ProcedureKind.InitialClimb).GroupBy(p => p.Aerodrome).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var voci = VociDelSector(sessione, scalo.Key);
            var aip = scalo.ToList();
            // Ogni parte di ogni voce del sector va alla procedura dell'AIP che le somiglia di più.
            var abbinate = new Dictionary<Procedure, List<(Voce Voce, int Parte)>>();
            var usate = new HashSet<Voce>();
            var ambigue = new List<string>();
            // Per ogni parte di ogni voce del sector, le procedure dell'AIP che le somigliano di più.
            var scelte = new List<(Voce Voce, int Parte, List<Procedure> Migliori)>();
            foreach (var v in voci)
                for (var k = 0; k < v.Parti.Count; k++)
                    if (Migliori(aip, v, k) is { Count: > 0 } migliori)
                        scelte.Add((v, k, migliori));
            // Un nome che va bene per più designatori (LIRF SOS5A: SOSAK 5A o SOSIV 5A) lascia quelli che un altro nome del
            // sector prende già da solo (SIV5A è SOSIV 5A): resta SOSAK 5A.
            var giaPresi = scelte.Where(x => x.Migliori.Select(p => p.Designator).Distinct().Count() == 1)
                .SelectMany(x => x.Migliori.Select(p => p.Designator)).ToHashSet(StringComparer.Ordinal);
            foreach (var (v, k, migliori) in scelte)
            {
                var restano = migliori;
                if (migliori.Select(p => p.Designator).Distinct().Count() > 1
                    && migliori.Where(p => !giaPresi.Contains(p.Designator)).ToList() is { Count: > 0 } liberi
                    && liberi.Select(p => p.Designator).Distinct().Count() == 1)
                    restano = liberi;
                // Una SID con la sua transizione (SOS5A-ESI8H): la SID giusta finisce dove la transizione comincia (ESINO 8H
                // parte da SOSAK: è SOSAK 5A, non SOSIV 5A).
                if (restano.Select(p => p.Designator).Distinct().Count() > 1 && k == 0 && v.Parti.Count > 1
                    && Migliori(aip, v, 1) is { Count: > 0 } transizioni
                    && transizioni.Select(t => Punti(t).FirstOrDefault()).OfType<string>().ToHashSet(StringComparer.Ordinal) is var inizi
                    && restano.Where(p => Punti(p).LastOrDefault() is { } fine && inizi.Contains(fine)).ToList() is { Count: > 0 } legate
                    && legate.Select(p => p.Designator).Distinct().Count() == 1)
                    restano = legate;
                if (restano.Select(p => p.Designator).Distinct().Count() > 1)
                    ambigue.Add($"{v.Parti[k]}: {string.Join(" o ", restano.Select(p => p.Designator).Distinct())}");
                foreach (var p in restano)
                {
                    if (!abbinate.TryGetValue(p, out var l)) abbinate[p] = l = [];
                    l.Add((v, k));
                }
                usate.Add(v);
                if (abbinamenti is not null)
                {
                    if (!abbinamenti.TryGetValue((v.File, v.Record), out var parti)) abbinamenti[(v.File, v.Record)] = parti = new Procedure?[v.Parti.Count];
                    parti[k] = restano.Select(p => p.Designator).Distinct().Count() == 1 ? restano[0] : null;
                }
            }

            foreach (var p in aip.OrderBy(p => p.Kind).ThenBy(p => p.Designator, StringComparer.Ordinal))
            {
                if (!abbinate.TryGetValue(p, out var suoi))
                {
                    var revisione = Revisioni(voci.Where(v => !usate.Contains(v)), p);
                    esiti.Add(new DifferenzaDiProcedura(EsitoDelConfronto.MancaNelSector, p.Aerodrome, p.Kind, p.Designator, p.Runways, [], null, null,
                    [
                        p.Legs.Count == 0 ? "convenzionale in sola prosa (non letta)" : $"{p.Legs.Count} tratti{(p.LegsFromDescription ? " (dalla prosa)" : "")}: {string.Join(" ", Punti(p))}",
                        .. revisione.Count > 0 ? [$"nel sector c'è {string.Join(", ", revisione.Select(v => v.Nome))}: stesso nome, altro numero (un'altra revisione?)"] : Array.Empty<string>(),
                    ]));
                    continue;
                }
                var dettagli = new List<string>();
                var pisteDelSector = suoi.SelectMany(s => s.Voce.Piste).ToHashSet(StringComparer.Ordinal);
                var mancano = p.Runways.Where(r => pisteDelSector.Count > 0 && !pisteDelSector.Contains(r)).ToList();
                if (mancano.Count > 0) dettagli.Add($"pista {string.Join(", ", mancano)}: nel sector non c'è");
                var punti = Punti(p);
                if (punti.Count > 0 && p.Kind == ProcedureKind.Star && p.LegsFromDescription)
                {
                    foreach (var (v, _) in suoi.DistinctBy(s => s.Voce))
                    {
                        var traccia = v.Tracciato ?? [];
                        var tutti = traccia.Where(x => x.PerNome).Select(x => x.Nome!).ToList();
                        List<string> nomi = [.. tutti.Where((f, j) => j == 0 || f != tutti[j - 1])];
                        if (!nomi.SequenceEqual(punti, StringComparer.Ordinal))
                            dettagli.Add($"{v.Nome}: punti con nome nel sector {Fila(nomi)}, nell'AIP {Fila(punti)}");
                        var posizioni = traccia.Select(x => x.Posizione ?? (x.Nome is { } n ? puntoDelSector(n) : null)).OfType<Coordinate>()
                            .Select(c => new Position(c.LatitudeDeg, c.LongitudeDeg)).ToList();
                        foreach (var t in p.Legs.Where(t => t.Position is not null))
                        {
                            var d = posizioni.Count == 0 ? double.PositiveInfinity : posizioni.Min(x => Sphere.DistanceNm(x, t.Position!.Value));
                            if (d > SogliaDeiCalcolati)
                                dettagli.Add($"{v.Nome}: {t.Computed}: il tracciato del sector passa a {(double.IsInfinity(d) ? "—" : d.ToString("0.0", CultureInfo.InvariantCulture))} NM");
                        }
                    }
                }
                else if (punti.Count > 0 && p.Kind == ProcedureKind.Star)
                {
                    foreach (var (v, _) in suoi.DistinctBy(s => s.Voce))
                        if (!v.Punti.SequenceEqual(punti, StringComparer.Ordinal))
                            dettagli.Add($"{v.Nome}: punti nel sector {Fila(v.Punti)}, nell'AIP {Fila(punti)}");
                }
                else if (punti.Count > 0)
                {
                    // Una SID: il punto del campo 7 è l'ultimo della SID (voce senza transizione) o della sua transizione.
                    foreach (var (v, parte) in suoi.Where(s => s.Voce.Parti.Count == s.Parte + 1).DistinctBy(s => s.Voce))
                        if (v.Fix is { Length: > 0 } fix && !string.Equals(fix.Trim(), punti[^1], StringComparison.Ordinal))
                            dettagli.Add($"{v.Nome}: punto {fix.Trim()} nel sector, {punti[^1]} alla fine dell'AIP");
                }
                foreach (var nome in punti.Distinct())
                {
                    var nelSector = puntoDelSector(nome);
                    if (nelSector is null)
                        dettagli.Add($"{nome}: il sector non ha il punto");
                    else if (puntoDellAip(nome) is { } aipPunto
                             && Sphere.DistanceNm(aipPunto, new Position(nelSector.Value.LatitudeDeg, nelSector.Value.LongitudeDeg)) is var d && d > Soglia)
                        dettagli.Add($"{nome}: nel sector a {d.ToString("0.00", CultureInfo.InvariantCulture)} NM dall'AIP");
                }
                var prima = suoi.OrderBy(s => s.Voce.Record).First().Voce;
                esiti.Add(new DifferenzaDiProcedura(dettagli.Count == 0 ? EsitoDelConfronto.Uguale : EsitoDelConfronto.FormaDiversa,
                    p.Aerodrome, p.Kind, p.Designator, p.Runways, [.. suoi.Select(s => s.Voce.Nome).Distinct()], prima.File, prima.Record, dettagli)
                {
                    VociSenzaTracciato = p.Legs.Count == 0 ? 0 : suoi.Select(s => s.Voce).Distinct().Count(v => v.SenzaTracciato),
                });
            }

            foreach (var v in voci.Where(v => !usate.Contains(v)))
            {
                var nellAip = aip.Where(p => !abbinate.ContainsKey(p) && Revisioni([v], p).Count > 0).Select(p => p.Designator).Distinct().ToList();
                esiti.Add(new DifferenzaDiProcedura(EsitoDelConfronto.SoloNelSector, scalo.Key, v.Star ? ProcedureKind.Star : ProcedureKind.Sid, v.Nome,
                    v.Piste, [v.Nome], v.File, v.Record,
                [
                    .. v.Star ? [$"punti: {Fila(v.Punti)}"] : Array.Empty<string>(),
                    .. nellAip.Count > 0 ? [$"nell'AIP c'è {string.Join(", ", nellAip)}: stesso nome, altro numero (un'altra revisione?)"] : Array.Empty<string>(),
                ]));
            }
            foreach (var a in ambigue.Distinct())
                esiti.Add(new DifferenzaDiProcedura(EsitoDelConfronto.FormaDiversa, scalo.Key, ProcedureKind.Sid, a.Split(':')[0], [], [], null, null,
                    [$"il nome del sector va bene per più procedure dell'AIP: {a}"]));
        }
        return esiti;
    }

    /// <summary>
    /// Le procedure dell'AIP che somigliano di più alla parte <paramref name="k"/> del nome di una voce del sector, con una pista
    /// in comune. La prima parte è una SID (o una STAR); la seconda una transizione — o una SID: LIRF stampa le sue
    /// transizioni (ESINO 8H, SOVAN 1E) fra le SID, senza pista.
    /// </summary>
    private static List<Procedure> Migliori(List<Procedure> aip, Voce v, int k)
    {
        var generi = v.Star ? [ProcedureKind.Star] : k == 0 ? [ProcedureKind.Sid] : new[] { ProcedureKind.Transition, ProcedureKind.Sid };
        var candidate = aip.Where(p => generi.Contains(p.Kind) && Somiglia(v.Parti[k], p.Designator) is not null
                                       && (p.Runways.Count == 0 || v.Piste.Count == 0 || p.Runways.Intersect(v.Piste).Any())).ToList();
        if (candidate.Count == 0) return [];
        var migliore = candidate.Min(p => Somiglia(v.Parti[k], p.Designator)!.Value);
        return [.. candidate.Where(p => Somiglia(v.Parti[k], p.Designator) == migliore)];
    }

    /// <summary>
    /// Quanto il nome di una parte del sector (<c>XIBR5A</c>) somiglia al designatore dell'AIP (<c>XIBRI 5A</c>): null se non
    /// è lui (coda diversa, o il nome del sector non è l'inizio di quello dell'AIP); altrimenti le lettere che mancano (0 = uguale).
    /// </summary>
    internal static int? Somiglia(string delSector, string dellAip)
    {
        var s = RxNome().Match(delSector.Trim());
        var a = RxNome().Match(dellAip.Replace(" ", "", StringComparison.Ordinal));
        if (!s.Success || !a.Success || s.Groups["c"].Value != a.Groups["c"].Value) return null;
        var bs = s.Groups["b"].Value;
        var ba = a.Groups["b"].Value;
        if (bs.Length < 3 || bs[0] != ba[0]) return null;
        if (ba.StartsWith(bs, StringComparison.Ordinal)) return ba.Length - bs.Length;
        // Il sector abbrevia anche saltando lettere (LIMC NOK1K per NOBKE 1K, LIRF SIV5A per SOSIV 5A): stessa iniziale e le
        // lettere nello stesso ordine. Vale meno dell'inizio del nome: fra due candidate vince quella che comincia così.
        var j = 0;
        foreach (var c in ba)
            if (j < bs.Length && c == bs[j]) j++;
        return j == bs.Length ? 10 + ba.Length - bs.Length : null;
    }

    /// <summary>
    /// Le voci del sector che sono forse un'altra revisione della procedura dell'AIP: stessa lettera, numero diverso, e il
    /// nome che torna con la regola del nome (<c>GILI3T</c> per <c>GILIO 4T</c>).
    /// </summary>
    private static List<Voce> Revisioni(IEnumerable<Voce> voci, Procedure p)
    {
        var a = RxNome().Match(p.Designator.Replace(" ", "", StringComparison.Ordinal));
        if (!a.Success) return [];
        var lettera = a.Groups["c"].Value[^1];
        return [.. voci.Where(v => v.Star == (p.Kind == ProcedureKind.Star) && v.Parti.Any(parte =>
            RxNome().Match(parte) is { Success: true } m && m.Groups["c"].Value[^1] == lettera && m.Groups["c"].Value != a.Groups["c"].Value
            && Somiglia(m.Groups["b"].Value + a.Groups["c"].Value, p.Designator) is not null))];
    }

    /// <summary>I punti della procedura in fila: i tratti con un punto (un CA non ne ha), senza ripetere il punto di seguito.</summary>
    internal static List<string> Punti(Procedure p)
    {
        var punti = new List<string>();
        foreach (var t in p.Legs)
            if (t.Fix is { } f && (punti.Count == 0 || punti[^1] != f))
                punti.Add(f);
        return punti;
    }

    private static string Fila(IReadOnlyList<string> punti) => punti.Count == 0 ? "nessuno" : string.Join(" ", punti);

    /// <summary>Le SID del <c>.sid</c> e le STAR del <c>.str</c> dello scalo (le voci con una pista vera e il tipo vuoto o 0).</summary>
    private static List<Voce> VociDelSector(SessioneAperta sessione, string icao)
    {
        var voci = new List<Voce>();
        foreach (var (file, aperto) in sessione.File)
        {
            var nome = file[(file.LastIndexOf('/') + 1)..];
            if (aperto is not IFileConRecord conRecord) continue;
            if (nome.Equals(icao + ".sid", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < conRecord.RecordDelModello.Count; i++)
                    // La mappa di gruppo in testa al file (MAPS, F7 slice 7f) non è una SID.
                    if (conRecord.RecordDelModello[i] is SidProcedure sid && sid.IcaoCode.Equals(icao, StringComparison.OrdinalIgnoreCase)
                        && !MappeComposte.EUnaMappa(sid))
                        voci.Add(new Voce(file, i, sid.Name.Trim(), Parti(sid.Name), Piste(sid.Runway), Star: false, sid.RelatedFix, [], sid.Track.Count == 0));
            }
            else if (nome.Equals(icao + ".str", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < conRecord.RecordDelModello.Count; i++)
                {
                    // Una STAR coi soli punti con nome è un ProcedureStrRecord; una che disegna le virate con righe di
                    // coordinate (le convenzionali di LICA: ARBU4F, DELI1H) il parser la legge come HoldingStrRecord.
                    if (conRecord.RecordDelModello[i] is not StrRecord star || !star.IcaoCode.Equals(icao, StringComparison.OrdinalIgnoreCase)
                        || star.RecordType != StrRecordType.Star) continue;
                    List<Punto>? tracciato = star switch
                    {
                        ProcedureStrRecord p => [.. p.Waypoints.Select(w => Punto.Nominato(w.FixName.Trim()))],
                        HoldingStrRecord h => [.. h.Points.Select(x => x is HoldingCoordPoint c ? Punto.Da(c.Position) : Punto.Nominato(((HoldingFixPoint)x).FixName.Trim()))],
                        _ => null,
                    };
                    if (tracciato is null) continue;
                    var piste = Piste(star.RunwaySpec);
                    if (piste.Count == 0) continue;
                    var nomi = tracciato.Where(x => x.PerNome).Select(x => x.Nome!).ToList();
                    voci.Add(new Voce(file, i, star.ProcedureId.Trim(), [star.ProcedureId.Trim()], piste, Star: true, null,
                        // Un punto ripetuto di seguito (DEVOX;DEVOX; due volte, LIMC) è lo stesso punto.
                        [.. nomi.Where((f, j) => j == 0 || f != nomi[j - 1])],
                        Tracciato: tracciato));
                }
            }
        }
        return voci;
    }

    private static IReadOnlyList<string> Parti(string nome) =>
        [.. nome.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>Le piste di un campo «06:24» o «16L:16R» (un <c>MAPS</c> nell'elenco non è una pista).</summary>
    private static IReadOnlyList<string> Piste(string campo) =>
        [.. campo.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(t => RxPista().IsMatch(t))];
}

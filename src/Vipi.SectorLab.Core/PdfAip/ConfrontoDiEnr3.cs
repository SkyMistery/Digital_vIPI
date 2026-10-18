using System.Globalization;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una riga del confronto di ENR 3 (carta F6, slice 2b): una rotta o un'attesa, com'è rispetto al sector, con le righe che
/// dicono le differenze. <paramref name="File"/>/<paramref name="Record"/>: la voce del sector da aprire (il primo pezzo
/// della rotta, l'attesa), se c'è.
/// </summary>
public sealed record DifferenzaDiEnr3(EsitoDelConfronto Esito, string Nome, string? File, int? Record, IReadOnlyList<string> Dettagli);

/// <summary>
/// Il confronto di ENR 3 col sector, in sola lettura (slice 2b).
/// <para><b>Le rotte</b> (ENR 3.2) coi tracciati <c>T;</c> di <c>itawlow.lairway</c>: un'aerovia può essere in più pezzi
/// (le interruzioni <c>T;BREAK</c>). Si confrontano i <b>lati</b> (due punti di seguito, in qualunque verso): quelli solo
/// dell'AIP e quelli solo del sector. I punti del sector oltre il primo e l'ultimo dell'AIP italiano (la rotta che continua
/// in Austria, a Malta) non sono differenze: si dicono a parte. Sui lati che ci sono tutti e due, i tratti: il verso e le
/// quote dell'AIP contro i tag <c>//@@ dir lower upper</c> del sector (B2), col verso rispetto all'ordine del file; un
/// tratto senza tag si conta, non è una differenza (il sector non li ha ancora: li porterà la 2c). Le posizioni: il punto
/// dell'AIP contro quello che il sector ha con quel nome, oltre 0,1 NM.</para>
/// <para><b>Le attese</b> (ENR 3.4.1) con <c>HOLDENR.hold</c>: per punto, la rotta d'avvicinamento, la virata e la quota
/// (fra le attese dell'AIP di quel punto, la più somigliante). Un'attesa dell'AIP che <c>HOLDENR.hold</c> non ha può essere
/// un'attesa di scalo (<c>HLD-…</c> in uno <c>.str</c>): si dice.</para>
/// </summary>
public static class ConfrontoDiEnr3
{
    /// <summary>Oltre questo scarto (NM) un punto dell'AIP non è quello del sector con lo stesso nome (D4).</summary>
    public const double Soglia = 0.1;

    public const string FileDelleAerovie = "itawlow.lairway";
    public const string FileDelleAttese = "HOLDENR.hold";

    private sealed record Pezzo(int Record, IReadOnlyList<PuntoConTag> Punti);

    // --- le rotte ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Le rotte dell'AIP contro le aerovie del sector. <paramref name="puntoDelSector"/>: dove il sector ha un punto, per
    /// nome (il catalogo del master scelto).
    /// </summary>
    public static IReadOnlyList<DifferenzaDiEnr3> Rotte(IEnumerable<Route> rotte, SessioneAperta sessione, Func<string, Coordinate?> puntoDelSector)
    {
        ArgumentNullException.ThrowIfNull(rotte);
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(puntoDelSector);
        var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + FileDelleAerovie, StringComparison.OrdinalIgnoreCase));
        var pezzi = new Dictionary<string, List<Pezzo>>(StringComparer.OrdinalIgnoreCase);
        if (file is not null && sessione.File[file] is IFileConRecord aerovie)
        {
            for (var i = 0; i < aerovie.RecordDelModello.Count; i++)
            {
                if (aerovie.RecordDelModello[i] is not Airway a || !TrattiDelleAerovie.EUnPezzo(a)) continue;
                if (!pezzi.TryGetValue(a.Name.Trim(), out var suoi)) pezzi[a.Name.Trim()] = suoi = [];
                suoi.Add(new Pezzo(i, aerovie.PuntiConTag(i)));
            }
        }

        var esiti = new List<DifferenzaDiEnr3>();
        foreach (var rotta in rotte.OrderBy(r => r.Designator, StringComparer.OrdinalIgnoreCase))
        {
            if (!pezzi.TryGetValue(rotta.Designator, out var suoi))
            {
                esiti.Add(new DifferenzaDiEnr3(EsitoDelConfronto.MancaNelSector, rotta.Designator, null, null,
                    [$"{rotta.Points.Count} punti nell'AIP: {string.Join(" ", rotta.Points.Select(p => p.Designator))}"]));
                continue;
            }
            esiti.Add(Rotta(rotta, suoi, file!, puntoDelSector));
        }

        return [.. esiti.OrderBy(e => Ordine(e.Esito)).ThenBy(e => e.Nome, StringComparer.OrdinalIgnoreCase)];
    }

    private static int Ordine(EsitoDelConfronto e) => e switch
    {
        EsitoDelConfronto.FormaDiversa => 0,
        EsitoDelConfronto.MancaNelSector => 1,
        EsitoDelConfronto.SoloNelSector => 2,
        _ => 3,
    };

    private static string Lato(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private static DifferenzaDiEnr3 Rotta(Route rotta, List<Pezzo> pezzi, string file, Func<string, Coordinate?> puntoDelSector)
    {
        var dettagli = new List<string>();
        var diversa = false;

        // I lati dell'AIP (non attraverso un'interruzione), coi loro tratti.
        var latiAip = new Dictionary<string, (int Indice, string Da, string A)>(StringComparer.Ordinal);
        for (var i = 0; i < rotta.Segments.Count; i++)
            if (!rotta.Segments[i].Discontinuity)
                latiAip.TryAdd(Lato(rotta.Points[i].Designator, rotta.Points[i + 1].Designator), (i, rotta.Points[i].Designator, rotta.Points[i + 1].Designator));

        // I lati del sector, col verso del file e il tag del punto che apre il tratto.
        var latiSector = new Dictionary<string, (string Da, string A, IReadOnlyDictionary<string, string> Tag)>(StringComparer.Ordinal);
        foreach (var pezzo in pezzi)
            for (var i = 0; i + 1 < pezzo.Punti.Count; i++)
                latiSector.TryAdd(Lato(pezzo.Punti[i].Punto, pezzo.Punti[i + 1].Punto), (pezzo.Punti[i].Punto, pezzo.Punti[i + 1].Punto, pezzo.Punti[i].Chiavi));

        // I punti del sector oltre gli estremi dell'AIP italiano: la rotta che continua fuori.
        var puntiAip = rotta.Points.Select(p => p.Designator).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var oltre = Oltre(pezzi, puntiAip, rotta.Points[0].Designator, rotta.Points[^1].Designator);

        var soloAip = latiAip.Where(l => !latiSector.ContainsKey(l.Key)).Select(l => $"{l.Value.Da}–{l.Value.A}").ToList();
        var soloSector = latiSector.Where(l => !latiAip.ContainsKey(l.Key) && !(oltre.Contains(l.Value.Da) || oltre.Contains(l.Value.A)))
            .Select(l => $"{l.Value.Da}–{l.Value.A}").ToList();
        if (soloAip.Count > 0)
        {
            diversa = true;
            dettagli.Add($"lati solo nell'AIP ({soloAip.Count}): {string.Join(", ", soloAip)}");
        }
        if (soloSector.Count > 0)
        {
            diversa = true;
            dettagli.Add($"lati solo nel sector ({soloSector.Count}): {string.Join(", ", soloSector)}");
        }
        if (oltre.Count > 0)
            dettagli.Add($"il sector continua oltre l'AIP italiano: {string.Join(" ", oltre)}");

        // I tratti dei lati comuni.
        int uguali = 0, senzaTag = 0;
        var tratti = new List<string>();
        foreach (var (chiave, (indice, da, a)) in latiAip.OrderBy(l => l.Value.Indice))
        {
            if (!latiSector.TryGetValue(chiave, out var suo)) continue;
            var t = rotta.Segments[indice];
            var stessoVerso = string.Equals(suo.Da, da, StringComparison.OrdinalIgnoreCase);
            var verso = (t.Forward, t.Backward) switch
            {
                (true, true) => "both",
                (true, false) => stessoVerso ? "fwd" : "back",
                (false, true) => stessoVerso ? "back" : "fwd",
                _ => null,
            };
            var atteso = (Verso: verso, Inferiore: t.Lower?.Text, Superiore: t.Upper?.Text);
            string? Tag(string k) => suo.Tag.TryGetValue(k, out var v) && Metadati.Testo(v).Trim() is { Length: > 0 } x ? x : null;
            var scritto = (Verso: Tag(TrattiDelleAerovie.Verso), Inferiore: Tag(TrattiDelleAerovie.Inferiore), Superiore: Tag(TrattiDelleAerovie.Superiore));
            if (scritto is { Verso: null, Inferiore: null, Superiore: null })
            {
                senzaTag++;
                continue;
            }
            if (Uguale(atteso.Verso, scritto.Verso) && Uguale(atteso.Inferiore, scritto.Inferiore) && Uguale(atteso.Superiore, scritto.Superiore))
            {
                uguali++;
                continue;
            }
            diversa = true;
            tratti.Add($"{suo.Da}→{suo.A}: AIP {atteso.Verso} {atteso.Inferiore}–{atteso.Superiore}, sector {scritto.Verso ?? "—"} {scritto.Inferiore ?? "—"}–{scritto.Superiore ?? "—"}");
        }
        var comuni = latiAip.Count - soloAip.Count;
        dettagli.Add($"tratti: {comuni} in comune, {uguali} coi tag uguali all'AIP, {tratti.Count} diversi, {senzaTag} senza tag nel sector");
        dettagli.AddRange(tratti.Select(t => "  " + t));

        // Le posizioni: il punto dell'AIP contro quello del sector con lo stesso nome.
        foreach (var p in rotta.Points)
        {
            if (puntoDelSector(p.Designator) is not { } c)
            {
                dettagli.Add($"{p.Designator}: il sector non ha un punto con questo nome");
                continue;
            }
            var scarto = Sphere.DistanceNm(p.Position, new Position(c.LatitudeDeg, c.LongitudeDeg));
            if (scarto > Soglia)
            {
                diversa = true;
                dettagli.Add($"{p.Designator}: nel sector a {scarto.ToString("0.00", CultureInfo.InvariantCulture)} NM da dove lo mette l'AIP");
            }
        }

        return new DifferenzaDiEnr3(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, rotta.Designator, file, pezzi[0].Record, dettagli);
    }

    /// <summary>I punti dei pezzi del sector che stanno oltre il primo o l'ultimo punto dell'AIP, senza tornare nell'AIP.</summary>
    private static HashSet<string> Oltre(List<Pezzo> pezzi, HashSet<string> puntiAip, string primo, string ultimo)
    {
        var oltre = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pezzo in pezzi)
        {
            var nomi = pezzo.Punti.Select(p => p.Punto).ToList();
            foreach (var estremo in new[] { primo, ultimo })
            {
                var i = nomi.FindIndex(n => string.Equals(n, estremo, StringComparison.OrdinalIgnoreCase));
                if (i < 0) continue;
                // Da che parte del pezzo sta il resto dell'AIP: si va dall'altra. Se il resto dell'AIP non è in questo pezzo
                // non si sa da che parte sia «fuori», e quel che segue è una differenza, non una continuazione.
                var primaNelPezzo = i > 0 && puntiAip.Contains(nomi[i - 1]);
                var dopoNelPezzo = i + 1 < nomi.Count && puntiAip.Contains(nomi[i + 1]);
                if (primaNelPezzo == dopoNelPezzo) continue;
                var verso = dopoNelPezzo ? -1 : +1;
                for (var j = i + verso; j >= 0 && j < nomi.Count && !puntiAip.Contains(nomi[j]); j += verso)
                    oltre.Add(nomi[j]);
            }
        }
        return oltre;
    }

    /// <summary>Due valori uguali, senza badare a maiuscole e spazi (<c>FL 95</c> e <c>FL95</c>).</summary>
    private static bool Uguale(string? a, string? b) =>
        string.Equals(Normale(a), Normale(b), StringComparison.OrdinalIgnoreCase);

    private static string? Normale(string? s) => s?.Replace(" ", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal);

    // --- le attese ---------------------------------------------------------------------------------------------------

    /// <summary>Le attese dell'AIP contro quelle di <c>HOLDENR.hold</c> (e i nomi <c>HLD-…</c> degli <c>.str</c>).</summary>
    public static IReadOnlyList<DifferenzaDiEnr3> Attese(IEnumerable<Holding> attese, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(attese);
        ArgumentNullException.ThrowIfNull(sessione);
        var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + FileDelleAttese, StringComparison.OrdinalIgnoreCase));
        var delSector = new Dictionary<string, (int Record, Attesa Attesa)>(StringComparer.OrdinalIgnoreCase);
        if (file is not null && sessione.File[file] is IFileConRecord hold)
            for (var i = 0; i < hold.RecordDelModello.Count; i++)
                if (hold.RecordDelModello[i] is Attesa a && a.Fix is { } fix)
                    delSector.TryAdd(fix, (i, a));

        // Le attese di scalo degli .str (LIBA;11L;HLD-ELVAD;…): il punto, e dove.
        var diScalo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativo, aperto) in sessione.File)
        {
            if (!relativo.EndsWith(".str", StringComparison.OrdinalIgnoreCase) || aperto is not IFileConRecord str) continue;
            foreach (var r in str.RecordDelModello.OfType<StrRecord>())
                if (r.ProcedureId.StartsWith("HLD-", StringComparison.OrdinalIgnoreCase))
                    diScalo.TryAdd(r.ProcedureId[4..].Trim(), relativo[(relativo.LastIndexOf('/') + 1)..]);
        }

        var esiti = new List<DifferenzaDiEnr3>();
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var gruppo in attese.GroupBy(a => a.Fix, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            visti.Add(gruppo.Key);
            var aip = gruppo.ToList();
            var descritte = string.Join(" · ", aip.Select(Descrizione));
            if (!delSector.TryGetValue(gruppo.Key, out var suo))
            {
                var dove = diScalo.TryGetValue(gruppo.Key, out var str) ? $" (c'è come attesa di scalo in {str})" : "";
                esiti.Add(new DifferenzaDiEnr3(EsitoDelConfronto.MancaNelSector, gruppo.Key, null, null, [$"AIP: {descritte}{dove}"]));
                continue;
            }

            var a = suo.Attesa;
            // Fra le attese dell'AIP di quel punto, la più somigliante a quella del sector.
            var vicina = aip.MinBy(h => (a.Rotta is { } r ? Math.Min(Math.Abs(h.InboundTrack - r), 360 - Math.Abs(h.InboundTrack - r)) : 999)
                                        + (a.Verso == (h.Turn == TurnDirection.Right ? 'R' : 'L') ? 0 : 100)
                                        + (Uguale(Quota(h.Minimum.Text), a.Quota) ? 0 : 10))!;
            var differenze = new List<string>();
            if (a.Rotta != vicina.InboundTrack) differenze.Add($"rotta d'avvicinamento AIP {vicina.InboundTrack:000}°, sector {a.Rotta:000}°");
            if (a.Verso != (vicina.Turn == TurnDirection.Right ? 'R' : 'L')) differenze.Add($"virata AIP {(vicina.Turn == TurnDirection.Right ? "R" : "L")}, sector {a.Verso}");
            if (!Uguale(Quota(vicina.Minimum.Text), a.Quota)) differenze.Add($"quota AIP {vicina.Minimum.Text}, sector {a.Quota}");
            var dettagli = new List<string> { $"sector: {a.Descrizione} · AIP: {descritte}" };
            dettagli.AddRange(differenze);
            esiti.Add(new DifferenzaDiEnr3(differenze.Count > 0 ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, gruppo.Key, file, suo.Record, dettagli));
        }
        foreach (var (fix, suo) in delSector.Where(s => !visti.Contains(s.Key)).OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
            esiti.Add(new DifferenzaDiEnr3(EsitoDelConfronto.SoloNelSector, fix, file, suo.Record,
                // Una sigla di tre lettere è una radioassistenza: le sue attese stanno in ENR 3.4.2, che non si legge ancora.
                fix.Length <= 3
                    ? [$"sector: {suo.Attesa.Descrizione}", "attesa su una radioassistenza: è in ENR 3.4.2, che il Lab non legge ancora"]
                    : [$"sector: {suo.Attesa.Descrizione}"]));

        return [.. esiti.OrderBy(e => Ordine(e.Esito)).ThenBy(e => e.Nome, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>La quota dell'AIP come la scrive il sector: <c>FL110</c>, <c>9000</c> (senza «only», «or TRL»).</summary>
    private static string Quota(string testo) => testo.Split(' ')[0];

    private static string Descrizione(Holding h) =>
        $"{h.InboundTrack:000}°{(h.Turn == TurnDirection.Right ? "R" : "L")} {h.Minimum.Text}{(h.Bearing is { } b ? $" ({b})" : "")}";
}

using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Cuore deterministico (puro, nessun IO) del calcolo confinanti: filtro dei confini ACC (CTR/FSS), adiacenza
/// geometrica domestici×esteri e aggregazione per coppia. Isolato da <see cref="NeighbourImportService"/> per
/// essere unit-testabile (doc refactor 05 §4.2). Non scarica nulla: opera su dati già forniti.
/// </summary>
public sealed class NeighbourAdjacencyComputer
{
    /// <summary>
    /// Solo le posizioni center (CTR) e flight service (FSS) definiscono i confini di un ACC. Le posizioni locali
    /// all'aeroporto (TWR, APP incl. <c>I_TWR</c>/<c>G_APP</c>, GND, DEL, ATIS…) NON sono confini ACC reali e vanno
    /// escluse dal calcolo di adiacenza. Filtro sul suffisso del callsign (dopo l'ultimo <c>_</c>).
    /// </summary>
    public static bool IsAccBoundaryPosition(string? composePosition)
    {
        if (string.IsNullOrWhiteSpace(composePosition)) return false;
        var i = composePosition.LastIndexOf('_');
        var suffix = (i >= 0 ? composePosition[(i + 1)..] : composePosition).Trim();
        return suffix.Equals("CTR", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("FSS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calcola le adiacenze tra i settori domestici e i subcenter degli ACC esteri, aggrega per coppia (Home,Foreign)
    /// e produce i candidati + il catalogo estero confinante da persistire. Puro: input → output, nessun side-effect.
    /// </summary>
    public NeighbourComputeResult ComputeImport(
        IReadOnlyList<DomesticSectorPoly> domestic,
        IReadOnlyList<ForeignAccData> foreign,
        double thresholdNm)
    {
        // Anelli domestici di confine, pre-parsati (scarta i non-confine e i non-parsabili).
        // ⚠️ UNA RIGA PER PEZZO: un settore di sette zone entra sette volte, e basta che UNA tocchi il
        // confinante perché la coppia esista. Con un anello solo, un vicino attaccato alla settima zona
        // sarebbe semplicemente mancato dall'elenco, senza nessun errore da nessuna parte.
        var domesticRings = domestic
            .Where(d => IsAccBoundaryPosition(d.ComposePosition))
            .SelectMany(d => d.Polygons.Select(poly => (d.CenterId, d.ComposePosition, Ring: PolygonGeometry.ToRing(poly))))
            .Where(x => x.Ring is not null)
            .ToList();

        var hits = new List<NeighbourHit>();
        var hitTuples = new List<(string Home, string HomeSect, string Foreign, string ForeignSect, string Name, string Country, double Dist, string? Poly)>();
        // Subcenter esteri risultati CONFINANTI (≥1 adiacenza): da persistire come catalogo AccSector.
        var confiningSubs = new Dictionary<string, (string Name, Dictionary<string, SourceSubcenter> Subs)>(StringComparer.OrdinalIgnoreCase);

        foreach (var fa in foreign)
        {
            foreach (var sub in fa.Subcenters)
            {
                if (!IsAccBoundaryPosition(sub.ComposePosition)) continue;   // TWR/APP/GND ecc. non sono confini ACC
                var fRing = PolygonGeometry.ToRing(sub.RegionMapPolygon);
                if (fRing is null) continue;   // senza poligono → non calcolabile qui (fallback manuale in UI)

                var subHit = false;
                // ⚠️ Due pezzi dello stesso settore che toccano lo stesso vicino sono UNA adiacenza, non due:
                // si tiene la distanza minore. Senza, un CTR di sette zone gonfierebbe l'elenco di sette
                // righe uguali — e il conteggio delle coppie, che è quel che si guarda, direbbe il falso.
                var vicine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var (homeCode, homeSect, homeRing) in domesticRings)
                {
                    if (!PolygonGeometry.AreAdjacent(homeRing, fRing, thresholdNm)) continue;
                    var dist = PolygonGeometry.MinEdgeDistanceNm(homeRing!.Points, fRing.Points);
                    var chiave = homeCode.ToUpperInvariant() + "|" + homeSect;
                    if (!vicine.TryGetValue(chiave, out var gia) || dist < gia) vicine[chiave] = dist;
                    subHit = true;
                }

                foreach (var (chiave, dist) in vicine)
                {
                    var pezzi = chiave.Split('|');
                    var home = pezzi[0];
                    var homeSect = pezzi[1];
                    hitTuples.Add((home, homeSect, fa.Code, sub.ComposePosition, fa.Name, fa.Country, dist, sub.RegionMapPolygon));
                    hits.Add(new NeighbourHit(home, homeSect, fa.Code, sub.ComposePosition, Math.Round(dist, 1)));
                }
                if (subHit)
                {
                    if (!confiningSubs.TryGetValue(fa.Code, out var entry))
                        confiningSubs[fa.Code] = entry = (fa.Name, new Dictionary<string, SourceSubcenter>(StringComparer.OrdinalIgnoreCase));
                    entry.Subs.TryAdd(sub.ComposePosition, sub);
                }
            }
        }

        // Catalogo ACC esteri confinanti (solo i subcenter con ≥1 adiacenza).
        var foreignCatalog = confiningSubs
            .Select(kv => new ForeignAccImport(kv.Key, kv.Value.Name, kv.Value.Subs.Values.ToList()))
            .ToList();

        // Aggregazione per coppia (Home, Foreign): min distanza + conteggio settori adiacenti.
        var agg = new Dictionary<(string Home, string Foreign), NeighbourPairAggregate>();
        foreach (var h in hitTuples)
        {
            var key = (h.Home, h.Foreign);
            if (!agg.TryGetValue(key, out var a))
                agg[key] = a = new NeighbourPairAggregate { ForeignName = h.Name, CountryId = h.Country };
            a.Count++;
            a.HomeSectors.Add(h.HomeSect);
            a.ForeignSectors.Add(h.ForeignSect);
            if (h.Dist < a.MinDist) { a.MinDist = h.Dist; a.BestForeignPolygon = h.Poly; }
        }

        var candidates = agg.Select(kv => new NeighbourCandidateUpsert(
            HomeAccCode: kv.Key.Home,
            ForeignAccCode: kv.Key.Foreign,
            ForeignAccName: kv.Value.ForeignName,
            CountryId: kv.Value.CountryId,
            ForeignRootCallsign: $"{kv.Key.Foreign}_CTR",
            RegionMapPolygon: kv.Value.BestForeignPolygon,
            MinDistanceNm: kv.Value.MinDist == double.MaxValue ? null : Math.Round(kv.Value.MinDist, 1),
            // Settori esteri distinti adiacenti — NON il numero di coppie settore×settore (che gonfia il conteggio).
            AdjacentSectorCount: kv.Value.ForeignSectors.Count,
            AdjacentHomeCallsigns: kv.Value.HomeSectors.ToList(),
            AdjacentForeignCallsigns: kv.Value.ForeignSectors.ToList())).ToList();

        return new NeighbourComputeResult(candidates, foreignCatalog, hits);
    }

    /// <summary>
    /// Calcola il dettaglio di verifica di una coppia (adiacenze settore↔settore + forme proiettate per la mappa).
    /// Puro: riceve i settori domestici (tutti) e i subcenter esteri già scaricati. <paramref name="seedWarnings"/>
    /// contiene eventuali warning a monte (es. fetch IVAO fallita), preservati nel risultato.
    /// </summary>
    public NeighbourPairDetail ComputePairDetail(
        string home, string foreign,
        IReadOnlyList<DomesticSectorPoly> allDomestic,
        IReadOnlyList<SourceSubcenter> foreignSubs,
        double thresholdNm,
        IReadOnlyList<string>? seedWarnings = null)
    {
        home = home.ToUpperInvariant();
        foreign = foreign.ToUpperInvariant();
        var warnings = new List<string>(seedWarnings ?? Array.Empty<string>());

        // Settori domestici (solo di questo ACC, solo confini CTR/FSS) col loro grezzo + Ring.
        // ⚠️ Una riga per PEZZO, come nel giro d'import: la mappa deve disegnare tutte le zone di un settore
        // agganciato, non la prima.
        var domestic = allDomestic
            .Where(d => string.Equals(d.CenterId, home, StringComparison.OrdinalIgnoreCase)
                        && IsAccBoundaryPosition(d.ComposePosition))
            .SelectMany(d => d.Polygons.Select(poly =>
                (Sect: d.ComposePosition, Raw: poly, Ring: PolygonGeometry.ToRing(poly))))
            .Where(x => x.Ring is not null)
            .ToList();

        // Subcenter esteri (già scaricati), stesso filtro.
        var foreignSects = foreignSubs
            .Where(s => IsAccBoundaryPosition(s.ComposePosition))
            .Select(s => (Sect: s.ComposePosition, Raw: s.RegionMapPolygon, Ring: PolygonGeometry.ToRing(s.RegionMapPolygon)))
            .Where(x => x.Ring is not null)
            .ToList();

        // Adiacenze settore↔settore.
        var usedHome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedForeign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Coppia settore↔settore → distanza minima fra i loro pezzi: due zone dello stesso settore che
        // toccano lo stesso vicino restano UNA riga.
        var perCoppia = new Dictionary<(string H, string F), double>();
        foreach (var f in foreignSects)
            foreach (var h in domestic)
            {
                if (!PolygonGeometry.AreAdjacent(h.Ring, f.Ring, thresholdNm)) continue;
                var dist = PolygonGeometry.MinEdgeDistanceNm(h.Ring!.Points, f.Ring!.Points);
                var chiave = (h.Sect, f.Sect);
                if (!perCoppia.TryGetValue(chiave, out var gia) || dist < gia) perCoppia[chiave] = dist;
                usedHome.Add(h.Sect);
                usedForeign.Add(f.Sect);
            }

        var adj = perCoppia
            .Select(kv => new NeighbourAdjacency(kv.Key.H, kv.Key.F, Math.Round(kv.Value, 1)))
            .OrderBy(a => a.DistanceNm).ThenBy(a => a.HomeSector).ToList();

        // Mappa: disegna i settori coinvolti nell'adiacenza; se nessuno confina, mostra tutti (per far vedere il perché).
        var homeShown = domestic.Where(d => usedHome.Contains(d.Sect)).ToList();
        if (homeShown.Count == 0) homeShown = domestic;
        var foreignShown = foreignSects.Where(f => usedForeign.Contains(f.Sect)).ToList();
        if (foreignShown.Count == 0) foreignShown = foreignSects;

        // Proiezione condivisa: prima i domestici, poi gli esteri (ordine preservato per indice).
        var raws = homeShown.Select(h => (string?)h.Raw).Concat(foreignShown.Select(f => (string?)f.Raw)).ToList();
        var proj = AorPolygonProjector.ProjectShared(raws);

        var homeShapes = new List<NeighbourMapShape>();
        var foreignShapes = new List<NeighbourMapShape>();
        if (proj is not null)
        {
            for (var i = 0; i < homeShown.Count; i++)
                if (proj.Polygons[i] is { } p) homeShapes.Add(new NeighbourMapShape(homeShown[i].Sect, p.Path, LatLng(homeShown[i].Raw)));
            for (var i = 0; i < foreignShown.Count; i++)
                if (proj.Polygons[homeShown.Count + i] is { } p) foreignShapes.Add(new NeighbourMapShape(foreignShown[i].Sect, p.Path, LatLng(foreignShown[i].Raw)));
        }
        if (foreignSects.Count == 0 && warnings.Count == 0)
            warnings.Add($"{foreign}: nessun settore CTR/FSS con poligono da IVAO (usa il fallback poligono manuale).");

        return new NeighbourPairDetail(home, foreign, adj, proj?.ViewBox, homeShapes, foreignShapes, warnings);
    }

    /// <summary>Punti [lat,lng] dell'anello grezzo, per la mappa Leaflet reale (lista vuota se non parsabile).</summary>
    private static IReadOnlyList<double[]> LatLng(string? raw) =>
        PolygonGeometry.ParsePoints(raw).Select(p => new[] { p.Lat, p.Lon }).ToList();
}

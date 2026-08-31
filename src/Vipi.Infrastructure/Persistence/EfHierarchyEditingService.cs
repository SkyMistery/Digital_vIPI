using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IHierarchyEditingService"/>
public sealed class EfHierarchyEditingService : IHierarchyEditingService
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly ISectorProjectionService _projection;
    private readonly NeighboursOptions _opt;
    private readonly DivisionOptions _division;

    public EfHierarchyEditingService(VipiDbContext db, IEditAuthorizationService authz,
        ISectorProjectionService projection, IOptions<NeighboursOptions> opt, IOptions<DivisionOptions> division)
    {
        _db = db;
        _authz = authz;
        _projection = projection;
        _opt = opt.Value;
        _division = division.Value;
    }

    /// <summary>Un ACC è ESTERO se il codice non inizia con un prefisso divisione. Regola pura in <see cref="HierarchyRules"/>.</summary>
    private bool IsForeignCode(string code) => HierarchyRules.IsForeignCode(code, _division.IcaoPrefixes);

    public async Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default)
    {
        var nodes = new List<HierarchyNode>();

        // Mappa ACC → prefisso nazione (per i padri per-nazione). Estero deciso dai prefissi divisione (non dal flag).
        var prefixByCode = await _db.Accs.AsNoTracking()
            .ToDictionaryAsync(a => a.Code, a => a.CountryPrefix, StringComparer.OrdinalIgnoreCase, ct);
        (bool F, string P) Meta(string code) =>
            (IsForeignCode(code), prefixByCode.TryGetValue(code, out var p) ? p : (code.Length >= 2 ? code[..2] : code));

        var accSectors = await _db.AccSectors.AsNoTracking()
            .OrderBy(s => s.CenterId).ThenBy(s => s.ComposePosition).ToListAsync(ct);
        foreach (var s in accSectors)
        {
            var (f, p) = Meta(s.CenterId);
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Acc, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.CenterId,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden, IsForeign: f, CountryPrefix: p));
        }

        // TUTTE le posizioni d'aeroporto, non solo gli APP: torre, ground e delivery hanno un padre di copertura
        // come gli altri e vanno modificabili qui. L'ATIS resta fuori (non è una posizione di controllo: la
        // proiezione lo esclude, quindi non è né un nodo né un padre possibile).
        var positions = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.Position == null || s.Position.ToUpper() != "ATIS")
            .OrderBy(s => s.AccCode).ThenBy(s => s.ComposePosition).ToListAsync(ct);

        // Stesso albero effettivo che valida l'anti-ciclo e che costruisce la proiezione: l'editor deve mostrare
        // il padre che il sistema usa davvero, non «da assegnare». ⚠️ Ricavarlo qui una seconda volta, a mano,
        // è precisamente il modo in cui i due alberi tornano a divergere.
        var effettivi = await EffectiveParentMapAsync(null, ct);

        foreach (var s in positions)
        {
            var (f, p) = Meta(s.AccCode);
            var derived = s.ParentCallsign is null ? effettivi.GetValueOrDefault(s.ComposePosition) : null;

            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.AirportPosition, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.AccCode,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden, IsForeign: f, CountryPrefix: p,
                DerivedParentCallsign: derived));
        }

        var airports = await _db.Airports.AsNoTracking().Include(a => a.Acc)
            .OrderBy(a => a.Icao).ToListAsync(ct);
        foreach (var a in airports)
        {
            var (f, p) = Meta(a.Acc?.Code ?? "");
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Airport, a.Id, Callsign: null,
                Label: string.IsNullOrWhiteSpace(a.Name) ? a.Icao : $"{a.Icao} — {a.Name}",
                AccCode: a.Acc?.Code ?? "", ParentCallsign: a.ParentCallsign, IsHidden: a.IsHidden, IsForeign: f, CountryPrefix: p));
        }

        return nodes;
    }

    // Cache del set confinanti (calcolo geometrico O(N²) su poligoni densi: costoso, cambia solo dopo import/edit
    // gerarchia). Static: condiviso tra richieste/utenti (il set è globale). TTL + invalidazione esplicita su SetParent.
    private static readonly object _confiningLock = new();
    private static IReadOnlySet<string>? _confiningCache;
    private static DateTime _confiningCachedAt;
    private static readonly TimeSpan _confiningTtl = TimeSpan.FromMinutes(5);
    /// <summary>Svuota la cache del set confinanti. Chiamata da <see cref="EfSectorProjectionService"/> a fine sync
    /// (choke point di ogni mutazione catalogo) così il set non resta stantio fino al TTL dopo un import/hide.</summary>
    internal static void InvalidateConfiningCache() { lock (_confiningLock) _confiningCache = null; }

    public async Task<IReadOnlySet<string>> ListConfiningForeignCallsignsAsync(CancellationToken ct = default)
    {
        lock (_confiningLock)
            if (_confiningCache is not null && DateTime.UtcNow - _confiningCachedAt < _confiningTtl)
                return _confiningCache;

        var set = await ComputeConfiningForeignCallsignsAsync(ct);
        lock (_confiningLock) { _confiningCache = set; _confiningCachedAt = DateTime.UtcNow; }
        return set;
    }

    private async Task<IReadOnlySet<string>> ComputeConfiningForeignCallsignsAsync(CancellationToken ct = default)
    {
        var all = await _db.AccSectors.AsNoTracking()
            .Where(s => !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.CenterId, s.RegionMapPolygon }).ToListAsync(ct);
        var domesticPolygons = all.Where(s => !IsForeignCode(s.CenterId)).Select(s => s.RegionMapPolygon!).ToList();
        var foreignSectors = all.Where(s => IsForeignCode(s.CenterId))
            .Select(s => (s.ComposePosition, (string?)s.RegionMapPolygon)).ToList();

        // Adiacenza estero↔domestico: regola pura testata.
        return HierarchyRules.ComputeConfiningForeignCallsigns(domesticPolygons, foreignSectors, _opt.AdjacencyThresholdNm);
    }

    public async Task SetParentAsync(HierarchyNodeKind kind, int nodeId, string? parentCallsign, CancellationToken ct = default)
    {
        parentCallsign = string.IsNullOrWhiteSpace(parentCallsign) ? null : parentCallsign.Trim();

        // 1. Risolvi il nodo figlio + il suo ACC (per l'autorizzazione) + il suo callsign (per l'anti-ciclo).
        string childAccCode;
        string? childCallsign;
        switch (kind)
        {
            case HierarchyNodeKind.Acc:
            {
                var e = await _db.AccSectors.FirstOrDefaultAsync(s => s.Id == nodeId, ct)
                    ?? throw new ValidationException(Lingua("Settore ACC inesistente.", "The ACC sector does not exist."));
                childAccCode = e.CenterId; childCallsign = e.ComposePosition;
                break;
            }
            case HierarchyNodeKind.AirportPosition:
            {
                var e = await _db.AirportSectors.FirstOrDefaultAsync(s => s.Id == nodeId, ct)
                    ?? throw new ValidationException(Lingua("Posizione APP inesistente.", "The APP position does not exist."));
                childAccCode = e.AccCode; childCallsign = e.ComposePosition;
                break;
            }
            case HierarchyNodeKind.Airport:
            {
                var e = await _db.Airports.Include(a => a.Acc).FirstOrDefaultAsync(a => a.Id == nodeId, ct)
                    ?? throw new ValidationException(Lingua("Aeroporto inesistente.", "The airport does not exist."));
                childAccCode = e.Acc?.Code ?? throw new ValidationException(Lingua("Aeroporto senza ACC.", "The airport has no ACC."));
                childCallsign = null;   // foglia: non referenziabile come padre
                break;
            }
            default:
                throw new ValidationException(Lingua("Tipo di nodo non valido.", "Invalid node type."));
        }

        // Gli ACC esteri (confinanti) non hanno grant per-ACC: l'editing della loro gerarchia è riservato agli admin.
        var isForeign = await _db.Accs.AsNoTracking()
            .Where(a => a.Code == childAccCode).Select(a => a.IsForeign).FirstOrDefaultAsync(ct);
        if (isForeign) _authz.EnsureAtLeast(VipiRole.Editor);
        else _authz.EnsureAtLeast(VipiRole.Editor);

        // 2. Valida il padre: dev'essere un nodo interno (ACC o APP) esistente; anti-ciclo per i nodi interni.
        if (parentCallsign is not null)
        {
            var internalParents = await InternalNodeParentMapAsync(ct);   // callsign → ParentCallsign
            if (!internalParents.ContainsKey(parentCallsign))
                throw new ValidationException(Lingua($"Il padre «{parentCallsign}» non è un settore ACC o APP valido.", $"The parent «{parentCallsign}» is not a valid ACC or APP sector."));

            if (childCallsign is not null)
            {
                if (string.Equals(parentCallsign, childCallsign, StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException(Lingua("Un nodo non può essere padre di sé stesso.", "A node cannot be its own parent."));
                await EnsureParentIsNotLowerAsync(childCallsign, parentCallsign, ct);
            }
        }

        // 2-bis. Anti-ciclo sull'albero EFFETTIVO, e SEMPRE — anche quando `parentCallsign` è null.
        //
        // ⚠️ Questo controllo stava dentro il blocco qui sopra, e guardava i soli padri SCRITTI. Due buchi in
        // uno, ed è la coppia che ha prodotto in produzione un settore nipote di sé stesso (LIMF, 31 agosto
        // 2026): scegliere «eredita» — cioè scrivere null — non passava da nessun controllo, e il padre che
        // ne nasceva era quello DERIVATO dalla scaletta, che quella mappa non conteneva.
        //
        // Si valida una SIMULAZIONE e non lo stato attuale, perché azzerare un padre scritto cambia anche il
        // padre derivato di ALTRE posizioni dello stesso scalo: `PickOnRung` sceglie la radice del gruppo
        // guardando proprio i padri scritti. Controllare il nodo toccato su una mappa vecchia direbbe di sì a
        // un anello che si chiude fra due sorelle.
        // Si rifiutano gli anelli che questa modifica CREA, non quelli che già esistono: in produzione un
        // anello c'è, e una guardia che rifiutasse ogni albero ciclico impedirebbe di ripararlo — la pagina
        // Struttura è l'unico posto da cui si scioglie.
        var dopo = await EffectiveParentMapAsync(new ModificaInSospeso(kind, nodeId, parentCallsign), ct);
        var anelliDopo = HierarchyRules.FindAllCycles(dopo);
        if (anelliDopo.Count > 0)
        {
            var giaNoti = HierarchyRules.FindAllCycles(await EffectiveParentMapAsync(null, ct))
                .Select(FirmaAnello).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var creato = anelliDopo.FirstOrDefault(a => !giaNoti.Contains(FirmaAnello(a)));
            if (creato is not null) HierarchyRules.EnsureNoCycle(creato[0], dopo);
        }

        // 3. Scrivi il ParentCallsign sull'entità giusta, e registra il cambio (da → a).
        string? padrePrima;
        var etichetta = childCallsign;   // per un aeroporto (foglia, senza callsign) l'etichetta è l'ICAO
        switch (kind)
        {
            case HierarchyNodeKind.Acc:
            {
                var e = await _db.AccSectors.FirstAsync(s => s.Id == nodeId, ct);
                padrePrima = e.ParentCallsign; e.ParentCallsign = parentCallsign;
                break;
            }
            case HierarchyNodeKind.AirportPosition:
            {
                var e = await _db.AirportSectors.FirstAsync(s => s.Id == nodeId, ct);
                padrePrima = e.ParentCallsign; e.ParentCallsign = parentCallsign;
                break;
            }
            case HierarchyNodeKind.Airport:
            {
                var e = await _db.Airports.FirstAsync(a => a.Id == nodeId, ct);
                padrePrima = e.ParentCallsign; e.ParentCallsign = parentCallsign; etichetta = e.Icao;
                break;
            }
            default:
                padrePrima = null;
                break;
        }

        // ⚠️ HierarchyChange è stato per due anni un valore d'enum che nessuno scriveva, mentre il sottotitolo
        // della pagina Audit prometteva «pubblicazioni, permessi, STRUTTURA». Chi cambia il padre di un settore
        // sposta traffico: è il tipo di modifica che, quando qualcosa non torna, si vuole poter ricostruire.
        // Il non-evento non si scrive: rimettere lo stesso padre non è un cambio.
        if (!string.Equals(padrePrima, parentCallsign, StringComparison.OrdinalIgnoreCase))
        {
            AuditScribe.Write(_db, _authz.CurrentUserId ?? 0, AuditAction.HierarchyChange, kind.ToString(),
                nodeId.ToString(),
                new { Nodo = etichetta, Acc = childAccCode, Da = padrePrima, A = parentCallsign });
        }

        await _db.SaveChangesAsync(ct);

        // 4. Riproietta i Sector operativi (l'albero AoR deriva da qui). La riproiezione invalida essa stessa la
        //    cache del set confinanti (vedi InvalidateConfiningCache in EfSectorProjectionService), quindi qui non
        //    serve un'invalidazione esplicita.
        await _projection.SyncFromCatalogsAsync(ct);
    }

    /// <summary>
    /// Il padre non può stare più IN BASSO del figlio nella scaletta d'aeroporto: un ground non copre una torre.
    /// Nella gerarchia il padre è chi ti assorbe quando chiudi, quindi al livello di un ground finirebbe traffico
    /// che quel ground non può gestire — e la vIPI direbbe a un controllore di rilasciare alla posizione sbagliata.
    /// Il picker del padre è un elenco lungo e piatto: è un errore da click, non da intenzione.
    ///
    /// Pari grado ammesso (<c>LIRF_E_TWR</c> sotto <c>LIRF_TWR</c>, gli split) e ogni salita.
    /// I settori d'area (CTR/FSS) stanno fuori dalla scaletta (gradino 0), quindi restano padri validi di tutto.
    /// </summary>
    private async Task EnsureParentIsNotLowerAsync(string childCallsign, string parentCallsign, CancellationToken ct)
    {
        var rungs = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == childCallsign || s.ComposePosition == parentCallsign)
            .Select(s => new { s.ComposePosition, s.Position })
            .ToListAsync(ct);

        int RungOf(string callsign) => rungs
            .Where(r => string.Equals(r.ComposePosition, callsign, StringComparison.OrdinalIgnoreCase))
            .Select(r => AirportPositionLadder.Rung(EffectiveHierarchy.TypeOfPosition(r.Position)))
            .DefaultIfEmpty(0)   // non è una posizione d'aeroporto ⇒ settore d'area, in cima
            .First();

        var childRung = RungOf(childCallsign);
        var parentRung = RungOf(parentCallsign);
        if (parentRung > childRung)
            throw new ValidationException(Lingua(
                $"«{parentCallsign}» non può coprire «{childCallsign}»: sta più in basso nella scaletta " +
                "dell'aeroporto (DEL → GND → TWR → APP).",
                $"«{parentCallsign}» cannot cover «{childCallsign}»: it sits lower on the airport ladder " +
                "(DEL → GND → TWR → APP)."));
    }


    /// <summary>Una modifica non ancora salvata, da applicare in memoria prima di validare.</summary>
    private sealed record ModificaInSospeso(HierarchyNodeKind Kind, int NodeId, string? Parent);

    /// <summary>
    /// Firma canonica di un anello, indipendente dal nodo da cui lo si è imboccato: si ruota l'elenco sul
    /// callsign minore. Senza, lo stesso anello letto da due nodi diversi sembrerebbe due anelli, e la
    /// guardia direbbe «ne hai creato uno nuovo» su uno che c'era già.
    /// </summary>
    private static string FirmaAnello(IReadOnlyList<string> anello)
    {
        var normalizzato = anello.Select(c => c.ToUpperInvariant()).ToList();
        var perno = normalizzato.IndexOf(normalizzato.Min()!);
        return string.Join("→", normalizzato.Skip(perno).Concat(normalizzato.Take(perno)));
    }

    /// <summary>
    /// Mappa callsign → padre <b>EFFETTIVO</b> di ogni nodo interno: quello scritto se c'è, altrimenti quello
    /// derivato dalla scaletta d'aeroporto. È l'albero che leggono davvero la proiezione, la ricaduta dei
    /// trasferimenti e la pagina Struttura — e quindi l'unico su cui abbia senso cercare un anello.
    ///
    /// <para><paramref name="modifica"/> applica in memoria un cambio non ancora salvato (<c>null</c> = stato
    /// attuale). Serve perché azzerare un padre scritto non cambia solo quel nodo: <c>PickOnRung</c> sceglie
    /// la radice del gruppo guardando i padri scritti delle sorelle, quindi la derivazione di ALTRE posizioni
    /// dello stesso scalo si sposta. Validare il solo nodo toccato, su una mappa vecchia, direbbe di sì a un
    /// anello che si chiude fra due sorelle.</para>
    /// </summary>
    private async Task<Dictionary<string, string?>> EffectiveParentMapAsync(
        ModificaInSospeso? modifica, CancellationToken ct)
    {
        string? PadreDi(HierarchyNodeKind kind, int id, string? scritto) =>
            modifica is not null && modifica.Kind == kind && modifica.NodeId == id ? modifica.Parent : scritto;

        var righe = new List<HierarchyCatalogRow>();

        foreach (var s in await _db.AccSectors.AsNoTracking()
                     .Select(s => new { s.Id, s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            righe.Add(new HierarchyCatalogRow(s.ComposePosition,
                PadreDi(HierarchyNodeKind.Acc, s.Id, s.ParentCallsign), null, SectorType.Ctr, IsHidden: false));

        // Le stesse righe di LoadTreeAsync: l'ATIS non è una posizione di controllo e non è un nodo.
        foreach (var s in await _db.AirportSectors.AsNoTracking()
                     .Where(s => s.Position == null || s.Position.ToUpper() != "ATIS")
                     .Select(s => new { s.Id, s.ComposePosition, s.AirportIcao, s.Position, s.ParentCallsign, s.IsHidden })
                     .ToListAsync(ct))
            righe.Add(new HierarchyCatalogRow(s.ComposePosition,
                PadreDi(HierarchyNodeKind.AirportPosition, s.Id, s.ParentCallsign),
                s.AirportIcao, EffectiveHierarchy.TypeOfPosition(s.Position), s.IsHidden));

        var padreScalo = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in await _db.Airports.AsNoTracking()
                     .Select(a => new { a.Id, a.Icao, a.ParentCallsign }).ToListAsync(ct))
            padreScalo[a.Icao] = PadreDi(HierarchyNodeKind.Airport, a.Id, a.ParentCallsign);

        return EffectiveHierarchy.ParentMap(righe, padreScalo);
    }

    /// <summary>Mappa callsign → ParentCallsign per i nodi interni (settori ACC + posizioni d'aeroporto).</summary>
    private async Task<Dictionary<string, string?>> InternalNodeParentMapAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.AccSectors.AsNoTracking()
                     .Select(s => new { s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            map[s.ComposePosition] = s.ParentCallsign;
        foreach (var s in await _db.AirportSectors.AsNoTracking()
                     .Where(s => s.Position == null || s.Position.ToUpper() != "ATIS")
                     .Select(s => new { s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            map[s.ComposePosition] = s.ParentCallsign;
        return map;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Impl. EF di <see cref="IDocumentImpactRepository"/>. Il cuore è il <b>reverse-lookup</b> settore →
/// documenti: dato un callsign che è cambiato, quali documenti lo raccontano.
///
/// <para>⚠️ <b>Riscritto il 25 agosto 2026</b> (carta «documenti da rivedere» §8, slice 0). La versione
/// precedente prendeva, dentro l'ACC del settore, <c>IsPrimary || Type == App || Callsign == X</c>: cioè
/// <b>ogni</b> documento primario e <b>ogni</b> APP dell'ACC, che il settore c'entrasse o no — nascondere
/// <c>LIRF_GND</c> segnalava anche Bologna, Napoli e Pisa. E allo stesso tempo <b>non</b> guardava
/// <see cref="Vipi.Domain.Entities.Airport.DocumentId"/>, che dal 25 agosto è il legame autoritativo del
/// documento d'aeroporto: uno scalo come LIBG — su IVAO ha solo un APP non remotizzato, e quello viene
/// perfino sganciato dal documento dello scalo — non produceva <b>nessuna</b> riga. Con un solo chiamante
/// manuale il difetto era tollerabile; come base di una casella alimentata da più rivelatori sarebbe rumore
/// da una parte e silenzio dall'altra.</para>
///
/// <para>La regola nuova è per <b>legame dimostrabile</b>, in sei passi (§6 della carta). Ogni passo è una
/// domanda a cui il database sa rispondere, non una somiglianza.</para>
/// </summary>
public sealed class EfDocumentImpactRepository : IDocumentImpactRepository
{
    private readonly VipiDbContext _db;
    public EfDocumentImpactRepository(VipiDbContext db) => _db = db;

    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <summary>Suffissi che pesano sulla <b>sezionazione dell'ACC</b>, e quindi sul documento ACC-wide: i settori
    /// d'area e gli avvicinamenti (che nella vIPI ACC sono i «gruppi APP»). Una torre o un ground non ci entrano:
    /// segnalarli lì vorrebbe dire riaprire la vIPI di Roma perché a Pisa è cambiato il GND.</summary>
    private static bool PesaSullAcc(string? position) =>
        (position ?? "").Trim().ToUpperInvariant() is "CTR" or "FSS" or "APP" or "DEP";

    public async Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSectorAsync(
        string composePosition, string accCode, CancellationToken ct = default)
    {
        var cs = (composePosition ?? "").Trim();
        var acc = (accCode ?? "").Trim();
        if (cs.Length == 0) return Array.Empty<AffectedDoc>();

        var ids = new HashSet<int>();

        // 1+2) Il documento che descrive il settore stesso, e — se è una posizione d'aeroporto — quello dello SCALO.
        foreach (var id in await DocsForCallsignsAsync(new[] { cs }, ct)) ids.Add(id);

        // 3) La vIPI ACC del centro di competenza, ma solo se il settore pesa sulla sezionazione (vedi PesaSullAcc).
        var position = await PositionOfAsync(cs, ct);
        if (acc.Length > 0 && PesaSullAcc(position))
            foreach (var id in await AccWideDocsAsync(acc, ct)) ids.Add(id);

        // 4) I vicini nella catena di copertura: il padre dichiarato e i figli diretti (cross-catalogo, cross-ACC).
        //    Un trasferimento si racconta da entrambi i lati: se sparisce il figlio, il documento del padre ha una
        //    consegna che non esiste più — ed è esattamente ciò che l'editore deve rileggere.
        var vicini = await VicinatoAsync(cs, ct);
        if (vicini.Count > 0)
            foreach (var id in await DocsForCallsignsAsync(vicini, ct)) ids.Add(id);

        // 5) Le citazioni dirette: chi nomina questo settore per Id.
        foreach (var id in await CitazioniDiretteAsync(cs, ct)) ids.Add(id);

        // 6) vLOA confinanti: il callsign è fra i settori domestici confinanti di una coppia con vLOA generata.
        foreach (var id in await VloaPerConfineAsync(cs, acc, ct)) ids.Add(id);

        if (ids.Count == 0) return Array.Empty<AffectedDoc>();

        return await _db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new AffectedDoc(d.Id, d.Title))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Documenti che descrivono i callsign dati: quello del <b>settore</b> (<c>Sector.DocumentId</c>, per ACC, APP
    /// e posizioni d'aeroporto) e quello dello <b>scalo</b> (<c>Airport.DocumentId</c>) quando il callsign è una
    /// posizione d'aeroporto.
    /// <para>⚠️ I settori <b>disattivati</b> entrano di proposito: dal 25 agosto la proiezione non recide più il
    /// legame quando un callsign sparisce, e un settore sparito è il soggetto della segnalazione, non un escluso.</para>
    /// </summary>
    private async Task<IReadOnlyList<int>> DocsForCallsignsAsync(
        IReadOnlyCollection<string> callsigns, CancellationToken ct)
    {
        var list = callsigns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(OIC).ToList();
        if (list.Count == 0) return Array.Empty<int>();

        var ids = await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId != null && list.Contains(s.Callsign))
            .Select(s => s.DocumentId!.Value)
            .ToListAsync(ct);

        var icaos = await _db.AirportSectors.AsNoTracking()
            .Where(s => list.Contains(s.ComposePosition))
            .Select(s => s.AirportIcao)
            .Distinct()
            .ToListAsync(ct);
        if (icaos.Count > 0)
            ids.AddRange(await _db.Airports.AsNoTracking()
                .Where(a => a.DocumentId != null && icaos.Contains(a.Icao))
                .Select(a => a.DocumentId!.Value)
                .ToListAsync(ct));

        return ids;
    }

    /// <summary>
    /// Suffisso di posizione del callsign, dai due cataloghi.
    /// <para>⚠️ Con un <b>ripiego sul settore proiettato</b>, e non è un di più: il caso in cui questa domanda
    /// conta di più è proprio quello in cui la riga di catalogo <b>non c'è più</b> — il callsign è sparito, ed
    /// è per questo che stiamo segnalando. Senza ripiego la posizione tornerebbe null, «non pesa sull'ACC»
    /// varrebbe sempre, e la vIPI ACC — il documento che più di tutti racconta quel settore — non verrebbe
    /// avvisata proprio quando il settore sparisce.</para>
    /// </summary>
    private async Task<string?> PositionOfAsync(string callsign, CancellationToken ct)
    {
        var daCatalogo =
            await _db.AccSectors.AsNoTracking().Where(s => s.ComposePosition == callsign).Select(s => s.Position)
                .FirstOrDefaultAsync(ct)
            ?? await _db.AirportSectors.AsNoTracking().Where(s => s.ComposePosition == callsign).Select(s => s.Position)
                .FirstOrDefaultAsync(ct);
        if (daCatalogo is not null) return daCatalogo;

        var tipo = await _db.Sectors.AsNoTracking().Where(s => s.Callsign == callsign)
            .Select(s => (SectorType?)s.Type).FirstOrDefaultAsync(ct);
        return tipo switch
        {
            SectorType.Ctr => "CTR",
            SectorType.App => "APP",
            SectorType.Twr or SectorType.ITwr => "TWR",
            SectorType.Gnd => "GND",
            SectorType.Del => "DEL",
            _ => null,
        };
    }

    /// <summary>
    /// Documenti vIPI ACC-wide del centro: quelli dei settori CTR <b>radice</b> con documento. Stesso criterio di
    /// <c>AccVipiReleaseTarget</c> e <c>EfAccDerivationRepository</c> — un ACC può avere più alberi, e ognuno ha
    /// il suo documento.
    /// </summary>
    private async Task<IReadOnlyList<int>> AccWideDocsAsync(string accCode, CancellationToken ct) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr
                        && s.ParentSectorId == null && s.DocumentId != null)
            .Select(s => s.DocumentId!.Value)
            .Distinct()
            .ToListAsync(ct);

    /// <summary>Padre dichiarato e figli diretti del callsign, dai due cataloghi più il nodo aeroporto
    /// (<c>Airport.ParentCallsign</c>): la catena di copertura vive per callsign, non per Id.</summary>
    private async Task<IReadOnlyList<string>> VicinatoAsync(string callsign, CancellationToken ct)
    {
        var vicini = new List<string>();

        var padreAcc = await _db.AccSectors.AsNoTracking()
            .Where(s => s.ComposePosition == callsign).Select(s => s.ParentCallsign).FirstOrDefaultAsync(ct);
        var padreApt = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == callsign).Select(s => s.ParentCallsign).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(padreAcc)) vicini.Add(padreAcc!);
        if (!string.IsNullOrWhiteSpace(padreApt)) vicini.Add(padreApt!);

        vicini.AddRange(await _db.AccSectors.AsNoTracking()
            .Where(s => s.ParentCallsign == callsign).Select(s => s.ComposePosition).ToListAsync(ct));
        vicini.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ParentCallsign == callsign).Select(s => s.ComposePosition).ToListAsync(ct));

        // Gli aeroporti agganciati a questo callsign: il nodo dell'albero è l'AEROPORTO, e le sue posizioni
        // ereditano il padre da lì (scaletta DEL→GND→TWR→APP). Il documento impattato è quello dello scalo.
        var icaoFigli = await _db.Airports.AsNoTracking()
            .Where(a => a.ParentCallsign == callsign && a.DocumentId != null)
            .Select(a => a.Icao).ToListAsync(ct);
        if (icaoFigli.Count > 0)
            vicini.AddRange(await _db.AirportSectors.AsNoTracking()
                .Where(s => icaoFigli.Contains(s.AirportIcao))
                .Select(s => s.ComposePosition).ToListAsync(ct));

        return vicini;
    }

    /// <summary>
    /// Chi nomina il settore <b>per Id</b>: le frequenze linkate di un aeroporto (<c>AirportFrequencyLink</c>), i
    /// blocchi di contenuto che vi si riferiscono (scope/da/a) e le parti di un documento (vLOA). Sono i legami
    /// che l'editore ha creato a mano: se il settore cambia, quelle righe raccontano qualcosa che non c'è più.
    /// </summary>
    private async Task<IReadOnlyList<int>> CitazioniDiretteAsync(string callsign, CancellationToken ct)
    {
        var sectorIds = await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == callsign).Select(s => s.Id).ToListAsync(ct);
        if (sectorIds.Count == 0) return Array.Empty<int>();

        var ids = new List<int>();

        // Frequenze linkate: la riga sta su un aeroporto → il documento è quello dello scalo.
        var icaos = await _db.AirportFrequencyLinks.AsNoTracking()
            .Where(l => sectorIds.Contains(l.SourceSectorId))
            .Select(l => l.Airport!.Icao)
            .Distinct().ToListAsync(ct);
        if (icaos.Count > 0)
            ids.AddRange(await _db.Airports.AsNoTracking()
                .Where(a => a.DocumentId != null && icaos.Contains(a.Icao))
                .Select(a => a.DocumentId!.Value).ToListAsync(ct));

        // Blocchi: scope, sorgente e destinazione di un coordinamento. Il documento è quello della versione.
        ids.AddRange(await _db.ContentBlocks.AsNoTracking()
            .Where(b => (b.ScopeSectorId != null && sectorIds.Contains(b.ScopeSectorId.Value))
                        || (b.FromSectorId != null && sectorIds.Contains(b.FromSectorId.Value))
                        || (b.ToSectorId != null && sectorIds.Contains(b.ToSectorId.Value)))
            .Select(b => b.DocumentVersion!.DocumentId)
            .Distinct().ToListAsync(ct));

        // Parti: è così che una vLOA dichiara i due lati.
        ids.AddRange(await _db.DocumentParties.AsNoTracking()
            .Where(p => sectorIds.Contains(p.SectorId))
            .Select(p => p.DocumentId)
            .Distinct().ToListAsync(ct));

        return ids;
    }

    /// <summary>vLOA confermate della ACC home il cui elenco di settori domestici confinanti contiene il callsign.
    /// L'elenco è JSON su <c>NeighbourCandidate</c>, quindi il filtro finale è in memoria (poche righe).</summary>
    private async Task<IReadOnlyList<int>> VloaPerConfineAsync(string callsign, string accCode, CancellationToken ct)
    {
        var cands = await _db.NeighbourCandidates.AsNoTracking()
            .Where(c => c.VloaDocumentId != null && c.AdjacentHomeCallsigns != null
                        && (accCode.Length == 0 || c.HomeAccCode == accCode))
            .Select(c => new { c.VloaDocumentId, c.AdjacentHomeCallsigns })
            .ToListAsync(ct);

        var ids = new List<int>();
        foreach (var c in cands)
        {
            List<string>? list;
            try { list = JsonSerializer.Deserialize<List<string>>(c.AdjacentHomeCallsigns!); }
            catch (JsonException) { list = null; }
            if (list is not null && list.Any(x => OIC.Equals(x, callsign))) ids.Add(c.VloaDocumentId!.Value);
        }
        return ids;
    }

    /// <summary>
    /// ACC del documento, per autorizzare chi scioglie la revisione. <b>Tre strade</b>, e servono tutte e tre:
    /// il settore descritto (vIPI ACC e APP), l'<b>aeroporto</b> descritto (dal 25 agosto il legame autoritativo
    /// della vIPI d'aeroporto) e la <b>parte Home</b> (vLOA, che i settori non li lega affatto).
    ///
    /// <para>⚠️ Prima del 25 agosto c'era solo la prima: per una vLOA tornava null, e il chiamante — che
    /// controllava il permesso solo <c>if (acc is not null)</c> — <b>saltava l'autorizzazione</b>. Le vLOA sono
    /// fra i documenti che questo stesso repository segnala, quindi il buco era raggiungibile.</para>
    /// </summary>
    public async Task<string?> GetDocAccCodeAsync(int documentId, CancellationToken ct = default)
    {
        var daSettore = await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId == documentId && s.Acc != null)
            .OrderByDescending(s => s.IsPrimary)
            .Select(s => s.Acc!.Code)
            .FirstOrDefaultAsync(ct);
        if (daSettore is not null) return daSettore;

        var daAeroporto = await _db.Airports.AsNoTracking()
            .Where(a => a.DocumentId == documentId && a.Acc != null)
            .Select(a => a.Acc!.Code)
            .FirstOrDefaultAsync(ct);
        if (daAeroporto is not null) return daAeroporto;

        return await _db.DocumentParties.AsNoTracking()
            .Where(p => p.DocumentId == documentId && p.Role == PartyRole.Home
                        && p.Sector != null && p.Sector.Acc != null)
            .Select(p => p.Sector!.Acc!.Code)
            .FirstOrDefaultAsync(ct);
    }

    // =============================================================================================
    //  Aree regolamentate: chi cita quest'area?
    // =============================================================================================

    /// <summary>
    /// Documenti la cui sezione <c>regulated</c> mostra l'area indicata. Due modi di citarla, e valgono
    /// entrambi: gli <b>id espliciti</b> (selezione manuale, piu' le extra di altri ACC) e l'<b>automatico</b>
    /// (<c>OwnAuto</c>), che vuol dire «tutte le aree del mio ACC» — quindi un'area del proprio ACC entra nel
    /// documento senza che il suo id sia scritto da nessuna parte.
    ///
    /// <para>Si guarda la <b>versione di lavoro</b> di ogni documento (bozza piu' recente, altrimenti la
    /// pubblicata, altrimenti l'ultima): le versioni storiche sono congelate per definizione, e segnalarle
    /// sarebbe rumore su qualcosa che nessuno puo' piu' correggere. Stessa regola del report di consistenza.</para>
    /// </summary>
    public async Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSpecialAreaAsync(
        string ivaoId, CancellationToken ct = default)
    {
        var id = (ivaoId ?? "").Trim();
        if (id.Length == 0) return Array.Empty<AffectedDoc>();

        var versioniDiLavoro = await VersioniDiLavoroAsync(ct);        // versionId -> documentId
        if (versioniDiLavoro.Count == 0) return Array.Empty<AffectedDoc>();

        var versionIds = versioniDiLavoro.Keys.ToList();
        var righe = await (
            from sec in _db.DocumentSections.AsNoTracking()
            where sec.SectionKey == "regulated" && versionIds.Contains(sec.DocumentVersionId)
            select new
            {
                sec.DocumentVersionId,
                Json = _db.ContentBlocks.AsNoTracking()
                    .Where(b => b.SectionId == sec.Id).OrderBy(b => b.Order)
                    .Select(b => b.BodyJson).FirstOrDefault(),
            }).ToListAsync(ct);
        if (righe.Count == 0) return Array.Empty<AffectedDoc>();

        // Gli ACC che elencano quest'area: servono per i documenti in automatico.
        var accDellArea = (await _db.SpecialAreaCenters.AsNoTracking()
            .Where(l => l.IvaoId == id).Select(l => l.CenterId).ToListAsync(ct))
            .ToHashSet(OIC);

        var ids = new HashSet<int>();
        foreach (var r in righe)
        {
            if (!versioniDiLavoro.TryGetValue(r.DocumentVersionId, out var docId)) continue;
            var sel = RegulatedSelectionJson.Parse(r.Json);

            if (sel.OwnIds.Concat(sel.ExtraIds).Any(x => OIC.Equals(x?.Trim(), id))) { ids.Add(docId); continue; }

            if (sel.OwnAuto && accDellArea.Count > 0)
            {
                var acc = await GetDocAccCodeAsync(docId, ct);
                if (acc is not null && accDellArea.Contains(acc)) ids.Add(docId);
            }
        }
        if (ids.Count == 0) return Array.Empty<AffectedDoc>();

        return await _db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new AffectedDoc(d.Id, d.Title))
            .ToListAsync(ct);
    }

    /// <summary>Versione di lavoro di ogni documento (bozza piu' recente, altrimenti la pubblicata corrente,
    /// altrimenti l'ultima): <c>versionId -> documentId</c>.</summary>
    private async Task<Dictionary<int, int>> VersioniDiLavoroAsync(CancellationToken ct)
    {
        var docs = await _db.Documents.AsNoTracking().Select(d => new { d.Id, d.CurrentVersionId }).ToListAsync(ct);
        var versions = await _db.DocumentVersions.AsNoTracking()
            .Select(v => new { v.Id, v.DocumentId, v.VersionNumber, v.Status }).ToListAsync(ct);

        var map = new Dictionary<int, int>();
        foreach (var d in docs)
        {
            var draft = versions.Where(v => v.DocumentId == d.Id && v.Status == DocumentStatus.Draft)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            var last = versions.Where(v => v.DocumentId == d.Id)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            if ((draft ?? d.CurrentVersionId ?? last) is int id) map[id] = d.Id;
        }
        return map;
    }

    // =============================================================================================
    //  La casella
    // =============================================================================================

    /// <summary>
    /// Apre l'impatto, o ritorna quello gia' aperto con la stessa chiave. ⚠️ Il controllo «esiste gia'?» non
    /// basta da solo: gli impatti si aprono da dentro la proiezione, che ha tredici chiamanti e gira anche
    /// mentre il giro notturno lavora. Chi arriva secondo sbatte sull'<b>indice unico</b>, e a quel punto la
    /// riga dell'altro c'e' gia': si rilegge e si torna quella. E' l'unico modo per non contare sulla fortuna.
    /// </summary>
    public async Task<int> RaiseAsync(RaiseImpactInput input, CancellationToken ct = default)
    {
        var sourceKey = (input.SourceKey ?? "").Trim();

        var esistente = await _db.DocumentImpacts
            .Where(i => i.DocumentId == input.DocumentId && i.Kind == input.Kind
                        && i.SourceKey == sourceKey && i.ClearedUtc == DocumentImpact.Aperto)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(ct);
        if (esistente is int id) return id;

        var riga = new DocumentImpact
        {
            DocumentId = input.DocumentId,
            Kind = input.Kind,
            SourceKey = sourceKey,
            ReasonKey = input.ReasonKey,
            ReasonArgsJson = input.ReasonArgs is { Count: > 0 } ? JsonSerializer.Serialize(input.ReasonArgs) : null,
            IsPublicNow = input.IsPublicNow,
            RaisedUtc = DateTime.UtcNow,
            ClearedUtc = DocumentImpact.Aperto,
        };
        _db.DocumentImpacts.Add(riga);
        try
        {
            await _db.SaveChangesAsync(ct);
            return riga.Id;
        }
        catch (DbUpdateException)
        {
            _db.Entry(riga).State = EntityState.Detached;
            return await _db.DocumentImpacts.AsNoTracking()
                .Where(i => i.DocumentId == input.DocumentId && i.Kind == input.Kind
                            && i.SourceKey == sourceKey && i.ClearedUtc == DocumentImpact.Aperto)
                .Select(i => i.Id)
                .FirstOrDefaultAsync(ct);
        }
    }

    public async Task ClearAsync(int impactId, int byUserId, DateTime whenUtc, CancellationToken ct = default)
    {
        var riga = await _db.DocumentImpacts.FirstOrDefaultAsync(i => i.Id == impactId, ct);
        if (riga is null || riga.ClearedUtc != DocumentImpact.Aperto) return;
        // ⚠️ Mai la sentinella: una chiusura registrata a `0001-01-01` sarebbe una riga che risulta ancora aperta.
        riga.ClearedUtc = whenUtc == DocumentImpact.Aperto ? DateTime.UtcNow : whenUtc;
        riga.ClearedByUserId = byUserId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey,
        int byUserId, DateTime whenUtc, CancellationToken ct = default)
    {
        var key = (sourceKey ?? "").Trim();
        if (kinds.Count == 0 || key.Length == 0) return 0;
        var tipi = kinds.ToList();

        var righe = await _db.DocumentImpacts
            .Where(i => i.SourceKey == key && tipi.Contains(i.Kind) && i.ClearedUtc == DocumentImpact.Aperto)
            .ToListAsync(ct);
        if (righe.Count == 0) return 0;

        var quando = whenUtc == DocumentImpact.Aperto ? DateTime.UtcNow : whenUtc;
        foreach (var r in righe) { r.ClearedUtc = quando; r.ClearedByUserId = byUserId; }
        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }

    public async Task<DocumentImpactRow?> GetOpenAsync(int impactId, CancellationToken ct = default) =>
        (await QueryRighe(_db.DocumentImpacts.AsNoTracking()
            .Where(i => i.Id == impactId && i.ClearedUtc == DocumentImpact.Aperto), ct)).FirstOrDefault();

    public async Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) =>
        await QueryRighe(_db.DocumentImpacts.AsNoTracking()
            .Where(i => i.DocumentId == documentId && i.ClearedUtc == DocumentImpact.Aperto)
            .OrderByDescending(i => i.RaisedUtc), ct);

    public async Task<IReadOnlyList<DocumentImpactRow>> ListOpenByKindAsync(ImpactKind kind, CancellationToken ct = default) =>
        await QueryRighe(_db.DocumentImpacts.AsNoTracking()
            .Where(i => i.Kind == kind && i.ClearedUtc == DocumentImpact.Aperto)
            .OrderByDescending(i => i.RaisedUtc), ct);

    public async Task<IReadOnlyList<DocumentImpactRow>> ListAllOpenAsync(CancellationToken ct = default) =>
        await QueryRighe(_db.DocumentImpacts.AsNoTracking()
            .Where(i => i.ClearedUtc == DocumentImpact.Aperto)
            .OrderByDescending(i => i.RaisedUtc), ct);

    private static async Task<IReadOnlyList<DocumentImpactRow>> QueryRighe(
        IQueryable<DocumentImpact> q, CancellationToken ct)
    {
        var righe = await q
            .Select(i => new
            {
                i.Id,
                i.DocumentId,
                Titolo = i.Document!.Title,
                i.Kind,
                i.SourceKey,
                i.ReasonKey,
                i.ReasonArgsJson,
                i.IsPublicNow,
                i.RaisedUtc,
            })
            .ToListAsync(ct);

        return righe.Select(i => new DocumentImpactRow(
            i.Id, i.DocumentId, i.Titolo ?? "", i.Kind, i.SourceKey, i.ReasonKey,
            Argomenti(i.ReasonArgsJson), i.IsPublicNow, i.RaisedUtc)).ToList();
    }

    private static IReadOnlyList<string> Argomenti(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    public async Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return new Dictionary<int, ImpactBadge>();
        var ids = documentIds.Distinct().ToList();

        var righe = await _db.DocumentImpacts.AsNoTracking()
            .Where(i => ids.Contains(i.DocumentId) && i.ClearedUtc == DocumentImpact.Aperto)
            .Select(i => new { i.DocumentId, i.Kind, i.IsPublicNow })
            .ToListAsync(ct);

        return righe.GroupBy(r => r.DocumentId).ToDictionary(
            g => g.Key,
            g => new ImpactBadge(
                Total: g.Count(),
                DaRipubblicare: g.Count(x => x.Kind.IsDaRipubblicare()),
                Rotti: g.Count(x => x.Kind.IsRotto()),
                GiaInPubblico: g.Count(x => x.IsPublicNow)));
    }

    /// <summary>Chiavi di sezione alimentate da ciascuna famiglia. Le aree ci sono per completezza, ma chi apre
    /// quegli impatti non lo chiede: la sezione «regulated» non la congela nessuna cattura di release, quindi e'
    /// <b>sempre</b> viva.</summary>
    private static readonly IReadOnlyDictionary<ImpactFamily, string[]> SezioniPerFamiglia =
        new Dictionary<ImpactFamily, string[]>
        {
            [ImpactFamily.Sector] = new[]
            {
                "aor", "frequencies", "coordination", "coordination:in", "coordination:out",
                "minima", "appgroup", "aerovia",
            },
            [ImpactFamily.Area] = new[] { "regulated" },
            [ImpactFamily.Document] = Array.Empty<string>(),
        };

    /// <summary>
    /// Quali documenti, fra quelli dati, hanno una sezione <b>Live</b> alimentata dalla famiglia: per loro il
    /// cambio e' gia' in pubblico, senza passare da una ripubblicazione.
    /// <para>Si guarda la versione <b>pubblicata corrente</b> (o l'ultima, se non ce n'e' una): la domanda e'
    /// «che cosa vede il pubblico adesso», non «che cosa vedra' quando pubblicheremo».</para>
    /// </summary>
    public async Task<IReadOnlySet<int>> WithLiveSectionAsync(
        IReadOnlyCollection<int> documentIds, ImpactFamily family, CancellationToken ct = default)
    {
        var chiavi = SezioniPerFamiglia.TryGetValue(family, out var k) ? k : Array.Empty<string>();
        if (documentIds.Count == 0 || chiavi.Length == 0) return new HashSet<int>();

        var ids = documentIds.Distinct().ToList();
        var docs = await _db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.CurrentVersionId })
            .ToListAsync(ct);

        var ultime = await _db.DocumentVersions.AsNoTracking()
            .Where(v => ids.Contains(v.DocumentId))
            .Select(v => new { v.Id, v.DocumentId, v.VersionNumber })
            .ToListAsync(ct);

        var versionePubblica = new Dictionary<int, int>();   // versionId -> documentId
        foreach (var d in docs)
        {
            var ultima = ultime.Where(v => v.DocumentId == d.Id)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            if ((d.CurrentVersionId ?? ultima) is int vid) versionePubblica[vid] = d.Id;
        }
        if (versionePubblica.Count == 0) return new HashSet<int>();

        var versionIds = versionePubblica.Keys.ToList();
        var live = await _db.DocumentSections.AsNoTracking()
            .Where(s => versionIds.Contains(s.DocumentVersionId)
                        && s.RenderMode == RenderMode.Live
                        && chiavi.Contains(s.SectionKey))
            .Select(s => s.DocumentVersionId)
            .Distinct()
            .ToListAsync(ct);

        return live.Where(versionePubblica.ContainsKey).Select(v => versionePubblica[v]).ToHashSet();
    }

    public async Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var vecchie = await _db.DocumentImpacts
            .Where(i => i.ClearedUtc != DocumentImpact.Aperto && i.ClearedUtc < cutoffUtc)
            .ToListAsync(ct);
        if (vecchie.Count == 0) return 0;
        _db.DocumentImpacts.RemoveRange(vecchie);
        await _db.SaveChangesAsync(ct);
        return vecchie.Count;
    }

    public async Task<string?> GetDocTitleAsync(int documentId, CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking().Where(d => d.Id == documentId).Select(d => d.Title)
            .FirstOrDefaultAsync(ct);
}

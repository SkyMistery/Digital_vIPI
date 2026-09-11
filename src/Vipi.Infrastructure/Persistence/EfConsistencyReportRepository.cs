using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: fotografia di sola lettura dei dati con soft-ref per il report di consistenza (nessuna scrittura).</summary>
public sealed class EfConsistencyReportRepository : IConsistencyReportRepository
{
    private readonly VipiDbContext _db;
    private readonly IAgreementRepository? _accordi;

    /// <param name="accordi">
    /// Serve ai punti di trasferimento (<see cref="ConsistencyDataset.TransferLadders"/>): chi riceve un
    /// punto lo decide l'<b>espansione</b> degli accordi, e rifarla a mano qui sarebbe un secondo modello
    /// della stessa cosa. ⚠️ Facoltativo: senza, quel pezzo del report non si fa — e i test che guardano
    /// altro non devono montarlo.
    /// </param>
    public EfConsistencyReportRepository(VipiDbContext db, IAgreementRepository? accordi = null)
    {
        _db = db;
        _accordi = accordi;
    }

    public async Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default)
    {
        // Condizioni pista/area: solo le clausole che hanno effettivamente un soft-ref o un'area da verificare.
        // Legge gli ACCORDI, non piu' i flussi: dopo il travaso la verita' sta li', e un report che guardasse le
        // tabelle storiche direbbe cose vere di un archivio che nessuno modifica piu'.
        var conditions = await (
            from c in _db.AgreementClauses.AsNoTracking()
            join s in _db.AgreementSections.AsNoTracking() on c.SectionId equals s.Id
            join g in _db.CoordinationAgreements.AsNoTracking() on s.AgreementId equals g.Id
            join a in _db.Accs.AsNoTracking() on g.OwnerAccId equals a.Id
            where c.ConditionRefId != null || c.ConditionAreaLabel != null
            select new TransferConditionRow(c.Id, a.Code, c.Cops, c.ConditionRefId, c.ConditionLabel, c.ConditionAreaLabel)
        ).ToListAsync(ct);

        var runwayIdents = await _db.AirportRunways.AsNoTracking()
            .Select(r => new { r.Id, r.Ident })
            .ToDictionaryAsync(x => x.Id, x => x.Ident, ct);

        var areaNames = (await _db.SpecialAreas.AsNoTracking().Select(s => s.Name).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Padri di copertura dichiarati (soft-ref per callsign, cross-catalogo, no FK).
        var parentRefs = new List<ParentRefRow>();
        parentRefs.AddRange(await _db.AccSectors.AsNoTracking()
            .Where(s => s.ParentCallsign != null)
            .Select(s => new ParentRefRow("Settore ACC", s.ComposePosition, s.ParentCallsign!, "Diag_Ent_SettoreAcc")).ToListAsync(ct));
        parentRefs.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ParentCallsign != null)
            .Select(s => new ParentRefRow("Settore APT", s.ComposePosition, s.ParentCallsign!, "Diag_Ent_SettoreApt")).ToListAsync(ct));
        parentRefs.AddRange(await _db.Airports.AsNoTracking()
            .Where(a => a.ParentCallsign != null)
            .Select(a => new ParentRefRow("Aeroporto", a.Icao, a.ParentCallsign!, "Diag_Ent_Aeroporto")).ToListAsync(ct));

        // L'albero EFFETTIVO, dalla porta unica: è quello su cui si cercano gli anelli. Costruirlo qui a mano
        // sarebbe la terza copia della scaletta, cioè il terzo posto da cui la divergenza può ricominciare.
        var righeGerarchia = new List<HierarchyCatalogRow>();
        righeGerarchia.AddRange((await _db.AccSectors.AsNoTracking()
                .Select(x => new { x.ComposePosition, x.ParentCallsign }).ToListAsync(ct))
            .Select(x => new HierarchyCatalogRow(x.ComposePosition, x.ParentCallsign, null, SectorType.Ctr, false)));
        righeGerarchia.AddRange((await _db.AirportSectors.AsNoTracking()
                .Where(x => x.Position == null || x.Position.ToUpper() != "ATIS")
                .Select(x => new { x.ComposePosition, x.ParentCallsign, x.AirportIcao, x.Position, x.IsHidden })
                .ToListAsync(ct))
            .Select(x => new HierarchyCatalogRow(x.ComposePosition, x.ParentCallsign, x.AirportIcao,
                EffectiveHierarchy.TypeOfPosition(x.Position), x.IsHidden)));

        var padreScalo = (await _db.Airports.AsNoTracking()
                .Select(a => new { a.Icao, a.ParentCallsign }).ToListAsync(ct))
            .ToDictionary(a => a.Icao, a => a.ParentCallsign, StringComparer.OrdinalIgnoreCase);

        // ⚠️ I doppioni escono di qui invece di essere sovrascritti in silenzio: lo stesso callsign può
        // stare in tutti e due i cataloghi (due indici unici, due tabelle) e l'albero effettivo ne tiene uno.
        var effectiveParents = EffectiveHierarchy.ParentMap(righeGerarchia, padreScalo, out var doppioni);

        // Callsign validi come padre = chiavi naturali dei cataloghi (ACC + aeroporto).
        var valid = (await _db.AccSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
            .Concat(await _db.AirportSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Shape dei settori, GREZZE: la diagnostica deve poter raccontare l'anomalia che il parsing ripara.
        // ⚠️ Solo i settori visibili: uno nascosto dall'admin non è un problema da segnalare tutti i giorni.
        var shapes = (await _db.AccSectors.AsNoTracking()
                .Where(x => !x.IsHidden)
                .Select(x => new SectorShapeRow("Settore ACC", x.ComposePosition, x.Position, x.RegionMapPolygon, false))
                .ToListAsync(ct))
            .Concat(await _db.AirportSectors.AsNoTracking()
                .Where(x => !x.IsHidden)
                .Select(x => new SectorShapeRow("Postazione", x.ComposePosition, x.Position, x.RegionMapPolygon, x.IsShapeSynthetic))
                .ToListAsync(ct))
            .ToList();

        // Le bande dichiarate dei settori visibili, per il rilievo «ricaduta che non copre la quota».
        var bande = (await _db.AccSectors.AsNoTracking()
                .Where(x => !x.IsHidden)
                .Select(x => new SectorBandRow(x.ComposePosition, x.LowerLimit, x.UpperLimit))
                .ToListAsync(ct))
            .Concat(await _db.AirportSectors.AsNoTracking()
                .Where(x => !x.IsHidden)
                .Select(x => new SectorBandRow(x.ComposePosition, x.LowerLimit, x.UpperLimit))
                .ToListAsync(ct))
            .ToList();

        var ripieghi = (await _db.SectorFallbacks.AsNoTracking()
                .OrderBy(r => r.SectorCallsign).ThenBy(r => r.Order)
                .Select(r => new { r.SectorCallsign, r.TargetCallsign, r.BaseFeet, r.TopFeet, r.TargetKind })
                .ToListAsync(ct))
            .GroupBy(r => r.SectorCallsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Vipi.Application.Content.FallbackRow>)g
                    .Select(r => new Vipi.Application.Content.FallbackRow(
                        r.TargetCallsign, r.BaseFeet, r.TopFeet, r.TargetKind)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        // L'albero PROIETTATO, per confrontarlo con quello dei cataloghi.
        var proiettati = await ProjectedParentsAsync(ct);

        return new ConsistencyDataset
        {
            SectorShapes = shapes,
            SectorBands = bande,
            Fallbacks = ripieghi,
            ProjectedParents = proiettati,
            TransferConditions = conditions,
            RunwayIdents = runwayIdents,
            AreaNames = areaNames,
            ParentRefs = parentRefs,
            EffectiveParents = effectiveParents,
            HierarchyDuplicates = doppioni,
            ValidCallsigns = valid,
            RegulatedRefs = await LoadRegulatedRefsAsync(ct),
            SpecialAreaIds = (await _db.SpecialAreas.AsNoTracking().Select(s => s.IvaoId).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            TransferLadders = await PuntiDiTrasferimentoAsync(ct),
            DocumentiFuoriCategoria = await DocumentiFuoriCategoriaAsync(ct),
        };
    }

    /// <summary>
    /// I documenti che la <b>categoria</b> del loro campo non ammette — una vIPI civile su un campo solo
    /// militare, un vSOP militare su un campo civile o civile con presenza militare — con le due domande che
    /// decidono se c'è qualcosa da dire: quel documento <b>si vede</b>? è ancora <b>unito</b> all'altra edizione?
    ///
    /// <para>⚠️ <b>Quattro letture in tutto</b>, non una per aeroporto: le appartenenze alle unioni e le release
    /// si chiedono in blocco. Questo elenco ha già pagato due volte il difetto N+1 altrove, e un rilievo che
    /// costa una query per riga si finisce per spegnerlo.</para>
    ///
    /// <para>⚠️ «Visibile» sono <b>tre</b> condizioni e non una, e sono esattamente quelle che chiede il
    /// caricatore pubblico: release in vigore <b>e</b> documento non nascosto <b>e</b> aeroporto non nascosto.
    /// Chiederne due su tre farebbe gridare il rilievo su un campo che dal web non si raggiunge.</para>
    ///
    /// <para>⚠️ Le categorie si confrontano per valore, e non con <c>AirportCategories.AllowsCivil</c>: un metodo
    /// dentro una <c>Where</c> non si traduce in SQL. Chi aggiunge una categoria cambia le due condizioni qui
    /// sotto insieme alle regole — lo presidia <c>DocumentiFuoriCategoriaTests</c>.</para>
    /// </summary>
    private async Task<IReadOnlyList<DocumentoFuoriCategoriaRow>> DocumentiFuoriCategoriaAsync(CancellationToken ct)
    {
        var campi = await _db.Airports.AsNoTracking()
            .Where(a => (a.Category == AirportCategory.MilitaryOnly && a.DocumentId != null)
                        || (a.Category != AirportCategory.MilitaryOnly
                            && a.Category != AirportCategory.MilitaryWithCivilPresence
                            && a.MilDocumentId != null))
            .Select(a => new
            {
                a.Icao,
                AccCode = a.Acc!.Code,
                AeroportoNascosto = a.IsHidden,
                a.Category,
                a.DocumentId,
                a.MilDocumentId,
            })
            .ToListAsync(ct);
        if (campi.Count == 0) return Array.Empty<DocumentoFuoriCategoriaRow>();

        // Per ogni campo, QUALE dei due documenti è fuori categoria e qual è l'altro.
        var fuori = campi.Select(c => c.Category == AirportCategory.MilitaryOnly
                ? (c.Icao, c.AccCode, c.AeroportoNascosto, Edizione: DocumentEdition.Civil,
                   Id: c.DocumentId!.Value, Altro: c.MilDocumentId)
                : (c.Icao, c.AccCode, c.AeroportoNascosto, Edizione: DocumentEdition.Military,
                   Id: c.MilDocumentId!.Value, Altro: c.DocumentId))
            .ToList();

        var ids = fuori.Select(f => f.Id).ToList();
        var nascosti = (await _db.Documents.AsNoTracking()
                .Where(d => ids.Contains(d.Id) && d.IsHidden)
                .Select(d => d.Id)
                .ToListAsync(ct))
            .ToHashSet();

        // ⚠️ Le due edizioni condividono la CHIAVE di release (l'ICAO) e si distinguono per il TIPO: la release
        // si cerca col tipo del documento fuori categoria, o si risponderebbe per quello sbagliato.
        var icaos = fuori.Select(f => f.Icao).ToList();
        var adesso = DateTime.UtcNow;
        var conRelease = (await _db.DocReleases.AsNoTracking()
                .Where(r => (r.TargetType == ReleaseTargetType.Airport || r.TargetType == ReleaseTargetType.AirportMil)
                            && icaos.Contains(r.TargetKey)
                            && r.ReleaseEffectiveUtc <= adesso)
                .Select(r => new { r.TargetType, r.TargetKey })
                .Distinct()
                .ToListAsync(ct))
            .Select(r => (r.TargetType, Icao: r.TargetKey.ToUpperInvariant()))
            .ToHashSet();

        // Le appartenenze dei documenti che ci interessano, in UNA lettura: (unione → documenti).
        var interessati = fuori.Select(f => f.Id)
            .Concat(fuori.Where(f => f.Altro != null).Select(f => f.Altro!.Value))
            .Distinct().ToList();
        var appartenenze = await _db.DocumentUnionMembers.AsNoTracking()
            .Where(m => interessati.Contains(m.DocumentId))
            .Select(m => new { m.UnionId, m.DocumentId })
            .ToListAsync(ct);
        var unioniDi = appartenenze.GroupBy(x => x.DocumentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UnionId).ToHashSet());

        return fuori.Select(f => new DocumentoFuoriCategoriaRow(
                f.Icao, f.AccCode, f.Edizione,
                Visibile: conRelease.Contains((f.Edizione == DocumentEdition.Civil ? ReleaseTargetType.Airport
                                                                                  : ReleaseTargetType.AirportMil,
                                               f.Icao.ToUpperInvariant()))
                          && !nascosti.Contains(f.Id) && !f.AeroportoNascosto,
                // ⚠️ «Unito» vuol dire unito ALL'ALTRA EDIZIONE DI QUESTO CAMPO, non «unito a qualcosa»: il
                // messaggio nomina l'altro documento, e nominare quello sbagliato manderebbe a sciogliere
                // l'unione sbagliata.
                UnitoAllAltra: f.Altro is int altro
                               && unioniDi.TryGetValue(f.Id, out var sue)
                               && unioniDi.TryGetValue(altro, out var sueAltro)
                               && sue.Overlaps(sueAltro)))
            .OrderBy(r => r.Icao, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Tutti i punti di trasferimento della divisione, uno per riga.
    ///
    /// <para>⚠️ Passa dall'<b>espansione</b> degli accordi e non da una query sua: chi riceve un punto dipende
    /// dalla direzione della sezione, e riscriverlo qui sarebbe un secondo modello di una cosa che ne ha già
    /// uno (<c>AgreementExpansion</c>). Gli ACC sono quattro: il giro costa quattro letture.</para>
    /// </summary>
    private async Task<IReadOnlyList<TransferLadderRow>> PuntiDiTrasferimentoAsync(CancellationToken ct)
    {
        if (_accordi is null) return Array.Empty<TransferLadderRow>();

        var codici = await _db.Accs.AsNoTracking()
            .Where(a => !a.IsForeign && !a.IsHidden)
            .Select(a => a.Code)
            .ToListAsync(ct);

        var righe = new List<TransferLadderRow>();
        foreach (var acc in codici)
        {
            var flussi = Vipi.Application.Content.AgreementExpansion.Expand(await _accordi.ListByAccAsync(acc, ct));
            foreach (var f in flussi)
                foreach (var p in f.Points)
                    righe.Add(new TransferLadderRow(
                        acc, p.ClauseId ?? 0, p.Cop, p.NextSectorCallsign, f.OwningSectorCallsign,
                        Vipi.Application.Content.FallbackChain.HandoffFeetOf(p)));
        }
        return righe;
    }

    /// <summary>callsign → padre nell'albero proiettato (<c>Sector.ParentSectorId</c>), risolto a callsign.</summary>
    private async Task<IReadOnlyDictionary<string, string?>> ProjectedParentsAsync(CancellationToken ct)
    {
        var settori = await _db.Sectors.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Callsign, s.ParentSectorId })
            .ToListAsync(ct);

        var perId = settori.ToDictionary(s => s.Id, s => s.Callsign);
        return settori.ToDictionary(
            s => s.Callsign,
            s => s.ParentSectorId is int pid && perId.TryGetValue(pid, out var p) ? p : null,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sezioni <c>regulated</c> (vIPI ACC: figlia di un blocco; vIPI APP non remotizzata: di primo livello) con il
    /// JSON della selezione. Solo la <b>versione di lavoro</b> di ogni documento — bozza più recente, altrimenti la
    /// pubblicata corrente, altrimenti l'ultima: le versioni storiche sono congelate per definizione e segnalarle
    /// sarebbe rumore su qualcosa che nessuno può più correggere.
    /// </summary>
    private async Task<IReadOnlyList<RegulatedRefRow>> LoadRegulatedRefsAsync(CancellationToken ct)
    {
        var docs = await _db.Documents.AsNoTracking()
            .Select(d => new { d.Id, d.Title, d.Type, d.CurrentVersionId })
            .ToListAsync(ct);
        if (docs.Count == 0) return Array.Empty<RegulatedRefRow>();

        var versions = await _db.DocumentVersions.AsNoTracking()
            .Select(v => new { v.Id, v.DocumentId, v.VersionNumber, v.Status })
            .ToListAsync(ct);

        var working = new Dictionary<int, int>();   // versionId → documentId
        foreach (var d in docs)
        {
            var draft = versions.Where(v => v.DocumentId == d.Id && v.Status == DocumentStatus.Draft)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            var last = versions.Where(v => v.DocumentId == d.Id)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefault();
            if ((draft ?? d.CurrentVersionId ?? last) is int id) working[id] = d.Id;
        }
        if (working.Count == 0) return Array.Empty<RegulatedRefRow>();

        var versionIds = working.Keys.ToList();

        // ⚠️ DUE chiavi dal 9 settembre 2026: le sezioni che portano una selezione d'aree sono «Aree di
        // lavoro» e, sul vSOP militare, «Bassa quota (BOAT)». Guardarne una sola vorrebbe dire che un'area
        // BOAT potata dai cataloghi sparisce dai documenti senza che il rapporto lo dica — cioè proprio il
        // silenzio che questo rapporto esiste per rompere.
        var chiaviAree = new[] { Vipi.Application.Content.SectionKeys.Regulated, Vipi.Application.Content.SectionKeys.LowLevel };
        var rows = (await (
            from s in _db.DocumentSections.AsNoTracking()
            where chiaviAree.Contains(s.SectionKey) && versionIds.Contains(s.DocumentVersionId)
            select new
            {
                s.DocumentVersionId,
                // ⚠️ TUTTI i blocchi, non il primo: la scelta di quale sia il payload la fa
                // `SectionPayload` sulla FORMA del JSON. Su una sezione «scheda + blocchi» il primo blocco
                // può benissimo essere una tabella scritta a mano o un'immagine — e su «Bassa quota», che
                // fino a ieri era solo prosa, è il caso normale.
                Jsons = _db.ContentBlocks.AsNoTracking()
                    .Where(b => b.SectionId == s.Id).OrderBy(b => b.Order).Select(b => b.BodyJson).ToList(),
            }).ToListAsync(ct))
            .Select(r => new { r.DocumentVersionId, Json = Vipi.Application.Content.SectionPayload.Scegli(r.Jsons) })
            .ToList();

        var byId = docs.ToDictionary(d => d.Id);
        return rows
            .Where(r => r.Json != null)
            .Select(r =>
            {
                var doc = byId[working[r.DocumentVersionId]];
                return new RegulatedRefRow(doc.Type == DocumentType.Vloa ? "vLOA" : "vIPI", doc.Title, r.Json);
            })
            .ToList();
    }
}

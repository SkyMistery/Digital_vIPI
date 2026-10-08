using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ICallsignRenameService"/>
/// <remarks>
/// <para><b>L'inventario non è a occhio.</b> I posti che si riscrivono qui vengono da una spazzata su OGNI
/// colonna testuale del <c>vipi.db</c> reale (26 agosto 2026), cercando la forma di un callsign italiano:</para>
/// <code>
///   AtcSessions.Callsign                     21267   ← STORIA, non si tocca (per questo esiste l'alias)
///   Sectors.Callsign                           204   ← riscritto, TENENDO l'Id
///   AirportSectors.ComposePosition              167  ← riscritto
///   AirportSectors.ParentCallsign                59  ← riscritto
///   AccSectors.ComposePosition                   37  ← riscritto
///   ContentBlocks.BodyJson                       35  ← riscritto (puntatori di configurazione)
///   NeighbourCandidates.AdjacentHomeCallsigns    33  ← si autoripara: l'import dei confinanti li ricalcola
///   Airports.ParentCallsign                      31  ← riscritto
///   AccSectors.ParentCallsign                    18  ← riscritto
///   DocReleases.TargetKey                        15  ← riscritto
///   AuditLogs.DetailsJson                         5  ← STORIA: il registro dice cosa fu fatto allora
///   DocumentImpacts.SourceKey                     5  ← riscritto, solo le righe APERTE
///   DocumentImpacts.ReasonArgsJson                3  ← STORIA: il testo della segnalazione com'era
///   AirportSectors.AtcCallsign / Sectors.Name     2  ← li riallineano import e proiezione
/// </code>
///
/// <para><b>Perché si riscrive anche la chiave di una release già pubblicata.</b> Non è storia: è un
/// <b>puntatore</b>, quello con cui si ritrova la copia pubblicata di un bersaglio. Lasciarlo indietro non
/// conserverebbe una verità, renderebbe il documento irraggiungibile — che è esattamente
/// <c>ImpactKind.ReleaseKeyMoved</c>. Il fatto storico («allora si chiamava così») resta, ed è in
/// <c>CallsignAlias</c>: l'alias esiste proprio perché i puntatori si possano riscrivere senza perdere
/// niente.</para>
/// </remarks>
public sealed class EfCallsignRenameService : ICallsignRenameService, ISectorSubstitution
{
    private readonly VipiDbContext _db;
    private readonly IDocumentImpactService? _impatti;

    /// <param name="impatti">
    /// Dove finisce l'avviso che il nominativo è cambiato. <b>Opzionale</b> come per la proiezione: rinominare
    /// e avvisare sono due cose, e un motore che non sa avvisare deve comunque saper rinominare — è quel che
    /// serve ai test della rinomina, che con la casella non c'entrano niente.
    /// </param>
    public EfCallsignRenameService(VipiDbContext db, IDocumentImpactService? impatti = null)
    {
        _db = db;
        _impatti = impatti;
    }

    public async Task<RenameOutcome> ApplyAsync(
        IReadOnlyList<CallsignRename> renames, CancellationToken ct = default)
    {
        if (renames.Count == 0) return RenameOutcome.Nothing;

        var applicate = new List<CallsignRename>();
        var rifiutate = new List<RenameRefused>();
        var enti = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in renames)
        {
            var motivo = await PerchePotrebbeNonSiPuoAsync(r, ct);
            if (motivo is not null) { rifiutate.Add(new RenameRefused(r, motivo)); continue; }

            enti[r.NewCallsign] = await RinominaAsync(r, ct) ?? "";
            applicate.Add(r);
        }

        if (applicate.Count == 0) return new RenameOutcome(applicate, rifiutate);

        await _db.SaveChangesAsync(ct);

        // ⚠️ Dopo il salvataggio, e cercando col nominativo NUOVO. Prima, il reverse-lookup girerebbe su uno
        // stato non ancora scritto; e col nominativo vecchio non troverebbe più niente, perché i legami sono
        // stati appena riscritti — proprio nel caso in cui l'avviso serve.
        await AvvisaAsync(applicate, enti, ct);

        return new RenameOutcome(applicate, rifiutate);
    }

    /// <summary>
    /// «Questo settore adesso si chiama così». Non è una domanda sull'identità — quella non è cambiata — ma
    /// sul <b>testo</b>: la prosa può ancora nominare il vecchio, e riscriverla non è un lavoro da calcolo.
    /// Per questo <c>SectorRenamed</c> lo chiude una persona.
    /// </summary>
    private async Task AvvisaAsync(
        IReadOnlyList<CallsignRename> applicate, IReadOnlyDictionary<string, string> enti, CancellationToken ct)
    {
        if (_impatti is null) return;

        foreach (var r in applicate)
        {
            var acc = enti.TryGetValue(r.NewCallsign, out var e) ? e : "";
            var righe = await _impatti.PrepareForSectorAsync(
                ImpactKind.SectorRenamed, r.NewCallsign, acc, new[] { r.OldCallsign, r.NewCallsign }, ct);
            if (righe.Count == 0) continue;

            await _impatti.RaiseForDocumentsAsync(ImpactKind.SectorRenamed,
                righe.Select(x => x.DocumentId).ToList(), r.NewCallsign,
                new[] { r.OldCallsign, r.NewCallsign }, ct);
        }
    }

    /// <summary>
    /// Il motivo per cui questa rinomina non si applica, o null se si può.
    ///
    /// <para>Il caso da fermare è il nominativo <b>già occupato</b> da qualcun altro. Succede in due modi, e
    /// nessuno dei due è normale: uno <b>scambio</b> fra due settori (A prende il nome di B e viceversa),
    /// oppure un archivio che porta già un fantasma da prima di questa carta e la sorgente ora rimanda il
    /// callsign su una riga diversa. Applicarla comunque violerebbe l'indice unico a metà giro, lasciando il
    /// resto dell'import in uno stato che nessuno ha chiesto; e indovinare chi dei due debba cedere il nome
    /// vuol dire scegliere quale documento perdere. Si riferisce e si lascia decidere a una persona.</para>
    /// </summary>
    private async Task<string?> PerchePotrebbeNonSiPuoAsync(CallsignRename r, CancellationToken ct)
    {
        var nuovo = r.NewCallsign;

        var occupatoInAcc = await _db.AccSectors
            .AnyAsync(x => x.ComposePosition == nuovo
                           && !(x.IvaoId == r.IvaoId && r.Catalog == SourceCatalog.Subcenter), ct);
        var occupatoInAeroporto = await _db.AirportSectors
            .AnyAsync(x => x.ComposePosition == nuovo
                           && !(x.IvaoId == r.IvaoId && r.Catalog == SourceCatalog.AirportPosition), ct);
        if (occupatoInAcc || occupatoInAeroporto)
            return $"{nuovo} è già in catalogo su un'altra riga";

        // Il settore proiettato del vecchio nominativo prenderà quello nuovo: se il nuovo è già di un ALTRO
        // settore, l'indice unico su Sectors.Callsign non lo permette.
        var settoreDelNuovo = await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == nuovo).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        var settoreDelVecchio = await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == r.OldCallsign).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (settoreDelNuovo is not null && settoreDelNuovo != settoreDelVecchio)
            return $"{nuovo} è già il callsign del settore #{settoreDelNuovo}";

        return null;
    }

    /// <returns>Il codice ACC della riga rinominata, che serve al reverse-lookup dell'avviso; null se la riga
    /// di catalogo non c'è più.</returns>
    private async Task<string?> RinominaAsync(CallsignRename r, CancellationToken ct)
    {
        var (vecchio, nuovo) = (r.OldCallsign, r.NewCallsign);
        string? accCode;

        // 1. La riga di catalogo, per IDENTITÀ: è l'unica ricerca del metodo che non passa dal nominativo.
        if (r.Catalog == SourceCatalog.Subcenter)
        {
            var riga = await _db.AccSectors.FirstOrDefaultAsync(x => x.IvaoId == r.IvaoId, ct);
            if (riga is not null) riga.ComposePosition = nuovo;
            accCode = riga?.CenterId;
        }
        else
        {
            var riga = await _db.AirportSectors.FirstOrDefaultAsync(x => x.IvaoId == r.IvaoId, ct);
            if (riga is not null) riga.ComposePosition = nuovo;
            accCode = riga?.AccCode;
        }

        // 2. Il settore proiettato: cambia il NOME, non l'Id — ed è tutto il punto di questa carta. Accordi,
        //    vLOA, blocchi, figli, documento, AoR e FeaturedRank puntano all'Id e non si accorgono di niente.
        var settore = await _db.Sectors.FirstOrDefaultAsync(s => s.Callsign == vecchio, ct);
        if (settore is not null) settore.Callsign = nuovo;

        await RiscriviRiferimentiAsync(vecchio, nuovo, ct);
        await AnnotaAliasAsync(vecchio, nuovo, r.Catalog, r.IvaoId, settore?.Id, ct);
        return accCode;
    }

    /// <summary>
    /// Tutti i posti che citano un settore PER NOME, dal vecchio al nuovo (passi 3–6 della rinomina). Lo usa anche
    /// la sostituzione a mano (S54), che è la stessa riscrittura quando la sorgente non ha conservato l'identità.
    /// </summary>
    private async Task RiscriviRiferimentiAsync(string vecchio, string nuovo, CancellationToken ct)
    {
        bool Stesso(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // 3. La gerarchia di copertura, che vive per callsign in tre posti e senza chiave esterna.
        foreach (var x in await _db.AccSectors.Where(x => x.ParentCallsign == vecchio).ToListAsync(ct))
            x.ParentCallsign = nuovo;
        foreach (var x in await _db.AirportSectors.Where(x => x.ParentCallsign == vecchio).ToListAsync(ct))
            x.ParentCallsign = nuovo;
        foreach (var x in await _db.Airports.Where(x => x.ParentCallsign == vecchio).ToListAsync(ct))
            x.ParentCallsign = nuovo;

        // 3-bis. La catena di ripiego, che vive per callsign come la gerarchia — e da DUE lati: il settore che
        //        ricade e il settore che raccoglie. Saltarne uno lascerebbe una riga che punta a un nominativo
        //        che non esiste più: la ricaduta la scavalcherebbe in silenzio, che è il difetto che questa
        //        tabella esiste per evitare.
        foreach (var x in await _db.SectorFallbacks.Where(x => x.SectorCallsign == vecchio).ToListAsync(ct))
            x.SectorCallsign = nuovo;
        foreach (var x in await _db.SectorFallbacks.Where(x => x.TargetCallsign == vecchio).ToListAsync(ct))
            x.TargetCallsign = nuovo;

        // 3-ter. 🔴 U-041 (revisione totale 3): gli agganci AIP, che si risolvono per callsign. Rimasti al vecchio
        //        nome, il settore tornava in silenzio alla forma di IVAO. Il ponte delle forme gira dopo il
        //        salvataggio, e vede già il nome nuovo.
        foreach (var x in await _db.SectorAirspaceBindings.Where(x => x.Callsign == vecchio).ToListAsync(ct))
            x.Callsign = nuovo;

        // 3-quater. Le scelte per callsign del profilo dei documenti (AoR e frequenze nascoste, ordine delle
        //        frequenze): stesso riscrittore dei blocchi, che tocca solo i valori uguali al vecchio nome.
        foreach (var p in await _db.DocumentProfiles
                     .Where(x => (x.HiddenAorSectorsJson != null && EF.Functions.Like(x.HiddenAorSectorsJson, $"%{vecchio}%"))
                                 || (x.HiddenFrequenciesJson != null && EF.Functions.Like(x.HiddenFrequenciesJson, $"%{vecchio}%"))
                                 || (x.FreqOrderJson != null && EF.Functions.Like(x.FreqOrderJson, $"%{vecchio}%")))
                     .ToListAsync(ct))
        {
            p.HiddenAorSectorsJson = JsonCallsignRewriter.Rewrite(p.HiddenAorSectorsJson, vecchio, nuovo) ?? p.HiddenAorSectorsJson;
            p.HiddenFrequenciesJson = JsonCallsignRewriter.Rewrite(p.HiddenFrequenciesJson, vecchio, nuovo) ?? p.HiddenFrequenciesJson;
            p.FreqOrderJson = JsonCallsignRewriter.Rewrite(p.FreqOrderJson, vecchio, nuovo) ?? p.FreqOrderJson;
        }

        // 4. Le chiavi di release e degli incarichi (vedi il commento del tipo sul perché si riscrivono).
        //    ⚠️ Solo la vIPI ACC. La vIPI APP dal 29 settembre 2026 (S49) è pubblicata sotto il CODICE del suo
        //    ente, che non cambia mai: riscriverla la staccherebbe dall'ente. Cambia la posizione dell'ente (4-bis).
        var suffissoAcc = "|" + vecchio;
        foreach (var rel in await _db.DocReleases
                     .Where(x => x.TargetType == ReleaseTargetType.AccVipi && x.TargetKey.EndsWith(suffissoAcc))
                     .ToListAsync(ct))
            rel.TargetKey = RiscriviChiave(rel.TargetKey, vecchio, nuovo);

        foreach (var t in await _db.EditorTasks
                     .Where(x => x.TargetKey != null
                                 && x.TargetType == ReleaseTargetType.AccVipi && x.TargetKey.EndsWith(suffissoAcc))
                     .ToListAsync(ct))
            t.TargetKey = RiscriviChiave(t.TargetKey!, vecchio, nuovo);

        // 4-bis. La posizione dell'ENTE segue il nominativo (S49). Se il nome nuovo è già di un ente, quella resta
        //        e questa si toglie: una posizione appartiene a un ente solo.
        var posizione = _db.AtcUnitPositions.Local.FirstOrDefault(p => Stesso(p.Callsign, vecchio))
                        ?? await _db.AtcUnitPositions.FirstOrDefaultAsync(p => p.Callsign == vecchio, ct);
        if (posizione is not null)
        {
            if (await _db.AtcUnitPositions.AnyAsync(p => p.Callsign == nuovo, ct)) _db.AtcUnitPositions.Remove(posizione);
            else posizione.Callsign = nuovo;
        }

        // 5. Le segnalazioni APERTE che citano il vecchio nominativo come origine: chiuse o no, restano
        //    ancorate al settore, e il settore è lo stesso. Le righe già chiuse non si toccano — quelle sono
        //    il verbale di un fatto passato.
        foreach (var i in await _db.DocumentImpacts
                     .Where(x => x.SourceKey == vecchio && x.ClearedUtc == DocumentImpact.Aperto)
                     .ToListAsync(ct))
            i.SourceKey = nuovo;

        // 6. I puntatori dentro le configurazioni a blocchi. Si filtra col LIKE per non caricare in memoria
        //    ogni BodyJson dell'archivio; il confronto vero, sul valore intero, lo fa il riscrittore.
        foreach (var b in await _db.ContentBlocks
                     .Where(x => x.BodyJson != null && EF.Functions.Like(x.BodyJson, $"%{vecchio}%"))
                     .ToListAsync(ct))
            if (JsonCallsignRewriter.Rewrite(b.BodyJson, vecchio, nuovo) is { } riscritto)
                b.BodyJson = riscritto;

        // 6-bis. Le configurazioni possibili dichiarate in Struttura: stessa forma JSON, stesso riscrittore.
        //        ⚠️ Solo i settori DENTRO l'elenco: la chiave del gruppo è il codice dell'ACC o dell'ente, e
        //        quello di un ente non segue le sue posizioni (è il punto di `AtcUnit.Code`).
        foreach (var e in await _db.SectorConfigurationSets
                     .Where(x => EF.Functions.Like(x.BodyJson, $"%{vecchio}%"))
                     .ToListAsync(ct))
            if (JsonCallsignRewriter.Rewrite(e.BodyJson, vecchio, nuovo) is { } riscritto)
                e.BodyJson = riscritto;
    }

    /// <summary>Il passo 7 della rinomina: l'alias vecchio → nuovo, per lo storico.</summary>
    private async Task AnnotaAliasAsync(string vecchio, string nuovo, SourceCatalog catalogo, int? ivaoId,
        int? sectorId, CancellationToken ct)
    {
        bool Stesso(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // 7. L'alias, per lo storico: AtcSessions da solo ne ha 21 267 righe, e quelle dicono un fatto.
        //
        // 🔴 T-029 (13 settembre 2026): un nominativo che TORNA. A→B, poi B→A, poi di nuovo A→B: il secondo
        // alias con OldCallsign = A violava l'indice unico, e la rinomina gira in testa all'import dei settori
        // senza catch — l'import falliva a ogni giro. Due regole:
        //   • il nominativo nuovo non è più «dismesso»: se un alias lo dava per tale, quell'alias ora mente
        //     (la storia tradurrebbe il nome di oggi in quello di ieri) e si toglie;
        //   • il vecchio ha al più UN successore: se l'alias c'è già, si aggiorna invece di aggiungerne un altro.
        // Si guarda anche fra le righe NON ancora salvate: più rinomine dello stesso giro vanno in un solo
        // SaveChanges, e una query non le vedrebbe.

        foreach (var rinato in _db.CallsignAliases.Local.Where(a => Stesso(a.OldCallsign, nuovo)).ToList()
                     .Concat(await _db.CallsignAliases.Where(a => a.OldCallsign == nuovo).ToListAsync(ct))
                     .Distinct())
            _db.CallsignAliases.Remove(rinato);

        var alias = _db.CallsignAliases.Local.FirstOrDefault(a => Stesso(a.OldCallsign, vecchio)
                                                                  && _db.Entry(a).State != EntityState.Deleted)
                    ?? await _db.CallsignAliases.FirstOrDefaultAsync(a => a.OldCallsign == vecchio, ct);
        if (alias is null)
        {
            alias = new CallsignAlias { OldCallsign = vecchio };
            _db.CallsignAliases.Add(alias);
        }
        alias.NewCallsign = nuovo;
        alias.Catalog = catalogo;
        alias.IvaoId = ivaoId;
        alias.SectorId = sectorId;
        alias.RenamedAtUtc = DateTime.UtcNow;
    }

    /// <inheritdoc cref="ISectorSubstitution"/>
    public async Task<SostituzioneEsito> SostituisciAsync(int vecchioSectorId, int nuovoSectorId, int actorUserId,
        CancellationToken ct = default)
    {
        var x = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == vecchioSectorId, ct)
                ?? throw new Vipi.Application.Aor.ValidationException(Lingua("Settore vecchio inesistente.", "The old sector does not exist."));
        var y = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == nuovoSectorId, ct)
                ?? throw new Vipi.Application.Aor.ValidationException(Lingua("Settore nuovo inesistente.", "The new sector does not exist."));
        if (x.Id == y.Id)
            throw new Vipi.Application.Aor.ValidationException(Lingua("Un settore non si sostituisce con sé stesso.", "A sector cannot replace itself."));
        if (!y.IsActive)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                $"{y.Callsign} non è attivo: IVAO non lo manda, e sostituire con lui sposterebbe tutto su un altro orfano.",
                $"{y.Callsign} is not active: IVAO does not send it, and replacing with it would move everything onto another orphan."));
        var (vecchio, nuovo) = (x.Callsign, y.Callsign);

        // ---- I rifiuti, TUTTI prima di scrivere: ognuno vorrebbe dire scegliere al posto di una persona. ----
        var accordi = await _db.CoordinationAgreements.Include(a => a.Sections)
            .Where(a => a.SideASectorId == x.Id || a.SideBSectorId == x.Id).ToListAsync(ct);
        foreach (var a in accordi)
        {
            var altro = a.SideASectorId == x.Id ? a.SideBSectorId : a.SideASectorId;
            if (altro == y.Id)
                throw new Vipi.Application.Aor.ValidationException(Lingua(
                    $"{vecchio} e {nuovo} hanno un accordo fra loro: sostituire l'uno con l'altro lo renderebbe un accordo con sé stesso. Eliminalo prima.",
                    $"{vecchio} and {nuovo} have an agreement with each other: replacing one with the other would make it an agreement with itself. Delete it first."));
            var (ca, cb) = altro <= y.Id ? (altro, y.Id) : (y.Id, altro);
            if (await _db.CoordinationAgreements.AnyAsync(b => b.SideASectorId == ca && b.SideBSectorId == cb, ct))
            {
                var nome = await _db.Sectors.Where(s => s.Id == altro).Select(s => s.Callsign).FirstAsync(ct);
                throw new Vipi.Application.Aor.ValidationException(Lingua(
                    $"{nuovo} ha già un accordo con {nome}, e anche {vecchio}: uniscili a mano prima di sostituire.",
                    $"{nuovo} already has an agreement with {nome}, and so has {vecchio}: merge them by hand before replacing."));
            }
        }
        if (x.DocumentId is int dx && y.DocumentId is int dy && dx != dy)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                $"{vecchio} e {nuovo} portano due documenti diversi: riaggancia o elimina uno dei due prima.",
                $"{vecchio} and {nuovo} carry two different documents: reattach or delete one of them first."));
        var enteVecchio = await _db.AtcUnitPositions.Where(p => p.Callsign == vecchio).Select(p => (int?)p.AtcUnitId).FirstOrDefaultAsync(ct);
        var enteNuovo = await _db.AtcUnitPositions.Where(p => p.Callsign == nuovo).Select(p => (int?)p.AtcUnitId).FirstOrDefaultAsync(ct);
        if (enteVecchio is not null && enteNuovo is not null && enteVecchio != enteNuovo)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                $"{vecchio} e {nuovo} sono posizioni di due enti diversi: sistema gli enti prima di sostituire.",
                $"{vecchio} and {nuovo} are positions of two different units: fix the units before replacing."));

        // ---- Per NUMERO: quel che la rinomina automatica non deve toccare, perché lì il settore è lo stesso. ----
        foreach (var a in accordi)
        {
            var altro = a.SideASectorId == x.Id ? a.SideBSectorId : a.SideASectorId;
            var (sideA, sideB) = altro <= y.Id ? (altro, y.Id) : (y.Id, altro);
            // ⚠️ La forma canonica (id minore = A): se i lati si scambiano, i versi delle sezioni si ribaltano con
            // loro, come in UpdateAgreementAsync — o ogni sezione direbbe il contrario di ciò che c'era scritto.
            var scambiati = a.SideASectorId != sideA;
            a.SideASectorId = sideA;
            a.SideBSectorId = sideB;
            if (scambiati)
            {
                foreach (var s in a.Sections)
                    s.Direction = s.Direction switch
                    {
                        AgreementDirection.AtoB => AgreementDirection.BtoA,
                        AgreementDirection.BtoA => AgreementDirection.AtoB,
                        var d => d,
                    };
            }
        }

        var blocchi = 0;
        foreach (var b in await _db.ContentBlocks
                     .Where(b => b.ScopeSectorId == x.Id || b.FromSectorId == x.Id || b.ToSectorId == x.Id).ToListAsync(ct))
        {
            if (b.ScopeSectorId == x.Id) b.ScopeSectorId = y.Id;
            if (b.FromSectorId == x.Id) b.FromSectorId = y.Id;
            if (b.ToSectorId == x.Id) b.ToSectorId = y.Id;
            blocchi++;
        }
        foreach (var p in await _db.DocumentParties.Where(p => p.SectorId == x.Id).ToListAsync(ct)) p.SectorId = y.Id;
        foreach (var l in await _db.AirportFrequencyLinks.Where(l => l.SourceSectorId == x.Id).ToListAsync(ct)) l.SourceSectorId = y.Id;
        foreach (var pr in await _db.DocumentProfiles
                     .Where(pr => pr.FreqLinksJson != null && EF.Functions.Like(pr.FreqLinksJson, $"%{x.Id}%")).ToListAsync(ct))
            pr.FreqLinksJson = SostituisciId(pr.FreqLinksJson!, x.Id, y.Id);

        var figli = await _db.Sectors.Where(s => s.ParentSectorId == x.Id && s.Id != y.Id).ToListAsync(ct);
        foreach (var f in figli) f.ParentSectorId = y.Id;
        // Il nuovo prende il posto del vecchio nella gerarchia, se non ne ha già uno suo.
        if (y.ParentSectorId is null && x.ParentSectorId is int px && px != y.Id) y.ParentSectorId = px;

        var documento = false;
        if (x.DocumentId is int doc)
        {
            y.DocumentId = doc;
            y.IsPrimary = x.IsPrimary || y.IsPrimary;
            y.FeaturedRank ??= x.FeaturedRank;
            x.DocumentId = null;
            x.IsPrimary = false;
            x.FeaturedRank = null;
            documento = true;
        }

        // ---- La riga di catalogo del nuovo prende il padre del vecchio, se non ne ha uno suo. ----
        var padreVecchio = await _db.AccSectors.Where(c => c.ComposePosition == vecchio).Select(c => c.ParentCallsign).FirstOrDefaultAsync(ct)
                           ?? await _db.AirportSectors.Where(c => c.ComposePosition == vecchio).Select(c => c.ParentCallsign).FirstOrDefaultAsync(ct);
        var rigaAcc = await _db.AccSectors.FirstOrDefaultAsync(c => c.ComposePosition == nuovo, ct);
        var rigaScalo = rigaAcc is null ? await _db.AirportSectors.FirstOrDefaultAsync(c => c.ComposePosition == nuovo, ct) : null;
        if (padreVecchio is not null && !string.Equals(padreVecchio, nuovo, StringComparison.OrdinalIgnoreCase))
        {
            if (rigaAcc is { ParentCallsign: null }) rigaAcc.ParentCallsign = padreVecchio;
            if (rigaScalo is { ParentCallsign: null }) rigaScalo.ParentCallsign = padreVecchio;
        }
        var (catalogo, ivaoId, idRiga) = rigaAcc is not null ? (SourceCatalog.Subcenter, rigaAcc.IvaoId, rigaAcc.Id)
            : rigaScalo is not null ? (SourceCatalog.AirportPosition, rigaScalo.IvaoId, rigaScalo.Id)
            : (SourceCatalog.Subcenter, (int?)null, (int?)null);

        // ---- Per NOME: la stessa riscrittura della rinomina automatica, e lo stesso alias per lo storico. ----
        await RiscriviRiferimentiAsync(vecchio, nuovo, ct);
        // Gli agganci AIP e le parti di forma puntano alla RIGA di catalogo: seguono quella del nuovo.
        if (idRiga is int riga)
        {
            foreach (var bnd in _db.SectorAirspaceBindings.Local.Where(v => string.Equals(v.Callsign, nuovo, StringComparison.OrdinalIgnoreCase)))
            { bnd.Catalog = catalogo; bnd.SectorId = riga; }
            foreach (var parte in await _db.SectorShapeParts.Where(v => v.Callsign == vecchio).ToListAsync(ct))
            { parte.Callsign = nuovo; parte.Catalog = catalogo; parte.SectorId = riga; }
        }
        await AnnotaAliasAsync(vecchio, nuovo, catalogo, ivaoId, y.Id, ct);

        AuditScribe.Write(_db, actorUserId, AuditAction.Update, "Sector", y.Id.ToString(),
            new { Sostituito = vecchio, Con = nuovo, Accordi = accordi.Count, Blocchi = blocchi, Figli = figli.Count, Documento = documento });
        await _db.SaveChangesAsync(ct);

        // Come la rinomina: la prosa può ancora nominare il vecchio, e riscriverla lo decide una persona.
        await AvvisaAsync(new[] { new CallsignRename(catalogo, ivaoId ?? 0, vecchio, nuovo) },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [nuovo] = await _db.Sectors.Where(s => s.Id == y.Id).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct) ?? "",
            }, ct);

        return new SostituzioneEsito(vecchio, nuovo, accordi.Count, blocchi, figli.Count, documento);
    }

    /// <summary>Nel JSON dei link di frequenza (una lista di id di settore) sostituisce l'id vecchio col nuovo, senza
    /// ripeterlo se c'era già.</summary>
    private static string SostituisciId(string json, int vecchio, int nuovo)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(json);
            if (ids is null || !ids.Contains(vecchio)) return json;
            var fuori = new List<int>();
            foreach (var id in ids.Select(i => i == vecchio ? nuovo : i))
                if (!fuori.Contains(id)) fuori.Add(id);
            return JsonSerializer.Serialize(fuori);
        }
        catch (JsonException) { return json; }
    }

    /// <summary>La chiave è il callsign nudo (App) o <c>{acc}|{callsign}</c> (AccVipi): si sostituisce solo
    /// la coda, così un codice ACC che per assurdo somigliasse al callsign resta dov'è.</summary>
    private static string RiscriviChiave(string chiave, string vecchio, string nuovo) =>
        chiave.Equals(vecchio, StringComparison.OrdinalIgnoreCase)
            ? nuovo
            : chiave[..^vecchio.Length] + nuovo;
}

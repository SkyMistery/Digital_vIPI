using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

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
public sealed class EfCallsignRenameService : ICallsignRenameService
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

        // 4. Le chiavi di release e degli incarichi (vedi il commento del tipo sul perché si riscrivono).
        var suffissoAcc = "|" + vecchio;
        foreach (var rel in await _db.DocReleases
                     .Where(x => (x.TargetType == ReleaseTargetType.App && x.TargetKey == vecchio)
                                 || (x.TargetType == ReleaseTargetType.AccVipi && x.TargetKey.EndsWith(suffissoAcc)))
                     .ToListAsync(ct))
            rel.TargetKey = RiscriviChiave(rel.TargetKey, vecchio, nuovo);

        foreach (var t in await _db.EditorTasks
                     .Where(x => x.TargetKey != null
                                 && ((x.TargetType == ReleaseTargetType.App && x.TargetKey == vecchio)
                                     || (x.TargetType == ReleaseTargetType.AccVipi && x.TargetKey.EndsWith(suffissoAcc))))
                     .ToListAsync(ct))
            t.TargetKey = RiscriviChiave(t.TargetKey!, vecchio, nuovo);

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

        // 7. L'alias, per lo storico: AtcSessions da solo ne ha 21 267 righe, e quelle dicono un fatto.
        _db.CallsignAliases.Add(new CallsignAlias
        {
            OldCallsign = vecchio,
            NewCallsign = nuovo,
            Catalog = r.Catalog,
            IvaoId = r.IvaoId,
            SectorId = settore?.Id,
            RenamedAtUtc = DateTime.UtcNow,
        });

        return accCode;
    }

    /// <summary>La chiave è il callsign nudo (App) o <c>{acc}|{callsign}</c> (AccVipi): si sostituisce solo
    /// la coda, così un codice ACC che per assurdo somigliasse al callsign resta dov'è.</summary>
    private static string RiscriviChiave(string chiave, string vecchio, string nuovo) =>
        chiave.Equals(vecchio, StringComparison.OrdinalIgnoreCase)
            ? nuovo
            : chiave[..^vecchio.Length] + nuovo;
}

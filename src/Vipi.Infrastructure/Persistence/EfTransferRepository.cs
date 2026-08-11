using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="ITransferRepository"/>: flussi (settore proprio) e loro punti (CoP/livello/Next).</summary>
public sealed class EfTransferRepository : ITransferRepository
{
    private readonly VipiDbContext _db;
    public EfTransferRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default)
    {
        var flows = await _db.TransferFlows.AsNoTracking()
            .Where(f => f.Acc!.Code == accCode)
            .Include(f => f.OwningSector)
            .Include(f => f.Points).ThenInclude(p => p.NextSector)
            .OrderBy(f => f.OwningSectorId).ThenBy(f => f.Order)
            .ToListAsync(ct);

        return flows.Select(MapFlow).ToList();
    }

    public async Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default)
    {
        var accId = await AccIdAsync(accCode, ct);
        var nextOrder = (await _db.TransferFlows
            .Where(f => f.AccId == accId && f.OwningSectorId == input.OwningSectorId)
            .MaxAsync(f => (int?)f.Order, ct) ?? 0) + 1;

        var f = new TransferFlow { AccId = accId, Order = nextOrder };
        ApplyFlow(f, input);
        _db.TransferFlows.Add(f);
        await _db.SaveChangesAsync(ct);
        return f.Id;
    }

    public async Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default)
    {
        var f = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Flusso {flowId} non appartiene alla ACC {accCode}.");
        ApplyFlow(f, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default)
    {
        var f = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct);
        if (f is null) return;
        _db.TransferFlows.Remove(f);   // i punti seguono in cascade
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default)
    {
        var flow = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Flusso {flowId} non appartiene alla ACC {accCode}.");
        var nextOrder = (await _db.TransferPoints.Where(p => p.FlowId == flowId).MaxAsync(p => (int?)p.Order, ct) ?? 0) + 1;

        var p = new TransferPoint { FlowId = flow.Id, Order = nextOrder };
        ApplyPoint(p, input);
        _db.TransferPoints.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        ApplyPoint(p, input);

        if (p.VariantGroup is int group)
        {
            // Una riga che scavalca le alternative non può stare dentro una: sarebbe una contraddizione fra il
            // proprio significato («vale per tutte») e la propria posizione («appartengo a questa»).
            if (p.IsGroupWide && p.VariantDepth > 0)
                throw new ValidationException("Una riga «in ogni caso» non può essere l'eccezione di un'altra riga.");

            // CoP e ricevente sono l'IDENTITÀ dell'accordo, condivisa da tutte le varianti: cambiarli su una riga
            // li cambia sul gruppo. Propagare è meglio che rifiutare — l'invariante resta vera senza chiedere
            // all'editore di ripetere la stessa modifica su ogni riga.
            foreach (var s in await GroupSiblingsAsync(p, group, ct))
            {
                s.Cop = p.Cop;
                s.NextSectorId = p.NextSectorId;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await _db.TransferPoints.FirstOrDefaultAsync(x => x.Id == pointId && x.Flow!.Acc!.Code == accCode, ct);
        if (p is null) return;
        var group = p.VariantGroup;
        _db.TransferPoints.Remove(p);
        if (group is int g) await DissolveIfAloneAsync(p.FlowId, g, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> AddAlternativeAsync(string accCode, int pointId, CancellationToken ct = default) =>
        AddVariantRowAsync(accCode, pointId, asException: false, ct);

    public Task<int> AddExceptionAsync(string accCode, int pointId, CancellationToken ct = default) =>
        AddVariantRowAsync(accCode, pointId, asException: true, ct);

    /// <summary>
    /// Nasce una riga nell'outline del gruppo, copiata dalla sorgente meno la condizione — che è esattamente
    /// ciò che deve dire di diverso. I dati restano piatti (nessuna eredità di campo: con <c>LevelValue</c>
    /// nullable, «null = eredita» sarebbe indistinguibile da «null = non specificato»), ma chi scrive non
    /// ridigita quindici campi per cambiarne uno.
    /// <para><paramref name="asException"/> decide dove finisce: **alternativa** = pari-grado alla sorgente,
    /// dopo tutto il suo sottoalbero, altrimenti spezzerebbe in due un blocco già scritto; **eccezione** = un
    /// livello più dentro, subito sotto la sorgente.</para>
    /// </summary>
    private async Task<int> AddVariantRowAsync(string accCode, int pointId, bool asException, CancellationToken ct)
    {
        var src = await PointInAccAsync(accCode, pointId, ct);

        // Il gruppo nasce alla prima variante: progressivo per flusso, indipendente dagli Id (leggibile e stabile).
        if (src.VariantGroup is null)
            src.VariantGroup = (await _db.TransferPoints.Where(x => x.FlowId == src.FlowId)
                .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;
        var group = src.VariantGroup.Value;

        // Un'alternativa di una riga annidata resta al livello di QUELLA riga, non torna a 0: «pari-grado alla
        // sorgente» è la promessa del tasto, e vale a qualunque profondità.
        var depth = asException ? src.VariantDepth + 1 : src.VariantDepth;

        var rows = await GroupRowsInOrderAsync(src.FlowId, group, ct);
        // L'eccezione va subito sotto la sorgente; l'alternativa dopo l'ultimo discendente della sorgente.
        var after = asException ? src : Subtree(rows, src)[^1];

        // Copia editoriale condivisa con la duplicazione del gruppo: i campi sono venti, e due elenchi
        // paralleli sono due posti in cui dimenticare quello aggiunto ieri.
        var copy = CopyOf(src);
        copy.VariantGroup = group;
        copy.VariantDepth = depth;
        copy.Order = after.Order + 1;
        // ⚠️ La CONDIZIONE no: è esattamente ciò che la riga nuova deve dire di diverso, e copiarla darebbe due
        // righe identiche. CopyOf la porta perché serve alla duplicazione del gruppo, dove invece va tenuta.
        copy.ConditionLabel = null; copy.ConditionRefId = null;
        copy.ConditionAreaLabel = null; copy.ConditionCustomLabel = null;

        foreach (var x in await _db.TransferPoints.Where(x => x.FlowId == src.FlowId && x.Order > after.Order).ToListAsync(ct))
            x.Order++;

        _db.TransferPoints.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task DetachVariantAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        if (p.VariantGroup is not int group) return;

        // Sfilare una riga porta via il suo SOTTOALBERO: le eccezioni descrivono la riga che le ospita, e
        // lasciarle indietro le riassegnerebbe in silenzio alla riga di sopra — cambiando ciò che dicono.
        var moved = Subtree(await GroupRowsInOrderAsync(p.FlowId, group, ct), p);

        // Il pezzo staccato riparte da zero: la radice torna a profondità 0 e i discendenti scalano con lei.
        var shift = p.VariantDepth;
        foreach (var x in moved) { x.VariantDepth -= shift; x.IsGroupWide = false; }

        // Resta un gruppo solo se ha ancora qualcosa da tenere insieme; una riga sola non è un gruppo.
        var newGroup = moved.Count > 1
            ? (await _db.TransferPoints.Where(x => x.FlowId == p.FlowId).MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1
            : (int?)null;
        foreach (var x in moved) x.VariantGroup = newGroup;

        await DissolveIfAloneAsync(p.FlowId, group, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        var rows = await _db.TransferPoints.Where(x => x.FlowId == p.FlowId).OrderBy(x => x.Order).ToListAsync(ct);

        // Si muove il BLOCCO, non la riga: una capofila che si sposta lasciando indietro le sue eccezioni le
        // riassegna alla riga di sopra, e quelle continuano a dire quello che dicevano di un'altra alternativa.
        // Nessun errore, significato cambiato: è la trappola dell'appartenenza per ordine.
        var block = Subtree(rows, p);
        var first = rows.IndexOf(block[0]);
        var last = first + block.Count - 1;

        // Il vicino nella stessa direzione è a sua volta un blocco: si scavalca intero, non riga per riga.
        List<TransferPoint>? neighbour = null;
        if (up && first > 0) neighbour = Subtree(rows, RootOf(rows, first - 1));
        else if (!up && last < rows.Count - 1) neighbour = Subtree(rows, rows[last + 1]);
        if (neighbour is null) return;   // estremo: no-op

        var reordered = new List<TransferPoint>(rows);
        reordered.RemoveAll(x => block.Contains(x));
        var anchor = reordered.IndexOf(up ? neighbour[0] : neighbour[^1]);
        reordered.InsertRange(up ? anchor : anchor + 1, block);
        for (var i = 0; i < reordered.Count; i++) reordered[i].Order = i + 1;

        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        var rows = await _db.TransferPoints.Where(x => x.FlowId == p.FlowId).OrderBy(x => x.Order).ToListAsync(ct);
        if (rows.Count < 2) return;

        // Anche qui si sposta il blocco: vale la stessa ragione di MovePointAsync.
        var block = Subtree(rows, p);
        rows.RemoveAll(x => block.Contains(x));
        if (top) rows.InsertRange(0, block); else rows.AddRange(block);
        for (var i = 0; i < rows.Count; i++) rows[i].Order = i + 1;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateVariantGroupAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        if (p.VariantGroup is not int group) return 0;

        var rows = await GroupRowsInOrderAsync(p.FlowId, group, ct);
        if (rows.Count == 0) return 0;

        // Gruppo nuovo e Order in coda: la copia nasce accanto all'originale, non dentro.
        var newGroup = (await _db.TransferPoints.Where(x => x.FlowId == p.FlowId)
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;
        var order = (await _db.TransferPoints.Where(x => x.FlowId == p.FlowId)
            .MaxAsync(x => (int?)x.Order, ct) ?? 0);

        foreach (var src in rows)
        {
            var copy = CopyOf(src);
            copy.VariantGroup = newGroup;
            // La struttura si copia com'è: profondità e righe trasversali sono ciò che rende utile duplicare
            // un gruppo invece delle sue righe una per una.
            copy.VariantDepth = src.VariantDepth;
            copy.IsGroupWide = src.IsGroupWide;
            copy.Order = ++order;
            _db.TransferPoints.Add(copy);
        }

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> SetReceiverAsync(string accCode, IReadOnlyList<int> pointIds, int? nextSectorId, CancellationToken ct = default)
    {
        if (pointIds.Count == 0) return 0;
        var rows = await _db.TransferPoints
            .Where(x => pointIds.Contains(x.Id) && x.Flow!.Acc!.Code == accCode)
            .ToListAsync(ct);

        foreach (var r in rows) r.NextSectorId = nextSectorId;

        // Il ricevente è l'identità dell'accordo, condivisa dal gruppo: cambiarlo su una riga lo cambia sulle
        // sorelle, esattamente come fa UpdatePointAsync. Senza, una selezione parziale spaccherebbe l'invariante.
        var groups = rows.Where(r => r.VariantGroup is not null).Select(r => (r.FlowId, r.VariantGroup)).Distinct().ToList();
        foreach (var (flowId, group) in groups)
            foreach (var s in await _db.TransferPoints.Where(x => x.FlowId == flowId && x.VariantGroup == group).ToListAsync(ct))
                s.NextSectorId = nextSectorId;

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    /// <summary>Copia di una riga senza identità né posizione: i campi editoriali e basta.</summary>
    private static TransferPoint CopyOf(TransferPoint src) => new()
    {
        FlowId = src.FlowId,
        Cop = src.Cop,
        LevelValue = src.LevelValue,
        LevelUnit = src.LevelUnit,
        LevelConstraint = src.LevelConstraint,
        LevelSpecial = src.LevelSpecial,
        Parity = src.Parity,
        VerticalState = src.VerticalState,
        NextSectorId = src.NextSectorId,
        ConditionLabel = src.ConditionLabel,
        ConditionRefId = src.ConditionRefId,
        ConditionAreaLabel = src.ConditionAreaLabel,
        ConditionCustomLabel = src.ConditionCustomLabel,
        HandoffKind = src.HandoffKind,
        HandoffLabel = src.HandoffLabel,
        HandoffLevelValue = src.HandoffLevelValue,
        HandoffLevelUnit = src.HandoffLevelUnit,
        HandoffLevelConstraint = src.HandoffLevelConstraint,
        CommsHandoffKind = src.CommsHandoffKind,
        CommsHandoffLabel = src.CommsHandoffLabel,
        SpeedValue = src.SpeedValue,
        SpeedConstraint = src.SpeedConstraint,
    };

    public async Task MovePointToAsync(string accCode, int pointId, int targetPointId, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        var target = await PointInAccAsync(accCode, targetPointId, ct);
        if (p.Id == target.Id || p.FlowId != target.FlowId) return;

        var rows = await _db.TransferPoints.Where(x => x.FlowId == p.FlowId).OrderBy(x => x.Order).ToListAsync(ct);
        var block = Subtree(rows, p);
        if (block.Any(x => x.Id == target.Id)) return;   // dentro sé stesso: non c'è dove andare

        var scendendo = target.Order > p.Order;
        rows.RemoveAll(x => block.Contains(x));
        var at = rows.IndexOf(target);
        if (at < 0) return;
        // Scendendo si va DOPO il bersaglio, salendo PRIMA: è quello che si aspetta chi trascina.
        rows.InsertRange(scendendo ? at + 1 : at, block);
        for (var i = 0; i < rows.Count; i++) rows[i].Order = i + 1;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Risale dalla riga in posizione <paramref name="index"/> alla radice del suo blocco: serve per
    /// scavalcare all'insù un vicino che è a sua volta un sottoalbero, e non finirgli in mezzo.</summary>
    private static TransferPoint RootOf(List<TransferPoint> rowsInOrder, int index)
    {
        var r = rowsInOrder[index];
        if (r.VariantGroup is null || r.VariantDepth == 0) return r;
        for (var k = index - 1; k >= 0; k--)
            if (rowsInOrder[k].VariantGroup == r.VariantGroup && rowsInOrder[k].VariantDepth < r.VariantDepth)
                return rowsInOrder[k];
        return r;
    }

    // ---- helper ----

    private async Task<int> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

    private async Task<TransferPoint> PointInAccAsync(string accCode, int pointId, CancellationToken ct) =>
        await _db.TransferPoints.FirstOrDefaultAsync(x => x.Id == pointId && x.Flow!.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Punto {pointId} non appartiene alla ACC {accCode}.");

    /// <summary>Le altre righe dello stesso gruppo di varianti (la riga passata esclusa).</summary>
    private Task<List<TransferPoint>> GroupSiblingsAsync(TransferPoint p, int group, CancellationToken ct) =>
        _db.TransferPoints.Where(x => x.FlowId == p.FlowId && x.VariantGroup == group && x.Id != p.Id).ToListAsync(ct);

    /// <summary>Le righe di un gruppo nell'ordine in cui si leggono — che nell'outline è anche la struttura.</summary>
    private Task<List<TransferPoint>> GroupRowsInOrderAsync(int flowId, int group, CancellationToken ct) =>
        _db.TransferPoints.Where(x => x.FlowId == flowId && x.VariantGroup == group)
            .OrderBy(x => x.Order).ToListAsync(ct);

    /// <summary>
    /// La riga più tutto ciò che le appartiene: quelle che la seguono finché restano nel suo gruppo e più
    /// profonde di lei. È la definizione di sottoalbero in un outline, e serve ovunque una riga si muova o si
    /// stacchi — perché muovere una capofila senza le sue eccezioni le riassegna a un'altra alternativa
    /// **senza un errore**: nessuna eccezione, nessun log, solo un accordo che dice un'altra cosa.
    /// <para>Funziona sia sull'elenco del solo gruppo sia su quello dell'intero flusso: le righe fuori dal
    /// gruppo non hanno profondità, quindi il confronto sul gruppo chiude il blocco da sé.</para>
    /// </summary>
    private static List<TransferPoint> Subtree(List<TransferPoint> rowsInOrder, TransferPoint root)
    {
        var i = rowsInOrder.FindIndex(x => x.Id == root.Id);
        if (i < 0 || root.VariantGroup is null) return new List<TransferPoint> { root };
        var block = new List<TransferPoint> { rowsInOrder[i] };
        for (var k = i + 1; k < rowsInOrder.Count
                            && rowsInOrder[k].VariantGroup == root.VariantGroup
                            && rowsInOrder[k].VariantDepth > root.VariantDepth; k++)
            block.Add(rowsInOrder[k]);
        return block;
    }

    /// <summary>Scioglie un gruppo rimasto con una sola riga: un gruppo di uno non è un gruppo, e lasciarlo
    /// significherebbe rendere una riga singola con l'intestazione di gruppo e un «negli altri casi» senza «casi».
    /// Non salva: il chiamante è già dentro una sua <c>SaveChangesAsync</c>.</summary>
    private async Task DissolveIfAloneAsync(int flowId, int group, CancellationToken ct)
    {
        var candidati = await _db.TransferPoints
            .Where(x => x.FlowId == flowId && x.VariantGroup == group)
            .ToListAsync(ct);

        // La query filtra su ciò che sta NEL DATABASE, ma qui siamo prima della SaveChanges: la riga appena
        // sfilata (VariantGroup = null) o appena rimossa torna comunque indietro dal SELECT. Va riletto lo
        // stato in memoria, che è quello che sta per essere scritto — altrimenti il gruppo sembra ancora
        // affollato e non si scioglie mai.
        var remaining = candidati
            .Where(x => x.VariantGroup == group && _db.Entry(x).State != EntityState.Deleted)
            .ToList();
        if (remaining.Count > 1) return;
        foreach (var x in remaining) { x.VariantGroup = null; x.VariantDepth = 0; x.IsGroupWide = false; }
    }

    private static void ApplyFlow(TransferFlow f, TransferFlowInput i)
    {
        f.OwningSectorId = i.OwningSectorId;
        f.Kind = i.Kind;
        f.AirportIcao = string.IsNullOrWhiteSpace(i.AirportIcao) ? null : i.AirportIcao.Trim().ToUpperInvariant();
        // Nome solo per aeroporti fuori DB: senza ICAO non ha senso; se c'è, si tiene la stringa grezza.
        f.AirportName = string.IsNullOrWhiteSpace(i.AirportIcao) || string.IsNullOrWhiteSpace(i.AirportName)
            ? null : i.AirportName.Trim();
        f.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
    }

    private static void ApplyPoint(TransferPoint p, TransferPointInput i)
    {
        p.Cop = (i.Cop ?? "").Trim();
        p.LevelValue = i.LevelConstraint == LevelConstraint.Special ? null : i.LevelValue;
        p.LevelUnit = i.LevelUnit;
        p.LevelConstraint = i.LevelConstraint;
        p.LevelSpecial = i.LevelConstraint == LevelConstraint.Special
            ? (string.IsNullOrWhiteSpace(i.LevelSpecial) ? null : i.LevelSpecial.Trim()) : null;
        p.Parity = i.Parity;
        p.VerticalState = i.VerticalState;
        p.NextSectorId = i.NextSectorId;

        // Condizione: tre dimensioni indipendenti (pista/area/personalizzata), ognuna trim→null se vuota.
        // Il soft-ref pista è tenuto solo se c'è una pista.
        p.ConditionLabel = NullIfBlank(i.ConditionLabel);
        p.ConditionRefId = p.ConditionLabel is null ? null : i.ConditionRefId;
        p.ConditionAreaLabel = NullIfBlank(i.ConditionAreaLabel);
        p.ConditionCustomLabel = NullIfBlank(i.ConditionCustomLabel);

        // Faccetta trasferimento. Senza tipo non c'è trasferimento distinto: i campi correlati vengono azzerati,
        // così una riga tornata a «coincide con l'ingresso» non si porta dietro un livello fantasma.
        p.HandoffKind = i.HandoffKind;
        p.HandoffLabel = i.HandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.HandoffLabel);
        p.HandoffLevelValue = i.HandoffKind == TransferHandoffKind.Unspecified ? null : i.HandoffLevelValue;
        p.HandoffLevelUnit = i.HandoffLevelUnit;
        p.HandoffLevelConstraint = i.HandoffLevelConstraint;
        p.CommsHandoffKind = i.CommsHandoffKind;
        p.CommsHandoffLabel = i.CommsHandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.CommsHandoffLabel);

        // Velocità: senza vincolo non c'è restrizione, e il valore residuo sparisce con essa.
        p.SpeedConstraint = i.SpeedConstraint;
        p.SpeedValue = i.SpeedConstraint == SpeedConstraint.Unspecified ? null : i.SpeedValue;

        // «Negli altri casi» ha senso solo dentro un gruppo, e il gruppo lo assegna AddVariantAsync: qui il flag
        // si accetta solo se la riga è già in un gruppo (VariantGroup non è un campo dell'input, apposta).
        // «Scavalca le alternative» ha senso solo dentro un gruppo: fuori non ci sono alternative da scavalcare.
        p.IsGroupWide = i.IsGroupWide && p.VariantGroup is not null;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TransferFlowRow MapFlow(TransferFlow f) => new()
    {
        Id = f.Id,
        AccCode = f.Acc?.Code ?? "",
        OwningSectorId = f.OwningSectorId,
        OwningSectorCallsign = f.OwningSector?.Callsign ?? $"#{f.OwningSectorId}",
        Kind = f.Kind,
        AirportIcao = f.AirportIcao,
        AirportName = f.AirportName,
        Description = f.Description,
        Order = f.Order,
        Points = f.Points.OrderBy(p => p.Order).Select(MapPoint).ToList(),
    };

    private static TransferPointRow MapPoint(TransferPoint p) => new()
    {
        Id = p.Id,
        Cop = p.Cop,
        LevelValue = p.LevelValue,
        LevelUnit = p.LevelUnit,
        LevelConstraint = p.LevelConstraint,
        LevelSpecial = p.LevelSpecial,
        Parity = p.Parity,
        VerticalState = p.VerticalState,
        LevelText = LevelFormatting.Format(p.LevelValue, p.LevelUnit, p.LevelConstraint, p.LevelSpecial, p.Parity, p.VerticalState),
        NextSectorId = p.NextSectorId,
        NextSectorCallsign = p.NextSector?.Callsign,
        ConditionLabel = p.ConditionLabel,
        ConditionRefId = p.ConditionRefId,
        ConditionAreaLabel = p.ConditionAreaLabel,
        ConditionCustomLabel = p.ConditionCustomLabel,
        HandoffKind = p.HandoffKind,
        HandoffLabel = p.HandoffLabel,
        HandoffLevelValue = p.HandoffLevelValue,
        HandoffLevelUnit = p.HandoffLevelUnit,
        HandoffLevelConstraint = p.HandoffLevelConstraint,
        CommsHandoffKind = p.CommsHandoffKind,
        CommsHandoffLabel = p.CommsHandoffLabel,
        SpeedValue = p.SpeedValue,
        SpeedConstraint = p.SpeedConstraint,
        VariantGroup = p.VariantGroup,
        VariantDepth = p.VariantDepth,
        IsGroupWide = p.IsGroupWide,
        Order = p.Order,
    };
}

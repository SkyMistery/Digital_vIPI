using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAgreementRepository"/>: accordi, parti, aeroporti e clausole.
///
/// <para><b>Lo scopo dell'outline è (accordo, verso).</b> Tutto ciò che sposta, annida o scioglie ragiona su
/// quell'insieme — le clausole del verso opposto non sono alternative delle prime, sono un'altra tabella. È la
/// sola differenza strutturale rispetto al repository dei flussi, dove lo scopo era il flusso.</para>
/// </summary>
public sealed class EfAgreementRepository : IAgreementRepository
{
    private readonly VipiDbContext _db;
    public EfAgreementRepository(VipiDbContext db) => _db = db;

    // ---- lettura ------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default)
    {
        // «Riguarda la ACC» = ne è responsabile OPPURE ha una parte fra i suoi settori. La seconda metà è ciò
        // che chiude la duplicazione per ACC: un accordo di confine è visibile da entrambi i capi, e un centro
        // estero che confina con due ACC italiane non va più riscritto due volte.
        var agreements = await _db.CoordinationAgreements.AsNoTracking()
            .Where(a => a.OwnerAcc!.Code == accCode
                        || a.Parties.Any(p => p.Sector!.Acc!.Code == accCode))
            .Include(a => a.OwnerAcc)
            .Include(a => a.Parties).ThenInclude(p => p.Sector)
            .Include(a => a.Airports)
            .Include(a => a.Clauses)
            .OrderBy(a => a.Order).ThenBy(a => a.Id)
            .ToListAsync(ct);

        return agreements.Select(Map).ToList();
    }

    // ---- intestazione -------------------------------------------------------------------------------

    public async Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default)
    {
        var accId = await AccIdAsync(accCode, ct);
        var order = (await _db.CoordinationAgreements.Where(a => a.OwnerAccId == accId)
            .MaxAsync(a => (int?)a.Order, ct) ?? 0) + 1;

        var a = new CoordinationAgreement { OwnerAccId = accId, Order = order };
        ApplyHeader(a, input);
        _db.CoordinationAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    public async Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default)
    {
        var a = await TrackedAgreementAsync(accCode, agreementId, ct);
        ApplyHeader(a, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default)
    {
        var a = await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == agreementId, ct);
        if (a is null) return;
        _db.CoordinationAgreements.Remove(a);   // parti, aeroporti e clausole seguono in cascade
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Riscrive parti e aeroporti al posto di aggiornarli uno per uno. La differenza si vede quando l'editore
    /// toglie un ente da un lato: con l'aggiornamento «per differenza» servirebbe sapere quale riga togliere, e
    /// l'editor dovrebbe portarsi dietro gli id delle parti — cioè conoscere la persistenza per modificare un
    /// elenco. Qui l'elenco è il dato, e chi lo scrive lo scrive intero.
    /// </summary>
    private void ApplyHeader(CoordinationAgreement a, AgreementInput i)
    {
        a.TrafficKind = i.TrafficKind;
        a.Description = NullIfBlank(i.Description);

        _db.AgreementParties.RemoveRange(a.Parties);
        a.Parties.Clear();
        AddParties(a, AgreementSide.A, i.SideA);
        AddParties(a, AgreementSide.B, i.SideB);

        _db.AgreementAirports.RemoveRange(a.Airports);
        a.Airports.Clear();
        var order = 0;
        foreach (var apt in i.Airports)
            a.Airports.Add(new AgreementAirport
            {
                Icao = apt.Icao.Trim().ToUpperInvariant(),
                // Il nome si tiene solo per gli scali fuori catalogo, dove è l'unica fonte. Per gli altri
                // arriva dal catalogo, e una copia qui divergerebbe alla prima rinomina.
                Name = NullIfBlank(apt.Name),
                Order = ++order,
            });
    }

    private static void AddParties(CoordinationAgreement a, AgreementSide side, IReadOnlyList<int> sectorIds)
    {
        var order = 0;
        foreach (var id in sectorIds.Distinct())
            a.Parties.Add(new AgreementParty { Side = side, SectorId = id, Order = ++order });
    }

    // ---- clausole -----------------------------------------------------------------------------------

    public async Task<int> AddClauseAsync(string accCode, int agreementId, AgreementDirection direction,
        AgreementClauseInput input, CancellationToken ct = default)
    {
        var a = await AgreementAsync(accCode, agreementId, ct);
        var order = (await Scope(a.Id, direction).MaxAsync(c => (int?)c.Order, ct) ?? 0) + 1;

        var c = new AgreementClause { AgreementId = a.Id, Direction = direction, Order = order };
        ApplyClause(c, input);
        _db.AgreementClauses.Add(c);
        await _db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        ApplyClause(c, input);

        if (c.VariantGroup is int group)
        {
            if (c.IsGroupWide && c.VariantDepth > 0)
                throw new ValidationException("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.");

            // I PUNTI sono l'identità dell'accordo dentro un gruppo — le varianti sono lo stesso accordo detto a
            // condizioni diverse — quindi cambiarli su una clausola li cambia sulle sorelle. Propagare è meglio
            // che rifiutare: l'invariante resta vera senza chiedere di ripetere la stessa modifica su ognuna.
            // Il RICEVENTE, che prima viaggiava con loro, qui non c'è: è dell'accordo, e non può più divergere.
            foreach (var s in await _db.AgreementClauses
                         .Where(x => x.AgreementId == c.AgreementId && x.Direction == c.Direction
                                     && x.VariantGroup == group && x.Id != c.Id)
                         .ToListAsync(ct))
                s.Cops = c.Cops;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClausesOf(accCode).FirstOrDefaultAsync(x => x.Id == clauseId, ct);
        if (c is null) return;
        var group = c.VariantGroup;
        _db.AgreementClauses.Remove(c);
        if (group is int g) await DissolveIfAloneAsync(c.AgreementId, c.Direction, g, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default) =>
        AddVariantAsync(accCode, clauseId, asException: false, ct);

    public Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default) =>
        AddVariantAsync(accCode, clauseId, asException: true, ct);

    /// <summary>
    /// Nasce una clausola nell'outline del gruppo, copiata dalla sorgente meno la condizione — che è
    /// esattamente ciò che deve dire di diverso. I dati restano piatti (nessuna eredità di campo: con un livello
    /// nullable, «null = eredita» sarebbe indistinguibile da «null = non specificato»), ma chi scrive non
    /// ridigita venti campi per cambiarne uno.
    /// </summary>
    private async Task<int> AddVariantAsync(string accCode, int clauseId, bool asException, CancellationToken ct)
    {
        var src = await ClauseInAccAsync(accCode, clauseId, ct);

        // Il gruppo nasce alla prima variante: progressivo per accordo (non per verso), così due gruppi «1» di
        // versi diversi non si somigliano leggendo l'archivio a mano.
        if (src.VariantGroup is null)
            src.VariantGroup = (await _db.AgreementClauses.Where(x => x.AgreementId == src.AgreementId)
                .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;
        var group = src.VariantGroup.Value;

        // Un'alternativa di una clausola annidata resta al livello di QUELLA clausola, non torna a 0:
        // «pari-grado alla sorgente» è la promessa del tasto, e vale a qualunque profondità.
        var depth = asException ? src.VariantDepth + 1 : src.VariantDepth;

        var rows = await GroupRowsAsync(src, group, ct);
        // L'eccezione va subito sotto la sorgente; l'alternativa dopo l'ultimo discendente della sorgente,
        // altrimenti spezzerebbe in due un blocco già scritto.
        var after = asException ? src : Subtree(rows, src)[^1];

        var copy = CopyOf(src);
        copy.VariantGroup = group;
        copy.VariantDepth = depth;
        copy.Order = after.Order + 1;
        // ⚠️ La CONDIZIONE no: è ciò che la clausola nuova deve dire di diverso, e copiarla darebbe due clausole
        // identiche. CopyOf la porta perché serve alla duplicazione del gruppo, dove invece va tenuta.
        copy.ConditionLabel = null; copy.ConditionRefId = null;
        copy.ConditionAreaLabel = null; copy.ConditionCustomLabel = null;

        foreach (var x in await Scope(src.AgreementId, src.Direction).Where(x => x.Order > after.Order).ToListAsync(ct))
            x.Order++;

        _db.AgreementClauses.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        if (c.VariantGroup is not int group) return;

        // Sfilare una clausola porta via il suo SOTTOALBERO: le eccezioni descrivono la clausola che le ospita,
        // e lasciarle indietro le riassegnerebbe in silenzio a quella di sopra — cambiando ciò che dicono.
        var moved = Subtree(await GroupRowsAsync(c, group, ct), c);

        var shift = c.VariantDepth;
        foreach (var x in moved) { x.VariantDepth -= shift; x.IsGroupWide = false; }

        // Resta un gruppo solo se ha ancora qualcosa da tenere insieme; una clausola sola non è un gruppo.
        var newGroup = moved.Count > 1
            ? (await _db.AgreementClauses.Where(x => x.AgreementId == c.AgreementId)
                .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1
            : (int?)null;
        foreach (var x in moved) x.VariantGroup = newGroup;

        await DissolveIfAloneAsync(c.AgreementId, c.Direction, group, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        var rows = await ScopeRowsAsync(c, ct);

        // Si muove il BLOCCO, non la riga: una capofila che si sposta lasciando indietro le sue eccezioni le
        // riassegna a quella di sopra, e quelle continuano a dire quello che dicevano di un'altra alternativa.
        // Nessun errore, significato cambiato: è la trappola dell'appartenenza per ordine.
        var block = Subtree(rows, c);
        var first = rows.IndexOf(block[0]);
        var last = first + block.Count - 1;

        // Il vicino nella stessa direzione è a sua volta un blocco: si scavalca intero, non riga per riga.
        List<AgreementClause>? neighbour = null;
        if (up && first > 0) neighbour = Subtree(rows, RootOf(rows, first - 1));
        else if (!up && last < rows.Count - 1) neighbour = Subtree(rows, rows[last + 1]);
        if (neighbour is null) return;   // estremo: no-op

        var reordered = new List<AgreementClause>(rows);
        reordered.RemoveAll(block.Contains);
        var anchor = reordered.IndexOf(up ? neighbour[0] : neighbour[^1]);
        reordered.InsertRange(up ? anchor : anchor + 1, block);
        Renumber(reordered);

        await _db.SaveChangesAsync(ct);
    }

    public async Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        var target = await ClauseInAccAsync(accCode, targetClauseId, ct);
        // Fra accordi diversi non si trascina, e nemmeno fra versi diversi: cambiare il verso di una clausola è
        // dire un'altra cosa, non spostarla.
        if (c.Id == target.Id || c.AgreementId != target.AgreementId || c.Direction != target.Direction) return;

        var rows = await ScopeRowsAsync(c, ct);
        var block = Subtree(rows, c);
        if (block.Any(x => x.Id == target.Id)) return;   // dentro sé stesso: non c'è dove andare

        var scendendo = target.Order > c.Order;
        rows.RemoveAll(block.Contains);
        var at = rows.IndexOf(target);
        if (at < 0) return;
        // Scendendo si va DOPO il bersaglio, salendo PRIMA: è quello che si aspetta chi trascina.
        rows.InsertRange(scendendo ? at + 1 : at, block);
        Renumber(rows);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        var c = await ClauseInAccAsync(accCode, clauseId, ct);
        if (c.VariantGroup is not int group) return 0;

        var rows = await GroupRowsAsync(c, group, ct);
        if (rows.Count == 0) return 0;

        var newGroup = (await _db.AgreementClauses.Where(x => x.AgreementId == c.AgreementId)
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;
        var order = await Scope(c.AgreementId, c.Direction).MaxAsync(x => (int?)x.Order, ct) ?? 0;

        foreach (var src in rows)
        {
            var copy = CopyOf(src);
            copy.VariantGroup = newGroup;
            // La struttura si copia com'è: profondità e clausole trasversali sono ciò che rende utile duplicare
            // un gruppo invece delle sue righe una per una.
            copy.VariantDepth = src.VariantDepth;
            copy.IsGroupWide = src.IsGroupWide;
            copy.Order = ++order;
            _db.AgreementClauses.Add(copy);
        }

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> CopyDirectionAsync(string accCode, int agreementId, AgreementDirection from,
        CancellationToken ct = default)
    {
        var a = await AgreementAsync(accCode, agreementId, ct);
        var to = from == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

        // Se il verso di destinazione ha già qualcosa non si tocca: sovrascrivere sarebbe buttare via ciò che
        // qualcuno ha scritto, e accodare produrrebbe un doppione di ogni clausola.
        if (await Scope(a.Id, to).AnyAsync(ct)) return 0;

        var source = await Scope(a.Id, from).OrderBy(x => x.Order).ToListAsync(ct);
        if (source.Count == 0) return 0;

        // Il gruppo si RINUMERA: i numeri sono progressivi per accordo, e riusarli farebbe sembrare le clausole
        // del verso opposto varianti delle prime.
        var nextGroup = (await _db.AgreementClauses.Where(x => x.AgreementId == a.Id)
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0);
        var groupMap = new Dictionary<int, int>();

        foreach (var src in source)
        {
            var copy = CopyOf(src);
            copy.Direction = to;
            copy.Order = src.Order;
            copy.VariantDepth = src.VariantDepth;
            copy.IsGroupWide = src.IsGroupWide;
            if (src.VariantGroup is int g)
            {
                if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                copy.VariantGroup = mapped;
            }
            _db.AgreementClauses.Add(copy);
        }

        await _db.SaveChangesAsync(ct);
        return source.Count;
    }

    public async Task<int> AbsorbAsReverseAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default)
    {
        if (keepId == absorbId) return 0;

        // ⚠️ Serve il grafo COMPLETO, e non basta la testata: il confronto è sui callsign delle parti (quindi
        // servono i Sector) e sui versi occupati (quindi servono le clausole). Con le parti caricate ma senza il
        // settore, Map ripiega su «#id» e due accordi specchiati non si riconoscerebbero mai.
        var keep = await FullAgreementAsync(accCode, keepId, ct);
        var absorb = await FullAgreementAsync(accCode, absorbId, ct);

        // ⚠️ Le condizioni si RIVALIDANO qui e non si dànno per buone dalla proposta: fra il momento in cui il
        // candidato è stato calcolato e il momento in cui si preme, qualcun altro può aver scritto nel verso che
        // deve restare libero — e accodare due scritture nella stessa tabella è proprio la scelta che il travaso
        // si era rifiutato di fare.
        var keepRow = Map(keep);
        var absorbRow = Map(absorb);
        if (!AgreementMerge.IsReverseOf(keepRow, absorbRow))
            throw new InvalidOperationException(
                $"Gli accordi {keepId} e {absorbId} non sono i due versi della stessa relazione.");
        if (!AgreementMerge.TargetFree(keepRow, absorbRow))
            throw new InvalidOperationException(
                $"L'accordo {keepId} ha già clausole nel verso in cui andrebbero quelle di {absorbId}.");

        var moving = await _db.AgreementClauses.Where(x => x.AgreementId == absorbId)
            .OrderBy(x => x.Direction).ThenBy(x => x.Order).ToListAsync(ct);
        if (moving.Count == 0) return 0;

        // I gruppi di varianti si rinumerano: sono progressivi per ACCORDO, e riusarli qui farebbe sembrare le
        // clausole arrivate varianti di quelle che c'erano già.
        var nextGroup = await _db.AgreementClauses.Where(x => x.AgreementId == keepId)
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0;
        var groupMap = new Dictionary<int, int>();

        foreach (var gruppo in moving.GroupBy(x => x.Direction))
        {
            var verso = AgreementMerge.Flip(gruppo.Key);
            var order = 0;
            foreach (var c in gruppo)
            {
                c.AgreementId = keepId;
                // Il verso si ribalta perché i due accordi hanno i lati scambiati: un A→B di là è un B→A di qua.
                c.Direction = verso;
                c.Order = ++order;
                if (c.VariantGroup is int g)
                {
                    if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                    c.VariantGroup = mapped;
                }
            }
        }

        // Il guscio se ne va DOPO che le clausole hanno cambiato padre: cancellarlo prima le porterebbe con sé
        // in cascade, e l'unione perderebbe proprio ciò che doveva salvare.
        await _db.SaveChangesAsync(ct);
        _db.CoordinationAgreements.Remove(absorb);
        await _db.SaveChangesAsync(ct);

        return moving.Count;
    }

    // ---- modifica in blocco -------------------------------------------------------------------------

    public async Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level,
        CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        foreach (var r in rows)
        {
            r.LevelConstraint = level.Constraint;
            r.LevelValue = level.Constraint == LevelConstraint.Special ? null : level.Value;
            r.LevelSpecial = level.Constraint == LevelConstraint.Special ? NullIfBlank(level.Special) : null;
            r.LevelUnit = level.Unit;
            r.Parity = level.Parity;
            r.VerticalState = level.VerticalState;
        }

        // Nessuna propagazione al gruppo: il livello è della singola clausola, ed è proprio ciò che due varianti
        // dicono diverso. Propagarlo le renderebbe tutte uguali.
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel,
        string? customLabel, CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        foreach (var r in rows)
        {
            r.ConditionAreaLabel = NullIfBlank(areaLabel);
            r.ConditionCustomLabel = NullIfBlank(customLabel);
        }

        // La pista non si tocca, e non è una dimenticanza: dipende dall'aeroporto, e la stessa sigla su scali
        // diversi è una pista diversa. Un accordo con quattro aeroporti lo rende ancora più vero di prima.
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        if (rows.Count == 0) return 0;

        var groups = rows.Where(r => r.VariantGroup is not null)
            .Select(r => (r.AgreementId, r.Direction, Group: r.VariantGroup!.Value)).Distinct().ToList();

        _db.AgreementClauses.RemoveRange(rows);
        foreach (var (agreementId, direction, group) in groups)
            await DissolveIfAloneAsync(agreementId, direction, group, ct);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    // ---- ripristino ---------------------------------------------------------------------------------

    public async Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default)
    {
        var id = await AddAgreementAsync(accCode, snapshot.Data, ct);

        foreach (var s in snapshot.Clauses.OrderBy(x => x.Order))
            _db.AgreementClauses.Add(ClauseFrom(id, s));

        await _db.SaveChangesAsync(ct);
        await EnsureOutlineIsSoundAsync(id, ct);
        return id;
    }

    public async Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses,
        CancellationToken ct = default)
    {
        if (clauses.Count == 0) return 0;

        // Solo gli accordi che esistono ancora: un annulla che ricreasse l'intestazione per rimetterci dentro
        // una clausola starebbe inventando un accordo che nessuno ha scritto.
        var ids = clauses.Select(c => c.AgreementId).Distinct().ToList();
        var alive = (await AgreementsOf(accCode)
            .Where(a => ids.Contains(a.Id))
            .Select(a => a.Id).ToListAsync(ct)).ToHashSet();

        var restored = clauses.Where(c => alive.Contains(c.AgreementId)).ToList();
        foreach (var c in restored) _db.AgreementClauses.Add(ClauseFrom(c.AgreementId, c.Clause));
        await _db.SaveChangesAsync(ct);

        foreach (var agreementId in restored.Select(c => c.AgreementId).Distinct())
            await EnsureOutlineIsSoundAsync(agreementId, ct);

        return restored.Count;
    }

    /// <summary>Una clausola dalla sua fotografia: qui la posizione (verso, ordine, gruppo, profondità)
    /// <b>viene dal dato</b>, ed è la differenza con <see cref="AddClauseAsync"/> — lì la decide il repository
    /// perché si sta scrivendo, qui si sta rimettendo.</summary>
    private static AgreementClause ClauseFrom(int agreementId, AgreementClauseSnapshot s)
    {
        var c = new AgreementClause
        {
            AgreementId = agreementId,
            Direction = s.Direction,
            Order = s.Order,
            VariantGroup = s.VariantGroup,
            VariantDepth = s.VariantDepth,
        };
        ApplyClause(c, s.Data);
        c.IsGroupWide = s.Data.IsGroupWide;
        return c;
    }

    /// <summary>
    /// Gli invarianti dell'outline dopo un ripristino, verso per verso. Una fotografia può essere vecchia di un
    /// archivio che nel frattempo è cambiato — la clausola di cui era eccezione può non esserci più — e non deve
    /// poter rientrare rotta: un'eccezione orfana descrive la clausola sbagliata, senza nessun errore a dirlo.
    /// </summary>
    private async Task EnsureOutlineIsSoundAsync(int agreementId, CancellationToken ct)
    {
        var all = await _db.AgreementClauses.Where(x => x.AgreementId == agreementId)
            .OrderBy(x => x.Direction).ThenBy(x => x.Order).ToListAsync(ct);

        foreach (var perDirection in all.GroupBy(x => x.Direction))
        {
            var depthByGroup = new Dictionary<int, int>();
            foreach (var r in perDirection)
            {
                if (r.VariantGroup is not int g) continue;

                if (r.IsGroupWide && r.VariantDepth > 0)
                    throw new ValidationException("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.");

                var previous = depthByGroup.TryGetValue(g, out var d) ? d : -1;
                if (r.VariantDepth > previous + 1)
                    throw new ValidationException(
                        $"La clausola «{r.Cops}» sta a profondità {r.VariantDepth} senza una clausola di " +
                        $"profondità {r.VariantDepth - 1} che la preceda.");

                depthByGroup[g] = r.VariantDepth;
            }
        }
    }

    // ---- attrezzi dell'outline ----------------------------------------------------------------------

    /// <summary>Le clausole di un <b>verso</b> di un accordo: è lo scopo dentro cui l'ordine ha significato.</summary>
    private IQueryable<AgreementClause> Scope(int agreementId, AgreementDirection direction) =>
        _db.AgreementClauses.Where(x => x.AgreementId == agreementId && x.Direction == direction);

    private Task<List<AgreementClause>> ScopeRowsAsync(AgreementClause c, CancellationToken ct) =>
        Scope(c.AgreementId, c.Direction).OrderBy(x => x.Order).ToListAsync(ct);

    private Task<List<AgreementClause>> GroupRowsAsync(AgreementClause c, int group, CancellationToken ct) =>
        Scope(c.AgreementId, c.Direction).Where(x => x.VariantGroup == group).OrderBy(x => x.Order).ToListAsync(ct);

    /// <summary>
    /// La clausola più tutto ciò che le appartiene: quelle che la seguono finché restano nel suo gruppo e più
    /// profonde di lei. È la definizione di sottoalbero in un outline, e serve ovunque una clausola si muova o
    /// si stacchi — perché muovere una capofila senza le sue eccezioni le riassegna a un'altra alternativa
    /// <b>senza un errore</b>: nessuna eccezione, nessun log, solo un accordo che dice un'altra cosa.
    /// </summary>
    private static List<AgreementClause> Subtree(List<AgreementClause> rowsInOrder, AgreementClause root)
    {
        var i = rowsInOrder.FindIndex(x => x.Id == root.Id);
        if (i < 0 || root.VariantGroup is null) return new List<AgreementClause> { root };
        var block = new List<AgreementClause> { rowsInOrder[i] };
        for (var k = i + 1; k < rowsInOrder.Count
                            && rowsInOrder[k].VariantGroup == root.VariantGroup
                            && rowsInOrder[k].VariantDepth > root.VariantDepth; k++)
            block.Add(rowsInOrder[k]);
        return block;
    }

    /// <summary>Risale dalla clausola in posizione <paramref name="index"/> alla radice del suo blocco: serve a
    /// scavalcare all'insù un vicino che è a sua volta un sottoalbero, e non finirgli in mezzo.</summary>
    private static AgreementClause RootOf(List<AgreementClause> rowsInOrder, int index)
    {
        var r = rowsInOrder[index];
        if (r.VariantGroup is null || r.VariantDepth == 0) return r;
        for (var k = index - 1; k >= 0; k--)
            if (rowsInOrder[k].VariantGroup == r.VariantGroup && rowsInOrder[k].VariantDepth < r.VariantDepth)
                return rowsInOrder[k];
        return r;
    }

    private static void Renumber(List<AgreementClause> rowsInOrder)
    {
        for (var i = 0; i < rowsInOrder.Count; i++) rowsInOrder[i].Order = i + 1;
    }

    /// <summary>Scioglie un gruppo rimasto con una sola clausola: un gruppo di uno non è un gruppo. Non salva —
    /// il chiamante è già dentro la sua <c>SaveChangesAsync</c>.</summary>
    private async Task DissolveIfAloneAsync(int agreementId, AgreementDirection direction, int group, CancellationToken ct)
    {
        var candidati = await Scope(agreementId, direction).Where(x => x.VariantGroup == group).ToListAsync(ct);

        // ⚠️ La query filtra su ciò che sta NEL DATABASE, ma qui siamo prima della SaveChanges: la clausola
        // appena sfilata (gruppo a null) o appena rimossa torna comunque indietro dal SELECT. Va riletto lo
        // stato in memoria, che è quello che sta per essere scritto — altrimenti il gruppo sembra ancora
        // affollato e non si scioglie mai.
        var remaining = candidati
            .Where(x => x.VariantGroup == group && _db.Entry(x).State != EntityState.Deleted)
            .ToList();
        if (remaining.Count > 1) return;
        foreach (var x in remaining) { x.VariantGroup = null; x.VariantDepth = 0; x.IsGroupWide = false; }
    }

    /// <summary>Copia editoriale di una clausola: i campi, non l'identità né la posizione.</summary>
    private static AgreementClause CopyOf(AgreementClause src) => new()
    {
        AgreementId = src.AgreementId,
        Direction = src.Direction,
        Cops = src.Cops,
        LevelValue = src.LevelValue,
        LevelUnit = src.LevelUnit,
        LevelConstraint = src.LevelConstraint,
        LevelSpecial = src.LevelSpecial,
        Parity = src.Parity,
        VerticalState = src.VerticalState,
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

    // ---- guardie e conversioni ----------------------------------------------------------------------

    /// <summary>
    /// Gli accordi che <b>riguardano</b> la ACC: ne è responsabile, o ha una parte fra i suoi settori. La stessa
    /// regola vale in lettura e in scrittura — chi vede un accordo può scriverlo, se ha il permesso sulla
    /// propria ACC.
    /// <para>È scritta come query e non come metodo su un'entità: un predicato C# dentro un <c>Where</c> EF non
    /// si traduce in SQL, e il difetto non si vede compilando — si vede al primo salvataggio.</para>
    /// </summary>
    private IQueryable<CoordinationAgreement> AgreementsOf(string accCode) =>
        _db.CoordinationAgreements.Where(a => a.OwnerAcc!.Code == accCode
                                              || a.Parties.Any(p => p.Sector!.Acc!.Code == accCode));

    /// <summary>Le clausole degli accordi che riguardano la ACC. Stessa regola di <see cref="AgreementsOf"/>,
    /// dall'altro capo della relazione.</summary>
    private IQueryable<AgreementClause> ClausesOf(string accCode) =>
        _db.AgreementClauses.Where(x => x.Agreement!.OwnerAcc!.Code == accCode
                                        || x.Agreement!.Parties.Any(p => p.Sector!.Acc!.Code == accCode));

    private async Task<int> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(a => a.Code == accCode).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

    private async Task<CoordinationAgreement> AgreementAsync(string accCode, int agreementId, CancellationToken ct) =>
        await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == agreementId, ct)
            ?? throw new InvalidOperationException($"Accordo {agreementId} non riguarda la ACC {accCode}.");

    /// <summary>Come <see cref="AgreementAsync"/> ma con parti e aeroporti tracciati: serve alla riscrittura
    /// dell'intestazione, che li sostituisce in blocco.</summary>
    private async Task<CoordinationAgreement> TrackedAgreementAsync(string accCode, int agreementId, CancellationToken ct) =>
        await AgreementsOf(accCode)
            .Include(x => x.Parties)
            .Include(x => x.Airports)
            .FirstOrDefaultAsync(x => x.Id == agreementId, ct)
        ?? throw new InvalidOperationException($"Accordo {agreementId} non riguarda la ACC {accCode}.");

    /// <summary>L'accordo col grafo completo e tracciato — parti <b>coi loro settori</b>, aeroporti, clausole.
    /// Serve a chi deve <b>confrontare</b> due accordi, non solo scrivere su uno.</summary>
    private async Task<CoordinationAgreement> FullAgreementAsync(string accCode, int agreementId, CancellationToken ct) =>
        await AgreementsOf(accCode)
            .Include(x => x.OwnerAcc)
            .Include(x => x.Parties).ThenInclude(p => p.Sector)
            .Include(x => x.Airports)
            .Include(x => x.Clauses)
            .FirstOrDefaultAsync(x => x.Id == agreementId, ct)
        ?? throw new InvalidOperationException($"Accordo {agreementId} non riguarda la ACC {accCode}.");

    private async Task<AgreementClause> ClauseInAccAsync(string accCode, int clauseId, CancellationToken ct) =>
        await ClausesOf(accCode).FirstOrDefaultAsync(x => x.Id == clauseId, ct)
            ?? throw new InvalidOperationException($"Clausola {clauseId} non riguarda la ACC {accCode}.");

    /// <summary>Le clausole indicate che riguardano davvero la ACC: il filtro è la guardia, non un dettaglio.</summary>
    private async Task<List<AgreementClause>> ClausesInAccAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct) =>
        clauseIds.Count == 0
            ? new List<AgreementClause>()
            : await ClausesOf(accCode).Where(x => clauseIds.Contains(x.Id)).ToListAsync(ct);

    private static void ApplyClause(AgreementClause c, AgreementClauseInput i)
    {
        // I punti si normalizzano passando dall'elenco: spazi, vuoti e separatori doppi spariscono qui, una
        // volta, invece che in ogni posto che li rilegge.
        c.Cops = CopList.Format(CopList.Parse(i.Cops));
        c.LevelValue = i.LevelConstraint == LevelConstraint.Special ? null : i.LevelValue;
        c.LevelUnit = i.LevelUnit;
        c.LevelConstraint = i.LevelConstraint;
        c.LevelSpecial = i.LevelConstraint == LevelConstraint.Special ? NullIfBlank(i.LevelSpecial) : null;
        c.Parity = i.Parity;
        c.VerticalState = i.VerticalState;

        c.ConditionLabel = NullIfBlank(i.ConditionLabel);
        c.ConditionRefId = c.ConditionLabel is null ? null : i.ConditionRefId;
        c.ConditionAreaLabel = NullIfBlank(i.ConditionAreaLabel);
        c.ConditionCustomLabel = NullIfBlank(i.ConditionCustomLabel);

        // Senza tipo non c'è trasferimento distinto: i campi correlati si azzerano, così una clausola tornata a
        // «coincide con l'ingresso» non si porta dietro un livello fantasma.
        c.HandoffKind = i.HandoffKind;
        c.HandoffLabel = i.HandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.HandoffLabel);
        c.HandoffLevelValue = i.HandoffKind == TransferHandoffKind.Unspecified ? null : i.HandoffLevelValue;
        c.HandoffLevelUnit = i.HandoffLevelUnit;
        c.HandoffLevelConstraint = i.HandoffLevelConstraint;
        c.CommsHandoffKind = i.CommsHandoffKind;
        c.CommsHandoffLabel = i.CommsHandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.CommsHandoffLabel);

        c.SpeedConstraint = i.SpeedConstraint;
        c.SpeedValue = i.SpeedConstraint == SpeedConstraint.Unspecified ? null : i.SpeedValue;

        // «Scavalca le alternative» ha senso solo dentro un gruppo: fuori non ci sono alternative da scavalcare.
        c.IsGroupWide = i.IsGroupWide && c.VariantGroup is not null;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static AgreementRow Map(CoordinationAgreement a) => new()
    {
        Id = a.Id,
        OwnerAccCode = a.OwnerAcc?.Code ?? "",
        TrafficKind = a.TrafficKind,
        Description = a.Description,
        Order = a.Order,
        Parties = a.Parties.OrderBy(p => p.Side).ThenBy(p => p.Order)
            .Select(p => new AgreementPartyRow(p.Side, p.SectorId, p.Sector?.Callsign ?? $"#{p.SectorId}", p.Order))
            .ToList(),
        Airports = a.Airports.OrderBy(x => x.Order)
            .Select(x => new AgreementAirportRow(x.Icao, x.Name, x.Order)).ToList(),
        Clauses = a.Clauses.OrderBy(c => c.Direction).ThenBy(c => c.Order).Select(MapClause).ToList(),
    };

    private static AgreementClauseRow MapClause(AgreementClause c) => new()
    {
        Id = c.Id,
        Direction = c.Direction,
        Cops = c.Cops,
        LevelValue = c.LevelValue,
        LevelUnit = c.LevelUnit,
        LevelConstraint = c.LevelConstraint,
        LevelSpecial = c.LevelSpecial,
        Parity = c.Parity,
        VerticalState = c.VerticalState,
        ConditionLabel = c.ConditionLabel,
        ConditionRefId = c.ConditionRefId,
        ConditionAreaLabel = c.ConditionAreaLabel,
        ConditionCustomLabel = c.ConditionCustomLabel,
        HandoffKind = c.HandoffKind,
        HandoffLabel = c.HandoffLabel,
        HandoffLevelValue = c.HandoffLevelValue,
        HandoffLevelUnit = c.HandoffLevelUnit,
        HandoffLevelConstraint = c.HandoffLevelConstraint,
        CommsHandoffKind = c.CommsHandoffKind,
        CommsHandoffLabel = c.CommsHandoffLabel,
        SpeedValue = c.SpeedValue,
        SpeedConstraint = c.SpeedConstraint,
        VariantGroup = c.VariantGroup,
        VariantDepth = c.VariantDepth,
        IsGroupWide = c.IsGroupWide,
        Order = c.Order,
    };
}

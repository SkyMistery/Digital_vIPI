using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAgreementRepository"/>: accordi, sezioni, aeroporti e clausole.
///
/// <para><b>Lo scopo dell'outline è la SEZIONE.</b> Tutto ciò che sposta, annida o scioglie ragiona sulle
/// clausole di una sola sezione — quelle di un'altra non sono alternative delle prime, sono un'altra tabella.
/// Fino al 18 agosto 2026 lo scopo era la coppia <c>(accordo, verso)</c>, che è la stessa cosa detta con due
/// chiavi invece di una.</para>
/// </summary>
public sealed class EfAgreementRepository : IAgreementRepository
{
    private readonly VipiDbContext _db;
    public EfAgreementRepository(VipiDbContext db) => _db = db;

    // ---- lettura ------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default)
    {
        var agreements = await AgreementsOf(accCode).AsNoTracking()
            .Include(a => a.OwnerAcc)
            .Include(a => a.SideASector)
            .Include(a => a.SideBSector)
            .Include(a => a.Sections).ThenInclude(s => s.Airports)
            .Include(a => a.Sections).ThenInclude(s => s.Clauses)
            .OrderBy(a => a.Order).ThenBy(a => a.Id)
            .ToListAsync(ct);

        return agreements.Select(Map).ToList();
    }

    public async Task<int?> FindByPairAsync(string accCode, int sectorX, int sectorY, CancellationToken ct = default)
    {
        var (a, b) = Canonical(sectorX, sectorY);
        return await AgreementsOf(accCode)
            .Where(x => x.SideASectorId == a && x.SideBSectorId == b)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    // ---- accordo ------------------------------------------------------------------------------------

    public async Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default)
    {
        var accId = await AccIdAsync(accCode, ct);
        var (sideA, sideB) = Canonical(input.SideASectorId, input.SideBSectorId);

        // La coppia è unica anche per indice; qui si risponde con una frase invece che con una violazione di
        // vincolo, e l'editor può proporre di aprire quello che c'è.
        if (await _db.CoordinationAgreements.AnyAsync(x => x.SideASectorId == sideA && x.SideBSectorId == sideB, ct))
            throw new ValidationException("Fra questi due enti esiste già un accordo: aggiungi una sezione a quello.");

        var order = (await _db.CoordinationAgreements.Where(a => a.OwnerAccId == accId)
            .MaxAsync(a => (int?)a.Order, ct) ?? 0) + 1;

        var a = new CoordinationAgreement
        {
            OwnerAccId = accId,
            SideASectorId = sideA,
            SideBSectorId = sideB,
            Note = NullIfBlank(input.Note),
            Order = order,
        };
        _db.CoordinationAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    /// <summary>
    /// Cambia i due capi e la nota.
    /// <para>⚠️ <b>Se i lati si scambiano, i versi delle sezioni si ribaltano con loro.</b> I lati stanno in
    /// forma canonica (id minore = A), quindi sostituire un ente può spostare l'altro dall'altra parte: lasciare
    /// i versi com'erano farebbe dire a ogni sezione il contrario di ciò che c'era scritto, <b>senza un
    /// errore</b>. È l'unico posto dove la canonizzazione si vede, ed è la ragione per cui il verso ha dovuto
    /// lasciare la clausola per la sezione.</para>
    /// </summary>
    public async Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default)
    {
        var a = await AgreementsOf(accCode).Include(x => x.Sections)
                    .FirstOrDefaultAsync(x => x.Id == agreementId, ct)
                ?? throw new InvalidOperationException(Lingua($"Accordo {agreementId} non riguarda la ACC {accCode}.", $"Agreement {agreementId} does not belong to ACC {accCode}."));

        var (sideA, sideB) = Canonical(input.SideASectorId, input.SideBSectorId);
        if (await _db.CoordinationAgreements
                .AnyAsync(x => x.Id != agreementId && x.SideASectorId == sideA && x.SideBSectorId == sideB, ct))
            throw new ValidationException("Fra questi due enti esiste già un altro accordo.");

        var swapped = (a.SideASectorId == sideB && sideB != a.SideBSectorId)
                      || (a.SideBSectorId == sideA && sideA != a.SideASectorId);

        a.SideASectorId = sideA;
        a.SideBSectorId = sideB;
        a.Note = NullIfBlank(input.Note);

        if (swapped)
            foreach (var s in a.Sections) s.Direction = Flip(s.Direction);

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default)
    {
        var a = await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == agreementId, ct);
        if (a is null) return;
        _db.CoordinationAgreements.Remove(a);   // sezioni, aeroporti e clausole seguono in cascade
        await _db.SaveChangesAsync(ct);
    }

    // ---- sezioni ------------------------------------------------------------------------------------

    public async Task<int> AddSectionAsync(string accCode, int agreementId, AgreementSectionInput input,
        CancellationToken ct = default)
    {
        var a = await AgreementAsync(accCode, agreementId, ct);
        var order = (await _db.AgreementSections.Where(s => s.AgreementId == a.Id)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;

        var section = new AgreementSection { AgreementId = a.Id, Order = order };
        ApplySection(section, input);
        _db.AgreementSections.Add(section);
        await _db.SaveChangesAsync(ct);
        return section.Id;
    }

    public async Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).Include(s => s.Airports)
                          .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        ApplySection(section, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null) return;
        _db.AgreementSections.Remove(section);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, CancellationToken ct = default)
    {
        var src = await SectionsOf(accCode)
                      .Include(s => s.Airports).Include(s => s.Clauses)
                      .FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                  ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        var reverse = Flip(src.Direction);
        var key = AirportKey(src.Airports.Select(x => x.Icao));

        // Se il reciproco c'è già non si tocca: sovrascriverlo sarebbe buttare via ciò che qualcuno ha scritto,
        // e accodarlo produrrebbe un doppione di ogni clausola.
        var esiste = await _db.AgreementSections.Include(s => s.Airports)
            .Where(s => s.AgreementId == src.AgreementId && s.Kind == src.Kind && s.Direction == reverse)
            .ToListAsync(ct);
        if (esiste.Any(s => AirportKey(s.Airports.Select(x => x.Icao)) == key)) return null;

        var order = (await _db.AgreementSections.Where(s => s.AgreementId == src.AgreementId)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;

        var copy = new AgreementSection
        {
            AgreementId = src.AgreementId,
            Kind = src.Kind,
            Direction = reverse,
            Description = src.Description,
            Order = order,
        };
        var airportOrder = 0;
        foreach (var apt in src.Airports.OrderBy(x => x.Order))
            copy.Airports.Add(new AgreementAirport { Icao = apt.Icao, Name = apt.Name, Order = ++airportOrder });

        // I gruppi di varianti si rinumerano dentro la copia: sono progressivi per accordo, e riusarli farebbe
        // sembrare le clausole del verso opposto varianti delle prime.
        var nextGroup = await ClausesOfAgreement(src.AgreementId).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        var groupMap = new Dictionary<int, int>();

        foreach (var c in src.Clauses.OrderBy(x => x.Order))
        {
            var copia = CopyOf(c);
            copia.Order = c.Order;
            copia.VariantDepth = c.VariantDepth;
            copia.IsGroupWide = c.IsGroupWide;
            if (c.VariantGroup is int g)
            {
                if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                copia.VariantGroup = mapped;
            }
            copy.Clauses.Add(copia);
        }

        _db.AgreementSections.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default)
    {
        if (keepId == absorbId) return 0;

        var keep = await SectionsOf(accCode).Include(s => s.Airports)
                       .FirstOrDefaultAsync(s => s.Id == keepId, ct)
                   ?? throw new InvalidOperationException(Lingua($"Sezione {keepId} non riguarda la ACC {accCode}.", $"Section {keepId} does not belong to ACC {accCode}."));
        var absorb = await SectionsOf(accCode).Include(s => s.Airports)
                         .FirstOrDefaultAsync(s => s.Id == absorbId, ct)
                     ?? throw new InvalidOperationException(Lingua($"Sezione {absorbId} non riguarda la ACC {accCode}.", $"Section {absorbId} does not belong to ACC {accCode}."));

        // ⚠️ Le condizioni si rivalidano QUI e non si dànno per buone dalla segnalazione: fra il cruscotto e il
        // tasto l'archivio può essere cambiato, e unire due tabelle che dicono cose diverse le mescolerebbe
        // senza che nessuno possa più separarle.
        if (keep.AgreementId != absorb.AgreementId || keep.Kind != absorb.Kind || keep.Direction != absorb.Direction
            || AirportKey(keep.Airports.Select(x => x.Icao)) != AirportKey(absorb.Airports.Select(x => x.Icao)))
            throw new ValidationException(Lingua("Le due sezioni non dicono la stessa cosa: si uniscono solo le gemelle.", "The two sections do not say the same thing: only twins can be merged."));

        var moving = await _db.AgreementClauses.Where(c => c.SectionId == absorbId)
            .OrderBy(c => c.Order).ToListAsync(ct);

        var order = await _db.AgreementClauses.Where(c => c.SectionId == keepId)
            .MaxAsync(c => (int?)c.Order, ct) ?? 0;
        var nextGroup = await ClausesOfAgreement(keep.AgreementId).MaxAsync(c => (int?)c.VariantGroup, ct) ?? 0;
        var groupMap = new Dictionary<int, int>();

        foreach (var c in moving)
        {
            c.SectionId = keepId;
            c.Order = ++order;
            if (c.VariantGroup is int g)
            {
                if (!groupMap.TryGetValue(g, out var mapped)) groupMap[g] = mapped = ++nextGroup;
                c.VariantGroup = mapped;
            }
        }

        // Il guscio se ne va DOPO che le clausole hanno cambiato padre: cancellarlo prima le porterebbe con sé
        // in cascade, e l'unione perderebbe proprio ciò che doveva salvare.
        await _db.SaveChangesAsync(ct);
        _db.AgreementSections.Remove(absorb);
        await _db.SaveChangesAsync(ct);

        return moving.Count;
    }

    /// <summary>
    /// Riscrive gli aeroporti al posto di aggiornarli uno per uno. La differenza si vede quando l'editore ne
    /// toglie uno: con l'aggiornamento «per differenza» servirebbe sapere quale riga togliere, e l'editor
    /// dovrebbe portarsi dietro gli id — cioè conoscere la persistenza per modificare un elenco. Qui l'elenco è
    /// il dato, e chi lo scrive lo scrive intero.
    /// </summary>
    private void ApplySection(AgreementSection s, AgreementSectionInput i)
    {
        s.Kind = i.Kind;
        s.Direction = i.Direction;
        s.Description = NullIfBlank(i.Description);

        _db.AgreementAirports.RemoveRange(s.Airports);
        s.Airports.Clear();
        var order = 0;
        foreach (var apt in i.Airports)
            s.Airports.Add(new AgreementAirport
            {
                Icao = apt.Icao.Trim().ToUpperInvariant(),
                // Il nome si tiene solo per gli scali fuori catalogo, dove è l'unica fonte. Per gli altri
                // arriva dal catalogo, e una copia qui divergerebbe alla prima rinomina.
                Name = NullIfBlank(apt.Name),
                Order = ++order,
            });
    }

    // ---- clausole -----------------------------------------------------------------------------------

    public async Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input,
        CancellationToken ct = default)
    {
        var section = await SectionsOf(accCode).FirstOrDefaultAsync(s => s.Id == sectionId, ct)
                      ?? throw new InvalidOperationException(Lingua($"Sezione {sectionId} non riguarda la ACC {accCode}.", $"Section {sectionId} does not belong to ACC {accCode}."));

        var order = (await Scope(section.Id).MaxAsync(c => (int?)c.Order, ct) ?? 0) + 1;

        var c = new AgreementClause { SectionId = section.Id, Order = order };
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
                throw new ValidationException(Lingua("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.", "An «in any case» clause cannot be the exception to another one."));

            // I PUNTI sono l'identità dell'accordo dentro un gruppo — le varianti sono lo stesso accordo detto a
            // condizioni diverse — quindi cambiarli su una clausola li cambia sulle sorelle. Propagare è meglio
            // che rifiutare: l'invariante resta vera senza chiedere di ripetere la stessa modifica su ognuna.
            foreach (var s in await _db.AgreementClauses
                         .Where(x => x.SectionId == c.SectionId && x.VariantGroup == group && x.Id != c.Id)
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
        if (group is int g) await DissolveIfAloneAsync(c.SectionId, g, ct);
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

        // Il gruppo nasce alla prima variante: progressivo per ACCORDO (non per sezione), così due gruppi «1» di
        // sezioni diverse non si somigliano leggendo l'archivio a mano.
        if (src.VariantGroup is null)
            src.VariantGroup = await NextGroupAsync(src.SectionId, ct);
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

        foreach (var x in await Scope(src.SectionId).Where(x => x.Order > after.Order).ToListAsync(ct))
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
        var newGroup = moved.Count > 1 ? await NextGroupAsync(c.SectionId, ct) : (int?)null;
        foreach (var x in moved) x.VariantGroup = newGroup;

        await DissolveIfAloneAsync(c.SectionId, group, ct);
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

        // Il vicino nella stessa sezione è a sua volta un blocco: si scavalca intero, non riga per riga.
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
        // Fra sezioni diverse non si trascina: cambiare la sezione di una clausola è dire un'altra cosa, non
        // spostarla — sono due tabelle, e il traffico o il verso cambierebbero sotto la riga.
        if (c.Id == target.Id || c.SectionId != target.SectionId) return;

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

        var newGroup = await NextGroupAsync(c.SectionId, ct);
        var order = await Scope(c.SectionId).MaxAsync(x => (int?)x.Order, ct) ?? 0;

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
        // diversi è una pista diversa. Una sezione con quattro aeroporti lo rende ancora più vero di prima.
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default)
    {
        var rows = await ClausesInAccAsync(accCode, clauseIds, ct);
        if (rows.Count == 0) return 0;

        var groups = rows.Where(r => r.VariantGroup is not null)
            .Select(r => (r.SectionId, Group: r.VariantGroup!.Value)).Distinct().ToList();

        _db.AgreementClauses.RemoveRange(rows);
        foreach (var (sectionId, group) in groups)
            await DissolveIfAloneAsync(sectionId, group, ct);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    // ---- ripristino ---------------------------------------------------------------------------------

    public async Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default)
    {
        // ⚠️ Il ripristino è FUORI dalle regole di proposito: un annulla che rifiutasse di rimettere ciò che ha
        // appena cancellato sarebbe peggio della regola. Passa comunque dalla forma canonica, perché quella non
        // è una regola editoriale ma la chiave dell'archivio.
        var accId = await AccIdAsync(accCode, ct);
        var (sideA, sideB) = Canonical(snapshot.Data.SideASectorId, snapshot.Data.SideBSectorId);
        var order = (await _db.CoordinationAgreements.Where(a => a.OwnerAccId == accId)
            .MaxAsync(a => (int?)a.Order, ct) ?? 0) + 1;

        var a = new CoordinationAgreement
        {
            OwnerAccId = accId,
            SideASectorId = sideA,
            SideBSectorId = sideB,
            Note = NullIfBlank(snapshot.Data.Note),
            Order = order,
        };
        foreach (var s in snapshot.Sections.OrderBy(x => x.Order))
            a.Sections.Add(SectionFrom(s));

        _db.CoordinationAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        await EnsureOutlineIsSoundAsync(a.Id, ct);
        return a.Id;
    }

    public async Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore restore, CancellationToken ct = default)
    {
        // Solo negli accordi che esistono ancora: ricrearne uno per ospitare la sezione sarebbe inventare una
        // relazione che nessuno ha scritto.
        var a = await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == restore.AgreementId, ct);
        if (a is null) return null;

        var section = SectionFrom(restore.Section);
        section.AgreementId = a.Id;
        _db.AgreementSections.Add(section);
        await _db.SaveChangesAsync(ct);
        await EnsureOutlineIsSoundAsync(a.Id, ct);
        return section.Id;
    }

    public async Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses,
        CancellationToken ct = default)
    {
        if (clauses.Count == 0) return 0;

        var ids = clauses.Select(c => c.SectionId).Distinct().ToList();
        var alive = (await SectionsOf(accCode).Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct))
            .ToHashSet();

        var restored = clauses.Where(c => alive.Contains(c.SectionId)).ToList();
        foreach (var c in restored) _db.AgreementClauses.Add(ClauseFrom(c.SectionId, c.Clause));
        await _db.SaveChangesAsync(ct);

        foreach (var agreementId in await _db.AgreementSections
                     .Where(s => alive.Contains(s.Id)).Select(s => s.AgreementId).Distinct().ToListAsync(ct))
            await EnsureOutlineIsSoundAsync(agreementId, ct);

        return restored.Count;
    }

    private static AgreementSection SectionFrom(AgreementSectionSnapshot s)
    {
        var section = new AgreementSection
        {
            Kind = s.Data.Kind,
            Direction = s.Data.Direction,
            Description = NullIfBlank(s.Data.Description),
            Order = s.Order,
        };
        var order = 0;
        foreach (var apt in s.Data.Airports)
            section.Airports.Add(new AgreementAirport
            {
                Icao = apt.Icao.Trim().ToUpperInvariant(),
                Name = NullIfBlank(apt.Name),
                Order = ++order,
            });
        foreach (var c in s.Clauses.OrderBy(x => x.Order))
            section.Clauses.Add(ClauseFrom(0, c));
        return section;
    }

    /// <summary>Una clausola dalla sua fotografia: qui la posizione (ordine, gruppo, profondità) <b>viene dal
    /// dato</b>, ed è la differenza con <see cref="AddClauseAsync"/> — lì la decide il repository perché si sta
    /// scrivendo, qui si sta rimettendo.</summary>
    private static AgreementClause ClauseFrom(int sectionId, AgreementClauseSnapshot s)
    {
        var c = new AgreementClause
        {
            Order = s.Order,
            VariantGroup = s.VariantGroup,
            VariantDepth = s.VariantDepth,
        };
        if (sectionId > 0) c.SectionId = sectionId;
        ApplyClause(c, s.Data);
        c.IsGroupWide = s.Data.IsGroupWide;
        return c;
    }

    /// <summary>
    /// Gli invarianti dell'outline dopo un ripristino, sezione per sezione. Una fotografia può essere vecchia di
    /// un archivio che nel frattempo è cambiato — la clausola di cui era eccezione può non esserci più — e non
    /// deve poter rientrare rotta: un'eccezione orfana descrive la clausola sbagliata, senza nessun errore a
    /// dirlo.
    /// </summary>
    private async Task EnsureOutlineIsSoundAsync(int agreementId, CancellationToken ct)
    {
        var all = await ClausesOfAgreement(agreementId)
            .OrderBy(x => x.SectionId).ThenBy(x => x.Order).ToListAsync(ct);

        foreach (var perSection in all.GroupBy(x => x.SectionId))
        {
            var depthByGroup = new Dictionary<int, int>();
            foreach (var r in perSection)
            {
                if (r.VariantGroup is not int g) continue;

                if (r.IsGroupWide && r.VariantDepth > 0)
                    throw new ValidationException(Lingua("Una clausola «in ogni caso» non può essere l'eccezione di un'altra.", "An «in any case» clause cannot be the exception to another one."));

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

    /// <summary>Le clausole di una <b>sezione</b>: è lo scopo dentro cui l'ordine ha significato.</summary>
    private IQueryable<AgreementClause> Scope(int sectionId) =>
        _db.AgreementClauses.Where(x => x.SectionId == sectionId);

    /// <summary>Le clausole di tutto l'accordo: i gruppi di varianti sono progressivi per accordo, e per
    /// numerarne uno nuovo bisogna vederli tutti.</summary>
    private IQueryable<AgreementClause> ClausesOfAgreement(int agreementId) =>
        _db.AgreementClauses.Where(x => x.Section!.Agreement!.Id == agreementId);

    private Task<int> AgreementIdOfAsync(int sectionId, CancellationToken ct) =>
        _db.AgreementSections.Where(s => s.Id == sectionId).Select(s => s.AgreementId).FirstAsync(ct);

    /// <summary>Il prossimo numero di gruppo libero nell'accordo che ospita la sezione.</summary>
    private async Task<int> NextGroupAsync(int sectionId, CancellationToken ct) =>
        (await ClausesOfAgreement(await AgreementIdOfAsync(sectionId, ct))
            .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;

    private Task<List<AgreementClause>> ScopeRowsAsync(AgreementClause c, CancellationToken ct) =>
        Scope(c.SectionId).OrderBy(x => x.Order).ToListAsync(ct);

    private Task<List<AgreementClause>> GroupRowsAsync(AgreementClause c, int group, CancellationToken ct) =>
        Scope(c.SectionId).Where(x => x.VariantGroup == group).OrderBy(x => x.Order).ToListAsync(ct);

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
    private async Task DissolveIfAloneAsync(int sectionId, int group, CancellationToken ct)
    {
        var candidati = await Scope(sectionId).Where(x => x.VariantGroup == group).ToListAsync(ct);

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
        SectionId = src.SectionId,
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
    /// Gli accordi che <b>riguardano</b> la ACC: ne è responsabile, o ha un capo fra i suoi settori. La stessa
    /// regola vale in lettura e in scrittura — chi vede un accordo può scriverlo, se ha il permesso sulla
    /// propria ACC.
    /// <para>È scritta come query e non come metodo su un'entità: un predicato C# dentro un <c>Where</c> EF non
    /// si traduce in SQL, e il difetto non si vede compilando — si vede al primo salvataggio.</para>
    /// </summary>
    private IQueryable<CoordinationAgreement> AgreementsOf(string accCode) =>
        _db.CoordinationAgreements.Where(a => a.OwnerAcc!.Code == accCode
                                              || a.SideASector!.Acc!.Code == accCode
                                              || a.SideBSector!.Acc!.Code == accCode);

    private IQueryable<AgreementSection> SectionsOf(string accCode) =>
        _db.AgreementSections.Where(s => s.Agreement!.OwnerAcc!.Code == accCode
                                         || s.Agreement!.SideASector!.Acc!.Code == accCode
                                         || s.Agreement!.SideBSector!.Acc!.Code == accCode);

    /// <summary>Le clausole degli accordi che riguardano la ACC. Stessa regola di <see cref="AgreementsOf"/>,
    /// due relazioni più in là.</summary>
    private IQueryable<AgreementClause> ClausesOf(string accCode) =>
        _db.AgreementClauses.Where(x => x.Section!.Agreement!.OwnerAcc!.Code == accCode
                                        || x.Section!.Agreement!.SideASector!.Acc!.Code == accCode
                                        || x.Section!.Agreement!.SideBSector!.Acc!.Code == accCode);

    private async Task<int> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(a => a.Code == accCode).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

    private async Task<CoordinationAgreement> AgreementAsync(string accCode, int agreementId, CancellationToken ct) =>
        await AgreementsOf(accCode).FirstOrDefaultAsync(x => x.Id == agreementId, ct)
            ?? throw new InvalidOperationException(Lingua($"Accordo {agreementId} non riguarda la ACC {accCode}.", $"Agreement {agreementId} does not belong to ACC {accCode}."));

    private async Task<AgreementClause> ClauseInAccAsync(string accCode, int clauseId, CancellationToken ct) =>
        await ClausesOf(accCode).FirstOrDefaultAsync(x => x.Id == clauseId, ct)
            ?? throw new InvalidOperationException(Lingua($"Clausola {clauseId} non riguarda la ACC {accCode}.", $"Clause {clauseId} does not belong to ACC {accCode}."));

    /// <summary>Le clausole indicate che riguardano davvero la ACC: il filtro è la guardia, non un dettaglio.</summary>
    private async Task<List<AgreementClause>> ClausesInAccAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct) =>
        clauseIds.Count == 0
            ? new List<AgreementClause>()
            : await ClausesOf(accCode).Where(x => clauseIds.Contains(x.Id)).ToListAsync(ct);

    /// <summary>I due lati in forma canonica: id minore = A. È la chiave dell'unicità, non una scelta
    /// editoriale — e non ha significato perché il verso sta sulla sezione.</summary>
    private static (int A, int B) Canonical(int x, int y) => x <= y ? (x, y) : (y, x);

    private static AgreementDirection Flip(AgreementDirection d) =>
        d == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

    /// <summary>La chiave con cui due sezioni «hanno gli stessi scali»: normalizzata e ordinata, perché
    /// «LIBD·LIBR» e «LIBR·LIBD» sono lo stesso gruppo.</summary>
    private static string AirportKey(IEnumerable<string> icaos) =>
        string.Join("·", icaos.Select(x => x.Trim().ToUpperInvariant()).OrderBy(x => x, StringComparer.Ordinal));

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
        SideA = new AgreementEndpoint(a.SideASectorId, a.SideASector?.Callsign ?? $"#{a.SideASectorId}"),
        SideB = new AgreementEndpoint(a.SideBSectorId, a.SideBSector?.Callsign ?? $"#{a.SideBSectorId}"),
        Note = a.Note,
        Order = a.Order,
        Sections = AgreementSectionOrder.Sort(a.Sections.Select(MapSection)),
    };

    private static AgreementSectionRow MapSection(AgreementSection s) => new()
    {
        Id = s.Id,
        Kind = s.Kind,
        Direction = s.Direction,
        Description = s.Description,
        Order = s.Order,
        Airports = s.Airports.OrderBy(x => x.Order).Select(x => new AgreementAirportRow(x.Icao, x.Name, x.Order)).ToList(),
        Clauses = s.Clauses.OrderBy(c => c.Order).ThenBy(c => c.Id).Select(MapClause).ToList(),
    };

    private static AgreementClauseRow MapClause(AgreementClause c) => new()
    {
        Id = c.Id,
        SectionId = c.SectionId,
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

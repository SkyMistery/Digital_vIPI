using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentUnionRepository"/>
public sealed class EfDocumentUnionRepository : IDocumentUnionRepository
{
    private readonly VipiDbContext _db;
    public EfDocumentUnionRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<UnionRow>> ListAsync(CancellationToken ct = default) =>
        await _db.DocumentUnionMembers.AsNoTracking()
            .OrderBy(m => m.UnionId).ThenBy(m => m.Order)
            .Select(m => new UnionRow(m.UnionId, m.Id, m.DocumentId, m.Order))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<UnionRow>> ByDocumentAsync(int documentId, CancellationToken ct = default)
    {
        // Due passi e non una sotto-query: il primo dice SE il documento è unito, e nel caso normale — che è
        // «non lo è» — il secondo non parte affatto. Questa domanda la fa ogni apertura di ogni viewer.
        var unionId = await _db.DocumentUnionMembers.AsNoTracking()
            .Where(m => m.DocumentId == documentId)
            .Select(m => (int?)m.UnionId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return unionId is null ? Array.Empty<UnionRow>() : await ByUnionAsync(unionId.Value, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UnionRow>> ByUnionAsync(int unionId, CancellationToken ct = default) =>
        await _db.DocumentUnionMembers.AsNoTracking()
            .Where(m => m.UnionId == unionId)
            .OrderBy(m => m.Order)
            .Select(m => new UnionRow(m.UnionId, m.Id, m.DocumentId, m.Order))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<int> CreateAsync(int hostDocumentId, int guestDocumentId, int createdByUserId,
                                       CancellationToken ct = default)
    {
        var unione = new DocumentUnion { CreatedUtc = DateTime.UtcNow, CreatedByUserId = createdByUserId };
        unione.Members.Add(new DocumentUnionMember { DocumentId = hostDocumentId, Order = 0 });
        unione.Members.Add(new DocumentUnionMember { DocumentId = guestDocumentId, Order = 1 });
        _db.DocumentUnions.Add(unione);
        await SalvaAsync(ct).ConfigureAwait(false);
        return unione.Id;
    }

    public async Task AddMemberAsync(int unionId, int documentId, CancellationToken ct = default)
    {
        var coda = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == unionId)
            .MaxAsync(m => (int?)m.Order, ct).ConfigureAwait(false) ?? -1;
        _db.DocumentUnionMembers.Add(new DocumentUnionMember
        {
            UnionId = unionId,
            DocumentId = documentId,
            Order = coda + 1,
        });
        await SalvaAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Salva, e traduce la violazione dell'indice unico in una frase leggibile.
    ///
    /// <para>⚠️ Il servizio controlla <b>prima</b> che il documento non sia già unito altrove, e quel
    /// controllo esiste proprio per dare un messaggio col titolo del documento invece di un errore tecnico.
    /// Ma fra il controllo e la scrittura c'è una finestra, e due redattori che uniscono lo stesso documento
    /// nello stesso istante ci passano: chi arriva secondo vedeva la <c>DbUpdateException</c> nuda, che è
    /// esattamente ciò che il controllo anticipato doveva risparmiargli.</para>
    ///
    /// <para>⚠️ Una transazione non basterebbe: l'indice unico è il guardiano, e due transazioni
    /// concorrenti lo violano lo stesso. Quel che serve è <b>raccontare</b> la violazione.</para>
    /// </summary>
    private async Task SalvaAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw new ValidationException(Lingua(
                "Questo documento è appena stato unito ad altri da qualcun altro: ricarica la pagina per vedere com'è adesso.",
                "This document has just been joined to others by someone else: reload the page to see how it stands now."));
        }
    }

    public async Task RemoveMemberAsync(int memberId, CancellationToken ct = default)
    {
        // ⚠️ Niente ExecuteDelete: desincronizza il change-tracker, e qui subito dopo si rinumerano i
        // fratelli con lo stesso context (memoria: EF ExecuteDelete nei repository).
        var riga = await _db.DocumentUnionMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct)
                                                 .ConfigureAwait(false);
        if (riga is null) return;

        var unionId = riga.UnionId;
        _db.DocumentUnionMembers.Remove(riga);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await RinumeraAsync(unionId, ct).ConfigureAwait(false);
    }

    public async Task DissolveAsync(int unionId, CancellationToken ct = default)
    {
        var unione = await _db.DocumentUnions
            .Include(u => u.Members)
            .FirstOrDefaultAsync(u => u.Id == unionId, ct).ConfigureAwait(false);
        if (unione is null) return;
        // I membri se ne vanno con la cascata; i DOCUMENTI non si toccano — sciogliere non è eliminare.
        _db.DocumentUnions.Remove(unione);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MoveAsync(int memberId, int delta, CancellationToken ct = default)
    {
        if (delta == 0) return;

        var riga = await _db.DocumentUnionMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct)
                                                 .ConfigureAwait(false);
        if (riga is null) return;

        var fratelli = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == riga.UnionId)
            .OrderBy(m => m.Order)
            .ToListAsync(ct).ConfigureAwait(false);

        var i = fratelli.FindIndex(m => m.Id == memberId);
        var j = i + Math.Sign(delta);
        // Ai bordi non si fa niente e non si protesta: è il tasto che deve essere spento, non l'operazione
        // che deve esplodere — la stessa scelta di MoveSectionAsync.
        if (i < 0 || j < 0 || j >= fratelli.Count) return;

        (fratelli[i].Order, fratelli[j].Order) = (fratelli[j].Order, fratelli[i].Order);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> TidyAsync(CancellationToken ct = default)
    {
        // Le unioni rimaste con meno di due membri. La FK cascade ha già portato via le righe dei documenti
        // eliminati: quel che resta da chiudere è l'unione che quelle righe tenevano in piedi.
        var magre = await _db.DocumentUnions
            .Include(u => u.Members)
            .Where(u => u.Members.Count < 2)
            .ToListAsync(ct).ConfigureAwait(false);
        if (magre.Count == 0) return 0;

        _db.DocumentUnions.RemoveRange(magre);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return magre.Count;
    }

    public async Task<int> CompattaAsync(CancellationToken ct = default)
    {
        // ⚠️ `RemoveMemberAsync` rinumera, la CASCATA della FK no: un documento eliminato lascia un buco
        // nelle posizioni (0, 2), e con un buco le frecce su/giu' si spegnevano sul numero sbagliato.
        // La UI oggi guarda la posizione nella lista e non ha piu' bisogno che i numeri siano densi — ma
        // tenerli densi costa una passata all'avvio, e il prossimo che leggera' `Order` lo trovera' sano.
        var unioni = await _db.DocumentUnionMembers.Select(m => m.UnionId).Distinct().ToListAsync(ct)
                                                   .ConfigureAwait(false);
        var toccate = 0;
        foreach (var u in unioni)
            if (await RinumeraAsync(u, ct).ConfigureAwait(false)) toccate++;
        return toccate;
    }

    /// <summary>Riporta le posizioni a 0,1,2… dopo una rimozione: un buco nell'ordine non rompe niente, ma
    /// rende «sposta giù» un gesto che a volte non muove nulla.</summary>
    private async Task<bool> RinumeraAsync(int unionId, CancellationToken ct)
    {
        var fratelli = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == unionId)
            .OrderBy(m => m.Order)
            .ToListAsync(ct).ConfigureAwait(false);

        var cambiato = false;
        for (var i = 0; i < fratelli.Count; i++)
            if (fratelli[i].Order != i) { fratelli[i].Order = i; cambiato = true; }

        if (cambiato) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return cambiato;
    }
}

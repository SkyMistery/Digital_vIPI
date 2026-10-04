using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Il contatore delle aperture. Carta <c>docs/feature/2026-10-01-aperture-documenti.md</c>.</summary>
public sealed class EfApertureDocumenti : IApertureDocumenti
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _bersagli;
    private readonly ILogger<EfApertureDocumenti> _log;
    private readonly TimeProvider _clock;

    public EfApertureDocumenti(VipiDbContext db, IReleaseTargetRegistry bersagli, ILogger<EfApertureDocumenti> log,
                               TimeProvider? clock = null)
    {
        _db = db;
        _bersagli = bersagli;
        _log = log;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Oggi => AperturaDocumento.GiornoDi(_clock.GetUtcNow().UtcDateTime);

    public async Task SegnaAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct = default)
    {
        try
        {
            if (await _bersagli.For(tipo).ResolveDocumentIdAsync(chiave, ct).ConfigureAwait(false) is not int id) return;
            await SegnaAsync(id, Oggi, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Apertura non contata: {Tipo} {Chiave}", tipo, chiave);
        }
    }

    /// <summary>
    /// +1 sulla riga del giorno, o la riga nuova.
    /// <para>⚠️ <b>L'incremento lo fa il database</b> (<c>UPDATE … SET Volte = Volte + 1</c>), non «leggi, somma,
    /// scrivi»: due lettori sullo stesso documento nello stesso istante darebbero un'apertura sola. E se la riga del
    /// giorno nasce in due insieme, il secondo inserimento urta la chiave e ripiega sull'incremento.</para>
    /// </summary>
    internal async Task SegnaAsync(int documentId, DateTime giorno, CancellationToken ct)
    {
        if (await Incrementa(documentId, giorno, ct).ConfigureAwait(false) > 0) return;
        _db.AperturaDocumenti.Add(new AperturaDocumento { DocumentId = documentId, Giorno = giorno, Volte = 1 });
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // ⚠️ La riga è nata nel frattempo: si stacca quella non salvata, o il prossimo SaveChanges di questa
            // richiesta ritenterebbe lo stesso inserimento.
            foreach (var e in _db.ChangeTracker.Entries<AperturaDocumento>().ToList()) e.State = EntityState.Detached;
            await Incrementa(documentId, giorno, ct).ConfigureAwait(false);
        }
    }

    private Task<int> Incrementa(int documentId, DateTime giorno, CancellationToken ct) =>
        _db.AperturaDocumenti.Where(a => a.DocumentId == documentId && a.Giorno == giorno)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Volte, a => a.Volte + 1), ct);

    public async Task<IReadOnlyDictionary<int, int>> RecentiAsync(CancellationToken ct = default)
    {
        try
        {
            var dal = Oggi.AddDays(-(ApertureDocumenti.Finestra - 1));
            var righe = await _db.AperturaDocumenti.AsNoTracking()
                .Where(a => a.Giorno >= dal)
                .GroupBy(a => a.DocumentId)
                .Select(g => new { Id = g.Key, Volte = g.Sum(a => a.Volte) })
                .ToListAsync(ct).ConfigureAwait(false);
            return righe.ToDictionary(r => r.Id, r => r.Volte);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Aperture recenti non lette: si ricade sull'ordine di prima");
            return new Dictionary<int, int>();
        }
    }
}

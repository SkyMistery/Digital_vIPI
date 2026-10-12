using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>L'archivio dei NOTAM. Carta <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>.</summary>
public sealed class EfNotamStore : INotamStore
{
    private readonly VipiDbContext _db;

    public EfNotamStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<Notam>> ListAsync(CancellationToken ct = default) =>
        await _db.Notams.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

    public async Task<NotamImportResult> SincronizzaAsync(IReadOnlyList<NotamDaSorgente> arrivati, DateTime adessoUtc,
                                                         CancellationToken ct = default)
    {
        var inArchivio = await _db.Notams.ToDictionaryAsync(n => n.IvaoId, ct);
        int creati = 0, aggiornati = 0;

        // ⚠️ Per id: se la sorgente ripetesse una voce, vale l'ultima — due righe con lo stesso IvaoId urterebbero
        // l'indice univoco e farebbero fallire il giro intero.
        var perId = arrivati.GroupBy(a => a.IvaoId).ToDictionary(g => g.Key, g => g.Last());
        foreach (var a in perId.Values)
        {
            if (!inArchivio.TryGetValue(a.IvaoId, out var riga))
            {
                riga = new Notam { IvaoId = a.IvaoId };
                Scrivi(riga, a);
                riga.ImportedAtUtc = adessoUtc;
                _db.Notams.Add(riga);
                creati++;
                continue;
            }

            Scrivi(riga, a);
            // «Aggiornato» solo se qualcosa è cambiato davvero: il timbro non si tocca a ogni giro, o direbbe
            // «importato adesso» di un NOTAM fermo da mesi.
            if (_db.Entry(riga).State != EntityState.Modified) continue;
            riga.ImportedAtUtc = adessoUtc;
            aggiornati++;
        }

        var tolti = inArchivio.Values.Where(n => !perId.ContainsKey(n.IvaoId)).ToList();
        _db.Notams.RemoveRange(tolti);

        await _db.SaveChangesAsync(ct);
        return new NotamImportResult(creati, aggiornati, tolti.Count);
    }

    private static void Scrivi(Notam riga, NotamDaSorgente a)
    {
        riga.Name = a.Nome.Length > Notam.NomeMassimo ? a.Nome[..Notam.NomeMassimo] : a.Nome;
        riga.StartUtc = a.InizioUtc;
        riga.EndUtc = a.FineUtc;
        riga.Text = a.Testo;
        riga.Military = a.Militare;
        riga.Airports = Notam.Unisci(a.Aeroporti);
        riga.Centers = Notam.Unisci(a.Centri);
        riga.SourceUpdatedUtc = a.AggiornatoUtc;
    }
}

/// <inheritdoc cref="INotamDeiDocumenti"/>
public sealed class NotamDeiDocumenti : INotamDeiDocumenti
{
    private readonly INotamStore _store;
    private readonly ILogger<NotamDeiDocumenti> _log;
    private readonly TimeProvider _clock;

    public NotamDeiDocumenti(INotamStore store, ILogger<NotamDeiDocumenti> log, TimeProvider? clock = null)
    {
        _store = store;
        _log = log;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<NotamAvviso>> PerAsync(AmbitoNotam ambito, CancellationToken ct = default)
    {
        try
        {
            var tutti = await _store.ListAsync(ct).ConfigureAwait(false);
            return NotamInVista.Scegli(tutti, ambito, _clock.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return Array.Empty<NotamAvviso>(); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "NOTAM non letti: il documento esce senza riquadro.");
            return Array.Empty<NotamAvviso>();
        }
    }
}

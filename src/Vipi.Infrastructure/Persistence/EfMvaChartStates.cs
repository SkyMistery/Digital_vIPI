using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le carte MRVA del sectorfile con quella in vigore per il ciclo AIRAC (<see cref="MvaChartState"/>).
///
/// <para>🔴 U-037 (revisione totale 3): la stessa regola delle aree di settore, su un file. La prima volta che una
/// carta si vede entra subito — nessuna carta è peggio di una in anticipo. Se il file cambia, il testo nuovo entra
/// dal ciclo <b>successivo</b> e quello di prima resta in vigore; se cambia ancora prima del ciclo, in vigore resta
/// sempre il primo. Arrivato il ciclo, il differimento si chiude alla prima lettura.</para>
///
/// <para>⚠️ Un file che sparisce (404) non tocca niente qui: l'assenza non cancella. Lo dice
/// <c>AuroraMvaProvider</c>, che in quel caso non chiama.</para>
/// </summary>
public sealed class EfMvaChartStates
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;
    private readonly TimeProvider _orologio;

    public EfMvaChartStates(VipiDbContext db, IAiracService airac, TimeProvider? orologio = null)
    {
        _db = db;
        _airac = airac;
        _orologio = orologio ?? TimeProvider.System;
    }

    /// <summary>Registra il testo visto adesso nel sectorfile, e apre o chiude il differimento.</summary>
    public async Task RiconciliaAsync(string path, string testo, CancellationToken ct = default)
    {
        var adesso = _orologio.GetUtcNow().UtcDateTime;
        var riga = await _db.MvaChartStates.FirstOrDefaultAsync(x => x.Path == path, ct);
        if (riga is null)
        {
            _db.MvaChartStates.Add(new MvaChartState { Path = path, Text = testo, UpdatedUtc = adesso });
            await _db.SaveChangesAsync(ct);
            return;
        }

        var cambiata = false;
        if (riga.AiracCycle is { } ciclo && !Differita(ciclo, _airac.GetCycle(adesso)))
        {
            riga.AiracCycle = null;       // il ciclo è arrivato: la corrente è quella in vigore
            riga.TextInForce = null;
            cambiata = true;
        }
        if (!string.Equals(riga.Text, testo, StringComparison.Ordinal))
        {
            // Se un differimento è già aperto, in vigore resta il testo di prima — non il corrente, che non lo
            // è mai stato.
            if (riga.AiracCycle is null) riga.TextInForce = riga.Text;
            riga.Text = testo;
            riga.AiracCycle = CicloSuccessivo(adesso);
            riga.UpdatedUtc = adesso;
            cambiata = true;
        }
        if (cambiata) await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Il testo in vigore al ciclo <paramref name="ciclo"/>, <b>se è diverso dal corrente</b>; null se al ciclo
    /// vale il corrente (o la carta non la conosciamo).
    /// </summary>
    public async Task<string?> TestoInVigoreAsync(string path, string ciclo, CancellationToken ct = default)
    {
        var stato = await _db.MvaChartStates.AsNoTracking()
            .Where(x => x.Path == path)
            .Select(x => new { x.AiracCycle, x.TextInForce })
            .FirstOrDefaultAsync(ct);
        return stato is { AiracCycle: { } daCiclo, TextInForce: { } vecchio } && Differita(daCiclo, ciclo) ? vecchio : null;
    }

    /// <summary>Vero se <paramref name="daCiclo"/> non è ancora arrivato a <paramref name="ciclo"/>. Per data.</summary>
    private bool Differita(string daCiclo, string ciclo)
    {
        try { return _airac.EffectiveUtcForCycle(ciclo) < _airac.EffectiveUtcForCycle(daCiclo); }
        catch (ArgumentException) { return false; }
    }

    private string CicloSuccessivo(DateTime adesso)
    {
        var prossimi = _airac.NextCycles(adesso, 2);
        return prossimi.Count > 1 ? prossimi[1].Cycle : _airac.GetCycle(adesso);
    }
}

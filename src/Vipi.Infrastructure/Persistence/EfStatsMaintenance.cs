using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IStatsMaintenance"/>
public sealed class EfStatsMaintenance : IStatsMaintenance
{
    /// <summary>Quanto indietro si ricalcolano i turni: l'anno che le pagine mostrano, più un margine.</summary>
    public static readonly TimeSpan FinestraTurni = TimeSpan.FromDays(400);

    private readonly VipiDbContext _db;
    private readonly IAtcSessionStore _sessioni;
    private readonly IImportStateStore _stati;

    public EfStatsMaintenance(VipiDbContext db, IAtcSessionStore sessioni, IImportStateStore stati)
    {
        _db = db;
        _sessioni = sessioni;
        _stati = stati;
    }

    /// <inheritdoc cref="IStatsMaintenance.RifaiStoricoAsync"/>
    public async Task<StoricoRifatto> RifaiStoricoAsync(CancellationToken ct = default)
    {
        var chiave = ImportCategories.StoricoStatistiche;
        if (await _stati.GetLastSuccessAsync(chiave, ct) is not null) return StoricoRifatto.Niente;

        var adesso = DateTimeOffset.UtcNow;
        var turni = await _sessioni.RecomputeShiftsAsync(adesso - FinestraTurni, adesso, ct);

        // «Preso» al suo stesso giorno = preso prima dell'assestamento: il pianificatore lo ripassa
        // (AirportRollupPlanner.DaRifare). Le righe restano: se la sorgente non lo restituisce più, il conto
        // di prima è meglio di un buco.
        var giorni = await _db.AirportDayTraffic
            .Where(d => d.FetchedUtc > d.Day)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.FetchedUtc, d => d.Day), ct);

        // Il timbro anche a zero righe: la passata ha guardato.
        await _stati.MarkSuccessAsync(chiave, DateTime.UtcNow, ct);
        return new StoricoRifatto(turni, giorni);
    }
}

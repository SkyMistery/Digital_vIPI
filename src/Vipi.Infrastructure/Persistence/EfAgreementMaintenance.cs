using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IAgreementMaintenance"/>
public sealed class EfAgreementMaintenance : IAgreementMaintenance
{
    private readonly VipiDbContext _db;
    private readonly ITransferRepository _flows;
    private readonly IImportStateStore _states;

    public EfAgreementMaintenance(VipiDbContext db, ITransferRepository flows, IImportStateStore states)
    {
        _db = db;
        _flows = flows;
        _states = states;
    }

    public async Task<int> MigrateFlowsToAgreementsAsync(CancellationToken ct = default)
    {
        var category = ImportCategories.TransferFlowsToAgreements;
        // Il segnaposto e non «la tabella degli accordi è vuota»: chi svuotasse gli accordi a mano — o li
        // cancellasse tutti da editor — si ritroverebbe l'archivio vecchio rimesso dentro al riavvio, senza
        // nessuna traccia del perché.
        if (await _states.GetLastSuccessAsync(category, ct) is not null) return 0;

        var created = 0;
        // Un ACC per volta: i flussi vivono nel «secchio» di una ACC sola, quindi il giro li copre tutti esatta-
        // mente una volta — anche quelli di mittenti esteri, che stanno nel secchio dell'ACC italiana confinante.
        foreach (var accCode in await _db.Accs.Select(a => a.Code).ToListAsync(ct))
        {
            var flows = await _flows.ListFlowsByAccAsync(accCode, ct);
            if (flows.Count == 0) continue;

            var accId = await _db.Accs.Where(a => a.Code == accCode).Select(a => a.Id).FirstAsync(ct);

            foreach (var a in FlowsToAgreements.Convert(flows))
            {
                _db.CoordinationAgreements.Add(ToEntity(accId, a));
                created++;
            }
            await _db.SaveChangesAsync(ct);
        }

        await _states.MarkSuccessAsync(category, DateTime.UtcNow, ct);
        return created;
    }

    /// <summary>
    /// L'accordo convertito come entità, <b>con le sue posizioni</b>: ordine, gruppo e profondità vengono dal
    /// dato, non dal repository. È la stessa distinzione fra «si scrive» e «si rimette» che vale per l'annulla —
    /// e ricostruirlo con <c>AddClauseAsync</c> appiattirebbe l'outline in silenzio.
    /// </summary>
    private static CoordinationAgreement ToEntity(int accId, AgreementRow a) => new()
    {
        OwnerAccId = accId,
        TrafficKind = a.TrafficKind,
        Description = a.Description,
        Order = a.Order,
        Parties = a.Parties
            .Select(p => new AgreementParty { Side = p.Side, SectorId = p.SectorId, Order = p.Order })
            .ToList(),
        Airports = a.Airports
            .Select(x => new AgreementAirport { Icao = x.Icao, Name = x.Name, Order = x.Order })
            .ToList(),
        Clauses = a.Clauses.Select(c => new AgreementClause
        {
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
        }).ToList(),
    };
}

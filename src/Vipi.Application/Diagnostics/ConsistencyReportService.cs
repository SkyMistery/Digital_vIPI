using Vipi.Application.Abstractions;

namespace Vipi.Application.Diagnostics;

/// <summary>
/// Report di consistenza dei soft-ref (Fase 2, audit 22 lug): le etichette denormalizzate e i riferimenti
/// per callsign non hanno FK (scelta deliberata: sopravvivono agli snapshot pubblicati e ai rename config),
/// quindi possono divergere dalla fonte. Questo servizio li <b>rileva</b> — non li corregge, non li vincola.
/// </summary>
public interface IConsistencyReportService
{
    Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ConsistencyReportService : IConsistencyReportService
{
    private readonly IConsistencyReportRepository _repo;
    private readonly ISchemaDriftProbe? _schema;

    /// <param name="schema">
    /// Opzionale: se c'è, al report si aggiunge il drift fra modello EF e schema fisico. Sta qui e non in
    /// <see cref="Analyze"/> perché non è un'incongruenza di <i>dati</i> ma di <i>schema</i>, e perché Analyze deve
    /// restare una funzione pura sul dataset di dominio. Agganciandolo in questo punto — l'unico consumato sia da
    /// <c>/vsop/admin/diagnostica</c> sia dall'health check — entrambi lo mostrano senza modifiche a valle.
    /// </param>
    public ConsistencyReportService(IConsistencyReportRepository repo, ISchemaDriftProbe? schema = null)
    {
        _repo = repo;
        _schema = schema;
    }

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        var findings = Analyze(await _repo.LoadAsync(ct));
        if (_schema is null) return findings;

        var drift = await _schema.RunAsync(ct);
        return drift.Count == 0 ? findings : findings.Concat(drift).ToList();
    }

    // Logica pura (nessuna dipendenza da EF): il dataset è già in memoria ⇒ testabile con fixture.
    public static IReadOnlyList<ConsistencyFinding> Analyze(ConsistencyDataset d)
    {
        var findings = new List<ConsistencyFinding>();

        foreach (var t in d.TransferConditions)
        {
            var who = $"TransferPoint #{t.PointId} ({t.AccCode}, CoP {t.Cop})";

            // 1) Pista orfana: soft-ref valorizzato ma la pista non esiste più.
            if (t.ConditionRefId is int refId && !d.RunwayIdents.ContainsKey(refId))
            {
                findings.Add(new ConsistencyFinding("Pista orfana", ConsistencySeverity.Error, who,
                    $"ConditionRefId={refId} non corrisponde a nessuna pista: rimossa o re-importata con altro Id."));
            }
            // 2) Label divergente: la pista esiste ma il suo ident non compare più nell'etichetta denormalizzata.
            else if (t.ConditionRefId is int okId
                     && d.RunwayIdents.TryGetValue(okId, out var ident)
                     && !string.IsNullOrWhiteSpace(t.ConditionLabel)
                     && !t.ConditionLabel!.Contains(ident, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ConsistencyFinding("Label pista divergente", ConsistencySeverity.Warning, who,
                    $"La pista referenziata è ora «{ident}» ma l'etichetta salvata è «{t.ConditionLabel}»: rinominata dopo il salvataggio."));
            }

            // 3) Area fantasma: l'area denormalizzata non corrisponde ad alcuna area speciale esistente.
            if (!string.IsNullOrWhiteSpace(t.ConditionAreaLabel) && !d.AreaNames.Contains(t.ConditionAreaLabel!.Trim()))
            {
                findings.Add(new ConsistencyFinding("Area fantasma", ConsistencySeverity.Warning, who,
                    $"Area «{t.ConditionAreaLabel}» non presente tra le aree speciali: rinominata o rimossa."));
            }
        }

        // 4) Gerarchia dangling: un padre di copertura per callsign che non risolve ad alcun nodo dei cataloghi.
        foreach (var p in d.ParentRefs)
        {
            if (!d.ValidCallsigns.Contains(p.ParentCallsign))
            {
                findings.Add(new ConsistencyFinding("Gerarchia dangling", ConsistencySeverity.Error,
                    $"{p.Kind} {p.Reference}",
                    $"ParentCallsign «{p.ParentCallsign}» non esiste nei cataloghi: catena di copertura interrotta."));
            }
        }

        return findings;
    }
}

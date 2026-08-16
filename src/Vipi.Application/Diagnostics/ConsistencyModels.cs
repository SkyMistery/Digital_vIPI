namespace Vipi.Application.Diagnostics;

/// <summary>Gravità di un'incongruenza rilevata (solo diagnosi: nessun dato viene modificato).</summary>
public enum ConsistencySeverity { Warning, Error }

/// <summary>Una singola incongruenza dati rilevata dal report di consistenza.</summary>
/// <param name="Category">Famiglia del controllo (es. «Pista orfana», «Gerarchia dangling»).</param>
/// <param name="Severity">Gravità.</param>
/// <param name="Entity">Riferimento leggibile all'entità coinvolta (es. «TransferPoint #42 (LIRR)»).</param>
/// <param name="Detail">Spiegazione del disallineamento e come si è prodotto.</param>
public sealed record ConsistencyFinding(string Category, ConsistencySeverity Severity, string Entity, string Detail);

/// <summary>Condizione di una clausola di accordo (soft-ref a pista/area denormalizzate).</summary>
/// <param name="Points">I punti della clausola, come si leggono: servono solo a dire QUALE clausola nel
/// messaggio del report.</param>
public sealed record TransferConditionRow(int ClauseId, string AccCode, string Points,
    int? ConditionRefId, string? ConditionLabel, string? ConditionAreaLabel);

/// <summary>Nodo dei cataloghi che dichiara un padre di copertura per callsign (soft-ref cross-catalogo, no FK).</summary>
public sealed record ParentRefRow(string Kind, string Reference, string ParentCallsign);

/// <summary>
/// Sezione <c>regulated</c> di un documento con la sua selezione di aree, come JSON grezzo: il parse sta
/// nell'analisi (funzione pura) e non nel repository. <paramref name="Reference"/> è il nome leggibile del
/// documento, <paramref name="Kind"/> ne dice la famiglia (vIPI ACC / vIPI APP).
/// </summary>
public sealed record RegulatedRefRow(string Kind, string Reference, string? Json);

/// <summary>
/// Fotografia di sola lettura dei dati soggetti a soft-ref, caricata dalla persistenza e analizzata dal
/// <see cref="ConsistencyReportService"/>. Separa i dati (repo) dalla logica di rilevazione (pura, testabile).
/// </summary>
public sealed class ConsistencyDataset
{
    /// <summary>Condizioni pista/area dei punti di trasferimento.</summary>
    public IReadOnlyList<TransferConditionRow> TransferConditions { get; init; } = Array.Empty<TransferConditionRow>();

    /// <summary>Piste esistenti: Id → Ident corrente (per rilevare ref orfani e label divergenti).</summary>
    public IReadOnlyDictionary<int, string> RunwayIdents { get; init; } = new Dictionary<int, string>();

    /// <summary>Nomi delle aree speciali esistenti (case-insensitive) per validare <c>ConditionAreaLabel</c>.</summary>
    public IReadOnlySet<string> AreaNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nodi che dichiarano un padre di copertura.</summary>
    public IReadOnlyList<ParentRefRow> ParentRefs { get; init; } = Array.Empty<ParentRefRow>();

    /// <summary>Callsign validi come padre (union delle chiavi naturali dei cataloghi ACC/aeroporto).</summary>
    public IReadOnlySet<string> ValidCallsigns { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sezioni <c>regulated</c> con la selezione di aree salvata (JSON grezzo).</summary>
    public IReadOnlyList<RegulatedRefRow> RegulatedRefs { get; init; } = Array.Empty<RegulatedRefRow>();

    /// <summary>IvaoId delle aree speciali esistenti, per validare gli id salvati nelle selezioni.</summary>
    public IReadOnlySet<string> SpecialAreaIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

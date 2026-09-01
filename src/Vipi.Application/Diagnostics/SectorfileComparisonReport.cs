namespace Vipi.Application.Diagnostics;

/// <summary>
/// L'ultima fotografia del confronto vIPI ↔ sectorfile, tenuta in memoria per il processo.
///
/// <para><b>Perché una fotografia e non una sonda.</b> Il confronto scarica tre file da GitHub, e il report
/// di consistenza che lo mostrerebbe è letto anche da <c>/vsop/health</c>, che è <b>anonimo</b> e lo
/// interroga un monitor: una chiamata di rete lì dentro sarebbe una porta aperta su una risorsa esterna. Il
/// modello è quello di <see cref="IStartupMaintenanceReport"/> — «non è una sonda: è già successo, qui si
/// legge soltanto».</para>
///
/// <para><b>Perché in memoria e non in archivio.</b> Non c'è niente da conservare fra un avvio e l'altro: la
/// domanda a cui risponde è «le due sorgenti concordano <i>adesso</i>?». Il costo del non-persistere è
/// riscaricare tre file (~200 KB) dopo un riavvio. ⚠️ E soprattutto non c'è una riga in
/// <c>ImportStates</c>: quello è il registro di ciò che <b>scrive</b>, e questo giro non scrive niente —
/// comparirebbe in Sorgenti come se importasse.</para>
///
/// <para>Singleton: scritto dal giro periodico, letto da più richieste insieme.</para>
/// </summary>
public interface ISectorfileComparisonReport
{
    /// <summary>I rilievi dell'ultimo confronto riuscito. Vuoto se non è ancora girato.</summary>
    IReadOnlyList<ConsistencyFinding> Findings { get; }

    /// <summary>Quando è stata presa la fotografia; null = mai.</summary>
    DateTime? LastRunUtc { get; }

    /// <summary>
    /// Perché l'ultimo tentativo non è riuscito; null = è andato. ⚠️ Non cancella i rilievi precedenti: una
    /// fotografia vecchia dice ancora qualcosa, «nessun rilievo» direbbe una cosa falsa.
    /// </summary>
    string? LastError { get; }

    void Set(IReadOnlyList<ConsistencyFinding> findings, DateTime utc);
    void SetError(string errore);
}

/// <inheritdoc />
public sealed class SectorfileComparisonReport : ISectorfileComparisonReport
{
    private readonly object _lock = new();
    private IReadOnlyList<ConsistencyFinding> _findings = Array.Empty<ConsistencyFinding>();
    private DateTime? _lastRun;
    private string? _lastError;

    public IReadOnlyList<ConsistencyFinding> Findings { get { lock (_lock) return _findings; } }
    public DateTime? LastRunUtc { get { lock (_lock) return _lastRun; } }
    public string? LastError { get { lock (_lock) return _lastError; } }

    public void Set(IReadOnlyList<ConsistencyFinding> findings, DateTime utc)
    {
        lock (_lock) { _findings = findings; _lastRun = utc; _lastError = null; }
    }

    /// <remarks>⚠️ <see cref="LastRunUtc"/> <b>non</b> si tocca: è il timbro dell'ultima fotografia
    /// <i>riuscita</i>, ed è ciò che permette a chi legge di sapere quanto è vecchio quel che sta guardando.
    /// Spostarlo su un tentativo fallito farebbe sembrare fresca una fotografia di ieri.</remarks>
    public void SetError(string errore)
    {
        lock (_lock) _lastError = errore;
    }
}

using Vipi.Sectorfile.IO;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// La raccolta degli avvisi dei lettori del motore. Il <c>WarningCollector</c> della libreria A non è stato portato
/// (dipendeva da <c>ILogger</c>, carta F2 slice 1): questa è la versione dell'app. Una per file, così la lettura
/// parallela non ha niente da condividere.
/// </summary>
public sealed class RaccoltaDiAvvisi : IWarningCollector
{
    private readonly List<LoadWarning> _avvisi = [];
    private readonly object _serratura = new();

    public event EventHandler<LoadWarning>? WarningAdded;

    public int Count
    {
        get
        {
            lock (_serratura)
                return _avvisi.Count;
        }
    }

    public void Add(LoadWarning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        lock (_serratura)
            _avvisi.Add(warning);
        WarningAdded?.Invoke(this, warning);
    }

    public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                    int? lineNumber = null, string? rawSnippet = null)
        => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

    public IReadOnlyList<LoadWarning> Snapshot()
    {
        lock (_serratura)
            return _avvisi.ToArray();
    }

    public void Clear()
    {
        lock (_serratura)
            _avvisi.Clear();
    }
}

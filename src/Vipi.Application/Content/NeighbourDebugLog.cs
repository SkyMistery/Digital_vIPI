namespace Vipi.Application.Content;

/// <summary>
/// Log su file TEMPORANEO per diagnosticare l'import ACC confinanti (append, best-effort). Scrive in
/// <c>neighbours-debug.log</c> nella working dir del processo (root progetto in dev). DA RIMUOVERE a debug finito.
/// </summary>
public static class NeighbourDebugLog
{
    private static readonly object Gate = new();
    private static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "neighbours-debug.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
                System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { /* best-effort: mai far fallire il flusso per il log */ }
    }

    public static string LogPath => Path;
}

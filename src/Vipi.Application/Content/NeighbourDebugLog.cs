namespace Vipi.Application.Content;

/// <summary>
/// Log su file TEMPORANEO per diagnosticare l'import ACC confinanti (append, best-effort). DA RIMUOVERE a
/// debug finito.
///
/// <para>⚠️ <b>Sta nella sottocartella <c>diagnostica/</c> e non accanto all'eseguibile</b>, dal 16 agosto
/// 2026. Ci finiscono stack trace interi — i chiamanti in <c>ConfinantiAdminPage</c> loggano
/// <c>$"PAGE INIT EX: {ex}"</c> — ed è lo stesso contenuto per cui l'11 agosto i due file di
/// <c>StartupDiagnostics</c> erano già stati spostati lì: su un hosting a pannello la cartella
/// dell'applicazione <i>può</i> coincidere col documento radice del sito, e allora il file si scarica da
/// fuori. Una cartella sola è una riga sola da negare nel proxy. Questo file era rimasto fuori da quella
/// decisione perché il grep di allora cercava le scritture, e qui la scrittura è un
/// <c>AppendAllText</c> dentro un metodo che si chiama «Log».</para>
/// </summary>
public static class NeighbourDebugLog
{
    private static readonly object Gate = new();

    // "diagnostica" è ripetuto e non preso da StartupDiagnostics.CartellaDiagnostica perché quella vive in
    // Vipi.Host, che dipende da qui e non viceversa: invertire le dipendenze per condividere una costante di
    // undici caratteri costerebbe più di quanto renda.
    private static readonly string Path =
        System.IO.Path.Combine(AppContext.BaseDirectory, "diagnostica", "neighbours-debug.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                // Creata qui: al primo import la cartella può non esistere ancora (StartupDiagnostics la crea
                // solo quando scrive), e senza di essa l'AppendAllText fallirebbe — in silenzio, per via del
                // catch qui sotto, lasciando credere che l'import non abbia loggato niente.
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch { /* best-effort: mai far fallire il flusso per il log */ }
    }

    public static string LogPath => Path;
}

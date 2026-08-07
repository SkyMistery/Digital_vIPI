using System.Globalization;
using System.Text;

namespace Vipi.AuroraBridge.Core;

/// <summary>
/// Registro locale del tool. Serve a due cose, entrambe operative: ricostruire **cosa è stato scritto in
/// Aurora e quando** (in sessione lo si dimentica), e capire un malfunzionamento senza dover riprodurre.
///
/// Volutamente minimale: un file di testo con un tetto di dimensione. Nessun dato personale, nessuna rete.
/// </summary>
public sealed class BridgeLog
{
    private const long MaxBytes = 512 * 1024;
    private readonly object _lock = new();
    private readonly string _path;

    public BridgeLog(string? path = null) =>
        _path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiAuroraBridge", "bridge.log");

    /// <summary>Percorso del file. Si chiama così e non «Path» per non coprire <see cref="System.IO.Path"/>
    /// dentro questa classe (il primo tentativo non compilava proprio per quello).</summary>
    public string FilePath => _path;

    /// <summary>Scrive una riga. Non solleva mai: un tool in cabina non deve morire perché il disco è pieno
    /// o il file è aperto altrove.</summary>
    public void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                var file = new FileInfo(_path);
                Directory.CreateDirectory(file.DirectoryName!);

                // Rotazione a taglio secco: superato il tetto si riparte. Tenere due file non aggiunge nulla
                // a un registro di sessione, e questo non è un audit legale.
                if (file.Exists && file.Length > MaxBytes) File.Delete(_path);

                var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}  {message}";
                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception) { /* il registro è un di più, non una dipendenza */ }
    }

    /// <summary>Traccia una scrittura nel tag: è la riga che serve davvero quando ci si chiede «cosa gli ho messo?».</summary>
    public void WroteLabel(string? traffic, string? value, string? cop, bool ok, string? error = null) =>
        Write(ok
            ? $"SCRITTO  {traffic}  «{value}»  (CoP {cop})"
            : $"RIFIUTATO {traffic}  «{value}»  (CoP {cop}) — {error}");
}

using System.Runtime.InteropServices;

namespace Vipi.Host;

/// <summary>
/// <b>Chi</b> ha chiesto lo spegnimento: un segnale del sistema operativo (e quale), oppure nessuno.
///
/// <para><b>Perché esiste.</b> La riga <c>ARRESTO</c> del registro dice che l'host si è chiuso in ordine, ma
/// non dice <b>da dove è partito l'ordine</b>, e le due risposte portano in due direzioni opposte:
/// <list type="bullet">
/// <item><c>SIGTERM</c> ⇒ l'ordine viene da <b>fuori</b>: è l'hosting che ferma il processo (inattività,
/// limite di memoria, ricambio). Nel codice non c'è niente da cercare;</item>
/// <item>nessun segnale ⇒ l'host si è fermato <b>da solo</b>, e allora il colpevole è dentro: un servizio in
/// background che è esploso (il comportamento predefinito di .NET è fermare l'host), o qualcuno che chiama
/// <c>StopApplication</c>.</item>
/// </list></para>
///
/// <para>⚠️ Il gestore <b>non annulla</b> il segnale: guarda e lascia passare. Annullarlo vorrebbe dire
/// impedire lo spegnimento, che è l'ultima cosa da fare su un host che sta già facendo fatica.</para>
///
/// <para>⚠️ Best-effort come tutta la diagnostica d'avvio: se la registrazione non è possibile (piattaforma
/// che non conosce quel segnale), si tace e il registro dirà «segnale sconosciuto». Un problema nel
/// raccontare un arresto non deve diventare un arresto.</para>
/// </summary>
public static class SegnaleDiArresto
{
    private static readonly List<PosixSignalRegistration> Registrazioni = new();
    private static string? _ricevuto;

    /// <summary>Il segnale arrivato, o <c>null</c> se non ne è arrivato nessuno.</summary>
    public static string? Ricevuto => Volatile.Read(ref _ricevuto);

    /// <summary>Come si legge nel registro.</summary>
    public static string Riassunto() =>
        Ricevuto is { Length: > 0 } s
            ? $"fermato da {s}"
            : "fermato DA DENTRO (nessun segnale dal sistema)";

    /// <summary>Comincia ad ascoltare. Va chiamata una volta, all'avvio; chiamarla due volte non fa danno.</summary>
    public static void Ascolta()
    {
        if (Registrazioni.Count > 0) return;
        // SIGTERM è quello che manda chi ferma un servizio (Passenger compreso); SIGINT è Ctrl+C; SIGQUIT
        // esiste solo su POSIX ed è quello che manda chi ha fretta.
        Prova(PosixSignal.SIGTERM, "SIGTERM");
        Prova(PosixSignal.SIGINT, "SIGINT");
        if (!OperatingSystem.IsWindows()) Prova(PosixSignal.SIGQUIT, "SIGQUIT");
    }

    /// <summary>Smette di ascoltare e dimentica: serve ai test, che accendono più host nello stesso processo.</summary>
    public static void Azzera()
    {
        foreach (var r in Registrazioni) r.Dispose();
        Registrazioni.Clear();
        Volatile.Write(ref _ricevuto, null);
    }

    private static void Prova(PosixSignal segnale, string nome)
    {
        try
        {
            Registrazioni.Add(PosixSignalRegistration.Create(segnale, ctx =>
            {
                Volatile.Write(ref _ricevuto, nome);
                // ⚠️ `ctx.Cancel` resta falso: si guarda, non si interferisce.
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vIPI] impossibile ascoltare {nome}: {ex.Message}");
        }
    }
}

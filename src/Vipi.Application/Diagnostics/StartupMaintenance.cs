namespace Vipi.Application.Diagnostics;

/// <summary>
/// Registro dei guasti delle manutenzioni d'avvio non critiche (riconciliazioni documentali, proiezione dei
/// settori, backfill e potatura delle release).
///
/// <para><b>Perché esiste.</b> Quelle passate giravano senza protezione, e con <c>Restart=always</c> nel
/// servizio systemd un loro guasto non era un degrado ma un <b>ciclo di riavvii</b>: il sito non parte, e il
/// motivo sta in un log che nessuno guarda. Ora un guasto viene catturato e l'avvio prosegue — ma
/// «prosegue» non deve voler dire «nessuno lo sa mai». Qui il guasto resta scritto, e da qui entra nel
/// report di consistenza, cioè in <c>/services/vsop/admin/diagnostics</c> e in <c>/vsop/health</c> (→ Degraded).</para>
///
/// <para>Vive quanto il processo: è la fotografia di <b>questo</b> avvio. Un riavvio riuscito la azzera, ed
/// è il comportamento voluto — la domanda a cui risponde è «l'istanza che sta servendo adesso è partita
/// intera?».</para>
///
/// <para>Registrato come singleton e scritto una volta sola, all'avvio, prima che l'app accetti richieste;
/// letto poi da più richieste insieme. La lista è protetta perché la <i>lettura</i> è concorrente, non la
/// scrittura.</para>
/// </summary>
public interface IStartupMaintenanceReport
{
    /// <summary>Annota una passata fallita. <paramref name="passata"/> è il nome leggibile che compare nella
    /// diagnostica (es. «riconciliazioni documentali»).</summary>
    void Record(string passata, Exception errore);

    /// <summary>Le segnalazioni da unire al report di consistenza. Vuota se l'avvio è andato intero.</summary>
    IReadOnlyList<ConsistencyFinding> Findings { get; }
}

/// <inheritdoc />
public sealed class StartupMaintenanceReport : IStartupMaintenanceReport
{
    /// <summary>Categoria con cui le segnalazioni compaiono nella diagnostica.</summary>
    public const string Category = "Manutenzione d'avvio";

    private readonly List<ConsistencyFinding> _findings = new();
    private readonly object _lock = new();

    public void Record(string passata, Exception errore)
    {
        // Il messaggio dice tre cose, e servono tutte e tre: cosa non è girato, cosa ne consegue per chi
        // legge, e che il rimedio è un riavvio (sono passate idempotenti — è il motivo per cui proseguire
        // era accettabile).
        var finding = new ConsistencyFinding(Category, ConsistencySeverity.Error, passata,
            $"La passata «{passata}» è fallita all'avvio ({errore.GetType().Name}: {errore.Message}). " +
            "L'applicazione è partita lo stesso, ma ciò che quella passata avrebbe sistemato è rimasto " +
            "com'era. È idempotente: un riavvio riuscito la rifà da capo.", ConsistencyArea.Avvio,
            CategoryKey: "Diag_Cat_Avvio", DetailKey: "Diag_Msg_Avvio",
            DetailArgs: new object[] { passata, errore.GetType().Name, errore.Message });

        lock (_lock) _findings.Add(finding);
    }

    public IReadOnlyList<ConsistencyFinding> Findings
    {
        get { lock (_lock) return _findings.ToArray(); }
    }
}

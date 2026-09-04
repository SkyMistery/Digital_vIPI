using System.Globalization;

namespace Vipi.Host;

/// <summary>
/// Quante richieste ha servito QUESTO processo, quando è arrivata l'ultima e da dove è arrivata la prima.
/// Tre numeri, tenuti in memoria e scritti in coda alla riga <c>ARRESTO</c> di <see cref="RegistroAvvii"/>.
///
/// <para><b>Perché esiste.</b> Su <c>atc.it.ivao.aero</c> il processo si spegne di continuo, e per tenerlo
/// sveglio è stato messo un keep-alive che lo interroga ogni dieci secondi. Non è bastato, e la domanda che
/// il registro degli avvii <b>non sa</b> rispondere è esattamente quella che decide la prossima mossa:
/// <b>quelle richieste al processo arrivano davvero?</b></para>
///
/// <para>Le due risposte portano a due strade opposte, e non c'è modo di indovinare quale:
/// <list type="bullet">
/// <item>tante richieste (una ogni dieci secondi) e muore lo stesso ⇒ <b>non è inattività</b>: il keep-alive
/// non c'entra, e la causa va cercata in un limite dell'hosting (memoria, numero di richieste, ricambio del
/// processo) — cioè nel pannello, non nel codice;</item>
/// <item>una richiesta sola, quella che l'ha svegliato ⇒ i ping <b>non passano</b> — se li serve la cache, o
/// il web server, o un'altra istanza, tenere sveglio un processo che non li vede è impossibile per
/// costruzione, e si aggiusta l'indirizzo che si interroga.</item>
/// </list></para>
///
/// <para>⚠️ Niente di tutto questo è deducibile da fuori: dal lato di chi manda i ping si vede una risposta
/// <c>200</c> in entrambi i casi. È la stessa lezione del keep-alive misurato con <c>curl</c> — la prova sta
/// nel file che scrive <b>il processo</b>, non nella risposta che vede chi bussa.</para>
///
/// <para>⚠️ Costo: un incremento atomico e una data per richiesta. Nessuna allocazione, nessun blocco,
/// nessuna scrittura su disco finché il processo non muore.</para>
/// </summary>
public static class TracciaRichieste
{
    private static long _servite;
    private static long _ultimaTick;
    private static string? _prima;

    /// <summary>Quante richieste sono arrivate all'applicazione da quando è partita.</summary>
    public static long Servite => Interlocked.Read(ref _servite);

    /// <summary>Quando è arrivata l'ultima, o <c>null</c> se non ne è arrivata nessuna.</summary>
    public static DateTime? UltimaUtc
    {
        get
        {
            var t = Interlocked.Read(ref _ultimaTick);
            return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
        }
    }

    /// <summary>Il percorso della PRIMA richiesta: dice chi ha svegliato il processo.</summary>
    public static string? Prima => Volatile.Read(ref _prima);

    /// <summary>Il percorso, tagliato: un indirizzo lungo qui non serve, e la riga del registro si legge.</summary>
    private static string Taglia(string percorso) =>
        percorso.Length <= 40 ? percorso : percorso[..40] + "…";

    /// <summary>Registra una richiesta. La chiama il middleware, una volta per richiesta.</summary>
    public static void Segna(string percorso)
    {
        if (Interlocked.Increment(ref _servite) == 1) Volatile.Write(ref _prima, Taglia(percorso));
        Interlocked.Exchange(ref _ultimaTick, DateTime.UtcNow.Ticks);
    }

    /// <summary>Azzera il conto: serve ai test, che accendono più host nello stesso processo.</summary>
    public static void Azzera()
    {
        Interlocked.Exchange(ref _servite, 0);
        Interlocked.Exchange(ref _ultimaTick, 0);
        Volatile.Write(ref _prima, null);
    }

    /// <summary>
    /// Il pezzo di riga da appendere all'<c>ARRESTO</c>. Vuoto se non è arrivata nessuna richiesta? No:
    /// <b>«richieste 0» è il risultato più interessante di tutti</b>, e va scritto a lettere.
    /// </summary>
    public static string Riassunto(DateTime adesso)
    {
        var n = Servite;
        if (n == 0) return "richieste 0 (nessuna: il processo non è stato interrogato)";

        var ultima = UltimaUtc;
        var fa = ultima is { } u ? $", ultima {(int)Math.Max(0, (adesso - u).TotalSeconds)}s fa" : "";
        var prima = Prima is { Length: > 0 } p ? $", svegliato da {p}" : "";
        return string.Create(CultureInfo.InvariantCulture, $"richieste {n}{fa}{prima}");
    }
}

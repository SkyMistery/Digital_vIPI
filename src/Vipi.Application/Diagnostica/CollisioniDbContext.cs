using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Vipi.Application.Diagnostica;

/// <summary>
/// Chi c'era già, quando una seconda operazione ha trovato il <c>DbContext</c> occupato.
///
/// <para><b>Perché esiste.</b> Il 24 agosto 2026 il registro degli errori ha finalmente mostrato le
/// eccezioni «A second operation was started on this context instance» — e si è visto che diceva solo metà
/// della storia: lo stack dell'eccezione racconta <b>chi è morto</b>, mai <b>chi stava già correndo</b>.
/// Senza quella metà, la causa resta un'ipotesi: è successo, ed è costato un giro di deploy speso su un
/// sospettato che non era il colpevole (voce E9 di <c>docs/lavori-aperti.md</c>).</para>
///
/// <para><b>Come.</b> Un intercettore EF annuncia qui l'inizio e la fine di ogni comando, con il chiamante.
/// Poi si ascolta <c>FirstChanceException</c>: quando qualcuno lancia «a second operation was started», si
/// fotografa <b>in quell'istante</b> che cosa è aperto. La pagina d'errore non cambia; cambia la riga nel
/// file di diagnostica.</para>
///
/// <para>⚠️ <b>Perché non basta l'intercettore, e perché la fotografia va scattata al lancio.</b> Il
/// controllo di concorrenza di EF scatta <b>prima</b> dell'esecuzione: la seconda query non arriva mai
/// all'intercettore, quindi una «collisione» qui non si vedrebbe mai. E aspettare il gestore d'errore
/// sarebbe tardi: mentre l'eccezione risale, la prima operazione fa in tempo a concludersi e la lista
/// tornerebbe vuota. <c>FirstChanceException</c> è l'unico momento in cui la scena è ancora intatta.</para>
///
/// <para>⚠️ Nel testo non entra mai un <b>parametro</b> della query: solo il comando, e tagliato. I
/// parametri sono i dati degli utenti, e questo file si spedisce per email.</para>
///
/// <para>🔴 <b>31 agosto 2026 — questo strumento è stato la causa di due morti del processo.</b> Teneva un
/// elenco a parte delle liste vive (<c>Viventi</c>) e ci aggiungeva un riferimento <b>a ogni comando SQL
/// eseguito</b>, potandolo solo quando scattava una fotografia — cioè quasi mai. In tre ore di esercizio
/// sono milioni di oggetti: <c>avvii.txt</c> del 31 agosto porta due AVVII senza ARRESTO in mezzo, alle
/// 10:57 e alle 13:05. La lezione, e la regola che ne resta: <b>uno strumento di diagnosi non può tenere
/// stato che cresce con il traffico</b>. Oggi la tabella debole è l'<b>unica</b> struttura, la si enumera
/// direttamente, e ogni lista ha un tetto. E c'è un interruttore per spegnerlo senza ricompilare
/// (variabile d'ambiente <c>VIPI_DIAGNOSTICA_COLLISIONI=0</c>).</para>
/// </summary>
public static class CollisioniDbContext
{
    private sealed record Aperta(string Sql, string Chiamante, long AvviataMs);

    /// <summary>
    /// Le operazioni aperte, per contesto. <b>Debole</b>: non tiene in vita nessun <c>DbContext</c>, e
    /// quando il contesto muore la sua lista muore con lui.
    ///
    /// <para>⚠️ È l'<b>unica</b> struttura di questa classe che cresce, ed è per questo che ci si enumera
    /// sopra direttamente invece di tenere un secondo elenco «dei vivi»: quel secondo elenco è stato la
    /// perdita di memoria del 31 agosto 2026. <c>ConditionalWeakTable</c> è enumerabile e prende il suo
    /// lucchetto da sé.</para>
    /// </summary>
    private static readonly ConditionalWeakTable<object, List<Aperta>> PerContesto = new();

    /// <summary>Le ultime fotografie scattate, la più recente per ultima.</summary>
    private static readonly ConcurrentQueue<(DateTime QuandoUtc, string Testo)> Scatti = new();

    /// <summary>
    /// Quante fotografie si tengono. Ne serve <b>una</b>, quella dell'ultimo guasto: le altre sono di
    /// richieste diverse e depistano. Cinque è il margine per un guasto a raffica.
    /// </summary>
    private const int Tetto = 5;

    /// <summary>
    /// Quante operazioni si tengono d'occhio per contesto. Serve un tetto perché una lettura
    /// <b>abbandonata</b> — richiesta annullata a metà enumerazione — non chiude mai il suo lettore e
    /// lascia la riga lì; su un circuito che vive ore le righe si accumulerebbero. Oltre il tetto si butta
    /// la più vecchia, che è quella che meno probabilmente è ancora davvero in corso.
    /// </summary>
    private const int TettoPerContesto = 64;

    /// <summary>Il messaggio con cui EF annuncia esattamente questo guasto. Confronto minuscolo, senza cultura.</summary>
    private const string Frase = "second operation was started";

    private static volatile bool _acceso =
        !string.Equals(Environment.GetEnvironmentVariable("VIPI_DIAGNOSTICA_COLLISIONI"), "0", StringComparison.Ordinal);

    /// <summary>
    /// Se lo strumento è acceso. ⚠️ Default <b>acceso</b>, ma spegnibile senza ricompilare
    /// (<c>VIPI_DIAGNOSTICA_COLLISIONI=0</c>): dopo il 31 agosto 2026 questo codice ha un precedente, e chi
    /// si trovasse un processo che cresce deve poterlo escludere in un minuto invece che in un pacchetto.
    /// </summary>
    public static bool Acceso
    {
        get => _acceso;
        set => _acceso = value;
    }

    static CollisioniDbContext()
    {
        // ⚠️ Il gestore gira su OGNI eccezione lanciata nel processo: dentro non ci va niente di costoso e
        // niente che possa lanciare a sua volta. Il filtro è un confronto di stringa e finisce lì.
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            try
            {
                if (!_acceso) return;
                if (e.Exception is not InvalidOperationException) return;
                if (e.Exception.Message.IndexOf(Frase, StringComparison.OrdinalIgnoreCase) < 0) return;
                Fotografa();
            }
            catch { /* la diagnostica non può diventare il guasto */ }
        };
    }

    /// <summary>Un comando comincia.</summary>
    public static void Apre(object contesto, string? sql)
    {
        if (!_acceso) return;
        try
        {
            var lista = PerContesto.GetOrCreateValue(contesto);
            var nuova = new Aperta(Taglia(sql), Chiamante(), Adesso());
            lock (lista)
            {
                lista.Add(nuova);
                // Vedi TettoPerContesto: si butta la più vecchia, non la più nuova.
                if (lista.Count > TettoPerContesto) lista.RemoveAt(0);
            }
        }
        catch { /* la diagnostica non può diventare il guasto */ }
    }

    /// <summary>Un comando finisce (o fallisce).</summary>
    public static void Chiude(object contesto, string? sql)
    {
        if (!_acceso) return;
        try
        {
            if (!PerContesto.TryGetValue(contesto, out var lista)) return;
            var taglio = Taglia(sql);
            lock (lista)
            {
                var i = lista.FindLastIndex(a => a.Sql == taglio);
                if (i >= 0) lista.RemoveAt(i);
            }
        }
        catch { /* idem */ }
    }

    /// <summary>
    /// L'ultima fotografia, <b>se è fresca</b>. Null se non ce n'è nessuna o se è più vecchia di
    /// <paramref name="freschezza"/>.
    ///
    /// <para>⚠️ La freschezza non è pignoleria. Il 31 agosto 2026 la voce delle 11:40:17 si portava dietro
    /// venti fotografie, la più recente delle <b>11:37:06</b>: tre minuti prima, di un'altra richiesta, con
    /// dentro query che con quella pagina non c'entravano niente. Una fotografia di un altro guasto
    /// allegata al tuo è peggio di nessuna fotografia — la prima volta la si legge come se fosse la
    /// scena.</para>
    /// </summary>
    public static string? UltimoScatto(TimeSpan freschezza)
    {
        try
        {
            (DateTime QuandoUtc, string Testo)? ultimo = null;
            foreach (var s in Scatti) ultimo = s;   // la coda è in ordine: l'ultima è la più recente
            if (ultimo is not { } scatto) return null;
            return DateTime.UtcNow - scatto.QuandoUtc <= freschezza ? scatto.Testo : null;
        }
        catch { return null; }
    }

    /// <summary>Le fotografie scattate, dalla più vecchia alla più recente. Vuoto = nessun guasto di questo tipo.</summary>
    public static IReadOnlyList<string> Scatti_() => Scatti.Select(s => s.Testo).ToArray();

    /// <summary>Che cosa è aperto, adesso: è la riga che risponde a «chi stava già correndo?».</summary>
    private static void Fotografa()
    {
        var ora = Adesso();
        var quando = DateTime.UtcNow;
        var sb = new StringBuilder();
        sb.AppendLine($"⚠️ Al momento del guasto, {quando:HH:mm:ss} UTC, erano aperte:");

        var trovate = 0;
        // ⚠️ Si enumera la tabella debole, che è l'unico elenco esistente. Prima c'era un secondo elenco
        // «dei vivi» riempito a ogni comando: stampava la stessa lista decine di volte (34, 38 e 44 nel
        // file del 31 agosto) e cresceva per sempre.
        foreach (var coppia in PerContesto)
        {
            var lista = coppia.Value;
            if (lista is null) continue;
            Aperta[] copia;
            lock (lista) copia = lista.ToArray();
            foreach (var a in copia)
            {
                sb.AppendLine($"   da {Math.Max(0, ora - a.AvviataMs)} ms · {a.Sql}")
                  .AppendLine($"      ↑ {a.Chiamante}");
                trovate++;
            }
        }

        if (trovate == 0) sb.AppendLine("   (nessuna: o si era già chiusa, o il guasto non nasce da qui)");

        Scatti.Enqueue((quando, sb.ToString().TrimEnd()));
        while (Scatti.Count > Tetto) Scatti.TryDequeue(out _);
    }

    /// <summary>Millisecondi monotoni. Non è un orario: serve solo a dire «da quanto è aperta».</summary>
    private static long Adesso() => Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);

    /// <summary>Il comando, su una riga e accorciato: serve a riconoscerlo, non a rieseguirlo.</summary>
    private static string Taglia(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return "(comando ignoto)";
        var una = string.Join(' ', sql.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()));
        return una.Length <= 140 ? una : una[..140] + "…";
    }

    /// <summary>
    /// Solo i fotogrammi <c>Vipi.</c>, dal più vicino: è la riga che dice quale nostra funzione ha chiesto
    /// quella query. ⚠️ Senza informazioni di file — sono quelle che costano.
    /// </summary>
    private static string Chiamante()
    {
        var frames = new StackTrace(2, fNeedFileInfo: false).GetFrames();
        var nostri = frames
            .Select(f => f.GetMethod())
            .Where(m => m?.DeclaringType?.FullName?.StartsWith("Vipi.", StringComparison.Ordinal) == true)
            .Select(m => $"{m!.DeclaringType!.FullName}.{m.Name}")
            .Take(3)
            .ToArray();
        return nostri.Length > 0 ? string.Join(" ← ", nostri) : "(nessun fotogramma Vipi)";
    }
}

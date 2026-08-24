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
/// <para><b>Quanto costa.</b> Una lista per contesto e una traccia di chiamata <b>senza informazioni di
/// file</b> per comando (qualche decina di microsecondi): su una pagina da trenta query è meno di un
/// millisecondo. Si tiene solo l'ultima ventina di collisioni, e non si tiene nulla quando non ne
/// succedono.</para>
///
/// <para>⚠️ Nel testo non entra mai un <b>parametro</b> della query: solo il comando, e tagliato. I
/// parametri sono i dati degli utenti, e questo file si spedisce per email.</para>
/// </summary>
public static class CollisioniDbContext
{
    private sealed record Aperta(string Sql, string Chiamante, long AvviataMs);

    /// <summary>Le operazioni aperte, per contesto. Debole: non tiene in vita nessun <c>DbContext</c>.</summary>
    private static readonly ConditionalWeakTable<object, List<Aperta>> PerContesto = new();

    /// <summary>Le ultime fotografie scattate, la più recente per ultima.</summary>
    private static readonly ConcurrentQueue<string> Scatti = new();

    private const int Tetto = 20;

    /// <summary>Il messaggio con cui EF annuncia esattamente questo guasto. Confronto minuscolo, senza cultura.</summary>
    private const string Frase = "second operation was started";

    static CollisioniDbContext()
    {
        // ⚠️ Il gestore gira su OGNI eccezione lanciata nel processo: dentro non ci va niente di costoso e
        // niente che possa lanciare a sua volta. Il filtro è un confronto di stringa e finisce lì.
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            try
            {
                if (e.Exception is not InvalidOperationException) return;
                if (e.Exception.Message.IndexOf(Frase, StringComparison.OrdinalIgnoreCase) < 0) return;
                Fotografa();
            }
            catch { /* la diagnostica non può diventare il guasto */ }
        };
    }

    /// <summary>Un comando comincia. Se il contesto è già occupato, annota la coppia.</summary>
    public static void Apre(object contesto, string? sql)
    {
        try
        {
            var lista = PerContesto.GetOrCreateValue(contesto);
            var nuova = new Aperta(Taglia(sql), Chiamante(), Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000));
            lock (lista) lista.Add(nuova);
            lock (Viventi) Viventi.Add(new WeakReference<List<Aperta>>(lista));
        }
        catch { /* la diagnostica non può diventare il guasto */ }
    }

    /// <summary>Un comando finisce (o fallisce).</summary>
    public static void Chiude(object contesto, string? sql)
    {
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

    /// <summary>Le fotografie scattate, dalla più vecchia alla più recente. Vuoto = nessun guasto di questo tipo.</summary>
    public static IReadOnlyList<string> Scatti_() => Scatti.ToArray();

    /// <summary>Le liste vive, per poterle guardare tutte al momento dello scatto. Riferimenti DEBOLI.</summary>
    private static readonly List<WeakReference<List<Aperta>>> Viventi = new();

    /// <summary>Che cosa è aperto, adesso: è la riga che risponde a «chi stava già correndo?».</summary>
    private static void Fotografa()
    {
        var ora = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
        var sb = new StringBuilder();
        sb.AppendLine($"⚠️ Al momento del guasto, {DateTime.UtcNow:HH:mm:ss} UTC, erano aperte:");

        var trovate = 0;
        lock (Viventi)
        {
            Viventi.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var w in Viventi)
            {
                if (!w.TryGetTarget(out var lista)) continue;
                lock (lista)
                    foreach (var a in lista)
                    {
                        sb.AppendLine($"   da {Math.Max(0, ora - a.AvviataMs)} ms · {a.Sql}")
                          .AppendLine($"      ↑ {a.Chiamante}");
                        trovate++;
                    }
            }
        }

        if (trovate == 0) sb.AppendLine("   (nessuna: o si era già chiusa, o il guasto non nasce da qui)");

        Scatti.Enqueue(sb.ToString().TrimEnd());
        while (Scatti.Count > Tetto) Scatti.TryDequeue(out _);
    }

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

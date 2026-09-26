using System.Diagnostics;
using System.Globalization;

namespace Vipi.Host;

/// <summary>
/// Quanta memoria usa il processo: una riga nel log del giorno ogni <see cref="Intervallo"/>, e la stessa misura,
/// corta, nella riga <c>ARRESTO</c> di <c>avvii.txt</c>.
///
/// <para><b>Perché esiste</b> (25 settembre 2026). Il sito «va giù»: alle 15:01:00 e alle 15:56:06 UTC le pagine
/// aperte sono state tagliate tutte insieme e il processo è sparito senza spegnersi in ordine. Tutto indicava
/// l'hosting — alle 15:01 il processo ha scritto nel log sedici secondi DOPO il taglio, e un processo morto non
/// scrive — ma una domanda restava senza numeri: <b>la memoria</b>. Un limite di memoria dell'hosting uccide
/// esattamente così, senza avvisare, e senza una misura non lo si può escludere. Con questa riga, di un processo
/// ucciso resta l'ultima fotografia di al massimo cinque minuti prima.</para>
///
/// <para>⚠️ Il <b>picco</b> conta più dell'istante: un processo che sale a 900 MB per tre secondi e ridiscende
/// non si vedrebbe mai in una fotografia ogni cinque minuti, e il picco invece lo tiene il sistema operativo per
/// noi (<see cref="Process.PeakWorkingSet64"/>).</para>
///
/// <para>⚠️ Non solleva mai: un <see cref="BackgroundService"/> che esplode ferma l'host (è il comportamento
/// predefinito di .NET), e una misura che spegne il sito sarebbe il contrario di quel che serve.</para>
/// </summary>
public sealed class MemoriaDelProcesso : BackgroundService
{
    /// <summary>Ogni quanto si scrive la riga. Cinque minuti sono dodici righe l'ora: niente accanto al poll IVAO,
    /// che ne scrive due al minuto, e abbastanza fitte da vedere una salita prima di un'uccisione.</summary>
    public static readonly TimeSpan Intervallo = TimeSpan.FromMinutes(5);

    private readonly ILogger<MemoriaDelProcesso> _log;

    public MemoriaDelProcesso(ILogger<MemoriaDelProcesso> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervallo);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                _log.LogInformation("Memoria del processo: {Misura}.", Riassunto());
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>La riga lunga, per il log: in uso, picco, heap gestito e il tetto che il runtime vede.</summary>
    public static string Riassunto()
    {
        try
        {
            using var io = Process.GetCurrentProcess();
            var info = GC.GetGCMemoryInfo();
            return string.Create(CultureInfo.InvariantCulture,
                $"in uso {Mb(Environment.WorkingSet)} (picco {Mb(io.PeakWorkingSet64)}), heap gestito {Mb(GC.GetTotalMemory(false))}, " +
                $"tetto visto dal runtime {Mb(info.TotalAvailableMemoryBytes)}");
        }
        catch (Exception ex)
        {
            return $"non misurabile ({ex.GetType().Name})";
        }
    }

    /// <summary>La riga corta, per <c>avvii.txt</c>: in uso e picco. Economica, perché si scrive allo spegnimento.</summary>
    public static string Breve()
    {
        try
        {
            using var io = Process.GetCurrentProcess();
            return $"{Mb(Environment.WorkingSet)} (picco {Mb(io.PeakWorkingSet64)})";
        }
        catch (Exception ex)
        {
            return $"non misurabile ({ex.GetType().Name})";
        }
    }

    /// <summary>Il tetto di memoria che il runtime vede: il limite del contenitore, se c'è, sennò la RAM della
    /// macchina. Serve in <c>avvio-diagnostica.txt</c>, dove dice se l'hosting ci ha messo un limite.</summary>
    public static string Tetto()
    {
        try { return Mb(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes); }
        catch (Exception ex) { return $"non misurabile ({ex.GetType().Name})"; }
    }

    internal static string Mb(long quanti) =>
        string.Create(CultureInfo.InvariantCulture, $"{quanti / (1024 * 1024)} MB");
}

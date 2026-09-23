using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// Il registro del Lab (chiesto dal committente alle prove a mano, 23 settembre): ogni gesto dell'AOD e ogni errore,
/// con lo stack intero, in <c>%LOCALAPPDATA%\VipiSectorLab\log\lab-aaaammgg.txt</c> — un file al giorno, tenuti 14
/// giorni. È quello che si chiede a un AOD quando «il Lab ha fatto una cosa strana»: che cosa ha fatto, in che ordine,
/// e che cosa è andato storto. Il diario d'avvio (<c>avvio.txt</c>) resta: dice come è partita QUESTA apertura.
/// <para>Non deve mai far cadere l'app che racconta: un disco pieno o un file bloccato si ingoiano.</para>
/// </summary>
public sealed class Registro
{
    /// <summary>Quanto si tengono i file del registro.</summary>
    public static readonly TimeSpan Tenuta = TimeSpan.FromDays(14);

    private readonly object _serratura = new();
    private readonly Func<DateTime> _adesso;

    public Registro(string cartellaDeiDati, Func<DateTime>? adesso = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaDeiDati);
        Cartella = Path.Combine(cartellaDeiDati, "log");
        _adesso = adesso ?? (() => DateTime.Now);
        PulisciIVecchi();
    }

    /// <summary>La cartella dei file del registro.</summary>
    public string Cartella { get; }

    /// <summary>Il file di oggi.</summary>
    public string FileDiOggi => Path.Combine(Cartella, $"lab-{_adesso():yyyyMMdd}.txt");

    /// <summary>Una riga: ora locale coi millisecondi, il filo, la categoria, il testo.</summary>
    public void Scrivi(string categoria, string testo)
    {
        string riga = string.Create(CultureInfo.InvariantCulture,
            $"{_adesso():yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId,3}] {categoria,-12} {testo}{Environment.NewLine}");
        lock (_serratura)
        {
            try
            {
                Directory.CreateDirectory(Cartella);
                File.AppendAllText(FileDiOggi, riga);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Il registro non deve mai far cadere l'app che racconta.
            }
        }
    }

    /// <summary>Un errore, con l'eccezione intera (tipo, messaggio, stack, eccezioni interne).</summary>
    public void Errore(string dove, Exception eccezione)
    {
        ArgumentNullException.ThrowIfNull(eccezione);
        Scrivi("ERRORE", $"{dove}: {eccezione}");
    }

    private void PulisciIVecchi()
    {
        try
        {
            if (!Directory.Exists(Cartella))
                return;
            foreach (string file in Directory.EnumerateFiles(Cartella, "lab-*.txt"))
            {
                string data = Path.GetFileNameWithoutExtension(file)["lab-".Length..];
                if (DateTime.TryParseExact(data, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var giorno)
                    && _adesso().Date - giorno > Tenuta)
                    File.Delete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Si riprova al prossimo avvio.
        }
    }
}

/// <summary>
/// Porta nel registro gli avvisi e gli errori di ASP.NET Core e di Blazor: un'eccezione in un componente o nel circuito
/// la scrive il framework, non il Lab, e senza questo ponte sparirebbe.
/// </summary>
public sealed class PonteDelRegistro(Registro registro) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Scrivano(registro, categoryName);

    public void Dispose()
    {
    }

    private sealed class Scrivano(Registro registro, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            string testo = $"{categoria}: {formatter(state, exception)}";
            if (exception is not null)
                testo += Environment.NewLine + exception;
            registro.Scrivi(logLevel == LogLevel.Warning ? "avviso" : "ERRORE", testo);
        }
    }
}

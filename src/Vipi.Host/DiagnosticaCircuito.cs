using Microsoft.Extensions.Logging;

namespace Vipi.Host;

/// <summary>
/// Porta nel registro degli errori anche i guasti che avvengono <b>dentro un circuito Blazor</b>, non dentro
/// una richiesta HTTP.
///
/// <para>
/// 🔴 <b>Perché esiste, e perché è servito due volte.</b> <c>DiagnosticaErrori.Gancio</c> è un
/// <c>IExceptionHandler</c>: lo chiama il middleware delle richieste. Un'eccezione sollevata mentre si preme
/// un tasto in una pagina interattiva <b>non passa di lì</b> — viaggia sulla connessione del circuito, il
/// framework la scrive nei log del processo, e su <c>atc.it.ivao.aero</c> i log del processo non li legge
/// nessuno. Risultato: l'utente vede la barra rossa «An unhandled error has occurred», e in
/// <c>diagnostica/</c> non compare <b>niente</b>. È successo il 2 settembre 2026 aggiungendo una
/// sotto-sezione a una vIPI: il guasto c'era, il file no.
/// </para>
/// <para>
/// ⚠️ E il pezzo che serviva davvero non è lo stack: è la <b>fotografia delle collisioni</b> che
/// <c>DiagnosticaErrori.Registra</c> allega da sé — «che cosa era aperto sul DbContext quando è successo».
/// Senza di quella, «A second operation was started» dice solo chi è <b>morto</b>, mai chi stava già
/// correndo, e la diagnosi resta un'ipotesi che sembra un fatto.
/// </para>
/// <para>
/// ⚠️ Si aggancia ai <b>log</b> e non a un gestore d'eccezioni, perché un gestore d'eccezioni per il
/// circuito non esiste: <c>CircuitHandler</c> sa dire quando un circuito nasce e muore, non perché. La
/// strada supportata è leggere ciò che il framework scrive.
/// </para>
/// </summary>
public sealed class DiagnosticaCircuito : ILoggerProvider
{
    /// <summary>
    /// Le categorie da cui arrivano i guasti del circuito. ⚠️ Il prefisso e non il nome esatto: fra una
    /// versione e l'altra il framework ha spostato queste classi di namespace, e un nome esatto che smette
    /// di combaciare non fallisce — <b>tace</b>, che è il modo in cui una rete diventa finta.
    /// </summary>
    private const string Prefisso = "Microsoft.AspNetCore.Components.Server";

    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(Prefisso, StringComparison.Ordinal)
            ? new Ascoltatore(categoryName)
            : NullLogger.Istanza;

    public void Dispose() { }

    private sealed class Ascoltatore : ILogger
    {
        private readonly string _categoria;
        public Ascoltatore(string categoria) => _categoria = categoria;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>Solo i guasti: il resto del circuito è rumore, e questo registro si scarica via FTP.</summary>
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // ⚠️ Senza eccezione non c'è niente da raccontare che valga una voce nel registro: un errore
            // loggato a parole lo si legge nei log del processo, dove i log del processo si leggono.
            if (!IsEnabled(logLevel) || exception is null) return;

            DiagnosticaErrori.Registra(
                codice: eventId.Name ?? eventId.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                metodo: "CIRCUITO",
                percorso: formatter(state, exception),
                utente: null,
                ex: exception);
        }
    }

    /// <summary>Il logger che non fa niente, per tutte le categorie che non ci riguardano.</summary>
    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Istanza = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}

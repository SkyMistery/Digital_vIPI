namespace Vipi.Application.Abstractions;

/// <summary>
/// Una connessione ATC come la racconta lo <b>storico</b> della sorgente: come
/// <see cref="SourceAtcConnection"/>, ma con la fine — che dal vivo non si può sapere.
/// </summary>
/// <param name="EndUtc">Fine dichiarata dalla sorgente; <c>null</c> = era ancora in corso al momento della lettura.</param>
public sealed record SourceAtcSessionHistory(
    long SessionId,
    int UserId,
    string Callsign,
    int Rating,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    int ConnectedSeconds);

/// <summary>
/// Porta neutra verso lo <b>storico</b> delle connessioni ATC.
///
/// <para>Serve a due cose diverse: riempire i dodici mesi passati la prima volta, e ripassare ogni giorno
/// sulle ultime ore per correggere le code — la fine vera di una sessione la sa solo la sorgente, mentre il
/// poller può solo dire «non c'era più al giro delle 21:03».</para>
///
/// <para>⚠️ Il filtro per callsign della sorgente IVAO è un <b>prefisso di almeno tre caratteri</b>
/// (misurato: <c>LI</c> → 0 risultati, <c>LIR</c> → 342 in trenta giorni), e la retention è di circa
/// <b>366 giorni</b>: oltre l'anno non esiste nulla da recuperare.</para>
/// </summary>
public interface IAtcHistorySource
{
    /// <summary>
    /// Connessioni ATC con callsign che comincia per <paramref name="callsignPrefix"/> nella finestra data.
    /// L'implementazione scorre da sé le pagine della sorgente.
    /// </summary>
    Task<IReadOnlyList<SourceAtcSessionHistory>> GetAtcSessionsAsync(
        string callsignPrefix, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

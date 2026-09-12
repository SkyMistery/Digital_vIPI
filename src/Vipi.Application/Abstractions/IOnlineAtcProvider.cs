namespace Vipi.Application.Abstractions;

/// <summary>Un ATC attualmente online (snapshot normalizzato dal polling IVAO). F3.</summary>
/// <param name="AtisLetter">
/// La lettera dell'ATIS che questa postazione sta trasmettendo, se ne trasmette uno e se la dichiara.
///
/// <para>⚠️ <b>Arriva di qui e non da una seconda chiamata</b>: la fotografia della rete porta già le righe
/// dell'ATIS di ogni ATC, e il poller la scarica una volta al minuto per tutti. Il prototipo del quadro
/// vAWOS se la scaricava da sé — un megabyte per scheda aperta, ogni minuto, moltiplicato per il numero di
/// controllori collegati.</para>
/// </param>
/// <param name="AtisTimeRaw">L'ora dichiarata nell'ATIS, forma <c>HH:MMz</c>. Null = non c'è.</param>
/// <param name="AtisArrRunways">Piste in arrivo lette dall'ATIS (<c>16L/16R</c>). Null = l'ATIS non lo dice.</param>
/// <param name="AtisDepRunways">Piste in partenza lette dall'ATIS. Null = l'ATIS non lo dice.</param>
/// <param name="AtisText">Il testo dell'ATIS, per mostrarlo per intero a chi lo chiede.</param>
public sealed record OnlineAtc(string Callsign, int UserId, string Name, int Rating,
    string? AtisLetter = null, string? AtisTimeRaw = null,
    string? AtisArrRunways = null, string? AtisDepRunways = null, string? AtisText = null);

/// <summary>
/// Fotografia immutabile dell'ATC online in un istante. <see cref="Callsigns"/> alimenta
/// <c>IAorService.Resolve</c>; <see cref="Details"/> serve alle liste UI. ADR-0001 D6.
/// </summary>
public sealed class OnlineAtcSnapshot
{
    public required IReadOnlySet<string> Callsigns { get; init; }
    public required IReadOnlyList<OnlineAtc> Details { get; init; }
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>Snapshot vuoto: usato prima del primo poll così le viste restano sicure (nessun online).</summary>
    public static readonly OnlineAtcSnapshot Empty = new()
    {
        Callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Details = Array.Empty<OnlineAtc>(),
        AsOf = DateTimeOffset.MinValue,
    };
}

/// <summary>
/// Porta read-only verso l'ATC online corrente (cache aggiornata dal polling). Pura per l'Application:
/// non fa I/O, legge l'ultima fotografia in memoria. Impl. = <c>OnlineAtcCache</c> in Infrastructure.
/// </summary>
public interface IOnlineAtcProvider
{
    OnlineAtcSnapshot GetCurrent();
}

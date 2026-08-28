namespace Vipi.Application.Abstractions;

/// <summary>Che fetta dell'archivio guardare: la divisione, il resto del mondo, o tutto.</summary>
public enum AtcArchiveScope { Division, World, All }

/// <summary>
/// Una riga dell'archivio delle connessioni ATC, così com'è: chi, dove, da quando, fino a quando.
/// <para><c>EndUtc</c> nullo significa <b>ancora aperta</b> all'ultimo giro del poller.</para>
/// </summary>
public sealed record AtcArchiveRow(
    long SessionId,
    int UserId,
    string Callsign,
    string? Position,
    string? Frequency,
    int? Rating,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    int DurationSeconds,
    bool IsOutsideDivision);

/// <summary>
/// Cosa cercare nell'archivio. Tutti i campi sono facoltativi tranne il tetto delle righe: è una lettura
/// che serve sia una pagina sia un endpoint macchina, e senza tetto la prima richiesta senza filtri
/// tirerebbe fuori dodici mesi di pianeta.
/// </summary>
public sealed record AtcArchiveFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? CallsignPrefix = null,
    int? UserId = null,
    bool OnlyOpen = false,
    AtcArchiveScope Scope = AtcArchiveScope.All,
    int Limit = 200,
    int Offset = 0);

/// <summary>Righe trovate più quante ce n'erano in tutto: il tetto non deve poter mentire sul totale.</summary>
public sealed record AtcArchivePage(IReadOnlyList<AtcArchiveRow> Rows, int Total);

/// <summary>
/// Lettura grezza dell'archivio delle connessioni ATC — comprese quelle <b>fuori divisione</b>, che dal 28
/// agosto 2026 il poller registra tutte (carta <c>docs/feature/2026-08-28-archivio-atc-mondiale.md</c>).
///
/// <para>⚠️ È una porta <b>diversa</b> da <c>IAtcStatsQueries</c>, e la differenza non è di comodo: quella
/// risponde «quanto ha lavorato la divisione» e ogni sua lettura passa dal filtro di divisione e dalla
/// soglia del minuto; questa risponde «chi c'era», senza soglie e senza confini. Mescolarle vorrebbe dire
/// avere un metodo che conta il pianeta in mezzo a quelli che contano l'Italia.</para>
/// </summary>
public interface IAtcArchiveQueries
{
    Task<AtcArchivePage> SearchAsync(AtcArchiveFilter filter, CancellationToken ct = default);
}

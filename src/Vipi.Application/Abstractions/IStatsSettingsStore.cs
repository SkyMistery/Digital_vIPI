namespace Vipi.Application.Abstractions;

/// <summary>
/// Le scelte della divisione sulle statistiche, più chi le ha prese e quando.
/// </summary>
/// <param name="PublicLeaderboard">
/// Vero se la classifica di divisione è visibile a <b>tutti</b> i loggati. Default <c>false</c>: la scelta di
/// esporre nome e ore degli altri è politica, non tecnica, e la prende lo staff — non nasce fatta.
/// </param>
public sealed record StatsSettings(bool PublicLeaderboard, DateTime? UpdatedUtc, int UpdatedByUserId)
{
    /// <summary>Com'è una divisione che non ha ancora deciso niente.</summary>
    public static readonly StatsSettings Default = new(PublicLeaderboard: false, null, 0);
}

/// <summary>Legge e scrive le scelte sulle statistiche. La scrittura passa dall'audit.</summary>
public interface IStatsSettingsStore
{
    Task<StatsSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Registra la scelta. Un salvataggio che non cambia niente non è un atto e non si scrive.</summary>
    Task SaveAsync(bool publicLeaderboard, int updatedByUserId, CancellationToken ct = default);
}

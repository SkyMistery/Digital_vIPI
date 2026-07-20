namespace Vipi.Application;

/// <summary>
/// Retention della pubblicazione (sezione "ReleaseRetention" di appsettings). Il flusso di publish non pota nulla di
/// suo: qui si dicono i limiti oltre i quali la crescita del DB viene contenuta, potando (a) le release Superseded
/// troppo vecchie e (b) le versioni Archived in eccesso. Le release Effective/Scheduled e le versioni Current/Draft
/// non si toccano mai. Consumato da <see cref="Content.ReleaseService"/>.
/// </summary>
public sealed class ReleaseRetentionOptions
{
    public const string SectionName = "ReleaseRetention";

    /// <summary>Tieni le release <c>Superseded</c> la cui data efficace è entro N cicli AIRAC (28 giorni) da adesso;
    /// pota le più vecchie. Default 13 (≈ 1 anno AIRAC).</summary>
    public int KeepSupersededWithinCycles { get; set; } = 13;

    /// <summary>Numero di versioni <c>Archived</c> da tenere per documento (le più recenti per VersionNumber);
    /// le eccedenti vengono potate. Default 3.</summary>
    public int KeepArchivedVersionsPerDocument { get; set; } = 3;
}

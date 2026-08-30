using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>Un caricamento, come lo mostra la pagina.</summary>
public sealed record AirspaceImportRow(
    int Id, string FileName, string Sha256, long SizeBytes, string? AiracCycle, DateTime? GeneratedUtc,
    DateTime UploadedUtc, string? UploadedByName, int VolumesRead, int VolumesUsable, int DuplicateKeys,
    int PointCount, bool IsCurrent);

/// <summary>
/// Un volume in archivio. <paramref name="PolygonJson"/> è nella forma <c>regionMapPolygon</c> della sorgente,
/// quindi si dà in pasto a <c>AorPolygonProjector</c> e alla mappa senza conversioni.
/// </summary>
public sealed record AirspaceVolumeRow(
    int Id, int ImportId, AirspaceFamily Family, string Name, string Category, string? AirspaceClass,
    AirspaceDatum BaseDatum, int? BaseFeet, string BaseRaw,
    AirspaceDatum TopDatum, int? TopFeet, string TopRaw,
    string PolygonJson, int RingCount, int PointCount, string NaturalKey, int Ordinal,
    double MinLat, double MinLon, double MaxLat, double MaxLon)
{
    /// <summary>Si può agganciare a un settore e mostrare? Lo decide la famiglia, in un posto solo.</summary>
    public bool IsUsable => AirspaceFamilies.IsUsable(Family);

    /// <summary>Come si legge la banda: <c>GND → 2500 FT AMSL</c>.</summary>
    public string BandLabel => $"{BaseRaw} → {TopRaw}";
}

/// <summary>Che cosa cercare nel catalogo. Tutti i campi sono facoltativi: senza nessuno, è «tutto».</summary>
public sealed record AirspaceVolumeQuery(
    int? ImportId = null,
    IReadOnlyList<AirspaceFamily>? Families = null,
    string? Search = null,
    bool UsableOnly = false,
    int Take = 500);

/// <summary>L'intestazione di un caricamento nuovo: quel che sa chi carica, non quel che dice il file.</summary>
public sealed record NewAirspaceImport(
    string FileName, byte[] Content, string? AiracCycle, int? UserId, string? UserName);

/// <summary>
/// L'archivio degli spazi aerei dell'AIP. ⚠️ <b>Nessun controllo di autorizzazione qui dentro</b>: il cancello
/// sta dove sta per tutte le scritture editoriali, e ripeterlo darebbe due cancelli che col tempo dicono cose
/// diverse.
/// </summary>
public interface IAirspaceCatalog
{
    /// <summary>I caricamenti, dal più recente.</summary>
    Task<IReadOnlyList<AirspaceImportRow>> ListImportsAsync(CancellationToken ct = default);

    /// <summary>Il caricamento in vigore, o null se non ne è mai stato fatto uno.</summary>
    Task<AirspaceImportRow?> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Salva un caricamento e lo mette <b>in vigore</b>, spegnendo il precedente. Ritorna la riga salvata.
    /// </summary>
    Task<AirspaceImportRow> SaveAsync(
        NewAirspaceImport header, AirspaceReadResult read, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>I volumi che rispondono alla domanda. Senza <c>ImportId</c>, quelli del caricamento in vigore.</summary>
    Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(AirspaceVolumeQuery query, CancellationToken ct = default);

    /// <summary>I volumi con questi id. ⚠️ <b>Nell'ordine chiesto</b>: l'ordine è una scelta di chi ha agganciato.</summary>
    Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(IReadOnlyList<int> ids, CancellationToken ct = default);

    /// <summary>Quanti volumi per famiglia, nel caricamento in vigore: l'intestazione della pagina.</summary>
    Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(int? importId = null, CancellationToken ct = default);

    /// <summary>Le segnalazioni del lettore, come le ha lasciate il caricamento.</summary>
    Task<IReadOnlyList<AirspaceIssue>> GetIssuesAsync(int importId, CancellationToken ct = default);

    /// <summary>Il KMZ così com'è arrivato, per riscaricarlo. Null se il caricamento non c'è.</summary>
    Task<(string FileName, byte[] Content)?> GetFileAsync(int importId, CancellationToken ct = default);

    /// <summary>Mette in vigore un caricamento già in archivio, spegnendo quello di prima.</summary>
    Task SetCurrentAsync(int importId, CancellationToken ct = default);

    /// <summary>Elimina un caricamento e i suoi volumi. ⚠️ Quello in vigore non si elimina.</summary>
    Task DeleteAsync(int importId, CancellationToken ct = default);
}

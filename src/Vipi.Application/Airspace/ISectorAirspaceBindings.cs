using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>Un volume agganciato, come lo si sceglie: chiave + ordinale, che è l'identità stabile.</summary>
public sealed record AirspaceVolumeKey(string Key, int Ordinal = 0);

/// <summary>
/// L'aggancio di un settore, risolto contro il caricamento <b>in vigore</b>.
///
/// <para><paramref name="Volumes"/> sono i volumi trovati, nell'ordine scelto; <paramref name="Missing"/>
/// sono gli agganci che nel caricamento in vigore <b>non esistono più</b> — un file nuovo può non
/// contenerli. ⚠️ Un aggancio scoperto non è un errore da nascondere: il settore torna alla forma di IVAO
/// e la pagina deve poter dire quale aggancio è rimasto senza volume.</para>
/// </summary>
public sealed record SectorAirspaceBindingRow(
    SourceCatalog Catalog, int SectorId, string Callsign,
    IReadOnlyList<AirspaceVolumeRow> Volumes, IReadOnlyList<AirspaceVolumeKey> Missing,
    DateTime? ChosenUtc, string? ChosenByName)
{
    /// <summary>Vero se c'è almeno un volume da disegnare: sotto questo, il settore resta com'era.</summary>
    public bool HasShape => Volumes.Count > 0;

    /// <summary>Quanti agganci in tutto, compresi quelli scoperti.</summary>
    public int Total => Volumes.Count + Missing.Count;
}

/// <summary>
/// Gli agganci settore → volumi dell'AIP: la scelta di una persona su quale forma un settore <b>mostra</b>.
///
/// <para>⚠️ <b>Non tocca la shape del settore.</b> Vale sull'AoR — mappa, viewer 3D, SVG, stampa — e non sui
/// confinanti, sull'attribuzione del traffico e sulla vLOA, che restano sulla forma di IVAO. Il perché sta
/// nella carta §6-bis: la colonna della shape tiene <b>un anello</b>, e i due casi che hanno fatto nascere
/// la richiesta sono a due e a sette zone.</para>
/// </summary>
public interface ISectorAirspaceBindings
{
    /// <summary>
    /// Gli agganci dei callsign chiesti, risolti contro il caricamento in vigore. Chiave = callsign
    /// maiuscolo; i callsign senza aggancio non compaiono affatto.
    /// </summary>
    Task<IReadOnlyDictionary<string, SectorAirspaceBindingRow>> ResolveAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>Tutti gli agganci, per la pagina che li cura. Ordinati per callsign.</summary>
    Task<IReadOnlyList<SectorAirspaceBindingRow>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Riscrive l'aggancio di un settore: l'elenco dato <b>sostituisce</b> quello di prima. Elenco vuoto =
    /// il settore torna alla forma di IVAO.
    /// </summary>
    Task SetAsync(SourceCatalog catalog, int sectorId, string callsign,
        IReadOnlyList<AirspaceVolumeKey> volumes, int? userId, string? userName, CancellationToken ct = default);
}

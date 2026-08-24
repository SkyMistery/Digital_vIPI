using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Un settore come serve all'attribuzione del traffico: chi è, da chi pende, e che volume occupa.
/// </summary>
/// <param name="Callsign">Callsign del settore proiettato (<c>Sector.Callsign</c>).</param>
/// <param name="ParentCallsign">Padre nell'albero <b>proiettato</b> (<c>Sector.ParentSectorId</c>), non nei
/// cataloghi: solo lì DEL/GND/TWR hanno un padre, derivato dalla scaletta.</param>
/// <param name="Type">Tipo di posizione: decide quali fasi di volo la posizione dichiara di gestire.</param>
/// <param name="AirportIcao">Aeroporto di appartenenza, se è una posizione d'aeroporto.</param>
/// <param name="RegionMapPolygon">Poligono grezzo dal catalogo; <c>null</c> per DEL e GND, che non ne hanno.</param>
/// <param name="LowerLimit">Limite inferiore grezzo (l'unità la interpreta <c>AorFlBand</c>).</param>
/// <param name="UpperLimit">Limite superiore grezzo; <c>null</c> = senza tetto.</param>
public sealed record SectorVolumeRow(
    string Callsign,
    string? ParentCallsign,
    SectorType Type,
    string? AirportIcao,
    string? RegionMapPolygon,
    int? LowerLimit,
    int? UpperLimit);

/// <summary>
/// Da dove l'attribuzione del traffico prende la mappa dei settori: albero di copertura più volumi.
/// Cambia solo quando cambiano i cataloghi (giri di import, una volta al giorno), quindi il chiamante
/// può tenersela in memoria invece di rileggerla ogni minuto.
/// </summary>
public interface ISectorVolumeCatalog
{
    /// <summary>Tutti i settori attivi con quel che serve a rivendicare traffico.</summary>
    Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default);
}

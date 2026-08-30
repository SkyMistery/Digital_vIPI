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
/// <param name="Parts">
/// I <b>pezzi</b> della forma, ognuno con le sue quote, come li dà la porta unica
/// (<c>ISectorShapeResolver</c>, carta refactor 15). Vuoto per DEL e GND, che un'area non ce l'hanno.
/// ⚠️ Erano un poligono e due quote sciolte: con un CTR di più zone quel modello rivendicava il cielo
/// fra una zona e l'altra, e sopra quella più bassa.
/// </param>
/// <param name="Source">Da dove viene la forma: finisce in archivio accanto alla tratta, perché un gradino
/// nei numeri dev'essere spiegabile fra sei mesi.</param>
public sealed record SectorVolumeRow(
    string Callsign,
    string? ParentCallsign,
    SectorType Type,
    string? AirportIcao,
    IReadOnlyList<Vipi.Application.Airspace.ShapePart> Parts,
    ShapeSource Source);

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

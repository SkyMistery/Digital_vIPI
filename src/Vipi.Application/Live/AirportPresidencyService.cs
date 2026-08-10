using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Application.Live;

/// <summary>
/// «Chi controlla questo aeroporto adesso», per le pagine che <b>non</b> stanno dentro la vista live e quindi
/// non hanno un <see cref="LiveStationContext"/> già pronto: la vista rapida e il viewer dell'aeroporto.
///
/// <para>La logica non è qui — è in <see cref="AirportPresidencyResolver"/>, condivisa con la vista live.
/// Questo servizio si limita a procurare gli ingredienti: le postazioni dell'aeroporto, la catena dei padri e
/// chi è online. Tenerli separati è ciò che permette di provare la regola senza database.</para>
/// </summary>
public interface IAirportPresidencyService
{
    /// <summary>Presidenza corrente di un aeroporto. Se l'aeroporto non ha postazioni note, torna UNICOM.</summary>
    Task<AirportPresidency> ResolveAsync(string icao, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportPresidencyService"/>
public sealed class AirportPresidencyService : IAirportPresidencyService
{
    private readonly IAirportSectorRepository _sectors;
    private readonly IStructureEditingService _structure;
    private readonly ITopologyProvider _topology;
    private readonly IOnlineAtcProvider _online;

    public AirportPresidencyService(IAirportSectorRepository sectors, IStructureEditingService structure,
        ITopologyProvider topology, IOnlineAtcProvider online)
    {
        _sectors = sectors;
        _structure = structure;
        _topology = topology;
        _online = online;
    }

    public async Task<AirportPresidency> ResolveAsync(string icao, CancellationToken ct = default)
    {
        var righe = await _sectors.ListByAirportAsync(icao, ct);

        // Le nascoste non presidiano nulla, e l'ATIS non è una posizione che controlla: lo esclude
        // FrequencyPositions.ToSectorType tornando null, invece di lasciarlo decidere a questo punto.
        var posizioni = righe
            .Where(r => !r.IsHidden)
            .Select(r => (r.ComposePosition, Tipo: FrequencyPositions.ToSectorType(r.Position)))
            .Where(x => x.Tipo is not null && !string.IsNullOrWhiteSpace(x.ComposePosition))
            .Select(x => (x.ComposePosition, x.Tipo!.Value))
            .ToList();

        // Il padre dell'aeroporto sta sul nodo Aeroporto, non sulle sue posizioni (scaletta DEL→GND→TWR→APP):
        // si legge dalla struttura della ACC che le righe stesse dichiarano.
        var accCode = righe.Select(r => r.AccCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        string? padre = null;
        if (accCode is not null)
        {
            var struttura = await _structure.LoadAsync(accCode, ct);
            padre = struttura?.Airports
                .FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase))?.ParentCallsign;
        }

        // Topologia GLOBALE: la copertura di uno scalo può uscire dalla sua ACC, come per i trasferimenti.
        var topologia = await _topology.BuildGlobalAsync(ct);
        var antenati = AirportPresidencyResolver.Ancestors(padre, topologia.Parent);

        return AirportPresidencyResolver.Resolve(posizioni, antenati, _online.GetCurrent().Callsigns);
    }
}

using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta read-only verso l'elenco ACC navigabili, derivato dalle ACC presenti nel DB.
/// Sostituisce l'elenco hardcoded: con DB vuoto non mostra ACC inesistenti; appena si crea una ACC
/// compare la card relativa. Sincrona di proposito (tabella piccola) per non propagare async ai call-site.
/// </summary>
public interface IStationDirectory
{
    IReadOnlyList<AccInfo> ListAccs();

    /// <summary>Aeroporti col loro ACC di competenza, per mappare i callsign d'aeroporto (TWR/APP…) al loro ACC.</summary>
    IReadOnlyList<AirportStation> ListAirports();
}

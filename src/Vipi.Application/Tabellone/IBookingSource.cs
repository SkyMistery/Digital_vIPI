namespace Vipi.Application.Tabellone;

/// <summary>
/// Porta verso il booking di IVAO Italia: il testo grezzo della risposta, che legge <see cref="BookingParser"/>.
/// Lancia <see cref="Abstractions.SorgenteNonConfigurataException"/> senza la chiave, e su ogni errore di rete o
/// di stato HTTP (401 a corpo vuoto con la chiave sbagliata): chi chiama tiene l'ultima lettura buona.
/// </summary>
public interface IBookingSource
{
    Task<string> LeggiAsync(CancellationToken ct = default);
}

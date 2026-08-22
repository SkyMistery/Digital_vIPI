using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Sorgente MRVA spenta: è l'esito di <c>Sectorfile:RawBaseUrl</c> vuota, cioè la configurazione dei test, che non
/// escono in rete. Le carte si provano nei test del parser e dell'adapter, dove il file c'è.
/// </summary>
internal sealed class NoMinimaSource : IVectoringMinimaSource
{
    public Task<MvaChart> GetAccChartAsync(string accCode, CancellationToken ct = default) =>
        Task.FromResult(MvaChart.Empty);

    public Task<MvaChart> GetAirportChartAsync(string icao, CancellationToken ct = default) =>
        Task.FromResult(MvaChart.Empty);
}

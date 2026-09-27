using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il tetto dei messaggi che il circuito Blazor accetta dal browser.
///
/// <para>🔴 <b>Perché (U-016, revisione totale 3).</b> Il default di SignalR è 32 KB, e blazor.web.js spedisce il
/// valore di un campo DUE volte a ogni evento (il valore del campo e quello di <c>ChangeEventArgs</c>): oltre
/// ~16 KB di testo il server chiude la connessione e l'evento si perde. Il DOM mostra il testo, il server no.
/// Riprodotto il 26-set-2026: 44 KB incollati in un campo di prosa, niente salvato, nessun avviso; il poligono
/// di LAAA (16,5 KB) in Confinanti staccava il circuito a ogni tasto.</para>
/// </summary>
public sealed class TettoDelCircuitoTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public TettoDelCircuitoTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    [Fact]
    public void Il_circuito_accetta_un_campo_da_mezzo_megabyte()
    {
        // ComponentHub è interno: si chiede per nome il tipo che AddHubOptions configura davvero.
        var hub = typeof(Microsoft.AspNetCore.Components.Server.CircuitOptions).Assembly
            .GetType("Microsoft.AspNetCore.Components.Server.ComponentHub", throwOnError: true)!;
        var tipo = typeof(IOptions<>).MakeGenericType(typeof(HubOptions<>).MakeGenericType(hub));
        var opzioni = (HubOptions)tipo.GetProperty("Value")!.GetValue(_factory.Services.GetRequiredService(tipo))!;

        Assert.True(opzioni.MaximumReceiveMessageSize >= 512 * 1024,
            $"MaximumReceiveMessageSize = {opzioni.MaximumReceiveMessageSize}");
    }
}

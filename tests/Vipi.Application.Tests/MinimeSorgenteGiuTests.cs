using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// 🔴 U-039 (revisione totale 3): con GitHub giù l'eccezione della sorgente MRVA risaliva fino alla pagina, che non
/// aveva un catch — la bozza, l'editor e l'anteprima di una vIPI ACC o APP cadevano per una sezione sola. Ora la
/// sezione arriva vuota e lo dice; la cattura di una release invece si ferma, perché congelare quel vuoto
/// mostrerebbe per sempre «nessuna carta» dove la carta c'è.
/// </summary>
public class MinimeSorgenteGiuTests
{
    private static readonly MvaChart UnaCarta = new(
        new[] { new MvaShape("A", true, new[] { new MvaPoint(41.0, 12.0), new MvaPoint(41.1, 12.1), new MvaPoint(41.2, 12.0) }) },
        Array.Empty<MvaLabel>());

    private sealed class Sorgente(params string[] giu) : IVectoringMinimaSource
    {
        public Task<MvaChart> GetAccChartAsync(string accCode, CancellationToken ct = default) => Carta(accCode);
        public Task<MvaChart> GetAirportChartAsync(string icao, CancellationToken ct = default) => Carta(icao);

        private Task<MvaChart> Carta(string codice) => giu.Contains(codice)
            ? throw new HttpRequestException($"timeout su {codice}")
            : Task.FromResult(UnaCarta);
    }

    [Fact]
    public async Task La_carta_enroute_che_non_arriva_diventa_una_sezione_che_lo_dice()
    {
        var v = await MinimaCharts.ForAccAsync(new Sorgente("LIRR"), "LIRR", CancellationToken.None);

        Assert.True(v.IsEmpty);
        Assert.True(v.SorgenteNonRaggiungibile);
    }

    [Fact]
    public async Task Una_carta_che_non_arriva_non_si_porta_via_le_altre()
    {
        var v = await MinimaCharts.ForPositionsAsync(new Sorgente("LIRN"), new[] { "LIRN_APP", "LIRF_APP" }, CancellationToken.None);

        Assert.Equal("LIRF", Assert.Single(v.Charts).Owner);
        Assert.True(v.SorgenteNonRaggiungibile);
    }

    [Fact]
    public async Task Con_la_sorgente_che_risponde_nessun_avviso()
    {
        var v = await MinimaCharts.ForPositionsAsync(new Sorgente(), new[] { "LIRN_APP" }, CancellationToken.None);

        Assert.False(v.SorgenteNonRaggiungibile);
        Assert.Same(v, MinimaCharts.DaCongelare(v));
    }

    [Fact]
    public async Task Una_release_non_congela_la_sezione_di_una_sorgente_giu()
    {
        var v = await MinimaCharts.ForAccAsync(new Sorgente("LIRR"), "LIRR", CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => MinimaCharts.DaCongelare(v));
    }
}

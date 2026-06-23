using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>Risoluzione "primo online" della catena handler dei trasferimenti (F3).</summary>
public class TransferOnlineResolverTests
{
    private static TransferRow Row(string[] chain, string fallback = "UNICOM") => new()
    {
        Id = 1, RelationKey = "LIRR-LIMM", RelationLabel = "Roma ↔ Milano", Phase = TransferPhase.Arrival,
        AirportIcao = "LIMC", Cop = "VALMA", FlRule = "FL280↑", HandlerChain = chain,
        StandardFallback = fallback, Order = 1,
    };

    private static HashSet<string> Online(params string[] cs) => new(cs, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Nessuno_online_usa_il_fallback()
    {
        var r = TransferOnlineResolver.Resolve(Row(new[] { "WS2", "ES2" }), Online());

        Assert.False(r.IsOnline);
        Assert.Equal("UNICOM", r.ResolvedHandler);
    }

    [Fact]
    public void Primo_della_catena_online_vince()
    {
        var r = TransferOnlineResolver.Resolve(Row(new[] { "WS2", "ES2" }), Online("LIMM_WS2_CTR", "LIMM_ES2_CTR"));

        Assert.True(r.IsOnline);
        Assert.Equal("WS2", r.ResolvedHandler);
    }

    [Fact]
    public void Salta_al_primo_online_se_il_precedente_e_offline()
    {
        // WS2 offline, ES2 online => si risolve ES2 (ordine catena rispettato).
        var r = TransferOnlineResolver.Resolve(Row(new[] { "WS2", "ES2" }), Online("LIMM_ES2_CTR"));

        Assert.True(r.IsOnline);
        Assert.Equal("ES2", r.ResolvedHandler);
    }

    [Fact]
    public void Match_per_segmento_del_callsign()
    {
        // "DTTC" è un segmento di "DTTC_CTR".
        var r = TransferOnlineResolver.Resolve(Row(new[] { "DTTC" }, "Confine"), Online("DTTC_CTR"));

        Assert.True(r.IsOnline);
        Assert.Equal("DTTC", r.ResolvedHandler);
    }

    [Fact]
    public void Callsign_non_correlato_non_attiva_handler()
    {
        var r = TransferOnlineResolver.Resolve(Row(new[] { "WS2" }), Online("LIRR_NE_CTR"));

        Assert.False(r.IsOnline);
        Assert.Equal("UNICOM", r.ResolvedHandler);
    }

    [Fact]
    public void Token_corto_non_sovramatcha_per_sottostringa()
    {
        // "WS" (len 2) non è un segmento di "LIMM_WSX_CTR" e il fallback sottostringa è disattivo (<4).
        var r = TransferOnlineResolver.Resolve(Row(new[] { "WS" }), Online("LIMM_WSX_CTR"));

        Assert.False(r.IsOnline);
    }

    [Fact]
    public void Token_composito_lungo_matcha_per_sottostringa()
    {
        // "LIMM_WS2" (len ≥4) non è un singolo segmento ma è sottostringa di "LIMM_WS2_CTR".
        var r = TransferOnlineResolver.Resolve(Row(new[] { "LIMM_WS2" }), Online("LIMM_WS2_CTR"));

        Assert.True(r.IsOnline);
        Assert.Equal("LIMM_WS2", r.ResolvedHandler);
    }
}

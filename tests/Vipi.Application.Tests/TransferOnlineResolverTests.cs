using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>Risoluzione "primo online" del ricevente di un punto di trasferimento (vista operativa/Ridotta).</summary>
public class TransferOnlineResolverTests
{
    private static HashSet<string> Online(params string[] cs) => new(cs, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Nessuno_online_va_su_UNICOM()
    {
        var (handler, online) = TransferOnlineResolver.Resolve(
            new[] { "LIMM_WS2_CTR", "LIMM_CTR" }, Online());

        Assert.False(online);
        Assert.Equal("UNICOM", handler);
    }

    [Fact]
    public void Primo_candidato_online_vince()
    {
        var (handler, online) = TransferOnlineResolver.Resolve(
            new[] { "LIMM_WS2_CTR", "LIMM_CTR" }, Online("LIMM_WS2_CTR"));

        Assert.True(online);
        Assert.Equal("LIMM_WS2_CTR", handler);
    }

    [Fact]
    public void Risale_al_primo_online_se_il_nominale_e_offline()
    {
        // nominale LIRF_TWR offline → risale al padre LIRR_NE_CTR online.
        var (handler, online) = TransferOnlineResolver.Resolve(
            new[] { "LIRF_TWR", "LIRR_NE_CTR" }, Online("LIRR_NE_CTR"));

        Assert.True(online);
        Assert.Equal("LIRR_NE_CTR", handler);
    }

    [Fact]
    public void Match_per_segmento_del_callsign()
    {
        var (handler, online) = TransferOnlineResolver.Resolve(
            new[] { "WS2" }, Online("LIMM_WS2_CTR"));

        Assert.True(online);
        Assert.Equal("WS2", handler);
    }

    [Fact]
    public void Nessun_candidato_da_UNICOM()
    {
        var (handler, online) = TransferOnlineResolver.Resolve(
            Array.Empty<string>(), Online("LIRR_NE_CTR"));

        Assert.False(online);
        Assert.Equal("UNICOM", handler);
    }

    [Fact]
    public void Token_corto_non_sovramatcha_per_sottostringa()
    {
        var (_, online) = TransferOnlineResolver.Resolve(new[] { "WS" }, Online("LIMM_WSX_CTR"));
        Assert.False(online);
    }
}

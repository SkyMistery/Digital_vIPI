using Vipi.Application.Content;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Configurazione e tetto di richieste dell'endpoint del bridge Aurora. Il limitatore protegge un endpoint
/// ANONIMO interrogato in polling da ogni tool desktop: senza tetto basta un client difettoso a caricare il DB.
/// </summary>
public class AuroraBridgeOptionsTests
{
    [Fact]
    public void Senza_configurazione_valgono_i_default()
    {
        var opt = new AuroraBridgeOptions().ToMatchOptions();

        Assert.Equal(AuroraLabelConvention.Number, opt.Convention);
        Assert.Equal(8, opt.MaxCandidates);
    }

    [Theory]
    [InlineData("FlPrefixed", AuroraLabelConvention.FlPrefixed)]
    [InlineData("flprefixed", AuroraLabelConvention.FlPrefixed)]
    [InlineData("Number", AuroraLabelConvention.Number)]
    [InlineData("bislacco", AuroraLabelConvention.Number)]   // valore ignoto → default, non eccezione
    [InlineData(null, AuroraLabelConvention.Number)]
    public void La_convenzione_si_legge_dalla_configurazione(string? configured, AuroraLabelConvention expected)
    {
        var opt = new AuroraBridgeOptions { LabelConvention = configured }.ToMatchOptions();

        Assert.Equal(expected, opt.Convention);
    }

    [Fact]
    public void Un_numero_di_candidati_assurdo_ricade_sul_default()
    {
        Assert.Equal(8, new AuroraBridgeOptions { MaxCandidates = 0 }.ToMatchOptions().MaxCandidates);
        Assert.Equal(8, new AuroraBridgeOptions { MaxCandidates = -3 }.ToMatchOptions().MaxCandidates);
    }

    [Fact]
    public void Il_limitatore_lascia_passare_fino_al_tetto_e_poi_blocca()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 3; i++)
            Assert.True(limiter.TryAcquire("1.2.3.4", 3));

        Assert.False(limiter.TryAcquire("1.2.3.4", 3));
    }

    [Fact]
    public void Il_tetto_e_per_chiave()
    {
        var limiter = new RequestRateLimiter();
        Assert.True(limiter.TryAcquire("1.2.3.4", 1));
        Assert.False(limiter.TryAcquire("1.2.3.4", 1));

        // Un altro IP non paga il conto del primo.
        Assert.True(limiter.TryAcquire("5.6.7.8", 1));
    }

    [Fact]
    public void Tetto_non_positivo_significa_nessun_limite()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 50; i++)
            Assert.True(limiter.TryAcquire("1.2.3.4", 0));
    }
}

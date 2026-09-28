using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>Guardia D1: l'identità dev fittizia (admin onnipotente) è ammessa solo in Development.</summary>
public class ProductionIdentityGuardTests
{
    [Theory]
    [InlineData(true, true, false)]    // Development + dev identity → OK
    [InlineData(false, false, false)]  // Production + host identity → OK
    [InlineData(true, false, false)]   // Development + host identity → OK
    [InlineData(false, true, true)]    // Production + dev identity → INSICURO
    public void Validate_flags_only_dev_identity_outside_development(bool isDev, bool useDev, bool expectError)
    {
        var error = ProductionIdentityGuard.Validate(isDev, useDev);
        Assert.Equal(expectError, error is not null);
    }

    /// <summary>
    /// 🔴 U-112 (scelta del committente 28-set): nel Vipi.Host l'identità di sviluppo nasce SOLO in Development, e la
    /// guardia sull'ambiente non poteva mai scattare. Una guardia vera guarda anche dove l'app è esposta e su quale
    /// database scrive: un «Development» su un indirizzo pubblico, o sul MySQL di produzione, è un admin per tutti.
    /// </summary>
    [Theory]
    [InlineData("http://0.0.0.0:5000", null)]
    [InlineData("http://*:80", null)]
    [InlineData("http://+:80", null)]
    [InlineData("http://192.168.1.10:5000", null)]
    [InlineData("https://vipi.example.org", null)]
    [InlineData("http://127.0.0.1:5199", "MySql")]
    public void In_Development_la_dev_identity_esposta_o_sul_MySQL_e_rifiutata(string urls, string? provider)
    {
        Assert.NotNull(ProductionIdentityGuard.Validate(true, true, urls, provider));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("http://127.0.0.1:5199", null)]
    [InlineData("http://localhost:5034;https://localhost:5035", "Sqlite")]
    [InlineData("http://[::1]:5000", "")]
    public void In_Development_la_dev_identity_in_locale_passa(string? urls, string? provider)
    {
        Assert.Null(ProductionIdentityGuard.Validate(true, true, urls, provider));
    }

    [Fact]
    public void Senza_dev_identity_indirizzo_e_database_non_contano()
    {
        Assert.Null(ProductionIdentityGuard.Validate(false, false, "http://0.0.0.0:80", "MySql"));
    }

    [Fact]
    public void EnsureSafe_throws_on_dev_identity_in_production()
    {
        Assert.Throws<InvalidOperationException>(() => ProductionIdentityGuard.EnsureSafe(false, true));
    }

    [Fact]
    public void EnsureSafe_passes_in_development()
    {
        ProductionIdentityGuard.EnsureSafe(true, true);   // non lancia
    }
}

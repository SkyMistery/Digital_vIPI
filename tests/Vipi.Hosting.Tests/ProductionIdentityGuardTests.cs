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

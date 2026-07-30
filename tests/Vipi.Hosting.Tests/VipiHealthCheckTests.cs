using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Il probe sulle migrazioni pendenti vale solo dove le migrazioni girano davvero. Su Postgres lo schema lo fa
/// PostgresSchemaReconciler (EnsureCreated), che non scrive in __EFMigrationsHistory: senza questa distinzione
/// l'health check risponderebbe SEMPRE Unhealthy in produzione, con lo schema perfettamente allineato.
/// </summary>
public class VipiHealthCheckTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", true)]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", false)]
    [InlineData("npgsql.entityframeworkcore.postgresql", false)]   // il confronto ignora il case
    [InlineData("Microsoft.EntityFrameworkCore.InMemory", true)]
    [InlineData(null, true)]                                        // provider ignoto: si controlla, meglio un falso allarme che un buco
    public void UsesEfMigrations_only_outside_postgres(string? providerName, bool expected)
    {
        Assert.Equal(expected, VipiReadinessCheck.UsesEfMigrations(providerName));
    }

    /// <summary>
    /// I due endpoint devono restare distinti: se i tag coincidessero, /vsop/health/ready si tirerebbe dietro il
    /// report di consistenza e la sonda dell'orchestratore tornerebbe a costare scansioni complete.
    /// </summary>
    [Fact]
    public void Readiness_and_full_are_distinct_tags()
    {
        Assert.NotEqual(VipiModuleExtensions.ReadinessTag, VipiModuleExtensions.FullTag);
    }
}

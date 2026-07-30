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
        Assert.Equal(expected, VipiHealthCheck.UsesEfMigrations(providerName));
    }
}

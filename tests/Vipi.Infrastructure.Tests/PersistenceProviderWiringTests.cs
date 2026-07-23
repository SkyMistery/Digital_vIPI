using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Infrastructure;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Branch di selezione provider in <see cref="DependencyInjection.AddVipiInfrastructure(IServiceCollection,string,IConfiguration?)"/>:
/// SQLite (default) registra il DbContext; Postgres fallisce con il rimando all'ADR (cutover non attuato).
/// </summary>
public class PersistenceProviderWiringTests
{
    private static IConfiguration Config(string? provider)
    {
        var dict = new Dictionary<string, string?>();
        if (provider is not null) dict["Persistence:Provider"] = provider;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Sqlite_default_registers_dbcontext()
    {
        var services = new ServiceCollection();
        services.AddVipiInfrastructure("Data Source=:memory:", Config(provider: null));
        Assert.Contains(services, d => d.ServiceType == typeof(Vipi.Infrastructure.Persistence.VipiDbContext));
    }

    [Fact]
    public void Postgres_selection_fails_with_adr_pointer()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddVipiInfrastructure("Host=localhost;Database=vipi", Config("Postgres")));
        Assert.Contains("adr-0007", ex.Message);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Infrastructure;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Branch di selezione provider in <see cref="DependencyInjection.AddVipiInfrastructure(IServiceCollection,string,IConfiguration?)"/>:
/// SQLite (default) e Postgres (deploy hostato Render+Neon, schema via EnsureCreated) registrano entrambi il DbContext.
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
    public void Postgres_selection_registers_dbcontext()
    {
        var services = new ServiceCollection();
        services.AddVipiInfrastructure("Host=localhost;Database=vipi", Config("Postgres"));
        Assert.Contains(services, d => d.ServiceType == typeof(Vipi.Infrastructure.Persistence.VipiDbContext));
    }
}

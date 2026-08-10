using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Infrastructure;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Branch di selezione provider in <see cref="DependencyInjection.AddVipiInfrastructure(IServiceCollection,string,IConfiguration?)"/>:
/// SQLite (sviluppo, default), Postgres (deploy di prova Render+Neon, schema via EnsureCreated) e MySQL
/// (produzione su atc.it.ivao.aero, schema da migrazioni dedicate) registrano tutti il DbContext.
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

    private const string ConnessioneMySql = "Server=localhost;Port=3306;Database=itivao_atc;User Id=u;Password=p";

#if NET8_0
    [Fact]
    public void MySql_selection_registers_dbcontext()
    {
        var services = new ServiceCollection();
        services.AddVipiInfrastructure(ConnessioneMySql, Config("MySql"));
        Assert.Contains(services, d => d.ServiceType == typeof(Vipi.Infrastructure.Persistence.VipiDbContext));
    }
#else
    /// <summary>
    /// Su net10 il provider MariaDB non esiste — Pomelo non ha una build per EF Core 10 — e il ramo deve
    /// fallire con un messaggio che lo dice. Non è un dettaglio di cortesia: senza, chi imposta
    /// <c>Persistence:Provider=MySql</c> su un host net10 andrebbe a cercare un errore di battitura nella
    /// configurazione invece di leggere che quel target non è quello giusto.
    /// </summary>
    [Fact]
    public void MySql_selection_su_net10_fallisce_spiegando_perche()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVipiInfrastructure(ConnessioneMySql, Config("MySql")));

        Assert.Contains("net8.0", ex.Message);
        Assert.Contains("Pomelo", ex.Message);
    }
#endif
}

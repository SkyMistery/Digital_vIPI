using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Selezione provider persistenza (ADR-0007): default SQLite, case-insensitive, sconosciuto ⇒ errore chiaro.</summary>
public class PersistenceProviderResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_value_defaults_to_sqlite(string? value)
    {
        Assert.Equal(PersistenceProvider.Sqlite, PersistenceProviderResolver.Resolve(value));
    }

    [Theory]
    [InlineData("Sqlite", PersistenceProvider.Sqlite)]
    [InlineData("sqlite", PersistenceProvider.Sqlite)]
    [InlineData("Postgres", PersistenceProvider.Postgres)]
    [InlineData(" POSTGRES ", PersistenceProvider.Postgres)]
    [InlineData("MySql", PersistenceProvider.MySql)]
    [InlineData(" mysql ", PersistenceProvider.MySql)]
    public void Parses_known_providers_case_insensitively(string value, PersistenceProvider expected)
    {
        Assert.Equal(expected, PersistenceProviderResolver.Resolve(value));
    }

    /// <summary>
    /// Il valore d'esempio qui era «MySql» finché MySQL non era supportato: dal 5 agosto 2026 è un provider
    /// valido (ADR-0007 §D4-bis) e il caso è passato fra quelli riconosciuti, sopra. Serve un valore che
    /// resti plausibile e sbagliato — un provider che non useremo.
    /// </summary>
    [Fact]
    public void Unknown_provider_throws_with_valid_values()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PersistenceProviderResolver.Resolve("SqlServer"));
        Assert.Contains("Sqlite", ex.Message);
        Assert.Contains("Postgres", ex.Message);
        Assert.Contains("MySql", ex.Message);
    }
}

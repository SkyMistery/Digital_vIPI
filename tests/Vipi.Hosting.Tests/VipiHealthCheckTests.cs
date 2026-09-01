using Vipi.Application.Diagnostics;
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

    /// <summary>
    /// ⚠️ Le divergenze col <b>sectorfile</b> non degradano la salute dell'istanza, e questa non è una
    /// sfumatura: ce n'è sempre qualcuna — le due sorgenti hanno cadenze diverse, IVAO in continuo e il
    /// sectorfile per ciclo AIRAC — quindi contarle qui vorrebbe dire <c>/vsop/health</c> perennemente
    /// «Degraded». Un monitor sempre giallo è un monitor spento, e con lui si spengono i guasti veri.
    ///
    /// <para>Il conteggio resta comunque nel corpo della risposta: saperlo è comodo, e non costa niente.</para>
    /// </summary>
    [Fact]
    public void Le_divergenze_col_sectorfile_non_degradano_la_salute()
    {
        ConsistencyFinding Rilievo(ConsistencyArea area) =>
            new("x", ConsistencySeverity.Warning, "x", "x", area);

        var findings = new[]
        {
            Rilievo(ConsistencyArea.Sectorfile),
            Rilievo(ConsistencyArea.Sectorfile),
            Rilievo(ConsistencyArea.Dati),
        };

        Assert.Equal(1, VipiHealthCheck.ContaIncongruenze(findings));
        Assert.Equal(2, VipiHealthCheck.ContaDivergenzeSectorfile(findings));

        // Il caso che conta davvero: SOLO divergenze col sectorfile ⇒ zero incongruenze ⇒ Healthy.
        var soloSectorfile = new[] { Rilievo(ConsistencyArea.Sectorfile) };
        Assert.Equal(0, VipiHealthCheck.ContaIncongruenze(soloSectorfile));
    }
}

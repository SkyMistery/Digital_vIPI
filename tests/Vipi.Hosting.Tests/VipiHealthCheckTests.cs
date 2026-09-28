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
            new("x", ConsistencySeverity.Error, "x", "x", area);

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

    /// <summary>
    /// U-100 (revisione 3): gli <b>avvisi</b> non degradano la salute, in nessuna area. Il report ne ha di
    /// permanenti su stati previsti («Shape sintetica» di ogni TWR col cerchio da 5 NM, «Settore senza
    /// poligono» delle FSS), e contarli teneva <c>/vsop/health</c> «Degraded» dalla 1.26.1: un guasto vero
    /// all'avvio non cambiava niente. Scelta del committente (28-set-2026): Degraded solo per gli errori.
    /// Gli avvisi restano nel corpo come numero.
    /// </summary>
    [Fact]
    public void Gli_avvisi_non_degradano_la_salute_gli_errori_si()
    {
        ConsistencyFinding Rilievo(ConsistencySeverity gravita, ConsistencyArea area) =>
            new("x", gravita, "x", "x", area);

        var soloAvvisi = new[]
        {
            Rilievo(ConsistencySeverity.Warning, ConsistencyArea.Sorgente),
            Rilievo(ConsistencySeverity.Warning, ConsistencyArea.Dati),
            Rilievo(ConsistencySeverity.Warning, ConsistencyArea.Avvio),
        };
        Assert.Equal(0, VipiHealthCheck.ContaIncongruenze(soloAvvisi));
        Assert.Equal(3, VipiHealthCheck.ContaAvvisi(soloAvvisi));

        // La passata d'avvio fallita (StartupMaintenance la scrive Error) deve muovere il verdetto.
        var conGuasto = soloAvvisi.Append(Rilievo(ConsistencySeverity.Error, ConsistencyArea.Avvio)).ToArray();
        Assert.Equal(1, VipiHealthCheck.ContaIncongruenze(conGuasto));
        Assert.Equal(3, VipiHealthCheck.ContaAvvisi(conGuasto));

        // Gli avvisi del sectorfile non si contano due volte: hanno già il loro numero.
        Assert.Equal(0, VipiHealthCheck.ContaAvvisi(new[] { Rilievo(ConsistencySeverity.Warning, ConsistencyArea.Sectorfile) }));
    }
}

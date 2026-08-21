using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Ogni rilievo dichiara <b>di chi è il problema</b>.
///
/// <para>Fino al 22 agosto 2026 la pagina si presentava come «incongruenze dei riferimenti deboli
/// (soft-ref)» e nella stessa tabella potevano comparire il drift di schema, le impostazioni del server di
/// database, il guasto di una manutenzione d'avvio e «nessuno può editare» — cinque famiglie presentate come
/// una, e chi legge non sapeva se aprire un editor, il pannello del server o un file di configurazione.</para>
///
/// <para>⚠️ Questi test sono la rete di quella scelta: l'area è un parametro <b>obbligatorio</b> proprio
/// perché non abbia un default, ma niente impedisce a un controllo nuovo di dichiarare quella sbagliata.
/// Qui si verifica che ogni produttore risponda per la sua.</para>
/// </summary>
public class ConsistencyAreaTests
{
    [Fact]
    public void I_rilievi_sui_soft_ref_sono_dell_area_Dati()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[]
            {
                new TransferConditionRow(1, "LIRR", "VALMA", 99001, null, "LI R99Z"),
                new TransferConditionRow(2, "LIRR", "EKMUR", 10, "Pista 34R", null),
            },
            RunwayIdents = new Dictionary<int, string> { [10] = "16R" },
            ParentRefs = new[] { new ParentRefRow("Settore APT", "LIRF_TWR", "LIXX_APP") },
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRF_TWR" },
            RegulatedRefs = new[] { new RegulatedRefRow("vIPI", "Roma ACC", """{"OwnIds":["999"],"ExtraIds":[]}""") },
        };

        var findings = ConsistencyReportService.Analyze(d);

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(ConsistencyArea.Dati, f.Area));
        // e sono davvero le famiglie attese, non una sola ripetuta
        Assert.True(findings.Select(f => f.Category).Distinct().Count() >= 4);
    }

    /// <summary>
    /// I quattro modi in cui il server può non essere come l'app lo assume. ⚠️ Un caso solo non basterebbe:
    /// i rilievi nascono in quattro rami diversi dell'analizzatore, e l'area va dichiarata in ognuno.
    /// (Un <c>Theory</c> qui non si può: <c>InlineData</c> passa gli interi come <c>int</c> e il parametro è
    /// <c>long?</c>.)
    /// </summary>
    [Fact]
    public void I_rilievi_sulle_impostazioni_del_server_sono_dell_area_Server()
    {
        (string? SqlMode, long? Packet)[] casi =
        {
            (null, 8_388_608L),                       // sql_mode illeggibile
            ("NO_ENGINE_SUBSTITUTION", 8_388_608L),   // niente strict mode
            ("STRICT_TRANS_TABLES", null),            // packet illeggibile
            ("STRICT_TRANS_TABLES", 1024L),           // packet sotto la soglia
        };

        foreach (var (sqlMode, packet) in casi)
        {
            var findings = ServerSettingsAnalyzer.Analyze(sqlMode, packet);
            Assert.NotEmpty(findings);
            Assert.All(findings, f => Assert.Equal(ConsistencyArea.Server, f.Area));
        }
    }

    [Fact]
    public void Il_guasto_di_una_passata_d_avvio_e_dell_area_Avvio()
    {
        var report = new StartupMaintenanceReport();
        report.Record("proiezione dei settori dai cataloghi", new InvalidOperationException("boom"));

        var f = Assert.Single(report.Findings);
        Assert.Equal(ConsistencyArea.Avvio, f.Area);
        Assert.Equal(ConsistencySeverity.Error, f.Severity);
    }

    /// <summary>
    /// ⚠️ «Nessuno può editare» NON è un problema di dati: è configurazione, e si corregge fuori
    /// dall'applicazione. È anche il rilievo più grave che l'applicazione sappia produrre — finché è vero,
    /// nessuno può nemmeno distribuire i permessi per rimediare.
    /// </summary>
    [Fact]
    public void Il_drift_di_schema_e_dell_area_Schema()
    {
        var findings = SchemaDriftAnalyzer.Compare(
            model: new[] { new SchemaColumn("Documents", "Titolo", "TEXT") },
            actual: new[] { new SchemaColumn("Documents", "Title", "TEXT") });

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(ConsistencyArea.Schema, f.Area));
    }
}

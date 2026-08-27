using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// C7a — il regime di scrittura dell'applicazione sta in <b>una riga sola</b>: se sparisce, l'applicazione
/// torna a «la sorgente scrive tutto» e il primo giro dopo sovrascrive TA e piste messe a mano. Il dato per
/// accorgersene c'era già (<see cref="ImportPolicyInfo"/> distingue «decisa da qualcuno» da «nata dai
/// default»): mancava chi lo dicesse.
/// </summary>
public class PolicyImportRilievoTests
{
    [Fact]
    public void Riga_assente_e_un_rilievo()
    {
        var info = new ImportPolicyInfo(ImportPolicySnapshot.AllImported, null, 0, RigaPresente: false);

        var f = Assert.Single(ConsistencyReportService.PolicyDiImport(info));

        Assert.Equal(ConsistencySeverity.Warning, f.Severity);
        Assert.Equal(ConsistencyArea.Dati, f.Area);
        Assert.Equal("/services/vsop/admin/sources", f.Where);
        Assert.Equal("Diag_Cat_PolicyAssente", f.CategoryKey);
    }

    [Fact]
    public void Policy_decisa_da_una_persona_non_produce_niente()
    {
        var manuale = new ImportPolicySnapshot(TransitionAltitude: false, Runways: false, Sectors: true);
        var info = new ImportPolicyInfo(manuale, new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc), 42);

        Assert.Empty(ConsistencyReportService.PolicyDiImport(info));
    }

    [Fact]
    public void Categorie_manuali_mai_decise_da_nessuno_sono_un_rilievo_e_si_nominano()
    {
        // Il caso ImportSids: colonna nata false su un DB già popolato, nessuno ha mai salvato.
        var info = new ImportPolicyInfo(new ImportPolicySnapshot(true, true, true, Sids: false), null, 0);

        var f = Assert.Single(ConsistencyReportService.PolicyDiImport(info));

        Assert.Equal("Diag_Cat_PolicyMaiDecisa", f.CategoryKey);
        Assert.Contains(nameof(ImportCategory.Sids), f.Detail);
        Assert.Equal(new object[] { nameof(ImportCategory.Sids) }, f.DetailArgs);
    }

    [Fact]
    public void Tutto_da_sorgente_e_mai_toccata_e_il_default_dichiarato_non_un_anomalia()
    {
        // La riga c'è, dice «tutto da sorgente» e nessuno l'ha firmata: è il default del prodotto. Mostrarlo
        // a ogni apertura della pagina insegnerebbe solo a ignorare la diagnostica.
        var info = new ImportPolicyInfo(ImportPolicySnapshot.AllImported, null, 0);

        Assert.Empty(ConsistencyReportService.PolicyDiImport(info));
    }
}

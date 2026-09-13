using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La ri-verifica del roster staff sta sul giro a timbro (T-033, 13 settembre 2026): una chiave sua, e la
/// cadenza dichiarata come quella degli altri giri.
///
/// <para>Prima era un <c>PeriodicTimer</c> senza stato: primo giro dopo 24 ore di processo acceso, cioè mai
/// sotto Passenger. Il sorgente del servizio si guarda qui sotto perché il giro vero aspetta minuti prima di
/// partire, e un test che lo aspettasse non lo farebbe nessuno.</para>
/// </summary>
public class VerificaRosterSulGiroATimbroTests
{
    [Fact]
    public void La_verifica_del_roster_ha_una_cadenza_dichiarata()
    {
        var orario = new ImportSchedule(
            Options.Create(new IvaoOptions { StaffVerifyHours = 24 }), Options.Create(new SectorfileOptions()));

        Assert.Equal(TimeSpan.FromHours(24), orario.PeriodOf(ImportCategories.StaffRoster));
    }

    [Fact]
    public void Il_servizio_gira_sul_giro_a_timbro_e_non_su_un_timer_senza_stato()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        const string relativo = "src/Vipi.Infrastructure/Ivao/StaffRosterVerificationService.cs";
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relativo))) dir = dir.Parent;
        Assert.NotNull(dir);

        var sorgente = File.ReadAllText(Path.Combine(dir!.FullName, relativo));
        Assert.Contains("GatedImportLoop.RunAsync(_scopes, ImportCategories.StaffRoster", sorgente);
        Assert.DoesNotContain("new PeriodicTimer", sorgente);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le registrazioni che si contano <b>per famiglia di documento</b> — e che una famiglia nuova fa crescere.
///
/// <para>
/// ⚠️ <b>Perché contare è una prova e non una pignoleria.</b> Due dei registri del sistema —
/// <c>FrozenSectionRegistry</c> e <c>DocRoutes</c> — sono dizionari risolti per tipo: una famiglia che manca
/// non è un errore, è una risposta vuota. Il difetto dei vSOP militari del 28 agosto 2026 è stato esattamente
/// questo: nessun <c>IFrozenSectionProvider</c> per <c>AirportMil</c>, e pubblicare non congelava niente
/// <b>in silenzio</b>. Un conteggio non impedisce di sbagliare, ma costringe chi aggiunge una famiglia a
/// passare di qui e a dire che cosa ha deciso.
/// </para>
/// </summary>
public class RegistrazioniPerFamigliaTests
{
    private static IServiceCollection Registrate() => new ServiceCollection().AddVipiApplication();

    [Fact]
    public void Le_famiglie_che_CONGELANO_sono_cinque()
    {
        // vLOA · vIPI APP · vIPI ACC · vIPI aeroporto · vSOP militare d'aeroporto.
        // ⚠️ `AppMil` NON c'è, ed è voluto: non esiste una porta che crei quel documento, quindi non esiste
        // niente da congelare. Va aggiunto INSIEME alla sua pagina — vedi `AppMilDocRoutes`.
        var provider = Registrate()
            .Where(d => d.ServiceType == typeof(IFrozenSectionProvider))
            .ToList();

        Assert.Equal(5, provider.Count);
    }

    [Fact]
    public void L_edizione_MILITARE_ha_la_sua_cattura()
    {
        // Il provider militare è lo STESSO tipo del civile — un motore solo, due bersagli — quindi si
        // registra con una fabbrica invece che per tipo. È l'unica del gruppo, e questo test è ciò che
        // impedisce di toglierla «perché sembra un doppione di quella sopra».
        var fabbriche = Registrate()
            .Where(d => d.ServiceType == typeof(IFrozenSectionProvider) && d.ImplementationFactory is not null)
            .ToList();

        Assert.Single(fabbriche);
    }

    [Fact]
    public void Ogni_famiglia_di_release_ha_le_sue_ROTTE()
    {
        // Sei descrittori per sei valori di ReleaseTargetType. ⚠️ `AppMil` è registrato ma dichiara rotte
        // NULLE finché le sue pagine non esistono: è dichiarato, non promesso.
        var rotte = Registrate()
            .Where(d => d.ServiceType == typeof(IDocKindRoutes))
            .ToList();

        Assert.Equal(6, rotte.Count);
    }
}

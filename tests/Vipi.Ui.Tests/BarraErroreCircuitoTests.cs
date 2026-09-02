using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La barra che compare quando il circuito muore.
///
/// <para>🔴 <b>Perché ha un test.</b> Blazor non ricarica e non dice niente da sé: cerca nel documento un
/// elemento con <c>id="blazor-error-ui"</c> e mostra quello. Se l'id cambia — o se qualcuno «pulisce» il
/// markup — il guasto torna <b>muto</b>: la pagina resta a schermo, i tasti smettono di funzionare, e chi
/// la usa non ha modo di capire perché. È esattamente com'è arrivata la segnalazione del 2 settembre 2026:
/// «clicco e non succede nulla».</para>
/// </summary>
public class BarraErroreCircuitoTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public BarraErroreCircuitoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    /// <summary>⚠️ L'id è quello e non un altro: lo cerca il JavaScript di Blazor.</summary>
    [Fact]
    public void L_id_e_quello_che_cerca_blazor()
    {
        var cut = RenderComponent<BarraErroreCircuito>();

        var bar = cut.Find("#blazor-error-ui");
        Assert.NotNull(bar);
    }

    /// <summary>Dentro ci vanno le due vie d'uscita: ricaricare, o togliersi la barra di mezzo.</summary>
    [Fact]
    public void Porta_il_ricarica_e_il_chiudi()
    {
        var cut = RenderComponent<BarraErroreCircuito>();

        Assert.NotNull(cut.Find("#blazor-error-ui a.reload"));
        Assert.NotNull(cut.Find("#blazor-error-ui a.dismiss"));
    }
}

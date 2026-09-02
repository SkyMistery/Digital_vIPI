using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il timbro di «Validità e revisione». Qui non si presidia che cosa mostra — quello sta nel servizio — ma
/// <b>quante volte lo chiede</b>.
///
/// <para>🔴 Questo componente compare in <b>ogni</b> documento, e su una vIPI ACC anche una volta per blocco,
/// e non aveva <b>nessuna</b> guardia: <c>OnParametersSetAsync</c> scatta a ogni ridisegno del genitore, non
/// solo quando i parametri cambiano, quindi rileggeva il database a ogni giro — con la lettura precedente
/// magari ancora in volo, cioè due operazioni sullo <b>stesso</b> contesto. È il fratello del difetto che il
/// 2 settembre 2026 ha abbattuto tre circuiti in produzione da <c>AttachmentBlockEditor</c>.</para>
///
/// <para>⚠️ Lo scope proprio (<c>OwningComponentBase</c>) <b>non</b> bastava, e la nota in cima al componente
/// lo lasciava credere: protegge dal contesto <b>del circuito</b>, non da <b>sé stessi</b>.</para>
/// </summary>
public class TimbroValiditaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Conta le letture: è tutto quel che serve a queste due prove.</summary>
    private sealed class ValiditaFinta : IDocumentValidityService
    {
        public int Letture { get; private set; }

        public Task<DocumentValidityStamp> ResolveAsync(
            ReleaseTargetType type, string key, int? releaseId = null, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(new DocumentValidityStamp(
                Published: true, AiracCycle: "2608",
                EffectiveUtc: new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
                ReviewerVid: 704798, ReviewerName: "Carmine", ReviewerPositions: new[] { "IT-AOA1" }));
        }
    }

    private readonly ValiditaFinta _validita = new();

    public TimbroValiditaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IDocumentValidityService>(_validita);
    }

    private IRenderedComponent<ValidityStamp> Timbro() =>
        RenderComponent<ValidityStamp>(p => p
            .Add(x => x.Target, ReleaseTargetType.Airport)
            .Add(x => x.Key, "LIBD"));

    [Fact]
    public void Ridisegnare_con_gli_stessi_parametri_non_rilegge_il_timbro()
    {
        var cut = Timbro();
        Assert.Equal(1, _validita.Letture);

        for (var i = 0; i < 5; i++)
            cut.SetParametersAndRender(p => p.Add(x => x.Key, "LIBD"));

        Assert.Equal(1, _validita.Letture);
    }

    /// <summary>
    /// Ma quando cambia la release guardata — è l'anteprima di una pubblicazione precisa — si rilegge: la
    /// guardia serve a non ripetersi, non a impedire di cambiare domanda. ⚠️ E il ciclo di una release
    /// diversa è diverso: una guardia sulla sola chiave mostrerebbe il timbro di un'altra pubblicazione.
    /// </summary>
    [Fact]
    public void Cambiare_la_release_guardata_rilegge()
    {
        var cut = Timbro();
        Assert.Equal(1, _validita.Letture);

        cut.SetParametersAndRender(p => p.Add(x => x.ReleaseId, 57));
        Assert.Equal(2, _validita.Letture);

        cut.SetParametersAndRender(p => p.Add(x => x.Key, "LIRF"));
        Assert.Equal(3, _validita.Letture);
    }
}

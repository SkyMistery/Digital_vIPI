using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il riquadro «Sezioni da scrivere» degli editor e della pagina Versioni (carta
/// <c>docs/feature/2026-10-18-sezioni-da-scrivere.md</c>).
/// </summary>
public class SezioniDaScriverePanelTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class ServizioFinto : ISezioniDaScrivereService
    {
        public StatoSezioni? Stato { get; set; }
        public int Letture { get; private set; }

        public Task<IReadOnlyDictionary<int, StatoSezioni>> QuadroAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, StatoSezioni>>(new Dictionary<int, StatoSezioni>());

        public Task<StatoSezioni?> DocumentoAsync(int documentId, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(Stato);
        }
    }

    private readonly ServizioFinto _servizio = new();

    public SezioniDaScriverePanelTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<ISezioniDaScrivereService>(_servizio);
    }

    private static StatoSezioni Milano() => new(7, new[]
    {
        new ContoSezioni("Settori di aerovia", 2, Array.Empty<SezioneVuota>()),
        new ContoSezioni("Milano APP", 4, new[]
        {
            new SezioneVuota(11, "vfr", "Gestione del traffico › VFR"),
            new SezioneVuota(12, "regulated", "Aree regolamentate"),
        }),
    });

    [Fact]
    public void Dice_quante_e_quali_sono_vuote_blocco_per_blocco()
    {
        _servizio.Stato = Milano();

        var cut = RenderComponent<SezioniDaScriverePanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Contains("amber", cut.Find(".sds-count .pill").ClassName);
        Assert.Equal(new[] { "Settori di aerovia", "Milano APP" }, cut.FindAll(".sds-part b").Select(b => b.TextContent));
        Assert.Equal(new[] { "Gestione del traffico › VFR", "Aree regolamentate" },
            cut.FindAll(".sds-empty li").Select(li => li.TextContent));
    }

    [Fact]
    public void Tutto_scritto_e_verde()
    {
        _servizio.Stato = new StatoSezioni(3, new[] { new ContoSezioni(null, 6, Array.Empty<SezioneVuota>()) });

        var cut = RenderComponent<SezioniDaScriverePanel>(p => p.Add(x => x.DocumentId, 3));

        Assert.Contains("green", cut.Find(".sds-count .pill").ClassName);
        Assert.Empty(cut.FindAll(".sds-empty"));
        Assert.Contains("SdS_AllDone", cut.Markup);
    }

    [Fact]
    public void Senza_documento_non_compare()
    {
        var cut = RenderComponent<SezioniDaScriverePanel>(p => p.Add(x => x.DocumentId, (int?)null));

        Assert.Empty(cut.FindAll("[data-sezioni-da-scrivere]"));
        Assert.Equal(0, _servizio.Letture);
    }

    /// <summary>
    /// Si rilegge a «Fine modifica», non a ogni ridisegno del genitore: durante la modifica il numero cambierebbe a
    /// ogni salvataggio, e ogni ridisegno sarebbe una lettura del database.
    /// </summary>
    [Fact]
    public void Si_rilegge_a_fine_modifica_e_non_a_ogni_ridisegno()
    {
        _servizio.Stato = Milano();
        var cut = RenderComponent<SezioniDaScriverePanel>(p => p.Add(x => x.DocumentId, 7).Add(x => x.InModifica, false));
        Assert.Equal(1, _servizio.Letture);

        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, true));
        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, true));
        Assert.Equal(1, _servizio.Letture);

        _servizio.Stato = new StatoSezioni(7, new[] { new ContoSezioni(null, 8, Array.Empty<SezioneVuota>()) });
        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, false));

        Assert.Equal(2, _servizio.Letture);
        Assert.Contains("green", cut.Find(".sds-count .pill").ClassName);
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I tre slot di una SOTTO-sezione il cui corpo lo rende la pagina: sotto-sezioni «prima» → scheda →
/// sotto-sezioni «dopo» (doc 11 §3g).
///
/// <para>⚠️ Fino al 4 settembre 2026 <c>SectionNode</c> rendeva la scheda e poi TUTTE le figlie: una figlia
/// marcata «sopra il corpo» usciva sotto, mentre l'editor la mostrava sopra. Non lo vedeva nessun test
/// perché gli altri due lettori — <c>DocumentSectionsView</c> (radici) e <c>AccSectionBody</c> (blocchi
/// ACC) — i tre slot li fanno giusti: il difetto stava sul terzo, cioè sulle figlie rese dalla pagina
/// (<c>frequenze</c>, <c>piste</c>, <c>quote di transizione</c> del vSOP militare, dentro «Dati generali»).</para>
/// </summary>
public class SectionNodeSlotsTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa (stesso stratagemma di CoordinationCollapseTests).</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SectionNodeSlotsTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static SectionView Sez(string id, string titolo, string chiave, int depth,
        bool prima = false, bool nascosta = false, IReadOnlyList<SectionView>? figlie = null) => new()
    {
        Id = id,
        Title = titolo,
        Depth = depth,
        SectionKey = chiave,
        BeforeParentBody = prima,
        IsHidden = nascosta,
        Blocks = Array.Empty<BlockView>(),
        Children = figlie ?? Array.Empty<SectionView>(),
    };

    /// <summary>La sotto-sezione resa dalla pagina, con una figlia «prima» e una «dopo».</summary>
    private static SectionView ConDueFiglie() => Sez("s-1", "Frequenze", "frequencies", 1, figlie: new[]
    {
        Sez("s-2", "NOTA-PRIMA", SectionKeys.NewCustom(), 2, prima: true),
        Sez("s-3", "NOTA-DOPO", SectionKeys.NewCustom(), 2),
    });

    private IRenderedComponent<SectionNode> Render(SectionView s, bool bozza = false) =>
        RenderComponent<SectionNode>(p => p
            .Add(x => x.Section, s)
            .Add(x => x.Profile, SectionProfile.App)
            .Add(x => x.IsDraft, bozza)
            .Add(x => x.DerivedContent, (RenderFragment<SectionView>)(sez => b =>
            {
                b.OpenElement(0, "p");
                b.AddContent(1, "SCHEDA-DELLA-PAGINA");
                b.CloseElement();
            })));

    [Fact]
    public void Una_figlia_marcata_prima_esce_sopra_la_scheda_della_pagina()
    {
        var markup = Render(ConDueFiglie()).Markup;

        var prima = markup.IndexOf("NOTA-PRIMA", StringComparison.Ordinal);
        var scheda = markup.IndexOf("SCHEDA-DELLA-PAGINA", StringComparison.Ordinal);
        var dopo = markup.IndexOf("NOTA-DOPO", StringComparison.Ordinal);

        Assert.True(prima >= 0 && scheda >= 0 && dopo >= 0, "mancano dei pezzi: " + markup);
        Assert.True(prima < scheda, "la figlia «prima» deve stare SOPRA la scheda");
        Assert.True(scheda < dopo, "la figlia «dopo» deve stare SOTTO la scheda");
    }

    /// <summary>Ogni figlia UNA volta sola: è il difetto gemello, quello che si prende sbagliando slot.</summary>
    [Fact]
    public void Le_figlie_non_si_rendono_due_volte()
    {
        var markup = Render(ConDueFiglie()).Markup;

        Assert.Equal(1, Occorrenze(markup, "NOTA-PRIMA"));
        Assert.Equal(1, Occorrenze(markup, "NOTA-DOPO"));
        Assert.Equal(1, Occorrenze(markup, "SCHEDA-DELLA-PAGINA"));
    }

    /// <summary>Una figlia nascosta resta fuori dal pubblico anche nello slot «prima»: lo slot cambia il
    /// posto, non il cancello.</summary>
    [Fact]
    public void Una_figlia_nascosta_resta_fuori_dal_pubblico_anche_sopra_la_scheda()
    {
        var s = Sez("s-1", "Frequenze", "frequencies", 1, figlie: new[]
        {
            Sez("s-2", "NOTA-NASCOSTA", SectionKeys.NewCustom(), 2, prima: true, nascosta: true),
        });

        Assert.DoesNotContain("NOTA-NASCOSTA", Render(s).Markup);
        Assert.Contains("NOTA-NASCOSTA", Render(s, bozza: true).Markup);
    }

    private static int Occorrenze(string testo, string ago)
    {
        var n = 0;
        for (var i = testo.IndexOf(ago, StringComparison.Ordinal); i >= 0;
             i = testo.IndexOf(ago, i + ago.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}

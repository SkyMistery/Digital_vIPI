using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il comando dell'ordinamento degli accordi: la <b>chiave</b> (una tendina) e il <b>verso</b> (un tasto), in un
/// pezzo solo. Committente, 9 ottobre 2026: ogni ordinamento si deve poter leggere al contrario — dalla Z alla A,
/// dalla quota più alta — in albero e in elenco.
///
/// <para>Le regole che questa rete prova vivono solo nel markup e nel filo fra il comando e la pagina: un verso
/// che resta acceso su «a mano», una tendina in elenco senza le chiavi dell'elenco, un tasto di riga tornato
/// glifo. Nessun altro test le vedrebbe rompersi.</para>
/// </summary>
public class XferSortChipTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public XferSortChipTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static readonly XferSortChip.Option[] Voci =
    {
        new("Manual", "a mano"), new("Points", "punti"), new("Level", "quota"),
    };

    private IRenderedComponent<XferSortChip> Render(string value, bool descending = false, bool canReverse = true,
        bool numeric = false, bool disabled = false, Action<string>? onKey = null, Action? onReverse = null) =>
        RenderComponent<XferSortChip>(p => p
            .Add(x => x.Options, Voci)
            .Add(x => x.Value, value)
            .Add(x => x.Descending, descending)
            .Add(x => x.CanReverse, canReverse)
            .Add(x => x.Numeric, numeric)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.OnKey, onKey ?? (_ => { }))
            .Add(x => x.OnReverse, onReverse ?? (() => { })));

    [Fact]
    public void La_tendina_porta_le_voci_date_e_la_chiave_scelta()
    {
        var cut = Render("Points");

        Assert.Equal(new[] { "Manual", "Points", "Level" }, cut.FindAll("select option").Select(o => o.GetAttribute("value")));
        Assert.Equal("Points", cut.Find("select").GetAttribute("value"));
    }

    [Theory]
    [InlineData(false, false, "A→Z")]
    [InlineData(false, true, "Z→A")]
    [InlineData(true, false, "1→9")]
    [InlineData(true, true, "9→1")]
    public void Il_tasto_del_verso_dice_il_verso_che_c_e(bool numerico, bool alContrario, string atteso)
    {
        var cut = Render(numerico ? "Level" : "Points", descending: alContrario, numeric: numerico);

        var verso = cut.Find(".xt-sortchip-dir");
        Assert.Equal(atteso, verso.TextContent.Trim());
        Assert.False(verso.HasAttribute("disabled"));
        Assert.Equal(alContrario ? "true" : "false", verso.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_mano_il_verso_resta_al_suo_posto_ma_spento_e_non_dice_Z_A()
    {
        // «A mano» non ha un verso: l'ordine è quello scritto, e nelle varianti è la struttura. Il tasto non
        // sparisce (la posizione di un comando è memoria) — e un «Z→A» rimasto da prima non deve leggersi.
        var cut = Render("Manual", descending: true, canReverse: false);

        var verso = cut.Find(".xt-sortchip-dir");
        Assert.True(verso.HasAttribute("disabled"));
        Assert.Equal("A→Z", verso.TextContent.Trim());
        Assert.Equal("false", verso.GetAttribute("aria-pressed"));
        Assert.Equal("Xfer_SortDirNone", verso.GetAttribute("title"));
    }

    [Fact]
    public void Scegliere_una_voce_e_premere_il_verso_tornano_alla_pagina()
    {
        string? chiave = null;
        var inversioni = 0;
        var cut = Render("Points", onKey: v => chiave = v, onReverse: () => inversioni++);

        cut.Find("select").Change("Level");
        cut.Find(".xt-sortchip-dir").Click();

        Assert.Equal("Level", chiave);
        Assert.Equal(1, inversioni);
    }

    [Fact]
    public void Spento_non_si_sceglie_e_non_si_inverte()
    {
        var cut = Render("Points", disabled: true);

        Assert.True(cut.Find("select").HasAttribute("disabled"));
        Assert.True(cut.Find(".xt-sortchip-dir").HasAttribute("disabled"));
        Assert.Contains("is-off", cut.Find(".xt-sortchip").ClassList);
    }

    // ---- Il filo con la pagina: regole che stanno nel sorgente ----

    private static readonly string Pagina = Sorgente("Pages", "AdminTrasferimentiPage.razor");
    private static readonly string Tabella = Sorgente("Components", "App", "XferRowsTable.razor");

    private static string Sorgente(params string[] pezzi)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "src", "Vipi.Ui" }.Concat(pezzi).ToArray()));
    }

    [Fact]
    public void La_barra_ordina_nelle_due_viste_e_in_elenco_porta_le_chiavi_dell_elenco()
    {
        // Il comando della barra non sta più dentro un `@if (_view == XferView.Tree)`: in elenco il committente
        // non trovava come ordinare (le intestazioni ordinavano, ma niente lo diceva).
        Assert.Matches(new Regex(@"<XferSortChip Label=""@L\[""Xfer_SortBy""\]""[^>]*Options=""ViewSortOptions\(\)""", RegexOptions.Singleline), Pagina);
        Assert.DoesNotContain("<select @bind=\"_sort\">", Pagina);

        var voci = Regex.Match(Pagina, @"private IReadOnlyList<XferSortChip\.Option> ViewSortOptions\(\).*?return voci;", RegexOptions.Singleline).Value;
        foreach (var chiave in new[] { "Manual", "Sender", "Receiver", "Airport", "Kind", "Points", "Level" })
            Assert.Contains($"nameof(RowSort.{chiave})", voci);
    }

    [Fact]
    public void Anche_in_albero_la_vista_segue_il_verso()
    {
        // Prima `_sortDesc` valeva solo in elenco: in albero «punti» era sempre dalla A.
        var corpo = Regex.Match(Pagina, @"private IReadOnlyList<AgreementClauseRow> SortedClauses\(.*?\n    }", RegexOptions.Singleline).Value;
        Assert.Contains("InDirection(base_, c => c.Cops)", corpo);
        Assert.Contains("InDirection(base_, LevelKey)", corpo);
        Assert.Contains("_sortDesc", Regex.Match(Pagina, @"private IOrderedEnumerable<T> InDirection<T>\(.*?\n    }", RegexOptions.Singleline).Value);
    }

    [Fact]
    public void L_ordine_salvato_della_sezione_ha_il_suo_verso_e_lo_salva()
    {
        Assert.Contains("OnReverse=\"() => SetClauseOrder(sec, AgreementClauseOrdering.Reverse(sec.ClauseOrder))\"", Pagina);
        Assert.Contains("Descending=\"@AgreementClauseOrdering.IsDescending(sec.ClauseOrder)\"", Pagina);
        // Il trascinamento resta spento con QUALUNQUE ordine dichiarato, verso opposto compreso: il confronto è
        // con «a mano», non con un elenco di ordini.
        Assert.Contains("sec.ClauseOrder == AgreementClauseOrder.Manual", Pagina);
    }

    [Fact]
    public void I_tasti_di_riga_sono_icone_del_set_e_non_glifi()
    {
        // Glifi presi ognuno dal font che capita hanno larghezze diverse, e la colonna dei tasti è a larghezza
        // fissa: col settimo tasto l'ultimo usciva dal bordo e la tabella scorreva di lato.
        var azioni = Regex.Match(Tabella, @"<div class=""xt-actrow"">.*?</td>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(azioni);
        foreach (var icona in new[] { "arrow-up-right", "split", "corner-down-right", "copy", "link", "pencil", "x" })
            Assert.Contains($"<Icon Name=\"{icona}\"", azioni);
        Assert.DoesNotMatch(new Regex(@">\s*[↗⑂↳⧉⛓✎✕]\s*</button>"), azioni);
    }
}

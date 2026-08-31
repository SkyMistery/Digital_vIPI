using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Tabella diff condivisa da ReleasePanel (editor) e VersioniPage (admin), prima duplicata identica nei due file
/// insieme a DiffPill e ChangeLabel.
/// </summary>
public class ReleaseDiffTableTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ReleaseDiffTableTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private IRenderedComponent<ReleaseDiffTable> Render(ReleaseDiff? diff) =>
        RenderComponent<ReleaseDiffTable>(p => p.Add(x => x.Diff, diff));

    [Fact]
    public void Diff_Nullo_Mostra_Il_Caricamento()
    {
        var cut = Render(null);

        Assert.Contains("Rel_LoadingDiff", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void Nessuna_Differenza_Mostra_Il_Messaggio_Dedicato()
    {
        var cut = Render(new ReleaseDiff(true, "2607", Array.Empty<ReleaseDiffRow>()));

        Assert.Contains("Rel_NoDiff", cut.Markup);
        Assert.Contains("Rel_BaselineCycle", cut.Markup);   // la baseline si mostra comunque
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void Rende_Una_Riga_Per_Differenza()
    {
        var diff = new ReleaseDiff(true, "2607", new[]
        {
            new ReleaseDiffRow("Separazioni", ReleaseChangeKind.Modified, 3, 5),
            new ReleaseDiffRow("Sezione VFR", ReleaseChangeKind.Added, null, 2),
            new ReleaseDiffRow("Minime", ReleaseChangeKind.Removed, 1, null),
        });

        var cut = Render(diff);

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Contains("Separazioni", cut.Markup);
        // I conteggi li formatta la UI (doc 13 §3k): il service consegna numeri, non frasi italiane.
        Assert.Contains("Rel_DiffItemsFromTo", cut.Markup);
        Assert.Contains("Rel_DiffItems", cut.Markup);
    }

    [Theory]
    [InlineData(ReleaseChangeKind.Added, "green")]
    [InlineData(ReleaseChangeKind.Removed, "grey")]
    [InlineData(ReleaseChangeKind.Modified, "blue")]
    public void Il_Colore_Della_Pill_Segue_Il_Tipo_Di_Modifica(ReleaseChangeKind change, string expectedPill)
    {
        var cut = Render(new ReleaseDiff(true, "2607", new[] { new ReleaseDiffRow("X", change, 1, 2) }));

        Assert.Contains(expectedPill, cut.Find("tbody tr span.pill").ClassName);
    }

    [Fact]
    public void Senza_Baseline_Si_Dice_Che_Non_Ce_Una_Release_In_Vigore()
    {
        var cut = Render(new ReleaseDiff(false, null, Array.Empty<ReleaseDiffRow>()));

        Assert.Contains("Rel_BaselineNone", cut.Markup);
    }

    [Fact]
    public void I_Valori_Dinamici_Escono_Encodati()
    {
        // Stessa garanzia degli altri componenti (vedi StructureComponentsTests): etichette e dettagli arrivano
        // dai contenuti editoriali, quindi non devono poter iniettare markup.
        var diff = new ReleaseDiff(true, "<b>base</b>", new[]
        {
            new ReleaseDiffRow("<script>alert(1)</script>", ReleaseChangeKind.Added, null, 1),
        });

        var cut = Render(diff);

        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.DoesNotContain("<img", cut.Markup);
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }
}

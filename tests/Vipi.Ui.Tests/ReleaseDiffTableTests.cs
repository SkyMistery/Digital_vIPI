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

    public ReleaseDiffTableTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

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
        var cut = Render(new ReleaseDiff(true, "AIRAC 2607", Array.Empty<ReleaseDiffRow>()));

        Assert.Contains("Rel_NoDiff", cut.Markup);
        Assert.Contains("AIRAC 2607", cut.Markup);   // la baseline si mostra comunque
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void Rende_Una_Riga_Per_Differenza()
    {
        var diff = new ReleaseDiff(true, "AIRAC 2607", new[]
        {
            new ReleaseDiffRow("Separazioni", "Modificata", "3 NM → 5 NM"),
            new ReleaseDiffRow("Sezione VFR", "Aggiunta", null),
            new ReleaseDiffRow("Minime", "Rimossa", "non più pubblicata"),
        });

        var cut = Render(diff);

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Contains("Separazioni", cut.Markup);
        Assert.Contains("3 NM → 5 NM", cut.Markup);
    }

    [Theory]
    [InlineData("Aggiunta", "green")]
    [InlineData("Rimossa", "grey")]
    [InlineData("Modificata", "blue")]
    [InlineData("Qualcos'altro", "blue")]     // tipo non previsto: colore neutro, non un errore
    public void Il_Colore_Della_Pill_Segue_Il_Tipo_Di_Modifica(string change, string expectedPill)
    {
        var cut = Render(new ReleaseDiff(true, "b", new[] { new ReleaseDiffRow("X", change, null) }));

        Assert.Contains(expectedPill, cut.Find("tbody tr span.pill").ClassName);
    }

    [Fact]
    public void Il_Tipo_Non_Previsto_Viene_Mostrato_Cosi_Come_E()
    {
        // I valori noti passano dal dizionario delle traduzioni; uno sconosciuto non deve sparire dalla vista.
        var cut = Render(new ReleaseDiff(true, "b", new[] { new ReleaseDiffRow("X", "Spostata", null) }));

        Assert.Contains("Spostata", cut.Markup);
    }

    [Fact]
    public void I_Valori_Dinamici_Escono_Encodati()
    {
        // Stessa garanzia degli altri componenti (vedi StructureComponentsTests): etichette e dettagli arrivano
        // dai contenuti editoriali, quindi non devono poter iniettare markup.
        var diff = new ReleaseDiff(true, "<b>base</b>", new[]
        {
            new ReleaseDiffRow("<script>alert(1)</script>", "Aggiunta", "<img src=x onerror=1>"),
        });

        var cut = Render(diff);

        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.DoesNotContain("<img", cut.Markup);
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }
}

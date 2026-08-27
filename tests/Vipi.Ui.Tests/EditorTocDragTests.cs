using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Riordino delle sezioni TRASCINANDO nel menu-sezioni (26 agosto 2026).
///
/// <para><b>Perché serve.</b> Il gesto attraversa tre pezzi — il pannello che lo raccoglie, la funzione pura che
/// lo traduce in «prima di questa», il motore che riscrive l'ordine — e i primi due sono qui. In particolare il
/// pannello è l'unico posto che sa a quale GRUPPO appartiene una voce: se sbagliasse, chiederebbe al motore una
/// mossa che il motore rifiuta in silenzio, e chi scrive vedrebbe solo una sezione che non si sposta.</para>
/// </summary>
public class EditorTocDragTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public EditorTocDragTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    /// <summary>Due gruppi (due blocchi di una vIPI ACC) più la voce del pannello Release, che sezione non è.</summary>
    private static readonly EditorTocItem[] Items =
    {
        new("s-10", "A", GroupLabel: "Blocco 1", SectionId: 10, DragGroup: "blk-1"),
        new("s-20", "B", GroupLabel: "Blocco 1", SectionId: 20, DragGroup: "blk-1"),
        new("s-30", "C", GroupLabel: "Blocco 1", SectionId: 30, DragGroup: "blk-1"),
        new("s-40", "D", GroupLabel: "Blocco 2", SectionId: 40, DragGroup: "blk-2"),
        new("p-release", "Release"),
    };

    private IRenderedComponent<EditorToc> Render(Action<TocReorder>? onReorder)
        => RenderComponent<EditorToc>(p =>
        {
            p.Add(x => x.Items, Items);
            if (onReorder is not null) p.Add(x => x.OnReorder, onReorder);
        });

    private static IElement Anchor(IRenderedComponent<EditorToc> c, string anchorId)
        => c.Find($"a[href='#{anchorId}']");

    private static void Drag(IRenderedComponent<EditorToc> c, string from, string onto)
    {
        Anchor(c, from).DragStart();
        Anchor(c, onto).Drop();
    }

    // Fuori dalla modifica l'host non passa OnReorder: l'indice resta un indice, e nemmeno l'attributo compare.
    [Fact]
    public void Without_OnReorder_nothing_is_draggable()
    {
        var c = Render(onReorder: null);
        Assert.All(c.FindAll("a"), a => Assert.False(a.HasAttribute("draggable")));
    }

    // ⚠️ Un <a href> nasce trascinabile da sé: la voce che sezione NON è (il pannello Release) va spenta a mano,
    // o si lascia prendere per poi non andare da nessuna parte.
    [Fact]
    public void Non_section_entries_are_explicitly_not_draggable()
    {
        var c = Render(_ => { });
        Assert.Equal("true", Anchor(c, "s-10").GetAttribute("draggable"));
        Assert.Equal("false", Anchor(c, "p-release").GetAttribute("draggable"));
    }

    [Fact]
    public void Dropping_downwards_asks_for_the_place_after_the_target()
    {
        TocReorder? got = null;
        var c = Render(r => got = r);

        Drag(c, from: "s-10", onto: "s-20");   // A sotto B -> prima di C

        Assert.Equal(new TocReorder(10, 30), got);
    }

    [Fact]
    public void Dropping_upwards_asks_for_the_target_place()
    {
        TocReorder? got = null;
        var c = Render(r => got = r);

        Drag(c, from: "s-30", onto: "s-10");   // C in testa -> prima di A

        Assert.Equal(new TocReorder(30, 10), got);
    }

    [Fact]
    public void Dropping_on_the_last_of_the_group_appends()
    {
        TocReorder? got = null;
        var c = Render(r => got = r);

        Drag(c, from: "s-10", onto: "s-30");

        // ⚠️ In coda al GRUPPO, non al pannello: «s-40» è di un altro blocco e non conta come fratello.
        Assert.Equal(new TocReorder(10, null), got);
    }

    // ⚠️ Il gruppo è il blocco: una sezione non passa da un blocco all'altro trascinandola (sarebbe una
    // riparentazione). Il rifiuto si vede qui, prima ancora di arrivare al motore.
    [Fact]
    public void Dropping_into_another_group_does_nothing()
    {
        var calls = 0;
        var c = Render(_ => calls++);

        Drag(c, from: "s-10", onto: "s-40");
        Drag(c, from: "s-10", onto: "p-release");

        Assert.Equal(0, calls);
    }

    // Lasciata dov'era: nessuna scrittura, nessun ricarico del documento.
    [Fact]
    public void Dropping_onto_itself_does_nothing()
    {
        var calls = 0;
        var c = Render(_ => calls++);

        Drag(c, from: "s-20", onto: "s-20");

        Assert.Equal(0, calls);
    }

    // La destinazione si illumina mentre ci si passa sopra, e SOLO se accetta.
    [Fact]
    public void Only_a_valid_target_shows_the_drop_mark()
    {
        var c = Render(_ => { });
        Anchor(c, "s-10").DragStart();

        Anchor(c, "s-20").DragEnter();
        Assert.Contains("toc-drop", Anchor(c, "s-20").ClassName);
        Assert.Contains("toc-dragging", Anchor(c, "s-10").ClassName);

        Anchor(c, "s-40").DragEnter();
        Assert.DoesNotContain("toc-drop", Anchor(c, "s-40").ClassName);
    }

    /// <summary>
    /// Il pezzo che i test qui sopra NON possono vedere, e che il 27 agosto 2026 era rotto in produzione con
    /// otto test verdi: perché il browser consegni il <c>drop</c>, qualcuno deve chiamare
    /// <c>preventDefault</c> sul <c>dragover</c> della voce. bUnit non lo sa — <c>Drop()</c> invoca il gestore
    /// direttamente, saltando la trattativa col browser — e il modificatore Razor che stava sul componente non
    /// faceva niente (Blazor ascolta un evento solo se qualcuno vi registra un gestore, e per <c>dragover</c>
    /// non ce n'era). Oggi lo fa <c>wireTocDrop</c> in vipi-ui.js: quella funzione e questo componente si
    /// parlano attraverso UN SELETTORE, scritto in due file diversi. Questo test lo legge dal JS e lo prova
    /// contro il markup vero, così una rinomina da una parte sola non passa.
    /// </summary>
    [Fact]
    public void Il_selettore_con_cui_il_JS_accetta_il_rilascio_trova_le_voci_trascinabili()
    {
        var js = File.ReadAllText(FileNellaWwwroot("vipi-ui.js"));

        // La funzione dev'essere agganciata, non solo definita: senza la chiamata in vipiWireUi non gira mai.
        Assert.Contains("wireTocDrop();", js, StringComparison.Ordinal);

        var corpo = Regex.Match(js, @"function\s+wireTocDrop\s*\(\)\s*\{(?<c>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(corpo.Success, "wireTocDrop non trovata in vipi-ui.js");
        Assert.Contains("'dragover'", corpo.Value, StringComparison.Ordinal);
        Assert.Contains("preventDefault()", corpo.Value, StringComparison.Ordinal);

        var sel = Regex.Match(corpo.Groups["c"].Value, @"closest\('(?<s>[^']+)'\)");
        Assert.True(sel.Success, "wireTocDrop non usa un closest('…'): il selettore non è più leggibile da qui");
        var selettore = sel.Groups["s"].Value;

        // In modifica il selettore deve pescare TUTTE e sole le voci-sezione: quattro, non il pannello Release.
        var inModifica = Render(_ => { });
        Assert.Equal(4, inModifica.FindAll(selettore).Count);

        // Fuori dalla modifica non deve pescare niente, o il menu accetterebbe rilasci che nessuno gestisce.
        var fuori = Render(onReorder: null);
        Assert.Empty(fuori.FindAll(selettore));
    }

    private static string FileNellaWwwroot(string nome)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot", nome);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{nome} non trovato risalendo da {AppContext.BaseDirectory}");
    }
}

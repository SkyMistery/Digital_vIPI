using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La sezione «Aree regolamentate» come UNA mappa con le chip (27 agosto 2026).
///
/// <para><b>Perché serve.</b> Prima ogni area aveva la propria mappina — su LIRR 105 — e la ragione per cui
/// sono state tolte è misurabile: il numero di contenitori mappa in pagina. Un test lo fissa, perché una
/// regressione qui non si vede a schermo (le mappe funzionerebbero) ma si sente sulla rete.</para>
///
/// <para>L'altra metà del contratto è col JS: la chip accende l'area sulla mappa <b>e</b> la sua descrizione,
/// e i due pezzi si trovano attraverso attributi scritti in due file diversi.</para>
/// </summary>
public class RegulatedAreasTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<Vipi.Ui.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public RegulatedAreasTests() =>
        Services.AddSingleton<IStringLocalizer<Vipi.Ui.SharedResource>>(new KeyLocalizer());

    private static AppAorPolygon Poly() =>
        new("0 0 10 10", "M0 0 L10 0 L10 10 Z",
            new List<double[]> { new[] { 41.0, 12.0 }, new[] { 41.5, 12.5 }, new[] { 41.0, 12.5 } },
            41.0, 12.0, 41.5, 12.5, 41.25, 12.25);

    private static readonly AccSpecialAreaView[] Aree =
    {
        new("1113", "LI R300A Amendola", "R", "descrizione R", "Permanently active", 0, 4000, Poly()),
        new("1014", "LI D409A", "D", "descrizione D", "H24", 1500, 14500, Poly()),
        new("777", "TSA senza shape", "TSA", null, null, null, null, null),
    };

    private IRenderedComponent<RegulatedAreas> Render(IReadOnlyList<AccSpecialAreaView>? aree = null, string? blocco = "blk-1")
        => RenderComponent<RegulatedAreas>(p =>
        {
            p.Add(x => x.Areas, aree ?? Aree);
            p.Add(x => x.BlockKey, blocco);
        });

    // ⚠️ Il motivo per cui la sezione è stata rifatta. Su LIRR erano 105 contenitori mappa: uno solo.
    [Fact]
    public void Una_mappa_sola_non_una_per_area()
    {
        var c = Render();

        Assert.Single(c.FindAll(".aor-leaflet"));
        Assert.Empty(c.FindAll(".area-map"));      // la classe delle mappine non deve tornare
        Assert.Equal(Aree.Length, c.FindAll("[data-areacard]").Count);
    }

    [Fact]
    public void Una_chip_per_area_con_lid_come_chiave_e_il_nome_come_etichetta()
    {
        var c = Render();

        // Scoping alla vista 2D: le stesse chip esistono anche nel riquadro 3D (le pilota lo stesso JS).
        // ⚠️ `.ToList()`: l'indicizzatore di RefreshableElementCollection cade su un MissingMethodException
        // di AngleSharp in questa combinazione di versioni. Enumerare va bene, indicizzare no.
        var chip = c.FindAll(".aor-view-2d .aor-chip").ToList();
        Assert.Equal(Aree.Length, chip.Count);
        Assert.Equal(Aree.Length, c.FindAll(".aor-view-3d .aor-chip").Count);
        Assert.Equal(new[] { "1113", "1014", "777" }, chip.Select(x => x.GetAttribute("data-sec")));
        // L'etichetta è il nome senza «LI »; il nome intero resta nel title, che è dove uno lo cerca.
        Assert.Contains("R300A Amendola", chip[0].TextContent);
        Assert.Equal("LI R300A Amendola", chip[0].GetAttribute("title"));
    }

    // ⚠️ La fila di tasti per tipo è disegnata da AccAor leggendo `View.Configs`: costruire i preset e non
    // metterli nella vista è come non averli, ed è successo — alla prima prova dal vivo su LIRR, con 105
    // chip e nessun modo di filtrarle. Il test guarda i TASTI, che è il punto in cui il difetto si vedeva.
    [Fact]
    public void I_tasti_per_tipo_ci_sono_uno_per_tipo()
    {
        var c = Render();

        var tasti = c.FindAll(".aor-view-2d .cfg-btn").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "R", "D", "TSA" }, tasti);   // ordine del catalogo, non di apparizione
        // Ognuno porta gli id che deve accendere: è il contratto che vipi-aor.js legge da `data-secs`.
        var r = c.Find(".aor-view-2d .cfg-btn");
        Assert.Equal("1113", r.GetAttribute("data-secs"));
    }

    // L'area senza shape non si disegna, ma la sua riga c'è e lo dice.
    [Fact]
    public void Larea_senza_shape_resta_in_elenco_e_lo_dichiara()
    {
        var c = Render();

        var card = c.Find("[data-areacard='777']");
        Assert.Contains("Reg_NoShape", card.InnerHtml);
        Assert.Contains("TSA senza shape", card.TextContent);
    }

    // Le descrizioni nascono CHIUSE: la sezione è un indice, non un muro di testo.
    [Fact]
    public void Le_descrizioni_nascono_chiuse_e_visibili()
    {
        var c = Render();

        foreach (var d in c.FindAll("details[data-areacard]"))
        {
            Assert.False(d.HasAttribute("open"));
            Assert.False(d.HasAttribute("hidden"));   // all'apertura sono tutte accese
        }
    }

    [Fact]
    public void Senza_aree_niente_mappa()
    {
        var c = Render(Array.Empty<AccSpecialAreaView>());

        Assert.Empty(c.FindAll(".aor-leaflet"));
        Assert.Empty(c.FindAll("[data-areacard]"));
        Assert.Contains("Reg_NoneSelected", c.Markup);
    }

    // Due sezioni «aree» nella stessa pagina (due blocchi di una vIPI ACC) non devono pilotarsi a vicenda.
    [Fact]
    public void Blocchi_diversi_hanno_scope_diversi()
    {
        var a = Render(blocco: "blk-1");
        var b = Render(blocco: "blk-2");

        var sa = a.Find("[data-areacards]").GetAttribute("data-areacards");
        var sb = b.Find("[data-areacards]").GetAttribute("data-areacards");
        Assert.NotEqual(sa, sb);
        // …e lo scope delle descrizioni è quello della mappa: è così che il JS le trova.
        Assert.Equal(a.Find(".aor-block").GetAttribute("data-aor"), sa);
    }

    // APP non remotizzata e vLOA hanno UNA sezione aree e non passano la chiave di blocco: lo scope deve
    // esserci lo stesso, o mappa e descrizioni non si trovano più.
    [Fact]
    public void Senza_chiave_di_blocco_lo_scope_ce_lo_stesso()
    {
        var c = Render(blocco: null);

        var scope = c.Find("[data-areacards]").GetAttribute("data-areacards");
        Assert.False(string.IsNullOrWhiteSpace(scope));
        Assert.Equal(c.Find(".aor-block").GetAttribute("data-aor"), scope);
    }

    /// <summary>
    /// Il contratto col JS, scritto in due file diversi. Stessa rete del menu-sezioni: il selettore lo legge
    /// da vipi-aor.js e lo prova contro il markup vero.
    /// </summary>
    [Fact]
    public void Gli_attributi_che_il_JS_cerca_sono_quelli_che_il_componente_scrive()
    {
        var js = File.ReadAllText(FileNellaWwwroot("vipi-aor.js"));

        // setCard e syncCount devono esistere ED essere chiamate: definite e mai invocate non fanno niente.
        Assert.Contains("function setCard(", js, StringComparison.Ordinal);
        Assert.Contains("setCard(sec, on);", js, StringComparison.Ordinal);
        Assert.Contains("syncCount();", js, StringComparison.Ordinal);

        var c = Render();
        var scope = c.Find("[data-areacards]").GetAttribute("data-areacards");

        // I tre attributi con cui il JS raggiunge le descrizioni.
        foreach (var attr in new[] { "data-areacards", "data-areacard", "data-areacount", "data-areaempty" })
            Assert.Contains(attr, js, StringComparison.Ordinal);

        Assert.NotNull(c.Find($"[data-areacards='{scope}'] [data-areacard]"));
        Assert.NotNull(c.Find($"[data-areacount='{scope}']"));
        Assert.NotNull(c.Find($"[data-areaempty='{scope}']"));

        // Il conteggio nasce scritto e porta il FORMATO per le riscritture del JS, con i due segnaposto che
        // il JS sostituisce: senza, la riga diventerebbe muta al primo clic.
        var conta = c.Find("[data-areacount]");
        Assert.False(string.IsNullOrWhiteSpace(conta.TextContent));
        Assert.NotNull(conta.GetAttribute("data-fmt"));
        Assert.Contains("dataset.fmt", js, StringComparison.Ordinal);
        Assert.Contains("replace('{0}'", js, StringComparison.Ordinal);
        Assert.Contains("replace('{1}'", js, StringComparison.Ordinal);

        // Lo scope della vista 3D è lo stesso con «-3d» in coda, e il JS lo toglie per ritrovare le
        // descrizioni: se il suffisso cambiasse da una parte sola, in 3D le chip non le muoverebbero più.
        Assert.Matches(new Regex(@"replace\(/-3d\$/,\s*''\)"), js);
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

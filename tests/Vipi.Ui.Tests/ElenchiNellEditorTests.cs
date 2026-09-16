using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Gli elenchi annidati e il campo che si adatta, dal lato dell'editor (16 settembre 2026, carta
/// <c>2026-09-16-elenchi-annidati-e-campo-che-cresce.md</c>).
///
/// <para>⚠️ La logica dei gesti (rientro, Invio che continua, rinumerazione) vive in <c>vipi-editor.js</c> e qui
/// non gira: non c'è un browser. Quello che si prova è il <b>contratto</b> fra le parti che stanno in file
/// diversi — ed è proprio dove un difetto resterebbe muto: un marcatore che il JS scrive e il renderer non
/// riconosce si vede solo nel documento pubblicato, come testo col trattino davanti.</para>
/// </summary>
public class ElenchiNellEditorTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ElenchiNellEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ---- Il contratto fra le tre copie della regola ----------------------------------------------------

    /// <summary>
    /// 🔴 La sintassi delle voci è scritta DUE volte: in C# (<see cref="VoceDiElenco"/>, che legge il renderer e
    /// il protettore della traduzione) e in JS (il gesto nell'editor). Si confronta il TESTO delle regex: il
    /// giorno che una delle due parti impara un marcatore e l'altra no, il JS scriverebbe voci che il documento
    /// mostra come testo, o il renderer riconoscerebbe voci che il Tab non sa spostare.
    /// </summary>
    [Fact]
    public void Le_regex_del_JS_sono_quelle_del_CSharp()
    {
        var cs = File.ReadAllText(FileDelRepo("src", "Vipi.Application", "Content", "VoceDiElenco.cs"));
        var js = File.ReadAllText(FileDelRepo("src", "Vipi.Ui", "wwwroot", "vipi-editor.js"));

        foreach (var (nomeCs, nomeJs) in new[]
                 {
                     ("Numerata", "RX_VOCE_NUMERATA"),
                     ("Trattini", "RX_VOCE_TRATTINI"),
                     ("Simbolo", "RX_VOCE_SIMBOLO"),
                 })
        {
            var inCs = Regex.Match(cs, $@"Regex {nomeCs} = new\(\s*@""(?<rx>[^""]+)""");
            var inJs = Regex.Match(js, $@"var {nomeJs} = /(?<rx>.+)/;");
            Assert.True(inCs.Success, $"regex {nomeCs} non trovata in VoceDiElenco.cs");
            Assert.True(inJs.Success, $"regex {nomeJs} non trovata in vipi-editor.js");
            Assert.Equal(inCs.Groups["rx"].Value, inJs.Groups["rx"].Value);
        }

        Assert.Contains($"var LIVELLI_ELENCO = {VoceDiElenco.LivelliMassimi};", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// Quel che il JS scrive, il renderer lo legge al livello giusto. Il marcatore canonico del JS è
    /// <c>'-'.repeat(livello)</c> per i puntati e <c>'-'.repeat(livello - 1) + n + ') '</c> per i numerati:
    /// qui si scrivono a mano gli stessi, per ogni livello.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void I_marcatori_che_scrive_il_JS_si_leggono_al_loro_livello(int livello)
    {
        Assert.True(VoceDiElenco.Prova(new string('-', livello) + " voce", out var puntata));
        Assert.Equal((livello, false, "voce"), (puntata.Livello, puntata.Ordinata, puntata.Testo));

        Assert.True(VoceDiElenco.Prova(new string('-', livello - 1) + "1) voce", out var numerata));
        Assert.Equal((livello, true, "voce"), (numerata.Livello, numerata.Ordinata, numerata.Testo));

        var js = File.ReadAllText(FileDelRepo("src", "Vipi.Ui", "wwwroot", "vipi-editor.js"));
        Assert.Contains("'-'.repeat(livello - 1) + numero + ') '", js, StringComparison.Ordinal);
        Assert.Contains("'-'.repeat(livello) + ' '", js, StringComparison.Ordinal);
    }

    // ---- Il componente --------------------------------------------------------------------------------

    [Fact]
    public void La_barra_ha_i_due_tasti_di_rientro_con_la_scorciatoia_nel_titolo()
    {
        var c = RenderComponent<RichTextArea>();

        var titoli = c.FindAll(".rta-btn").Select(b => b.GetAttribute("title")).ToList();
        Assert.Contains("Rta_Indent", titoli);
        Assert.Contains("Rta_Outdent", titoli);
    }

    [Fact]
    public void I_tasti_di_rientro_chiamano_il_JS_col_verso_giusto()
    {
        var c = RenderComponent<RichTextArea>();

        c.Find(".rta-btn[title='Rta_Indent']").Click();
        c.Find(".rta-btn[title='Rta_Outdent']").Click();

        var chiamate = JSInterop.Invocations["vipiMdRientro"];
        Assert.Equal(2, chiamate.Count);
        Assert.Equal(1, chiamate[0].Arguments[1]);
        Assert.Equal(-1, chiamate[1].Arguments[1]);
    }

    /// <summary>
    /// ⚠️ Come gli altri tasti: senza <c>preventDefault</c> sul <c>mousedown</c>, premere il tasto toglie il fuoco
    /// al campo e il browser azzera la selezione — il rientro agirebbe sul nulla.
    /// </summary>
    [Fact]
    public void I_tasti_di_rientro_non_rubano_il_fuoco()
    {
        var c = RenderComponent<RichTextArea>();
        foreach (var t in new[] { "Rta_Indent", "Rta_Outdent" })
            Assert.Contains("blazor:onmousedown:preventdefault", c.Find($".rta-btn[title='{t}']").OuterHtml,
                            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Il campo si adatta al testo: il JS agisce solo sulle textarea marcate.</summary>
    [Fact]
    public void Il_campo_di_prosa_e_marcato_per_adattarsi()
    {
        var c = RenderComponent<RichTextArea>();
        Assert.True(c.Find("textarea.app-ta").HasAttribute("data-adatta"));

        var js = File.ReadAllText(FileDelRepo("src", "Vipi.Ui", "wwwroot", "vipi-editor.js"));
        Assert.Contains("textarea[data-adatta]", js, StringComparison.Ordinal);
        Assert.Contains("hasAttribute('data-adatta')", js, StringComparison.Ordinal);
    }

    private static string FileDelRepo(params string[] parti)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(new[] { dir.FullName }.Concat(parti).ToArray());
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{Path.Combine(parti)} non trovato risalendo da {AppContext.BaseDirectory}");
    }
}

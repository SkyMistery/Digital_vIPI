using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Presìdi sul sorgente della barra in alto (<c>SopLayout.razor</c> e <c>vipi-theme.css</c>): la barra è SSR statico
/// dentro il layout, e la verifica vera è a schermo (fatta il 29 settembre 2026 a 1900, 1000 e 375px). Qui si tiene
/// ferma la forma che quella verifica ha trovato giusta.
/// </summary>
public class BarraInAltoTests
{
    /// <summary>
    /// 🔴 Committente, 29 settembre 2026: «nelle dimensioni della pagina ridotta il tasto cerca non porta alla pagina
    /// search». A barra stretta la ricerca è un collegamento a <c>/services/vsop/search</c>, e il modulo non si rende.
    /// </summary>
    [Fact]
    public void A_barra_stretta_la_lente_e_un_collegamento_alla_pagina_di_ricerca()
    {
        var layout = Leggi("Shared/SopLayout.razor");
        Assert.Matches(new Regex("<a class=\"top-search-go[^\"]*\" href=\"/services/vsop/search\""), layout);

        var tema = Leggi("wwwroot/vipi-theme.css");
        Assert.Contains(":where(.vipi-root) .top-search-go{display:none}", tema);
        Assert.Contains(":where(.vipi-root) .topbar.tb-3 .top-search{display:none}", tema);
        Assert.Contains(":where(.vipi-root) .topbar.tb-3 .top-search-go{display:inline-flex", tema);
        // Il vecchio assetto (modulo stretto che si riapre sotto la barra al fuoco) non deve tornare.
        Assert.DoesNotContain(".top-search:focus-within{position:fixed", tema);
    }

    /// <summary>
    /// 🔴 Committente, 29 settembre 2026: «non tutti i tasti hanno la stessa altezza». Misurati prima: 32, 34, 36 e
    /// 38px. L'altezza la decide la barra, per tutti i comandi insieme.
    /// </summary>
    [Fact]
    public void I_comandi_della_barra_hanno_una_sola_altezza()
    {
        var tema = Leggi("wwwroot/vipi-theme.css");
        var regola = Regex.Match(tema, @"\.topbar :is\(([^)]*)\)\{height:var\(--tb-ctl\)");
        Assert.True(regola.Success, "manca la regola dell'altezza comune dei comandi della barra");
        foreach (var comando in new[] { ".editor-btn", ".zoom-ctrl", ".lang-ctrl", ".live-badge", ".top-search", ".acc-nav a" })
            Assert.Contains(comando, regola.Groups[1].Value);
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}

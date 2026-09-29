namespace Vipi.Ui.Tests;

/// <summary>
/// Il quadro vAWOS (committente, 30 settembre 2026): niente TEST METAR, e l'altezza è quella VISIBILE.
/// ⚠️ Presidi sul sorgente: la pagina è SSR statica con dieci servizi dietro, e l'altezza è una regola del foglio
/// che nessun test di componente misura.
/// </summary>
public class AwosQuadroTests
{
    [Fact]
    public void Il_quadro_non_ha_piu_il_METAR_di_prova()
    {
        var pagina = Leggi("Pages/AwosPage.razor");
        Assert.DoesNotContain(">TEST METAR<", pagina);
        Assert.DoesNotContain("data-awos-mask=\"prova\"", pagina);
        Assert.DoesNotContain("Name = \"test\"", pagina);

        // Il modulo non deve più inoltrare all'API un `?test=` preso dall'indirizzo della pagina.
        Assert.DoesNotContain("get('test')", Leggi("wwwroot/vipi-awos.js"));
    }

    /// <summary>
    /// 🔴 Nell'Edge del committente, a 1920×917 visibili, il quadro misurava ~1003px con una pista come con due:
    /// `100vh` valeva più della finestra. `100dvh` è l'altezza visibile, e dev'essere l'ULTIMA dichiarazione.
    /// </summary>
    [Fact]
    public void Il_quadro_prende_l_altezza_visibile()
    {
        var foglio = Leggi("wwwroot/vipi-awos.css");
        var inizio = foglio.IndexOf(".awos {", StringComparison.Ordinal);
        var regola = foglio[inizio..foglio.IndexOf('}', inizio)];

        Assert.True(regola.IndexOf("height: 100dvh;", StringComparison.Ordinal)
                    > regola.IndexOf("height: 100vh;", StringComparison.Ordinal));
        Assert.Contains("height: 100dvh;", regola);
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

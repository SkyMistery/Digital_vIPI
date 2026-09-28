using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-020 (revisione totale 3): il tooltip 2D dell'AoR non esegue quello che il nome di un volume contiene.
///
/// <para><b>Il difetto.</b> <c>vipi-aor.js</c> passava a <c>bindTooltip</c> il testo così com'era: Leaflet mette
/// una stringa nel tooltip con <c>innerHTML</c>. Il testo è «nome del volume · base → tetto», e il nome arriva
/// dal KMZ importato o dalla tabella degli spazi aerei — quindi un nome come
/// <c>&lt;img src=x onerror=…&gt;</c> girava nel browser di chiunque passasse col mouse sulla mappa di una
/// pagina pubblica. Il viewer 3D lo faceva già giusto (<c>esc</c> in <c>vipi-aor3d.js</c>).</para>
///
/// <para>⚠️ <b>Perché un presidio sul TESTO.</b> La suite non ha un motore JavaScript: il comportamento si prova a
/// schermo (voce S18 di <c>docs/filoni/sito.md</c>), qui si tiene ferma la regola — nessuna chiamata a
/// <c>bindTooltip</c>, <c>bindPopup</c> o <c>setContent</c> riceve un valore che non passi da <c>esc</c>.</para>
/// </summary>
public class TooltipAorSenzaHtmlTests
{
    [Fact]
    public void Ogni_tooltip_della_mappa_AoR_passa_dall_escape()
    {
        var js = File.ReadAllText(FileNellaWwwroot("vipi-aor.js"));

        Assert.Matches(@"function\s+esc\s*\(|var\s+esc\s*=", js);

        var chiamate = Regex.Matches(js, @"\.(bindTooltip|bindPopup|setContent)\(\s*(?<arg>[^,)]*)");
        Assert.NotEmpty(chiamate);
        foreach (Match m in chiamate)
        {
            var arg = m.Groups["arg"].Value.Trim();
            Assert.True(arg.StartsWith("esc(", StringComparison.Ordinal),
                $"{m.Groups[1].Value}({arg}…) in vipi-aor.js: Leaflet scrive la stringa in innerHTML, " +
                "e il testo viene dai nomi dei volumi (KMZ). Va passato da esc().");
        }
    }

    [Fact]
    public void L_escape_copre_i_caratteri_che_aprono_un_tag_o_un_attributo()
    {
        var js = File.ReadAllText(FileNellaWwwroot("vipi-aor.js"));
        var m = Regex.Match(js, @"(?:function\s+esc\s*\([^)]*\)|var\s+esc\s*=\s*function\s*\([^)]*\))\s*\{(?<c>.*?)\}\s*;?\s*\n",
            RegexOptions.Singleline);
        Assert.True(m.Success, "esc non trovata in vipi-aor.js");
        foreach (var entita in new[] { "&amp;", "&lt;", "&gt;", "&quot;" })
            Assert.Contains(entita, m.Groups["c"].Value, StringComparison.Ordinal);
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

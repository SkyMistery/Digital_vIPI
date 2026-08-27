using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il corpo delle sezioni della Guida è reso con <c>@((MarkupString)…)</c>, cioè **come HTML**.
///
/// <para><b>Perché serve.</b> Il 27 agosto 2026 novantasei tag erano scritti **escapati** (<c>&amp;lt;b&amp;gt;</c>)
/// e trentasette apostrofi **raddoppiati** (<c>l''editor</c>), abitudini prese da altri contesti di quoting che
/// qui non valgono: in una <c>MarkupString</c> dentro una stringa verbatim C# escono **letterali**. A schermo si
/// leggeva «Le sezioni &lt;b&gt;derivate&lt;/b&gt;» e «L''editor». Cinque sezioni della guida utente, per mesi.</para>
///
/// <para>Non lo vedeva nessuno perché la Guida è **testo**: nessun test la esercitava, nessuna asserzione poteva
/// accorgersene, e chi la scrive guarda il sorgente e non la pagina. Questo test guarda il sorgente al posto suo.</para>
/// </summary>
public class GuidaMarkupTests
{
    [Fact]
    public void I_tag_della_Guida_non_sono_escapati()
    {
        var testo = File.ReadAllText(GuidaPage());

        // ⚠️ `&amp;` e `&nbsp;` restano leciti: sono entità che si VOGLIONO vedere. Qui si cercano solo i tag.
        var escapati = Regex.Matches(testo, @"&lt;/?[a-zA-Z]").Count;
        Assert.True(escapati == 0,
            $"{escapati} tag scritti escapati in GuidaPage.razor: il corpo è una MarkupString, li mostrerebbe letterali.");
    }

    [Fact]
    public void Gli_apostrofi_della_Guida_non_sono_raddoppiati()
    {
        var testo = File.ReadAllText(GuidaPage());

        // In una stringa verbatim C# l'escape è `""` per le virgolette; `''` non è un escape di niente e si
        // vede raddoppiato a schermo.
        var doppi = Regex.Matches(testo, @"''").Count;
        Assert.True(doppi == 0,
            $"{doppi} apostrofi raddoppiati in GuidaPage.razor: in una stringa verbatim non è un escape, si vedono.");
    }

    private static string GuidaPage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "Pages", "GuidaPage.razor");
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("GuidaPage.razor non trovato risalendo da " + AppContext.BaseDirectory);
    }
}

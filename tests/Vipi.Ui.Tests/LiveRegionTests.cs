using System.Text.RegularExpressions;
using Bunit;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La regione degli esiti c'è anche quando non c'è niente da dire.
///
/// <para><b>Il difetto che presidia.</b> Uno screen reader annuncia i cambiamenti che avvengono DENTRO una
/// live region che stava già lì. Il modo naturale di scrivere un messaggio d'esito in Blazor —
/// <c>@@if (_msg is not null) { &lt;span role="status"&gt;… }</c> — crea la regione nello stesso istante in
/// cui compare il testo, e quel testo in genere non viene letto affatto. Su una decina di pagine admin
/// l'esito di un salvataggio si vedeva e non si sentiva, cioè mancava esattamente a chi non ha altro modo
/// di sapere se il salvataggio è andato.</para>
///
/// <para>Il test guarda la cosa che rende il rimedio efficace: che il nodo con <c>role="status"</c> esista
/// <b>a contenuto vuoto</b>. Se qualcuno rimettesse la condizione attorno al componente invece che dentro,
/// tornerebbe rosso.</para>
/// </summary>
public class LiveRegionTests : TestContext
{
    [Fact]
    public void La_regione_esiste_anche_vuota()
    {
        var cut = RenderComponent<LiveRegion>();

        var regione = cut.Find("[role=status]");
        Assert.Equal("polite", regione.GetAttribute("aria-live"));
        Assert.Equal("", regione.TextContent.Trim());
    }

    [Fact]
    public void Il_contenuto_sta_DENTRO_la_regione()
    {
        var cut = RenderComponent<LiveRegion>(p =>
            p.AddChildContent("<span class=\"st-msg ok\">Salvato</span>"));

        Assert.Contains("Salvato", cut.Find("[role=status]").TextContent);
    }

    /// <summary>
    /// ⚠️ La regione non deve avere un riquadro: le testate sono `flex` con `gap`, e un elemento in più —
    /// anche largo zero — vi lascerebbe un vuoto permanente. Il foglio le dà `display:contents`; qui si
    /// presidia la classe che ci si aggancia, perché è l'unico legame fra le due metà.
    /// </summary>
    [Fact]
    public void Porta_la_classe_che_la_toglie_dal_layout()
    {
        var cut = RenderComponent<LiveRegion>();
        Assert.Contains("live-region", cut.Find("[role=status]").GetAttribute("class"));
    }

    /// <summary>
    /// L'altra metà: nel foglio, `.live-region` è `display:contents`. Se qualcuno togliesse quella regola le
    /// testate guadagnerebbero un vuoto di 10px che nessuno saprebbe spiegare.
    /// </summary>
    [Fact]
    public void Il_foglio_la_tiene_fuori_dal_layout()
    {
        var css = File.ReadAllText(FoglioTema());
        Assert.Matches(new Regex(@"\.live-region\s*\{[^}]*display:\s*contents"), css);
    }

    private static string FoglioTema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot", "vipi-theme.css");
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("vipi-theme.css non trovato risalendo da " + AppContext.BaseDirectory);
    }
}

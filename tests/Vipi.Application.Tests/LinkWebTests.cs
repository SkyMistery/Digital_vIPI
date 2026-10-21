using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il link a un indirizzo web nel testo (S122, 21 ottobre 2026, committente): solo http e https, assoluti, con un host.
/// </summary>
public class LinkWebTests
{
    [Theory]
    [InlineData("https://esempio.it", "https://esempio.it/")]
    [InlineData("  http://esempio.it/a?b=1  ", "http://esempio.it/a?b=1")]
    [InlineData("HTTPS://ESEMPIO.IT/X", "https://esempio.it/X")]
    public void Un_indirizzo_web_assoluto_passa(string indirizzo, string atteso)
    {
        Assert.Equal(atteso, LinkWeb.Sicuro(indirizzo));
    }

    /// <summary>Tutto il resto no: altri schemi, indirizzi relativi o senza schema, credenziali nell'indirizzo.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("ftp://esempio.it")]
    [InlineData("mailto:a@esempio.it")]
    [InlineData("/services/vsop")]
    [InlineData("//esempio.it")]
    [InlineData("esempio.it")]
    [InlineData("https://utente:segreto@esempio.it")]
    public void Il_resto_no(string? indirizzo)
    {
        Assert.Null(LinkWeb.Sicuro(indirizzo));
        Assert.Null(LinkWeb.Scrivi("x", indirizzo));
    }

    /// <summary>
    /// Come si scrive nel testo: le parole senza quadre né a capo, vuote = l'indirizzo; e le parentesi tonde
    /// dell'indirizzo codificate, o chiuderebbero il link a metà.
    /// </summary>
    [Fact]
    public void Il_link_si_scrive_in_modo_che_si_rilegga()
    {
        Assert.Equal("[Eurocontrol](https://www.eurocontrol.int/)", LinkWeb.Scrivi(" Eurocontrol ", "https://www.eurocontrol.int"));
        Assert.Equal("[https://esempio.it/](https://esempio.it/)", LinkWeb.Scrivi("", "https://esempio.it"));
        Assert.Equal("[a (b) c](https://esempio.it/)", LinkWeb.Scrivi("a [b]\nc", "https://esempio.it"));

        var wiki = LinkWeb.Scrivi("Fiumicino", "https://it.wikipedia.org/wiki/Aeroporto_(Roma)");
        Assert.Equal("[Fiumicino](https://it.wikipedia.org/wiki/Aeroporto_%28Roma%29)", wiki);
        var m = LinkWeb.Link.Match(wiki!);
        Assert.True(m.Success);
        Assert.Equal("https://it.wikipedia.org/wiki/Aeroporto_%28Roma%29", m.Groups[2].Value);
    }
}

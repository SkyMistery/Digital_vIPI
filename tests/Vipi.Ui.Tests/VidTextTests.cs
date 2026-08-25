using System.Xml.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete su <c>VidText</c>, che rende premibili i VID scritti <b>dentro una frase</b>.
///
/// <para><b>Perché serve.</b> Le frasi del narratore del Registro, il «Deciso da …» di Sorgenti e
/// l'«Assegnato da …» di Incarichi nascono da template tradotti: il VID è una parola in mezzo, non un campo,
/// e nessun markup lo può avvolgere senza spezzare la traduzione. La verifica live del 25 agosto 2026 ne ha
/// contati <b>nove</b> muti sul solo Registro — l'unico buco vero trovato guidando le pagine.</para>
/// </summary>
public class VidTextTests : TestContext
{
    private sealed class FormatLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] => new(
            name, name == "Audit_VidN" ? $"VID {arguments[0]}" : name, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public VidTextTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new FormatLocalizer());

    // ---- il taglio, provato da solo -------------------------------------------------------------------

    [Fact]
    public void La_frase_si_taglia_in_testo_vid_testo()
    {
        var pezzi = VidText.Spezza("Concesso il permesso a VID 704798 su LIRR");

        Assert.Equal(3, pezzi.Count);
        Assert.Equal("Concesso il permesso a ", pezzi[0].Testo);
        Assert.Equal(704798, pezzi[1].Vid);
        Assert.Equal(" su LIRR", pezzi[2].Testo);
        Assert.Null(pezzi[0].Vid);
        Assert.Null(pezzi[2].Vid);
    }

    [Fact]
    public void Una_frase_senza_vid_resta_un_pezzo_solo()
    {
        var pezzi = VidText.Spezza("Pubblicata la versione 12 di LIBB");

        Assert.Single(pezzi);
        Assert.Null(pezzi[0].Vid);
    }

    /// <summary>Un numero qualunque non è un VID: si taglia sulla forma che scriviamo noi, «VID 1234567».</summary>
    [Theory]
    [InlineData("AIRAC 2609 pubblicato")]
    [InlineData("704798 senza etichetta")]
    [InlineData("VID 12")]              // sotto il pavimento delle tre cifre
    public void Non_si_taglia_su_un_numero_qualunque(string frase)
    {
        var pezzi = VidText.Spezza(frase);

        Assert.Single(pezzi);
        Assert.Null(pezzi[0].Vid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Niente_frase_niente_pezzi(string? frase) => Assert.Empty(VidText.Spezza(frase));

    // ---- il componente ---------------------------------------------------------------------------------

    [Fact]
    public void Il_vid_della_frase_diventa_un_link_e_il_resto_resta_testo()
    {
        var cut = RenderComponent<VidText>(p => p.Add(x => x.Testo, "Concesso il permesso a VID 704798 su LIRR"));

        var a = cut.Find("a.vid-link");
        Assert.Equal("https://ivao.aero/Member.aspx?Id=704798", a.GetAttribute("href"));
        Assert.Equal("VID 704798", a.TextContent);
        Assert.Contains("Concesso il permesso a", cut.Markup);
        Assert.Contains("su LIRR", cut.Markup);
    }

    /// <summary>
    /// ⚠️ La frase porta dentro titoli di documento e note scritte da persone: esce come <b>testo</b>, non
    /// come markup. Senza questa prova, il giorno che qualcuno passasse a <c>MarkupString</c> per comodità
    /// una nota diventerebbe HTML eseguibile e nessun test se ne accorgerebbe.
    /// </summary>
    [Fact]
    public void Il_testo_intorno_non_diventa_markup()
    {
        var cut = RenderComponent<VidText>(p => p.Add(x => x.Testo, "Nota <b>grassa</b> di VID 704798"));

        Assert.Empty(cut.FindAll("b"));
        Assert.Contains("&lt;b&gt;", cut.Markup);
        Assert.Single(cut.FindAll("a.vid-link"));
    }

    // ---- la guardia sulla risorsa ----------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>La forma riconosciuta viene da una risorsa tradotta.</b> `VidText` taglia su «VID 1234567»
    /// perché è così che <c>Audit_VidN</c> scrive un VID, in italiano e in inglese. Se qualcuno la
    /// ritraduce — «ID IVAO 1234567», «n. 1234567» — il componente smette di trovare qualunque cosa
    /// <b>in silenzio</b>: le frasi resterebbero mute e nessuno se ne accorgerebbe fino alla prossima
    /// verifica live. Questa prova legge i <c>.resx</c> dal disco e fa fallire la suite invece.
    /// </summary>
    [Theory]
    [InlineData("src/Vipi.Ui/Resources/SharedResource.resx")]
    [InlineData("src/Vipi.Ui/Resources/SharedResource.en.resx")]
    public void La_forma_tagliata_e_ancora_quella_che_la_risorsa_scrive(string percorsoRelativo)
    {
        var formato = ValoreDi(percorsoRelativo, "Audit_VidN");
        var scritto = string.Format(formato, 704798);

        var pezzi = VidText.Spezza($"Prima {scritto} dopo");

        Assert.True(pezzi.Any(p => p.Vid == 704798),
            $"`Audit_VidN` in {percorsoRelativo} vale «{formato}», e VidText non ci riconosce più un VID: " +
            "le frasi del Registro, di Sorgenti e di Incarichi resterebbero senza link, in silenzio. " +
            "Se il testo va cambiato davvero, va cambiata anche la forma tagliata in VidText.");
    }

    private static string ValoreDi(string percorsoRelativo, string chiave)
    {
        var percorso = Path.Combine(RadiceDelRepo(), percorsoRelativo.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(percorso), "File di risorse non trovato: " + percorso);

        var valore = XDocument.Load(percorso).Root!.Elements("data")
            .FirstOrDefault(e => e.Attribute("name")?.Value == chiave)?.Element("value")?.Value;

        Assert.False(string.IsNullOrEmpty(valore), $"Chiave {chiave} assente in {percorsoRelativo}");
        return valore!;
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione (come SharedResourceIntegrityTests).</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La chip <i>Tutto · Pilota · ATC</i> e il badge di sezione (carta <c>2026-08-27-vsop-militari.md</c> §3).
///
/// <para>
/// ⚠️ <b>Sono tre COLLEGAMENTI, non tre bottoni</b>, e questi test lo pretendono. La pagina pubblica è SSR
/// statica: un bottone che cambia stato lì dentro non parte, perché lo stato che cambia deve vivere dentro
/// l'isola che lo cambia — trappola già pagata con le chip METAR/TAF. Un link naviga, e la pagina si
/// ridisegna col filtro applicato. In più è <b>condivisibile</b>: la divisione manda ai piloti l'indirizzo
/// della vista pilota, ed è probabilmente il valore vero della funzione.
/// </para>
/// </summary>
public class AudienceChipTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AudienceChipTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ⚠️ Sempre .ToList() dopo FindAll: indicizzare direttamente il risultato di bUnit esplode con
    // «Method not found: IHtmlCollection.get_Item» sulla coppia bUnit/AngleSharp di questo repository.
    private IRenderedComponent<AudienceChip> Chip(bool visibile, SectionAudience? vista = null,
                                                  string href = "/services/vsop/libb/vloa?acc=LGGG") =>
        RenderComponent<AudienceChip>(p => p
            .Add(x => x.Visibile, visibile)
            .Add(x => x.Vista, vista)
            .Add(x => x.BaseHref, href));

    // ---- Quando compare ------------------------------------------------------------------------------

    [Fact]
    public void Senza_sezioni_marcate_la_chip_non_si_disegna()
    {
        // Sarebbe un selettore che non filtra niente: rumore su ogni pagina italiana per una funzione che
        // ne riguarda poche.
        Assert.DoesNotContain("aud-chip", Chip(visibile: false).Markup);
    }

    [Fact]
    public void Con_almeno_una_sezione_marcata_compaiono_le_tre_scelte()
    {
        var cut = Chip(visibile: true);
        Assert.Contains("aud-chip", cut.Markup);
        Assert.Equal(3, cut.FindAll("a").Count);
    }

    // ---- Sono link, e portano dove devono ------------------------------------------------------------

    [Fact]
    public void Sono_COLLEGAMENTI_e_non_bottoni()
    {
        // ⚠️ Su pagina SSR statica un bottone non farebbe niente. Se qualcuno un giorno li convertisse in
        // <button> «per uniformità», la chip smetterebbe di funzionare in silenzio.
        var cut = Chip(visibile: true);
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal(3, cut.FindAll("a[href]").Count);
    }

    [Fact]
    public void Il_link_TUTTO_non_porta_nessun_parametro()
    {
        // «Tutto» è l'assenza del filtro, non un valore: mettercelo lascerebbe indirizzi con dentro una
        // parola che non serve a niente.
        var href = Chip(visibile: true).FindAll("a").ToList()[0].GetAttribute("href");
        Assert.Equal("/services/vsop/libb/vloa?acc=LGGG", href);
    }

    [Fact]
    public void I_link_filtrati_APPENDONO_il_parametro_agli_altri()
    {
        // ⚠️ La vLOA porta gia' «?acc=», e perderlo vorrebbe dire mandare il lettore su un altro documento.
        var link = Chip(visibile: true).FindAll("a").ToList();
        Assert.Equal("/services/vsop/libb/vloa?acc=LGGG&vista=pilota", link[1].GetAttribute("href"));
        Assert.Equal("/services/vsop/libb/vloa?acc=LGGG&vista=atc", link[2].GetAttribute("href"));
    }

    [Fact]
    public void Su_un_indirizzo_senza_parametri_il_primo_e_un_punto_interrogativo()
    {
        var link = Chip(visibile: true, href: "/services/vsop/lirf/airports").FindAll("a").ToList();
        Assert.Equal("/services/vsop/lirf/airports?vista=pilota", link[1].GetAttribute("href"));
    }

    // ---- Dove si è ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, 0)]
    [InlineData(SectionAudience.Pilots, 1)]
    [InlineData(SectionAudience.Controllers, 2)]
    public void La_vista_corrente_si_vede_ed_e_dichiarata_a_chi_non_vede(SectionAudience? vista, int indice)
    {
        var link = Chip(visibile: true, vista: vista).FindAll("a").ToList();
        Assert.Contains("on", link[indice].GetAttribute("class"));
        Assert.Equal("true", link[indice].GetAttribute("aria-current"));

        // Gli altri due non devono dichiararsi correnti: un lettore di schermo sentirebbe tre «corrente».
        foreach (var altro in Enumerable.Range(0, 3).Where(i => i != indice))
            Assert.Null(link[altro].GetAttribute("aria-current"));
    }

    // ---- Il badge -------------------------------------------------------------------------------------

    [Fact]
    public void Una_sezione_PER_TUTTI_non_porta_badge()
    {
        // ⚠️ Etichetta prima, filtro dopo -- ma se il badge lo portassero TUTTE smetterebbe di dire
        // qualcosa. Marcare e' l'eccezione, e si vede perche' e' l'eccezione.
        var cut = RenderComponent<AudienceBadge>(p => p.Add(x => x.Audience, SectionAudience.Both));
        Assert.DoesNotContain("aud-badge", cut.Markup);
    }

    [Theory]
    [InlineData(SectionAudience.Pilots, "Aud_BadgePilots")]
    [InlineData(SectionAudience.Controllers, "Aud_BadgeControllers")]
    public void Una_sezione_marcata_lo_dice(SectionAudience a, string chiave)
    {
        var cut = RenderComponent<AudienceBadge>(p => p.Add(x => x.Audience, a));
        Assert.Contains("aud-badge", cut.Markup);
        Assert.Contains(chiave, cut.Markup);
    }
}

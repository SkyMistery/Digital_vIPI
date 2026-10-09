using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il piè di pagina del sito (<see cref="SitoFooter"/>).
///
/// <para>🔴 <b>Perché (29 settembre 2026).</b> Il committente, con davanti quello dell'hub IVAO Italy: «in fondo al sito
/// dobbiamo mettere una cosa così, con le descrizioni appropriate e versione e commit spostati dalla barra in alto a in
/// basso, visibili solo allo staff. Deve essere presente ovunque tranne che nel vAWOS».</para>
/// </summary>
public class PieDiPaginaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private IRenderedComponent<SitoFooter> Rendi(bool staff, string? motto = null)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        var divisione = new DivisionOptions { Name = "Italy" };
        if (motto is not null) divisione.Motto = motto;
        Services.AddSingleton<IOptions<DivisionOptions>>(Options.Create(divisione));
        return RenderComponent<SitoFooter>(p => p
            .Add(x => x.MostraVersione, staff)
            .Add(x => x.Versione, "1.48.0 · abc1234")
            .Add(x => x.VersioneDettaglio, "pacchetto 1.48.0, commit abc1234"));
    }

    [Fact]
    public void Allo_staff_la_versione_e_il_commit()
    {
        var cut = Rendi(staff: true);

        var ver = cut.Find(".ver-chip");
        Assert.Equal("1.48.0 · abc1234", ver.TextContent);
        Assert.Equal("pacchetto 1.48.0, commit abc1234", ver.GetAttribute("title"));
    }

    [Fact]
    public void Al_lettore_niente_versione()
    {
        var cut = Rendi(staff: false);

        Assert.Empty(cut.FindAll(".ver-chip"));
        Assert.DoesNotContain("abc1234", cut.Markup);
    }

    [Fact]
    public void Porta_i_collegamenti_di_IVAO_in_una_scheda_nuova()
    {
        var cut = Rendi(staff: false);

        var link = cut.FindAll(".sf-links a").ToList();
        // ⚠️ Le tre pagine di ivao.aero (termini, privacy, regole) rispondono 404 dal 29 settembre 2026: tutto sta
        // sulla wiki di IVAO, sotto una pagina sola.
        // Il sito della divisione italiana subito dopo quello di IVAO (committente, 30 settembre 2026).
        Assert.Equal(new[] { "https://www.ivao.aero", "https://it.ivao.aero/", "https://wiki.ivao.aero/en/home/ivao/information" },
            link.Select(a => a.GetAttribute("href")));
        Assert.All(link, a =>
        {
            Assert.Equal("_blank", a.GetAttribute("target"));
            Assert.Equal("noopener", a.GetAttribute("rel"));
        });
    }

    /// <summary>Committente, 29 settembre 2026: «built by Carmine (704798)», con nome e VID che portano al profilo IVAO.</summary>
    [Fact]
    public void Dice_chi_l_ha_realizzato_col_link_al_profilo_IVAO()
    {
        var cut = Rendi(staff: false);

        // Carmine due volte (realizzato e testato), Nicola una: ognuno al suo profilo IVAO (1 ottobre 2026).
        var carmine = cut.FindAll(".sf-bottom a").Where(a => a.TextContent == "Carmine (704798)").ToList();
        Assert.Equal(2, carmine.Count);
        Assert.All(carmine, a =>
        {
            Assert.Equal("https://ivao.aero/Login.aspx?r=Member.aspx?Id=704798", a.GetAttribute("href"));
            Assert.Equal("_blank", a.GetAttribute("target"));
        });
        var nicola = cut.FindAll(".sf-bottom a").Single(a => a.TextContent == "Nicola (201143)");
        Assert.Equal("https://ivao.aero/Login.aspx?r=Member.aspx?Id=201143", nicola.GetAttribute("href"));
        var testo = cut.Find(".sf-cred").TextContent;
        Assert.Contains("Foot_BuiltBy", testo);
        Assert.Contains("Foot_TestedBy Nicola (201143) Foot_And Carmine (704798)", System.Text.RegularExpressions.Regex.Replace(testo, @"\s+", " "));
        Assert.True(testo.IndexOf("Foot_PartOf", StringComparison.Ordinal) < testo.IndexOf("Foot_BuiltBy", StringComparison.Ordinal));
    }

    [Fact]
    public void Dice_chi_e_la_divisione_e_che_e_simulazione()
    {
        var cut = Rendi(staff: false);

        Assert.Contains("Foot_About IVAO Italy", cut.Markup);
        Assert.Contains("Foot_Disclaimer IVAO Italy", cut.Markup);
        Assert.Contains($"© {DateTime.UtcNow.Year} IVAO Italy.", cut.Markup);
        Assert.Contains("Foot_PartOf", cut.Markup);
    }

    /// <summary>Committente, 9 ottobre 2026: il motto della divisione, «it takes time», sotto il marchio.</summary>
    [Fact]
    public void Il_motto_della_divisione_sta_sotto_il_marchio_e_non_si_traduce()
    {
        var cut = Rendi(staff: false);

        var motto = cut.Find(".sf-about .sf-motto");
        Assert.Equal("it takes time", motto.TextContent);
        // In inglese anche nella pagina italiana, e lo dice: un lettore di schermo cambia pronuncia su `lang`.
        Assert.Equal("en", motto.GetAttribute("lang"));
        // Subito dopo il marchio, prima delle frasi che descrivono il sito: è la firma del nome.
        var figli = cut.Find(".sf-about").Children.Select(e => e.ClassName ?? e.TagName.ToLowerInvariant()).ToList();
        Assert.Equal(new[] { "sf-brand", "sf-motto", "p", "p" }, figli);
        // Una volta sola, e non nella striscia dei diritti e dei crediti.
        Assert.Single(cut.FindAll(".sf-motto"));
        Assert.DoesNotContain("it takes time", cut.Find(".sf-bottom").TextContent);
    }

    [Fact]
    public void Una_divisione_senza_motto_non_ha_la_riga()
    {
        var cut = Rendi(staff: false, motto: "  ");

        Assert.Empty(cut.FindAll(".sf-motto"));
    }

    /// <summary>
    /// Ovunque tranne il vAWOS: sta nel layout comune, non in quello del vAWOS; e la versione non sta più in barra.
    /// ⚠️ Presidio sul sorgente: il layout comune ha servizi e sedi che un test di componente non monta.
    /// </summary>
    [Fact]
    public void Sta_in_ogni_pagina_tranne_il_vAWOS_e_la_versione_non_sta_in_barra()
    {
        var layout = Leggi("Shared/SopLayout.razor");
        Assert.Contains("<SitoFooter ", layout);
        Assert.DoesNotContain("class=\"ver-chip\"", layout);

        Assert.DoesNotContain("SitoFooter", Leggi("Shared/AwosLayout.razor"));

        // Le pagine con un layout diverso dal comune: oggi solo il vAWOS. Una nuova resterebbe senza piè di pagina.
        var altri = Directory.GetFiles(Path.Combine(Radice(), "Pages"), "*.razor")
            .Where(f => File.ReadAllText(f).Contains("@layout ") && !File.ReadAllText(f).Contains("@layout SopLayout"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Equal(new[] { "AwosPage.razor" }, altri);
    }

    /// <summary>
    /// I cookie (committente, 30 settembre 2026): solo tecnici, quindi niente banner ma una pagina che li elenca, col
    /// suo link in fondo al piè di pagina. ⚠️ L'elenco è scritto a mano: il presidio lega almeno il cookie del login al
    /// codice che lo scrive, perché un nome cambiato lì e non qui farebbe dire alla pagina una cosa falsa.
    /// </summary>
    [Fact]
    public void Il_piede_porta_alla_pagina_dei_cookie_che_elenca_quelli_veri()
    {
        var cut = Rendi(staff: false);
        var link = cut.FindAll(".sf-bottom a").Single(a => a.GetAttribute("href") == "/services/cookies");
        Assert.Equal("Ck_Title", link.TextContent.Trim());
        Assert.Null(link.GetAttribute("target"));                    // è una pagina nostra: stessa scheda

        var nomi = Vipi.Ui.Pages.CookiePage.Cookie.Select(c => c.Nome).ToList();
        Assert.Contains("vipi.auth", nomi);
        Assert.Contains(".AspNetCore.Culture", nomi);
        var auth = File.ReadAllText(Path.Combine(Radice(), "..", "Vipi.Host", "Auth", "VipiStandaloneAuthExtensions.cs"));
        Assert.Contains("o.Cookie.Name = \"vipi.auth\";", auth);
        Assert.Contains("o.ExpireTimeSpan = TimeSpan.FromDays(7);", auth);   // la durata scritta nella pagina
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

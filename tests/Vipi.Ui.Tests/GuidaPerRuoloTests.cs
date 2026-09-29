using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La Guida cambia con chi legge.
///
/// <para>🔴 <b>Perché (29 settembre 2026).</b> Il committente: «la guida deve essere diversa a seconda di cosa l'utente
/// può vedere, un utente normale non può vedere la parte di guida sull'editor». Fino ad allora la pagina era la stessa
/// per tutti: un lettore anonimo trovava le istruzioni dell'editor, delle release e delle pagine d'amministrazione.</para>
///
/// <para>Il confine è uno solo, <see cref="GuideSearchCatalog.AncorePubbliche"/>, e vale per la pagina e per la ricerca.
/// Questi test lo tengono uguale alla parte «Consultare» che la pagina rende.</para>
/// </summary>
public class GuidaPerRuoloTests : TestContext
{
    internal sealed class AuthzFinto(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() { }
    }

    private List<string> Capitoli(VipiRole ruolo)
    {
        Services.AddSingleton<IEditAuthorizationService>(new AuthzFinto(ruolo));
        var cut = RenderComponent<GuidaPage>();
        return cut.FindAll(".guida-sec").Select(e => e.Id!).ToList();
    }

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    [InlineData(VipiRole.DivisionStaff)]
    public void Chi_non_modifica_legge_solo_i_capitoli_pubblici(VipiRole ruolo)
    {
        var capitoli = Capitoli(ruolo);

        Assert.Equal(GuideSearchCatalog.AncorePubbliche.OrderBy(a => a), capitoli.OrderBy(a => a));
        Assert.DoesNotContain(capitoli, a => a.StartsWith("editor-") || a.StartsWith("admin-"));
    }

    [Fact]
    public void All_Editor_la_guida_intera()
    {
        var capitoli = Capitoli(VipiRole.Editor);

        Assert.Superset(GuideSearchCatalog.AncorePubbliche.ToHashSet(), capitoli.ToHashSet());
        Assert.Contains("editor-release", capitoli);
        Assert.Contains("anteprime", capitoli);
        Assert.True(capitoli.Count > GuideSearchCatalog.AncorePubbliche.Count);
    }

    [Fact]
    public void Al_lettore_nemmeno_l_indice_nomina_la_parte_sull_editor()
    {
        Services.AddSingleton<IEditAuthorizationService>(new AuthzFinto(VipiRole.User));
        var cut = RenderComponent<GuidaPage>();

        var indice = cut.Find(".guida-toc");
        Assert.DoesNotContain(indice.QuerySelectorAll("a"), a => a.GetAttribute("href") == "#editor-release");
        Assert.DoesNotContain(cut.FindAll(".guida-part"), p => p.TextContent.Contains("Modificare") || p.TextContent.Contains("Editing"));
    }
}

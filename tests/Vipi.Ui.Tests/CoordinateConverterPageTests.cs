using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il convertitore di coordinate. Il motore ha i suoi test in <c>Vipi.Application.Tests</c> — lì c'è la prova
/// vera, quella coi dati del committente — quindi qui si guarda solo ciò che il motore non può sapere: che il
/// <b>cancello</b> ci sia davvero, e che i campi che non hanno effetto non si mostrino.
///
/// <para>⚠️ Un <c>TestContext</c> per livello, e non è pignoleria: bUnit congela il contenitore al primo
/// render, e due livelli chiesti allo stesso contesto darebbero due volte la stessa risposta — cioè un test
/// che passa sempre.</para>
/// </summary>
public class CoordinateConverterPageTests
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeAuthz(VipiRole livello) : IEditAuthorizationService
    {
        public VipiRole Role { get; } = livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    private sealed class Contesto : TestContext
    {
        public IRenderedComponent<CoordinateConverterPage> Apri(VipiRole livello)
        {
            Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
            Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
            Services.AddSingleton(new EnglishStrings());
            return RenderComponent<CoordinateConverterPage>();
        }
    }

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    public void Sotto_Lo_Staff_Di_Divisione_La_Pagina_Rifiuta(VipiRole livello)
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(livello);

        // ⚠️ Nascondere la scheda nell'hub non basta: un indirizzo si scrive anche a mano.
        Assert.Contains("Common_AccessReserved", cut.Markup);
        Assert.Empty(cut.FindAll("textarea"));
    }

    [Theory]
    [InlineData(VipiRole.DivisionStaff)]
    [InlineData(VipiRole.Editor)]
    [InlineData(VipiRole.Admin)]
    public void Dallo_Staff_Di_Divisione_In_Su_Si_Entra(VipiRole livello)
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(livello);

        // I livelli sono CUMULATIVI: Editor e Admin entrano senza una regola in più.
        Assert.DoesNotContain("Common_AccessReserved", cut.Markup);
        Assert.NotEmpty(cut.FindAll("textarea"));
    }

    [Fact]
    public void Si_Apre_Sull_Elenco_Punti_Del_Sectorfile()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        // È il formato che il committente ha chiesto come default.
        var acceso = cut.Find("button.aor-chip.on");
        Assert.Contains("Conv_SfPunti", acceso.TextContent);
    }

    [Fact]
    public void Tipo_E_Nome_Esistono_Solo_Per_I_Segmenti()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        // ⚠️ Un campo che non ha effetto su ciò che si vede è peggio di un campo assente.
        Assert.DoesNotContain("Conv_Type", cut.Markup);

        // ⚠️ `.ToArray()` e non l'indicizzatore: in questa versione di bUnit/AngleSharp
        // `RefreshableElementCollection[i]` cerca un metodo che non c'è più (MissingMethodException).
        cut.FindAll("button.aor-chip").ToArray()[2].Click();   // Sectorfile · segmenti

        Assert.Contains("Conv_Type", cut.Markup);
        Assert.Contains("Conv_CloseRing", cut.Markup);
    }

    [Fact]
    public void Il_Db_Non_Ha_La_Forma_Dms_Ma_I_Decimali()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        cut.FindAll("button.aor-chip").ToArray()[0].Click();   // DB IVAO

        Assert.Contains("Conv_Decimals", cut.Markup);
        // ⚠️ Si guarda «Conv_Dotted», non «Conv_Form»: quest'ultimo è un prefisso di «Conv_Formats», la riga
        // che elenca i formati riconosciuti, e l'asserzione passerebbe per il motivo sbagliato.
        Assert.DoesNotContain("Conv_Dotted", cut.Markup);
    }

    [Fact]
    public void Le_Coordinate_Incollate_Escono_Convertite()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        cut.Find("textarea.conv-ta").Input("42.00777778:11.96833333\n41.975:11.92");

        var uscita = cut.Find("textarea.conv-out");
        Assert.Contains("N042.00.28.000;E011.58.06.000;", uscita.TextContent);
        Assert.Contains("N041.58.30.000;E011.55.12.000;", uscita.TextContent);
        Assert.Contains("Conv_Read", cut.Markup);   // il conto di ciò che è stato letto
    }

    [Fact]
    public void La_Riga_Non_Letta_Compare_Nella_Diagnostica()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        cut.Find("textarea.conv-ta").Input("42.00777778:11.96833333\nquesta riga non e' niente");

        Assert.Contains("Conv_IssueUnread", cut.Markup);
        Assert.Contains("questa riga non e' niente", cut.Markup);
    }

    [Fact]
    public void Il_Filo_Di_Arianna_Porta_All_Hub_Dei_Servizi()
    {
        using var ctx = new Contesto();
        var cut = ctx.Apri(VipiRole.DivisionStaff);

        Assert.Equal("/services", cut.Find(".breadcrumb a").GetAttribute("href"));
    }
}

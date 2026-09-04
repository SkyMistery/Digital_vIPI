using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Import;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// «Da un altro documento…»: la tendina che prende la stessa tabella da un altro vSOP.
///
/// <para>⚠️ Segnalazione del committente (4 settembre 2026): «il tasto non funziona». Guidando l'app si è
/// visto che cosa succede davvero: il documento si legge benissimo, ma <b>in quella tabella non ha righe</b>
/// — ed è il caso normale finché i vSOP sono da riempire. Il pannello non cambiava di una virgola e non
/// diceva niente: un comando che non fa niente e non lo dice si legge come rotto.</para>
/// </summary>
public class ImportDaAltroDocumentoTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ImportDaAltroDocumentoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static readonly SpecImport Spec =
        SpecImport.ColonneFisse("milcallsigns", new[] { "Squadriglia", "Nominativo OAT", "Nominativo GAT" });

    private static readonly IReadOnlyList<SorgenteTabella> Sorgenti = new[]
    {
        new SorgenteTabella("LIML", "LIML Milano Linate"),
        new SorgenteTabella("LIBN", "LIBN Lecce Galatina"),
    };

    /// <summary>LIML ha righe, LIBN no: è la situazione vera del corpus.</summary>
    private static Task<Griglia> Sorgente(string chiave) => Task.FromResult(chiave == "LIML"
        ? new Griglia(new[] { new[] { "SEME-UNO", "118.500", "Torre" } }, FormaGriglia.AltroDocumento)
        : Griglia.Vuota);

    private IRenderedComponent<ImportaTabella> Pannello() =>
        RenderComponent<ImportaTabella>(p => p
            .Add(x => x.Spec, Spec)
            .Add(x => x.Sorgenti, Sorgenti)
            .Add(x => x.CaricaSorgente, Sorgente));

    private static IElement Tendina(IRenderedComponent<ImportaTabella> cut) =>
        cut.Find("select[aria-label='Imp_FromDoc']");

    [Fact]
    public void Un_documento_con_righe_apre_l_anteprima()
    {
        var cut = Pannello();

        Tendina(cut).Change("LIML");

        Assert.Contains("SEME-UNO", cut.Markup);
        Assert.DoesNotContain("Imp_FromDocEmpty", cut.Markup);
    }

    /// <summary>Il difetto segnalato: un documento senza righe in QUESTA tabella lo dice, invece di tacere.</summary>
    [Fact]
    public void Un_documento_senza_righe_lo_dice()
    {
        var cut = Pannello();

        Tendina(cut).Change("LIBN");

        Assert.Contains("Imp_FromDocEmpty", cut.Markup);
    }

    /// <summary>⚠️ E la tendina torna al segnaposto: è un comando, non uno stato. Lasciata sul documento
    /// scelto, sceglierlo una seconda volta non emette nessun <c>change</c> — e chi l'ha appena visto non
    /// produrre niente prova proprio quello.</summary>
    [Fact]
    public void La_tendina_torna_al_segnaposto()
    {
        var cut = Pannello();

        Tendina(cut).Change("LIBN");

        Assert.Equal("", Tendina(cut).GetAttribute("value"));
    }

    /// <summary>L'avviso è di chi l'ha acceso: una sorgente nuova se lo porta via.</summary>
    [Fact]
    public void Un_altra_sorgente_spegne_l_avviso()
    {
        var cut = Pannello();
        Tendina(cut).Change("LIBN");
        Assert.Contains("Imp_FromDocEmpty", cut.Markup);

        Tendina(cut).Change("LIML");

        Assert.DoesNotContain("Imp_FromDocEmpty", cut.Markup);
        Assert.Contains("SEME-UNO", cut.Markup);
    }

    /// <summary>⚠️ La pastiglia dice DA DOVE viene la griglia, e su una tabella presa da un altro documento
    /// diceva «righe intere» — cioè l'opposto: «una cella sola per riga, spezzala tu». Le celle sono già
    /// quelle.</summary>
    [Fact]
    public void La_pastiglia_dice_da_dove_viene()
    {
        var cut = Pannello();

        Tendina(cut).Change("LIML");

        Assert.Contains("Imp_FormFromDoc", cut.Markup);
        Assert.DoesNotContain("Imp_FormLines", cut.Markup);
    }
}

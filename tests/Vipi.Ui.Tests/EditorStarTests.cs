using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Domain.Entities;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Lo stesso editor montato sugli ARRIVI: quel che cambia col verso — i titoli, l'etichetta della colonna del
/// nome, la colonna «Initial climb» che su una STAR non vuol dire niente, il tasto di reimport che sta da una
/// parte sola, e gli id degli elenchi a discesa, che con due montaggi nella stessa pagina non possono coincidere.
/// </summary>
public class EditorStarTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public EditorStarTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddLogging();
    }

    private static ImportedSidEdit Imp(int id, string fix, string nome, string? pista) =>
        new() { Id = id, Fix = fix, Name = nome, Runway = pista };

    private IRenderedComponent<AirportSidsEditor> Rendi(ProcedureKind kind, bool editing = true) =>
        RenderComponent<AirportSidsEditor>(p => p
            .Add(x => x.Kind, kind)
            .Add(x => x.Imported, new List<ImportedSidEdit> { Imp(1, "GILIO", kind == ProcedureKind.Star ? "GILI3A" : "ALAX7G", "16L") })
            .Add(x => x.Manual, new List<SidEdit> { new() { Fix = "OSTIA", Name = "OST7A" } })
            .Add(x => x.RunwayIdents, new[] { "16L", "16R" })
            .Add(x => x.Editing, editing)
            .Add(x => x.PersistImported, _ => Task.CompletedTask)
            .Add(x => x.RunGuarded, async (Func<Task> a) => { await a(); return true; })
            .Add(x => x.ManualChanged, () => { }));

    [Fact]
    public void La_Colonna_Del_Nome_Dice_Star()
    {
        var arrivi = Rendi(ProcedureKind.Star).Markup;
        var partenze = Rendi(ProcedureKind.Sid).Markup;

        Assert.Contains("<th class=\"col-sid\">STAR</th>", arrivi);
        Assert.DoesNotContain("<th class=\"col-sid\">SID</th>", arrivi);
        Assert.Contains("<th class=\"col-sid\">SID</th>", partenze);
    }

    [Fact]
    public void Sugli_Arrivi_Non_Ce_La_Colonna_Initial_Climb()
    {
        // Su un arrivo la quota iniziale di salita non vuol dire niente: una colonna che nessuno può
        // riempire è peggio di una colonna che non c'è.
        Assert.DoesNotContain("col-climb", Rendi(ProcedureKind.Star).Markup);
        Assert.Contains("col-climb", Rendi(ProcedureKind.Sid).Markup);
    }

    [Fact]
    public void I_Titoli_Sono_Quelli_Degli_Arrivi()
    {
        // Il localizzatore di prova rende la CHIAVE: così si vede quale chiave chiede il componente.
        var arrivi = Rendi(ProcedureKind.Star).Markup;

        Assert.Contains("Ape_StarImportedTitle", arrivi);
        Assert.Contains("Ape_StarManualTitle", arrivi);
        Assert.DoesNotContain("Ape_SidImportedTitle", arrivi);
    }

    [Fact]
    public void Il_Reimport_Sta_Solo_Sulle_Partenze()
    {
        // Il giro d'import porta i due versi insieme: due tasti che fanno la stessa cosa sono due occasioni
        // di chiedersi quale premere.
        Assert.DoesNotContain("Ape_ReimportSid", Rendi(ProcedureKind.Star).Markup);
        Assert.Contains("Ape_ReimportSid", Rendi(ProcedureKind.Sid).Markup);
    }

    [Fact]
    public void I_Due_Montaggi_Non_Condividono_Gli_Id_Degli_Elenchi()
    {
        // Le due tabelle stanno nella STESSA pagina: due elementi con lo stesso id sono un documento
        // sbagliato, e il browser ne sceglie uno solo.
        var arrivi = Rendi(ProcedureKind.Star).Markup;
        var partenze = Rendi(ProcedureKind.Sid).Markup;

        Assert.Contains("id=\"ape-rwy-idents-star\"", arrivi);
        Assert.Contains("id=\"ape-sid-types-star\"", arrivi);
        Assert.Contains("id=\"ape-rwy-idents\"", partenze);
        Assert.DoesNotContain("ape-rwy-idents-star", partenze);
    }
}

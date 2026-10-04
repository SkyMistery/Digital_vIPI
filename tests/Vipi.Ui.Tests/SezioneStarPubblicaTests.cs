using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La sezione STAR come la vede il PUBBLICO: stessa tabella delle SID, montata sugli arrivi. Quel che cambia
/// è il nome della colonna del codice, l'<c>Initial climb</c> che su un arrivo non c'è, e quale pista in uso
/// marca la chip — in arrivo, non in partenza.
/// </summary>
public class SezioneStarPubblicaTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SezioneStarPubblicaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static AirportSidView Arrivi() => new(new[]
    {
        new AirportSidRowView("16L", "GILIO", "GILI 3A", "—", "—", "RNAV", "—", "—", "—"),
        new AirportSidRowView("34R", "ELKAN", "ELKA 3C", "—", "—", "RNAV", "—", "—", "—"),
    });

    private IRenderedComponent<AirportSids> Rendi(ProcedureKind kind, string[]? dep = null, string[]? arr = null) =>
        RenderComponent<AirportSids>(p => p
            .Add(x => x.Kind, kind)
            .Add(x => x.View, Arrivi())
            .Add(x => x.DepIdents, dep ?? Array.Empty<string>())
            .Add(x => x.ArrIdents, arr ?? Array.Empty<string>()));

    [Fact]
    public void La_Colonna_Del_Codice_Dice_Star()
    {
        Assert.Contains("<th class=\"col-sid\">STAR</th>", Rendi(ProcedureKind.Star).Markup);
        Assert.Contains("<th class=\"col-sid\">SID</th>", Rendi(ProcedureKind.Sid).Markup);
    }

    [Fact]
    public void Sugli_Arrivi_Niente_Initial_Climb()
    {
        Assert.DoesNotContain("col-climb", Rendi(ProcedureKind.Star).Markup);
        Assert.Contains("col-climb", Rendi(ProcedureKind.Sid).Markup);
    }

    [Fact]
    public void La_Chip_Marca_La_Pista_In_ARRIVO()
    {
        // ⚠️ La pista in uso di una tabella d'arrivi è quella in arrivo: marcare quella in partenza
        // direbbe al lettore che scende dove invece si decolla.
        var conArrivo = Rendi(ProcedureKind.Star, dep: new[] { "16L" }, arr: new[] { "34R" }).Markup;

        Assert.Contains("🛬", conArrivo);
        Assert.DoesNotContain("🛫", conArrivo);
        // La chip marcata è quella della 34R, non della 16L.
        var i34 = conArrivo.IndexOf("34R", StringComparison.Ordinal);
        var iMark = conArrivo.IndexOf("🛬", StringComparison.Ordinal);
        Assert.True(iMark > i34, "il segno sta nella chip della pista in arrivo");
    }

    [Fact]
    public void Senza_Righe_Lo_Dice_Con_Le_Parole_Degli_Arrivi()
    {
        var cut = RenderComponent<AirportSids>(p => p
            .Add(x => x.Kind, ProcedureKind.Star)
            .Add(x => x.View, AirportSidView.Empty));

        Assert.Contains("Airport_NoStarsTitle", cut.Markup);
        Assert.DoesNotContain("Airport_NoSidsTitle", cut.Markup);
    }

    /// <summary>
    /// «Transition» sul telefono (committente, 1 ottobre 2026): la colonna è stretta e la parola finiva sopra
    /// «Initial climb». L'intestazione porta tutt'e due le forme — il foglio sceglie «Tran.» sotto i 760px — e quella
    /// corta dice per esteso che cosa abbrevia.
    /// </summary>
    [Fact]
    public void Transition_ha_anche_la_forma_corta_per_il_telefono()
    {
        var th = Rendi(ProcedureKind.Sid).Find("th.col-trans");

        Assert.Equal("Transition", th.QuerySelector(".th-lungo")!.TextContent);
        var corta = th.QuerySelector("abbr.th-corto")!;
        Assert.Equal("Tran.", corta.TextContent);
        Assert.Equal("Transition", corta.GetAttribute("title"));
    }
}

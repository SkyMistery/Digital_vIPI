using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le SID citate nel testo (§A73) nel posto dove si disegnano: <see cref="BlockRenderer"/> sostituisce il
/// riferimento col nome che la pagina gli passa a cascata, in ogni tipo di blocco. Senza nomi, esce l'ultimo
/// nome visto — mai il codice <c>[[SID …]]</c>.
/// </summary>
public class RiferimentiSidRenderTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public RiferimentiSidRenderTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static readonly NomiSid Oggi = new(new Dictionary<string, AirportSidView>
    {
        ["LIRF"] = new(new[] { new AirportSidRowView("16L", "OSTIA", "OST2E", "—", "—", "—", "—", "—", "—") }),
    });

    private static BlockView Blocco(BlockFormat format, string? body = null, string? json = null,
        CalloutKind? callout = null) => new()
    {
        Id = 1, Format = format, State = RenderState.Expanded, Body = body, BodyJson = json, CalloutKind = callout,
    };

    private IRenderedComponent<BlockRenderer> Disegna(BlockView blocco, NomiSid? nomi) =>
        nomi is null
            ? RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, blocco))
            : RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, blocco).AddCascadingValue(nomi));

    [Fact]
    public void Nella_prosa_esce_il_nome_di_oggi()
    {
        var cut = Disegna(Blocco(BlockFormat.Prose, body: "Expect **[[SID LIRF OST1E]]** after departure."), Oggi);
        Assert.Contains("<strong>OSTIA 2E</strong>", cut.Markup);
        Assert.DoesNotContain("[[SID", cut.Markup);
        Assert.DoesNotContain("OST1E", cut.Markup);
    }

    [Fact]
    public void Nel_callout_esce_il_nome_di_oggi()
    {
        var cut = Disegna(Blocco(BlockFormat.Callout, body: "Solo [[SID LIRF OST1E]].", callout: CalloutKind.Warning), Oggi);
        Assert.Contains("Solo OSTIA 2E.", cut.Markup);
    }

    [Fact]
    public void Nella_cella_di_una_tabella_esce_il_nome_di_oggi()
    {
        var json = """{"columns":["Punto","SID"],"rows":[{"cells":["OSTIA","[[SID LIRF OST1E]]"]}]}""";
        var cut = Disegna(Blocco(BlockFormat.Table, json: json), Oggi);
        var celle = cut.FindAll("td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "OSTIA", "OSTIA 2E" }, celle);
    }

    /// <summary>⚠️ La rete: una pagina che non passa i nomi (o un blocco disegnato fuori da un documento) non
    /// mostra MAI il codice del riferimento.</summary>
    [Fact]
    public void Senza_nomi_esce_l_ultimo_nome_visto()
    {
        var cut = Disegna(Blocco(BlockFormat.Prose, body: "Expect [[SID LIRF OST1E]]."), null);
        Assert.Contains("Expect OST1E.", cut.Markup);
        Assert.DoesNotContain("[[SID", cut.Markup);
    }

    [Fact]
    public void Un_blocco_senza_riferimenti_passa_identico()
    {
        var blocco = Blocco(BlockFormat.Prose, body: "Niente SID qui.");
        Assert.Same(blocco, blocco.ConTesti(t => RiferimentiSid.Sostituisci(t, Oggi)));
    }
}

using System.Linq;
using Bunit;
using Vipi.Application.Abstractions;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'elenco nativo dei punti. Sono ~1400 voci in una pagina che si ridisegna a ogni tasto scritto nella
/// casella di ricerca delle SID: la ragione per cui è un componente a parte è che <b>non si ridisegni</b>.
/// Se un giorno qualcuno togliesse <c>ShouldRender</c>, niente si romperebbe a vista — la pagina si
/// limiterebbe a rifare 1400 nodi d'albero per ogni carattere digitato. Questo test è l'unico posto che se
/// ne accorgerebbe.
/// </summary>
public class NavaidDatalistTests : TestContext
{
    private static readonly IReadOnlyList<NavaidName> Tre = new[]
    {
        new NavaidName("ALAXI", NavaidKind.Fix),
        new NavaidName("OST", NavaidKind.Vor),
        new NavaidName("AVI", NavaidKind.Ndb),
    };

    [Fact]
    public void Ogni_voce_porta_il_nome_e_la_natura()
    {
        var cut = RenderComponent<NavaidDatalist>(p => p.Add(x => x.Entries, Tre));

        // ToList: indicizzare direttamente la collezione «aggiornabile» di bUnit non funziona con la
        // versione di AngleSharp in uso (MissingMethodException sull'indicizzatore).
        var options = cut.FindAll("option").ToList();
        Assert.Equal(3, options.Count);
        // Il VALORE è il solo nome: è quello che finisce nel campo. La natura è il testo accanto, che il
        // browser mostra come descrizione — serve a distinguere due omonimi, non a essere scritto.
        Assert.Equal("ALAXI", options[0].GetAttribute("value"));
        Assert.Equal("FIX", options[0].TextContent);
        Assert.Equal("VOR", options[1].TextContent);
        Assert.Equal("NDB", options[2].TextContent);
    }

    [Fact]
    public void L_id_predefinito_e_quello_che_i_campi_citano()
    {
        var cut = RenderComponent<NavaidDatalist>(p => p.Add(x => x.Entries, Tre));
        Assert.Equal(NavaidDatalist.DefaultId, cut.Find("datalist").GetAttribute("id"));
    }

    [Fact]
    public void Un_nuovo_render_col_MEDESIMO_elenco_non_ridisegna()
    {
        var cut = RenderComponent<NavaidDatalist>(p => p.Add(x => x.Entries, Tre));
        var dopoIlPrimo = cut.RenderCount;

        // È il caso vero: la pagina si ridisegna (un tasto nella ricerca) e passa lo stesso elenco.
        cut.SetParametersAndRender(p => p.Add(x => x.Entries, Tre));

        Assert.Equal(dopoIlPrimo, cut.RenderCount);
    }

    [Fact]
    public void Quando_il_catalogo_ARRIVA_si_ridisegna_una_volta()
    {
        // Il catalogo si carica dopo il primo disegno: si parte vuoti, e quel passaggio deve passare.
        var cut = RenderComponent<NavaidDatalist>(p => p.Add(x => x.Entries, Array.Empty<NavaidName>()));
        Assert.Empty(cut.FindAll("option").ToList());
        var vuoto = cut.RenderCount;

        cut.SetParametersAndRender(p => p.Add(x => x.Entries, Tre));

        Assert.True(cut.RenderCount > vuoto);
        Assert.Equal(3, cut.FindAll("option").ToList().Count);
    }

    [Fact]
    public void Un_catalogo_vuoto_e_un_elenco_vuoto_non_un_errore()
    {
        // Sorgente irraggiungibile: il campo resta scrivibile, si perde solo il suggerimento.
        var cut = RenderComponent<NavaidDatalist>(p => p.Add(x => x.Entries, Array.Empty<NavaidName>()));
        Assert.NotNull(cut.Find("datalist"));
        Assert.Empty(cut.FindAll("option").ToList());
    }
}

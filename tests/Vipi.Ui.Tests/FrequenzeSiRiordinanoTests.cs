using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le frequenze si riordinano in TUTTI gli editor, non solo in quello di ACC.
///
/// <para>ACC e APP trascinavano le righe dall'inizio; la vLOA e i due editor d'aeroporto (civile e militare)
/// no. Qui stanno le due metà che mancavano: la tabella condivisa che deve mostrare <b>insieme</b> il
/// riordino e le azioni di riga (la vLOA accende e spegne le frequenze mentre le ordina), e le righe
/// COLLEGATE dell'aeroporto, le sole che si spostano — il catalogo dei settori è dell'anagrafica, e da qui
/// non si tocca.</para>
/// </summary>
public class FrequenzeSiRiordinanoTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public FrequenzeSiRiordinanoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ---- la tabella condivisa (ACC, APP, vLOA) ----

    private static AppFreqRow Freq(string callsign, string freq) =>
        new(null, callsign, callsign, freq, "CTR", false, false);

    /// <summary>
    /// ⚠️ In modifica le due colonne di coda CONVIVONO. Erano in alternativa: la vLOA, che è l'unica a
    /// passare <c>RowActions</c>, in modifica perdeva l'interruttore «mostra/nascondi» — cioè il comando che
    /// decide che cosa finisce nel documento — appena si dava alla tabella la possibilità di riordinare.
    /// </summary>
    [Fact]
    public void In_modifica_riordino_e_azioni_di_riga_stanno_insieme()
    {
        var cut = RenderComponent<AppFrequencies>(p => p
            .Add(x => x.Rows, new[] { Freq("LIRR_NE_CTR", "128.800"), Freq("DTTC_CTR", "129.300") })
            .Add(x => x.Editing, true)
            .Add(x => x.RowActions, (RenderFragment<AppFreqRow>)(row => b => { b.OpenElement(0, "button"); b.AddAttribute(1, "class", "interruttore"); b.AddContent(2, row.Callsign); b.CloseElement(); })));

        Assert.Equal(2, cut.FindAll(".app-drag").Count);          // la maniglia su ogni riga
        Assert.Equal(2, cut.FindAll("button.interruttore").Count); // e l'azione di riga, che spariva
        // Intestazione e corpo devono avere lo stesso numero di colonne, o la tabella si sfasa.
        Assert.Equal(cut.FindAll("thead th").Count, cut.FindAll("tbody tr").First().QuerySelectorAll("td").Length);
    }

    [Fact]
    public void Fuori_modifica_non_c_e_niente_da_trascinare()
    {
        var cut = RenderComponent<AppFrequencies>(p => p
            .Add(x => x.Rows, new[] { Freq("LIRR_NE_CTR", "128.800") })
            .Add(x => x.Editing, false));

        Assert.Empty(cut.FindAll(".app-drag"));
    }

    // ---- aeroporto e militare: solo le righe collegate ----

    private static AirportSectorRow Catalogo(string compose, string position, string freq) =>
        new(1, compose, "LIRP", "LIRR", position, null, freq, null, null, false, false, false, false);

    private static LinkableFrequencyRow Collegabile(int id, string callsign, string freq) =>
        new(id, "LIRP", callsign, freq);

    [Fact]
    public void Le_righe_collegate_si_trascinano_il_catalogo_no()
    {
        var links = new List<int> { 7, 9 };
        var cut = RenderComponent<AirportFrequenciesEditor>(p => p
            .Add(x => x.Catalogo, new[] { Catalogo("LIRP_TWR", "TWR", "118.300") })
            .Add(x => x.LinkIds, links)
            .Add(x => x.Linkable, new[] { Collegabile(7, "LIRR_NE_CTR", "128.800"), Collegabile(9, "LIML_APP", "126.000") })
            .Add(x => x.Editing, true));

        // Tre righe in tabella, ma solo le due collegate hanno la maniglia e i tasti d'ordine.
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Equal(2, cut.FindAll(".app-drag").Count);
    }

    [Fact]
    public void Il_tasto_giu_scambia_le_righe_collegate_e_salva()
    {
        var links = new List<int> { 7, 9 };
        var salvataggi = 0;
        var cut = RenderComponent<AirportFrequenciesEditor>(p => p
            .Add(x => x.Catalogo, new[] { Catalogo("LIRP_TWR", "TWR", "118.300") })
            .Add(x => x.LinkIds, links)
            .Add(x => x.Linkable, new[] { Collegabile(7, "LIRR_NE_CTR", "128.800"), Collegabile(9, "LIML_APP", "126.000") })
            .Add(x => x.Editing, true)
            .Add(x => x.LinksChanged, () => { salvataggi++; }));

        cut.Find("button[title='Common_Down']").Click();   // il primo ↓: la prima riga collegata

        Assert.Equal(new[] { 9, 7 }, links);   // l'ordine È la lista dei link: chi la salva scrive Order
        Assert.Equal(1, salvataggi);           // e si salva subito, come collegare e scollegare
    }

    /// <summary>Senza il lock del documento non si sposta niente: stessa regola della ✕ e del picker.</summary>
    [Fact]
    public void Senza_modifica_le_righe_collegate_non_si_spostano()
    {
        var cut = RenderComponent<AirportFrequenciesEditor>(p => p
            .Add(x => x.Catalogo, System.Array.Empty<AirportSectorRow>())
            .Add(x => x.LinkIds, new List<int> { 7 })
            .Add(x => x.Linkable, new[] { Collegabile(7, "LIRR_NE_CTR", "128.800") })
            .Add(x => x.Editing, false));

        Assert.Empty(cut.FindAll(".app-drag"));
        Assert.Empty(cut.FindAll("button[title='Common_Down']"));
    }
}

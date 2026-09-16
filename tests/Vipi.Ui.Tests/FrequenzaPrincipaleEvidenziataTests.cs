using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La frequenza principale (★) è evidenziata di blu in TUTTE le tabelle frequenze (16 settembre 2026).
///
/// <para>Segnalato dal committente: «alcune di quelle con la stellina non sono evidenziate di blu, altre sì».
/// Le tabelle frequenze passano da tre componenti, e la regola del foglio è una sola —
/// <c>.freq-table tr.primary td</c> — che pretende la classe <c>freq-table</c> su un <b>antenato</b> della riga.
/// <c>AppFrequencies</c> (APP, vIPI ACC, vLOA, Live) la metteva sulla riga stessa, e il blu non scattava mai.</para>
///
/// <para>⚠️ Il test non guarda il colore — bUnit non applica fogli di stile — ma il <b>contratto</b> che il
/// foglio pretende: ogni ★ sta in una <c>tr.primary</c> dentro una <c>table.freq-table</c>. È la stessa
/// condizione del selettore, controllata su ogni componente, in lettura e in modifica.</para>
/// </summary>
public class FrequenzaPrincipaleEvidenziataTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public FrequenzaPrincipaleEvidenziataTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    /// <summary>Il selettore del foglio, a parole: la stella sta in una riga <c>primary</c> di una tabella <c>freq-table</c>.</summary>
    private static void OgniStellaEvidenziata(IRenderedFragment c)
    {
        var stelle = c.FindAll(".freq-key");
        Assert.NotEmpty(stelle);
        foreach (var s in stelle)
        {
            var riga = s.Closest("tr");
            Assert.NotNull(riga);
            Assert.True(riga!.ClassList.Contains("primary"), "la riga della ★ non ha la classe primary");
            Assert.NotNull(riga.Closest("table.freq-table"));
            // ⚠️ E NON sulla riga stessa: lì il selettore `.freq-table tr.primary` non combacia.
            Assert.False(riga.ClassList.Contains("freq-table"), "freq-table va sulla tabella, non sulla riga");
        }
        // E al contrario: nessuna riga blu senza stella.
        Assert.Equal(stelle.Count, c.FindAll("table.freq-table tr.primary").Count);
    }

    private static AppFreqRow App(string callsign, string freq, bool primaria) =>
        new(null, callsign, callsign, freq, "APP", primaria, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AppFrequencies_ACC_APP_vLOA_Live(bool inModifica)
    {
        var c = RenderComponent<AppFrequencies>(p => p
            .Add(x => x.Rows, new[] { App("LIBR_TWR", "118.300", false), App("LIBR_APP", "119.100", true) })
            .Add(x => x.Editing, inModifica));

        OgniStellaEvidenziata(c);
    }

    [Fact]
    public void AppFrequencies_raggruppata_della_vIPI_ACC()
    {
        var c = RenderComponent<AppFrequencies>(p => p
            .Add(x => x.Rows, new[] { App("LIBB_NE_CTR", "128.300", true), App("LIBB_ES_CTR", "129.300", false) })
            .Add(x => x.GroupOf, (Func<AppFreqRow, string?>)(_ => "Brindisi")));

        OgniStellaEvidenziata(c);
        Assert.NotNull(c.Find("table.freq-table.cfg-table"));   // la classe del raggruppamento resta
    }

    [Fact]
    public void AirportFrequencies_vIPI_aeroporto_e_vSOP_militare()
    {
        var c = RenderComponent<AirportFrequencies>(p => p.Add(x => x.View, new AirportFreqView(new[]
        {
            new AirportFreqRowView("Brindisi Tower", "LIBR_TWR", "118.300", true),
            new AirportFreqRowView("Brindisi Ground", "LIBR_GND", "121.900", false),
        })));

        OgniStellaEvidenziata(c);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AirportFrequenciesEditor_editor_aeroporto_e_militare(bool inModifica)
    {
        var c = RenderComponent<AirportFrequenciesEditor>(p => p
            .Add(x => x.Catalogo, new[]
            {
                new AirportSectorRow(1, "LIBR_TWR", "LIBR", "LIBB", "TWR", null, "118.300", null, null, false, false, true, false),
                new AirportSectorRow(2, "LIBR_GND", "LIBR", "LIBB", "GND", null, "121.900", null, null, false, false, false, false),
            })
            .Add(x => x.LinkIds, new List<int>())
            .Add(x => x.Linkable, Array.Empty<LinkableFrequencyRow>())
            .Add(x => x.Editing, inModifica));

        OgniStellaEvidenziata(c);
    }

    /// <summary>
    /// Le tabelle salvate nei blocchi: <c>star</c> e <c>primary</c> sono due flag del JSON, e una riga con la sola
    /// stella usciva senza blu — lo stesso difetto, in forma di dato.
    /// </summary>
    [Fact]
    public void TableBlock_una_riga_con_la_sola_stella_e_blu()
    {
        var c = RenderComponent<Vipi.Ui.Components.Blocks.TableBlock>(p => p.Add(x => x.Block, new BlockView
        {
            Id = 1,
            Format = BlockFormat.Table,
            State = RenderState.Expanded,
            BodyJson = """{"columns":["Callsign","Freq"],"rows":[{"cells":["LIBR_TWR","118.300"],"star":true},{"cells":["LIBR_GND","121.900"]}]}""",
        }));

        OgniStellaEvidenziata(c);
    }

    /// <summary>
    /// Il foglio: la regola del blu c'è, e nessuna regola di modifica la ricopre. Quella che c'era
    /// (<c>.freq-edit tr.primary td</c>) veniva dopo con la stessa specificità e in modifica dava una tinta chiara.
    /// </summary>
    [Fact]
    public void Il_foglio_ha_il_blu_e_niente_lo_ricopre_in_modifica()
    {
        var css = File.ReadAllText(FileNellaWwwroot("vipi-theme.css"));
        Assert.Contains(".freq-table tr.primary td{background:", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".freq-edit tr.primary td{", css, StringComparison.Ordinal);
    }

    private static string FileNellaWwwroot(string nome)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot", nome);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{nome} non trovato risalendo da {AppContext.BaseDirectory}");
    }
}

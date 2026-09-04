using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Import;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Il pannello «Importa tabella» non deve toccare il DbContext del circuito.</b>
///
/// <para>🔴 Nove richieste in errore in produzione il 4 settembre 2026, quattro da qui:
/// <c>A second operation was started on this context instance</c>, con lo stack
/// <c>ScriviTesto</c> → <c>Ricostruisci</c> → <c>CostruttoreProposta</c> → <c>RisolutoreCelle.ScaliAsync</c>
/// → <c>EfAirportNameLookup</c>.</para>
///
/// <para>⚠️ <b>E la diagnostica ha corretto la diagnosi.</b> La prima lettura era «due ricostruzioni che si
/// sovrappongono»; il riquadro «che cosa era aperto sul DbContext» diceva altro — <c>SELECT … FROM Airports</c>
/// e <c>SELECT … FROM AirportSectors</c>, cioè <b>il caricamento della pagina che ospita l'import</b>. Non
/// l'import contro sé stesso: l'import contro la pagina. Il rimedio quindi non è mettere in fila le
/// ricostruzioni — è togliere il pannello dal contesto condiviso.</para>
///
/// <para>⚠️ <b>Perché qui ci sono anche guardie strutturali.</b> Il banco di prova monta un componente per
/// volta, e il dispatcher di Blazor serializza i gestori d'evento: una collisione fra il pannello e il
/// caricamento della sua pagina <b>non si riproduce</b> montando il solo pannello. Quel che si può difendere
/// è la <b>forma</b> del rimedio — lo scope proprio, e nessuna iniezione dal circuito — e diventa rossa se
/// qualcuno la disfa.</para>
/// </summary>
public class ImportaTabellaUnaAllaVoltaTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string n] => new(n, n, resourceNotFound: false);
        public LocalizedString this[string n, params object[] a] => new(n, n, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool p) => Enumerable.Empty<LocalizedString>();
    }

    public ImportaTabellaUnaAllaVoltaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ---- la forma del rimedio ---------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>La guardia che conta.</b> Il pannello deriva da <c>OwningComponentBase</c>: ha uno scope suo,
    /// quindi un <c>DbContext</c> suo, e non può più scontrarsi con la pagina che lo contiene. Chi lo
    /// riporta a <c>ComponentBase</c> rimette il difetto.
    /// </summary>
    [Fact]
    public void Il_pannello_ha_uno_scope_tutto_suo()
    {
        Assert.True(typeof(OwningComponentBase).IsAssignableFrom(typeof(ImportaTabella)),
            "ImportaTabella deve derivare da OwningComponentBase: senza, il risolutore torna sul DbContext del circuito.");
    }

    /// <summary>
    /// E nessuno glielo passa più dall'alto: <c>MilSectionsEditor</c> lo prendeva con <c>@@inject</c>, cioè
    /// dal circuito, e lo consegnava al pannello. Era quello il filo che legava l'import alla pagina.
    /// </summary>
    [Theory]
    [InlineData("Components/Doc/MilSectionsEditor.razor")]
    [InlineData("Components/App/MilDiversionsEditor.razor")]
    public void Nessuno_passa_piu_il_risolutore_dal_circuito(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(RadiceUi(), relativo));

        Assert.DoesNotContain("@inject Vipi.Application.Import.IRisolutoreCelle", sorgente);
        Assert.DoesNotContain("Risolutore=\"", sorgente);
    }

    // ---- una ricostruzione alla volta -------------------------------------------------------------------

    /// <summary>
    /// Il risolutore finto conta quante risoluzioni sono <b>dentro</b> nello stesso momento. Cede il
    /// controllo con <c>Task.Yield</c>: bastano quelle a far interlacciare due flussi asincroni.
    /// </summary>
    private sealed class RisolutoreCheConta : IRisolutoreCelle
    {
        private int _dentro;
        public int MassimoInsieme { get; private set; }
        public int Chiamate { get; private set; }

        public async Task<IReadOnlyDictionary<string, EsitoRisoluzione>> RisolviAsync(
            TipoCella tipo, IReadOnlyCollection<string> valori, CancellationToken ct = default)
        {
            Chiamate++;
            var ora = ++_dentro;
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try
            {
                for (var i = 0; i < 5; i++) { await Task.Yield(); ct.ThrowIfCancellationRequested(); }
                return valori.ToDictionary(
                    v => v,
                    v => new EsitoRisoluzione($"{v} risolto", EsitoCella.Risolta, v),
                    StringComparer.OrdinalIgnoreCase);
            }
            finally { _dentro--; }
        }
    }

    /// <summary>Una colonna di tipo Aeroporto: è quella che manda il risolutore sul catalogo.</summary>
    private static readonly SpecImport Spec = new("alternati", new[]
    {
        new ColonnaSpec("scalo", "Scalo", TipoCella.Aeroporto),
        new ColonnaSpec("nota", "Nota", TipoCella.Testo),
    });

    private IRenderedComponent<ImportaTabella> Pannello(IRisolutoreCelle risolutore) =>
        RenderComponent<ImportaTabella>(p => p
            .Add(x => x.Spec, Spec)
            .Add(x => x.Risolutore, risolutore));

    private const string Incollato = "LIRF\tuno\nLIMC\tdue\nLIPZ\ttre";

    /// <summary>
    /// Ora che il pannello ha un <c>DbContext</c> suo, l'unica collisione che gli resta possibile è
    /// <b>contro sé stesso</b>: due ricostruzioni sue, sullo stesso contesto privato. Questo lo esclude.
    /// ⚠️ Il dispatcher serializza già i gestori d'evento, quindi questa prova è un <b>pavimento</b>, non la
    /// dimostrazione del rimedio: quella è la guardia strutturale qui sopra.
    /// </summary>
    [Fact]
    public async Task Due_gesti_vicini_non_sovrappongono_due_risoluzioni()
    {
        var risolutore = new RisolutoreCheConta();
        var cut = Pannello(risolutore);

        var primo = cut.Find("textarea").ChangeAsync(new() { Value = Incollato });
        var secondo = cut.Find("input[type=checkbox]").ChangeAsync(new() { Value = true });
        await Task.WhenAll(primo, secondo);

        Assert.Equal(1, risolutore.MassimoInsieme);
        Assert.Equal(2, risolutore.Chiamate);
    }

    /// <summary>E l'anteprima alla fine c'è: mettere in fila non deve lasciare il pannello vuoto.</summary>
    [Fact]
    public async Task Alla_fine_l_anteprima_c_e()
    {
        var risolutore = new RisolutoreCheConta();
        var cut = Pannello(risolutore);

        var primo = cut.Find("textarea").ChangeAsync(new() { Value = Incollato });
        var secondo = cut.Find("input[type=checkbox]").ChangeAsync(new() { Value = true });
        await Task.WhenAll(primo, secondo);

        Assert.Contains("LIMC", cut.Markup);
    }

    private static string RadiceUi()
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

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Import;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete sulla <b>barra dei comandi</b> dell'import: è una regressione che i test non vedono mai da soli —
/// un <c>&lt;input type=file&gt;</c> nudo funziona benissimo, e l'unica cosa che non va è che disegna il
/// bottone del <b>sistema</b> («Choose File», in inglese qualunque sia la lingua della pagina) in mezzo a
/// comandi nostri.
///
/// <para>Morde se torna l'input nudo (l'etichetta-bottone sparisce), se l'input smette di stare
/// <i>dentro</i> l'etichetta — è lui a ricevere il clic, e fuori non lo riceve più nessuno — o se le due
/// caselle perdono la classe che dà loro la cornice della barra.</para>
/// </summary>
public class ImportaTabellaBarraTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ImportaTabellaBarraTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private IRenderedComponent<ImportaTabella> Barra() =>
        RenderComponent<ImportaTabella>(p => p.Add(x => x.Spec, SpecImport.Generica()));

    /// <summary>⚠️ Il file si sceglie da un'<b>etichetta</b> con l'aspetto di un bottone: l'input vero ci
    /// sta dentro, invisibile ma sovrapposto.</summary>
    [Fact]
    public void Il_file_si_sceglie_da_un_bottone_nostro_non_da_quello_di_sistema()
    {
        var cut = Barra();

        var etichetta = cut.Find("label.btn-file");
        Assert.Contains("btn", etichetta.ClassList);
        Assert.NotNull(etichetta.QuerySelector("input[type=file]"));

        // Nessun input file FUORI dall'etichetta: sarebbe di nuovo il bottone del sistema.
        foreach (var input in cut.FindAll("input[type=file]"))
            Assert.NotNull(input.Closest("label.btn-file"));
    }

    /// <summary>Le due caselle sono comandi della barra e ne portano la cornice: senza la classe si leggono
    /// come etichette di testo in mezzo a un bottone e a una tendina.</summary>
    [Fact]
    public void Le_due_caselle_portano_la_cornice_della_barra()
    {
        var cut = Barra();

        var caselle = cut.FindAll(".imp-tools label.imp-check");
        Assert.Equal(2, caselle.Count);
        Assert.All(caselle, c => Assert.NotNull(c.QuerySelector("input[type=checkbox]")));
    }

    /// <summary>La tendina «da un altro documento» c'è solo se qualcuno le ha dato delle sorgenti: senza,
    /// sarebbe un comando che non porta da nessuna parte.</summary>
    [Fact]
    public void La_tendina_degli_altri_documenti_compare_solo_con_le_sorgenti()
    {
        Assert.Empty(Barra().FindAll(".imp-tools select"));

        var conSorgenti = RenderComponent<ImportaTabella>(p => p
            .Add(x => x.Spec, SpecImport.Generica())
            .Add(x => x.Sorgenti, new[] { new SorgenteTabella("LIBG", "LIBG Grottaglie") }));

        Assert.Single(conSorgenti.FindAll(".imp-tools select"));
    }

    /// <summary>
    /// ⚠️ Il comando «piega in N colonne» si offre <b>solo</b> quando la lettura e' venuta a una colonna
    /// sola — cioe' nel caso del PDF che non emette righe ma una cella per riga. Su un incolla che era gia'
    /// a posto sarebbe un comando in piu' da capire.
    /// </summary>
    [Fact]
    public void Il_comando_per_piegare_si_offre_solo_a_colonna_sola()
    {
        var cut = Barra();
        Assert.Empty(cut.FindAll(".imp-fold"));

        cut.Find("textarea.imp-ta").Change("NAME\nApron ALPHA\nApron OSCAR\nNUMBERS");
        Assert.Single(cut.FindAll(".imp-fold"));

        // Con le colonne gia' separate non c'e' niente da piegare, e il comando sparisce.
        cut.Find("textarea.imp-ta").Change("a\tb\tc\nd\te\tf");
        Assert.Empty(cut.FindAll(".imp-fold"));
    }

    /// <summary>La colonna sola piegata in tre diventa una tabella a tre colonne: e' quel che l'anteprima
    /// deve mostrare prima che qualcuno prema «importa».</summary>
    [Fact]
    public void La_colonna_sola_piegata_diventa_una_tabella()
    {
        var cut = Barra();
        cut.Find("textarea.imp-ta").Change("NOME\nNUMERI\nUSATO DA\nAlfa\n1-4\nSquadrone 1");

        cut.Find(".imp-foldn").Change("3");

        // ⚠️ `.ToList()` e non l'indice diretto: la raccolta viva di bunit indicizza attraverso AngleSharp,
        // e su questa combinazione di versioni l'indicizzatore non si risolve (MissingMethodException).
        var righe = cut.FindAll(".imp-preview tbody tr").ToList();
        Assert.Equal(2, righe.Count);
        Assert.Equal(new[] { "NOME", "NUMERI", "USATO DA" },
            righe[0].QuerySelectorAll("td").Skip(1).Select(c => c.TextContent.Trim()));
    }

    /// <summary>La casella d'incolla dice al foglio di stile — e a `vipi-ui.js` — che qui il TAB scrive una
    /// tabulazione: senza l'attributo il tasto tornerebbe a cambiare campo.</summary>
    [Fact]
    public void La_casella_dichiara_che_il_tab_scrive_una_tabulazione()
    {
        var cut = Barra();

        var casella = cut.Find("textarea.imp-ta");
        Assert.True(casella.HasAttribute("data-tab-testo"));

        // E la scorciatoia e' scritta, perche' una scorciatoia che non si vede non esiste.
        var aiuto = cut.Find(".imp-tabhint");
        Assert.Equal(aiuto.Id, casella.GetAttribute("aria-describedby"));
    }
}

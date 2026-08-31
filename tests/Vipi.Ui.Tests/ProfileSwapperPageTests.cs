using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete sulla pagina dello swapper. Il motore ha i suoi test — e sono quelli veri, sui 26 profili reali —
/// quindi qui si guarda solo ciò che il motore non può sapere: che la pagina si apra senza un profilo
/// caricato (è lo stato in cui la trova chiunque ci arrivi), che il filo di Arianna punti all'hub e non
/// alla documentazione, e che la frase sulla privacy sia quella giusta.
///
/// <para>Quest'ultima non è pignoleria: fuori di qui lo stesso strumento è WebAssembly e promette che i
/// file non lasciano il browser. Copiare quella frase dentro un'applicazione Blazor Server significherebbe
/// scrivere in pagina una cosa falsa, ed è l'unico difetto di questo trasloco che nessun compilatore
/// potrebbe vedere.</para>
/// </summary>
public class ProfileSwapperPageTests : TestContext
{
    /// <summary>Localizer che rende la chiave: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private IRenderedComponent<ProfileSwapperPage> Render()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        // La briciola di pane legge le stringhe in INGLESE FISSO (regole-lingua R3): senza questo
        // servizio la pagina non si costruisce nemmeno.
        Services.AddSingleton(new EnglishStrings());
        return RenderComponent<ProfileSwapperPage>();
    }

    [Fact]
    public void Si_apre_con_le_due_aree_di_caricamento_e_nientaltro()
    {
        var cut = Render();

        // Le due aree di trascinamento ci sono entrambe: sorgente e destinazioni.
        Assert.Equal(2, cut.FindAll("label.swap-drop").Count);

        // Senza profili non si mostra né l'elenco delle sezioni né il tasto che copia: non ci sarebbe
        // nulla da copiare, e un tasto spento su una pagina vuota è solo una domanda senza risposta.
        Assert.Empty(cut.FindAll(".swap-grid"));
        Assert.Empty(cut.FindAll("button.btn.primary"));
    }

    [Fact]
    public void Il_filo_di_arianna_porta_ai_servizi_non_alla_documentazione()
    {
        var cut = Render();
        var link = cut.Find(".breadcrumb a");

        // /services, non /services/vsop: lo swapper è un servizio pari grado della documentazione,
        // non una sua sottopagina. È l'intera ragione della forma delle URL scelta in questo giro.
        Assert.Equal("/services", link.GetAttribute("href"));
    }

    [Fact]
    public void Dichiara_che_i_profili_passano_dal_server()
    {
        var cut = Render();
        Assert.Contains("Swap_Privacy", cut.Markup);
    }

    /// <summary>
    /// Il taglio dell'anteprima esiste perché su un circuito Server ogni riga di diff è markup che passa
    /// dalla rete. Qui si verifica il diff stesso, che è la parte che produce quelle righe: due sezioni
    /// diverse devono dare aggiunte e rimozioni, due identiche nessuna.
    /// </summary>
    [Fact]
    public void Il_diff_distingue_aggiunte_rimozioni_e_uguali()
    {
        var prima = new[] { "[X]\r\n", "A=1\r\n", "B=2\r\n" };
        var dopo = new[] { "[X]\r\n", "A=9\r\n", "B=2\r\n" };

        var righe = LineDiff.Diff(prima, dopo);

        Assert.Contains(righe, r => r.Kind == DiffKind.Removed && r.Text == "A=1");
        Assert.Contains(righe, r => r.Kind == DiffKind.Added && r.Text == "A=9");
        Assert.Contains(righe, r => r.Kind == DiffKind.Equal && r.Text == "B=2");

        // Identiche: nessuna riga marcata. È il caso che in pagina diventa «identica — nessuna modifica».
        Assert.All(LineDiff.Diff(prima, prima), r => Assert.Equal(DiffKind.Equal, r.Kind));
    }
}

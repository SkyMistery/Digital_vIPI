using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La tabella delle aree, nelle sue DUE vesti (carta <c>2026-09-09-aree-boat.md</c> §3e).
///
/// <para>Sotto «Aree di lavoro» ha cinque colonne — freccetta, nome, limiti, attività, nota — e i quindici
/// gettoni. Sotto «Bassa quota (BOAT)» ne ha quattro: lì l'attività sarebbe sempre «LOW LEVEL», e una
/// colonna che dice sempre la stessa cosa non aggiunge niente.</para>
///
/// <para>La freccetta e la sua riga di dettaglio sono del 16 settembre 2026: da allora la tabella è
/// l'<b>unico</b> elenco sotto la mappa, e si è presa quel che diceva l'elenco a schede — tipo, pallino,
/// attivazione, descrizione (carta <c>2026-09-16-aree-di-lavoro-una-tabella-sola.md</c>).</para>
///
/// <para>⚠️ È <b>un parametro con un default</b>, non due componenti: un default si prova, o il giorno che
/// qualcuno lo capovolge la sezione con quindici gettoni perde la colonna e nessuno se ne accorge.</para>
/// </summary>
public class TabellaAreeBoatTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<Vipi.Ui.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public TabellaAreeBoatTests() =>
        Services.AddSingleton<IStringLocalizer<Vipi.Ui.SharedResource>>(new KeyLocalizer());

    private static readonly AccSpecialAreaView[] Aree =
    {
        new("1113", "LI R300A Amendola", "R", "descrizione", "Permanently active", 0, 4000, null),
        new("1014", "LI D409A", "D", null, null, 1500, 14500, null),
    };

    private IRenderedComponent<MilWorkingAreas> Render(bool? attivita = null, bool editing = false,
                                                       IReadOnlyList<AccSpecialAreaView>? aree = null,
                                                       string scope = "reg-regulated") =>
        RenderComponent<MilWorkingAreas>(p =>
        {
            p.Add(x => x.Areas, aree ?? Aree);
            p.Add(x => x.Editing, editing);
            p.Add(x => x.Scope, scope);
            if (attivita is { } a) p.Add(x => x.MostraAttivita, a);
        });

    [Fact]
    public void Le_aree_di_lavoro_hanno_CINQUE_colonne_senza_chiedere_niente()
    {
        // ⚠️ Il default: chi monta la tabella sotto «Aree di lavoro» non passa il parametro, e la colonna
        // dell'attività deve esserci lo stesso.
        var c = Render();

        Assert.Equal(5, c.FindAll("thead th").Count);
        Assert.Single(c.FindAll("th.c-act2"));
        Assert.Equal(Aree.Length, c.FindAll("tbody td.c-act2").Count);
    }

    [Fact]
    public void Le_aree_BOAT_ne_hanno_QUATTRO_intestazione_e_celle_insieme()
    {
        var c = Render(attivita: false);

        Assert.Equal(4, c.FindAll("thead th").Count);
        Assert.Empty(c.FindAll("th.c-act2"));

        // ⚠️ Le CELLE e non solo l'intestazione: una tabella con quattro `th` e cinque `td` per riga si
        // disallinea in silenzio, e le note finirebbero sotto il titolo sbagliato.
        Assert.Empty(c.FindAll("tbody td.c-act2"));
        Assert.Equal(Aree.Length, c.FindAll("tbody td.c-note").Count);
    }

    /// <summary>
    /// La colonna della freccetta c'è in tutt'e due le vesti, e ha un titolo da SENTIRE: un <c>th</c> vuoto
    /// lascia un lettore di schermo ad annunciare «colonna 1, vuota» su ogni riga.
    /// </summary>
    [Fact]
    public void La_colonna_della_freccetta_ce_in_tutte_e_due_e_ha_un_titolo_per_chi_ascolta()
    {
        foreach (var c in new[] { Render(), Render(attivita: false) })
        {
            Assert.Single(c.FindAll("thead th.c-exp"));
            Assert.Contains("Area_Details", c.Find("thead th.c-exp").InnerHtml);
            Assert.NotNull(c.Find("thead th.c-exp .sr-only"));
            Assert.Equal(Aree.Length, c.FindAll("tbody td.c-exp button.milarea-exp").Count);
        }
    }

    [Fact]
    public void In_modifica_le_aree_BOAT_non_mostrano_i_gettoni()
    {
        // La colonna sparisce anche a chi scrive: i quindici gettoni sono l'unico modo di scrivere
        // un'attività, e senza colonna non ce n'è nessuno da premere.
        Assert.Empty(Render(attivita: false, editing: true).FindAll(".tok-set"));
        Assert.NotEmpty(Render(editing: true).FindAll(".tok-set"));
    }

    [Fact]
    public void Senza_aree_la_riga_vuota_copre_TUTTE_le_colonne_che_ci_sono()
    {
        // Un colspan che non torna lascia una cella orfana a destra: si vede subito, ed è il genere di
        // difetto che nasce quando una colonna diventa condizionale.
        var vuoto = Array.Empty<AccSpecialAreaView>();

        Assert.Equal("5", Render(aree: vuoto).Find("tbody td.muted").GetAttribute("colspan"));
        Assert.Equal("4", Render(attivita: false, aree: vuoto).Find("tbody td.muted").GetAttribute("colspan"));
    }

    /// <summary>
    /// La riga di dettaglio: c'è per ogni area, nasce CHIUSA, copre tutte le colonne, e porta attivazione e
    /// descrizione — cioè le due cose per cui esisteva l'elenco a schede.
    /// </summary>
    [Fact]
    public void Ogni_area_ha_la_sua_riga_di_dettaglio_chiusa_e_a_tutta_larghezza()
    {
        var c = Render();

        var righe = c.FindAll("tbody tr.milarea-more").ToList();
        Assert.Equal(Aree.Length, righe.Count);
        foreach (var r in righe)
        {
            Assert.True(r.HasAttribute("hidden"));                       // chiusa all'apertura
            Assert.Equal("5", r.QuerySelector("td")!.GetAttribute("colspan"));
        }

        // La prima area ha tutt'e due i testi, con l'etichetta davanti all'attivazione.
        var prima = c.Find("[data-areamore='1113']");
        Assert.Contains("Area_Activation", prima.InnerHtml);
        Assert.Contains("Permanently active", prima.TextContent);
        Assert.Contains("descrizione", prima.TextContent);

        // ⚠️ La seconda non ne ha nessuno dei due: la freccetta resta accesa e la riga dice «—». Un tasto
        // spento senza motivo, qui, si legge «non ti è permesso».
        var seconda = c.Find("[data-areamore='1014']");
        Assert.Contains("—", seconda.TextContent);
        Assert.DoesNotContain("disabled", c.Find("tbody td.c-exp button.milarea-exp").OuterHtml);
    }

    /// <summary>
    /// Il contratto col JS, e il motivo per cui è un test e non un commento: tolto l'elenco a schede, è la
    /// TABELLA a portare gli attributi con cui <c>vipi-aor.js</c> accende e spegne le voci insieme alla chip.
    /// Se si perdessero, la mappa filtrerebbe e la tabella no — senza un errore.
    /// </summary>
    [Fact]
    public void La_tabella_porta_gli_attributi_che_il_JS_cerca()
    {
        var c = Render(scope: "reg-lowlevel");

        Assert.Equal("reg-lowlevel", c.Find("[data-areacards]").GetAttribute("data-areacards"));
        Assert.NotNull(c.Find("[data-areacount='reg-lowlevel']"));
        Assert.NotNull(c.Find("[data-areaempty='reg-lowlevel']"));
        Assert.Equal(new[] { "1113", "1014" },
                     c.FindAll("tbody tr[data-areacard]").Select(x => x.GetAttribute("data-areacard")));

        // 🔴 La riga di dettaglio NON è un'area: il JS conta i `data-areacard` per dire «ne vedi N su M», e
        // contandola il totale raddoppierebbe.
        Assert.Equal(Aree.Length, c.FindAll("[data-areacard]").Count);
        Assert.Equal(Aree.Length, c.FindAll("[data-areamore]").Count);
        Assert.Empty(c.FindAll("tr.milarea-more[data-areacard]"));
    }

    /// <summary>
    /// Senza aree il conteggio non c'è: la tabella dice già «nessuna area scelta», e sopra usciva «Aree accese:
    /// 0 di 0» — visto dal vivo sul pacchetto 1.28.0, nella sezione BOAT di LIBG.
    /// </summary>
    [Fact]
    public void Senza_aree_niente_conteggio_sopra_la_riga_vuota()
    {
        var c = Render(aree: Array.Empty<AccSpecialAreaView>());

        Assert.Empty(c.FindAll("[data-areacount]"));
        Assert.Empty(c.FindAll("[data-areaempty]"));
        Assert.Contains("Area_None", c.Find("tbody td.muted").TextContent);
        // Con le aree, invece, c'è.
        Assert.Single(Render().FindAll("[data-areacount]"));
    }

    /// <summary>
    /// Due tabelle nella stessa pagina non devono condividere gli <c>id</c> delle righe di dettaglio: due
    /// <c>aria-controls</c> uguali puntano allo stesso bersaglio, e la freccetta di BOAT aprirebbe una riga
    /// delle aree di lavoro.
    /// </summary>
    [Fact]
    public void Scope_diversi_danno_id_diversi_alle_righe_di_dettaglio()
    {
        var a = Render(scope: "reg-regulated").Find("[data-areamore='1113']").Id;
        var b = Render(attivita: false, scope: "reg-lowlevel").Find("[data-areamore='1113']").Id;

        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.NotEqual(a, b);
        // …e il bottone della riga punta al PROPRIO dettaglio, o `aria-expanded` racconterebbe di un altro.
        Assert.Equal(a, Render(scope: "reg-regulated")
            .Find("tr[data-areacard='1113'] button.milarea-exp").GetAttribute("aria-controls"));
    }

    /// <summary>
    /// Quel che la tabella si è presa dall'elenco a schede: il pallino col colore della MAPPA, il tipo, e
    /// «senza forma» — che è un fatto operativo (quell'area sulla mappa non c'è, e la chip non la farà
    /// comparire), non una finezza di resa.
    /// </summary>
    [Fact]
    public void La_cella_del_nome_porta_pallino_tipo_e_senza_forma()
    {
        var c = Render(aree: new[]
        {
            new AccSpecialAreaView("1113", "LI R300A Amendola", "R", null, null, 0, 4000, null),
        });

        var cella = c.Find("tbody tr[data-areacard='1113'] td:nth-child(2)");
        Assert.NotNull(cella.QuerySelector(".reg-dot"));
        Assert.Equal("R", cella.QuerySelector(".reg-type")!.TextContent.Trim());
        Assert.Contains("LI R300A Amendola", cella.TextContent);
        Assert.Contains("Reg_NoShape", cella.InnerHtml);
    }

    /// <summary>
    /// Il gesto lo commuta <c>vipi-ui.js</c>, perché la pagina del lettore è SSR statica: il contratto è
    /// scritto in due file diversi, quindi si prova contro il file vero — come già fa
    /// <c>RegulatedAreasTests</c> per le chip.
    /// </summary>
    [Fact]
    public void Il_JS_che_apre_la_riga_cerca_quello_che_il_componente_scrive()
    {
        var ui = File.ReadAllText(FileNellaWwwroot("vipi-ui.js"));
        var aor = File.ReadAllText(FileNellaWwwroot("vipi-aor.js"));

        // Il bersaglio del clic, la riga gemella, e la verità che commuta.
        Assert.Contains(".milarea-exp", ui, StringComparison.Ordinal);
        Assert.Contains("milarea-more", ui, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", ui, StringComparison.Ordinal);
        Assert.Contains("wireRigheArea();", ui, StringComparison.Ordinal);   // definita E chiamata

        // 🔴 Spegnere una chip chiude anche il dettaglio: senza, resterebbe in tabella una cella di prosa
        // staccata dalla riga che dice di quale area parli.
        Assert.Contains("data-areamore=", aor, StringComparison.Ordinal);

        var c = Render();
        Assert.NotNull(c.Find("button.milarea-exp[aria-expanded='false']"));
        Assert.NotNull(c.Find("tr.milarea-more[data-areamore]"));
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

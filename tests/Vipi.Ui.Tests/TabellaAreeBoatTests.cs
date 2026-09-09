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
/// <para>Sotto «Aree di lavoro» ha quattro colonne — nome, limiti, attività, nota — e i quindici gettoni.
/// Sotto «Bassa quota (BOAT)» ne ha tre: lì l'attività sarebbe sempre «LOW LEVEL», e una colonna che dice
/// sempre la stessa cosa non aggiunge niente.</para>
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
                                                       IReadOnlyList<AccSpecialAreaView>? aree = null) =>
        RenderComponent<MilWorkingAreas>(p =>
        {
            p.Add(x => x.Areas, aree ?? Aree);
            p.Add(x => x.Editing, editing);
            if (attivita is { } a) p.Add(x => x.MostraAttivita, a);
        });

    [Fact]
    public void Le_aree_di_lavoro_hanno_QUATTRO_colonne_senza_chiedere_niente()
    {
        // ⚠️ Il default: chi monta la tabella sotto «Aree di lavoro» non passa il parametro, e la colonna
        // dell'attività deve esserci lo stesso.
        var c = Render();

        Assert.Equal(4, c.FindAll("thead th").Count);
        Assert.Single(c.FindAll("th.c-act2"));
        Assert.Equal(Aree.Length, c.FindAll("tbody td.c-act2").Count);
    }

    [Fact]
    public void Le_aree_BOAT_ne_hanno_TRE_intestazione_e_celle_insieme()
    {
        var c = Render(attivita: false);

        Assert.Equal(3, c.FindAll("thead th").Count);
        Assert.Empty(c.FindAll("th.c-act2"));

        // ⚠️ Le CELLE e non solo l'intestazione: una tabella con tre `th` e quattro `td` per riga si
        // disallinea in silenzio, e le note finirebbero sotto il titolo sbagliato.
        Assert.Empty(c.FindAll("tbody td.c-act2"));
        Assert.Equal(Aree.Length, c.FindAll("tbody td.c-note").Count);
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

        Assert.Equal("4", Render(aree: vuoto).Find("tbody td.muted").GetAttribute("colspan"));
        Assert.Equal("3", Render(attivita: false, aree: vuoto).Find("tbody td.muted").GetAttribute("colspan"));
    }
}

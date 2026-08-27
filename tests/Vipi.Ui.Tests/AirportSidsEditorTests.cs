using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'editor delle SID, montato per davvero — cosa che prima non si poteva fare, perché erano 252 righe di
/// marcatura dentro una pagina da duemila (doc 14 §3g).
///
/// <para>
/// ⚠️ La prova che conta è la prima: <b>ogni</b> modifica dice alla pagina che c'è qualcosa da salvare. Il
/// blocco delle SID manuali marcava la sezione <c>"SID"</c> — con la S maiuscola — mentre la chiave vera è
/// <c>"sids"</c>: una stringa che non corrisponde a nessun caso dello smistamento del salvataggio, quindi
/// «Salva tutto» la saltava <b>in silenzio</b> e restava per sempre fra le non salvate. Si è vista spostando
/// il codice, e questa prova impedisce che tornino a divergere.
/// </para>
/// </summary>
public class AirportSidsEditorTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AirportSidsEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddLogging();
    }

    private static ImportedSidEdit Imp(int id, string fix, string nome, string? pista, bool daRivedere = false) =>
        new() { Id = id, Fix = fix, Name = nome, Runway = pista, NeedsReview = daRivedere };

    private static SidEdit Man(string fix, string nome, string? pista = null) =>
        new() { Fix = fix, Name = nome, Runway = pista };

    private IRenderedComponent<AirportSidsEditor> Rendi(
        List<ImportedSidEdit>? importate = null, List<SidEdit>? manuali = null,
        Action? suCambio = null, Action<int>? suNonSalvate = null) =>
        RenderComponent<AirportSidsEditor>(p => p
            .Add(x => x.Imported, importate ?? new List<ImportedSidEdit>())
            .Add(x => x.Manual, manuali ?? new List<SidEdit>())
            .Add(x => x.RunwayIdents, new[] { "16L", "16R", "34L", "34R" })
            .Add(x => x.CanEdit, true)
            .Add(x => x.PersistImported, _ => Task.CompletedTask)
            .Add(x => x.RunGuarded, async (Func<Task> a) => { await a(); return true; })
            .Add(x => x.OnChanged, () => suCambio?.Invoke())
            .Add(x => x.OnDirtyCountChanged, (int n) => suNonSalvate?.Invoke(n)));

    // ---------------------------------------------------------------------------------------------------
    // Il difetto trovato spostando il codice
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Modificare_una_SID_manuale_dice_alla_pagina_che_c_e_da_salvare()
    {
        var avvisata = false;
        var cut = Rendi(manuali: new List<SidEdit> { Man("ALAXI", "ALAXI 5A", "16L") },
                        suCambio: () => avvisata = true);

        // ⚠️ Si scatena il cambio sul BLOCCO, che è l'elemento che porta il gestore: in un browser vero
        // l'evento della cella ci rimbalza sopra, ma bUnit il rimbalzo non lo simula. Quel che questa prova
        // deve guardare è il COLLEGAMENTO — che il blocco delle manuali chiami la stessa notifica di tutto il
        // resto — ed è esattamente ciò che era rotto.
        var blocchi = cut.FindAll("div.block").ToList();
        blocchi.Last().Change("ELKAP");

        Assert.True(avvisata, "una modifica alle SID manuali non ha segnalato la sezione da salvare");
    }

    [Fact]
    public void Aggiungere_una_SID_manuale_la_mette_in_elenco_e_avvisa()
    {
        var righe = new List<SidEdit>();
        var avvisata = false;
        var cut = Rendi(manuali: righe, suCambio: () => avvisata = true);

        cut.FindAll("button").First(b => b.TextContent.Contains("SID")).Click();

        Assert.Single(righe);
        Assert.True(avvisata);
    }

    // ---------------------------------------------------------------------------------------------------
    // Quel che l'editor mostra
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Senza_importate_il_blocco_delle_importate_non_c_e()
    {
        // Uno scalo che non ha mai importato dal sectorfile non deve vedere una tabella vuota con i filtri.
        var cut = Rendi(manuali: new List<SidEdit> { Man("ALAXI", "ALAXI 5A") });

        Assert.DoesNotContain("Ape_SidImportedTitle", cut.Markup);
        Assert.Contains("Ape_SidManualTitle", cut.Markup);
    }

    [Fact]
    public void Con_le_importate_ci_sono_le_due_tabelle()
    {
        var cut = Rendi(
            importate: new List<ImportedSidEdit> { Imp(1, "ALAXI", "ALAXI 5A", "16L") },
            manuali: new List<SidEdit> { Man("TAQ", "TAQ 1X") });

        Assert.Contains("Ape_SidImportedTitle", cut.Markup);
        Assert.Contains("Ape_SidManualTitle", cut.Markup);
        Assert.Contains("ALAXI 5A", cut.Markup);
        Assert.Contains("TAQ 1X", cut.Markup);
    }

    [Fact]
    public void I_chip_delle_piste_escono_dalle_SID_importate()
    {
        var cut = Rendi(importate: new List<ImportedSidEdit>
        {
            Imp(1, "ALAXI", "ALAXI 5A", "16L"),
            Imp(2, "ELKAP", "ELKAP 3B", "34R"),
            Imp(3, "TAQ", "TAQ 1X", "16L"),
        });

        // Le due piste presenti, non le quattro dello scalo: i chip dicono dove ci sono davvero SID.
        var testo = cut.Markup;
        Assert.Contains("16L", testo);
        Assert.Contains("34R", testo);
    }

    // ---------------------------------------------------------------------------------------------------
    // Lo stato di vista è del componente, e si azzera quando i dati cambiano
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Quando_la_pagina_ricarica_la_selezione_si_azzera()
    {
        // ⚠️ Lo faceva LoadAsync della pagina, che quello stato lo possedeva. Ora lo possiede il componente,
        // e se ne accorge da sé: senza, la selezione parlerebbe di righe che non esistono più.
        var conteggi = new List<int>();
        var prime = new List<ImportedSidEdit> { Imp(1, "ALAXI", "ALAXI 5A", "16L") };
        var cut = Rendi(importate: prime, suNonSalvate: conteggi.Add);

        // Nuovi buffer = ricaricamento della pagina.
        cut.SetParametersAndRender(p => p
            .Add(x => x.Imported, new List<ImportedSidEdit> { Imp(9, "OST", "OST 2C", "34R") }));

        // L'ultimo conteggio comunicato è zero: non restano righe «non salvate» di un elenco che non c'è più.
        Assert.Equal(0, conteggi.Last());
    }
}

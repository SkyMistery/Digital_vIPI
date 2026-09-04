using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'editor delle piste, montato per davvero.
///
/// <para>⚠️ La prova che conta è quella sulla <b>✕ a sorgente bloccata</b>. Il 4 settembre 2026 IVAO ha
/// ri-denominato Rimini (13/31 → 12/30, deriva magnetica) e LIPR si è ritrovato quattro piste: le due morte
/// portavano TORA/LDA scritti a mano, quindi il merge le teneva — giustamente — ma la ✕ compariva solo a
/// policy di import spenta. E la policy è GLOBALE: per ripulire un aeroporto bisognava sbloccarli tutti.
/// L'amministratore era chiuso fuori dal suo stesso archivio.</para>
/// </summary>
public class AirportRunwaysEditorTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join("|", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AirportRunwaysEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddLogging();
    }

    private static RwEdit Rw(string ident, string? tora = null) =>
        new() { Ident = ident, LengthM = 2962, Tora = tora };

    private IRenderedComponent<AirportRunwaysEditor> Rendi(
        List<RwEdit> righe, bool bloccate = true, IReadOnlyList<string>? orfane = null,
        Action? suCambio = null) =>
        RenderComponent<AirportRunwaysEditor>(p => p
            .Add(x => x.Rows, righe)
            .Add(x => x.SourceLocked, bloccate)
            .Add(x => x.MissingFromSource, orfane ?? Array.Empty<string>())
            .Add(x => x.Editing, true)
            .Add(x => x.RowsChanged, () => suCambio?.Invoke()));

    /// <summary>⚠️ Il caso LIPR: a sorgente bloccata la ✕ c'è comunque, una per riga.</summary>
    [Fact]
    public void La_croce_c_e_anche_a_sorgente_bloccata()
    {
        var c = Rendi(new List<RwEdit> { Rw("12"), Rw("30"), Rw("13", "2800") });

        Assert.Equal(3, c.FindAll("tbody button").Count);
    }

    /// <summary>Una pista NUOVA invece non si inventa: «+ Pista» resta sotto chiave, perché un'aggiunta a
    /// mano non si ripara da sé mentre una rimozione sì (il re-import successivo la rimette).</summary>
    [Fact]
    public void Aggiungere_resta_sotto_chiave_a_sorgente_bloccata()
    {
        var bloccato = Rendi(new List<RwEdit> { Rw("12") });
        var libero = Rendi(new List<RwEdit> { Rw("12") }, bloccate: false);

        // A sorgente bloccata c'è la sola ✕ della riga; a sorgente libera si aggiunge «+ Pista».
        // ⚠️ Il tasto «Salva piste» non c'è più da nessuna delle due parti: ogni gesto scrive.
        Assert.Single(bloccato.FindAll("button"));
        Assert.Equal(2, libero.FindAll("button").Count);
    }

    /// <summary>La ✕ toglie la riga e avvisa la pagina: è l'avviso che fa scattare il salvataggio.</summary>
    [Fact]
    public void La_croce_toglie_la_riga_e_avvisa()
    {
        var righe = new List<RwEdit> { Rw("12"), Rw("13", "2800") };
        var avvisi = 0;
        var c = Rendi(righe, suCambio: () => avvisi++);

        c.FindAll("tbody button").ToList()[1].Click();

        Assert.Equal(new[] { "12" }, righe.Select(r => r.Ident).ToArray());
        Assert.Equal(1, avvisi);
    }

    /// <summary>
    /// ⚠️ <b>Scrivere in una cella deve avvisare la pagina.</b> È l'avviso che fa scattare
    /// l'auto-salvataggio, quindi se muore muoiono i dati — ed era morto: fino al 4 settembre 2026 il
    /// gestore stava su un <c>@onchange</c> del <c>div</c> che avvolge la tabella, e non veniva <b>mai</b>
    /// chiamato. Misurato sul pacchetto pubblicato: il valore entrava nel modello (quindi il <c>change</c>
    /// del DOM era partito e Blazor l'aveva gestito sull'input), ma «Salva tutto» restava a (0) e nel
    /// registro del database non compariva nessuna scrittura. Ora l'aggancio è su ogni input
    /// (<c>@bind:after</c>).
    /// </summary>
    [Theory]
    [InlineData(0)]   // TORA
    [InlineData(1)]   // LDA
    [InlineData(2)]   // APP procedures
    [InlineData(3)]   // Patterns
    [InlineData(4)]   // Circling
    public void Scrivere_in_una_cella_editoriale_avvisa_la_pagina(int colonna)
    {
        var righe = new List<RwEdit> { Rw("12") };
        var avvisi = 0;
        var c = Rendi(righe, suCambio: () => avvisi++);

        c.FindAll("tbody tr td input").ToList()[colonna].Change("1700");

        Assert.Equal(1, avvisi);
    }

    /// <summary>E il valore arriva davvero nel modello: l'avviso senza il dato non salverebbe niente.</summary>
    [Fact]
    public void Scrivere_in_TORA_porta_il_valore_nel_modello()
    {
        var righe = new List<RwEdit> { Rw("12") };
        var c = Rendi(righe);

        c.FindAll("tbody tr td input").ToList()[0].Change("1700");

        Assert.Equal("1700", righe[0].Tora);
    }

    /// <summary>L'ident resta in sola lettura a sorgente bloccata: la ✕ non ha allentato quello. ⚠️ Non è
    /// più un campo spento ma un campo che non c'è: dal 4 settembre 2026 il cancello è un RAMO di render, come
    /// negli altri editor, e quel che non si può scrivere si legge e basta.</summary>
    [Fact]
    public void L_ident_resta_in_sola_lettura_a_sorgente_bloccata()
    {
        var c = Rendi(new List<RwEdit> { Rw("12") });

        var prima = c.FindAll("tbody tr td").First();
        Assert.Empty(prima.QuerySelectorAll("input"));
        Assert.Contains("12", prima.TextContent);
    }

    /// <summary>E senza il lock non si scrive proprio niente: nessun campo, nessun tasto.</summary>
    [Fact]
    public void Senza_lock_la_tabella_e_sola_lettura()
    {
        var c = RenderComponent<AirportRunwaysEditor>(p => p
            .Add(x => x.Rows, new List<RwEdit> { Rw("12", "2800") })
            .Add(x => x.SourceLocked, false)
            .Add(x => x.Editing, false));

        Assert.Empty(c.FindAll("input"));
        Assert.Empty(c.FindAll("button"));
        Assert.Contains("2800", c.Markup);
    }

    /// <summary>Le orfane si dichiarano: un callout in cima e un contrassegno sulla riga giusta, così chi
    /// apre l'aeroporto sa quali piste la sorgente non nomina più e perché sono ancora lì.</summary>
    [Fact]
    public void Le_orfane_si_dichiarano_in_cima_e_sulla_riga()
    {
        var c = Rendi(new List<RwEdit> { Rw("12"), Rw("13", "2800") }, orfane: new[] { "13" });

        Assert.Contains("Ape_RwMissingFromSource:13", c.Markup);
        var righe = c.FindAll("tbody tr").ToList();
        Assert.DoesNotContain("Ape_RwMissingRow", righe[0].InnerHtml);
        Assert.Contains("Ape_RwMissingRow", righe[1].InnerHtml);
    }

    /// <summary>Nessuna orfana, nessun avviso: il callout non deve comparire a vuoto.</summary>
    [Fact]
    public void Senza_orfane_non_c_e_nessun_avviso()
    {
        var c = Rendi(new List<RwEdit> { Rw("12") });

        Assert.DoesNotContain("Ape_RwMissingFromSource", c.Markup);
        Assert.DoesNotContain("Ape_RwMissingRow", c.Markup);
    }
}

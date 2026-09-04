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
            .Add(x => x.CanEdit, true)
            .Add(x => x.OnChanged, () => suCambio?.Invoke()));

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

        // A sorgente bloccata: la ✕ della riga + «Salva piste». A sorgente libera si aggiunge «+ Pista».
        Assert.Equal(2, bloccato.FindAll("button").Count);
        Assert.Equal(3, libero.FindAll("button").Count);
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
    [InlineData(1)]   // TORA
    [InlineData(2)]   // LDA
    [InlineData(3)]   // APP procedures
    [InlineData(4)]   // Patterns
    [InlineData(5)]   // Circling
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

        c.FindAll("tbody tr td input").ToList()[1].Change("1700");

        Assert.Equal("1700", righe[0].Tora);
    }

    /// <summary>L'ident resta in sola lettura a sorgente bloccata: la ✕ non ha allentato quello.</summary>
    [Fact]
    public void L_ident_resta_in_sola_lettura_a_sorgente_bloccata()
    {
        var c = Rendi(new List<RwEdit> { Rw("12") });

        Assert.True(c.Find("tbody tr td input").HasAttribute("disabled"));
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

using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La sezione «Radioassistenze» del vSOP militare (carta <c>2026-08-27-vsop-militari.md</c> §12).
///
/// <para>⚠️ Il punto della sezione è la <b>divisione del lavoro</b>: il documento dice <i>quali</i> righe cita
/// e in <i>che ordine</i>, l'anagrafica di divisione dice <i>quanto valgono</i>. Questi test presidiano il
/// lato documento; i valori li presidia <c>AnagraficaRadioassistenzeTests</c>.</para>
/// </summary>
public class MilNavaidsSezioneTests
{
    // ---- Il payload ------------------------------------------------------------------------------------

    /// <summary>⚠️ L'ordine è CONTENUTO, non presentazione: in un SOP le radioassistenze si elencano come le
    /// vuole chi scrive, e ordinarle per codice butterebbe via una scelta editoriale.</summary>
    [Fact]
    public void Il_payload_conserva_l_ordine()
    {
        var json = MilNavaidsPayload.Scrivi(new[]
        {
            new NavaidKey("MNL", "VHF", "99Y"), new NavaidKey("AEA", "VHF", "54Y"), new NavaidKey("AVI", "NDB", null),
        });

        Assert.Equal(new[] { "MNL", "AEA", "AVI" }, MilNavaidsPayload.Leggi(json).Select(k => k.Code));
    }

    /// <summary>I tre pezzi dell'identità si normalizzano <b>alla scrittura</b>: <c>mnl</c> e <c>MNL</c> non
    /// sono due radioassistenze, e un payload che le distingue troverebbe l'anagrafica vuota.</summary>
    [Fact]
    public void Il_payload_normalizza_le_identita()
    {
        var json = MilNavaidsPayload.Scrivi(new[] { new NavaidKey(" mnl ", "vhf", " 99y ") });

        var k = Assert.Single(MilNavaidsPayload.Leggi(json));
        Assert.Equal("MNL", k.Code);
        Assert.Equal("VHF", k.Kind);
        Assert.Equal("99Y", k.Channel);
    }

    /// <summary>Nessuna riga ⇒ <c>null</c>: null e «lista vuota» devono essere la stessa cosa in archivio, o
    /// «non c'è niente» avrebbe due forme.</summary>
    [Fact]
    public void Nessuna_riga_non_si_salva_come_lista_vuota()
    {
        Assert.Null(MilNavaidsPayload.Scrivi(Array.Empty<NavaidKey>()));
        Assert.Null(MilNavaidsPayload.Scrivi(new[] { new NavaidKey("", "VHF", null), new NavaidKey("MNL", "  ", null) }));
    }

    /// <summary>Un payload illeggibile è una tabella vuota, non un errore in faccia a chi legge: un documento
    /// pubblicato non deve poter esplodere per un blocco scritto male.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    [InlineData("""{"variant":"milnavaids"}""")]
    [InlineData("""{"variant":"milnavaids","rows":[{"code":"","kind":"VOR"}]}""")]
    public void Un_payload_illeggibile_da_zero_righe(string? json) =>
        Assert.Empty(MilNavaidsPayload.Leggi(json));

    /// <summary>Il payload porta il discriminatore come gli altri blocchi con <c>variant</c>: è come si
    /// riconosce un blocco di struttura da uno di prosa guardando il database.</summary>
    [Fact]
    public void Il_payload_si_riconosce_dalla_variante()
    {
        var json = MilNavaidsPayload.Scrivi(new[] { new NavaidKey("MNL", "VHF", "99Y") });

        Assert.Contains("\"variant\":\"milnavaids\"", json);
    }

    // ---- Il catalogo -----------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>Derivata</b>, e non è un dettaglio di classificazione: solo le derivate le cattura
    /// <c>FrozenSectionScan</c>, quindi solo così una release <i>fotografa</i> la tabella. Senza, una
    /// frequenza corretta oggi cambierebbe da sola un SOP pubblicato al ciclo scorso.
    /// </summary>
    [Fact]
    public void La_sezione_e_derivata_e_quindi_si_congela()
    {
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf("navaids"));
        Assert.False(SectionCatalog.IsAlwaysLive("navaids"));
        Assert.True(SectionCatalog.IsRenderModeToggleable("navaids"));
    }

    /// <summary>
    /// Scheda <b>e</b> blocchi: la tabella la disegna la pagina, e sotto restano i paragrafi che il
    /// caricatore dei SOP ha già scritto. ⚠️ Con <c>Host</c> puro quella prosa sparirebbe dallo schermo.
    /// </summary>
    [Fact]
    public void La_sezione_tiene_anche_la_prosa_dei_SOP()
    {
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "navaids"));
        Assert.True(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, "navaids"));
    }

    /// <summary>⚠️ È una FIGLIA di «Dati generali», ed è la ragione per cui il payload ha dovuto imparare a
    /// scendere: cercarla fra le sole radici non la trova.</summary>
    [Fact]
    public void La_sezione_e_annidata()
    {
        Assert.DoesNotContain(SectionCatalog.For(SectionProfile.AirportMil), d => d.Key == "navaids");
        Assert.NotNull(SectionCatalog.Find(SectionProfile.AirportMil, "navaids"));
    }
}

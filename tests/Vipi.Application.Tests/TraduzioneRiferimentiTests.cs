using System.Text.RegularExpressions;
using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// I riferimenti del nostro formato davanti al motore di traduzione (§A73, slice 2): una SID citata
/// (<c>[[SID LIRF OST1E]]</c>) e un link a un allegato (<c>[testo](allegato:slug)</c>). Sono sintassi, non
/// parole: il motore non li deve vedere, e una persona che scrive una resa a mano non li deve cambiare.
/// </summary>
public class TraduzioneRiferimentiTests
{
    private static readonly TextProtector Protettore = new();

    /// <summary>Il testo che il motore riceverebbe davvero: i segnaposto tolti.</summary>
    private static string Fuori(string protetto) =>
        Regex.Replace(protetto, @"<x id=""\d+""\s*/>|<x id=""\d+""\s*>[^<]*</x>", "@");

    // ---- SID citate ------------------------------------------------------------------------------------

    [Fact]
    public void Una_SID_citata_non_arriva_al_motore()
    {
        var p = Protettore.Protect("Expect [[SID LIRF OST1E]] after departure.");

        Assert.Equal("Expect @ after departure.", Fuori(p.Text));
        // ⚠️ Tag VUOTO: col valore dentro il motore vedrebbe le parentesi, e un «[[» toccato butterebbe la frase.
        Assert.DoesNotContain("SID", p.Text);
        Assert.True(p.Safe);
    }

    /// <summary>
    /// 🔴 Il gemello degli ARRIVI (§A80). La guardia veloce del protettore cercava «[[SID » cablato: la regex
    /// riconosceva già <c>[[STAR …]]</c>, la guardia no, e un riferimento d'arrivo partiva verso il motore —
    /// un difetto muto, che si vede solo leggendo una traduzione.
    /// </summary>
    [Fact]
    public void Una_STAR_citata_non_arriva_al_motore()
    {
        var p = Protettore.Protect("Expect [[STAR LIRF ELKA3A]] inbound.");

        Assert.Equal("Expect @ inbound.", Fuori(p.Text));
        Assert.DoesNotContain("STAR", p.Text);
        Assert.True(p.Safe);

        var dalMotore = p.Text.Replace("Expect", "Prevedere").Replace("inbound", "in arrivo");
        Assert.True(TextProtector.TryRestore(dalMotore, p, out var tornato));
        Assert.Equal("Prevedere [[STAR LIRF ELKA3A]] in arrivo.", tornato);
    }

    [Fact]
    public void Il_motore_traduce_intorno_e_il_riferimento_torna_intatto()
    {
        var p = Protettore.Protect("Expect [[SID LIRF OST1E]] after departure.");
        var dalMotore = p.Text.Replace("Expect", "Prevedere").Replace("after departure", "dopo il decollo");

        Assert.True(TextProtector.TryRestore(dalMotore, p, out var tornato));
        Assert.Equal("Prevedere [[SID LIRF OST1E]] dopo il decollo.", tornato);
    }

    /// <summary>Una cella che è solo un riferimento non parte nemmeno: non c'è niente da tradurre.</summary>
    [Fact]
    public void Una_cella_che_e_solo_un_riferimento_non_parte()
    {
        var p = Protettore.Protect("[[SID LIBV CDC6A]]");
        Assert.True(TextProtector.SoloSegnaposti(p.Text));
    }

    [Fact]
    public void Piu_riferimenti_e_identificatori_nella_stessa_frase()
    {
        var p = Protettore.Protect("Contact LIRF_TWR on 118.700, then [[SID LIRF OST1E]] or [[SID LIRF RATI1D]].");
        Assert.Equal("Contact @ on @, then @ or @.", Fuori(p.Text));
        Assert.True(TextProtector.TryRestore(p.Text, p, out var tornato));
        Assert.Equal("Contact LIRF_TWR on 118.700, then [[SID LIRF OST1E]] or [[SID LIRF RATI1D]].", tornato);
    }

    // ---- link agli allegati ------------------------------------------------------------------------------

    /// <summary>⚠️ Fino al 18 settembre 2026 il link partiva così com'era: il motore poteva tradurre
    /// «allegato» e il link diventava testo in silenzio. Il TESTO del link invece si traduce.</summary>
    [Fact]
    public void Del_link_a_un_allegato_si_traduce_il_testo_e_non_la_destinazione()
    {
        var p = Protettore.Protect("Vedi la [lettera di accordo](allegato:loa-lirr-lfmm) per i dettagli.");

        Assert.Equal("Vedi la @lettera di accordo@ per i dettagli.", Fuori(p.Text));
        Assert.DoesNotContain("allegato", Fuori(p.Text));

        var dalMotore = p.Text.Replace("Vedi la", "See the").Replace("lettera di accordo", "letter of agreement")
                               .Replace("per i dettagli", "for details");
        Assert.True(TextProtector.TryRestore(dalMotore, p, out var tornato));
        Assert.Equal("See the [letter of agreement](allegato:loa-lirr-lfmm) for details.", tornato);
    }

    /// <summary>Sei cifre dentro uno slug somigliano a un VID: la regola sui VID non deve spezzare il link.</summary>
    [Fact]
    public void Uno_slug_con_sei_cifre_non_viene_spezzato_dalla_regola_dei_VID()
    {
        var p = Protettore.Protect("Vedi [la carta](allegato:carta-202609) prima.");
        // Senza la regola sui riferimenti partirebbe «[la carta](allegato:carta-@)»: il VID-sembrante tolto
        // dallo slug e il resto del link in mano al motore.
        Assert.Equal("Vedi @la carta@ prima.", Fuori(p.Text));
        Assert.True(TextProtector.TryRestore(p.Text, p, out var tornato));
        Assert.Equal("Vedi [la carta](allegato:carta-202609) prima.", tornato);
        Assert.True(p.Safe);
    }

    // ---- la resa scritta a mano --------------------------------------------------------------------------

    [Theory]
    [InlineData("Expect [[SID LIRF OST1E]].", "Prevedere [[SID LIRF OST1E]].", true)]
    [InlineData("Expect [[SID LIRF OST1E]].", "Prevedere OST1E.", false)]
    [InlineData("Expect [[SID LIRF OST1E]].", "Prevedere [[SID LIRF OST2E]].", false)]
    [InlineData("A [[SID LIRF OST1E]] e [[SID LIRA TIBER6A]].", "[[SID LIRA TIBER6A]] and [[SID LIRF OST1E]].", true)]
    [InlineData("Vedi [LoA](allegato:loa-a).", "See [the LoA](allegato:loa-a).", true)]
    [InlineData("Vedi [LoA](allegato:loa-a).", "See [LoA](attachment:loa-a).", false)]
    [InlineData("Niente qui.", "Nothing here.", true)]
    [InlineData("Niente qui.", "Nothing [[SID LIRF OST1E]].", false)]
    public void Una_resa_a_mano_deve_tenere_gli_stessi_riferimenti(string sorgente, string resa, bool ok)
    {
        Assert.Equal(ok, TextProtector.StessiRiferimenti(sorgente, resa));
    }

    [Fact]
    public void Una_voce_di_glossario_con_un_riferimento_si_rifiuta()
    {
        Assert.True(TextProtector.ContieneIdentificatori("seguire la [[SID LIRF OST1E]]"));
        Assert.True(TextProtector.ContieneIdentificatori("vedi [la carta](allegato:carta-a)"));
    }
}

using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le sezioni <b>in comune</b> fra i documenti di un'unione, e che cosa nascondere (richiesta del
/// committente, 7 settembre 2026): unendo la vIPI d'aeroporto di uno scalo e il suo vSOP militare la pagina
/// ripete METAR, frequenze, piste, quote di transizione.
///
/// <para>⚠️ Il difetto che questi test cercano non dà errore: una chiave di troppo nell'elenco nasconde una
/// sezione che qualcuno voleva vedere, e a dirlo non c'è nessuno — la sezione semplicemente non c'è più
/// nella pagina pubblicata.</para>
/// </summary>
public class SezioniComuniTests
{
    private const int Vipi = 26;
    private const int Vsop = 3;

    [Fact]
    public void In_comune_e_la_CHIAVE_non_il_titolo()
    {
        // ⚠️ Nel vSOP le frequenze si chiamano «Frequenze ATC/CRC» e nella vIPI «Frequenze»: lo stesso dato
        // con due titoli. Confrontare i titoli non troverebbe niente proprio nel caso per cui esiste.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "frequencies", "Frequenze"))),
            (Vsop, Sezioni(Sez(2, "frequencies", "Frequenze ATC/CRC"))),
        });

        var c = Assert.Single(comuni);
        Assert.Equal("frequencies", c.Chiave);
        Assert.Equal(new[] { 1, 2 }, c.Presenze.Select(p => p.SectionId));
    }

    [Fact]
    public void Si_guarda_anche_DENTRO_le_sezioni()
    {
        // 🔴 Nel vSOP militare frequenze, piste e quote di transizione sono FIGLIE di «Dati generali»,
        // nella vIPI d'aeroporto stanno in cima. Un confronto sui soli primi livelli darebbe «nessuna
        // sezione in comune» sul caso vero.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "frequencies", "Frequenze"), Sez(2, "runways", "Piste"))),
            (Vsop, Sezioni(Sez(10, "generaldata", "Dati generali",
                                Sez(11, "frequencies", "Frequenze ATC/CRC"), Sez(12, "runways", "Piste")))),
        });

        Assert.Equal(new[] { "frequencies", "runways" }, comuni.Select(c => c.Chiave));
    }

    [Fact]
    public void Le_sezioni_LIBERE_non_sono_mai_in_comune()
    {
        // La loro chiave nasce unica: due sezioni scritte a mano non si somigliano mai, nemmeno quando si
        // chiamano uguale. È il confronto per TITOLO che il committente non ha chiesto — e che sarebbe un
        // indovinello, non un fatto.
        var libera1 = SectionKeys.NewCustom();
        var libera2 = SectionKeys.NewCustom();

        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, libera1, "LVP"))),
            (Vsop, Sezioni(Sez(2, libera2, "LVP"))),
        });

        Assert.Empty(comuni);
    }

    [Fact]
    public void Una_chiave_che_ha_UN_documento_solo_non_e_in_comune()
    {
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "sids", "SID"), Sez(2, "weather", "METAR & TAF"))),
            (Vsop, Sezioni(Sez(3, "weather", "METAR & TAF"), Sez(4, "parkings", "Parcheggi"))),
        });

        Assert.Equal(new[] { "weather" }, comuni.Select(c => c.Chiave));
    }

    [Fact]
    public void La_VALIDITA_e_in_elenco_ma_non_spuntata()
    {
        // ⚠️ Comune per chiave, non per significato: dice ciclo AIRAC e release DI QUEL documento, e in
        // un'unione sono due. Chi la vuole nascondere può, ma deve dirlo.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"), Sez(2, "validity", "Validità e revisione"))),
            (Vsop, Sezioni(Sez(3, "weather", "METAR & TAF"), Sez(4, "validity", "Validità e revisione"))),
        });

        Assert.True(comuni.Single(c => c.Chiave == "weather").Proposta);
        Assert.False(comuni.Single(c => c.Chiave == SezioniComuni.ChiaveValidita).Proposta);
    }

    // ---- il piano: sparisce da chi si SPUNTA -------------------------------------------------------

    [Fact]
    public void Sparisce_dai_documenti_SPUNTATI_e_resta_negli_altri()
    {
        // La polarita' chiesta dal committente: spunto la vIPI, spariscono quelle della vIPI.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"))),
            (Vsop, Sezioni(Sez(2, "weather", "METAR & TAF"))),
        });

        var piano = SezioniComuni.Piano(comuni, new[] { "weather" }, new[] { Vipi });

        // La sezione della vIPI si nasconde; quella del vSOP era gia' visibile, quindi non si tocca.
        Assert.Equal(new[] { (1, true) }, piano);
    }

    [Fact]
    public void Cambiare_idea_RIMETTE_quella_dell_altro()
    {
        // 🔴 Senza questo, la seconda scelta nasconderebbe l'altro senza rimettere il primo: due copie
        // nascoste, e la pagina unita senza METAR.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF", nascosta: true))),
            (Vsop, Sezioni(Sez(2, "weather", "METAR & TAF"))),
        });

        var piano = SezioniComuni.Piano(comuni, new[] { "weather" }, new[] { Vsop });

        Assert.Equal(new[] { (1, false), (2, true) }, piano);
    }

    [Fact]
    public void Le_chiavi_NON_scelte_non_si_toccano()
    {
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"), Sez(2, "validity", "Validità e revisione"))),
            (Vsop, Sezioni(Sez(3, "weather", "METAR & TAF"), Sez(4, "validity", "Validità e revisione"))),
        });

        var piano = SezioniComuni.Piano(comuni, new[] { "weather" }, new[] { Vsop });

        Assert.Equal(new[] { 3 }, piano.Select(x => x.SectionId));
    }

    [Fact]
    public void Un_piano_VUOTO_e_una_risposta()
    {
        // Premere due volte non deve «nascondere sei sezioni» la seconda volta: il conto dice quante ne ha
        // cambiate DAVVERO.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"))),
            (Vsop, Sezioni(Sez(2, "weather", "METAR & TAF", nascosta: true))),
        });

        Assert.Empty(SezioniComuni.Piano(comuni, new[] { "weather" }, new[] { Vsop }));
    }

    [Fact]
    public void Spuntare_TUTTI_i_documenti_si_puo_ma_si_deve_dire()
    {
        // Legittimo — «quel dato qui non lo vogliamo» — ma la sezione sparisce dalla pagina unita per
        // intero: la scheda lo avvisa invece di vietarlo.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"))),
            (Vsop, Sezioni(Sez(2, "weather", "METAR & TAF"))),
        });

        Assert.True(SezioniComuni.SparisceDaTutti(comuni, new[] { "weather" }, new[] { Vipi, Vsop }));
        Assert.False(SezioniComuni.SparisceDaTutti(comuni, new[] { "weather" }, new[] { Vipi }));
    }

    // ---- da dove nascondere lo dice lo STATO ---------------------------------------------------------

    [Fact]
    public void A_unione_appena_nata_si_propone_TUTTI_TRANNE_l_ospite()
    {
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF"))),
            (Vsop, Sezioni(Sez(2, "weather", "METAR & TAF"))),
        });

        // Nessuno ha nascosto niente: la pagina unita si legge a casa dell'ospite, che e' il primo.
        Assert.Equal(new[] { Vsop }, SezioniComuni.DoveNascondere(comuni, new[] { Vipi, Vsop }));
    }

    [Fact]
    public void Riaprendo_la_scheda_si_propone_CHI_LE_HA_GIA_NASCOSTE()
    {
        // 🔴 Il difetto trovato a schermo il 7 settembre 2026: con una proposta fissa, chi riapriva e
        // premeva senza guardare RIBALTAVA la scelta di prima — «22 sezioni cambiate» invece di nessuna.
        var comuni = SezioniComuni.Di(new[]
        {
            (Vipi, Sezioni(Sez(1, "weather", "METAR & TAF", nascosta: true),
                           Sez(2, "runways", "Piste", nascosta: true))),
            (Vsop, Sezioni(Sez(3, "weather", "METAR & TAF"), Sez(4, "runways", "Piste"))),
        });

        var dove = SezioniComuni.DoveNascondere(comuni, new[] { Vipi, Vsop });

        Assert.Equal(new[] { Vipi }, dove);
        // E con quella proposta, premere di nuovo non cambia niente.
        Assert.Empty(SezioniComuni.Piano(comuni, new[] { "weather", "runways" }, dove));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static IReadOnlyList<EditableSection> Sezioni(params EditableSection[] s) => s;

    private static EditableSection Sez(int id, string chiave, string titolo, params EditableSection[] figlie) =>
        Sez(id, chiave, titolo, nascosta: false, figlie);

    private static EditableSection Sez(int id, string chiave, string titolo, bool nascosta,
                                       params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = chiave,
        Depth = 0,
        Order = id,
        IsHidden = nascosta,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };
}

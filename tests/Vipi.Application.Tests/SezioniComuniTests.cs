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

    // ---- CHI si confronta (9 settembre 2026) ------------------------------------------------------
    //
    // 🔴 Segnalato dal committente unendo un TERZO documento (LIBV_APP) alla vIPI e al vSOP di Gioia
    // del Colle: la scheda si apriva e proponeva di nascondere sezioni che NON sono ripetizioni.
    // ⚠️ La stessa chiave non vuol dire lo stesso dato: un APP e la vIPI d'aeroporto condividono
    // `frequencies`, `operationaltechnique` e `validity`, ma le frequenze di un APP sono quelle
    // dell'AVVICINAMENTO e quelle dell'aeroporto sono del CAMPO. Nasconderne una perde contenuto vero.

    [Fact]
    public void Un_APP_non_partecipa_al_confronto()
    {
        var confrontabili = SezioniComuni.Confrontabili(new[]
        {
            (Vipi, ReleaseTargetType.Airport),
            (Vsop, ReleaseTargetType.AirportMil),
            (99, ReleaseTargetType.App),
        });

        Assert.Equal(new[] { Vipi, Vsop }, confrontabili);
    }

    /// <summary>Aeroporto + APP soli: non resta nessuna coppia che descriva lo stesso luogo, quindi
    /// <b>niente</b> da confrontare — e la scheda non ha niente da chiedere.</summary>
    [Fact]
    public void Aeroporto_e_APP_da_soli_non_hanno_niente_da_confrontare()
    {
        Assert.Empty(SezioniComuni.Confrontabili(new[]
        {
            (Vipi, ReleaseTargetType.Airport),
            (99, ReleaseTargetType.App),
        }));
    }

    /// <summary>LIBV ha DUE APP. Settori diversi, frequenze diverse: nemmeno fra loro c'è una
    /// ripetizione da togliere.</summary>
    [Fact]
    public void Due_APP_dello_stesso_campo_non_si_confrontano_fra_loro()
    {
        Assert.Empty(SezioniComuni.Confrontabili(new[]
        {
            (98, ReleaseTargetType.App),
            (99, ReleaseTargetType.App),
        }));
    }

    /// <summary>La coppia per cui la scheda esiste continua a funzionare: è il controllo che dice che la
    /// regola nuova non ha spento anche quella.</summary>
    [Fact]
    public void La_coppia_aeroporto_e_vSOP_militare_si_confronta_ancora()
    {
        Assert.Equal(new[] { Vipi, Vsop }, SezioniComuni.Confrontabili(new[]
        {
            (Vipi, ReleaseTargetType.Airport),
            (Vsop, ReleaseTargetType.AirportMil),
        }));
    }

    /// <summary>L'ordine è quello dell'unione: l'ospite per primo, perché da là esce l'ordine
    /// dell'elenco e la proposta di <see cref="SezioniComuni.DoveNascondere"/>.</summary>
    [Fact]
    public void L_ordine_dell_unione_si_conserva()
    {
        Assert.Equal(new[] { Vsop, Vipi }, SezioniComuni.Confrontabili(new[]
        {
            (Vsop, ReleaseTargetType.AirportMil),
            (99, ReleaseTargetType.App),
            (Vipi, ReleaseTargetType.Airport),
        }));
    }

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

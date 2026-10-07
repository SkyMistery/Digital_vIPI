using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I tre ausili di riempimento: riceventi proposti, incolla-tabella, cruscotto delle lacune.
/// <para>Sono tutti e tre <b>funzioni pure</b> perché sono tutti e tre <b>giudizi</b> — «probabilmente vuoi la
/// torre di Bari», «questa colonna è un livello», «questo aeroporto dovrebbe avere degli arrivi» — e un giudizio
/// va potuto provare e smentire senza un database.</para>
/// </summary>
public class AgreementFillingTests
{
    // ---- riceventi proposti --------------------------------------------------------------------------

    private static readonly IReadOnlyList<SuggestionSector> Sectors = new[]
    {
        new SuggestionSector("LIBB_ES_CTR", "LIBB", SectorType.Ctr),
        new SuggestionSector("LIBD_CS0_APP", "LIBB", SectorType.App),
        new SuggestionSector("LIBD_TWR", "LIBB", SectorType.Twr),
        new SuggestionSector("LIBDX_TWR", "LIBB", SectorType.Twr),      // prefisso simile, aeroporto diverso
        new SuggestionSector("LIRR_TS_CTR", "LIRR", SectorType.Ctr),
        new SuggestionSector("LGGG_W_CTR", "LGGG", SectorType.Ctr),
        new SuggestionSector("LGKR_APP", "LGGG", SectorType.App),
    };

    private static readonly IReadOnlyDictionary<string, string> Parents =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LIBD"] = "LIBD_CS0_APP" };

    private static IReadOnlySet<string> Set(params string[] v) =>
        new HashSet<string>(v, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Per_un_arrivo_propone_gli_enti_dell_aeroporto()
    {
        var s = AgreementSuggestions.ForReceivingSide(TransferFlowKind.Arrival, new[] { "LIBD" },
            Sectors, Parents, Set(), Set());

        // L'avvicinamento che copre l'aeroporto viene per primo: e' il ricevente piu' probabile di un arrivo.
        Assert.Equal("LIBD_CS0_APP", s[0].Callsign);
        Assert.Equal(AgreementSuggestionReason.AirportApproach, s[0].Reason);
        Assert.Contains(s, x => x.Callsign == "LIBD_TWR" && x.Reason == AgreementSuggestionReason.AirportUnit);
    }

    [Fact]
    public void Il_confronto_sul_callsign_e_per_pezzo_non_per_prefisso()
    {
        // LIBD non deve pescare LIBDX_TWR: un prefisso nudo lo farebbe, e proporrebbe la torre di un ALTRO
        // aeroporto come ricevente degli arrivi di questo.
        var s = AgreementSuggestions.ForReceivingSide(TransferFlowKind.Arrival, new[] { "LIBD" },
            Sectors, Parents, Set(), Set());

        Assert.DoesNotContain(s, x => x.Callsign == "LIBDX_TWR");
    }

    [Fact]
    public void Per_un_sorvolo_non_propone_torri()
    {
        // Un sorvolo non ha un aeroporto da consegnare: proporgli una torre sarebbe proporre qualcosa che non
        // ha senso, e chi accetta senza guardare scriverebbe un accordo sbagliato.
        var s = AgreementSuggestions.ForReceivingSide(TransferFlowKind.Overflight, new[] { "LIBD" },
            Sectors, Parents, Set("LGGG"), Set());

        Assert.DoesNotContain(s, x => x.Callsign == "LIBD_TWR");
        Assert.Contains(s, x => x.Callsign == "LGGG_W_CTR");
    }

    [Fact]
    public void I_confinanti_confermati_arrivano_col_centro_per_primo()
    {
        var s = AgreementSuggestions.ForReceivingSide(TransferFlowKind.Overflight, Array.Empty<string>(),
            Sectors, Parents, Set("LGGG"), Set());

        var greci = s.Where(x => x.Reason == AgreementSuggestionReason.ConfirmedNeighbour).ToList();
        Assert.Equal(new[] { "LGGG_W_CTR", "LGKR_APP" }, greci.Select(x => x.Callsign));
    }

    [Fact]
    public void Chi_e_gia_nell_accordo_non_si_ripropone()
    {
        var s = AgreementSuggestions.ForReceivingSide(TransferFlowKind.Arrival, new[] { "LIBD" },
            Sectors, Parents, Set(), Set("LIBD_CS0_APP"));

        Assert.DoesNotContain(s, x => x.Callsign == "LIBD_CS0_APP");
        Assert.Contains(s, x => x.Callsign == "LIBD_TWR");
    }

    // ---- incolla-tabella -----------------------------------------------------------------------------

    [Fact]
    public void Una_tabella_incollata_con_i_tab_diventa_clausole()
    {
        var parsed = ClausePaste.Parse("EKMUR\tFL130-\tLIBD_CS0_APP\nBIRSU, TOPNO\tFL150\tLIBD_CS0_APP");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.True(p.Ok));
        Assert.Equal("EKMUR", parsed[0].Clause!.Cops);
        Assert.Equal(130, parsed[0].Clause!.LevelValue);
        Assert.Equal(LevelConstraint.AtOrBelow, parsed[0].Clause!.LevelConstraint);
        // Il ricevente NON entra nella clausola: e' il lato B dell'accordo. Resta accanto per dire che righe
        // con riceventi diversi sono accordi diversi.
        Assert.Equal("LIBD_CS0_APP", parsed[0].Receiver);
        Assert.Equal("BIRSU, TOPNO", parsed[1].Clause!.Cops);
    }

    [Fact]
    public void La_virgola_non_separa_le_colonne()
    {
        // Separa gia' i punti dentro una cella: usarla anche fra colonne renderebbe le due cose
        // indistinguibili, e «EKMUR, PISIP» diventerebbe un punto piu' un livello illeggibile.
        var p = Assert.Single(ClausePaste.Parse("EKMUR, PISIP"));
        Assert.Equal("EKMUR, PISIP", p.Clause!.Cops);
        Assert.Equal(2, CopList.Count(p.Clause!.Cops));
    }

    [Fact]
    public void Una_colonna_sola_si_legge_come_i_soli_punti()
    {
        // E' l'uso piu' semplice: si incolla una colonna di fix e si mettono i livelli dopo. Rifiutarla
        // vorrebbe dire chiedere di scrivere qualcosa prima di poter incollare.
        var parsed = ClausePaste.Parse("EKMUR\nPISIP\nBIRSU");
        Assert.Equal(3, parsed.Count);
        Assert.All(parsed, x => Assert.True(x.Ok));
        Assert.Equal(new[] { "EKMUR", "PISIP", "BIRSU" }, parsed.Select(x => x.Clause!.Cops));
    }

    [Fact]
    public void Le_righe_vuote_si_saltano_in_silenzio()
    {
        // Un incolla da PDF produce righe di soli separatori a decine: segnalarle riempirebbe l'anteprima di
        // rumore, e il rumore fa saltare gli errori veri.
        var parsed = ClausePaste.Parse("EKMUR\tFL130\n\t\n   \nPISIP\tFL150");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.True(p.Ok));
        // La riga porta il numero del TESTO INCOLLATO, non della sequenza letta: chi rilegge cerca la riga nel
        // proprio appunto, non nella nostra tabella.
        Assert.Equal(new[] { 1, 4 }, parsed.Select(p => p.Line));
    }

    [Fact]
    public void Una_riga_senza_punti_ne_livello_riporta_l_errore_col_suo_numero()
    {
        // Il caso vero: un incolla che ha perso le prime colonne e ha portato solo il ricevente. Ha contenuto,
        // quindi non e' una riga vuota da saltare — ed e' proprio quella che va DETTA, perche' sparire in
        // silenzio farebbe credere che sia stata importata.
        var parsed = ClausePaste.Parse("EKMUR\tFL130\n\t\tLIBD_CS0_APP\nPISIP\tFL150");

        var rotta = Assert.Single(parsed, p => !p.Ok);
        Assert.Equal(2, rotta.Line);
        Assert.NotNull(rotta.Error);
    }

    [Fact]
    public void Un_livello_illeggibile_diventa_il_livello_speciale_e_si_vede()
    {
        // LevelFormatting.Parse non fallisce mai: cio' che non e' un livello e' il livello «speciale». Non c'e'
        // un errore da riportare — c'e' da mostrare il livello RESO nell'anteprima, cosi' chi rilegge vede
        // cosa il sistema ha capito.
        var p = Assert.Single(ClausePaste.Parse("EKMUR\tper aerovia"));
        Assert.True(p.Ok);
        Assert.Equal(LevelConstraint.Special, p.Clause!.LevelConstraint);
        Assert.Equal("per aerovia", p.Clause!.LevelSpecial);
    }

    [Fact]
    public void I_riceventi_distinti_dicono_quanti_accordi_servono()
    {
        var parsed = ClausePaste.Parse(
            "AMSOR\tFL200-\tLIRN_US0_APP\nLUNAR\tFL210-\tLIRN_US0_APP\nVEGAN\tFL210-\tLIRR_TS_CTR");

        // Il caso vero degli arrivi LIRN: due riceventi, quindi DUE accordi. Metterli sotto lo stesso sarebbe
        // ricreare esattamente il difetto che il modello nuovo ha appena chiuso.
        Assert.Equal(new[] { "LIRN_US0_APP", "LIRR_TS_CTR" }, ClausePaste.DistinctReceivers(parsed));
    }

    // ---- cruscotto delle lacune ----------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, SectorType> Types =
        Sectors.ToDictionary(s => s.Callsign, s => s.Type, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Due_sezioni_speculari_con_punti_diversi_sono_una_lacuna()
    {
        // Il caso vero in archivio: BELIX di qua, OLGAT di la'. Fino al 18 agosto 2026 i due versi vivevano in
        // ACCORDI diversi e nessuno lo vedeva; adesso stanno nello stesso accordo, una sezione sotto l'altra.
        var a = Agreement(1, "LGGG_W_CTR",
            Section(1, TransferFlowKind.Overflight, AgreementDirection.AtoB, null, "TIGRA, BELIX"),
            Section(2, TransferFlowKind.Overflight, AgreementDirection.BtoA, null, "TIGRA, OLGAT"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, Array.Empty<string>(), Set(), Types);

        var asim = Assert.Single(gaps, g => g.Kind == AgreementGapKind.AsymmetricDirections);
        // Il dettaglio NON e' una frase: e' l'elenco dei punti spaiati. Le parole le mette l'interfaccia, che
        // esiste anche in inglese.
        Assert.Equal(new[] { "BELIX", "OLGAT" }, asim.Items);
        // Porta ENTRAMBE le sezioni: senza la seconda la voce potrebbe solo indicare, non far confrontare.
        Assert.Equal(1, asim.SectionId);
        Assert.Equal(2, asim.PairSectionId);
    }

    [Fact]
    public void Due_sezioni_speculari_con_gli_stessi_punti_non_sono_una_lacuna()
    {
        var a = Agreement(1, "LGGG_W_CTR",
            Section(1, TransferFlowKind.Overflight, AgreementDirection.AtoB, null, "TIGRA, NOSTO"),
            Section(2, TransferFlowKind.Overflight, AgreementDirection.BtoA, null, "NOSTO, TIGRA"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, Array.Empty<string>(), Set(), Types);

        Assert.DoesNotContain(gaps, g => g.Kind == AgreementGapKind.AsymmetricDirections);
    }

    [Fact]
    public void Un_sorvolo_scritto_in_un_verso_solo_chiede_il_reciproco()
    {
        // Prima non si poteva nemmeno porre la domanda: i due versi stavano in accordi diversi, e «manca» era
        // indistinguibile da «e' scritto nel nodo accanto».
        var a = Agreement(1, "LGGG_W_CTR",
            Section(1, TransferFlowKind.Overflight, AgreementDirection.AtoB, null, "TIGRA"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, Array.Empty<string>(), Set(), Types);

        var mancante = Assert.Single(gaps, g => g.Kind == AgreementGapKind.MissingReverse);
        Assert.Equal(1, mancante.SectionId);
    }

    [Fact]
    public void Un_arrivo_non_ha_un_reciproco_e_non_lo_chiede()
    {
        // Il traffico scende verso uno scalo e basta: un ACC->APP e' a senso unico per natura. Segnalarlo
        // riempiva il cruscotto di falsi — sei su sette sull'archivio vero — e una categoria che urla sempre
        // insegna a non guardarla.
        var a = Agreement(1, "LIBD_CS0_APP",
            Section(1, TransferFlowKind.Arrival, AgreementDirection.AtoB, "LIBD", "EKMUR, PISIP"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, new[] { "LIBD" }, Set(), Types);

        Assert.DoesNotContain(gaps, g => g.Kind is AgreementGapKind.AsymmetricDirections or AgreementGapKind.MissingReverse);
    }

    [Fact]
    public void Due_sezioni_gemelle_si_segnalano_e_si_possono_unire()
    {
        // Stesso traffico, stesso verso, stessi scali: e' la «relazione spezzata» che il travaso ha ereditato
        // (#26/#27 in archivio). ⚠️ E' un AVVISO, non un errore: due arrivi a LIBD a condizioni diverse si
        // scrivono con le varianti, e vietare la seconda sezione non lo insegnerebbe a nessuno.
        var a = Agreement(1, "LIBD_CS0_APP",
            Section(1, TransferFlowKind.Arrival, AgreementDirection.AtoB, "LIBD", "EKMUR"),
            Section(2, TransferFlowKind.Arrival, AgreementDirection.AtoB, "LIBD", "PISIP"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, new[] { "LIBD" }, Set(), Types);

        var gemelle = Assert.Single(gaps, g => g.Kind == AgreementGapKind.TwinSections);
        Assert.Equal(1, gemelle.SectionId);
        Assert.Equal(2, gemelle.PairSectionId);
        Assert.Equal(2, gemelle.Count);
    }

    [Fact]
    public void Una_sezione_senza_clausole_si_vede()
    {
        var a = Agreement(1, "LIBD_CS0_APP",
            Section(1, TransferFlowKind.Departure, AgreementDirection.BtoA, "LIBD"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, new[] { "LIBD" }, Set(), Types);

        var vuota = Assert.Single(gaps, g => g.Kind == AgreementGapKind.EmptySection);
        Assert.Equal(1, vuota.SectionId);
    }

    [Fact]
    public void Un_confinante_confermato_senza_accordi_si_vede()
    {
        var gaps = AgreementGaps.Find("LIBB", Array.Empty<AgreementRow>(), Sectors,
            Array.Empty<string>(), Set("LGGG"), Types);

        var v = Assert.Single(gaps, g => g.Kind == AgreementGapKind.NeighbourWithoutAgreement);
        Assert.Equal("LGGG", v.Subject);
    }

    [Fact]
    public void Un_aeroporto_senza_arrivi_si_vede_ma_uno_con_arrivi_no()
    {
        var a = Agreement(1, "LIBD_CS0_APP",
            Section(1, TransferFlowKind.Arrival, AgreementDirection.AtoB, "LIBD", "EKMUR"));

        var gaps = AgreementGaps.Find("LIBB", new[] { a }, Sectors, new[] { "LIBD", "LIBR" }, Set(), Types);

        var senza = gaps.Where(g => g.Kind == AgreementGapKind.AirportWithoutArrivals).Select(g => g.Subject);
        Assert.Equal(new[] { "LIBR" }, senza);
    }

    [Fact]
    public void Un_settore_che_non_compare_in_nessun_accordo_si_vede()
    {
        var gaps = AgreementGaps.Find("LIBB", Array.Empty<AgreementRow>(), Sectors,
            Array.Empty<string>(), Set(), Types);

        var soli = gaps.Where(g => g.Kind == AgreementGapKind.SectorWithoutAgreements).Select(g => g.Subject);
        // Solo CTR e APP della ACC: una torre senza accordi propri e' normale, li riceve dall'avvicinamento.
        Assert.Equal(new[] { "LIBB_ES_CTR", "LIBD_CS0_APP" }, soli);
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static AgreementRow Agreement(int id, string sideB, params AgreementSectionRow[] sections) => new()
    {
        Id = id,
        OwnerAccCode = "LIBB",
        SideA = new AgreementEndpoint(10, "LIBB_ES_CTR"),
        SideB = new AgreementEndpoint(20, sideB),
        Order = id,
        Sections = sections,
    };

    private static AgreementSectionRow Section(int id, TransferFlowKind kind, AgreementDirection direction,
        string? icao, params string[] cops) => new()
    {
        Id = id,
        Kind = kind,
        Direction = direction,
        Order = id,
        Airports = icao is null
            ? Array.Empty<AgreementAirportRow>()
            : new[] { new AgreementAirportRow(icao, null, 1) },
        Clauses = cops.Select((c, i) => Clause(id * 100 + i, id, c)).ToList(),
    };

    private static AgreementClauseRow Clause(int id, int sectionId, string cops) => new()
    {
        Id = id, SectionId = sectionId, Cops = cops, LevelValue = 130,
        LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow, Order = id,
    };

    // ---- l'incolla clausole ora sta sul primo stadio comune ---------------------------------------------

    /// <summary>
    /// ⚠️ Lo spezzamento non è più di <c>ClausePaste</c>: lo fa <c>Griglia</c>, lo stesso primo stadio di
    /// ogni tabella importabile. Il guadagno si vede qui — la stessa incolla adesso legge anche il
    /// <b>Markdown</b>, senza che nessuno abbia scritto una seconda grammatica.
    /// </summary>
    [Fact]
    public void L_incolla_clausole_legge_anche_una_tabella_markdown()
    {
        var parsed = ClausePaste.Parse("| EKMUR | FL200 | LIRR_CTR |\n|---|---|---|\n| PISIP | FL240 | LIRR_CTR |");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.True(p.Ok));
        Assert.Equal(new[] { "EKMUR", "PISIP" }, parsed.Select(p => p.Clause!.Cops));
        Assert.Equal(new[] { "LIRR_CTR" }, ClausePaste.DistinctReceivers(parsed));
    }

    /// <summary>E la tabella HTML che Excel mette davvero in clipboard, dove le celle SONO celle.</summary>
    [Fact]
    public void L_incolla_clausole_legge_anche_una_tabella_html()
    {
        var parsed = ClausePaste.Parse(
            "<table><tr><td>EKMUR</td><td>FL200</td><td>LIRR_CTR</td></tr></table>");

        var uno = Assert.Single(parsed);
        Assert.True(uno.Ok);
        Assert.Equal("EKMUR", uno.Clause!.Cops);
        Assert.Equal("LIRR_CTR", uno.Receiver);
    }

    /// <summary>
    /// ⚠️ Un'intestazione incollata insieme alle righe si riconosce e si salta: prima diventava una clausola
    /// con i punti «POINTS», che poi qualcuno avrebbe dovuto cancellare a mano dall'accordo.
    /// </summary>
    [Fact]
    public void L_intestazione_di_una_LoA_non_diventa_una_clausola()
    {
        var parsed = ClausePaste.Parse("POINTS\tLEVEL\tRECEIVER\nEKMUR\tFL200\tLIRR_CTR");

        var uno = Assert.Single(parsed);
        Assert.Equal("EKMUR", uno.Clause!.Cops);
        Assert.Equal(2, uno.Line);          // il numero è quello del TESTO incollato: la riga 2
    }

    /// <summary>
    /// ⚠️ E la virgola continua a NON separare le colonne: «EKMUR, PISIP» è una cella sola, con dentro due
    /// punti. È la ragione per cui <c>Griglia.Leggi</c> sa spegnere la virgola.
    /// </summary>
    [Fact]
    public void La_virgola_resta_dentro_la_cella_dei_punti()
    {
        var parsed = ClausePaste.Parse("EKMUR, PISIP\nABESI, TOSTA");

        Assert.Equal(2, parsed.Count);
        Assert.Equal("EKMUR, PISIP", parsed[0].Clause!.Cops);
    }
}

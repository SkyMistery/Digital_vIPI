using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La **frase capofila** e la **provenienza** che permette alla tabella di richiudere i punti.
/// <para>La richiusura vera vive in <c>CoordTable</c> (Razor), e non si prova da qui: quello che si prova qui è
/// che il dato per farla ci sia — <c>ClauseId</c> uguale per le righe della stessa clausola, l'elenco completo
/// dei punti su ognuna — e che la capofila dica le stesse cose della frase distesa meno ciò che cambia da riga
/// a riga.</para>
/// </summary>
public class CoordinationLeadSentenceTests
{
    private static readonly CoordinationSentenceTemplate It = CoordinationSentenceTemplate.Default;

    private static readonly IReadOnlyDictionary<string, SectorType> Types = new Dictionary<string, SectorType>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = SectorType.Ctr,
        ["LIBD_CS0_APP"] = SectorType.App,
    };
    private static readonly IReadOnlyDictionary<string, string> Names = Types.Keys.ToDictionary(k => k, k => k, System.StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = "ES",
        ["LIBD_CS0_APP"] = "CS0",
    };
    private static readonly IReadOnlyDictionary<string, string> Atc = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = "Brindisi Radar",
        ["LIBD_CS0_APP"] = "Brindisi Radar",
    };
    private static readonly IReadOnlyDictionary<string, string> Airports = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBD"] = "Bari Palese",
    };

    // ---- la capofila ---------------------------------------------------------------------------------

    [Fact]
    public void La_capofila_dice_chi_a_chi_e_che_traffico()
    {
        var lead = CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", "LIBD", TransferFlowKind.Arrival);

        Assert.Equal("Brindisi Radar ES trasferisce a Brindisi Radar CS0 il traffico con destinazione " +
                     "Bari Palese LIBD secondo la tabella seguente:", lead);
    }

    [Fact]
    public void La_capofila_non_dice_livello_ne_punto()
    {
        // E' il suo scopo: quelli sono cio' che la tabella dice riga per riga, e anticiparne uno vorrebbe dire
        // eleggere una riga a rappresentante delle altre.
        var lead = CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", "LIBD", TransferFlowKind.Arrival)!;

        Assert.DoesNotContain("livello", lead);
        Assert.DoesNotContain("FL", lead);
        Assert.DoesNotContain("su ", lead);
    }

    [Fact]
    public void La_capofila_tace_sugli_stessi_dati_incompleti_della_frase_distesa()
    {
        // Il contratto e' uno solo: senza soggetto, senza destinatario, o con un arrivo senza aeroporto non
        // c'e' frase. Due contratti diversi darebbero una tabella con la capofila e senza righe, o viceversa.
        Assert.Null(CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "", "LIBD_CS0_APP", "LIBD", TransferFlowKind.Arrival));
        Assert.Null(CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", null, TransferFlowKind.Arrival));
        // Un sorvolo senza aeroporto invece la frase ce l'ha: usa la relazione neutra.
        Assert.NotNull(CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", null, TransferFlowKind.Overflight));
    }

    [Fact]
    public void La_capofila_esiste_anche_in_inglese()
    {
        // Le vLOA la vogliono in inglese, e la lingua vive nel template: una vista che la componesse da se' la
        // scriverebbe in italiano dentro un documento bilaterale — difetto gia' pagato una volta sulla parita'.
        var lead = CoordinationSentences.ComposeLead(CoordinationSentenceTemplate.English,
            Types, Names, Codes, Airports, Atc, "LIBB_ES_CTR", "LIBD_CS0_APP", "LIBD", TransferFlowKind.Arrival);

        Assert.Equal("Brindisi Radar ES transfers to Brindisi Radar CS0 the traffic inbound to " +
                     "Bari Palese LIBD as per the table below:", lead);
    }

    [Fact]
    public void La_capofila_e_la_frase_distesa_nominano_gli_enti_allo_stesso_modo()
    {
        // Il codice di posizione sul mittente e' la regola meno ovvia del composer, ed e' la ragione per cui le
        // due frasi condividono la risoluzione invece di ricavarsela ognuna.
        var distesa = CoordinationSentences.Compose(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", "LIBD", LevelConstraint.AtOrBelow, 130, LevelUnit.Fl, null,
            LevelParity.Any, "BIRSU", TransferFlowKind.Arrival)!;
        var lead = CoordinationSentences.ComposeLead(It, Types, Names, Codes, Airports, Atc,
            "LIBB_ES_CTR", "LIBD_CS0_APP", "LIBD", TransferFlowKind.Arrival)!;

        Assert.StartsWith("Brindisi Radar ES trasferisce a Brindisi Radar CS0", distesa);
        Assert.StartsWith("Brindisi Radar ES trasferisce a Brindisi Radar CS0", lead);
    }

    // ---- la provenienza ------------------------------------------------------------------------------

    [Fact]
    public void Le_righe_di_una_clausola_portano_la_stessa_clausola_e_l_elenco_intero()
    {
        var a = Agreement(Clause(1, "EKMUR, PISIP", 130));
        var points = Assert.Single(AgreementExpansion.Expand(new[] { a })).Points;

        Assert.Equal(2, points.Count);
        // La stessa clausola: e' la chiave con cui la tabella le richiude. Raggrupparle per somiglianza
        // fonderebbe due clausole che per caso dicono la stessa cosa.
        Assert.All(points, p => Assert.Equal(1, p.ClauseId));
        // E ognuna porta l'elenco INTERO, non il proprio pezzo: la tabella deve poter scrivere «EKMUR, PISIP»
        // anche partendo dalla seconda riga.
        Assert.All(points, p => Assert.Equal("EKMUR, PISIP", p.Cops));
        Assert.Equal(new[] { "EKMUR", "PISIP" }, points.Select(p => p.Cop));
    }

    [Fact]
    public void Due_clausole_diverse_restano_distinte_anche_se_dicono_la_stessa_cosa()
    {
        // Due clausole identiche sono due accordi che qualcuno ha scritto due volte apposta: fonderle in
        // lettura nasconderebbe una scelta editoriale.
        var a = Agreement(Clause(1, "EKMUR", 130), Clause(2, "EKMUR", 130));
        var points = Assert.Single(AgreementExpansion.Expand(new[] { a })).Points;

        Assert.Equal(new int?[] { 1, 2 }, points.Select(p => p.ClauseId));
    }

    [Fact]
    public void L_elenco_degli_aeroporti_viaggia_solo_quando_sono_piu_d_uno()
    {
        // Con uno solo ripeterebbe il nodo sotto cui la riga si legge gia'.
        var uno = Agreement(Clause(1, "EKMUR", 130));
        Assert.All(AgreementExpansion.Expand(new[] { uno }).SelectMany(f => f.Points),
                   p => Assert.Null(p.AgreementAirports));

        var quattro = uno with
        {
            Sections = new[]
            {
                uno.Sections[0] with
                {
                    Airports = new[]
                    {
                        new AgreementAirportRow("LIRF", null, 1), new AgreementAirportRow("LIRA", null, 2),
                        new AgreementAirportRow("LIRU", null, 3), new AgreementAirportRow("LIRE", null, 4),
                    },
                },
            },
        };
        Assert.All(AgreementExpansion.Expand(new[] { quattro }).SelectMany(f => f.Points),
                   p => Assert.Equal("LIRF · LIRA · LIRU · LIRE", p.AgreementAirports));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static AgreementRow Agreement(params AgreementClauseRow[] clauses) => new()
    {
        Id = 1,
        OwnerAccCode = "LIBB",
        SideA = new AgreementEndpoint(10, "LIBB_ES_CTR"),
        SideB = new AgreementEndpoint(20, "LIBD_CS0_APP"),
        Order = 1,
        Sections = new[]
        {
            new AgreementSectionRow
            {
                Id = 1,
                Kind = TransferFlowKind.Arrival,
                Direction = AgreementDirection.AtoB,
                Order = 1,
                Airports = new[] { new AgreementAirportRow("LIBD", null, 1) },
                Clauses = clauses,
            },
        },
    };

    private static AgreementClauseRow Clause(int id, string cops, int level) => new()
    {
        Id = id, SectionId = 1, Cops = cops, LevelValue = level,
        LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow, Order = id,
    };
}

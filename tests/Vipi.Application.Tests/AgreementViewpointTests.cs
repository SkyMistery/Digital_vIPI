using System;
using System.Collections.Generic;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// **L'orientamento di un accordo rispetto alla ACC che lo guarda.**
///
/// <para>La lente esiste perché il lato A e il lato B in archivio non dicono «noi» e «loro»: dicono chi ha
/// scritto l'accordo per primo. Sul <c>vipi.db</c> vero 13 accordi su 40 hanno LIBB solo sul lato B, e per LIRR
/// sono 10 su 11 — quindi il caso «la ACC è il lato B» non è un limite, è metà dei dati.</para>
/// </summary>
public class AgreementViewpointTests
{
    private static readonly IReadOnlyList<SuggestionSector> Sectors = new[]
    {
        new SuggestionSector("LIBB_ES_CTR", "LIBB", SectorType.Ctr),
        new SuggestionSector("LIBD_CS0_APP", "LIBB", SectorType.App),
        new SuggestionSector("LIRR_TS_CTR", "LIRR", SectorType.Ctr),
        new SuggestionSector("LIRN_US0_APP", "LIRR", SectorType.App),
        new SuggestionSector("LDZO_CTR", "LDZO", SectorType.Ctr),
        new SuggestionSector("LGGG_W_CTR", "LGGG", SectorType.Ctr),
    };

    private static AgreementViewpoint From(string acc) => new(acc, Sectors);

    [Fact]
    public void Con_la_ACC_sul_lato_A_noi_siamo_A_e_loro_sono_B()
    {
        var a = Agreement("LIBB_ES_CTR", "LDZO_CTR");
        var v = From("LIBB");

        var o = v.Orient(a);
        Assert.Equal(AgreementSide.A, o.NearSide);
        Assert.Equal(AgreementSide.B, o.FarSide);
        Assert.Equal(AgreementDirection.AtoB, o.Outbound);
        Assert.Equal(AgreementDirection.BtoA, o.Inbound);
        Assert.Equal(new[] { "LIBB_ES_CTR" }, v.Near(a));
        Assert.Equal(new[] { "LDZO_CTR" }, v.Far(a));
        Assert.Equal("LDZO", v.FarAccCode(a));
    }

    [Fact]
    public void Con_la_ACC_sul_lato_B_l_orientamento_si_ribalta()
    {
        // È l'accordo #28 dell'archivio: LDZO_CTR → LIBB_ES_CTR, scritto dall'altro capo. Prima la controparte
        // risultava «LIBB_ES_CTR», cioè noi stessi.
        var a = Agreement("LDZO_CTR", "LIBB_ES_CTR");
        var v = From("LIBB");

        var o = v.Orient(a);
        Assert.Equal(AgreementSide.B, o.NearSide);
        Assert.Equal(AgreementDirection.BtoA, o.Outbound);
        Assert.Equal(AgreementDirection.AtoB, o.Inbound);
        Assert.Equal(new[] { "LIBB_ES_CTR" }, v.Near(a));
        Assert.Equal(new[] { "LDZO_CTR" }, v.Far(a));
        Assert.Equal("LDZO", v.FarAccCode(a));
    }

    [Fact]
    public void Lo_stesso_accordo_guardato_dalle_due_ACC_si_orienta_al_contrario()
    {
        var a = Agreement("LIBB_ES_CTR", "LIRR_TS_CTR");

        Assert.Equal(AgreementSide.A, From("LIBB").Orient(a).NearSide);
        Assert.Equal(AgreementSide.B, From("LIRR").Orient(a).NearSide);
        Assert.Equal("LIRR", From("LIBB").FarAccCode(a));
        Assert.Equal("LIBB", From("LIRR").FarAccCode(a));
    }

    [Fact]
    public void Un_accordo_interno_non_ha_un_loro_e_lo_dichiara()
    {
        // Area ↔ un proprio avvicinamento: tre in archivio. La convenzione è «noi = lato A», e va annunciata.
        var a = Agreement("LIBB_ES_CTR", "LIBD_CS0_APP");
        var o = From("LIBB").Orient(a);

        Assert.True(o.IsInternal);
        Assert.False(o.IsDetached);
        Assert.Equal(AgreementSide.A, o.NearSide);
    }

    [Fact]
    public void Un_accordo_senza_controparte_resta_orientato_su_A()
    {
        var a = Agreement("LIBB_ES_CTR", null);
        var v = From("LIBB");

        Assert.Equal(AgreementSide.A, v.Orient(a).NearSide);
        Assert.Empty(v.Far(a));
        Assert.Null(v.FarAccCode(a));
    }

    [Fact]
    public void Un_accordo_visto_solo_perche_la_ACC_ne_e_responsabile_si_marca_staccato()
    {
        // Nessun lato in casa: succede quando l'accordo è stato scritto fra due centri esteri con l'owner
        // italiano. Si mostra — sparire dall'elenco di chi ne è responsabile sarebbe peggio — ma la testata
        // deve poter dire che «noi» non c'è.
        var a = Agreement("LDZO_CTR", "LGGG_W_CTR");
        var o = From("LIBB").Orient(a);

        Assert.True(o.IsDetached);
        Assert.False(o.IsInternal);
        Assert.Equal(AgreementSide.A, o.NearSide);
    }

    [Fact]
    public void Un_ente_fuori_catalogo_non_e_di_casa()
    {
        // Accordo scritto verso un ente che nel frattempo è sparito dai cataloghi: non va confuso con uno nostro.
        var a = Agreement("LIBB_ES_CTR", "LXXX_CTR");
        var v = From("LIBB");

        Assert.Equal(AgreementSide.A, v.Orient(a).NearSide);
        Assert.Null(v.FarAccCode(a));
        Assert.Equal(new[] { "LXXX_CTR" }, v.Far(a));
    }

    [Fact]
    public void Il_verso_di_una_clausola_si_legge_da_qui()
    {
        var a = Agreement("LDZO_CTR", "LIBB_ES_CTR",
            Clause(1, "BELIX", AgreementDirection.AtoB),
            Clause(2, "OLGAT", AgreementDirection.BtoA));
        var v = From("LIBB");

        // Noi siamo il lato B: la clausola AtoB entra in casa, la BtoA esce.
        Assert.False(v.IsOutbound(a, a.Clauses[0]));
        Assert.True(v.IsOutbound(a, a.Clauses[1]));
    }

    // ---- il reciproco del tipo di traffico -----------------------------------------------------------

    [Theory]
    [InlineData(TransferFlowKind.Arrival, TransferFlowKind.Departure)]
    [InlineData(TransferFlowKind.Departure, TransferFlowKind.Arrival)]
    [InlineData(TransferFlowKind.Overflight, TransferFlowKind.Overflight)]
    [InlineData(TransferFlowKind.Vfr, TransferFlowKind.Vfr)]
    [InlineData(TransferFlowKind.Other, TransferFlowKind.Other)]
    public void Il_verso_opposto_di_un_arrivo_e_una_partenza(TransferFlowKind kind, TransferFlowKind atteso)
    {
        Assert.Equal(atteso, TrafficKinds.Reciprocal(kind));
        Assert.Equal(kind != atteso, TrafficKinds.HasDistinctReciprocal(kind));
    }

    // ---- punti spaiati fra i due versi ---------------------------------------------------------------

    [Fact]
    public void I_punti_spaiati_sono_quelli_che_stanno_da_un_lato_solo()
    {
        var a = Agreement("LIBB_ES_CTR", "LGGG_W_CTR",
            Clause(1, "TIGRA, BELIX", AgreementDirection.AtoB),
            Clause(2, "TIGRA, OLGAT", AgreementDirection.BtoA));

        // TIGRA sta da entrambe le parti: è l'accordo che regge, e non va segnalato.
        Assert.Equal(new[] { "BELIX", "OLGAT" }, AgreementPoints.UnpairedWithin(a));
    }

    [Fact]
    public void Un_verso_vuoto_non_e_un_asimmetria()
    {
        // È un reciproco da scrivere, e lo dice il conteggio delle clausole. Segnalarlo qui farebbe scattare
        // l'avviso su tutti i 41 accordi dell'archivio, che di bilaterali non ne ha nessuno.
        var a = Agreement("LIBB_ES_CTR", "LGGG_W_CTR", Clause(1, "BELIX", AgreementDirection.AtoB));

        Assert.Empty(AgreementPoints.UnpairedWithin(a));
    }

    [Fact]
    public void Due_versi_con_gli_stessi_punti_non_hanno_niente_da_dire()
    {
        var a = Agreement("LIBB_ES_CTR", "LGGG_W_CTR",
            Clause(1, "TIGRA, BELIX", AgreementDirection.AtoB),
            Clause(2, "BELIX, TIGRA", AgreementDirection.BtoA));

        Assert.Empty(AgreementPoints.UnpairedWithin(a));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static AgreementRow Agreement(string sideA, string? sideB, params AgreementClauseRow[] clauses)
    {
        var parties = new List<AgreementPartyRow> { new(AgreementSide.A, 10, sideA, 1) };
        if (sideB is not null) parties.Add(new AgreementPartyRow(AgreementSide.B, 20, sideB, 1));

        return new AgreementRow
        {
            Id = 1, OwnerAccCode = "LIBB", TrafficKind = TransferFlowKind.Overflight, Order = 1,
            Parties = parties,
            Airports = Array.Empty<AgreementAirportRow>(),
            Clauses = clauses,
        };
    }

    private static AgreementClauseRow Clause(int id, string cops, AgreementDirection d) => new()
    {
        Id = id, Direction = d, Cops = cops, LevelValue = 130,
        LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow, Order = id,
    };
}

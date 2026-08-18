using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// **L'orientamento di un accordo rispetto alla ACC che lo guarda.**
///
/// <para>La lente esiste perché il lato A e il lato B in archivio non dicono «noi» e «loro»: dal 18 agosto 2026
/// dicono anche meno di prima — sono in <b>forma canonica</b> (id minore = A), che è la chiave dell'unicità
/// della coppia e non una scelta editoriale. Sul <c>vipi.db</c> vero 13 accordi su 40 avevano LIBB solo sul lato
/// B, e per LIRR erano 10 su 11: il caso «la ACC è il lato B» non è un limite, è metà dei dati.</para>
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
        Assert.Equal("LIBB_ES_CTR", v.Near(a));
        Assert.Equal("LDZO_CTR", v.Far(a));
        Assert.Equal("LDZO", v.FarAccCode(a));
    }

    [Fact]
    public void Con_la_ACC_sul_lato_B_l_orientamento_si_ribalta()
    {
        // È il caso di metà archivio, e adesso è anche il caso NORMALE: i lati stanno in forma canonica, quindi
        // quale sia A dipende dagli id, non da chi ha scritto per primo.
        var a = Agreement("LDZO_CTR", "LIBB_ES_CTR");
        var v = From("LIBB");

        var o = v.Orient(a);
        Assert.Equal(AgreementSide.B, o.NearSide);
        Assert.Equal(AgreementDirection.BtoA, o.Outbound);
        Assert.Equal(AgreementDirection.AtoB, o.Inbound);
        Assert.Equal("LIBB_ES_CTR", v.Near(a));
        Assert.Equal("LDZO_CTR", v.Far(a));
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
        Assert.Equal("LXXX_CTR", v.Far(a));
    }

    [Fact]
    public void Il_verso_di_una_sezione_si_legge_da_qui()
    {
        // ⚠️ Il verso sta sulla SEZIONE dal 18 agosto 2026: era sulla clausola, e lì costringeva a tenerlo
        // d'accordo su righe che dicono la stessa cosa.
        var a = Agreement("LDZO_CTR", "LIBB_ES_CTR",
            Section(1, AgreementDirection.AtoB),
            Section(2, AgreementDirection.BtoA));
        var v = From("LIBB");

        // Noi siamo il lato B: la sezione AtoB entra in casa, la BtoA esce.
        Assert.False(v.IsOutbound(a, a.Sections[0]));
        Assert.True(v.IsOutbound(a, a.Sections[1]));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static AgreementRow Agreement(string sideA, string sideB, params AgreementSectionRow[] sections) => new()
    {
        Id = 1,
        OwnerAccCode = "LIBB",
        SideA = new AgreementEndpoint(10, sideA),
        SideB = new AgreementEndpoint(20, sideB),
        Order = 1,
        Sections = sections,
    };

    private static AgreementSectionRow Section(int id, AgreementDirection direction) => new()
    {
        Id = id,
        Kind = TransferFlowKind.Overflight,
        Direction = direction,
        Order = id,
        Airports = Array.Empty<AgreementAirportRow>(),
        Clauses = Array.Empty<AgreementClauseRow>(),
    };
}

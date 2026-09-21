using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// SID e STAR fra i punti di un trasferimento (21 settembre 2026): la frase dice «autorizzato via» la procedura,
/// e senza un luogo scritto il trasferimento avviene al confine dell'AoR.
/// </summary>
public class ProceduraNeiPuntiTests
{
    [Theory]
    [InlineData("BANAV 9A", true)]
    [InlineData("BANA9A", true)]
    [InlineData("ELB 1A", true)]
    [InlineData("pis1x", true)]
    [InlineData(" TIBER 2A ", true)]
    [InlineData("BANAV", false)]       // un fix
    [InlineData("ELB", false)]         // una radioassistenza
    [InlineData("ALL", false)]
    [InlineData("ALL to GR", false)]
    [InlineData("4530N", false)]
    [InlineData("BANAV 12", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Una_procedura_si_riconosce_dalla_forma(string? punto, bool attesa) =>
        Assert.Equal(attesa, ProceduraNeiPunti.E(punto));

    [Fact]
    public void Basta_una_procedura_fra_i_punti()
    {
        Assert.True(ProceduraNeiPunti.Contiene("BANAV, BANAV 9A"));
        Assert.False(ProceduraNeiPunti.Contiene("BANAV, ELB"));
    }

    [Theory]
    [InlineData(TransferHandoffKind.Unspecified, "PIS 1A", TransferHandoffKind.AorBoundary)]
    [InlineData(TransferHandoffKind.Unspecified, "MAREL", TransferHandoffKind.Unspecified)]
    [InlineData(TransferHandoffKind.Point, "PIS 1A", TransferHandoffKind.Point)]       // una scelta scritta resta
    [InlineData(TransferHandoffKind.Custom, "PIS 1A", TransferHandoffKind.Custom)]
    public void Il_luogo_di_trasferimento_che_vale(TransferHandoffKind scritto, string punti, TransferHandoffKind atteso) =>
        Assert.Equal(atteso, ProceduraNeiPunti.Consegna(scritto, punti));

    [Fact]
    public void Il_salvataggio_scrive_il_confine()
    {
        var input = new AgreementClauseInput { Cops = "PIS 1A", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow, HandoffLevelConstraint = LevelConstraint.AtOrAbove };
        var n = ProceduraNeiPunti.Normalizza(input);
        Assert.Equal(TransferHandoffKind.AorBoundary, n.HandoffKind);
        Assert.Equal(LevelConstraint.Exact, n.HandoffLevelConstraint);

        var senza = new AgreementClauseInput { Cops = "MAREL", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow };
        Assert.Same(senza, ProceduraNeiPunti.Normalizza(senza));
    }

    private static readonly Dictionary<string, SectorType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIRR_NE_CTR"] = SectorType.Ctr, ["LIRP_APP"] = SectorType.App,
    };
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIRR_NE_CTR"] = "LIRR_NE_CTR", ["LIRP_APP"] = "LIRP Approach",
    };
    private static readonly Dictionary<string, string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIRR_NE_CTR"] = "NE", ["LIRP_APP"] = "US0",
    };
    private static readonly Dictionary<string, string> Airports = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIRP"] = "Pisa - San Giusto",
    };
    private static readonly Dictionary<string, string> Atc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIRR_NE_CTR"] = "Roma Radar", ["LIRP_APP"] = "Pisa Approach",
    };

    private static string? Frase(string cop, TransferHandoffFacet? facet = null, CoordinationSentenceTemplate? tpl = null) =>
        CoordinationSentences.Compose(tpl ?? CoordinationSentenceTemplate.Default, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, 120, LevelUnit.Fl, null, LevelParity.Any,
            cop, TransferFlowKind.Arrival, facet: facet);

    [Fact]
    public void Con_una_STAR_la_frase_e_via_procedura_al_confine_dell_AoR()
    {
        Assert.Equal(
            "Roma Radar NE trasferisce a Pisa Approach US0 il traffico con destinazione Pisa - San Giusto LIRP "
            + "autorizzato via PIS 1A a livello 120 o livello inferiore, al confine dell'AoR.",
            Frase("PIS 1A"));
        Assert.EndsWith("cleared via PIS 1A at level 120 or below, at the AoR boundary.",
            Frase("PIS 1A", tpl: CoordinationSentenceTemplate.English));
    }

    [Fact]
    public void Con_un_fix_la_frase_resta_quella_breve()
    {
        Assert.EndsWith("a livello 120 o livello inferiore su MAREL.", Frase("MAREL"));
    }

    // ---- il nome di oggi: come nelle tabelle e nelle citazioni ----

    private static AirportSidRowView Riga(string nome, string fix) => new("07", fix, nome, "—", "—", "—", "—", "—", "—");

    private static NomiProcedura Archivio(ProcedureKind kind, string icao, params (string Nome, string Fix)[] righe) =>
        new(new Dictionary<(ProcedureKind, string), AirportSidView>
        {
            [(kind, icao)] = new(righe.Select(r => Riga(r.Nome, r.Fix)).ToList()),
        });

    [Theory]
    [InlineData("BANA9A")]      // il codice d'archivio
    [InlineData("BANAV 9A")]    // il nome per esteso, scritto a mano o dal suggerimento
    [InlineData("bana9a")]
    public void Una_STAR_scritta_esce_col_nome_per_esteso(string scritto)
    {
        var nomi = Archivio(ProcedureKind.Star, "LIBD", ("BANA9A", "BANAV"));
        Assert.Equal("BANAV 9A", ProceduraNeiPunti.Risolvi(scritto, new[] { "LIBD" }, TransferFlowKind.Arrival, nomi));
    }

    [Theory]
    [InlineData("BANA9A")]
    [InlineData("BANAV 9A")]
    public void Rinominata_nell_archivio_la_procedura_segue_senza_toccare_la_clausola(string scritto)
    {
        // Il nuovo ciclo porta BANA1A al posto di BANA9A: stessa radice, la clausola non si riscrive.
        var nomi = Archivio(ProcedureKind.Star, "LIBD", ("BANA1A", "BANAV"));
        Assert.Equal("BANAV 1A", ProceduraNeiPunti.Risolvi(scritto, new[] { "LIBD" }, TransferFlowKind.Arrival, nomi));
    }

    [Fact]
    public void Il_verso_conta_e_i_fix_restano_come_sono()
    {
        var sid = Archivio(ProcedureKind.Sid, "LIBD", ("BANA9A", "BANAV"));
        // Un arrivo cerca fra le STAR: una SID omonima non è sua.
        Assert.Equal("BANA9A, MAREL", ProceduraNeiPunti.Risolvi("BANA9A, MAREL", new[] { "LIBD" }, TransferFlowKind.Arrival, sid));
        Assert.Equal("BANAV 9A, MAREL", ProceduraNeiPunti.Risolvi("BANA9A, MAREL", new[] { "LIBD" }, TransferFlowKind.Departure, sid));
    }

    [Fact]
    public void Una_procedura_sparita_resta_l_ultimo_nome_scritto()
    {
        var nomi = Archivio(ProcedureKind.Star, "LIBD", ("DIVK8A", "DIVKU"));
        Assert.Equal("BANAV 9A", ProceduraNeiPunti.Risolvi("BANAV 9A", new[] { "LIBD" }, TransferFlowKind.Arrival, nomi));
    }

    [Fact]
    public void Gli_accordi_escono_coi_nomi_di_oggi_e_le_tabelle_sono_solo_quelle_citate()
    {
        var clausola = new AgreementClauseRow { Id = 1, SectionId = 1, Order = 1, Cops = "BANA9A", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow };
        var fix = clausola with { Id = 2, Order = 2, Cops = "MAREL" };
        AgreementSectionRow Sezione(int id, TransferFlowKind kind, AgreementClauseRow c) => new()
        {
            Id = id, Kind = kind, Direction = AgreementDirection.AtoB, Order = id,
            Airports = new[] { new AgreementAirportRow("LIBD", null, 1) }, Clauses = new[] { c },
        };
        var accordo = new AgreementRow
        {
            Id = 1, OwnerAccCode = "LIBB", Order = 1, SideA = new(1, "LIBB_ES_CTR"), SideB = new(2, "LIBD_CS0_APP"),
            Sections = new[] { Sezione(1, TransferFlowKind.Arrival, clausola), Sezione(2, TransferFlowKind.Departure, fix) },
        };

        // Solo la sezione con una procedura chiede una tabella, e nel suo verso.
        Assert.Equal(new[] { (ProcedureKind.Star, "LIBD") }, ProceduraNeiPunti.TabelleCitate(new[] { accordo }));

        var oggi = ProceduraNeiPunti.ConNomiDiOggi(new[] { accordo }, Archivio(ProcedureKind.Star, "LIBD", ("BANA1A", "BANAV")));
        Assert.Equal("BANAV 1A", oggi[0].Sections[0].Clauses[0].Cops);
        Assert.Same(accordo.Sections[1], oggi[0].Sections[1]);   // niente da risolvere: stessa istanza
    }

    [Fact]
    public void Un_luogo_scritto_con_una_procedura_resta_il_suo()
    {
        var suPunto = TransferHandoffFacet.None with { Kind = TransferHandoffKind.Point, Label = "CHI" };
        var s = Frase("PIS 1A", suPunto);
        Assert.Contains("autorizzato via PIS 1A", s);
        Assert.Contains("su CHI", s);
        Assert.DoesNotContain("confine", s);
    }
}

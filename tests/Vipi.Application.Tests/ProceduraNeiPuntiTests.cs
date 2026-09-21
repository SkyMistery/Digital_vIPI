using Vipi.Application.Content;
using Vipi.Domain;
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

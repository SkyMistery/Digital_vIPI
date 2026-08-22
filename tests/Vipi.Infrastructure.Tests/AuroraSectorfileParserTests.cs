using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Parser sectorfile Aurora: estrazione fix/designatore/transition/RNAV, completion fix→VOR→alias, StableKey.</summary>
public class AuroraSectorfileParserTests
{
    // Estratti reali (formato itfix / itvor).
    private const string Fix = """
        ALAXI;N040.00.00.000;E010.00.00.000;0;1;
        SOSIV;N041.00.00.000;E012.00.00.000;0;1;
        SOSAK;N041.30.00.000;E012.30.00.000;0;1;
        ESINO;N042.00.00.000;E013.00.00.000;0;1;
        """;
    private const string Vor = """
        OST;114.90;N041.48.13.600;E012.14.15.100;;;;HLD-OST;
        """;

    private static IReadOnlyDictionary<string, string> NoAlias => new Dictionary<string, string>();

    private static IReadOnlyList<Application.Abstractions.SourceSid> Parse(string sid,
        IReadOnlyDictionary<string, string>? alias = null)
    {
        var nav = AuroraSectorfileParser.ParseNavaids(Fix, Vor);
        return AuroraSectorfileParser.ParseSids("LIRN", sid, nav.Names, alias ?? NoAlias);
    }

    [Fact]
    public void Completes_Fix_From_Fix_And_Vor()
    {
        // ALAX7G → prefix ALAX → ALAXI (fix); OST1E → prefix OST → OST (VOR).
        var rows = Parse("LIRN;06;ALAX7G;;;;;1;\nLIRN;07;OST1E;;;;;1;");
        Assert.Equal("ALAXI", rows.Single(r => r.Name == "ALAX7G").Fix);
        Assert.Equal("OST", rows.Single(r => r.Name == "OST1E").Fix);
        Assert.All(rows, r => Assert.False(r.NeedsFixReview));
        Assert.Equal("RNAV", rows[0].Type);
    }

    [Fact]
    public void StableKey_Ignores_Revision_Digit()
    {
        var g7 = Parse("LIRN;06;ALAX7G;;;;;1;").Single();
        var g8 = Parse("LIRN;06;ALAX8G;;;;;1;").Single();
        var j7 = Parse("LIRN;06;ALAX7J;;;;;1;").Single();
        Assert.Equal(g7.StableKey, g8.StableKey);        // 7G e 8G stessa identità
        Assert.NotEqual(g7.StableKey, j7.StableKey);      // 7G e 7J distinte
    }

    [Fact]
    public void SidTrans_Uses_Transition_Fix_From_Col6()
    {
        // SOSI-only e SIV-trans risolvono lo stesso fix SOSIV (via prefix/alias) → coerenza del punto.
        var only = Parse("LIRN;25;SOSI5A;;;;;1;").Single();
        var trans = Parse("LIRN;25;SIV5A-ESI8H; ; ;0;ESINO;1;",
            new Dictionary<string, string> { ["SIV"] = "SOSIV" }).Single();
        Assert.Equal("SOSIV", only.Fix);
        Assert.Equal("SOSIV", trans.Fix);
        Assert.Equal("ESINO", trans.Transition);
        Assert.Null(only.Transition);
    }

    [Fact]
    public void Unresolved_Prefix_Flags_NeedsReview()
    {
        var r = Parse("LIRN;25;ZZZ5A;;;;;1;").Single();
        Assert.True(r.NeedsFixReview);
        Assert.Equal("ZZZ", r.Fix);   // prefisso grezzo
    }

    [Fact]
    public void Ambiguous_Prefix_Flags_Review_Unless_Alias()
    {
        // "SOS" matcha sia SOSIV che SOSAK → ambiguo → da verificare.
        var amb = Parse("LIRN;25;SOS5A-ESI8H; ; ;0;ESINO;1;").Single();
        Assert.True(amb.NeedsFixReview);
        // Con alias autoritativo l'ambiguità è risolta.
        var aliased = Parse("LIRN;25;SOS5A-ESI8H; ; ;0;ESINO;1;",
            new Dictionary<string, string> { ["SOS"] = "SOSAK" }).Single();
        Assert.False(aliased.NeedsFixReview);
        Assert.Equal("SOSAK", aliased.Fix);
    }

    [Fact]
    public void Expands_Multiple_Runways()
    {
        var rows = Parse("LIRN;16L:16R;ALAX7G;;;;;1;");
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Runway == "16L");
        Assert.Contains(rows, r => r.Runway == "16R");
        Assert.NotEqual(rows[0].StableKey, rows[1].StableKey);   // la pista fa parte dell'identità
    }

    // --- Catalogo dei punti (itvor/itndb/itfix) -------------------------------------------------------

    [Fact]
    public void Catalogo_Distingue_Fix_Vor_E_Ndb()
    {
        const string Ndb = "AVI;390.0;N045.55.27.600;E012.25.42.600;";
        var c = AuroraSectorfileParser.ParseNavaids(Fix, Vor, Ndb);

        Assert.Equal(Application.Abstractions.NavaidKind.Vor, c.Entries.Single(e => e.Name == "OST").Kind);
        Assert.Equal(Application.Abstractions.NavaidKind.Ndb, c.Entries.Single(e => e.Name == "AVI").Kind);
        Assert.Equal(Application.Abstractions.NavaidKind.Fix, c.Entries.Single(e => e.Name == "ALAXI").Kind);
        Assert.Equal(6, c.Entries.Count);   // 4 fix + 1 VOR + 1 NDB
    }

    [Fact]
    public void Catalogo_Ignora_Righe_Vuote_E_File_Assenti()
    {
        // Un file 404 arriva qui come null (SectorfileRaw), e le righe vuote in coda sono la norma nei file veri.
        var c = AuroraSectorfileParser.ParseNavaids("ALAXI;N040.00.00.000;E010.00.00.000;0;1;\n\n\n", null, null);
        Assert.Equal("ALAXI", Assert.Single(c.Entries).Name);
    }

    [Fact]
    public void Catalogo_E_Le_Stesse_Voci_Che_Completano_Le_SID()
    {
        // L'invariante che tiene insieme le due funzioni: l'editor non deve poter segnalare come sbagliato un
        // nome che l'import considera buono. Vale finché i due leggono lo STESSO catalogo.
        var c = AuroraSectorfileParser.ParseNavaids(Fix, Vor);
        var rows = AuroraSectorfileParser.ParseSids("LIRN", "LIRN;06;ALAX7G;;;;;1;", c.Names, NoAlias);

        Assert.Contains(rows.Single().Fix, c.Names);
    }
}

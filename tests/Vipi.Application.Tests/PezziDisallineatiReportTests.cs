using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La riga della Diagnostica che dice se il ponte delle forme regge (S11 fase A, carta 15 §4-bis): in produzione deve
/// essere <b>vuota</b> prima che le letture passino ai pezzi, ed è l'unico modo di saperlo da fuori.
/// </summary>
public class PezziDisallineatiReportTests
{
    private static ConsistencyDataset Con(params string[] disallineati) => new()
    {
        PezziDiFormaDisallineati = disallineati,
        RunwayIdents = new Dictionary<int, string>(),
        AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ParentRefs = Array.Empty<ParentRefRow>(),
        ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Allineati_non_si_dice_niente()
    {
        Assert.Empty(ConsistencyReportService.Analyze(Con()));
    }

    [Fact]
    public void Disallineati_si_dice_quanti_e_quali_con_un_tetto_ai_nomi()
    {
        var nomi = Enumerable.Range(1, 12).Select(i => $"LIRR_{i:00}_CTR").ToArray();

        var f = Assert.Single(ConsistencyReportService.Analyze(Con(nomi)));

        Assert.Equal(("Diag_Cat_PezziDisallineati", "Diag_Msg_PezziDisallineati"), (f.CategoryKey, f.DetailKey));
        Assert.Equal(ConsistencyArea.Dati, f.Area);
        Assert.Equal(12, f.DetailArgs![0]);
        Assert.Contains("LIRR_10_CTR", (string)f.DetailArgs[1]);
        Assert.DoesNotContain("LIRR_11_CTR", (string)f.DetailArgs[1]);
        Assert.EndsWith("…", (string)f.DetailArgs[1]);
    }
}

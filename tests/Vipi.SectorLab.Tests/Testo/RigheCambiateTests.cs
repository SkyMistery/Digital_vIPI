using Vipi.SectorLab.Core.Testo;

namespace Vipi.SectorLab.Tests.Testo;

/// <summary>
/// Pagina Testo (riordino, D12): dal testo scritto alle righe da cambiare, nella forma di <c>CambiaRighe</c>. Una riga
/// nuova si attacca a una vicina che non è un tag, così i tag che non cambiano restano fuori dalle sostituzioni.
/// </summary>
public sealed class RigheCambiateTests
{
    private static bool Tag(string r) => r.TrimStart().StartsWith("//@", StringComparison.Ordinal);

    private static readonly string[] Prima = ["//titolo", "//@\"A\" note=\"x\"", "//@START", "A;1;", "//@END \"A\"", "B;2;", "C;3;"];

    [Fact]
    public void UgualeNienteDaCambiare() => Assert.Empty(RigheCambiate.Di(Prima, [.. Prima], Tag));

    [Fact]
    public void UnaRigaCambiata_SoloLei()
    {
        var dopo = Prima.ToArray();
        dopo[5] = "B;22;";
        var s = RigheCambiate.Di(Prima, dopo, Tag);
        Assert.Equal([6], s.Keys);
        Assert.Equal(["B;22;"], s[6]);
    }

    [Fact]
    public void UnaRigaNuova_SiAttaccaAQuellaPrima_OADopoSeQuellaPrimaEUnTag()
    {
        // Dopo «B;2;»: in coda a lei.
        var s = RigheCambiate.Di(Prima, [.. Prima[..6], "B2;5;", .. Prima[6..]], Tag);
        Assert.Equal([6], s.Keys);
        Assert.Equal(["B;2;", "B2;5;"], s[6]);

        // Dopo «//@END "A"» (un tag): in testa alla riga dopo, «B;2;».
        s = RigheCambiate.Di(Prima, [.. Prima[..5], "//nuovo", .. Prima[5..]], Tag);
        Assert.Equal([6], s.Keys);
        Assert.Equal(["//nuovo", "B;2;"], s[6]);
    }

    [Fact]
    public void RigheTolteEUnPezzoRiscritto()
    {
        // Tolte «B» e «C»: la prima prende niente, la seconda si toglie.
        var s = RigheCambiate.Di(Prima, Prima[..5], Tag);
        Assert.Equal([6, 7], s.Keys);
        Assert.All(s.Values, v => Assert.Empty(v));

        // Due righe diventano tre: la prima del pezzo le prende tutte.
        s = RigheCambiate.Di(Prima, [.. Prima[..5], "X;1;", "Y;2;", "Z;3;"], Tag);
        Assert.Equal(["X;1;", "Y;2;", "Z;3;"], s[6]);
        Assert.Empty(s[7]);
    }

    [Fact]
    public void LeSostituzioni_RifannoIlTestoScritto()
    {
        // Qualunque cambio: applicando le sostituzioni al testo di prima viene quello scritto.
        string[] dopo = ["//titolo nuovo", "//@\"A\" note=\"x\"", "//@START", "A;1;", "A2;9;", "//@END \"A\"", "C;3;", "D;4;"];
        var s = RigheCambiate.Di(Prima, dopo, Tag);
        var rifatto = Prima.SelectMany((r, i) => s.TryGetValue(i + 1, out var nuove) ? nuove : [r]).ToArray();
        Assert.Equal(dopo, rifatto);
        // I tag non cambiati restano fuori.
        Assert.DoesNotContain(s.Keys, k => Tag(Prima[k - 1]));
    }

    [Fact]
    public void OltreIlTetto_IlPezzoInMezzoSiSostituisceInBlocco()
    {
        var prima = Enumerable.Range(0, 3000).Select(i => $"P{i};").ToArray();
        var dopo = Enumerable.Range(0, 3000).Select(i => $"Q{i};").ToArray();
        var s = RigheCambiate.Di(prima, dopo, Tag);
        Assert.Equal(dopo, s[1]);
        Assert.Equal(2999, s.Count(kv => kv.Value.Count == 0));
    }
}

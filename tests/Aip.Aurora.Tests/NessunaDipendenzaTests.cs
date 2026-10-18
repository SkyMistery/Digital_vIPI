using System.Reflection;

namespace Aip.Aurora.Tests;

/// <summary>
/// 🔴 Il converter si porta via insieme al modello, e basta (carta F6 §3): i test leggono col motore del sector, il
/// converter no.
/// </summary>
public sealed class NessunaDipendenzaTests
{
    [Fact]
    public void IlConverterReferenziaSoloIlModelloEIlRuntime()
    {
        var converter = typeof(AuroraGeo).Assembly;

        var estranei = converter.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n != "netstandard" && n != "mscorlib" && n != "Aip" && !n.StartsWith("System", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(estranei);
        Assert.DoesNotContain("vipi", converter.GetName().Name!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(converter.GetTypes(), t => (t.Namespace ?? "").Contains("vipi", StringComparison.OrdinalIgnoreCase));
    }
}

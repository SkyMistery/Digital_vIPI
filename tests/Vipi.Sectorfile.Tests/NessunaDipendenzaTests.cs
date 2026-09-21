using System.Reflection;

namespace Vipi.Sectorfile.Tests;

/// <summary>
/// La decisione della carta F2 §2.1, tenuta vera: il motore del sector non dipende da nessun altro assieme del
/// repo né da pacchetti. Lo userà l'app desktop del Lab, che non deve portarsi dietro EF, MySQL e l'host.
/// </summary>
public sealed class NessunaDipendenzaTests
{
    [Fact]
    public void IlMotoreReferenziaSoloIlRuntime()
    {
        var motore = Assembly.Load("Vipi.Sectorfile");

        var estranei = motore.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n != "netstandard" && n != "mscorlib" && !n.StartsWith("System", StringComparison.Ordinal)
                && !n.StartsWith("Microsoft.Win32", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(estranei);
    }
}

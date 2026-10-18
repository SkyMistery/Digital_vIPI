using System.Reflection;

namespace Aip.Tests;

/// <summary>
/// 🔴 Il modello AIP si prende e si porta in un altro progetto (committente, 18 ottobre; carta F6 §3): se un
/// giorno referenziasse il Lab, il sito o il motore del sector, non lo si potrebbe più fare, e nessuno se ne
/// accorgerebbe finché non ci prova.
/// </summary>
public sealed class NessunaDipendenzaTests
{
    [Fact]
    public void IlModelloReferenziaSoloIlRuntime()
    {
        var modello = typeof(AipDocument).Assembly;

        var estranei = modello.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n != "netstandard" && n != "mscorlib" && !n.StartsWith("System", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(estranei);
    }

    [Fact]
    public void NeiNomiNonCeVipi()
    {
        var modello = typeof(AipDocument).Assembly;

        Assert.DoesNotContain("vipi", modello.GetName().Name!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(modello.GetTypes(), t => (t.Namespace ?? "").Contains("vipi", StringComparison.OrdinalIgnoreCase));
    }
}

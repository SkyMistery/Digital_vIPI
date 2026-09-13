using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 <b>Nella UI un semaforo non si smaltisce.</b> Il 4 settembre 2026 (<c>DocumentEditorShell</c>) e di nuovo il
/// 13 settembre (T-013 <c>ImportaTabella</c>, T-037 <c>LivePage</c>): un componente smontato smaltiva il suo
/// <c>SemaphoreSlim</c>, il lavoro in volo tornava dopo, e il suo <c>Release()</c> abbatteva il circuito. Un
/// <c>SemaphoreSlim</c> senza <c>AvailableWaitHandle</c> non tiene risorse: il garbage collector basta.
///
/// <para>Due casi su tre li ha trovati una revisione, non un test: questa guardia li trova per costruzione.</para>
/// </summary>
public class SemaforiNonSiSmaltisconoTests
{
    [Fact]
    public void Nessun_componente_smaltisce_un_suo_semaforo()
    {
        var radice = RadiceUi();
        var colpevoli = new List<string>();

        foreach (var file in Directory.EnumerateFiles(radice, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs"))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var testo = File.ReadAllText(file);
            foreach (Match campo in Regex.Matches(testo, @"SemaphoreSlim\s+(_\w+)"))
            {
                var nome = campo.Groups[1].Value;
                // Si cerca la CHIAMATA, fuori dai commenti: le spiegazioni la citano apposta.
                var righe = testo.Split('\n').Where(r => !r.TrimStart().StartsWith("//") && !r.TrimStart().StartsWith("///"));
                if (righe.Any(r => Regex.IsMatch(r, $@"\b{Regex.Escape(nome)}\??\.Dispose\(\)")))
                    colpevoli.Add($"{Path.GetRelativePath(radice, file)}: {nome}.Dispose()");
            }
        }

        Assert.True(colpevoli.Count == 0,
            "Semafori smaltiti allo smontaggio: il lavoro in volo che torna dopo abbatte il circuito col suo Release().\n"
            + string.Join("\n", colpevoli));
    }

    private static string RadiceUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}

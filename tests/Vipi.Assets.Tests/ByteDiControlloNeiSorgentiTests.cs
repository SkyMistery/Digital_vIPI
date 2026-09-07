using Xunit;

namespace Vipi.Assets.Tests;

/// <summary>
/// <b>Nei sorgenti non ci sono byte di controllo scritti come byte.</b>
///
/// <para>Due volte, da due autori diversi, un separatore è finito nel file come <b>byte vero</b> invece che
/// come escape: un <c>NUL</c> in <c>SchemaDrift.Key</c> e un <c>0x1F</c>/<c>0x1E</c> nella firma dell'indice
/// unito (<c>UnionMembersEditor</c>). Il codice funziona — ed è il problema: per ogni strumento testuale
/// quel file è <b>binario</b>. <c>grep</c> lo salta, <c>git diff</c> non lo mostra, e una normalizzazione
/// qualunque può togliere quei byte in silenzio. Nel caso della firma, toglierli riaprirebbe esattamente il
/// difetto che il commento sopra dice di aver chiuso — senza che nessun test cada
/// (revisione del 6 settembre 2026, R-001 e R-025).</para>
///
/// <para>⚠️ <b>La lezione vale più dei due fix</b>: dopo il primo caso nessuno andò a vedere se ce n'erano
/// altri, e ce n'era un altro. Questo test chiude la famiglia invece dei due esemplari — che è la ragione
/// per cui esiste, e per cui il messaggio dice come si scrive la forma giusta.</para>
///
/// <para>⚠️ Un difetto <b>non</b> si chiude con l'occhio: <c>0x1F</c> a schermo non si vede.</para>
/// </summary>
public class ByteDiControlloNeiSorgentiTests
{
    /// <summary>I soli byte di controllo che un sorgente può contenere: tabulazione, a capo, ritorno.</summary>
    private static bool Ammesso(byte b) => b is 0x09 or 0x0A or 0x0D;

    private static bool DiControllo(byte b) => (b < 0x20 && !Ammesso(b)) || b == 0x7F;

    private static readonly string[] Estensioni =
        { "*.cs", "*.razor", "*.css", "*.js", "*.json", "*.md", "*.resx", "*.csproj", "*.yml" };

    [Fact]
    public void Nessun_sorgente_porta_un_byte_di_controllo_scritto_come_byte()
    {
        var colpevoli = new List<string>();

        foreach (var file in Sorgenti())
        {
            var byteFile = File.ReadAllBytes(file);
            var i = Array.FindIndex(byteFile, DiControllo);
            if (i < 0) continue;

            var riga = byteFile.Take(i).Count(b => b == 0x0A) + 1;
            colpevoli.Add($"{Path.GetRelativePath(Radice(), file)}:{riga} → 0x{byteFile[i]:X2}");
        }

        Assert.True(colpevoli.Count == 0,
            "Sorgenti con un byte di controllo scritto come byte:\n  " + string.Join("\n  ", colpevoli) +
            "\n\nSi scrivono come ESCAPE — `'\\u001F'`, `\"\\u001E\"`, `\"\\0\"` — e il comportamento non " +
            "cambia di un bit. Scritti come byte il file è binario per grep e per git diff, e una " +
            "normalizzazione può portarseli via in silenzio.");
    }

    private static IEnumerable<string> Sorgenti()
    {
        foreach (var cartella in new[] { "src", "tests", "tools" })
        {
            var radice = Path.Combine(Radice(), cartella);
            if (!Directory.Exists(radice)) continue;

            foreach (var estensione in Estensioni)
                foreach (var f in Directory.EnumerateFiles(radice, estensione, SearchOption.AllDirectories))
                    // `bin` e `obj` non sono sorgenti: dentro ci stanno gli assiemi, che di byte di
                    // controllo ne hanno per costruzione.
                    if (!f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                        yield return f;
        }
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src"))
                && File.Exists(Path.Combine(dir.FullName, "Vipi.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"radice del repository non trovata da {AppContext.BaseDirectory}");
    }
}

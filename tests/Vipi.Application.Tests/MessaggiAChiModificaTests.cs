using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Application.Tests;

/// <summary>
/// Ogni messaggio che finisce sotto gli occhi di <b>chi modifica</b> ha due lingue.
///
/// <para>
/// ⚠️ <b>La regola, e il confine.</b> In questo codice il tipo dell'eccezione dice a chi si parla:
/// <c>ValidationException</c> e <c>EditConflictException</c> sono <b>frasi per una persona</b> — arrivano
/// in cima all'editor e le legge un controllore — mentre <c>InvalidOperationException</c> e
/// <c>KeyNotFoundException</c> sono <b>invarianti</b>: «Sezione 41 inesistente» non dice niente a nessuno
/// e finisce nella pagina d'errore e nel registro, dove vale l'eccezione dichiarata di
/// <c>docs/design/regole-lingua.md</c> («log e diagnostica restano in italiano»).
/// </para>
///
/// <para>
/// ⚠️ <b>Perché una guardia e non solo una passata.</b> Il 28 agosto 2026 se ne sono tradotti 52 in un
/// colpo. Il cinquantatreesimo lo scriverà qualcun altro fra un mese, e non avrà nessun motivo di
/// ricordarsi di questa regola: una riga in italiano dentro un editor inglese non rompe niente, non
/// compare in nessun log, e la vede solo chi sbaglia — cioè la persona meno disposta a segnalarla.
/// </para>
///
/// <para>
/// Il controllo è <b>strutturale</b> e non linguistico: non si prova a indovinare se una stringa è
/// italiana (un elenco di parole sbaglia in tutti e due i versi), si pretende che l'argomento sia
/// <c>Lingua(...)</c> — l'unica forma che porta le due lingue. Una stringa già inglese scritta da sola
/// verrebbe segnalata lo stesso, ed è giusto: è il messaggio che manca in italiano.
/// </para>
/// </summary>
public sealed class MessaggiAChiModificaTests
{
    private readonly ITestOutputHelper _out;

    public MessaggiAChiModificaTests(ITestOutputHelper output) => _out = output;

    /// <summary>I due tipi che, in questo codice, vogliono dire «lo legge una persona».</summary>
    private static readonly Regex PerUnaPersona = new(
        @"new\s+[\w.]*(?:ValidationException|EditConflictException)\s*\(\s*(?!Lingua\()(\$?@?""[^""]*"")",
        RegexOptions.Compiled);

    /// <summary>
    /// Le eccezioni alla regola, <b>dichiarate</b>. Vuoto: se un domani ce ne fosse una, va scritta qui col
    /// suo perché — un elenco vuoto è una regola, un elenco che cresce in silenzio è una scusa.
    /// </summary>
    private static readonly string[] Ammesse = Array.Empty<string>();

    [Theory]
    [InlineData("Vipi.Application")]
    [InlineData("Vipi.Infrastructure")]
    public void Ogni_messaggio_di_validazione_porta_le_due_lingue(string progetto)
    {
        var radice = RadiceDelRepo();
        var cartella = Path.Combine(radice, "src", progetto);

        var soli = new List<string>();
        foreach (var file in Directory.EnumerateFiles(cartella, "*.cs", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(radice, file);
            if (relativo.Contains("Migrations", StringComparison.Ordinal)) continue;
            if (relativo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                relativo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            // I commenti di documentazione portano esempi di codice: non sono codice.
            var testo = Regex.Replace(File.ReadAllText(file), @"///.*", "");
            foreach (Match m in PerUnaPersona.Matches(testo))
            {
                var riga = testo[..m.Index].Count(c => c == '\n') + 1;
                var voce = $"{relativo}:{riga}";
                if (Ammesse.Contains(voce)) continue;
                soli.Add($"{voce}  {m.Groups[1].Value}");
            }
        }

        foreach (var s in soli) _out.WriteLine(s);

        Assert.True(soli.Count == 0,
            $"{soli.Count} messaggi a chi modifica hanno una lingua sola. Vanno avvolti in " +
            "`Lingua(\"italiano\", \"english\")` (using static Vipi.Application.Messaggio): li legge un " +
            "controllore, e un editor che risponde in italiano a chi ha scelto l'inglese è una schermata " +
            "mezza tradotta.\n  " + string.Join("\n  ", soli));
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}

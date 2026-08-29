using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le tabelle dei vSOP militari e la pagina dell'anagrafica radioassistenze: <b>forma</b>, non contenuto.
///
/// <para><b>Il difetto che presidia.</b> Le sei tabelle sono nate <c>cfg-table</c>, che non è una tabella
/// generica: è quella delle «Configurazioni operative», e cabla le larghezze su <b>quattro</b> colonne
/// (26/38/18/18%). Su una tabella da otto colonne — l'anagrafica — le prime quattro si prendevano tutto e
/// le altre finivano a zero; su una da tre le proporzioni erano quelle di un'altra tabella. È esattamente il
/// difetto che le SID avevano già pagato, ritrovato tale e quale un mese dopo.</para>
///
/// <para><b>E le larghezze in linea coprivano il caso a metà</b>: <c>style="width:76px"</c> vale per il
/// <c>th</c> e non per il <c>td</c>, quindi bastava una cella lunga a rimetterle in discussione. Stanno nel
/// foglio, accanto a quelle delle altre cinque tabelle — in linea, chi cambia la colonna «Coordinate» ne
/// trova quattro su cinque.</para>
///
/// <para>Si legge il <b>sorgente</b> e non il DOM per la stessa ragione di <see cref="GerarchiaTitoliTests"/>:
/// la domanda è strutturale, e renderli tutti costerebbe più di quanto il test vale.</para>
/// </summary>
public class TabelleMilitariTests
{
    /// <summary>I file che devono rispettare la regola: le tabelle militari e la pagina che le governa.</summary>
    public static TheoryData<string> File()
    {
        var dati = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Radice(), "Components", "App"), "Mil*.razor"))
            dati.Add(Path.Combine("Components", "App", Path.GetFileName(f)));
        dati.Add(Path.Combine("Pages", "AdminNavaidsPage.razor"));
        return dati;
    }

    [Theory]
    [MemberData(nameof(File))]
    public void Nessuna_tabella_militare_e_una_cfg_table(string percorso)
    {
        var testo = System.IO.File.ReadAllText(Path.Combine(Radice(), percorso));

        Assert.DoesNotContain("cfg-table", testo.Replace("`cfg-table`", ""));   // i commenti la nominano apposta
    }

    /// <summary>
    /// ⚠️ Nessuna larghezza scritta in linea. Le altre proprietà in linea non si guardano: qui il difetto
    /// pagato è la <b>larghezza</b>, che è una scelta ripetuta in sei file e cambia per tutte insieme.
    /// </summary>
    [Theory]
    [MemberData(nameof(File))]
    public void Nessuna_larghezza_scritta_in_linea(string percorso)
    {
        var testo = System.IO.File.ReadAllText(Path.Combine(Radice(), percorso));

        var larghezze = Regex.Matches(testo, @"style=""[^""]*width:[^""]*""", RegexOptions.IgnoreCase)
            .Select(m => m.Value).ToList();

        Assert.True(larghezze.Count == 0,
            $"{percorso}: larghezze scritte in linea.\n  " + string.Join("\n  ", larghezze) +
            "\n  Vanno nel foglio, nel gruppo `.res-table.mil-table` — in linea valgono per il `th` e non " +
            "per il `td`, e chi le cambia le trova solo in parte.");
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidata = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(candidata)) return candidata;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/Vipi.Ui non trovata risalendo da " + AppContext.BaseDirectory);
    }
}

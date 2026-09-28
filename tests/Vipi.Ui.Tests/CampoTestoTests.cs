using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-084 (revisione totale 3): la forma <c>value="@x" @oninput</c> fa perdere i tasti in Blazor Server — ogni tasto va
/// al server, che riscrive il campo com'era un giro prima (misurato il 5 settembre 2026 su LIRN: otto Backspace, tre
/// caratteri sopravvissuti). <see cref="CampoTesto"/> scrive nel campo solo quando il valore cambia da parte del server.
/// </summary>
public class CampoTestoTests : TestContext
{
    /// <summary>Il chiamante: tiene il valore, lo aggiorna a ogni tasto, e può svuotarlo o riscriverlo lui.</summary>
    private sealed class Chiamante
    {
        public string Valore = "";
    }

    private IRenderedComponent<CampoTesto> Monta(Chiamante c) =>
        RenderComponent<CampoTesto>(p => p
            .Add(x => x.Valore, c.Valore)
            .Add(x => x.OnInput, (ChangeEventArgs e) => { c.Valore = e.Value?.ToString() ?? ""; })
            .AddUnmatched("class", "htree-search"));

    private static void Ridisegna(IRenderedComponent<CampoTesto> cut, Chiamante c) =>
        cut.SetParametersAndRender(p => p.Add(x => x.Valore, c.Valore));

    /// <summary>Quante volte l'elemento è stato ricreato (il suo <c>@key</c>): bUnit non conserva l'identità dei nodi.</summary>
    private static int Ricreazioni(IRenderedComponent<CampoTesto> cut) =>
        (int)typeof(CampoTesto).GetField("_gen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    [Fact]
    public void Mentre_si_digita_il_server_non_riscrive_il_campo()
    {
        var c = new Chiamante();
        var cut = Monta(c);

        cut.Find("input").Input("AGNI7G");
        Ridisegna(cut, c);
        cut.Find("input").Input("AGN");      // tre Backspace
        Ridisegna(cut, c);

        Assert.Equal("AGN", c.Valore);
        // Il value del DOM resta quello scritto dal server all'apertura: il testo è del browser.
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
        Assert.Equal("htree-search", cut.Find("input").GetAttribute("class"));
    }

    /// <summary>Un ridisegno del chiamante col valore di un giro prima (una risposta in ritardo) non tocca niente.</summary>
    [Fact]
    public void Un_ridisegno_col_valore_vecchio_non_riscrive()
    {
        var c = new Chiamante();
        var cut = Monta(c);
        cut.Find("input").Input("AB");
        cut.SetParametersAndRender(p => p.Add(x => x.Valore, ""));   // il chiamante non ha ancora visto «AB»

        Assert.Equal(0, Ricreazioni(cut));
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>Il ✕ che svuota: il server scrive «» come all'apertura, e il diff non lo vedrebbe. L'elemento si ricrea.</summary>
    [Fact]
    public void Svuotare_dal_server_ricrea_il_campo_vuoto()
    {
        var c = new Chiamante();
        var cut = Monta(c);
        cut.Find("input").Input("LIRF");
        Ridisegna(cut, c);

        c.Valore = "";
        Ridisegna(cut, c);

        Assert.Equal(1, Ricreazioni(cut));
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>Una scelta da un elenco: il valore nuovo si scrive, e l'elemento resta lo stesso (il fuoco non si perde).</summary>
    [Fact]
    public void Un_valore_nuovo_dal_server_si_scrive_senza_ricreare()
    {
        var c = new Chiamante();
        var cut = Monta(c);
        cut.Find("input").Input("LIR");
        Ridisegna(cut, c);

        c.Valore = "LIRF Fiumicino";
        Ridisegna(cut, c);

        Assert.Equal(0, Ricreazioni(cut));
        Assert.Equal("LIRF Fiumicino", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>
    /// 🔴 La guardia: nessun campo del prodotto torna alla forma che perde i tasti. Un numero che si salva usa
    /// <c>@onchange</c>; un testo che si cerca o si scrive usa <see cref="CampoTesto"/>.
    /// </summary>
    [Fact]
    public void Nessun_campo_ha_value_e_oninput_insieme()
    {
        var radice = Radice();
        var trovati = new List<string>();
        foreach (var file in Directory.EnumerateFiles(radice, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.EndsWith("CampoTesto.razor", StringComparison.Ordinal)) continue;
            var s = File.ReadAllText(file);
            foreach (var (inizio, tag) in Tag(s))
                if (Regex.IsMatch(tag, @"\bvalue=""@") && tag.Contains("@oninput", StringComparison.Ordinal))
                    trovati.Add($"{Path.GetRelativePath(radice, file)}:{s[..inizio].Count(ch => ch == '\n') + 1}");
        }

        Assert.True(trovati.Count == 0,
            "Campi con value=\"@…\" e @oninput sullo stesso elemento (perdono i tasti, U-084):\n  " + string.Join("\n  ", trovati) +
            "\nUsare <CampoTesto Valore=… OnInput=…>, o @onchange per un valore che si salva.");
    }

    /// <summary>I tag input e textarea letti rispettando le virgolette: dentro un attributo «=>» non chiude il tag.</summary>
    private static IEnumerable<(int Inizio, string Tag)> Tag(string s)
    {
        foreach (Match m in Regex.Matches(s, @"<(input|textarea)\b"))
        {
            char? q = null;
            for (var i = m.Index + m.Length; i < s.Length; i++)
            {
                var c = s[i];
                if (q is not null) { if (c == q) q = null; }
                else if (c is '"' or '\'') q = c;
                else if (c == '>') { yield return (m.Index, s[m.Index..(i + 1)]); break; }
            }
        }
    }

    private static string Radice()
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

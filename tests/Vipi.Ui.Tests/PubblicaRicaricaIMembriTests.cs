using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 <b>Dopo «Pubblica», un editor unito rilegge anche i MEMBRI.</b>
///
/// <para>La pubblicazione è accoppiata: promuove la bozza di ogni documento dell'unione e molla i lock. Fino al 25
/// settembre 2026 le tre pagine con l'unione ricaricavano solo l'ospite: i membri restavano «in modifica» sulla
/// versione di prima, che bozza non era più, e il gesto successivo — congelare una sezione resa live un attimo
/// prima — finiva in «Modifica consentita solo su una versione in bozza». Segnalato dal campo su Catania.</para>
///
/// <para>⚠️ Presidio sul sorgente, e su TUTTE le pagine che montano insieme <c>UnionMembersEditor</c> e
/// <c>ReleasePanel</c>: il difetto stava identico in tre, e una quarta nata copiando una di loro lo erediterebbe.
/// Con il sorgente di prima fallisce su tutte e tre.</para>
/// </summary>
public sealed class PubblicaRicaricaIMembriTests
{
    private static List<string> Trovate() =>
        Directory.GetFiles(Path.Combine(Radice(), "Pages"), "*.razor")
            .Where(f => File.ReadAllText(f) is var t && t.Contains("<UnionMembersEditor") && t.Contains("<ReleasePanel"))
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    public static TheoryData<string> PagineUnite()
    {
        var dati = new TheoryData<string>();
        foreach (var p in Trovate()) dati.Add(p);
        return dati;
    }

    [Fact]
    public void Le_pagine_unite_sono_quelle_che_ci_aspettiamo()
    {
        // ⚠️ Il rovescio: se la ricerca qui sopra smettesse di trovarle, la teoria resterebbe verde su niente.
        Assert.Equal(new[] { "AeroportoEditorPage", "AppEditorPage", "MilEditorPage" }, Trovate());
    }

    [Theory]
    [MemberData(nameof(PagineUnite))]
    public void Dopo_la_pubblicazione_si_ricaricano_anche_i_membri(string pagina)
    {
        var testo = File.ReadAllText(Path.Combine(Radice(), "Pages", pagina + ".razor"));

        var gestore = Regex.Match(testo, @"<ReleasePanel\b[^>]*\bPublished=""(\w+)""", RegexOptions.Singleline);
        Assert.True(gestore.Success, $"{pagina}: <ReleasePanel> senza Published.");

        var corpo = Corpo(testo, gestore.Groups[1].Value);
        Assert.True(corpo.Contains("_membri") && corpo.Contains("RicaricaAsync("),
            $"{pagina}: dopo «Pubblica» ({gestore.Groups[1].Value}) i membri dell'unione non si rileggono, e restano " +
            "«in modifica» su una versione che bozza non è più.");
    }

    /// <summary>Il corpo del metodo, dalla firma alla graffa che chiude alla sua rientranza. Un metodo scritto
    /// come espressione (<c>=&gt;</c>) finisce al primo punto e virgola.</summary>
    private static string Corpo(string testo, string nome)
    {
        var firma = Regex.Match(testo, $@"private (async )?Task {nome}\(\)");
        Assert.True(firma.Success, $"Metodo non trovato: {nome}");
        var dopo = testo[firma.Index..];
        var freccia = dopo.IndexOf("=>", StringComparison.Ordinal);
        var graffa = dopo.IndexOf('{');
        if (freccia >= 0 && (graffa < 0 || freccia < graffa)) return dopo[..(dopo.IndexOf(';') + 1)];
        return dopo[..dopo.IndexOf("\n    }", StringComparison.Ordinal)];
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

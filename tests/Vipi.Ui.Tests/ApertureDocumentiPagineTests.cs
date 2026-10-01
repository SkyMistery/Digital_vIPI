using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Chi conta le aperture e chi le legge (S93, carta <c>docs/feature/2026-10-01-aperture-documenti.md</c>).
/// <para>⚠️ Sono guardie sul sorgente, e di proposito: una pagina che smette di contare non dà nessun errore — la sua
/// riga sulla pagina dell'ACC scende piano piano, e nessuno saprebbe perché.</para>
/// </summary>
public class ApertureDocumentiPagineTests
{
    /// <summary>Le quattro pagine pubbliche contano, e contano SOLO la vista pubblica: bozze e anteprime le apre chi
    /// scrive, e gonfierebbero proprio i documenti in lavorazione.</summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor", "ReleaseTargetType.Airport")]
    [InlineData("Pages/MilDocumentPage.razor", "ReleaseTargetType.AirportMil")]
    [InlineData("Pages/AppnPage.razor", "ReleaseTargetType.App")]
    [InlineData("Pages/VloaListPage.razor", "ReleaseTargetType.Vloa")]
    public void Ogni_pagina_pubblica_conta_solo_la_vista_pubblica(string relativo, string tipo)
    {
        var righe = Leggi(relativo).Split('\n');
        var chiamate = righe.Select((r, i) => (r, i)).Where(x => x.r.Contains("Aperture.SegnaAsync(")).ToList();

        var (riga, indice) = Assert.Single(chiamate);
        Assert.Contains(tipo + ",", riga, StringComparison.Ordinal);
        // La condizione sta sulla stessa riga o su quella prima.
        var condizione = riga + (indice > 0 ? righe[indice - 1] : "");
        Assert.Matches(new Regex(@"[Mm]ode\.Kind == PreviewKind\.Public"), condizione);
    }

    /// <summary>La pagina dell'ACC sceglie i suoi tre con la regola che ha i test, non con un ordinamento suo.</summary>
    [Fact]
    public void La_pagina_dell_ACC_usa_la_regola_dei_primi_per_tutti_e_tre_i_gruppi()
    {
        var sorgente = Leggi("Pages/AccLanding.razor");

        Assert.Equal(3, Regex.Matches(sorgente, @"ApertureDocumenti\.Primi\(").Count);
        Assert.DoesNotContain(".Take(3)", sorgente, StringComparison.Ordinal);
    }

    private static string Leggi(string relativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui")))
            dir = dir.Parent;
        var radice = dir?.FullName ?? throw new DirectoryNotFoundException("Radice del repo non trovata.");
        return File.ReadAllText(Path.Combine(radice, "src", "Vipi.Ui", relativo.Replace('/', Path.DirectorySeparatorChar)));
    }
}

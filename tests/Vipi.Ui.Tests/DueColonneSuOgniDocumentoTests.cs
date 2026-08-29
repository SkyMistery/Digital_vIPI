namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Un documento pubblico si legge in tre colonne.</b> Indice a sinistra, testo al centro, riquadro dei
/// collegamenti a destra: è la forma della lettura in questo prodotto, e vale per <i>tutte</i> e cinque le
/// famiglie — vIPI ACC, vIPI d'aeroporto, APP non remotizzato, vLOA, vSOP militare.
///
/// <para><b>Perché un test sul SORGENTE e non un render.</b> Il rail di destra compare solo sopra i 1500px
/// (<c>vipi-theme.css</c>) e l'indice è sticky: un test di render dovrebbe simulare una viewport per vedere
/// qualcosa, e finirebbe per provare il CSS invece della pagina. Qui la domanda è più semplice e più
/// stabile: <b>le tre colonne sono state scritte?</b></para>
///
/// <para>⚠️ Il difetto che questo test presidia è reale, non ipotetico. Fino al 29 agosto 2026 il vSOP
/// militare era l'unico dei cinque senza <c>doc-layout</c>: aveva nove sezioni e nessun indice per
/// raggiungerle, e il ponte verso l'edizione civile stava solo nella testata. Nessuno se n'era accorto
/// perché ogni viewer si legge da solo, e da solo sembrava a posto.</para>
/// </summary>
public sealed class DueColonneSuOgniDocumentoTests
{
    /// <summary>
    /// I cinque viewer di documento pubblico, col percorso relativo a <c>src/Vipi.Ui</c>.
    /// <para>⚠️ La vLOA non è una pagina ma un COMPONENTE (<c>VloaDocumentView</c>): la sua rotta pubblica e
    /// l'anteprima di release lo montano entrambe, ed è lì che sta il suo markup. Cercare solo dentro
    /// <c>Pages/</c> l'avrebbe saltata in silenzio — che è il modo in cui un elenco cablato smette di
    /// valere senza dirlo.</para>
    /// </summary>
    public static TheoryData<string> Viewer() => new()
    {
        "Pages/AccVipiPage.razor",
        "Pages/AeroportoPage.razor",
        "Pages/AppnPage.razor",
        "Pages/MilDocumentPage.razor",
        "Components/VloaDocumentView.razor",
    };

    [Theory]
    [MemberData(nameof(Viewer))]
    public void Ogni_documento_pubblico_ha_le_tre_colonne(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("doc-layout", sorgente, StringComparison.Ordinal);
        Assert.True(HaUnIndice(sorgente), $"{relativo}: nessun indice, ne' <DocumentToc> ne' un aside .toc scritto a mano");
        Assert.Contains("doc-rail", sorgente, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <c>reading-cap</c> (un tetto di 1180px) e <c>doc-layout</c> non convivono: la griglia a tre colonne
    /// chiede tutta la larghezza, e <c>.wrap:has(.doc-layout){max-width:none}</c> gliela dà — lasciando la
    /// classe a dichiarare un limite che non vale più. Due regole che dicono cose diverse sullo stesso
    /// elemento sono un difetto che aspetta chi cambierà una delle due.
    ///
    /// <para>⚠️ Si guarda il contenitore PIÙ VICINO, non tutto il file: <c>AeroportoPage</c> è due schermate
    /// in una — l'elenco degli aeroporti e il documento — e l'elenco un tetto di lettura ce l'ha, a
    /// ragione. Un test che cercasse la stringa ovunque direbbe di toglierlo di là.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Viewer))]
    public void Il_contenitore_delle_tre_colonne_non_porta_anche_il_tetto_di_lettura(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

        var colonne = sorgente.IndexOf("doc-layout", StringComparison.Ordinal);
        Assert.True(colonne > 0, "doc-layout non trovato");

        var wrap = sorgente.LastIndexOf("class=\"wrap", colonne, StringComparison.Ordinal);
        if (wrap < 0) return;   // la vLOA è un componente: il `wrap` glielo mette la pagina che la monta

        var fine = sorgente.IndexOf('"', wrap + "class=\"".Length);
        var classi = sorgente[(wrap + "class=\"".Length)..fine];
        Assert.DoesNotContain("reading-cap", classi, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'indice c'e': o il componente condiviso <c>DocumentToc</c>, o un <c>aside class="toc"</c> scritto in
    /// pagina. ⚠️ La seconda forma non e' un residuo da togliere: la vIPI <b>ACC</b> ha un indice raggruppato
    /// per BLOCCO (Aerovia, gruppi APP) e non per sezione, quindi il componente condiviso non la descrive —
    /// la stessa ragione per cui non passa nemmeno da <c>DocumentSectionsView</c>.
    /// </summary>
    private static bool HaUnIndice(string sorgente) =>
        sorgente.Contains("<DocumentToc", StringComparison.Ordinal)
        || sorgente.Contains("class=\"toc\"", StringComparison.Ordinal);

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

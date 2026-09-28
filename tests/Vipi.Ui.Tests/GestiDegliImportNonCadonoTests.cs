using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-029 (revisione totale 3): i gesti che parlano con la sorgente IVAO — «Importa da IVAO» e le aree nella pagina
/// ACC, «Assegna aeroporti noti» nella pagina Aeroporti — non fanno cadere il circuito e non partono due volte.
///
/// <para><b>Il difetto.</b> I gestori prendevano solo <c>EditNotAllowedException</c>, <c>InvalidOperationException</c>
/// (e a volte <c>ValidationException</c>). Un timeout IVAO (<c>TaskCanceledException</c>), un token rifiutato
/// (<c>HttpRequestException</c>), una pagina HTML con 200 (<c>JsonException</c>), una collisione col giro notturno
/// (<c>DbUpdateException</c>) uscivano dal gestore: «An error has occurred», pagina da ricaricare, import a metà. E
/// senza guardia prima dell'await il doppio clic lanciava due import sullo stesso DbContext.</para>
///
/// <para>⚠️ <b>Perché un presidio sul testo.</b> Le due pagine tirano una dozzina di servizi e il lock della
/// struttura: la prova di comportamento sta dal vivo (voce S21 di <c>docs/filoni/sito.md</c>: import con IVAO
/// irraggiungibile, che sul codice di prima fa cadere il circuito). Qui si tiene ferma la forma della porta.</para>
/// </summary>
public class GestiDegliImportNonCadonoTests
{
    [Theory]
    [InlineData("AccAdminPage.razor", "Gesto")]
    [InlineData("AeroportiPage.razor", "Guarded")]
    public void La_porta_dei_gesti_ha_la_guardia_e_prende_ogni_eccezione(string pagina, string porta)
    {
        var corpo = CorpoDi(Leggi(pagina), porta);

        var guardia = corpo.IndexOf("if (_busy) return;", StringComparison.Ordinal);
        var attesa = corpo.IndexOf("await ", StringComparison.Ordinal);
        Assert.True(guardia >= 0 && guardia < attesa, $"{pagina}: la guardia `_busy` deve stare PRIMA del primo await.");
        Assert.Matches(@"catch \(Exception ex\)", corpo);
    }

    /// <summary>Nella pagina ACC ogni gesto che scrive o chiama la sorgente passa dalla porta: nessun gestore con i
    /// suoi catch parziali accanto.</summary>
    [Theory]
    [InlineData("Import")]
    [InlineData("ImportAreas")]
    [InlineData("SetAreasEnabled")]
    [InlineData("SetHidden")]
    [InlineData("SetSubHidden")]
    [InlineData("SaveLimits")]
    public void I_gesti_della_pagina_ACC_passano_dalla_porta(string gestore)
    {
        var s = Leggi("AccAdminPage.razor");
        Assert.Matches(new Regex($@"private Task {gestore}\([^)]*\)\s*=>\s*(\r?\n\s*)?Gesto\("), s);
    }

    /// <summary>
    /// 🔴 U-174 (revisione totale 3): i due gesti della pagina ACC che restavano fuori dalla porta. Il clic su una
    /// riga ACC legge le sue aree dal DbContext del circuito, e restava cliccabile mentre un import scriveva; «Salva
    /// limiti (N)» (anche da «Fine modifica») partiva sopra un import in volo. Stessa guardia della porta, prima del
    /// primo await, e nessuna eccezione che esca dal gestore.
    /// </summary>
    [Theory]
    [InlineData("TogglePick")]
    [InlineData("SaveAllLimits")]
    public void I_gesti_ACC_fuori_dalla_porta_hanno_la_stessa_guardia(string gestore)
    {
        var m = Regex.Match(Leggi("AccAdminPage.razor"), $@"private async Task {gestore}\([^)]*\)\s*\{{(?<c>.*?)\n    \}}",
            RegexOptions.Singleline);
        Assert.True(m.Success, $"{gestore} non trovato");
        var corpo = m.Groups["c"].Value;

        var guardia = corpo.IndexOf("if (_busy) return;", StringComparison.Ordinal);
        var attesa = corpo.IndexOf("await ", StringComparison.Ordinal);
        Assert.True(guardia >= 0 && guardia < attesa, $"{gestore}: la guardia `_busy` deve stare PRIMA del primo await.");
        Assert.Matches(@"catch \(Exception ex\)", corpo);
    }

    private static string CorpoDi(string sorgente, string metodo)
    {
        var m = Regex.Match(sorgente, $@"private async Task {metodo}\(Func<Task> [a-z]+(, string [a-z]+)?\)\s*\{{(?<c>.*?)\n    \}}",
            RegexOptions.Singleline);
        Assert.True(m.Success, $"{metodo} non trovato");
        return m.Groups["c"].Value;
    }

    private static string Leggi(string pagina) =>
        File.ReadAllText(Path.Combine(Radice(), "Pages", pagina)).Replace("\r\n", "\n");

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

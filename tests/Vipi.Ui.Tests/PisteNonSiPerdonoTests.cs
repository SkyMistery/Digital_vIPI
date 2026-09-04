using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Le celle TORA/LDA non devono potersi perdere.</b>
///
/// <para>Il guasto, visto su LIPR il 4 settembre 2026: erano stati scritti TORA e LDA sulle piste, poi si è
/// premuto «Re-import from IVAO», e sono spariti senza una parola. La catena era questa — la tabella piste
/// non ha un salvataggio automatico ma un bottone, <c>Reimport()</c> chiedeva conferma <b>solo</b> per le SID
/// non salvate, e subito dopo <c>LoadAsync()</c> ricarica <b>ogni</b> buffer dal database e azzera
/// <c>_dirtySections</c>. Le celle scritte e non salvate se ne andavano insieme al flag che diceva che
/// c'erano.</para>
///
/// <para>⚠️ <b>Perché guardie strutturali e non un banco di prova.</b> Quel che va difeso è un <b>ordine fra
/// tre cose</b> — si chiede, poi si importa, poi si ricarica — e un componente montato da solo direbbe che
/// fa quel che il suo codice dice, che era vero anche col difetto dentro. Il test che serve diventa rosso
/// quando qualcuno toglie la domanda o l'auto-salvataggio.</para>
///
/// <para>⚠️ E c'è una ragione in più perché l'auto-salvataggio non sia un lusso: in produzione Passenger
/// rigenera il processo ogni ~50 secondi. Un circuito che muore mentre si scrive porta via tutto quel che
/// non è ancora in tabella, senza bisogno di nessun bottone.</para>
/// </summary>
public class PisteNonSiPerdonoTests
{
    private static string Aeroporto() => Sorgente("Components/Doc/AirportSectionsEditor.razor");
    private static string Militare() => Sorgente("Components/Doc/MilSectionsEditor.razor");

    /// <summary>
    /// Il re-import chiede prima di buttare via QUALUNQUE sezione toccata, non solo le SID. È la domanda che
    /// mancava: <c>_sidNonSalvate</c> c'era, <c>_dirtySections</c> no.
    /// </summary>
    [Fact]
    public void Il_reimport_chiede_prima_di_scartare_le_sezioni_non_salvate()
    {
        var s = Aeroporto();
        var reimport = s[s.IndexOf("private async Task Reimport()", StringComparison.Ordinal)..];
        var fino = reimport[..reimport.IndexOf("await LoadAsync();", StringComparison.Ordinal)];

        Assert.Contains("_dirtySections.Count > 0", fino);
        Assert.Contains("Ape_ReimportDiscardConfirm", fino);
        Assert.Contains("return;", fino);
    }

    /// <summary>
    /// A piste di sorgente ogni gesto sulla tabella si salva da solo. ⚠️ <b>Solo</b> lì: a policy spenta si
    /// aggiungono righe a mano e una riga a metà — ident ancora vuoto — non deve tentare di salvarsi.
    /// </summary>
    [Theory]
    [InlineData("PisteCambiate")]
    public void Le_piste_si_salvano_a_ogni_gesto_ma_solo_a_sorgente_bloccata(string metodo)
    {
        foreach (var s in new[] { Aeroporto(), Militare() })
        {
            Assert.Contains($"OnChanged=\"{metodo}\"", s);
            var corpo = s[s.IndexOf($"private async Task {metodo}()", StringComparison.Ordinal)..];
            corpo = corpo[..corpo.IndexOf("\n    }", StringComparison.Ordinal)];
            Assert.Contains("if (_policy.Runways)", corpo);
        }
    }

    /// <summary>Le orfane si mostrano DOPO il ricaricamento: prima verrebbero azzerate da <c>LoadAsync</c>,
    /// che è esattamente il modo in cui si erano persi i dati che ora si segnalano.</summary>
    [Fact]
    public void Le_orfane_si_posano_sopra_il_ricaricamento_non_prima()
    {
        var s = Aeroporto();
        var reimport = s[s.IndexOf("private async Task Reimport()", StringComparison.Ordinal)..];

        var load = reimport.IndexOf("await LoadAsync();", StringComparison.Ordinal);
        var orfane = reimport.IndexOf("_rwyOrfane = piste?", StringComparison.Ordinal);

        Assert.True(load >= 0 && orfane > load, "«_rwyOrfane» va assegnato DOPO «await LoadAsync()».");
    }

    private static string Sorgente(string relativo) => File.ReadAllText(Path.Combine(Radice(), relativo));

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

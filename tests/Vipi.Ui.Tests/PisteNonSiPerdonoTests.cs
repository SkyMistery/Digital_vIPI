using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Le celle TORA/LDA non devono potersi perdere.</b>
///
/// <para>Il guasto, visto su LIPR il 4 settembre 2026: erano stati scritti TORA e LDA sulle piste, poi si è
/// premuto «Re-import from IVAO», e sono spariti senza una parola. La catena era questa — la tabella piste
/// non aveva un salvataggio automatico ma un bottone, e subito dopo <c>LoadAsync()</c> ricarica <b>ogni</b>
/// buffer dal database. Le celle scritte e non salvate se ne andavano senza essere mai passate per il
/// server.</para>
///
/// <para>⚠️ <b>La cura è cambiata lo stesso giorno</b> (carta 2026-09-04-aeroporto-porta-sola): era una
/// domanda prima del re-import più un auto-salvataggio limitato alle piste di sorgente; ora è che <b>ogni
/// gesto scrive</b>, in tutte le sezioni e in tutt'e due gli editor. Non c'è più un buffer da difendere, e
/// la guardia diventa questa: che il salvataggio non torni a essere condizionato.</para>
///
/// <para>⚠️ <b>Perché guardie strutturali e non un banco di prova.</b> Quel che va difeso è un <b>ordine fra
/// due cose</b> — prima si importa, poi si ricarica — e un componente montato da solo direbbe che fa quel che
/// il suo codice dice, che era vero anche col difetto dentro.</para>
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
    /// Il re-import chiede prima di rifare piste e quote: è una riscrittura, e va confermata. ⚠️ Le altre due
    /// domande che stavano qui — «hai SID non salvate?», «hai sezioni non salvate?» — sono cadute con i buffer
    /// che annunciavano: quel che si vede a schermo è già in archivio.
    /// </summary>
    [Fact]
    public void Il_reimport_chiede_conferma_prima_di_riscrivere()
    {
        var s = Aeroporto();
        var reimport = s[s.IndexOf("private async Task Reimport()", StringComparison.Ordinal)..];
        var fino = reimport[..reimport.IndexOf("await LoadAsync();", StringComparison.Ordinal)];

        Assert.Contains("Ape_ReimportConfirm", fino);
        Assert.Contains("return;", fino);
    }

    /// <summary>
    /// Le piste si salvano a OGNI gesto, in tutt'e due gli editor, e senza condizioni.
    ///
    /// <para>⚠️ Fino al 4 settembre 2026 l'auto-salvataggio valeva solo a piste di sorgente
    /// (<c>if (_policy.Runways)</c>): a policy spenta si aggiungono righe a mano, e una riga a metà — ident
    /// ancora vuoto — non deve tentare di salvarsi a ogni tasto. Quel «ma solo» è caduto perché il caso della
    /// riga incompleta lo tiene ora <c>AirportSaveGate</c>, che guarda il DATO e non la policy.</para>
    /// </summary>
    [Fact]
    public void Le_piste_si_salvano_a_ogni_gesto()
    {
        foreach (var (s, metodo) in new[] { (Aeroporto(), "SaveRwys"), (Militare(), "SalvaPiste") })
        {
            Assert.Contains($"RowsChanged=\"{metodo}\"", s);
            Assert.Contains($"Task {metodo}() => AirportSaveGate.Runways(_rwys)", s);
            Assert.DoesNotContain("if (_policy.Runways) await", s);
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

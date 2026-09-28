using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-144 (revisione totale 3): la porta dei gesti di <c>/services/vsop/versions</c> — <c>Run</c> — prendeva
/// solo le eccezioni di dominio e non guardava se un gesto era già in volo. Una <c>DbUpdateException</c> (due
/// pubblicazioni dello stesso bersaglio nello stesso istante urtano l'indice unico) usciva dal gestore e abbatteva
/// il circuito: «Attempting to reconnect». <c>ReleasePanel.Run</c> ha avuto le stesse due righe con U-017.
///
/// <para>Si legge il sorgente perché la pagina non ha un banco bUnit (quindici servizi iniettati): è la stessa
/// scelta di <see cref="FiltroPerTipoCompletoTests"/>. Si legge il <b>solo corpo</b> di <c>Run</c>, non la pagina,
/// che di <c>catch (Exception</c> ne ha altri per altre ragioni.</para>
/// </summary>
public class GestiDellaPaginaVersioniTests
{
    private static string CorpoDiRun()
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Pages", "VersioniPage.razor"));
        var inizio = sorgente.IndexOf("private async Task Run(Func<Task> action, string ok)", StringComparison.Ordinal);
        Assert.True(inizio > 0, "la dichiarazione di Run non e' stata trovata in VersioniPage.razor");
        var fine = sorgente.IndexOf("\n    }", inizio, StringComparison.Ordinal);
        return sorgente[inizio..fine];
    }

    [Fact]
    public void Run_non_riparte_mentre_un_gesto_e_in_volo()
    {
        var corpo = CorpoDiRun();
        var sentinella = corpo.IndexOf("if (_busy) return;", StringComparison.Ordinal);
        Assert.True(sentinella > 0, "manca la sentinella in testa a Run");
        Assert.True(sentinella < corpo.IndexOf("_busy = true", StringComparison.Ordinal),
            "la sentinella deve venire prima di accendere _busy");
    }

    [Fact]
    public void Run_non_lascia_uscire_un_eccezione_imprevista()
    {
        Assert.Contains("catch (Exception ex)", CorpoDiRun());
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

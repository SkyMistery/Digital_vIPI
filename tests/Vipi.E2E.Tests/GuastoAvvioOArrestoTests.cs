using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Un guasto all'AVVIO e un guasto all'ARRESTO non sono la stessa cosa, e fino al 3 settembre 2026
/// finivano nello stesso file con la stessa intestazione: «l'avvio è FALLITO».
///
/// <para>Il motivo è strutturale e non si vede leggendo <c>Program.cs</c>: <c>app.Run()</c> blocca fino
/// allo spegnimento, quindi <b>qualunque</b> eccezione dell'arresto esce dal medesimo <c>catch</c>.
/// Il 3 settembre 2026 è successo sul server vero — un processo acceso da un'ora e cinquanta è morto
/// chiudendo, e il foglio d'aggiornamento appena spedito diceva «se compare avvio-errore.txt,
/// fermatevi». Un'ora persa a cercare un difetto nella versione appena caricata, che non c'entrava.</para>
///
/// <para>⚠️ Qui non si prova un comportamento dell'host: si prova la <b>sola riga che decide</b>, nei due
/// stati, senza farlo partire. Farlo partire non servirebbe — dentro un host avviato il flag è già vero e
/// il caso «avvio fallito» non sarebbe riproducibile.</para>
/// </summary>
public sealed class GuastoAvvioOArrestoTests
{
    [Fact]
    public void Prima_che_l_host_parta_il_guasto_e_un_avvio_fallito()
    {
        Assert.Equal(StartupDiagnostics.CrashFileName, StartupDiagnostics.FileDelGuasto(false));

        var testo = StartupDiagnostics.Descrivi(new InvalidOperationException("boom"), avvioRiuscito: false);

        Assert.Contains("l'avvio è FALLITO", testo, StringComparison.Ordinal);
        Assert.Contains("boom", testo, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ La parte che conta: dopo <c>ApplicationStarted</c> il file cambia NOME. Se restasse
    /// <c>avvio-errore.txt</c>, il consiglio scritto su ogni foglio d'aggiornamento («non deve esistere»)
    /// tornerebbe a essere falso per lo spegnimento più comune di questo hosting.
    /// </summary>
    [Fact]
    public void Dopo_l_avvio_il_guasto_e_un_arresto_e_va_su_un_altro_file()
    {
        Assert.Equal(StartupDiagnostics.ShutdownFileName, StartupDiagnostics.FileDelGuasto(true));
        Assert.NotEqual(StartupDiagnostics.CrashFileName, StartupDiagnostics.ShutdownFileName);

        var testo = StartupDiagnostics.Descrivi(new InvalidOperationException("boom"), avvioRiuscito: true);

        Assert.DoesNotContain("l'avvio è FALLITO", testo, StringComparison.Ordinal);
        Assert.Contains("DOPO essere partito", testo, StringComparison.Ordinal);
        Assert.Contains("avvii.txt", testo, StringComparison.Ordinal);
        Assert.Contains("boom", testo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lo stack trace intero deve restare in tutti e due i casi: è il contenuto per cui il file esiste, ed
    /// è anche il motivo per cui la cartella <c>diagnostica/</c> non va servita dal web.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lo_stack_trace_c_e_in_tutti_e_due_i_casi(bool avvioRiuscito)
    {
        Exception preso;
        try { throw new InvalidOperationException("con stack"); }
        catch (Exception ex) { preso = ex; }

        var testo = StartupDiagnostics.Descrivi(preso, avvioRiuscito);

        Assert.Contains("Lo_stack_trace_c_e_in_tutti_e_due_i_casi", testo, StringComparison.Ordinal);
    }
}

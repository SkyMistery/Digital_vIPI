using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il cronometro delle fasi d'avvio.
///
/// <para>Esiste per una domanda che su questo host non aveva risposta — «ci mette tanto a ripartire, ma
/// tanto DOVE?» — e la risposta ha cambiato una decisione: la compilazione anticipata del pacchetto
/// (ReadyToRun) è stata provata e scartata perché 1 172 ms su ~1 300 sono database, non compilazione.
/// Vedi il commento in <c>Vipi.Host.csproj</c>.</para>
/// </summary>
public sealed class CronometroAvvioTests
{
    /// <summary>
    /// ⚠️ La cosa che il cronometro NON deve mai fare è impedire un avvio. Qui si chiama <c>Scrivi</c>
    /// senza aver segnato nulla, che è il caso in cui un errore di programmazione lo lascerebbe vuoto:
    /// dev'essere un non-fare, non un'eccezione sul percorso critico.
    /// </summary>
    [Fact]
    public void Un_cronometro_senza_fasi_non_scrive_e_non_solleva()
    {
        var crono = new StartupDiagnostics.CronometroAvvio();

        crono.Scrivi();   // niente da scrivere: non deve succedere niente
    }

    /// <summary>Le fasi escono nell'ordine in cui sono state segnate, con la loro durata.</summary>
    [Fact]
    public void Le_fasi_si_segnano_in_ordine_e_scrivere_non_solleva()
    {
        var crono = new StartupDiagnostics.CronometroAvvio();

        crono.Segna("prima");
        crono.Segna("seconda");

        crono.Scrivi();
    }

    /// <summary>
    /// E l'avvio vero lascia il proprio riepilogo nel file di diagnostica: è l'unico posto in cui, su un
    /// host senza shell e senza log, si può leggere dove è andato il tempo.
    ///
    /// <para>⚠️ <b>Questo test vuole essere solo.</b> Legge un file che è uno solo per processo e che ogni
    /// avvio riscrive da capo: se un altro host parte fra la scrittura e la rilettura, quel che rilegge non
    /// è più suo. È il rosso intermittente di lavori-aperti Q5, chiuso serializzando l'intero progetto —
    /// vedi <c>ParallelismoDelProgetto.cs</c>. Chi rimettesse il parallelismo lo riaprirebbe.</para>
    /// </summary>
    [Fact]
    public void Lavvio_vero_lascia_il_riepilogo_nel_file_di_diagnostica()
    {
        using var fabbrica = new SmokeTests.VipiAppFactory();
        fabbrica.CreateClient();   // forza la costruzione dell'host

        var percorso = Path.Combine(AppContext.BaseDirectory,
            StartupDiagnostics.CartellaDiagnostica, StartupDiagnostics.InfoFileName);

        Assert.True(File.Exists(percorso), $"nessun file di diagnostica in {percorso}");

        // Il messaggio dice DOVE guardare se ricapita: senza, un rosso qui sembra un difetto del cronometro
        // mentre quasi sempre è un secondo avvio nella stessa finestra.
        var contenuto = File.ReadAllText(percorso);
        Assert.True(contenuto.Contains("Durata delle fasi d'avvio", StringComparison.Ordinal),
            $"il file {percorso} c'è ma non porta il riepilogo delle fasi. Le due cause, in ordine: (1) un "
            + "ALTRO avvio ha riscritto il file fra il nostro e questa lettura — il parallelismo del progetto "
            + "è spento apposta (ParallelismoDelProgetto.cs), controllare che lo sia ancora; (2) "
            + "CronometroAvvio.Scrivi() non è più chiamato in VipiStartup prima di app.Run()."
            + Environment.NewLine + "--- il file riletto ---" + Environment.NewLine + contenuto);
    }
}

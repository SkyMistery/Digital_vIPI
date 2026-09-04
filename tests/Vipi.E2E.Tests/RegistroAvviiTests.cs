using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il registro degli avvii — la misura che serviva per rispondere a «il sito ogni tanto si stacca» con un
/// numero invece che con un'ipotesi.
///
/// <para>La domanda vera non è «quando è ripartito», che <c>avvio-diagnostica.txt</c> diceva già: è
/// <b>quante volte</b> e <b>come è morto quello di prima</b>. Su questo hosting un processo che si spegne
/// per inattività è normale (Passenger), uno che muore male no — e producono lo stesso identico sintomo
/// nel browser. Qui si prova la sola riga che li distingue.</para>
/// </summary>
public sealed class RegistroAvviiTests
{
    private static readonly DateTime Adesso = new(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>Nessuna riga prima: non c'è niente da giudicare, e il verdetto non deve inventare.</summary>
    [Fact]
    public void Il_primo_avvio_non_accusa_nessuno()
    {
        var verdetto = RegistroAvvii.Verdetto(null, Adesso);

        Assert.Contains("primo avvio", verdetto, StringComparison.Ordinal);
        Assert.DoesNotContain("⚠", verdetto, StringComparison.Ordinal);
    }

    /// <summary>
    /// Un ARRESTO prima dell'AVVIO è lo spegnimento per inattività: fisiologico qui, e il verdetto non deve
    /// suonare come un guasto — altrimenti chi legge il file va a cercare un difetto che non c'è.
    /// </summary>
    [Fact]
    public void Un_arresto_ordinato_non_e_un_allarme()
    {
        var verdetto = RegistroAvvii.Verdetto((Adesso.AddHours(-3), true), Adesso);

        Assert.Contains("in modo ordinato", verdetto, StringComparison.Ordinal);
        Assert.Contains("03:00:00", verdetto, StringComparison.Ordinal);
        Assert.DoesNotContain("⚠", verdetto, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ Il caso che vale il file intero: due AVVIO di fila. Nessuno ha chiuso in modo ordinato, quindi il
    /// processo precedente è morto male — crash, memoria esaurita, o una dll sovrascritta via FTP mentre
    /// era in uso (successo il 23→24 agosto 2026). È l'unico caso in cui vale la pena aprire
    /// <c>avvio-errore.txt</c>.
    /// </summary>
    [Fact]
    public void Due_avvii_di_fila_sono_un_processo_morto_male()
    {
        var verdetto = RegistroAvvii.Verdetto((Adesso.AddMinutes(-12), false), Adesso);

        Assert.Contains("⚠", verdetto, StringComparison.Ordinal);
        Assert.Contains("NON si è spento in modo ordinato", verdetto, StringComparison.Ordinal);
        Assert.Contains("00:12:00", verdetto, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'ultima riga «di evento» è l'ultima che porti un orario: l'intestazione spiega come si legge il
    /// file e non racconta nessun avvio. ⚠️ Se le righe di commento contassero, ogni file appena creato
    /// direbbe «processo morto male» al secondo avvio.
    /// </summary>
    [Fact]
    public void Lintestazione_non_conta_come_evento()
    {
        var righe = new[]
        {
            "# vIPI — registro degli avvii.",
            "#",
            "######################################################################",
            string.Empty,
        };

        Assert.Null(RegistroAvvii.UltimoEvento(righe));
    }

    /// <summary>E fra le righe vere vince l'ultima, con il suo tipo e il suo orario.</summary>
    [Fact]
    public void Lultimo_evento_e_lultima_riga_con_un_orario()
    {
        var righe = new[]
        {
            "# intestazione",
            RegistroAvvii.RigaAvvio("1.2.3", null, Adesso.AddHours(-2)),
            RegistroAvvii.RigaArresto(TimeSpan.FromMinutes(90), Adesso.AddMinutes(-30)),
        };

        var evento = RegistroAvvii.UltimoEvento(righe);

        Assert.NotNull(evento);
        Assert.True(evento!.Value.Arresto, "l'ultima riga è un ARRESTO e deve essere riconosciuta come tale");
        Assert.Equal(Adesso.AddMinutes(-30), evento.Value.Quando);
    }

    /// <summary>
    /// Le due righe si rileggono da sole: è il giro completo — si scrive una riga, la si rilegge, e ne esce
    /// quel che ci si era messo. È ciò che tiene onesto il verdetto quando il file cresce per mesi.
    /// </summary>
    [Fact]
    public void Le_righe_scritte_si_rileggono()
    {
        var avvio = RegistroAvvii.RigaAvvio("1.2.3+abc1234", null, Adesso);

        var evento = RegistroAvvii.UltimoEvento(new[] { avvio });

        Assert.NotNull(evento);
        Assert.False(evento!.Value.Arresto);
        Assert.Equal(Adesso, evento.Value.Quando);
        Assert.Contains("1.2.3+abc1234", avvio, StringComparison.Ordinal);
    }

    /// <summary>
    /// La durata si legge senza contare le cifre, e oltre le 24 ore dice i giorni. ⚠️ Il formato <c>c</c>
    /// di <see cref="TimeSpan"/> stamperebbe anche i tick: in un file che si legge a occhio sono rumore.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(0, 12, 30, "00:12:30")]
    [InlineData(3, 4, 5, "03:04:05")]
    public void La_durata_si_legge_a_occhio(int ore, int minuti, int secondi, string atteso) =>
        Assert.Equal(atteso, RegistroAvvii.Durata(new TimeSpan(ore, minuti, secondi)));

    /// <summary>Oltre il giorno il conto dei giorni sta davanti, o «49:00:00» ingannerebbe chi legge.</summary>
    [Fact]
    public void Oltre_le_ventiquattro_ore_i_giorni_stanno_davanti() =>
        Assert.Equal("2g 01:00:00", RegistroAvvii.Durata(TimeSpan.FromHours(49)));

    /// <summary>
    /// Un orologio che va indietro (l'ora legale, o due macchine che non concordano) non deve produrre
    /// durate negative in un file che si legge a occhio: si mostra zero.
    /// </summary>
    [Fact]
    public void Una_durata_negativa_diventa_zero() =>
        Assert.Equal("00:00:00", RegistroAvvii.Durata(TimeSpan.FromSeconds(-30)));

    /// <summary>
    /// E l'avvio vero lascia la sua riga nel file, in coda a quelle di prima.
    ///
    /// <para>⚠️ Come il gemello in <c>CronometroAvvioTests</c>, questo test vuole essere solo: legge un
    /// file che è uno per cartella e a cui ogni avvio aggiunge una riga. Il parallelismo del progetto è
    /// spento apposta (<c>ParallelismoDelProgetto.cs</c>).</para>
    /// </summary>
    [Fact]
    public void Lavvio_vero_lascia_la_sua_riga_nel_registro()
    {
        using var fabbrica = new SmokeTests.VipiAppFactory();
        fabbrica.CreateClient();   // forza la costruzione dell'host

        var percorso = Path.Combine(AppContext.BaseDirectory,
            StartupDiagnostics.CartellaDiagnostica, RegistroAvvii.FileName);

        Assert.True(File.Exists(percorso), $"nessun registro degli avvii in {percorso}");

        var righe = File.ReadAllLines(percorso);
        Assert.NotNull(RegistroAvvii.UltimoEvento(righe));
        Assert.Contains(righe, r => r.Contains("AVVIO", StringComparison.Ordinal));
    }

    // ---- le due misure che il solo uptime non dava (4 settembre 2026) ----------------------------------
    // ⚠️ Stanno in QUESTA classe e non in una nuova apposta: leggono uno stato statico, e due classi di test
    // girano in parallelo. Dentro una classe i test sono in fila, e la fila è ciò che le rende leggibili.

    /// <summary>
    /// «Richieste 0» è il risultato più interessante di tutti, e si scrive a lettere: dice che al processo
    /// non ha bussato nessuno — cioè che il keep-alive non gli parla.
    /// </summary>
    [Fact]
    public void La_riga_darresto_dice_quando_nessuno_ha_bussato()
    {
        TracciaRichieste.Azzera();
        SegnaleDiArresto.Azzera();

        var riga = RegistroAvvii.RigaArresto(TimeSpan.FromSeconds(50), Adesso);

        Assert.Contains("richieste 0", riga, StringComparison.Ordinal);
        Assert.Contains("nessuna", riga, StringComparison.Ordinal);
    }

    /// <summary>Con le richieste: quante, quanto fa l'ultima, e chi ha svegliato il processo.</summary>
    [Fact]
    public void La_riga_darresto_conta_le_richieste_e_dice_chi_ha_svegliato()
    {
        TracciaRichieste.Azzera();
        SegnaleDiArresto.Azzera();
        TracciaRichieste.Segna("/vsop/health");
        TracciaRichieste.Segna("/services/vsop/libb/vipi");

        var riga = RegistroAvvii.RigaArresto(TimeSpan.FromSeconds(50), DateTime.UtcNow);

        Assert.Contains("richieste 2", riga, StringComparison.Ordinal);
        Assert.Contains("svegliato da /vsop/health", riga, StringComparison.Ordinal);
        Assert.DoesNotContain("/services/vsop/libb/vipi", riga, StringComparison.Ordinal);   // la PRIMA, non l'ultima
    }

    /// <summary>
    /// E dice chi ha chiesto lo spegnimento. Senza segnale la risposta è «da dentro», ed è quella che manda
    /// a cercare nel codice invece che nel pannello dell'hosting.
    /// </summary>
    [Fact]
    public void La_riga_darresto_dice_chi_ha_chiesto_lo_spegnimento()
    {
        TracciaRichieste.Azzera();
        SegnaleDiArresto.Azzera();

        Assert.Contains("DA DENTRO", RegistroAvvii.RigaArresto(TimeSpan.FromSeconds(50), Adesso), StringComparison.Ordinal);
    }

    /// <summary>⚠️ La riga resta LEGGIBILE dal lettore del file: è più lunga, e il verdetto sul processo
    /// precedente si costruisce rileggendola.</summary>
    [Fact]
    public void La_riga_piu_lunga_si_rilegge_lo_stesso()
    {
        TracciaRichieste.Azzera();
        SegnaleDiArresto.Azzera();
        TracciaRichieste.Segna("/vsop/health");

        var righe = new[] { RegistroAvvii.RigaArresto(TimeSpan.FromMinutes(90), Adesso.AddMinutes(-30)) };
        var evento = RegistroAvvii.UltimoEvento(righe);

        Assert.NotNull(evento);
        Assert.True(evento!.Value.Arresto);
        Assert.Equal(Adesso.AddMinutes(-30), evento.Value.Quando);
    }

    /// <summary>Il conto è di QUESTO processo: azzerarlo lo riporta a zero, come un avvio nuovo.</summary>
    [Fact]
    public void Il_conto_delle_richieste_si_azzera()
    {
        TracciaRichieste.Segna("/x");
        TracciaRichieste.Azzera();

        Assert.Equal(0, TracciaRichieste.Servite);
        Assert.Null(TracciaRichieste.UltimaUtc);
        Assert.Null(TracciaRichieste.Prima);
    }

    /// <summary>
    /// La prova che conta: <b>l'host vero</b>, una richiesta vera, e la riga d'ARRESTO che la conta.
    ///
    /// <para>⚠️ Le prove sulla stringa qui sopra non dicono se il contatore è ATTACCATO alla pipeline: un
    /// middleware registrato nel posto sbagliato — dopo qualcosa che risponde per conto suo — lascerebbe
    /// tutti quei test verdi e il registro a «richieste 0» in produzione, cioè proprio la misura che si sta
    /// andando a leggere. Qui l'host si accende, si bussa, e si rilegge il file che ha scritto lui.</para>
    /// </summary>
    [Fact]
    public async Task Lhost_vero_conta_la_richiesta_e_la_scrive_nella_riga_darresto()
    {
        var percorso = Path.Combine(AppContext.BaseDirectory,
            StartupDiagnostics.CartellaDiagnostica, RegistroAvvii.FileName);
        var primaDelGiro = File.Exists(percorso) ? File.ReadAllLines(percorso).Length : 0;

        // ⚠️ Il conto è del PROCESSO, e in produzione un processo ospita un'applicazione sola — ma qui gli
        // host sono decine, nello stesso processo, e le richieste degli altri test sono arrivate prima.
        // Senza questo azzeramento «chi ha svegliato» sarebbe la prima richiesta di un altro test: il test
        // passava da solo e cadeva in fila, che è il modo peggiore di sbagliarsi.
        TracciaRichieste.Azzera();

        using (var fabbrica = new SmokeTests.VipiAppFactory())
        {
            var cliente = fabbrica.CreateClient();
            await cliente.GetAsync("/vsop/health");
        }   // il Dispose ferma l'host: è qui che si scrive l'ARRESTO

        var righe = File.ReadAllLines(percorso).Skip(primaDelGiro).ToArray();
        var arresto = righe.LastOrDefault(r => r.Contains("ARRESTO", StringComparison.Ordinal));

        Assert.NotNull(arresto);
        Assert.DoesNotContain("richieste 0", arresto!, StringComparison.Ordinal);
        Assert.Contains("svegliato da /vsop/health", arresto!, StringComparison.Ordinal);
    }
}

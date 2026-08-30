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
}

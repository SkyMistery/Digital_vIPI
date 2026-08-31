using Vipi.Application.Diagnostica;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Lo strumento che racconta le corse sul <c>DbContext</c> — e che il <b>31 agosto 2026</b> è stato lui la
/// causa di due morti del processo.
///
/// <para>🔴 <b>Che cosa era successo.</b> <see cref="CollisioniDbContext"/> teneva, oltre alla tabella
/// debole delle operazioni aperte, un secondo elenco delle liste vive, e ci aggiungeva un riferimento <b>a
/// ogni comando SQL eseguito</b>. Quell'elenco veniva potato solo quando scattava una fotografia, cioè
/// quasi mai. Due conseguenze, tutt'e due lette nei file scaricati dal server quel giorno:</para>
/// <list type="bullet">
///   <item><c>avvii.txt</c>: due AVVII senza ARRESTO in mezzo (10:57 e 13:05) — «il processo precedente
///         NON si è spento in modo ordinato».</item>
///   <item><c>errori-richieste.txt</c>: la stessa lista di operazioni ripetuta 34, 38 e 44 volte dentro una
///         sola fotografia. Erano i duplicati dell'elenco, non altrettante query.</item>
/// </list>
///
/// <para><b>La regola che resta</b>, e che questi test presidiano: <b>uno strumento di diagnosi non può
/// tenere stato che cresce con il traffico</b>, e quel che stampa deve essere la scena di <i>questo</i>
/// guasto — non un archivio.</para>
///
/// <para>ℹ️ <b>Il gemello.</b> <c>Vipi.E2E.Tests/CollisioniDbContextTests</c> prova l'altra metà: che
/// l'aggancio a <c>FirstChanceException</c> scatti e che l'intercettore sia davvero montato sul contesto
/// dell'applicazione. Qui si prova che lo strumento non <b>costi</b> e non <b>menta</b>.</para>
///
/// <para>⚠️ La classe sotto esame è <c>static</c> e vive quanto il processo: i test usano marcatori SQL
/// unici e contano solo i propri, così non dipendono da che altro gira nello stesso assembly.</para>
/// </summary>
public class CollisioniSenzaPerditaTests
{
    /// <summary>Sta al posto di un <c>DbContext</c>: alla tabella debole serve solo un'identità di riferimento.</summary>
    private sealed class ContestoFinto { }

    /// <summary>Fa scattare la fotografia come la fa scattare EF: lanciando <i>quella</i> frase.</summary>
    private static void FaiScattareLaFotografia()
    {
        try
        {
            throw new InvalidOperationException(
                "A second operation was started on this context instance before a previous operation completed.");
        }
        catch (InvalidOperationException)
        {
            // Il gestore è su FirstChanceException: ha già visto il lancio, e a noi l'eccezione non serve.
        }
    }

    private static string TuttiGliScatti() => string.Join("\n", CollisioniDbContext.Scatti_());

    private static int Occorrenze(string testo, string ago)
    {
        var n = 0;
        for (var i = testo.IndexOf(ago, StringComparison.Ordinal); i >= 0;
             i = testo.IndexOf(ago, i + ago.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>
    /// Ogni operazione aperta compare <b>una volta</b>. È il difetto che si leggeva a occhio nudo nel file
    /// del 31 agosto: «38 operazioni aperte» erano la stessa lista stampata 38 volte.
    /// </summary>
    [Fact]
    public void Ogni_operazione_aperta_compare_una_volta_sola()
    {
        var ctx = new ContestoFinto();
        var marcatore = "/*prova-" + Guid.NewGuid().ToString("N") + "*/";

        // Tre comandi aperti sullo stesso contesto, come in una corsa vera.
        CollisioniDbContext.Apre(ctx, "SELECT 1 " + marcatore + " uno");
        CollisioniDbContext.Apre(ctx, "SELECT 2 " + marcatore + " due");
        CollisioniDbContext.Apre(ctx, "SELECT 3 " + marcatore + " tre");

        FaiScattareLaFotografia();

        var testo = TuttiGliScatti();
        Assert.Equal(1, Occorrenze(testo, marcatore + " uno"));
        Assert.Equal(1, Occorrenze(testo, marcatore + " due"));
        Assert.Equal(1, Occorrenze(testo, marcatore + " tre"));

        GC.KeepAlive(ctx);   // la tabella è DEBOLE: senza, il contesto può sparire prima dell'asserzione
    }

    /// <summary>Un comando che si chiude esce dalla scena: la fotografia dopo non lo nomina più.</summary>
    [Fact]
    public void Un_comando_chiuso_non_compare_piu()
    {
        var ctx = new ContestoFinto();
        var marcatore = "/*chiuso-" + Guid.NewGuid().ToString("N") + "*/";
        var sql = "SELECT 1 " + marcatore;

        CollisioniDbContext.Apre(ctx, sql);
        CollisioniDbContext.Chiude(ctx, sql);

        FaiScattareLaFotografia();

        Assert.Equal(0, Occorrenze(TuttiGliScatti(), marcatore));
        GC.KeepAlive(ctx);
    }

    /// <summary>
    /// ⚠️ Il tetto per contesto. Una lettura <b>abbandonata</b> — richiesta annullata a metà enumerazione —
    /// non chiude mai il suo lettore e lascia la riga lì; su un circuito che vive ore le righe si
    /// accumulerebbero. Oltre il tetto si butta la più vecchia.
    /// </summary>
    [Fact]
    public void Le_operazioni_tenute_per_contesto_hanno_un_tetto()
    {
        var ctx = new ContestoFinto();
        var marcatore = "/*tetto-" + Guid.NewGuid().ToString("N") + "*/";

        for (var i = 0; i < 500; i++) CollisioniDbContext.Apre(ctx, $"SELECT {i} {marcatore}");

        FaiScattareLaFotografia();

        var quante = Occorrenze(TuttiGliScatti(), marcatore);
        Assert.InRange(quante, 1, 64);
        GC.KeepAlive(ctx);
    }

    /// <summary>
    /// La fotografia si allega solo se è di <b>poco prima</b>.
    ///
    /// <para>⚠️ È la riga che il 31 agosto 2026 mancava: la voce delle 11:40:17 portava in coda venti
    /// fotografie, la più recente delle 11:37:06 — tre minuti prima, di un'altra richiesta, con dentro
    /// query che con quella pagina non c'entravano niente. Una scena di un altro guasto allegata al tuo si
    /// legge come se fosse la tua.</para>
    /// </summary>
    [Fact]
    public void Una_fotografia_vecchia_non_si_allega()
    {
        FaiScattareLaFotografia();

        // Appena scattata: si allega.
        Assert.NotNull(CollisioniDbContext.UltimoScatto(TimeSpan.FromMinutes(1)));

        // Qualunque fotografia è più vecchia di zero: il cancello la rifiuta.
        Assert.Null(CollisioniDbContext.UltimoScatto(TimeSpan.Zero));
    }

    /// <summary>
    /// Spento, non annota niente. È l'interruttore che il 31 agosto sarebbe servito per escludere questo
    /// codice in un minuto invece che in un pacchetto.
    /// </summary>
    [Fact]
    public void Spento_non_annota_niente()
    {
        var ctx = new ContestoFinto();
        var marcatore = "/*spento-" + Guid.NewGuid().ToString("N") + "*/";

        CollisioniDbContext.Acceso = false;
        try
        {
            CollisioniDbContext.Apre(ctx, "SELECT 1 " + marcatore);
            FaiScattareLaFotografia();
        }
        finally
        {
            CollisioniDbContext.Acceso = true;
        }

        Assert.Equal(0, Occorrenze(TuttiGliScatti(), marcatore));
        GC.KeepAlive(ctx);
    }
}

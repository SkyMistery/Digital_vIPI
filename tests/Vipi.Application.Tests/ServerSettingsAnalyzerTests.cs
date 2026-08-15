using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le due impostazioni del server che l'applicazione assume e non può imporre. Il giudizio è una funzione
/// pura apposta: si prova senza un database, e la sonda che le legge resta una query sola.
/// </summary>
public class ServerSettingsAnalyzerTests
{
    private const string ModoStretto = "STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION";
    private const long PacchettoOk = 16 * 1024 * 1024;

    [Fact]
    public void Un_server_configurato_come_serve_non_produce_segnalazioni()
        => Assert.Empty(ServerSettingsAnalyzer.Analyze(ModoStretto, PacchettoOk));

    /// <summary>
    /// Il caso che questa sonda esiste per prendere: strict mode spento. È il default di parecchi hosting
    /// condivisi, e la conseguenza — troncamento silenzioso invece di errore — non lascia tracce.
    /// </summary>
    [Fact]
    public void Senza_strict_mode_si_segnala_un_errore()
    {
        var f = Assert.Single(ServerSettingsAnalyzer.Analyze("NO_ENGINE_SUBSTITUTION", PacchettoOk));

        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Equal("sql_mode", f.Entity);
        Assert.Contains("tronca", f.Detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Il confronto guarda le voci della lista, non il testo intero: <c>STRICT_ALL_TABLES</c> contiene
    /// «STRICT» ma non è la modalità che ci serve, e una <c>Contains</c> nuda la scambierebbe per buona.
    /// </summary>
    [Fact]
    public void Una_modalita_che_somiglia_non_conta_per_quella_giusta()
        => Assert.NotEmpty(ServerSettingsAnalyzer.Analyze("STRICT_ALL_TABLES,NO_ZERO_DATE", PacchettoOk));

    [Theory]
    [InlineData("strict_trans_tables")]                       // il server la riporta in minuscolo
    [InlineData(" STRICT_TRANS_TABLES ,NO_ZERO_DATE")]        // spazi attorno alla voce
    public void La_modalita_si_riconosce_comunque_sia_scritta(string sqlMode)
        => Assert.Empty(ServerSettingsAnalyzer.Analyze(sqlMode, PacchettoOk));

    /// <summary>
    /// Il tetto si supera in un colpo solo, il giorno in cui qualcuno carica una carta grande: l'app accetta
    /// fino a 3 MB e il server ne deve reggere almeno 4.
    /// </summary>
    [Fact]
    public void Un_max_allowed_packet_troppo_basso_si_segnala()
    {
        var f = Assert.Single(ServerSettingsAnalyzer.Analyze(ModoStretto, 1024 * 1024));

        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Equal("max_allowed_packet", f.Entity);
    }

    [Fact]
    public void Il_valore_esatto_al_minimo_va_bene()
        => Assert.Empty(ServerSettingsAnalyzer.Analyze(ModoStretto, ServerSettingsAnalyzer.MinMaxAllowedPacket));

    /// <summary>
    /// Un valore illeggibile non è un valore buono. Se la sonda tacesse, il silenzio si leggerebbe come
    /// «tutto a posto» — che è esattamente l'errore da cui nasce questa voce dell'audit.
    /// </summary>
    [Fact]
    public void Un_valore_illeggibile_si_segnala_come_avviso()
    {
        var findings = ServerSettingsAnalyzer.Analyze(null, null);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(ConsistencySeverity.Warning, f.Severity));
    }

    [Fact]
    public void Le_segnalazioni_portano_la_categoria_con_cui_compaiono_nella_diagnostica()
        => Assert.All(ServerSettingsAnalyzer.Analyze("", 1),
            f => Assert.Equal(ServerSettingsAnalyzer.Category, f.Category));
}

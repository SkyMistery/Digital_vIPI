using Microsoft.Extensions.Logging;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La rete che porta nel registro anche i guasti del <b>circuito</b> Blazor.
///
/// <para>🔴 Serve perché la mancanza è <b>silenziosa in tutti e due i sensi</b>: se questo aggancio smette
/// di combaciare non fallisce niente — semplicemente, il giorno che una pagina interattiva muore, in
/// <c>diagnostica/</c> non compare nulla e si riparte da zero. È successo il 2 settembre 2026.</para>
///
/// <para>⚠️ Si prova la <b>decisione</b> (quali categorie, quali livelli) e non la scrittura del file: la
/// scrittura dipende dalla cartella dell'applicazione, e un test che la esercita finirebbe per provare
/// <c>StartupDiagnostics</c> invece di questo.</para>
/// </summary>
public class DiagnosticaCircuitoTests
{
    private static ILogger PerCategoria(string categoria) =>
        new DiagnosticaCircuito().CreateLogger(categoria);

    /// <summary>⚠️ Il confronto è per PREFISSO: fra una versione e l'altra il framework ha spostato queste
    /// classi di namespace, e un nome esatto che smette di combaciare <b>tace</b> invece di rompersi.</summary>
    [Theory]
    [InlineData("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost")]
    [InlineData("Microsoft.AspNetCore.Components.Server.ComponentHub")]
    [InlineData("Microsoft.AspNetCore.Components.Server.Circuits.RemoteRenderer")]
    public void Le_categorie_del_circuito_sono_ascoltate(string categoria) =>
        Assert.True(PerCategoria(categoria).IsEnabled(LogLevel.Error));

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    [InlineData("Vipi.Errori")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
    public void Le_altre_categorie_non_lo_sono(string categoria) =>
        Assert.False(PerCategoria(categoria).IsEnabled(LogLevel.Error));

    /// <summary>Solo i guasti: il resto del circuito è rumore, e questo registro si scarica via FTP.</summary>
    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void Solo_dai_guasti_in_su(LogLevel livello, bool atteso) =>
        Assert.Equal(atteso,
            PerCategoria("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost").IsEnabled(livello));

    /// <summary>⚠️ Un errore raccontato a parole, senza eccezione, non lascia una voce: nel registro ci va
    /// quel che ha uno stack — e con lo stack la fotografia delle collisioni.</summary>
    [Fact]
    public void Senza_eccezione_non_si_scrive_niente()
    {
        var log = PerCategoria("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost");

        // Non deve alzare: se scrivesse, scriverebbe una voce vuota di quel che serve.
        log.Log(LogLevel.Error, new EventId(1, "prova"), "solo parole", null, (s, _) => s);
    }
}

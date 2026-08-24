using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Auth;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Quando una pagina muore, l'utente deve capire che cosa fare e noi dobbiamo poter capire perché.
///
/// <para>Fino al 24 agosto 2026 non succedeva né l'una né l'altra cosa: usciva la <c>Error.razor</c> del
/// modello di progetto — inglese, senza marchio, con tre paragrafi su come si accende la modalità di
/// sviluppo — e dell'eccezione non restava traccia leggibile, perché su <c>atc.it.ivao.aero</c> i log del
/// processo non li legge nessuno (niente shell, solo FTP).</para>
///
/// <para>⚠️ L'ambiente è <b>Staging</b> e non Development di proposito: <c>UseExceptionHandler</c> è
/// montato solo fuori da Development, quindi in Development questa strada non esiste proprio.</para>
/// </summary>
public sealed class PaginaErroreTests
{
    [Fact]
    public async Task Una_pagina_che_muore_da_la_pagina_nostra_e_lascia_una_riga()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        Assert.NotNull(registro);
        if (File.Exists(registro)) File.Delete(registro);

        using var fabbrica = new FabbricaRotta();
        var res = await fabbrica.CreateClient().GetAsync("/services");
        var html = await res.Content.ReadAsStringAsync();

        // 1. Lo stato resta 500: le sonde devono continuare a vedere un guasto come tale.
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);

        // 2. La pagina è la nostra, e parla a chi la legge.
        Assert.Contains("Questa pagina non si è aperta", html);
        Assert.Contains("/services/vsop", html);

        // 3. Dell'eccezione in pagina non entra niente: né tipo né messaggio.
        Assert.DoesNotContain("InvalidOperationException", html);
        Assert.DoesNotContain(Rotto.Messaggio, html);

        // 4. Il codice mostrato è quello scritto nel registro: è il filo fra la fotografia e lo stack trace.
        var codice = Codice(html);
        var righe = await File.ReadAllTextAsync(registro!);
        Assert.Contains(codice, righe);
        Assert.Contains(Rotto.Messaggio, righe);
        Assert.Contains("GET /services", righe);
    }

    /// <summary>La stringa di query non deve finire nel registro: su /signin-oidc è una credenziale.</summary>
    [Fact]
    public async Task La_stringa_di_query_non_finisce_nel_registro()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        if (File.Exists(registro)) File.Delete(registro);

        using var fabbrica = new FabbricaRotta();
        await fabbrica.CreateClient().GetAsync("/services?code=SEGRETISSIMO");

        var righe = await File.ReadAllTextAsync(registro!);
        Assert.Contains("GET /services", righe);
        Assert.DoesNotContain("SEGRETISSIMO", righe);
    }

    /// <summary>Il codice mostrato in pagina, dal riquadro in fondo.</summary>
    private static string Codice(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, @"<span class=""m"">([^<]+)</span>");
        Assert.True(m.Success, "la pagina d'errore non mostra nessun codice: senza, una segnalazione non è " +
                               "rintracciabile e il registro serve a metà.\n" + html[..Math.Min(800, html.Length)]);
        return m.Groups[1].Value;
    }

    /// <summary>
    /// L'identità non si risolve: è un guasto DENTRO il layout condiviso — e non è guardato di proposito,
    /// perché senza sapere chi sei la pagina non può nemmeno decidere che cosa mostrarti. È il caso che una
    /// pagina d'errore fatta di componenti non sopravviverebbe: passando dallo stesso layout, lancerebbe una
    /// seconda volta e l'utente resterebbe davanti a una risposta vuota.
    ///
    /// <para>⚠️ Prima qui c'era un catalogo ACC illeggibile. Non serve più: da oggi quel caso <b>degrada</b>
    /// invece di morire (<c>BarraNonAffondaLaPaginaTests</c>), e un test che pretende un 500 da una strada
    /// che ora regge proverebbe il contrario di quello che vogliamo.</para>
    /// </summary>
    private sealed class Rotto : IEditAuthorizationService
    {
        internal const string Messaggio = "autorizzazione non risolvibile (simulato)";
        private static Exception Giu() => new InvalidOperationException(Messaggio);

        // È ciò che il layout chiede per decidere che cosa mostrare, e non è guardato di proposito.
        public bool IsAdmin => throw Giu();

        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => throw Giu();
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => throw Giu();
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => throw Giu();
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => throw Giu();
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => throw Giu();
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) => throw Giu();
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => throw Giu();
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => throw Giu();
        public void EnsureAdmin() => throw Giu();
    }

    private sealed class FabbricaRotta : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-errore-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<IEditAuthorizationService>();
                s.AddScoped<IEditAuthorizationService, Rotto>();
            });
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }
}

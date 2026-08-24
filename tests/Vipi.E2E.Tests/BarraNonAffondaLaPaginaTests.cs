using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La barra in cima è il CONTORNO: se non riesce a decidere una sua decorazione, la pagina si serve lo
/// stesso senza quella decorazione.
///
/// <para><b>Il difetto vero, del 24 agosto 2026.</b> Un socio senza incarichi — cioè chiunque entri per la
/// prima volta — ha visto «An error occurred while processing your request.» su <c>/services</c>, che è
/// l'elenco degli strumenti e non legge una riga di database; lo stesso indirizzo, senza accedere,
/// rispondeva 200. L'unica cosa che un utente loggato fa in più di un anonimo su quella pagina era la
/// domanda «hai qualcosa da modificare?», e quella domanda va al database: l'anonimo non ci arriva
/// (<c>_user is not null</c> è falso) e l'admin nemmeno (esce sui codici staff). Restava a pagarla il
/// socio qualunque, e un intoppo qualsiasi del database diventava una pagina d'errore <b>per i soli
/// utenti loggati</b> — mentre ogni sonda anonima continuava a dire che il sito era su.</para>
/// </summary>
public sealed class BarraNonAffondaLaPaginaTests
{
    /// <summary>Le quattro pagine del contorno pubblico, per un socio qualunque: devono aprirsi.</summary>
    [Theory]
    [InlineData("/services")]
    [InlineData("/services/vsop")]
    [InlineData("/services/vsop/live")]
    [InlineData("/services/vsop/guide")]
    public async Task Un_socio_senza_incarichi_apre_le_pagine(string percorso)
    {
        using var fabbrica = new FabbricaSocio(concessioniRotte: false);

        var res = await fabbrica.CreateClient().GetAsync(percorso);

        await Assert200(percorso, res);
    }

    /// <summary>
    /// Il caso che si è visto in produzione: la domanda della barra fallisce. Prima la pagina moriva con
    /// lei; ora esce senza il tasto «Modifica», che è esattamente ciò che quella domanda decideva.
    /// </summary>
    [Theory]
    [InlineData("/services")]
    [InlineData("/services/vsop")]
    public async Task Se_la_domanda_della_barra_fallisce_la_pagina_esce_lo_stesso(string percorso)
    {
        using var fabbrica = new FabbricaSocio(concessioniRotte: true);

        var res = await fabbrica.CreateClient().GetAsync(percorso);

        await Assert200(percorso, res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/services/vsop/versions", html);   // il tasto «Modifica» resta spento
    }

    private static async Task Assert200(string percorso, HttpResponseMessage res)
    {
        var corpo = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"{percorso} -> {(int)res.StatusCode}\n{corpo[..Math.Min(1500, corpo.Length)]}");
    }

    /// <summary>Identità di un socio qualunque: nessuna posizione staff ⇒ non admin, e nessuna concessione.</summary>
    private sealed class SocioSemplice : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(123456, "Mario Rossi", "LIRR", Array.Empty<string>());
    }

    /// <summary>Le concessioni non si possono leggere: è il guasto del database visto dalla barra.</summary>
    private sealed class ConcessioniRotte : IEditGrantRepository
    {
        private static Exception Giu() => new InvalidOperationException("database non raggiungibile (simulato)");

        public Task<bool> HasAnyGrantAsync(int UserId, CancellationToken ct = default) => throw Giu();
        public Task<bool> HasGrantAsync(int UserId, string accCode, CancellationToken ct = default) => throw Giu();
        public Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default) => throw Giu();
        public Task<int> AddAsync(int UserId, string? displayName, string accCode, int GrantedByUserId, CancellationToken ct = default) => throw Giu();
        public Task RevokeAsync(int grantId, int actorUserId, CancellationToken ct = default) => throw Giu();
        public Task<string?> GetDocumentAccCodeAsync(int documentId, CancellationToken ct = default) => throw Giu();
    }

    private sealed class FabbricaSocio : WebApplicationFactory<Program>
    {
        private readonly bool _concessioniRotte;
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-socio-{Guid.NewGuid():N}.db");

        public FabbricaSocio(bool concessioniRotte) => _concessioniRotte = concessioniRotte;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<ICurrentUserProvider>();
                s.AddScoped<ICurrentUserProvider, SocioSemplice>();
                if (_concessioniRotte)
                {
                    s.RemoveAll<IEditGrantRepository>();
                    s.AddScoped<IEditGrantRepository, ConcessioniRotte>();
                }
            });
            // Come le altre fabbriche E2E: niente OIDC reale in CI (vedi SmokeTests.VipiAppFactory).
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

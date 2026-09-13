using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;
using Vipi.Host.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Le posizioni staff nel cookie si rileggono da IVAO (T-002, 13 settembre 2026): chi perde l'incarico perde
/// il livello, e un guasto di IVAO non butta fuori nessuno. Vedi <see cref="RiconvalidaPosizioniStaff"/>.
/// </summary>
public class RiconvalidaPosizioniStaffTests
{
    private static readonly DateTimeOffset Login = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Chi_perde_l_incarico_lo_perde_anche_nel_cookie()
    {
        var ctx = Contesto(new[] { "IT-DIR" }, new SourceUserStaff(704798, null, null, "IT", IsStaff: false, Array.Empty<string>()));

        await RiconvalidaPosizioniStaff.ValidaAsync(ctx, Login.AddHours(5));

        Assert.Null(ctx.Principal!.FindFirst("userStaffPositions"));
        Assert.Equal("704798", ctx.Principal!.FindFirst("id")!.Value);   // l'identità resta
        Assert.True(ctx.ShouldRenew);
    }

    [Fact]
    public async Task Un_incarico_nuovo_entra_nel_cookie()
    {
        var ctx = Contesto(new[] { "IT-AOA1" }, new SourceUserStaff(704798, null, null, "IT", true, new[] { "IT-AOA1", "LIRR-CH" }));

        await RiconvalidaPosizioniStaff.ValidaAsync(ctx, Login.AddHours(5));

        var codici = JsonSerializer.Deserialize<string[]>(ctx.Principal!.FindFirst("userStaffPositions")!.Value);
        Assert.Equal(new[] { "IT-AOA1", "LIRR-CH" }, codici);
    }

    [Fact]
    public async Task Prima_dell_intervallo_non_chiede_niente()
    {
        var elenco = new ElencoFinto(new SourceUserStaff(704798, null, null, "IT", false, Array.Empty<string>()));
        var ctx = Contesto(new[] { "IT-DIR" }, elenco);

        await RiconvalidaPosizioniStaff.ValidaAsync(ctx, Login.AddHours(1));

        Assert.Equal(0, elenco.Chiamate);
        Assert.NotNull(ctx.Principal!.FindFirst("userStaffPositions"));
    }

    [Fact]
    public async Task Ivao_che_non_risponde_non_butta_fuori_nessuno()
    {
        var ctx = Contesto(new[] { "IT-DIR" }, (SourceUserStaff?)null);

        await RiconvalidaPosizioniStaff.ValidaAsync(ctx, Login.AddHours(5));

        Assert.Contains("IT-DIR", ctx.Principal!.FindFirst("userStaffPositions")!.Value);
    }

    [Fact]
    public async Task Una_risposta_incoerente_non_declassa()
    {
        // isStaff vero e nessuna posizione: la forma di un cambio di contratto dell'API, non di un declassamento.
        var ctx = Contesto(new[] { "IT-DIR" }, new SourceUserStaff(704798, null, null, "IT", true, Array.Empty<string>()));

        await RiconvalidaPosizioniStaff.ValidaAsync(ctx, Login.AddHours(5));

        Assert.Contains("IT-DIR", ctx.Principal!.FindFirst("userStaffPositions")!.Value);
    }

    /// <summary>
    /// Il gancio è montato sul cookie vero. In questi test l'autenticazione standalone è spenta (niente
    /// ClientId), quindi la riga si guarda nel sorgente: senza, tutto il resto sarebbe codice che non gira.
    /// </summary>
    [Fact]
    public void Il_gancio_e_montato_sul_cookie_di_sessione()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Vipi.Host", "Auth", "VipiStandaloneAuthExtensions.cs")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var sorgente = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Vipi.Host", "Auth", "VipiStandaloneAuthExtensions.cs"));
        Assert.Contains("o.Events.OnValidatePrincipal = RiconvalidaPosizioniStaff.OnValidatePrincipal;", sorgente);
    }

    private static CookieValidatePrincipalContext Contesto(string[] posizioni, SourceUserStaff? risposta) =>
        Contesto(posizioni, new ElencoFinto(risposta));

    private static CookieValidatePrincipalContext Contesto(string[] posizioni, ElencoFinto elenco)
    {
        var servizi = new ServiceCollection().AddSingleton<IUserDirectory>(elenco).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = servizi };
        var identita = new ClaimsIdentity(new[]
        {
            new Claim("id", "704798"),
            new Claim("name", "Chi Prova"),
            new Claim("userStaffPositions", JsonSerializer.Serialize(posizioni)),
        }, "Cookies");
        var props = new AuthenticationProperties { IssuedUtc = Login };
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identita), props, "Cookies");
        return new CookieValidatePrincipalContext(http,
            new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(), ticket);
    }

    private sealed class ElencoFinto(SourceUserStaff? risposta) : IUserDirectory
    {
        public int Chiamate { get; private set; }
        public Task<SourceUserStaff?> GetUserAsync(int UserId, CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult(risposta);
        }
    }
}

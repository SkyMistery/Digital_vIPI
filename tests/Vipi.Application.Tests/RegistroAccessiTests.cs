using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>Il registro degli accessi (30 settembre 2026): l'elenco è solo degli amministratori, la potatura dei
/// dodici mesi gira al più una volta al giorno.</summary>
public class RegistroAccessiTests
{
    private sealed class Deposito : IRegistroAccessiStore
    {
        public int Registrati, Potature, Letture;
        public DateTime? UltimaSoglia;
        public string? Divisione;

        public Task RegistraAsync(int userId, string nome, string? divisione, string? acc, DateTime oraUtc, CancellationToken ct = default)
        { Registrati++; Divisione = divisione; return Task.CompletedTask; }

        public Task<int> PotaAsync(DateTime ultimoPrimaDi, CancellationToken ct = default)
        { Potature++; UltimaSoglia = ultimoPrimaDi; return Task.FromResult(0); }

        public Task<ElencoAccessi> ElencoAsync(string? cerca, int limite, CancellationToken ct = default)
        { Letture++; return Task.FromResult(new ElencoAccessi(Array.Empty<AccessoAlSitoRiga>(), 0)); }
    }

    private sealed class Livello(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "Prova";
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }

    [Fact]
    public async Task L_elenco_e_solo_degli_amministratori()
    {
        var d = new Deposito();
        await Assert.ThrowsAsync<EditNotAllowedException>(() => new RegistroAccessi(d, new Livello(VipiRole.Editor)).ElencoAsync(null));
        Assert.Equal(0, d.Letture);

        await new RegistroAccessi(d, new Livello(VipiRole.Admin)).ElencoAsync("Mario");
        Assert.Equal(1, d.Letture);
    }

    [Fact]
    public async Task Registrare_passa_la_divisione_dell_utente()
    {
        var d = new Deposito();
        var utente = new CurrentUser(704798, "Carmine", "LIRR", Array.Empty<string>()) { Division = "IT" };
        await new RegistroAccessi(d, new Livello(VipiRole.User)).RegistraAsync(utente);

        Assert.Equal(1, d.Registrati);
        Assert.Equal("IT", d.Divisione);
    }

    [Fact]
    public void La_potatura_gira_una_volta_ogni_ventiquattro_ore()
    {
        long ultima = 0;
        var t0 = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

        Assert.True(RegistroAccessi.DevePotare(ref ultima, t0));
        Assert.False(RegistroAccessi.DevePotare(ref ultima, t0.AddHours(23)));
        Assert.True(RegistroAccessi.DevePotare(ref ultima, t0.AddHours(24)));
        Assert.False(RegistroAccessi.DevePotare(ref ultima, t0.AddHours(25)));
    }
}

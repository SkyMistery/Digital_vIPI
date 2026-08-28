using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Il fotogramma delle promozioni a mano tenuto in memoria. Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §6.
///
/// <para>⚠️ <b>Il difetto che questi test tengono fermo ha un precedente.</b> Se il livello si risolvesse
/// con una query per richiesta, la rimetteremmo dove stava <c>HasAnyGrantAsync</c>: nel layout, sul
/// <c>DbContext</c> di circuito, cioè il posto esatto da cui sono uscite due volte le corse «A second
/// operation was started on this context». Qui si prova che la lettura <b>non tocca il database</b> e che
/// una promozione fa effetto <b>solo</b> dopo una ricarica esplicita.</para>
/// </summary>
public class RoleOverrideCacheTests
{
    private const int Promosso = 654321;

    /// <summary>Uno store finto che conta quante volte lo si interroga: è il conto che conta.</summary>
    private sealed class DepositoFinto : IRoleOverrideStore
    {
        public readonly Dictionary<int, VipiRole> Righe = new();
        public int Letture;

        public Task<IReadOnlyList<RoleOverrideRow>> ListAsync(CancellationToken ct = default)
        {
            Letture++;
            IReadOnlyList<RoleOverrideRow> righe = Righe
                .Select(r => new RoleOverrideRow(r.Key, r.Value, 0, DateTime.UtcNow, null, null))
                .ToList();
            return Task.FromResult(righe);
        }

        public Task SetAsync(int userId, VipiRole level, int grantedByUserId, string? displayName, string? note, CancellationToken ct = default)
        {
            Righe[userId] = level;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(int userId, CancellationToken ct = default) => Task.FromResult(Righe.Remove(userId));
    }

    private static (RoleOverrideCache Cache, DepositoFinto Deposito) Cache()
    {
        var deposito = new DepositoFinto();
        var provider = new ServiceCollection()
            .AddScoped<IRoleOverrideStore>(_ => deposito)
            .BuildServiceProvider();

        return (new RoleOverrideCache(provider.GetRequiredService<IServiceScopeFactory>()), deposito);
    }

    [Fact]
    public async Task Dopo_la_ricarica_dice_il_livello_scritto_a_mano()
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = VipiRole.Editor;

        await cache.ReloadAsync();

        Assert.Equal(VipiRole.Editor, cache.For(Promosso));
        Assert.True(cache.Loaded);
    }

    /// <summary>
    /// ⚠️ <c>null</c> vuol dire «nessuna promozione», non «non lo so»: chi chiama ricade sul livello dello
    /// staff. Prima di qualunque lettura del database il comportamento dev'essere già quello giusto — una
    /// promozione che tarda è un fastidio, un permesso negato a chi lo ha per ruolo sarebbe un guasto.
    /// </summary>
    [Fact]
    public void Prima_di_qualunque_lettura_non_promuove_e_non_nega_niente()
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = VipiRole.Admin;

        Assert.Null(cache.For(Promosso));
        Assert.Empty(cache.All);
        Assert.False(cache.Loaded);
        Assert.Equal(0, deposito.Letture);   // e soprattutto: non è andata a chiederlo
    }

    /// <summary>Il cuore della cosa: mille domande, zero query.</summary>
    [Fact]
    public async Task Mille_domande_non_fanno_nessuna_query()
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = VipiRole.Editor;
        await cache.ReloadAsync();

        for (var i = 0; i < 1000; i++) cache.For(Promosso + i % 3);

        Assert.Equal(1, deposito.Letture);   // solo quella della ricarica
    }

    /// <summary>
    /// Una promozione scritta da un'altra parte <b>non</b> fa effetto da sola: la fa la ricarica. È il
    /// prezzo dichiarato del fotogramma in memoria, e chi scrive deve ricaricare.
    /// </summary>
    [Fact]
    public async Task Una_promozione_fa_effetto_solo_dopo_la_ricarica()
    {
        var (cache, deposito) = Cache();
        await cache.ReloadAsync();
        Assert.Null(cache.For(Promosso));

        deposito.Righe[Promosso] = VipiRole.Admin;
        Assert.Null(cache.For(Promosso));      // ancora il fotogramma di prima

        await cache.ReloadAsync();
        Assert.Equal(VipiRole.Admin, cache.For(Promosso));
    }

    /// <summary>E un declassamento pure: se la riga sparisce, deve sparire anche dal fotogramma.</summary>
    [Fact]
    public async Task Togliere_una_promozione_fa_effetto_dopo_la_ricarica()
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = VipiRole.Admin;
        await cache.ReloadAsync();

        deposito.Righe.Remove(Promosso);
        await cache.ReloadAsync();

        Assert.Null(cache.For(Promosso));
        Assert.Empty(cache.All);
    }

    [Fact]
    public async Task Il_fotogramma_intero_e_quello_che_si_mostra_in_pagina()
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = VipiRole.Editor;
        deposito.Righe[111222] = VipiRole.DivisionStaff;

        await cache.ReloadAsync();

        Assert.Equal(2, cache.All.Count);
        Assert.Equal(VipiRole.Editor, cache.All[Promosso]);
        Assert.Equal(VipiRole.DivisionStaff, cache.All[111222]);
    }

    /// <summary>
    /// Il pavimento, provato dove vive: <c>max</c> fra ciò che dà lo staff e ciò che dà la promozione. Un
    /// «declassamento» sotto il pavimento non è vietato — è <b>inerte</b>, e deve restare tale.
    /// </summary>
    [Theory]
    [InlineData("IT-T01", VipiRole.Admin, VipiRole.Admin)]              // promosso davvero
    [InlineData("IT-T01", VipiRole.User, VipiRole.DivisionStaff)]       // declassato sotto il pavimento: inerte
    [InlineData("IT-DIR", VipiRole.IvaoStaff, VipiRole.Admin)]          // la direzione non si declassa
    [InlineData("LIRR-CH", VipiRole.Admin, VipiRole.Admin)]             // un chief promosso ad admin
    [InlineData("LIRR-CH", VipiRole.DivisionStaff, VipiRole.Editor)]    // e uno "declassato": resta Editor
    public async Task Il_livello_effettivo_e_il_massimo_fra_staff_e_promozione(
        string posizione, VipiRole promozione, VipiRole atteso)
    {
        var (cache, deposito) = Cache();
        deposito.Righe[Promosso] = promozione;
        await cache.ReloadAsync();

        var daStaff = new RoleResolver(new AuthOptions(), new DivisionOptions())
            .Resolve(Promosso, new[] { posizione });

        var effettivo = (VipiRole)Math.Max((int)daStaff, (int)(cache.For(Promosso) ?? VipiRole.User));

        Assert.Equal(atteso, effettivo);
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Lock di editing esclusivo su risorse nominate (pagine admin di struttura / nuovo doc). ResourceLockService + EfResourceLockRepository.</summary>
public class ResourceLockTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private const string Key = ResourceLockKeys.Structure;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static CurrentUser Admin(int uid) => new(uid, $"Admin{uid}", "LIRR", new[] { "IT-AOC" });
    private static CurrentUser Plain(int uid) => new(uid, $"User{uid}", "LIRR", System.Array.Empty<string>());

    private ResourceLockService Build(CurrentUser user)
    {
        var provider = new FakeUser { User = user };
        var authz = new EditAuthorizationService(provider,
            new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()), SenzaPromozioni.Instance);
        return new ResourceLockService(new EfResourceLockRepository(_db), authz);
    }

    [Fact]
    public async Task Second_Editor_Is_Blocked_Until_Release()
    {
        var a = Build(Admin(1));
        var b = Build(Admin(2));

        var lockA = await a.AcquireAsync(Key);
        Assert.True(lockA.IsMine);

        // B vede il lock altrui e non riesce ad acquisirlo.
        var lockB = await b.AcquireAsync(Key);
        Assert.True(lockB.Locked);
        Assert.False(lockB.IsMine);
        Assert.Equal(1, lockB.ByUserId);

        // Le azioni di B sono bloccate finché A tiene il lock.
        await Assert.ThrowsAsync<EditConflictException>(() => b.EnsureHeldAsync(Key));

        // A rilascia → B acquisisce.
        await a.ReleaseAsync(Key);
        var lockB2 = await b.AcquireAsync(Key);
        Assert.True(lockB2.IsMine);
    }

    [Fact]
    public async Task Admin_Can_Force_Unlock_Others()
    {
        var a = Build(Admin(1));
        var b = Build(Admin(2));
        await a.AcquireAsync(Key);

        await b.ForceUnlockAsync(Key);          // admin: sblocca il lock di A
        var lockB = await b.AcquireAsync(Key);
        Assert.True(lockB.IsMine);
    }

    [Fact]
    public async Task Non_Admin_Cannot_Force_Unlock()
    {
        var a = Build(Admin(1));
        await a.AcquireAsync(Key);

        var plain = Build(Plain(555));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => plain.ForceUnlockAsync(Key));
    }

    /// <summary>
    /// Il lock della struttura è di una pagina admin, quindi lo prende solo un admin. Fino all'11 agosto
    /// 2026 bastava essere autenticati: qualunque membro IVAO poteva prenderlo e — con l'heartbeat della
    /// UI — tenerlo per sempre, lasciando gli admin fuori dal proprio strumento. Il force-unlock lo
    /// risolveva ogni volta, ma la volta dopo ricominciava.
    /// </summary>
    [Fact]
    public async Task Non_Admin_Cannot_Take_The_Structure_Lock()
    {
        var plain = Build(Plain(555));

        await Assert.ThrowsAsync<EditNotAllowedException>(() => plain.AcquireAsync(Key));

        // E non ha lasciato niente dietro: l'admin trova la risorsa libera.
        var admin = Build(Admin(1));
        Assert.True((await admin.AcquireAsync(Key)).IsMine);
    }

    /// <summary>
    /// Il gemello: <c>editor:newdoc</c> resta prendibile da chi non è admin. Creare un documento è già
    /// filtrato dai grant per ACC dentro il servizio che lo crea, e chiedere l'admin qui toglierebbe il
    /// lock proprio a chi ha il permesso di scrivere.
    /// </summary>
    [Fact]
    public async Task Non_Admin_Can_Still_Take_The_NewDoc_Lock()
    {
        var plain = Build(Plain(555));
        Assert.True((await plain.AcquireAsync(ResourceLockKeys.NewDoc)).IsMine);
    }

    [Fact]
    public async Task Owner_Reacquire_Is_Idempotent()
    {
        var a = Build(Admin(1));
        var first = await a.AcquireAsync(Key);
        var second = await a.AcquireAsync(Key);   // già mio → resta mio, nessun conflitto
        Assert.True(first.IsMine);
        Assert.True(second.IsMine);
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Autorizzazione ACC-scoped + lock esclusivo + concorrenza ottimistica sui blocchi.</summary>
public class AuthLockTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _accDocId;

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
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        _accDocId = await _db.Documents.Where(d => d.Type == DocumentType.Vipi).Select(d => d.Id).FirstAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static CurrentUser Admin(int UserId) => new(UserId, $"Admin{UserId}", "LIRR", new[] { "IT-AOC" });
    private static CurrentUser Plain(int UserId) => new(UserId, $"User{UserId}", "LIRR", System.Array.Empty<string>());

    private (EditingService editing, EditAuthorizationService authz, EfEditGrantRepository grants) Build(CurrentUser user)
    {
        var provider = new FakeUser { User = user };
        var grants = new EfEditGrantRepository(_db);
        var authz = new EditAuthorizationService(provider, grants,
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.Auth.AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.DivisionOptions()));
        var editing = new EditingService(new EfEditingRepository(_db, new AiracService()), authz,
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()));
        return (editing, authz, grants);
    }

    [Fact]
    public async Task Admin_Can_Edit_Any_Acc()
    {
        var (editing, _, _) = Build(Admin(1));
        var draftId = await editing.CreateDraftAsync(_accDocId); // non lancia
        Assert.True(draftId > 0);
    }

    [Theory]
    [InlineData("LIRR-CH", true)]     // chief ACC → admin completo
    [InlineData("LIMM-ACH", true)]    // assistant chief ACC → admin completo
    [InlineData("IT-DIR", true)]      // ruolo di divisione → admin
    [InlineData("LIRR-TC", false)]    // altro ruolo ACC → non admin
    [InlineData("LIRR-CHX", false)]   // suffisso non esatto → non admin
    public void Acc_Chief_Roles_Are_Admin(string staffCode, bool expectedAdmin)
    {
        var user = new CurrentUser(42, "Tizio", "LIRR", new[] { staffCode });
        var (_, authz, _) = Build(user);
        Assert.Equal(expectedAdmin, authz.IsAdmin);
    }

    [Fact]
    public async Task Plain_User_Without_Grant_Is_Denied()
    {
        var (editing, _, _) = Build(Plain(555));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => editing.CreateDraftAsync(_accDocId));
    }

    [Fact]
    public async Task Granted_User_Edits_Only_Granted_Acc()
    {
        // admin concede a 555 la ACC LIRR
        var (_, adminAuthz, _) = Build(Admin(1));
        await adminAuthz.AddGrantAsync(555, "Mario", "LIRR");

        var (editing, authz, _) = Build(Plain(555));
        var draftId = await editing.CreateDraftAsync(_accDocId);   // LIRR concesso → ok
        Assert.True(draftId > 0);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => authz.EnsureCanEditAccAsync("LIMM")); // altra ACC → negato
    }

    [Fact]
    public async Task Grant_Management_Is_Admin_Only()
    {
        var (_, plainAuthz, _) = Build(Plain(555));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => plainAuthz.AddGrantAsync(777, null, "LIRR"));
    }

    [Fact]
    public async Task Lock_Blocks_Second_Editor_Until_Force_Unlock()
    {
        var (edA, _, _) = Build(Admin(1));
        var lockA = await edA.AcquireLockAsync(_accDocId);
        Assert.True(lockA.IsMine);

        var (edB, _, _) = Build(Admin(2));
        var lockB = await edB.AcquireLockAsync(_accDocId);
        Assert.False(lockB.IsMine);
        Assert.Equal(1, lockB.ByUserId);

        // B non può creare bozza mentre A tiene il lock
        await Assert.ThrowsAsync<EditConflictException>(() => edB.CreateDraftAsync(_accDocId));

        // B (admin) forza lo sblocco e poi acquisisce
        await edB.ForceUnlockAsync(_accDocId);
        var lockB2 = await edB.AcquireLockAsync(_accDocId);
        Assert.True(lockB2.IsMine);
    }

    [Fact]
    public async Task Stale_Block_Update_Raises_Conflict()
    {
        var (editing, _, _) = Build(Admin(1));
        var draftId = await editing.CreateDraftAsync(_accDocId);   // acquisisce il lock
        var doc = await editing.LoadForEditAsync(_accDocId);
        var blk = doc!.Sections.SelectMany(Flatten).SelectMany(s => s.Blocks)
            .First(b => b.Format == BlockFormat.Prose);
        var staleToken = blk.RowVersion;

        // primo salvataggio col token valido → ok (bumpa il RowVersion)
        await editing.UpdateBlockAsync(blk.Id, new BlockEdit
        { Tier = blk.Tier, Visibility = blk.Visibility, Body = "v1", RowVersion = staleToken });

        // secondo salvataggio col token ORMAI vecchio → conflitto
        await Assert.ThrowsAsync<EditConflictException>(() => editing.UpdateBlockAsync(blk.Id, new BlockEdit
        { Tier = blk.Tier, Visibility = blk.Visibility, Body = "v2", RowVersion = staleToken }));
    }

    private static IEnumerable<EditableSection> Flatten(EditableSection s)
    {
        yield return s;
        foreach (var c in s.Children) foreach (var d in Flatten(c)) yield return d;
    }
}

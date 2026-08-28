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
            new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()), SenzaPromozioni.Instance);
        var editing = new EditingService(new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)), authz,
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

    /// <summary>
    /// ⚠️ <b>Dal 28 agosto 2026 il chief d'ACC non è più admin: è Editor.</b> Cura i documenti — tutti, non
    /// solo la sua ACC — e non distribuisce permessi. Il test è rimasto lo stesso caso, ma la colonna
    /// attesa è cambiata: è qui che si legge il cambio di regola.
    /// </summary>
    [Theory]
    [InlineData("LIRR-CH", VipiRole.Editor)]           // chief ACC → cura i documenti
    [InlineData("LIMM-ACH", VipiRole.Editor)]          // assistant chief ACC → idem
    [InlineData("IT-DIR", VipiRole.Admin)]             // direzione della divisione → admin
    [InlineData("IT-AOA1", VipiRole.DivisionStaff)]    // staff nostro fuori dagli otto → solo statistiche
    [InlineData("LIRR-TC", VipiRole.IvaoStaff)]        // altro ruolo ACC → nessun permesso qui
    [InlineData("LIRR-CHX", VipiRole.IvaoStaff)]       // suffisso non esatto → i pattern sono ancorati
    public void Il_livello_segue_il_codice_staff(string staffCode, VipiRole atteso)
    {
        var user = new CurrentUser(42, "Tizio", "LIRR", new[] { staffCode });
        var (_, authz, _) = Build(user);

        Assert.Equal(atteso, authz.Role);
        Assert.Equal(atteso >= VipiRole.Admin, authz.IsAdmin);
        Assert.Equal(atteso >= VipiRole.Editor, authz.IsEditor);
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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Media;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Deposito immagini: content-addressed (stesso file = stessa riga) e immutabile, perché lo sha finisce dentro gli
/// snapshot di release e deve continuare a risolversi anche dopo che il blocco che lo citava è stato cancellato.
/// </summary>
public class MediaStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(4242, "Tester", "LIRR", new[] { "IT-AOC" });
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EfMediaStore Store(MediaOptions? options = null) =>
        new(_db, new FakeUser(), Options.Create(options ?? new MediaOptions()));

    /// PNG minimo ma riconoscibile: conta l'intestazione, non i pixel.
    private static byte[] Png(int w, int h, byte marcatore = 0)
    {
        var b = new byte[25];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        b[11] = 0x0D;
        "IHDR"u8.ToArray().CopyTo(b, 12);
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        b[24] = marcatore;   // per ottenere due contenuti diversi a parità di dimensioni
        return b;
    }

    [Fact]
    public async Task Salva_e_rilegge_con_tipo_e_dimensioni_dai_byte()
    {
        var saved = await Store().SaveAsync(new MemoryStream(Png(1600, 900)), "foto.png");

        Assert.Equal(64, saved.Sha256.Length);
        Assert.Equal("image/png", saved.ContentType);
        Assert.Equal(1600, saved.Width);
        Assert.Equal(900, saved.Height);

        var back = await Store().GetAsync(saved.Sha256);
        Assert.NotNull(back);
        Assert.Equal("image/png", back!.ContentType);
        Assert.Equal(saved.ByteSize, back.Bytes.Length);
    }

    [Fact]
    public async Task Stesso_file_caricato_due_volte_e_una_riga_sola()
    {
        var store = Store();

        var primo = await store.SaveAsync(new MemoryStream(Png(800, 600)), "a.png");
        var secondo = await store.SaveAsync(new MemoryStream(Png(800, 600)), "b.png");

        Assert.Equal(primo.Sha256, secondo.Sha256);
        Assert.Equal(1, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task Contenuti_diversi_hanno_sha_diversi()
    {
        var store = Store();

        var uno = await store.SaveAsync(new MemoryStream(Png(800, 600, marcatore: 1)), null);
        var due = await store.SaveAsync(new MemoryStream(Png(800, 600, marcatore: 2)), null);

        Assert.NotEqual(uno.Sha256, due.Sha256);
        Assert.Equal(2, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task Oltre_il_limite_configurato_viene_rifiutata_e_nulla_viene_scritto()
    {
        var store = Store(new MediaOptions { MaxUploadBytes = 16 });

        await Assert.ThrowsAsync<ValidationException>(
            () => store.SaveAsync(new MemoryStream(Png(800, 600)), "grande.png"));

        Assert.Equal(0, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task File_che_non_e_immagine_viene_rifiutato()
    {
        var store = Store();
        var finto = System.Text.Encoding.UTF8.GetBytes("PK sono uno zip travestito da png");

        await Assert.ThrowsAsync<ValidationException>(() => store.SaveAsync(new MemoryStream(finto), "foto.png"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("non-uno-sha")]
    [InlineData("../../etc/passwd")]
    [InlineData("ZZZZ567890123456789012345678901234567890123456789012345678901234")]
    public async Task Sha_non_valido_torna_null_senza_interrogare_il_database(string sha)
    {
        Assert.Null(await Store().GetAsync(sha));
    }

    [Fact]
    public async Task Lo_sha_sopravvive_al_blocco_che_lo_citava()
    {
        // Non c'è FK verso i blocchi: cancellare un blocco (o un documento) non deve togliere i byte da sotto una
        // release già pubblicata, che nel payload porta solo lo sha.
        var saved = await Store().SaveAsync(new MemoryStream(Png(640, 480)), "vecchia.png");

        Assert.Empty(_db.Model.FindEntityType(typeof(Vipi.Domain.Entities.MediaAsset))!.GetForeignKeys());
        Assert.NotNull(await Store().GetAsync(saved.Sha256));
    }
}

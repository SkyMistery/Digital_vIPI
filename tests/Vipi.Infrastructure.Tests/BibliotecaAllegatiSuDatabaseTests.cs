using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-048 (revisione totale 3): la biblioteca degli allegati sul database.
///
/// <para><b>Il difetto.</b> Il titolo ha una colonna da 200 caratteri e la nota di versione una da 500, e la
/// biblioteca non li guardava: <c>AttachmentRules.TitleMaxLength</c> esisteva e non lo usava nessuno. Su MariaDB il
/// salvataggio falliva a metà, e le righe <c>Added</c> restavano nel DbContext del CIRCUITO (la pagina lo prende di
/// lì): ogni salvataggio dopo, anche giusto, ritentava anche quelle e falliva a sua volta, finché non si ricaricava.</para>
///
/// <para>⚠️ SQLite le lunghezze non le fa rispettare: il rifiuto per lunghezza si prova con l'esito, il contesto
/// sporco con un salvataggio che fallisce apposta.</para>
/// </summary>
public class BibliotecaAllegatiSuDatabaseTests : IAsyncLifetime
{
    private const string Link = "https://drive.google.com/file/d/AAAAAAAAAAAA/view";
    private const string AltroLink = "https://drive.google.com/file/d/BBBBBBBBBBBB/view";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly SalvataggioCheCade _guasto = new();
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn)
            .AddInterceptors(_guasto).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Fa fallire il prossimo salvataggio come lo farebbe il database: un indice o una colonna violati.</summary>
    private sealed class SalvataggioCheCade : SaveChangesInterceptor
    {
        public bool Cadi { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Cadi) return ValueTask.FromResult(result);
            Cadi = false;
            throw new DbUpdateException("Data too long for column 'Title' at row 1");
        }
    }

    private EfAttachmentLibrary Biblioteca() => new(_db);

    private static AttachmentDraft Bozza(string slug, string titolo) =>
        new(slug, titolo, AttachmentKind.Loa, AttachmentScope.Division, null, null, Link);

    [Fact]
    public async Task Un_titolo_oltre_la_colonna_si_rifiuta_prima_di_scrivere()
    {
        var (esito, riga) = await Biblioteca().CreateAsync(
            Bozza("loa-lunga", new string('T', AttachmentRules.TitleMaxLength + 1)), userId: 1);

        Assert.Equal(AttachmentCreate.TitoloTroppoLungo, esito);
        Assert.Null(riga);
        Assert.False(await _db.Attachments.AnyAsync());

        var (giusto, _) = await Biblioteca().CreateAsync(
            Bozza("loa-giusta", new string('T', AttachmentRules.TitleMaxLength)), userId: 1);
        Assert.Equal(AttachmentCreate.Ok, giusto);
    }

    [Fact]
    public async Task Una_nota_di_versione_oltre_la_colonna_si_rifiuta_prima_di_scrivere()
    {
        await Biblioteca().CreateAsync(Bozza("loa-lirr-lfmm", "LoA LIRR–LFMM"), userId: 1);

        var (esito, _) = await Biblioteca().ReplaceAsync(
            "loa-lirr-lfmm", AltroLink, new string('n', AttachmentRules.NoteMaxLength + 1), userId: 1);

        Assert.Equal(AttachmentReplace.NotaTroppoLunga, esito);
        Assert.Equal(1, await _db.AttachmentVersions.CountAsync());
    }

    /// <summary>
    /// Il cuore del difetto: dopo un salvataggio fallito il contesto resta pulito, e il gesto dopo salva solo sé stesso.
    /// Prima il secondo salvataggio si portava dietro le righe del primo (e su MariaDB falliva con loro).
    /// </summary>
    [Fact]
    public async Task Dopo_un_salvataggio_fallito_il_contesto_resta_pulito()
    {
        _guasto.Cadi = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => Biblioteca().CreateAsync(Bozza("loa-prima", "Prima"), userId: 1));

        Assert.Empty(_db.ChangeTracker.Entries());

        var (esito, _) = await Biblioteca().CreateAsync(Bozza("loa-seconda", "Seconda"), userId: 1);
        Assert.Equal(AttachmentCreate.Ok, esito);
        Assert.Equal(new[] { "loa-seconda" }, await _db.Attachments.Select(a => a.Slug).ToListAsync());
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I due ingressi di <see cref="ISidImporter"/>. L'importatore riscrive righe — <c>ReplaceImportedSidsAsync</c>
/// fa delete+add — ed era, fino all'11 agosto 2026, l'unico percorso di scrittura del progetto senza
/// <c>EnsureCanEdit*</c>: oltre sessanta chiamate su venti servizi, e questa mancava. Non era sfruttabile
/// (Blazor consegna solo gli eventi dell'albero renderizzato, e il bottone sta dietro il controllo di editing
/// della pagina), ma il principio è scritto in cima a <c>IEditAuthorizationService</c>: «verifica sempre
/// server-side».
///
/// <para>Prima di questo file l'importatore SID non aveva <b>alcun</b> test.</para>
/// </summary>
public class SidImporterAuthorizationTests : IAsyncLifetime
{
    private const string Icao = "LIRF";
    private const string Acc = "LIRR";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = Acc, Name = "Roma" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
        _db.Airports.Add(new Airport { Icao = Icao, Name = "Fiumicino", AccId = acc.Id });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    /// <summary>Una SID sola: qui conta chi può scrivere, non che cosa si scrive.</summary>
    private sealed class UnaSid : ISidProvider
    {
        public Task<IReadOnlyList<SourceSid>> GetSidsAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceSid>>(new[]
            {
                new SourceSid(icao, "16R", "OST", "OST1A", null, "RNAV", $"{icao}|OST|A||16R", false),
            });
    }

    private sealed class TuttoImportato : IImportPolicyStore
    {
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(ImportPolicySnapshot.AllImported);

        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) =>
            Task.FromResult(new ImportPolicyInfo(ImportPolicySnapshot.AllImported, null, 0));

        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private SidImporter Build(CurrentUser? user)
    {
        var provider = new FakeUser { User = user };
        var authz = new EditAuthorizationService(provider, new EfEditGrantRepository(_db),
            Microsoft.Extensions.Options.Options.Create(new AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.DivisionOptions()));

        return new SidImporter(new UnaSid(), new EfAirportRepository(_db, new EfMediaMaintenance(_db)),
            new TuttoImportato(), new AiracService(), authz);
    }

    private async Task<int> SidImportateAsync() =>
        await _db.AirportSids.CountAsync(s => s.IsImported);

    /// <summary>L'ingresso della UI chiede il permesso sulla ACC dell'aeroporto.</summary>
    [Fact]
    public async Task Un_utente_senza_permessi_non_importa_le_SID()
    {
        var importer = Build(new CurrentUser(555, "Tizio", Acc, Array.Empty<string>()));

        await Assert.ThrowsAsync<EditNotAllowedException>(() => importer.ImportForCurrentUserAsync(Icao));
        Assert.Equal(0, await SidImportateAsync());   // e non ha scritto niente prima di rifiutare
    }

    [Fact]
    public async Task Un_anonimo_non_importa_le_SID()
    {
        var importer = Build(null);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => importer.ImportForCurrentUserAsync(Icao));
        Assert.Equal(0, await SidImportateAsync());
    }

    [Fact]
    public async Task Un_admin_importa_le_SID()
    {
        var importer = Build(new CurrentUser(1, "Capo", Acc, new[] { "IT-AOC" }));

        Assert.Equal(1, await importer.ImportForCurrentUserAsync(Icao));
        Assert.Equal(1, await SidImportateAsync());
    }

    /// <summary>
    /// Il job periodico gira senza utente e deve continuare a funzionare: è il motivo per cui l'ingresso
    /// senza controllo resta, invece di sparire.
    /// </summary>
    [Fact]
    public async Task Il_job_di_sistema_importa_anche_senza_utente()
    {
        var importer = Build(null);

        Assert.Equal(1, await importer.ImportAsync(Icao));
        Assert.Equal(1, await SidImportateAsync());
    }

    /// <summary>Aeroporto inesistente: errore di validazione leggibile, non NullReference.</summary>
    [Fact]
    public async Task Un_aeroporto_sconosciuto_da_un_errore_di_validazione()
    {
        var importer = Build(new CurrentUser(1, "Capo", Acc, new[] { "IT-AOC" }));

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => importer.ImportForCurrentUserAsync("ZZZZ"));
    }
}

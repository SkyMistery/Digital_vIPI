using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// C7b — le cancellazioni della Struttura (ACC, aeroporto, settore) finivano fuori dal registro, mentre
/// l'eliminazione di un <b>documento</b> ci finisce dal 22 agosto 2026: era il «buco 5» dell'audit di quel
/// giorno, chiuso solo per <c>SetParentAsync</c>. ⚠️ La riga si scrive <b>prima</b> della cancellazione —
/// dopo, callsign e ICAO non sono più leggibili e resterebbe «eliminato il settore 7».
/// </summary>
public class CancellazioniStrutturaliNelRegistroTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfStructureEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfStructureEditingRepository(_db, new AttoreFinto());
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Il_settore_eliminato_lascia_callsign_e_nome_nel_registro()
    {
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        var id = await _repo.AddSectorAsync("LIMM", "LIMM_NW_CTR", SectorType.Ctr, SectorKind.Acc,
            "Milano Radar NW", "128.800", 10, null, null, null);

        await _repo.DeleteSectorAsync("LIMM", id);

        var riga = await _db.AuditLogs.SingleAsync(a => a.EntityType == "Sector");
        Assert.Equal(AuditAction.Delete, riga.Action);
        Assert.Equal(id.ToString(), riga.EntityId);
        Assert.Equal(7, riga.UserId);
        Assert.Contains("LIMM_NW_CTR", riga.DetailsJson);
        Assert.Contains("Milano Radar NW", riga.DetailsJson);
        Assert.Empty(await _db.Sectors.ToListAsync());
    }

    [Fact]
    public async Task L_aeroporto_eliminato_lascia_ICAO_e_nome_nel_registro()
    {
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        var aptId = await _repo.CreateAirportAsync("LIMM", "LIML", "Milano Linate");

        await _repo.DeleteAirportAsync("LIMM", aptId);

        var riga = await _db.AuditLogs.SingleAsync(a => a.EntityType == "Airport");
        Assert.Equal(AuditAction.Delete, riga.Action);
        Assert.Contains("LIML", riga.DetailsJson);
        Assert.Contains("Milano Linate", riga.DetailsJson);
    }

    [Fact]
    public async Task L_ACC_eliminata_lascia_codice_e_nome_nel_registro()
    {
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        await _repo.CreateAirportAsync("LIMM", "LIML", "Milano Linate");   // segue la ACC

        await _repo.DeleteAccAsync("LIMM");

        var riga = await _db.AuditLogs.SingleAsync(a => a.EntityType == "Acc");
        Assert.Equal(AuditAction.Delete, riga.Action);
        Assert.Equal("LIMM", riga.EntityId);                 // il codice, non l'Id numerico: è la chiave leggibile
        Assert.Contains("Milano ACC", riga.DetailsJson);
        Assert.Contains("\"Aeroporti\":1", riga.DetailsJson);   // quanti sono spariti con lei
    }

    [Fact]
    public async Task Una_cancellazione_rifiutata_non_scrive_niente()
    {
        // La riga di audit sta nella STESSA SaveChanges dell'atto: un blocco che lancia prima non deve
        // lasciare un registro che racconta un fatto mai avvenuto.
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        await _repo.AddSectorAsync("LIMM", "LIMM_NW_CTR", SectorType.Ctr, SectorKind.Acc,
            "Milano Radar NW", "128.800", 10, null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.DeleteAccAsync("LIMM"));

        Assert.Empty(await _db.AuditLogs.ToListAsync());
    }

    private sealed class AttoreFinto : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 7;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}

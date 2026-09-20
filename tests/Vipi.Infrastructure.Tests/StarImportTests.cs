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
/// Le STAR entrano nella stessa tabella delle SID: quel che va provato è che <b>i due versi non si tocchino</b>
/// — importare gli arrivi non deve poter cancellare le partenze, né a mano né dal giro automatico.
/// </summary>
public class StarImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc });
        await _db.SaveChangesAsync();
        _repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static ImportedProcedure Imp(string name, string fix, string key, string? rwy = "16L") =>
        new(Runway: rwy, Fix: fix, Name: name, Transition: null, Type: "RNAV", StableKey: key, NeedsFixReview: false);

    private Task<int> ConteggioAsync(ProcedureKind kind) =>
        _db.AirportProcedures.CountAsync(p => p.Kind == kind);

    [Fact]
    public async Task L_Import_Degli_Arrivi_Non_Tocca_Le_Partenze()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid,
            new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G||16L") }, "2606");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");

        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Sid));
        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Star));

        // Un secondo giro degli arrivi, con una revisione nuova: le partenze restano dov'erano.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI4A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2607");

        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Sid));
        var partenza = await _db.AirportProcedures.SingleAsync(p => p.Kind == ProcedureKind.Sid);
        Assert.Equal("ALAX7G", partenza.Name);
        var arrivo = await _db.AirportProcedures.SingleAsync(p => p.Kind == ProcedureKind.Star);
        Assert.Equal("GILI4A", arrivo.Name);
    }

    [Fact]
    public async Task La_Scheda_Delle_Sid_Non_Vede_Gli_Arrivi()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid,
            new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G||16L") }, "2606");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids;

        Assert.Equal("ALAX7G", Assert.Single(sids).Name);
    }

    [Fact]
    public async Task Le_Manuali_Salvate_Non_Cancellano_Gli_Arrivi()
    {
        // SaveSidsAsync riscrive TUTTE le righe manuali dello scalo: senza il filtro sul verso si porterebbe via
        // anche le STAR importate, che manuali non sono ma nella stessa tabella stanno.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");

        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new SidRow(0, "16L", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null),
        });

        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Star));
    }

    [Fact]
    public async Task La_Scheda_Porta_Gli_Arrivi_A_Parte()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid,
            new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G||16L") }, "2606");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");

        var dati = (await _repo.LoadAsync("LIRF"))!;

        Assert.Equal("ALAX7G", Assert.Single(dati.Sids).Name);
        Assert.Equal("GILI3A", Assert.Single(dati.Stars).Name);
    }

    [Fact]
    public async Task Le_Manuali_Di_Un_Verso_Non_Toccano_L_Altro()
    {
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new SidRow(0, "16L", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null),
        });
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Star, new[]
        {
            new SidRow(0, "16L", "GILIO", "GILI3A", null, null, "RNAV", null, null, null),
        });

        var dati = (await _repo.LoadAsync("LIRF"))!;
        Assert.Equal("OST7A", Assert.Single(dati.Sids).Name);
        Assert.Equal("GILI3A", Assert.Single(dati.Stars).Name);

        // Riscrivere le manuali degli arrivi non si porta via quelle delle partenze.
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Star, Array.Empty<SidRow>());

        var dopo = (await _repo.LoadAsync("LIRF"))!;
        Assert.Single(dopo.Sids);
        Assert.Empty(dopo.Stars);
    }

    [Fact]
    public async Task La_Priorita_Si_Riapplica_Dentro_Il_Suo_Verso()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");
        var prima = await _db.AirportProcedures.SingleAsync(p => p.Kind == ProcedureKind.Star);
        await _repo.UpdateImportedSidAsync(prima.Id, priority: 2, forcePublished: true, resolvedFix: null,
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        // Revisione nuova, stessa StableKey: priorità e forzatura seguono la riga.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI4A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2607");

        var dopo = await _db.AirportProcedures.SingleAsync(p => p.Kind == ProcedureKind.Star);
        Assert.Equal("GILI4A", dopo.Name);
        Assert.Equal(2, dopo.Priority);
        Assert.True(dopo.ForcePublished);
        // Il nome è cambiato: è una revisione NUOVA, e entra in vigore dal ciclo di adesso. (Il primo ciclo si
        // conserva solo quando il contenuto è identico — stessa regola delle SID, carta §AW2.)
        Assert.Equal("2607", dopo.SourceAiracCycle);
    }

    // --- L'importatore: un giro, due versi -----------------------------------------------------------

    private sealed class DueVersi : IProcedureProvider
    {
        public Task<IReadOnlyList<SourceProcedure>> GetAsync(string icao, ProcedureKind kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceProcedure>>(kind == ProcedureKind.Star
                ? new[] { new SourceProcedure(icao, "16L", "GILIO", "GILI3A", null, "RNAV", $"STAR|{icao}|GILIO|A||16L", false, kind) }
                : new[] { new SourceProcedure(icao, "16L", "ALAXI", "ALAX7G", null, "RNAV", $"{icao}|ALAXI|G||16L", false, kind) });
    }

    /// <summary>La sorgente ha le partenze ma non gli arrivi: il caso di 36 `.str` su 90.</summary>
    private sealed class SoloPartenze : IProcedureProvider
    {
        public Task<IReadOnlyList<SourceProcedure>> GetAsync(string icao, ProcedureKind kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceProcedure>>(kind == ProcedureKind.Star
                ? Array.Empty<SourceProcedure>()
                : new[] { new SourceProcedure(icao, "16L", "ALAXI", "ALAX7G", null, "RNAV", $"{icao}|ALAXI|G||16L", false, kind) });
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

    private ProcedureImporter Importatore(IProcedureProvider provider) =>
        new(provider, _repo, new TuttoImportato(), new AiracService(),
            new Vipi.Application.Auth.EditAuthorizationService(
                new SenzaUtente(),
                new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()),
                SenzaPromozioni.Instance));

    private sealed class SenzaUtente : ICurrentUserProvider
    {
        public CurrentUser? Get() => null;
    }

    [Fact]
    public async Task Un_Giro_Importa_Partenze_E_Arrivi()
    {
        var scritte = await Importatore(new DueVersi()).ImportAsync("LIRF");

        Assert.Equal(2, scritte);
        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Sid));
        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Star));
    }

    [Fact]
    public async Task Un_Verso_Senza_Righe_Non_Cancella_Quel_Che_Ce()
    {
        // Le STAR ci sono già (import precedente, o file sparito dalla sorgente): un giro che non ne porta
        // NESSUNA non è «non ce ne sono più».
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A||16L") }, "2606");

        var scritte = await Importatore(new SoloPartenze()).ImportAsync("LIRF");

        Assert.Equal(1, scritte);                                      // solo la partenza
        Assert.Equal(1, await ConteggioAsync(ProcedureKind.Star));     // l'arrivo di prima è ancora lì
    }
}

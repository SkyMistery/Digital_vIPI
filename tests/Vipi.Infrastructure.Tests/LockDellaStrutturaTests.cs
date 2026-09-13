using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 <b>T-025</b> (revisione del 13 settembre 2026): le scritture delle cinque pagine di struttura (Struttura,
/// ACC, Aeroporti, Trasferimenti, Confinanti) pretendono il lock <c>admin:structure</c>. Prima nessun servizio lo
/// guardava: le pagine spengono i comandi senza lock, ma lo scoprono solo al battito successivo (60 s), e in
/// quella finestra — dopo uno «sblocca comunque» — continuavano a salvare sopra chi il lock l'aveva preso.
///
/// <para>Si prova la <b>porta</b>: il lock è la prima cosa che ogni metodo chiede, quindi le dipendenze che
/// verrebbero toccate dopo si passano nulle. Se un giorno qualcuno spostasse il controllo in fondo, il test
/// non uscirebbe con l'eccezione di conflitto e lo direbbe.</para>
/// </summary>
public class LockDellaStrutturaTests : IAsyncLifetime
{
    private const int Io = 1;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfResourceLockRepository _locks = default!;

    private sealed class Authz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => Io;
        public string? CurrentName => "io";
        public void EnsureAdmin() { }
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _locks = new EfResourceLockRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Costruisce un servizio col lock vero, l'autorizzazione di un admin e il resto a null.</summary>
    private T Servizio<T>(Type tipo)
    {
        var authz = new Authz();
        var locks = new ResourceLockService(_locks, authz);
        var ctor = tipo.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var argomenti = ctor.GetParameters().Select(p =>
        {
            if (p.ParameterType == typeof(IEditAuthorizationService)) return (object?)authz;
            if (p.ParameterType == typeof(IResourceLockService)) return locks;
            if (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(IOptions<>))
            {
                var valore = Activator.CreateInstance(p.ParameterType.GetGenericArguments()[0]);
                return typeof(Options).GetMethod(nameof(Options.Create))!
                    .MakeGenericMethod(p.ParameterType.GetGenericArguments()[0]).Invoke(null, new[] { valore });
            }
            return p.HasDefaultValue ? p.DefaultValue : null;
        }).ToArray();
        return (T)ctor.Invoke(argomenti);
    }

    private static Type Interno(string nome) =>
        typeof(IAccAdminService).Assembly.GetType("Vipi.Application.Content." + nome, throwOnError: true)!;

    /// <summary>Una scrittura per servizio, chiamata come la chiama la sua pagina.</summary>
    public static TheoryData<string> Scritture() => new()
    {
        "Accordi.AddAgreement", "Accordi.UpdateClause", "Accordi.DeleteClauses", "Accordi.RestoreClauses",
        "Struttura.SetAirportHidden", "Struttura.MoveAirport", "Struttura.GenerateAirportDocument",
        "Orfani.Reattach", "Gerarchia.SetParent", "Ripieghi.Replace",
        "Acc.SetHidden", "Acc.SetSubcenterLimits", "Acc.ImportFromSource",
        "Confinanti.SetStatus", "Confinanti.AddManual", "Confinanti.ImportAndCompute",
    };

    private Task Chiama(string scrittura) => scrittura switch
    {
        "Accordi.AddAgreement" => Servizio<IAgreementService>(typeof(AgreementService))
            .AddAgreementAsync("LIRR", null!),
        "Accordi.UpdateClause" => Servizio<IAgreementService>(typeof(AgreementService))
            .UpdateClauseAsync("LIRR", 1, null!),
        "Accordi.DeleteClauses" => Servizio<IAgreementService>(typeof(AgreementService))
            .DeleteClausesAsync("LIRR", new[] { 1 }),
        "Accordi.RestoreClauses" => Servizio<IAgreementService>(typeof(AgreementService))
            .RestoreClausesAsync("LIRR", Array.Empty<AgreementClauseRestore>()),
        "Struttura.SetAirportHidden" => Servizio<IStructureEditingService>(typeof(StructureEditingService))
            .SetAirportHiddenAsync("LIRR", 1, true),
        "Struttura.MoveAirport" => Servizio<IStructureEditingService>(typeof(StructureEditingService))
            .MoveAirportAsync(1, "LIRR", "LIMM"),
        "Struttura.GenerateAirportDocument" => Servizio<IStructureEditingService>(typeof(StructureEditingService))
            .GenerateAirportDocumentAsync("LIRF"),
        "Orfani.Reattach" => Servizio<IOrphanSectorService>(typeof(OrphanSectorService))
            .ReattachAsync(1, 2),
        "Gerarchia.SetParent" => Servizio<IHierarchyEditingService>(typeof(EfHierarchyEditingService))
            .SetParentAsync(HierarchyNodeKind.Acc, 1, "LIRR_CTR"),
        "Ripieghi.Replace" => Servizio<ISectorFallbackService>(typeof(EfSectorFallbackService))
            .ReplaceAsync("LIRR_NE_CTR", Array.Empty<FallbackRowEdit>()),
        "Acc.SetHidden" => Servizio<IAccAdminService>(Interno("AccAdminService")).SetHiddenAsync(1, true),
        "Acc.SetSubcenterLimits" => Servizio<IAccAdminService>(Interno("AccAdminService")).SetSubcenterLimitsAsync(1, 0, 245),
        "Acc.ImportFromSource" => Servizio<IAccAdminService>(Interno("AccAdminService")).ImportFromSourceAsync(),
        "Confinanti.SetStatus" => Servizio<INeighbourImportService>(Interno("NeighbourImportService"))
            .SetStatusAsync(1, NeighbourCandidateStatus.Confirmed),
        "Confinanti.AddManual" => Servizio<INeighbourImportService>(Interno("NeighbourImportService"))
            .AddManualAsync("LIRR", "LFMM", "Marseille", "FR", "LFMM_CTR", null),
        "Confinanti.ImportAndCompute" => Servizio<INeighbourImportService>(Interno("NeighbourImportService"))
            .ImportAndComputeAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(scrittura)),
    };

    /// <summary>
    /// Le pagine di struttura mostrano a schermo le <see cref="InvalidOperationException"/> dei loro gesti. Il
    /// conflitto di lock deve essere una di loro, o il primo «sblocca comunque» farebbe cadere il circuito invece
    /// di dire che il lock è di un altro.
    /// </summary>
    [Fact]
    public void Il_conflitto_di_lock_lo_mostrano_i_catch_che_le_pagine_hanno_gia() =>
        Assert.IsAssignableFrom<InvalidOperationException>(new EditConflictException("x"));

    [Theory]
    [MemberData(nameof(Scritture))]
    public async Task Senza_il_lock_della_struttura_non_si_scrive(string scrittura) =>
        await Assert.ThrowsAsync<EditConflictException>(() => Chiama(scrittura));

    /// <summary>Lo scenario del registro: il lock l'ha preso un altro dopo lo «sblocca comunque».</summary>
    [Theory]
    [MemberData(nameof(Scritture))]
    public async Task Col_lock_di_un_altro_non_si_scrive(string scrittura)
    {
        await _locks.AcquireOrInspectAsync(ResourceLockKeys.Structure, userId: 2, "altro", 3);
        await Assert.ThrowsAsync<EditConflictException>(() => Chiama(scrittura));
    }

    /// <summary>La controprova: col lock in mano la porta si apre. Il metodo prosegue e cade più avanti sulle
    /// dipendenze nulle — qualunque eccezione, purché non sia il conflitto di lock.</summary>
    [Theory]
    [MemberData(nameof(Scritture))]
    public async Task Col_lock_in_mano_la_porta_si_apre(string scrittura)
    {
        await _locks.AcquireOrInspectAsync(ResourceLockKeys.Structure, Io, "io", 3);
        var ex = await Record.ExceptionAsync(() => Chiama(scrittura));
        Assert.IsNotType<EditConflictException>(ex);
    }
}

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
/// U-232 (revisione 3, S39): il lock della struttura si provava <b>a campione</b> — sedici scritture su
/// cinquantacinque (<see cref="LockDellaStrutturaTests"/>). Togliere <c>await StrutturaAsync(ct)</c> da una delle altre
/// lasciava la suite verde, e con quella riga se ne vanno insieme il lock e il ruolo.
///
/// <para>Qui si provano <b>tutte</b>: per riflessione, ogni metodo dell'interfaccia di ognuno dei sette servizi
/// delle pagine di struttura. Ciò che non è una scrittura sta in <see cref="NonScritture"/>, per nome e col suo
/// perché. ⚠️ Un metodo nuovo che manca da quell'elenco è trattato da scrittura: se è una lettura il test lo dice,
/// e lo si aggiunge; se è una scrittura senza lock, il test è rosso ed è giusto così.</para>
/// </summary>
public class PorteTutteLeScrittureTests : IAsyncLifetime
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

    private static Type Interno(string nome) =>
        typeof(IAccAdminService).Assembly.GetType("Vipi.Application.Content." + nome, throwOnError: true)!;

    private static readonly (Type Interfaccia, Type Servizio)[] Servizi =
    {
        (typeof(IAgreementService), typeof(AgreementService)),
        (typeof(IStructureEditingService), typeof(StructureEditingService)),
        (typeof(IOrphanSectorService), typeof(OrphanSectorService)),
        (typeof(IHierarchyEditingService), typeof(EfHierarchyEditingService)),
        (typeof(ISectorFallbackService), typeof(EfSectorFallbackService)),
        (typeof(ISectorConfigurationService), typeof(EfSectorConfigurationService)),
        (typeof(IAccAdminService), Interno("AccAdminService")),
        (typeof(INeighbourImportService), Interno("NeighbourImportService")),
    };

    /// <summary>Le letture, e l'unica scrittura che il lock della struttura non lo chiede di proposito.</summary>
    private static readonly HashSet<string> NonScritture = new(StringComparer.Ordinal)
    {
        "IAgreementService.FindByPairAsync", "IAgreementService.ListByAccAsync", "IAgreementService.ListFlowsByAccAsync",
        "IAgreementService.ProcedureNonTrovateAsync", "IAgreementService.LevelWarningsAsync", "IAgreementService.ResolveForAccAsync",
        "IStructureEditingService.ListAccsAsync", "IStructureEditingService.ListAllAirportsAsync",
        "IStructureEditingService.ListAllSectorsAsync", "IStructureEditingService.ListSectorNodesAsync",
        "IStructureEditingService.LoadAsync", "IStructureEditingService.LookupExternalAirportAsync",
        "IOrphanSectorService.ListAsync", "IOrphanSectorService.ReattachTargetsAsync",
        "IHierarchyEditingService.ListConfiningForeignCallsignsAsync", "IHierarchyEditingService.LoadTreeAsync",
        "ISectorFallbackService.ListAsync", "ISectorFallbackService.RipiegoAutomaticoAsync", "ISectorFallbackService.SuggestAsync",
        "ISectorConfigurationService.GruppiAsync", "ISectorConfigurationService.ListAsync", "ISectorConfigurationService.TutteAsync",
        "IAccAdminService.ListAccsAsync", "IAccAdminService.ListSubcentersAsync",
        "INeighbourImportService.GetPairDetailAsync",
        // Scrive, ma la chiama la pagina degli spazi aerei, che il lock della struttura non lo tiene (commento su
        // NeighbourImportService.StrutturaAsync).
        "INeighbourImportService.RecomputeFromArchiveAsync",
    };

    public static TheoryData<string> Scritture()
    {
        var dati = new TheoryData<string>();
        foreach (var nome in Servizi.SelectMany(s => Metodi(s.Interfaccia)).Select(m => Nome(m)).Distinct()
                     .Where(n => !NonScritture.Contains(n)).OrderBy(n => n, StringComparer.Ordinal))
            dati.Add(nome);
        return dati;
    }

    public static TheoryData<string> Letture()
    {
        var dati = new TheoryData<string>();
        foreach (var n in NonScritture.Where(n => n != "INeighbourImportService.RecomputeFromArchiveAsync")
                     .OrderBy(n => n, StringComparer.Ordinal))
            dati.Add(n);
        return dati;
    }

    private static IEnumerable<MethodInfo> Metodi(Type interfaccia) =>
        interfaccia.GetMethods().Where(m => typeof(Task).IsAssignableFrom(m.ReturnType));

    private static string Nome(MethodInfo m) => m.DeclaringType!.Name + "." + m.Name;

    /// <summary>Il servizio col lock vero, l'autorizzazione di un admin e il resto a null (come LockDellaStrutturaTests).</summary>
    private object Servizio(Type tipo)
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
        return ctor.Invoke(argomenti);
    }

    /// <summary>Un argomento qualunque del tipo giusto: la porta viene prima di tutto, quindi il valore non conta.
    /// Gli elenchi di interi hanno un elemento, perché «nessun id» potrebbe uscire prima di chiedere niente.</summary>
    internal static object? Valore(Type t)
    {
        if (t == typeof(string)) return "LIRR";
        if (t == typeof(CancellationToken)) return CancellationToken.None;
        if (Nullable.GetUnderlyingType(t) is not null) return null;
        if (t == typeof(int)) return 1;
        if (t == typeof(long)) return 1L;
        if (t == typeof(bool)) return true;
        if (t.IsEnum) return Enum.GetValues(t).GetValue(0);
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 0);
        if (t.IsInterface && t.IsGenericType && t.GetGenericArguments().Length == 1
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
        {
            var el = t.GetGenericArguments()[0];
            var arr = Array.CreateInstance(el, el == typeof(int) ? 1 : 0);
            if (el == typeof(int)) arr.SetValue(1, 0);
            return t.IsAssignableFrom(arr.GetType()) ? arr : null;
        }
        return null;
    }

    /// <summary>Chiama tutti i sovraccarichi col nome dato; restituisce la prima eccezione di ognuno.</summary>
    internal static async Task<List<Exception?>> ChiamaAsync(object servizio, Type interfaccia, string metodo,
        Func<Type, object?> valore)
    {
        var esiti = new List<Exception?>();
        foreach (var m in Metodi(interfaccia).Where(m => m.Name == metodo))
        {
            try
            {
                await (Task)m.Invoke(servizio, m.GetParameters().Select(p => valore(p.ParameterType)).ToArray())!;
                esiti.Add(null);
            }
            catch (TargetInvocationException ex) { esiti.Add(ex.InnerException); }
            catch (Exception ex) { esiti.Add(ex); }
        }
        Assert.NotEmpty(esiti);
        return esiti;
    }

    private Task<List<Exception?>> ChiamaAsync(string nome)
    {
        var (interfaccia, servizio) = Servizi.Single(s => s.Interfaccia.Name == nome.Split('.')[0]);
        return ChiamaAsync(Servizio(servizio), interfaccia, nome.Split('.')[1], Valore);
    }

    [Theory]
    [MemberData(nameof(Scritture))]
    public async Task Senza_il_lock_della_struttura_nessuna_scrittura_passa(string scrittura)
    {
        foreach (var ex in await ChiamaAsync(scrittura))
            Assert.True(ex is EditConflictException,
                $"{scrittura} senza lock è uscito con {ex?.GetType().Name ?? "nessuna eccezione"}: se è una lettura va " +
                "in NonScritture, se è una scrittura le manca la porta.");
    }

    /// <summary>La controprova: col lock in mano il conflitto non c'è. Il metodo cade poi sulle dipendenze nulle.</summary>
    [Theory]
    [MemberData(nameof(Scritture))]
    public async Task Col_lock_in_mano_nessuna_scrittura_si_ferma_alla_porta(string scrittura)
    {
        await _locks.AcquireOrInspectAsync(ResourceLockKeys.Structure, Io, "io", 3);
        foreach (var ex in await ChiamaAsync(scrittura))
            Assert.IsNotType<EditConflictException>(ex);
    }

    /// <summary>L'elenco delle letture non nasconde scritture: nessuna chiede il lock.</summary>
    [Theory]
    [MemberData(nameof(Letture))]
    public async Task Le_letture_non_chiedono_il_lock(string lettura)
    {
        foreach (var ex in await ChiamaAsync(lettura))
            Assert.IsNotType<EditConflictException>(ex);
    }
}

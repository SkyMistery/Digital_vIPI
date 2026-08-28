using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Hosting;

/// <summary>
/// Chi impersonare in sviluppo (sezione <c>DevIdentity</c>). Serve a una cosa sola, ed è una cosa che
/// altrimenti non si può fare: <b>guidare l'app a un livello diverso da admin</b>.
///
/// <para>⚠️ Senza questo, verificare a schermo i cinque livelli richiedeva cinque ricompilazioni con una
/// costante cambiata a mano — cioè, in pratica, non si verificava. E il difetto che si cerca in una
/// funzione di permessi è proprio quello che si vede solo entrando come qualcun altro.</para>
///
/// <para>Vale <b>solo in Development</b>: fuori di lì l'adapter non è nemmeno registrato, e
/// <c>ProductionIdentityGuard</c> ferma l'avvio se qualcuno ce lo mettesse.</para>
/// </summary>
public sealed class DevIdentityOptions
{
    public const string SectionName = "DevIdentity";

    /// <summary>VID da impersonare. Default: chi ha costruito il sistema.</summary>
    public int UserId { get; set; } = 704798;

    /// <summary>
    /// Posizioni staff da usare <b>invece</b> di chiederle a IVAO. Vuoto = si chiedono all'API, che è il
    /// comportamento di sempre e verifica anche la pipeline dell'identità.
    /// </summary>
    public List<string> StaffPositions { get; set; } = new();
}

/// <summary>
/// Adapter di sviluppo per <see cref="ICurrentUserProvider"/>: simula "il login" con un UserId reale e ne
/// legge le posizioni staff **dal vivo** dall'API IVAO (così si verifica l'intera pipeline
/// identità → CurrentUser → UI). Memoizzato (una fetch sola). Con fallback statico se l'API non risponde.
/// In produzione è sostituito da <see cref="HostIdentityCurrentUserProvider"/> (claim host). ADR-0002.
/// </summary>
public sealed class DevCurrentUserProvider : ICurrentUserProvider
{
    private static CurrentUser? _cached;
    private static readonly object Lock = new();

    private readonly IUserDirectory _ivao;
    private readonly DevIdentityOptions _opt;
    private readonly ILogger<DevCurrentUserProvider> _log;

    public DevCurrentUserProvider(IUserDirectory ivao, IOptions<DevIdentityOptions> opt,
        ILogger<DevCurrentUserProvider> log)
    {
        _ivao = ivao;
        _opt = opt.Value;
        _log = log;
    }

    private int DevUserId => _opt.UserId;

    public CurrentUser? Get()
    {
        if (_cached is not null) return _cached;
        lock (Lock)
        {
            _cached ??= Build();
            return _cached;
        }
    }

    private CurrentUser Build()
    {
        // Posizioni scritte in config: si impersona esattamente quello, senza chiamare IVAO. È il modo in cui
        // si guida l'app a un livello che non è il proprio.
        if (_opt.StaffPositions is { Count: > 0 } scelte)
        {
            var codici = scelte.Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
            _log.LogInformation("DevCurrentUserProvider: identità da config, VID {UserId}, posizioni {Codici}.",
                DevUserId, string.Join(", ", codici));
            return new CurrentUser(DevUserId, $"VID {DevUserId}", "LIRR", codici) { CanEdit = codici.Length > 0 };
        }

        try
        {
            // Niente SynchronizationContext in ASP.NET Core: il blocking qui è sicuro e avviene una volta sola.
            var info = _ivao.GetUserAsync(DevUserId).GetAwaiter().GetResult();
            if (info is not null)
                return new CurrentUser(
                    UserId: info.UserId,
                    Name: info.Nickname ?? $"UserId {info.UserId}",
                    Acc: null,
                    StaffPositions: info.StaffPositionCodes)
                {
                    CanEdit = info.StaffPositionCodes.Count > 0,
                };
        }
        catch (Exception ex)
        {
            // Offline / credenziali assenti: si usa il fallback statico. NON ingoiare in silenzio (nascondeva anche
            // errori di programmazione, es. NRE nel fetcher): logga così la degradazione è diagnosticabile.
            _log.LogWarning(ex, "DevCurrentUserProvider: fetch utente {UserId} fallita, uso il fallback statico.", DevUserId);
        }

        return new CurrentUser(DevUserId, $"VID {DevUserId}", "LIRR",
            new[] { "IT-AOA1", "IT-T03" }) { CanEdit = true };
    }
}

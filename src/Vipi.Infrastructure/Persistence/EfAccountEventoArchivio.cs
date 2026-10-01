using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Application.EventKits;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La copia nel database di chi controlla con un account dell'evento (carta 2026-10-01-account-evento.md §4). ⚠️ Non
/// lancia: se il database non risponde la vista live va avanti dalla memoria, e si perde solo la sopravvivenza a un
/// riavvio — che non vale una pagina d'errore a chi sta controllando.
/// </summary>
public sealed class EfAccountEventoArchivio : IAccountEventoArchivio
{
    private readonly VipiDbContext _db;
    private readonly ILogger<EfAccountEventoArchivio> _log;

    public EfAccountEventoArchivio(VipiDbContext db, ILogger<EfAccountEventoArchivio> log)
    {
        _db = db;
        _log = log;
    }

    public Task SalvaAsync(int vidPersonale, int vidEvento, DateTime scadeUtc, CancellationToken ct = default) =>
        ProvaAsync("salvare", async () =>
        {
            var riga = await _db.AccountEventoInUso.FirstOrDefaultAsync(a => a.VidPersonale == vidPersonale, ct);
            if (riga is null) _db.AccountEventoInUso.Add(riga = new AccountEventoInUso { VidPersonale = vidPersonale });
            riga.VidEvento = vidEvento;
            riga.ScadeUtc = scadeUtc;
            await _db.SaveChangesAsync(ct);
        });

    public Task TogliAsync(int vidPersonale, CancellationToken ct = default) =>
        ProvaAsync("togliere", () => _db.AccountEventoInUso.Where(a => a.VidPersonale == vidPersonale).ExecuteDeleteAsync(ct));

    public Task SvuotaAsync(CancellationToken ct = default) =>
        ProvaAsync("svuotare", () => _db.AccountEventoInUso.ExecuteDeleteAsync(ct));

    public async Task<IReadOnlyList<AccountEventoSalvato>> TuttiAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.AccountEventoInUso.AsNoTracking()
                .Select(a => new AccountEventoSalvato(a.VidPersonale, a.VidEvento, a.ScadeUtc))
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Account dell'evento: lettura all'avvio non riuscita, si riparte da vuoto");
            return Array.Empty<AccountEventoSalvato>();
        }
    }

    private async Task ProvaAsync(string cosa, Func<Task> lavoro)
    {
        try { await lavoro(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ⚠️ Il contesto si pulisce: è lo stesso della pagina, e una riga rimasta appesa farebbe cadere il prossimo
            // salvataggio di qualcun altro.
            _db.ChangeTracker.Clear();
            _log.LogWarning(ex, "Account dell'evento: non sono riuscito a {Cosa} la copia nel database", cosa);
        }
    }
}

/// <summary>
/// All'avvio rimette in memoria chi stava controllando con un account dell'evento: un riavvio durante l'evento non deve
/// far riscrivere il VID a nessuno (committente, 1 ottobre 2026).
/// <para>⚠️ Prima di servire richieste (<c>StartAsync</c> si aspetta): è una lettura di poche righe, e partire senza
/// vorrebbe dire una finestra in cui la vista live di quelle persone è vuota.</para>
/// </summary>
public sealed class AccountEventoAvvio : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AccountEventoRegistro _registro;

    public AccountEventoAvvio(IServiceScopeFactory scopes, AccountEventoRegistro registro)
    {
        _scopes = scopes;
        _registro = registro;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var archivio = scope.ServiceProvider.GetRequiredService<IAccountEventoArchivio>();
        _registro.Carica(await archivio.TuttiAsync(ct), DateTime.UtcNow);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

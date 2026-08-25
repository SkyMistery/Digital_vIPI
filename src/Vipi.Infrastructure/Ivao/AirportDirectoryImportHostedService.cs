using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Assegna automaticamente alla loro ACC gli aeroporti nuovi della divisione (default ogni 24h), oltre al
/// bottone «Assegna aeroporti noti» di <c>/services/vsop/admin/airports</c>.
///
/// <para>⚠️ <b>È l'unico giro che CREA entità</b>: un aeroporto comparso nell'anagrafica della sorgente entra
/// nel sito da sé, e si tira dietro il suo catalogo settori. Fino al 22 agosto 2026 era di proposito un atto
/// di una persona; è diventato automatico per una richiesta esplicita — che in un giorno tutto sia
/// aggiornato, senza dipendere da chi si ricorda di premere. La pagina Sorgenti lo <b>dichiara</b> nella sua
/// riga, perché un giro che crea non deve essere una sorpresa.</para>
///
/// <para>L'assegnazione è <b>additiva</b>: <c>AutoAssignAirportsAsync</c> salta gli ICAO già in archivio e
/// non rimuove né riassegna niente. Un aeroporto tolto dall'anagrafica della sorgente resta dov'è, e va
/// tolto a mano — come deve essere, perché sopra ci può stare del lavoro editoriale.</para>
///
/// <para>Additiva sulle <b>entità</b>, però: dal 25 agosto 2026 lo stesso giro riallinea anche i campi
/// <b>anagrafici</b> degli aeroporti già dentro (presenza militare, IATA, quota, variazione magnetica) —
/// altrimenti un campo aggiunto al modello resterebbe al suo default su tutto l'archivio per sempre. Ciò che
/// decide una persona (nome, ACC di competenza, «solo militare») non viene toccato.</para>
///
/// <para>Job di sistema: nessuna authz utente, delega al core condiviso col manual
/// (<see cref="IAirportImportUseCase"/>; il guard admin lo mette
/// <c>StructureEditingService.AutoAssignKnownAirportsAsync</c>, che è l'altro chiamante).</para>
/// </summary>
public sealed class AirportDirectoryImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportDirectoryImportHostedService> _log;

    public AirportDirectoryImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportDirectoryImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    // Gated, e SUBITO dopo gli ACC (15s): un aeroporto si assegna a una ACC che deve già esserci, e i giri
    // che vengono dopo — SID 30s, settori 40s, TA/piste 50s — iterano gli aeroporti che questo ha creato.
    // Nell'ordine sbagliato uno scalo nuovo resterebbe senza settori e senza piste fino al giorno dopo.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.AirportDirectory,
            TimeSpan.FromHours(Math.Max(1, _opt.AirportDirectoryImportHours)), ImportOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(25));

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var import = sp.GetRequiredService<IAirportImportUseCase>();

        try
        {
            var r = await import.RunAsync(ct);
            foreach (var f in r.Failures)
                _log.LogWarning(f.Error, "Import settori dell'aeroporto {Icao} appena assegnato fallito; l'aeroporto resta senza catalogo fino al giro dei settori.", f.Icao);

            // Il conteggio si logga SEMPRE, anche a zero: «nessun aeroporto nuovo» è la risposta normale di
            // questo giro, e distinguerla da «non è girato» serve a chi legge i log dopo un'assenza.
            _log.LogInformation("Anagrafica aeroporti automatica: {Assigned} aeroporti assegnati, {Refreshed} aggiornati dalla sorgente, {Failed} senza catalogo settori.",
                r.Assigned, r.Refreshed, r.Failures.Count);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore (non un fallimento da ritentare a 1h).
            _log.LogInformation("Anagrafica aeroporti automatica saltata: {Reason}", ex.Message);
            return true;
        }
    }
}

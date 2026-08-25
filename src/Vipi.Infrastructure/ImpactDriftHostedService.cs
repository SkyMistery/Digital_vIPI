using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure;

/// <summary>
/// Il giro della <b>deriva</b>: una volta al giorno confronta ogni documento pubblicato con la sua copia in
/// vigore e apre (o richiude) le segnalazioni «da ripubblicare». Carta
/// <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c> §5-B.
///
/// <para><b>Perché passa da <see cref="GatedImportLoop"/> pur non essendo un import.</b> Di quel giro serve
/// tutto tranne il nome: il salto del primo giro se lo stato è fresco (un riavvio non rifà il lavoro), il
/// ritentativo breve in caso di guasto invece del periodo pieno, e la registrazione dell'ultimo esito — che
/// è l'unica cosa che permette di dire «questo controllo non gira da tre giorni» invece di credere al
/// silenzio. ⚠️ Non compare però nella pagina Sorgenti: quell'elenco parla di sorgenti esterne, e questo
/// giro non ne interroga nessuna. Si legge in Diagnostica.</para>
///
/// <para><b>bootDelay 100s</b>: dopo tutti gli import (l'ultimo, lo storico ATC, parte a 70s). Il senso del
/// giro è guardare il mondo <b>dopo</b> che gli import l'hanno aggiornato; partire prima vorrebbe dire
/// misurare la deriva di ieri.</para>
/// </summary>
public sealed class ImpactDriftHostedService : BackgroundService
{
    /// <summary>Ogni quanto. Un giorno: la deriva la producono gli import, che girano una volta al giorno —
    /// guardare più spesso costerebbe senza poter trovare niente di nuovo.</summary>
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ImpactDriftHostedService> _log;

    public ImpactDriftHostedService(IServiceScopeFactory scopes, ILogger<ImpactDriftHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.ImpactDrift,
            Periodo,
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(100));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var esito = await sp.GetRequiredService<IImpactDriftUseCase>().RunAsync(ct);

        _log.LogInformation(
            "Deriva delle pubblicazioni: {Esaminati} documenti esaminati, {Aperti} segnalazioni aperte, " +
            "{Chiusi} richiuse da sé, {Potati} righe chiuse potate.",
            esito.Esaminati, esito.Aperti, esito.Chiusi, esito.Potati);

        return true;
    }
}

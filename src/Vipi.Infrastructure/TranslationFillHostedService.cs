using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Infrastructure.Ivao;

namespace Vipi.Infrastructure;

/// <summary>
/// Il giro che riempie la memoria di traduzione (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §6).
///
/// <para>
/// ⚠️ <b>Non si traduce al salvataggio</b>, e non è una scelta di comodità: il testo italiano <i>è</i> il
/// documento, la traduzione è un servizio. Legare il salvataggio a una chiamata di rete vorrebbe dire che
/// un disservizio di Azure impedisce a un controllore di salvare il suo lavoro. Chi scrive salva; questo
/// giro raccoglie ciò che manca.
/// </para>
///
/// <para>
/// <b>Perché ogni quarto d'ora e non ogni giorno.</b> Gli altri giri interrogano sorgenti esterne e girano
/// una volta al giorno perché il mondo non cambia più in fretta. Qui il tempo che conta è un altro: quanto
/// aspetta un lettore prima di vedere in inglese la frase che qualcuno ha appena scritto. Un giorno sarebbe
/// una funzione che sembra rotta.
/// <br/>
/// E costa quasi niente quando non c'è niente da fare: «cosa manca» è la differenza fra i segmenti del
/// corpus e le impronte in memoria — misurato, 499 campi per 23.344 caratteri — e <b>se non manca niente la
/// rete non si tocca affatto</b>. C'è un test che lo pretende.
/// </para>
///
/// <para>
/// ⚠️ <b>Le direzioni sono due, non una.</b> La vLOA nasce in inglese e per lei l'italiano è il bersaglio.
/// Girare solo it→en lascerebbe metà del corpus senza traduzione, ed è proprio la metà che oggi esiste.
/// </para>
///
/// <para>
/// <b>bootDelay 120s</b>: dopo tutti gli import e dopo la deriva. Tradurre prima vorrebbe dire tradurre il
/// corpus di ieri, e pagare di nuovo domani per le frasi arrivate nel frattempo.
/// </para>
/// </summary>
public sealed class TranslationFillHostedService : BackgroundService
{
    /// <summary>Vedi il commento di classe: qui il metro è l'attesa del lettore, non il ritmo di una sorgente.</summary>
    private static readonly TimeSpan Periodo = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TranslationFillHostedService> _log;

    public TranslationFillHostedService(IServiceScopeFactory scopes, ILogger<TranslationFillHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.Translation,
            Periodo,
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(120));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var opzioni = sp.GetRequiredService<IOptions<TranslationOptions>>().Value;
        if (!opzioni.Enabled)
        {
            _log.LogDebug("Traduzione spenta (Translation:Enabled=false): giro saltato.");
            return true;   // non è un guasto: è un sito che non traduce
        }

        var protettore = await ProtettoreColRosterAsync(sp, ct).ConfigureAwait(false);
        var giro = new TranslationFillUseCase(
            sp.GetRequiredService<ITranslatableCorpus>(),
            sp.GetRequiredService<ITranslationMemory>(),
            sp.GetServices<ITranslationEngine>(),
            protettore,
            opzioni);

        var tutteRiuscite = true;

        // Ogni lingua verso ogni altra: it→en per le vIPI, en→it per le vLOA.
        foreach (var sorgente in opzioni.Targets)
            foreach (var bersaglio in opzioni.Targets)
            {
                if (string.Equals(sorgente, bersaglio, StringComparison.OrdinalIgnoreCase)) continue;
                ct.ThrowIfCancellationRequested();

                var esito = await giro.EseguiAsync(sorgente, bersaglio, ct).ConfigureAwait(false);

                if (esito.Esito == TranslationOutcome.Ok)
                {
                    // Si registra solo quando c'è qualcosa da dire: un giro che non ha trovato niente da
                    // fare è il caso normale, e riempirne il registro nasconderebbe quelli che contano.
                    if (esito.Tradotti > 0 || esito.DaTradurreAMano > 0 || esito.Scartati > 0)
                        _log.LogInformation(
                            "Traduzione {Da}→{A} ({Motore}): {Tradotti} nuove, {Cache} già in memoria, " +
                            "{AMano} da tradurre a mano, {Scartati} scartate perché il motore ha cambiato un identificatore.",
                            sorgente, bersaglio, esito.Motore, esito.Tradotti, esito.GiaInMemoria,
                            esito.DaTradurreAMano, esito.Scartati);
                    continue;
                }

                // ⚠️ Il livello dipende dall'azione, non dalla gravità sentimentale: quota finita e chiave
                // rifiutata vogliono una PERSONA, un guasto passeggero no — segnalarli allo stesso modo
                // vuol dire che presto nessuno legge più nessuno dei due.
                var livello = esito.Esito is TranslationOutcome.TemporaryFailure
                    ? LogLevel.Information
                    : LogLevel.Warning;

                _log.Log(livello,
                    "Traduzione {Da}→{A} non riuscita: {Esito}. {Dettaglio}",
                    sorgente, bersaglio, esito.Esito, esito.Dettaglio);

                // Un guasto passeggero non deve far ritentare l'intero giro fra poco: al prossimo periodo
                // si riprova, e nel frattempo il documento si legge nella lingua sorgente.
                if (esito.Esito != TranslationOutcome.TemporaryFailure) tutteRiuscite = false;
            }

        return tutteRiuscite;
    }

    /// <summary>
    /// Il protettore con dentro i nomi dello staff. ⚠️ Vanno letti a <b>ogni</b> giro e non una volta
    /// all'avvio: il roster cresce a ogni login nuovo, e un protettore costruito ieri non conosce lo
    /// staffista arrivato stamattina — cioè lascerebbe uscire proprio il nome più recente.
    /// </summary>
    private static async Task<TextProtector> ProtettoreColRosterAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<Persistence.VipiDbContext>();
        var nomi = await db.StaffMembers.AsNoTracking()
            .Where(s => s.DisplayName != null)
            .Select(s => s.DisplayName!)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return new TextProtector(nomi);
    }
}

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

    /// <summary>Quanto di un segmento rotto si scrive nel registro: quel che basta a riconoscerlo.</summary>
    private const int TagliaSegmento = 120;

    /// <summary>I segmenti rotti su una riga sola, tagliati e separati. Vuoto = «(nessuno)», mai una riga muta.</summary>
    private static string Elenca(IReadOnlyList<string>? rotti)
    {
        if (rotti is null || rotti.Count == 0) return "(non registrati)";
        return string.Join(" · ", rotti.Select(r =>
            r.Length <= TagliaSegmento ? r : r[..TagliaSegmento] + "…"));
    }

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TranslationFillHostedService> _log;

    /// <summary>
    /// Se l'avviso «acceso ma senza chiave» è già stato dato. ⚠️ Serve perché il giro è ogni quarto d'ora e
    /// l'avviso è di quelli che non cambiano da soli: senza questo campo sarebbero <b>96 righe di Warning
    /// al giorno per sempre</b>, cioè il modo sicuro di far smettere di leggere i Warning.
    /// </summary>
    private bool _avvisoSenzaMotoreDato;

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

        // ⚠️ ACCESO SENZA CHIAVE si dice UNA volta e in modo esplicito, non due volte a giro per sempre.
        // Senza questa uscita anticipata il giro arriva in fondo alla catena, non trova nessun motore
        // configurato e riporta `NotConfigured` per ogni direzione: due Warning ogni quarto d'ora, che
        // dicono «non riuscita» quando la verità è «non è mai stata chiesta a nessuno». Il rimedio è una
        // riga di configurazione, non un guasto da ritentare — e un avviso che si ripete senza cambiare è
        // un avviso che fra due giorni nessuno legge più.
        var motori = sp.GetServices<ITranslationEngine>().ToList();
        if (!motori.Any(m => m.IsConfigured))
        {
            if (!_avvisoSenzaMotoreDato)
            {
                _avvisoSenzaMotoreDato = true;
                _log.LogWarning(
                    "Translation:Enabled è true ma nessun motore ha una chiave ({Motori}): non si traduce " +
                    "niente e i documenti restano nella lingua sorgente. La chiave va sotto " +
                    "Translation:Azure:ApiKey (con Translation:Azure:Region) o Translation:DeepL:ApiKey — " +
                    "in produzione nel file della cartella «segreti». Questo avviso non si ripete.",
                    string.Join(", ", motori.Select(m => m.Name)));
            }

            // Vero e non falso: la configurazione manca, non è il giro ad essere fallito. Segnare un
            // fallimento farebbe ritentare a raffica una cosa che nessun ritentativo può risolvere.
            return true;
        }

        // ⚠️ PRIMA di chiedere alla macchina: i titoli che hanno un originale ufficiale si mettono in
        // memoria a mano, o il giro li traduce e paga per una risposta sbagliata («Piste» → «Slopes», visto
        // dal vivo il 28 agosto 2026). Idempotente: dal secondo giro non scrive niente.
        var memoria = sp.GetRequiredService<ITranslationMemory>();

        // ⚠️ E prima ancora: la fotografia di quel che era già stato speso quando il registro non c'era.
        // Senza, il primo giro dopo la migrazione troverebbe il contatore a ZERO e crederebbe di avere tutta
        // la franchigia davanti — e per DeepL la franchigia non si rinnova. Idempotente, e la domanda «l'ho
        // già fatta?» la fa al database, non a un campo: il processo si riavvia.
        var fotografie = await memoria
            .FotografaSpesaPregressaAsync(motori.Select(m => m.Name).ToList(), DateTime.UtcNow, ct)
            .ConfigureAwait(false);
        if (fotografie > 0)
            _log.LogInformation(
                "Registro della spesa: scritta la fotografia iniziale per {Quanti} motori. Da qui in poi la " +
                "spesa si conta, non si deduce.", fotografie);
        var seminati = await TitoliUfficiali.SeminaAsync(memoria, ct).ConfigureAwait(false);
        if (seminati > 0)
            _log.LogInformation("Titoli ufficiali messi in memoria: {Quanti}.", seminati);

        // ⚠️ E il glossario di fraseologia, per la stessa ragione un gradino più in là: là si compra una
        // traduzione sbagliata di un titolo, qui una frase che SEMBRA giusta («bring it back downwind») e che
        // nessuno correggerà mai leggendo. Semina solo se il glossario è ancora vuoto: da quando lo tocca una
        // persona, questo codice non ci scrive più (lavori-aperti §Q3).
        // ⚠️ E le frasi di partenza delle vLOA, per la stessa ragione dei titoli: sono parola nostra, e una
        // di loro tornava rotta a ogni giro — 155 caratteri buttati ogni quarto d'ora (lavori aperti §Q16b).
        var semiVloa = await FrasiVloa
            .SeminaAsync(memoria, sp.GetRequiredService<INeighbourRepository>(), ct).ConfigureAwait(false);
        if (semiVloa > 0)
            _log.LogInformation("Frasi di partenza delle vLOA messe in memoria: {Quante}.", semiVloa);

        var glossarioStore = sp.GetRequiredService<IGlossaryStore>();
        var semiGlossario = await GlossarioFraseologia
            .SeminaAsync(glossarioStore, ct: ct).ConfigureAwait(false);
        if (semiGlossario > 0)
            _log.LogInformation("Glossario di fraseologia, voci di partenza scritte: {Quanti}.", semiGlossario);

        // ⚠️ I nomi si leggono UNA volta per giro; il glossario, invece, una per COPPIA DI LINGUE — vedi il
        // ciclo qui sotto. Sono due cose diverse: un nome di persona non si traduce in nessun verso, una
        // formula sì e in un verso solo.
        var nomi = await NomiDelloStaffAsync(sp, ct).ConfigureAwait(false);

        var tutteRiuscite = true;

        // Ogni lingua verso ogni altra: it→en per le vIPI, en→it per le vLOA.
        foreach (var sorgente in opzioni.Targets)
            foreach (var bersaglio in opzioni.Targets)
            {
                if (string.Equals(sorgente, bersaglio, StringComparison.OrdinalIgnoreCase)) continue;
                ct.ThrowIfCancellationRequested();

                // ⚠️ Un protettore per VERSO, e non uno solo per tutto il giro: il glossario è direzionale.
                // Con un protettore solo, le formule it→en verrebbero cercate anche nel testo inglese delle
                // vLOA — dove non compaiono mai, quindi nessun danno — ma soprattutto il verso en→it
                // resterebbe senza glossario per sempre, e non lo direbbe nessun errore.
                var glossario = await GlossarioFraseologia
                    .CaricaAsync(glossarioStore, sorgente, bersaglio, ct).ConfigureAwait(false);

                var giro = new TranslationFillUseCase(
                    sp.GetRequiredService<ITranslatableCorpus>(),
                    memoria,
                    motori,
                    new TextProtector(nomi, glossario),
                    opzioni);

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

                    // ⚠️ Le scartate si RIPAGANO a ogni giro: non finiscono in memoria, quindi il conto della
                    // spesa non le vede e il giro dopo le rispedisce — ogni quarto d'ora, per sempre. Finché
                    // sono zero non c'è niente da dire; quando non lo sono, questa riga è l'unico posto in cui
                    // la perdita si vede. È un Warning perché vuole una persona: vedi lavori-aperti §Q16.
                    if (esito.CaratteriScartati > 0)
                        // ⚠️ E si dice QUALI, per esteso. «1 segmento tornato rotto» non si puo' cercare: il
                        // corpus ne ha decine, e per trovare quello giusto bisognava interrogare il database a
                        // mano — fatto il 30 agosto 2026, e il colpevole era una frase NOSTRA che ora si semina
                        // (FrasiVloa). Il testo si taglia a 120 caratteri: serve a riconoscerlo, non a rileggerlo.
                        _log.LogWarning(
                            "Traduzione {Da}→{A} ({Motore}): {Caratteri} caratteri spesi per {Scartati} segmenti " +
                            "tornati rotti. Non entrano nel conto della spesa e il prossimo giro li rispedisce. " +
                            "Sono: {Rotti}",
                            sorgente, bersaglio, esito.Motore, esito.CaratteriScartati, esito.Scartati,
                            Elenca(esito.Rotti));
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
    /// I nomi dello staff, per il protettore. ⚠️ Vanno letti a <b>ogni</b> giro e non una volta all'avvio: il
    /// roster cresce a ogni login nuovo, e un protettore costruito ieri non conosce lo staffista arrivato
    /// stamattina — cioè lascerebbe uscire proprio il nome più recente.
    /// </summary>
    private static async Task<List<string>> NomiDelloStaffAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<Persistence.VipiDbContext>();
        return await db.StaffMembers.AsNoTracking()
            .Where(s => s.DisplayName != null)
            .Select(s => s.DisplayName!)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}

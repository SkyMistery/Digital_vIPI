using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Translation;

/// <inheritdoc cref="ITraduciOra"/>
public sealed class TraduciOraService : ITraduciOra
{
    private readonly IEditAuthorizationService _authz;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRegistroDeiGiri _registro;
    private readonly TranslationOptions _opzioni;
    private readonly ILogger<TraduciOraService> _log;

    /// <param name="authz">⚠️ Viene dallo scope della <b>richiesta</b>, ed è l'unico servizio qui dentro che
    /// deve venire da lì: risolve l'identità dall'<c>HttpContext</c>, e in uno scope creato dopo la richiesta
    /// risponderebbe «anonimo» — cioè rifiuterebbe il permesso a tutti. È la stessa regola già scritta per i
    /// sette componenti isolati dell'editor.</param>
    /// <param name="scopes">Per il lavoro pesante: un <c>DbContext</c> tutto suo.</param>
    public TraduciOraService(IEditAuthorizationService authz, IServiceScopeFactory scopes,
        IRegistroDeiGiri registro, IOptions<TranslationOptions> opzioni, ILogger<TraduciOraService> log)
    {
        _authz = authz;
        _scopes = scopes;
        _registro = registro;
        _opzioni = opzioni.Value;
        _log = log;
    }

    public async Task<RispostaTraduciOra> EseguiAsync(int documentId, CancellationToken ct = default)
    {
        // Il cancello, prima di tutto: chi non può scrivere il documento non può nemmeno spendere per farlo
        // tradurre.
        _authz.EnsureAtLeast(VipiRole.Editor);
        var attore = _authz.CurrentUserId ?? 0;

        if (!_opzioni.Enabled) return new RispostaTraduciOra(EsitoDellaPressione.Spenta);

        // 🔴 UNO SCOPE TUTTO SUO, e non è una raffinatezza: questa chiamata dura quanto una risposta di rete
        // (secondi), e nel frattempo l'editor che l'ha lanciata continua a disegnarsi e a leggere. Sul
        // `DbContext` del circuito sarebbe la settima corsa «A second operation was started on this context»
        // — quella con dentro una chiamata a un servizio esterno, cioè la più larga di tutte.
        using var scope = _scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var motori = sp.GetServices<ITranslationEngine>().ToList();
        if (!motori.Any(m => m.IsConfigured))
            return new RispostaTraduciOra(EsitoDellaPressione.SenzaMotore);

        var stato = sp.GetRequiredService<IStatoTraduzione>();
        var mancanti = await stato.MancantiAsync(documentId, ct).ConfigureAwait(false);
        if (mancanti is null) return new RispostaTraduciOra(EsitoDellaPressione.NienteDaFare);
        if (mancanti.Bloccata) return new RispostaTraduciOra(EsitoDellaPressione.Bloccata);
        if (mancanti.Mancanti.Count == 0)
            return new RispostaTraduciOra(EsitoDellaPressione.NienteDaFare);

        // 🔴 Il lucchetto, condiviso col giro automatico: insieme spedirebbero gli STESSI segmenti — la
        // memoria si legge all'inizio e si scrive alla fine — e quei caratteri si pagherebbero due volte.
        using var lucchetto = _registro.ProvaAEntrare();
        if (lucchetto is null) return new RispostaTraduciOra(EsitoDellaPressione.GiroInCorso);

        var db = sp.GetRequiredService<VipiDbContext>();
        var memoria = sp.GetRequiredService<ITranslationMemory>();
        var nomi = await ArnesiDelGiro.NomiDelloStaffAsync(db, ct).ConfigureAwait(false);
        var protettore = await ArnesiDelGiro
            .ProtettoreAsync(sp.GetRequiredService<IGlossaryStore>(), nomi, mancanti.Da, mancanti.A, ct)
            .ConfigureAwait(false);

        var giro = new TranslationFillUseCase(
            sp.GetRequiredService<ITranslatableCorpus>(), memoria, motori, protettore, _opzioni);

        // ⚠️ Si passano TUTTI i segmenti del documento e non i soli mancanti: il confronto con la memoria lo
        // fa il giro, ed è lo stesso confronto di ogni quarto d'ora. Due modi di decidere «che cosa manca»
        // divergerebbero al primo cambio di normalizzazione.
        var rapporto = await giro
            .EseguiSuAsync(mancanti.Segmenti, mancanti.Da, mancanti.A, TranslationSpendKind.ManualDispatch, ct)
            .ConfigureAwait(false);

        _registro.Registra(new EsitoDelGiro(DateTime.UtcNow, mancanti.Da, mancanti.A, Manuale: true, rapporto));

        // Chi ha premuto resta scritto. ⚠️ Nessuna tabella nuova: il registro di audit c'è già ed è generico,
        // e la spesa porta il suo `ManualDispatch`. Davanti a una spesa che cresce, «chi» è la prima domanda.
        AuditScribe.Write(db, attore, AuditAction.Update, "TranslationRun", documentId.ToString(), new
        {
            rapporto.Tradotti,
            rapporto.Scartati,
            rapporto.DaTradurreAMano,
            rapporto.Motore,
            Verso = $"{mancanti.Da}→{mancanti.A}",
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (rapporto.Esito != TranslationOutcome.Ok)
        {
            _log.LogWarning(
                "«Traduci ora» sul documento {Documento} ({Da}→{A}) non riuscito: {Esito}. {Dettaglio}",
                documentId, mancanti.Da, mancanti.A, rapporto.Esito, rapporto.Dettaglio);

            return new RispostaTraduciOra(
                rapporto.Esito == TranslationOutcome.QuotaExceeded
                    ? EsitoDellaPressione.TettoFinito
                    : EsitoDellaPressione.MotoreGiu,
                Mancavano: mancanti.Mancanti.Count,
                Dettaglio: rapporto.Dettaglio);
        }

        _log.LogInformation(
            "«Traduci ora» sul documento {Documento} ({Da}→{A}, {Motore}): {Tradotti} nuove, {Scartati} rotte, " +
            "chiesto da {Attore}.",
            documentId, mancanti.Da, mancanti.A, rapporto.Motore, rapporto.Tradotti, rapporto.Scartati, attore);

        return new RispostaTraduciOra(
            EsitoDellaPressione.Fatto,
            rapporto.Tradotti,
            mancanti.Mancanti.Count,
            rapporto.Scartati,
            rapporto.DaTradurreAMano,
            rapporto.Motore,
            rapporto.Rotti,
            rapporto.Dettaglio);
    }
}

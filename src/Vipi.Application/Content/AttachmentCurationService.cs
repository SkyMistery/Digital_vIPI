using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Che cosa è successo sostituendo un file, e su quali documenti se n'è preso nota.</summary>
/// <param name="Impattati">
/// I documenti che citano la voce, letti <b>prima</b> della scrittura. Sono gli stessi che la conferma ha
/// mostrato a chi ha premuto: se la conferma dicesse una cosa e la segnalazione ne toccasse un'altra, la
/// schermata che esiste per far decidere starebbe mentendo.
/// </param>
public sealed record AttachmentReplacementOutcome(
    AttachmentReplace Esito, AttachmentRow? Riga, IReadOnlyList<AttachmentCitation> Impattati);

/// <summary>
/// Gli atti che si compiono <b>su una voce di biblioteca già citata</b>: sostituirne il file, o toglierla.
///
/// <para><b>Perché è un servizio a parte e non due metodi della biblioteca.</b> Scrivere una versione o
/// cancellare una riga sono scritture sole; accorgersi che <i>tre documenti pubblicati</i> cambiano sotto i
/// piedi a qualcuno è un'altra cosa, e richiede di sapere chi cita cosa — una scansione che il redirect e
/// l'elenco non devono pagare. Tenerle insieme vorrebbe dire che la porta più calda del sistema
/// (<c>/vsop/files/{slug}</c>, chiamata a ogni clic) si porta dietro la lettura più cara.</para>
///
/// <para>⚠️ Si chiamava <c>IAttachmentReplacement</c> finché faceva una cosa sola. Un nome che descrive
/// metà di quel che c'è dentro mente a chi legge fra sei mesi, e rinominarlo adesso — prima che qualcuno lo
/// citi — costa una riga.</para>
/// </summary>
public interface IAttachmentCuration
{
    /// <summary>
    /// Chi cambia se si sostituisce questa voce. È la <b>conferma informata</b>: si chiede <i>prima</i>, e
    /// quel che risponde è l'elenco che la schermata mostra.
    /// </summary>
    Task<IReadOnlyList<AttachmentCitation>> ImpactPreviewAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Sostituisce, e apre una riga «da rivedere» su ogni documento che cita la voce.
    ///
    /// <para>⚠️ <b>Le righe si aprono anche se non c'è niente di rotto</b>, ed è il punto: il link segue
    /// sempre la versione corrente, quindi un documento <b>già pubblicato</b> mostra il file nuovo senza che
    /// nessuno l'abbia toccato. Chi lo cura deve saperlo, non scoprirlo — e la rilettura può benissimo
    /// concludersi con «va bene così», che è perché la riga si chiude a mano.</para>
    /// </summary>
    Task<AttachmentReplacementOutcome> ReplaceAsync(
        string slug, string link, string? note, int userId, CancellationToken ct = default);

    /// <summary>
    /// Elimina la voce, e apre una riga «da rivedere» su ogni documento che la citava.
    ///
    /// <para>⚠️ <b>Non si rifiuta</b> quando la voce è citata, e la scelta è deliberata: rifiutare avrebbe
    /// senso se ci fosse un modo automatico di rimediare, e non c'è — le citazioni stanno dentro testo
    /// scritto da persone. Chi decide vede <b>quali</b> documenti restano col link morto e conferma; la
    /// segnalazione fa in modo che quei documenti non restino così per mesi.</para>
    ///
    /// <para>⚠️ E il file sul deposito <b>resta dov'è</b>: i byte non sono nostri.</para>
    /// </summary>
    Task<AttachmentDeletionOutcome> DeleteAsync(string slug, int userId, CancellationToken ct = default);
}

/// <summary>Che cosa è successo eliminando una voce, e quali documenti restano col link morto.</summary>
public sealed record AttachmentDeletionOutcome(
    AttachmentDelete Esito, IReadOnlyList<AttachmentCitation> Orfani);

/// <inheritdoc cref="IAttachmentCuration"/>
public sealed class AttachmentCurationService : IAttachmentCuration
{
    private readonly IAttachmentLibrary _biblioteca;
    private readonly IAttachmentUsage _uso;
    private readonly IDocumentImpactService _impatti;

    public AttachmentCurationService(IAttachmentLibrary biblioteca, IAttachmentUsage uso,
        IDocumentImpactService impatti)
    {
        _biblioteca = biblioteca;
        _uso = uso;
        _impatti = impatti;
    }

    public Task<IReadOnlyList<AttachmentCitation>> ImpactPreviewAsync(string slug, CancellationToken ct = default) =>
        _uso.WhereUsedAsync(slug, ct);

    public async Task<AttachmentReplacementOutcome> ReplaceAsync(
        string slug, string link, string? note, int userId, CancellationToken ct = default)
    {
        // ⚠️ Chi cita si legge PRIMA di scrivere, e non è indifferente: la scrittura non cambia le citazioni,
        // ma leggerle dopo vorrebbe dire che un salvataggio contemporaneo in un'altra scheda cambia l'elenco
        // fra la conferma e la segnalazione — e chi ha premuto avrebbe deciso su un elenco diverso.
        var citazioni = await _uso.WhereUsedAsync(slug, ct);

        var (esito, riga) = await _biblioteca.ReplaceAsync(slug, link, note, userId, ct);
        if (esito != AttachmentReplace.Ok)
            return new AttachmentReplacementOutcome(esito, riga, citazioni);

        var documenti = DocumentiDi(citazioni);
        if (documenti.Count > 0)
        {
            // La chiave d'origine è lo SLUG: due sostituzioni della stessa voce non fanno due righe su uno
            // stesso documento — la deduplicazione della casella lavora su (documento, tipo, origine).
            // L'argomento della frase è il titolo, che è quel che chi legge riconosce.
            await _impatti.RaiseForDocumentsAsync(
                ImpactKind.AttachmentReplaced, documenti, slug,
                new[] { riga?.Title ?? slug }, ct);
        }

        return new AttachmentReplacementOutcome(esito, riga, citazioni);
    }

    public async Task<AttachmentDeletionOutcome> DeleteAsync(string slug, int userId, CancellationToken ct = default)
    {
        // Come per la sostituzione: chi cita si legge PRIMA. Qui però è anche l'unico momento in cui si può
        // leggere — dopo la cancellazione la voce non c'è più, e con lei sparirebbe la ragione per cui quei
        // documenti vanno riaperti.
        var citazioni = await _uso.WhereUsedAsync(slug, ct);

        var esito = await _biblioteca.DeleteAsync(slug, userId, ct);
        if (esito != AttachmentDelete.Ok) return new AttachmentDeletionOutcome(esito, citazioni);

        var documenti = DocumentiDi(citazioni);
        if (documenti.Count > 0)
            await _impatti.RaiseForDocumentsAsync(
                ImpactKind.AttachmentDeleted, documenti, slug, new[] { slug }, ct);

        return new AttachmentDeletionOutcome(esito, citazioni);
    }

    /// <summary>I documenti citanti, senza doppioni: uno che cita in dieci punti è un documento solo.</summary>
    private static List<int> DocumentiDi(IReadOnlyList<AttachmentCitation> citazioni) =>
        citazioni
            .Select(c => c.DocumentId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
}

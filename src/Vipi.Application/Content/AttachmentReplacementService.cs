using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Che cosa è successo sostituendo un file, e su quanti documenti se n'è preso nota.</summary>
/// <param name="Impattati">
/// I documenti che citano la voce, letti <b>prima</b> della scrittura. Sono gli stessi che la conferma ha
/// mostrato a chi ha premuto: se la conferma dicesse una cosa e la segnalazione ne toccasse un'altra, la
/// schermata che esiste per far decidere starebbe mentendo.
/// </param>
public sealed record AttachmentReplacementOutcome(
    AttachmentReplace Esito, AttachmentRow? Riga, IReadOnlyList<AttachmentCitation> Impattati);

/// <summary>
/// La sostituzione di un allegato, con quel che ne consegue.
///
/// <para><b>Perché è un servizio a parte e non un metodo della biblioteca.</b> Sostituire un file è una
/// scrittura sola; accorgersi che <i>tre documenti pubblicati</i> ora mostrano un altro PDF è un'altra cosa,
/// e richiede di sapere chi cita cosa — una scansione che il redirect e l'elenco non devono pagare. Tenerle
/// insieme vorrebbe dire che la porta più calda del sistema si porta dietro la lettura più cara.</para>
/// </summary>
public interface IAttachmentReplacement
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
}

/// <inheritdoc cref="IAttachmentReplacement"/>
public sealed class AttachmentReplacementService : IAttachmentReplacement
{
    private readonly IAttachmentLibrary _biblioteca;
    private readonly IAttachmentUsage _uso;
    private readonly IDocumentImpactService _impatti;

    public AttachmentReplacementService(IAttachmentLibrary biblioteca, IAttachmentUsage uso,
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

        var documenti = citazioni
            .Select(c => c.DocumentId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

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
}

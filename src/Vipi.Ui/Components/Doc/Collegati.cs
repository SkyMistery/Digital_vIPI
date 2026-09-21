using Microsoft.Extensions.Logging;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// I documenti collegati (§A109) per le cinque pagine: il caricamento e l'indirizzo di ogni link. La regola di CHI
/// collegare sta in <see cref="DocumentiCollegati"/>; il disegno in <c>DocumentiCollegatiCard</c>.
/// </summary>
public static class Collegati
{
    /// <summary>
    /// I link del documento aperto, nel modo in cui lo si sta guardando.
    /// <para>⚠️ Un link in più non vale una pagina che non si apre: un guasto qui toglie il BLOCCO, non il
    /// documento — e lo si scrive nel log, perché un blocco sparito non lo segnala nessuno.</para>
    /// </summary>
    public static async Task<IReadOnlyList<ResolvedDocLink>> CaricaAsync(IDocLinkService servizio, ILogger log,
        ReleaseTargetType tipo, string chiave, PreviewMode modo)
    {
        try
        {
            return await servizio.ForPageAsync(tipo, chiave,
                modo.Kind == PreviewKind.Release ? modo.ReleaseId : null,
                bozza: modo.Kind == PreviewKind.Draft);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Documenti collegati di {Tipo}/{Chiave}: blocco omesso", tipo, chiave);
            return Array.Empty<ResolvedDocLink>();
        }
    }

    /// <summary>
    /// L'indirizzo di un link. Un bersaglio che sta nella STESSA pagina unita diventa un salto alla sua sezione,
    /// invece di un link che ricarica la stessa cosa da un'altra porta.
    /// </summary>
    public static string Href(ResolvedDocLink l, IReadOnlyList<MembroUnito> unione) =>
        unione.Any(m => m.Membro.DocumentId == l.Target.DocumentId)
            ? "#" + MembroUnito.AncoraDi(l.Target.DocumentId)
            : l.Href;
}

using Microsoft.Extensions.Logging;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// I documenti collegati (§A109) come gruppi del riquadro di sinistra. Una sola forma per le cinque pagine: la
/// regola di CHI collegare sta in <see cref="DocumentiCollegati"/>, qui c'è solo come si dispone a schermo.
/// </summary>
public static class TocCollegati
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
    /// I gruppi da dare a <c>DocumentToc.Collegati</c>.
    /// </summary>
    /// <param name="perAcc">vIPI ACC: due gruppi intestati e CHIUSI, «APP» e «Aeroporti» (possono essere lunghi
    /// quanto il sommario). Gli altri documenti: un elenco solo, nell'ordine ACC → APP → aeroporti.</param>
    /// <param name="unione">Gli altri membri della pagina unita: un bersaglio che sta QUI diventa un'ancora alla sua
    /// sezione, invece di un link che ricarica la stessa cosa da un'altra porta.</param>
    public static IReadOnlyList<TocGruppo> Gruppi(IReadOnlyList<ResolvedDocLink> link, bool perAcc,
        IReadOnlyList<MembroUnito> unione, StringheDelSito sito)
    {
        if (link.Count == 0) return Array.Empty<TocGruppo>();

        var qui = unione.Select(m => m.Membro.DocumentId).ToHashSet();
        TocVoce Voce(ResolvedDocLink l) => TocVoce.Collegamento(l.Target.Label,
            qui.Contains(l.Target.DocumentId) ? "#" + MembroUnito.AncoraDi(l.Target.DocumentId) : l.Href);

        if (!perAcc) return new[] { new TocGruppo(null, link.Select(Voce).ToList()) };

        return new[]
        {
            new TocGruppo("APP", link.Where(l => l.Group == DocLinkGroup.App).Select(Voce).ToList()) { Chiuso = true },
            new TocGruppo(sito["AccLanding_Airports"], link.Where(l => l.Group == DocLinkGroup.Airport).Select(Voce).ToList()) { Chiuso = true },
        };
    }
}

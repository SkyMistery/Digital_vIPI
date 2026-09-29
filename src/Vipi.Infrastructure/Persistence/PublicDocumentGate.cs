using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Gate di visibilità pubblica condiviso da ricerca e «Cosa è cambiato» (doc 13 §3f). È la stessa regola delle
/// pagine — <b>documento non nascosto</b> e <b>release AIRAC effettiva</b> (doc 10 §3f/§S6b) — più l'esclusione
/// delle <b>sezioni nascoste</b> (doc 11 §3c).
/// <para>
/// Serviva perché i due indici partivano da <c>CurrentVersionId != null</c> e basta: uscivano documenti nascosti
/// dall'admin, sezioni marcate «nascosta» col loro estratto, e contenuto di versioni pubblicate che la pagina non
/// serve perché senza release. Un indice non è un posto meno pubblico della pagina.
/// </para>
/// </summary>
internal static class PublicDocumentGate
{
    /// <summary>Sottoinsieme visibile al pubblico: scarta i nascosti e quelli senza release effettiva, in una
    /// sola query batch sui bersagli (come fa l'elenco unificato).</summary>
    public static async Task<IReadOnlyList<T>> VisibleAsync<T>(
        IReadOnlyList<T> items, Func<T, Document> doc, Func<T, ManagedDoc> managed,
        IReleaseRepository releases, CancellationToken ct)
    {
        var candidates = items.Where(i => !doc(i).IsHidden && PaginaAperta(doc(i), managed(i))).ToList();
        if (candidates.Count == 0) return Array.Empty<T>();

        var targets = candidates.Select(i => (managed(i).ReleaseTarget, managed(i).ReleaseKey)).Distinct().ToList();
        var summaries = await releases.SummariesAsync(targets, ct);

        return candidates
            .Where(i => summaries.TryGetValue((managed(i).ReleaseTarget, managed(i).ReleaseKey), out var s)
                        && s.EffectiveCycle is not null)
            .ToList();
    }

    /// <summary>
    /// La pagina pubblica del documento è aperta? Le stesse condizioni di <c>EfContentRepository</c> oltre al
    /// documento nascosto: lo <b>scalo</b> nascosto chiude la vIPI civile e il vSOP militare, l'<b>APP</b> che
    /// la proiezione ha disattivato (nascosto dall'admin o sparito dalla sorgente) chiude la sua pagina.
    ///
    /// <para>🔴 U-142 (revisione totale 3): il cancello guardava solo il documento. La pagina spariva, e la
    /// ricerca ne citava testi ed estratti con un link a «non disponibile»; «Cosa è cambiato» la elencava.
    /// Vuole <c>Airport</c>, <c>MilAirport</c> e <c>Sectors</c> caricati: le due query che passano di qui li
    /// includono già per i descrittori.</para>
    /// </summary>
    internal static bool PaginaAperta(Document d, ManagedDoc m) => m.Kind switch
    {
        ReleaseTargetType.Airport => d.Airport is not { IsHidden: true },
        ReleaseTargetType.AirportMil => d.MilAirport is not { IsHidden: true },
        // La vIPI APP è dell'ENTE (S49) e non chiude quando una sua posizione sparisce: il documento non dipende
        // dai nominativi IVAO. Vuole `AtcUnit` caricato, come i descrittori.
        ReleaseTargetType.App => d.AtcUnit is { Mode: AtcUnitMode.OwnDocument } ente
                                 && string.Equals(ente.Code, m.ReleaseKey, StringComparison.OrdinalIgnoreCase),
        _ => true,
    };

    /// <summary>Id delle sezioni da NON indicizzare: quelle nascoste e tutto ciò che sta sotto di esse — nel
    /// documento una sezione nascosta si porta via il proprio sottoalbero.</summary>
    public static HashSet<int> HiddenSectionIds(IReadOnlyList<DocumentSection> sections)
    {
        var byId = sections.ToDictionary(s => s.Id);
        var hidden = new HashSet<int>();
        foreach (var s in sections)
        {
            var cur = s;
            var guard = 0;
            while (guard++ < DocumentSection.MaxDepth + 2)
            {
                if (cur.IsHidden) { hidden.Add(s.Id); break; }
                if (cur.ParentSectionId is not int pid || !byId.TryGetValue(pid, out var parent)) break;
                cur = parent;
            }
        }
        return hidden;
    }
}

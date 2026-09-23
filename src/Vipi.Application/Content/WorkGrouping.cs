using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Come si legge la lista «Da fare». Carta <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §2/D1.</summary>
public enum WorkView
{
    /// <summary>Un gruppo per cambiamento a monte: «l'area X è cambiata» e sotto i documenti che la citano.</summary>
    Cambiamento,

    /// <summary>Un gruppo per documento: tutto ciò che aspetta su quel documento.</summary>
    Documento,

    /// <summary>Una riga per (documento, fatto), com'era prima del 23 settembre 2026.</summary>
    Elenco,
}

/// <summary>
/// Un gruppo di righe che dicono la stessa cosa (lo stesso cambiamento, o lo stesso documento).
///
/// <para>Un gruppo di <b>una</b> riga non è un gruppo (§2/D2): la UI mostra la riga com'è. Esiste lo stesso come
/// <see cref="WorkGroup"/> perché l'elenco dei gruppi resti uno solo, ordinato con una regola sola.</para>
/// </summary>
/// <param name="Chiave">Identità stabile del gruppo: serve al <c>@key</c> e a ricordare quali gruppi sono aperti.</param>
/// <param name="Righe">Le righe, già nell'ordine di <see cref="WorkOrdering"/>. La prima è la più urgente.</param>
public sealed record WorkGroup(string Chiave, IReadOnlyList<WorkItem> Righe)
{
    /// <summary>La riga più urgente: dà al gruppo pastiglia, frase e titolo.</summary>
    public WorkItem Capo => Righe[0];

    /// <summary>Più di una riga: c'è una testata da aprire.</summary>
    public bool IsGruppo => Righe.Count > 1;

    /// <summary>
    /// Tutte le righe si chiudono col ✓ (§2/D4). ⚠️ Basta una riga da ripubblicare o da sistemare perché il ✓
    /// di gruppo non ci sia: spuntarla sarebbe la promessa che il giro notturno smentisce.
    /// </summary>
    public bool SiSpuntanoTutte => Righe.All(r => r.SiSpunta && r.ImpactId is not null);
}

/// <summary>
/// Da una lista di righe ai gruppi di una vista. Puro e senza IO, come <see cref="WorkOrdering"/>: la regola
/// si fissa nei test senza database.
/// </summary>
public static class WorkGrouping
{
    public static IReadOnlyList<WorkGroup> Raggruppa(IReadOnlyList<WorkItem> righe, WorkView vista)
    {
        var ordinate = WorkOrdering.Ordina(righe);
        Func<WorkItem, string> chiave = vista switch
        {
            WorkView.Cambiamento => ChiaveCambiamento,
            WorkView.Documento => r => r.DocumentId is int id ? $"doc:{id}" : $"riga:{r.Chiave}",
            _ => r => $"riga:{r.Chiave}",
        };

        // GroupBy conserva l'ordine d'ingresso dentro il gruppo e l'ordine della PRIMA comparsa fra i gruppi:
        // con righe già ordinate, il gruppo sta dove sta la sua riga più urgente (e, a parità, più vecchia).
        // ⚠️ Non basta per l'ordine fra gruppi quando due hanno la stessa urgenza: il più VECCHIO va sopra
        // (§2/D5), e la sua riga più vecchia può non essere la prima. Si riordina esplicitamente.
        return ordinate
            .GroupBy(chiave, StringComparer.Ordinal)
            .Select(g => new WorkGroup(g.Key, g.ToList()))
            .OrderBy(g => (int)g.Righe.Min(r => r.Severita))
            .ThenBy(g => g.Righe.Min(r => r.Da))
            .ThenBy(g => g.Chiave, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Quale cambiamento a monte racconta la riga. Carta §0: dipende da che cosa la riga sa della sua causa.
    /// </summary>
    public static string ChiaveCambiamento(WorkItem r)
    {
        // Incarico scritto da una persona, o nato da una segnalazione ormai chiusa: nessuna causa da condividere.
        if (r.Tipo is not ImpactKind tipo) return $"riga:{r.Chiave}";

        // Guasti di UN documento (il suo bersaglio): due documenti rotti non sono lo stesso cambiamento.
        if (tipo.IsRotto()) return $"riga:{r.Chiave}";

        // La deriva non sa la causa: confronta la copia pubblicata con la bozza. Due documenti con la STESSA
        // frase — stesso tipo, stesse sezioni cambiate — sono lo stesso cambiamento letto due volte.
        // ⚠️ Il separatore è un carattere che non compare nei titoli delle sezioni: con la virgola, «A, B» + «C»
        // e «A» + «B, C» farebbero la stessa chiave.
        if (tipo.IsDaRipubblicare() || tipo.IsDaPreparare())
            return $"frase:{(int)tipo}:{r.FraseKey}:{string.Join("\u001f", r.FraseArgs)}";

        // Un evento sa la sua causa: la sorgente (callsign, area, allegato). Senza sorgente è un fatto sul
        // documento intero, e non si condivide con nessuno.
        return string.IsNullOrWhiteSpace(r.Sorgente)
            ? $"riga:{r.Chiave}"
            : $"ev:{(int)tipo}:{r.Sorgente.Trim().ToUpperInvariant()}";
    }
}

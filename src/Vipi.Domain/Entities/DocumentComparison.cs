namespace Vipi.Domain.Entities;

/// <summary>
/// Il riassunto dei due confronti di un documento — la bozza di oggi contro la release in vigore e contro la prossima
/// programmata — per la pagina Documenti (S133, carta <c>docs/feature/2026-10-28-stato-dei-documenti.md</c> §2.3).
///
/// <para><b>Perché si conserva.</b> Ogni confronto costa uno snapshot del documento; l'elenco ne ha decine, e farli
/// all'apertura della pagina la renderebbe lenta proprio dove serve guardare tutto insieme. Lo scrive il giro della
/// deriva (dopo le modifiche, di notte, dopo una pubblicazione) e la scheda del documento quando la si apre: una riga
/// per documento, riscritta ogni volta. Non è storia, è una cache con l'ora in cui è stata calcolata.</para>
/// <para>⚠️ Conta le sezioni cambiate e basta: quali e come lo dice la scheda, che il confronto lo rifà dal vivo.</para>
/// </summary>
public class DocumentComparison
{
    /// <summary>Il documento: una riga sola per documento, ed è la chiave.</summary>
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public DateTime ComputedUtc { get; set; }

    /// <summary>Il ciclo della release che il pubblico legge; null = nessuna in vigore.</summary>
    public string? EffectiveCycle { get; set; }

    /// <summary>Le sezioni in cui la bozza differisce dalla release in vigore.</summary>
    public int ChangedVsEffective { get; set; }

    /// <summary>Il ciclo della prossima release programmata; null = nessuna.</summary>
    public string? ScheduledCycle { get; set; }

    /// <summary>Le sezioni in cui la bozza differisce dalla prossima programmata.</summary>
    public int ChangedVsScheduled { get; set; }

    /// <summary>Le derivate congelate della programmata non sono più quelle di oggi (TORA, minimi LVP…).</summary>
    public bool ScheduledDerivedChanged { get; set; }
}

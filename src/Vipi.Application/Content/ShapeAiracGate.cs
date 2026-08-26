using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Lo stato della shape di un settore, per il gate. Read-model: niente entità qui dentro.</summary>
/// <param name="Current">La più recente (quel che vede l'editor).</param>
/// <param name="InForce">Quella in vigore, se diversa; null = la corrente è già in vigore.</param>
/// <param name="FromCycle">Il ciclo dal quale <paramref name="Current"/> entra in vigore; null = subito.</param>
public sealed record ShapeState(
    string? Current, string? InForce, string? FromCycle, ShapeSource Source, bool ForcePublished);

/// <summary>
/// Il gate AIRAC delle shape: <b>quale geometria mettere in un documento pubblicato per un dato ciclo</b>.
///
/// <para><b>Perché esiste.</b> Il sectorfile Aurora lo scriviamo noi <b>prima</b> che il ciclo esca, quindi
/// può già contenere il confine del ciclo prossimo. La sezione <c>aor</c> è <c>Frozen</c>, cioè il
/// congelamento di release fotografa quel che trova in catalogo in quell'istante: senza gate, pubblicare
/// oggi metterebbe in vigore in anticipo un confine che non lo è.</para>
///
/// <para><b>La domanda giusta la pone la release.</b> Non «qual è la shape di oggi» ma «qual è la shape in
/// vigore <b>al ciclo di questa release</b>» — e <c>DocRelease</c> il suo ciclo lo sa già
/// (<c>ReleaseAiracCycle</c>). Formulata così regge nei due versi: pubblico per il ciclo corrente e prende
/// quella vecchia, pubblico in anticipo per il prossimo — che è quel che si fa preparando un AIRAC — e
/// prende quella nuova. Senza interruttori da ricordare.</para>
///
/// <para>Funzione pura, e in un posto solo: la stessa regola serve al congelamento, all'avviso di chi
/// pubblica e alla promozione al giro d'import.</para>
/// </summary>
public static class ShapeAiracGate
{
    /// <summary>
    /// La geometria da pubblicare in una release del ciclo <paramref name="releaseCycle"/>.
    ///
    /// <para>Cede alla corrente in ogni caso che non sia «differita e non ancora arrivata»: senza in-vigore
    /// da mostrare, senza ciclo, con la provenienza sbagliata o con la forzatura. <b>Fail-open</b>, come il
    /// gate delle SID: un meccanismo di sicurezza che in caso di dubbio nasconde un'area farebbe più danno
    /// di quello che previene.</para>
    /// </summary>
    public static string? ForRelease(ShapeState shape, string releaseCycle, IAiracService airac) =>
        IsDeferredAt(shape, releaseCycle, airac) ? shape.InForce : shape.Current;

    /// <summary>
    /// Vero se la geometria corrente <b>non è ancora in vigore</b> al ciclo indicato, e c'è una precedente da
    /// mostrare al posto suo. È la condizione che fa scattare sia la sostituzione sia l'avviso a chi pubblica.
    /// </summary>
    public static bool IsDeferredAt(ShapeState shape, string cycle, IAiracService airac)
    {
        if (shape.ForcePublished) return false;                      // qualcuno ha deciso: pubblicala
        if (shape.Source != ShapeSource.Sectorfile) return false;     // solo il sectorfile corre avanti
        if (string.IsNullOrWhiteSpace(shape.FromCycle)) return false; // già in vigore
        if (string.IsNullOrWhiteSpace(shape.InForce)) return false;   // ⚠️ niente da mostrare al posto suo:
                                                                     // è la PRIMA shape del settore, e nessuna
                                                                     // area è peggio di una in anticipo
        try
        {
            // In vigore quando il ciclo della release ha raggiunto quello di entrata. Il confronto è sulle
            // DATE e non sulle stringhe: "2701" viene dopo "2613", ma non in ordine alfabetico.
            return airac.EffectiveUtcForCycle(cycle) < airac.EffectiveUtcForCycle(shape.FromCycle!);
        }
        catch (ArgumentException)
        {
            return false;   // ciclo illeggibile: si pubblica quel che si ha, e lo si vede
        }
    }

    /// <summary>
    /// Il ciclo è arrivato: la geometria corrente è ora in vigore e il differimento si può chiudere. Lo
    /// chiede il giro d'import a ogni passata — nessun lavoro schedulato, nessuna magia sull'orologio.
    /// </summary>
    public static bool IsPromotable(ShapeState shape, DateTime nowUtc, IAiracService airac)
    {
        if (string.IsNullOrWhiteSpace(shape.FromCycle)) return false;
        try { return nowUtc >= airac.EffectiveUtcForCycle(shape.FromCycle!); }
        catch (ArgumentException) { return true; }   // ciclo illeggibile: si chiude, invece di restare appeso
    }
}

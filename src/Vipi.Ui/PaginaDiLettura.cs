using Microsoft.AspNetCore.Components;

namespace Vipi.Ui;

/// <summary>
/// Il tipo della pagina che si sta servendo. Lo fornisce chi ospita il modulo (Vipi.Hosting, dall'endpoint della
/// richiesta): la RCL non vede la richiesta HTTP. ⚠️ Facoltativo: senza, il layout tiene il riquadro di riconnessione
/// dappertutto, che è il comportamento di prima.
/// </summary>
public interface IPaginaCorrente
{
    Type? Tipo { get; }
}

/// <summary>
/// Una pagina di SOLA LETTURA è una pagina SSR statica: il suo tipo non porta un <c>@rendermode</c>, che il compilatore
/// scrive come <see cref="RenderModeAttribute"/> sulla classe. Lì le isole interattive (il badge Live, il meteo) non
/// hanno niente di loro da perdere, e un circuito caduto non deve coprire il documento (30 settembre 2026).
/// </summary>
public static class PaginaDiLettura
{
    public static bool E(Type pagina) => !pagina.IsDefined(typeof(RenderModeAttribute), inherit: true);
}

/// <summary>
/// Una pagina INTERATTIVA ma pubblica, dove chi guarda non costruisce niente: la vista live, la ricerca, le novità, gli
/// spazi aerei. (Non l'elenco vSOP né le statistiche di divisione: hanno un gesto di lavoro dentro, «Crea» e «pubblica».) Lì un circuito caduto non deve coprire la pagina né dire
/// niente, se si rimette in piedi da solo (committente, 1 ottobre 2026: «le persone non se ne accorgono nemmeno il 90%
/// delle volte»): il riquadro non si vede, al «circuito sconosciuto» si ricarica in silenzio allo stesso punto, e
/// l'avviso discreto compare solo quando serve ricaricare A MANO.
/// <para>⚠️ Si dichiara pagina per pagina, e nel dubbio NO: una pagina di lavoro (editor, admin, richieste, strumenti)
/// che la prendesse per sbaglio perderebbe in silenzio il gesto appena fatto — esattamente il difetto che l'avviso
/// «l'ultimo comando potrebbe non essere arrivato» esiste per dire.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RiconnessioneDiscretaAttribute : Attribute;

/// <summary>Il valore di <c>data-riconnessione</c> che il layout scrive per una pagina (vipi-riconnessione.js lo legge).</summary>
public static class ModoRiconnessione
{
    /// <summary>«silenziosa» (SSR statica, i documenti), «discreta» (interattiva pubblica), null (pagina di lavoro).</summary>
    public static string? Di(Type pagina) =>
        PaginaDiLettura.E(pagina) ? "silenziosa"
        : pagina.IsDefined(typeof(RiconnessioneDiscretaAttribute), inherit: true) ? "discreta"
        : null;
}

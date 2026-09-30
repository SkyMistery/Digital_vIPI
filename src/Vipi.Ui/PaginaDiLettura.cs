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

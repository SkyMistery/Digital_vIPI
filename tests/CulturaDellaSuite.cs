using System.Globalization;
using System.Runtime.CompilerServices;

namespace Vipi.Tests.Comune;

/// <summary>
/// Fissa la cultura di ogni assembly di test a <c>en-GB</c> prima che parta il primo test.
///
/// <para>🔴 <b>Perché.</b> Molti test confrontano parole lette dalle risorse, e le aspettano in inglese: è la
/// lingua delle macchine di sviluppo, dove la suite è verde. Il runner Linux della CI gira in cultura
/// invariante, che cade sul resx neutro — l'italiano — e lì gli stessi test erano rossi. Un test che dipende da
/// com'è configurato il PC di chi lo lancia non prova niente: la cultura si dichiara qui, una volta.</para>
///
/// <para>⚠️ Si imposta la cultura <b>predefinita dei thread</b>, non quella del thread corrente: xUnit fa girare i
/// test sul pool. I test che vogliono l'italiano la cambiano da sé e la rimettono (vedi <c>CulturaDiProva</c>).</para>
/// </summary>
internal static class CulturaDellaSuite
{
    #pragma warning disable CA2255 // È un assembly di test, non una libreria: inizializzarlo al caricamento è lo scopo.
    [ModuleInitializer]
    #pragma warning restore CA2255
    internal static void Fissa()
    {
        var cultura = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
    }
}

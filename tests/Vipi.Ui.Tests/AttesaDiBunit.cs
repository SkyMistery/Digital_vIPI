using System.Runtime.CompilerServices;
using Bunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Quanto aspettano i <c>WaitFor…</c> di bUnit in tutta la suite: dieci secondi invece di uno.
///
/// <para>🔴 <b>Perché.</b> In CI (30 settembre 2026, run 36711964233) <c>CorrezioniSpaziAereiPaginaTests.La_matita…</c> è
/// caduto con «Check count: 0, render count: 4»: nel secondo di default l'asserzione non era stata <b>nemmeno provata</b>
/// una volta — il controllo di bUnit non aveva avuto il turno sul runner carico, con le classi di test in parallelo. Da S63
/// <c>Gesto</c> della pagina Spazi aerei cede il passo (<c>Task.Yield</c>) prima di lavorare, e il lavoro arriva dopo un
/// giro in più: la stessa famiglia di cadute del cartellino (<c>DiagnosticaUnGiroAllaVoltaTests</c>,
/// <c>PannelloUnioneUnGiroAllaVoltaTests</c>), e qua e là qualcuno aveva già alzato a mano il timeout a tre secondi.</para>
///
/// <para>⚠️ Un timeout più lungo non rallenta i test che passano: <c>WaitFor</c> esce alla prima verifica buona. Allunga
/// solo quelli che falliscono davvero — e nessun test della suite aspetta apposta un <c>WaitFor</c> che scade
/// (<c>WaitForFailedException</c> non compare nei test).</para>
///
/// <para>Un inizializzatore del modulo e non una classe base: il valore è statico di bUnit, e deve valere prima del
/// primo test di qualunque classe, senza che ogni classe si ricordi di impostarlo.</para>
/// </summary>
internal static class AttesaDiBunit
{
    public static readonly TimeSpan Massima = TimeSpan.FromSeconds(10);

    [ModuleInitializer]
    internal static void Imposta() => TestContextBase.DefaultWaitTimeout = Massima;
}

/// <summary>Che l'attesa sia davvero quella: un inizializzatore tolto per sbaglio non darebbe nessun errore, solo i
/// rossi a tempo di prima.</summary>
public class AttesaDiBunitTests
{
    [Xunit.Fact]
    public void I_WaitFor_della_suite_aspettano_dieci_secondi() =>
        Xunit.Assert.Equal(AttesaDiBunit.Massima, TestContextBase.DefaultWaitTimeout);
}

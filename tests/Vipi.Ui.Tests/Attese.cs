namespace Vipi.Ui.Tests;

/// <summary>
/// Quanto aspetta un <c>WaitForAssertion</c>/<c>WaitForState</c> di bUnit prima di arrendersi.
///
/// <para>⚠️ Il default di bUnit è <b>un secondo</b>, e sul runner della CI non basta: nel job che corre tutti i
/// TFM insieme, con l'Infrastruttura che macina accanto per due minuti, <c>BloccoAllegatoTests</c> è caduto due
/// volte di fila sul commit di L12 (13 settembre 2026), mentre lo stesso test passava nel job solo-net8 e in
/// locale. Un'attesa lunga non rallenta niente: <c>WaitFor</c> esce appena la condizione è vera, e i dieci secondi
/// si pagano solo quando il test è rosso davvero.</para>
/// </summary>
internal static class Attese
{
    internal static readonly TimeSpan AttesaSottoCarico = TimeSpan.FromSeconds(10);
}

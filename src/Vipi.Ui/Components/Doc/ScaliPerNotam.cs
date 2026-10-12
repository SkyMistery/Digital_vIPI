using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Gli scali di cui una pagina di documento mostra i NOTAM: il proprio e quelli degli altri documenti
/// dell'unione (carta <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>).
///
/// <para>⚠️ Anche i membri, e non solo la porta: una pagina unita disegna più documenti uno sotto l'altro, e il
/// riquadro in cima è <b>uno</b>. Chi legge la vIPI di uno scalo unita al suo avvicinamento non deve perdere un
/// NOTAM solo perché è entrato dall'altra porta.</para>
///
/// <para>Sta qui e non nelle tre pagine perché i chiamanti sono tre: la stessa regola scritta tre volte è la
/// prima a divergere.</para>
/// </summary>
internal static class ScaliPerNotam
{
    public static IEnumerable<string> Di(string proprio, IReadOnlyList<MembroUnito> altri) =>
        new[] { AmbitoNotam.AeroportoDi(proprio) }.Concat(
            altri.Select(a => a.Membro.Doc)
                 .Where(d => d.ReleaseTarget is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil
                                             or ReleaseTargetType.App or ReleaseTargetType.AppMil)
                 .Select(d => AmbitoNotam.AeroportoDi(d.ReleaseKey)));
}

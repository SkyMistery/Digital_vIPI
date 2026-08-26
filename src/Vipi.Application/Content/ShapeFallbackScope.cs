using Microsoft.Extensions.Options;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Di chi si occupano i <b>ripieghi</b> delle shape (sectorfile, GitHub TWR, cerchio sintetico): <b>solo gli
/// enti della divisione</b>.
///
/// <para><b>Perché.</b> Decisione del committente, 26 agosto 2026: <i>le aree degli ATC esteri le dà IVAO, se
/// ce le dà</i>. Un ente straniero senza poligono resta senza poligono — non lo si va a prendere dal
/// sectorfile italiano né da GitHub. La ragione è che quei confini non sono nostri: disegnarne uno preso da
/// una fonte che non è l'anagrafica del titolare vuol dire pubblicare come vera un'area che nessuno di
/// competente ha approvato. Meglio nessuna area di un'area inventata.</para>
///
/// <para>⚠️ <b>Vale per i ripieghi, non per l'anagrafica.</b> Le shape che IVAO manda si scrivono per tutti,
/// esteri compresi, esattamente come prima: qui si decide soltanto dove i ripieghi hanno diritto di parola.</para>
///
/// <para>La regola sta in un posto solo perché i ripieghi sono tre, e tre copie della stessa condizione sono
/// tre racconti che prima o poi divergono.</para>
/// </summary>
public sealed class ShapeFallbackScope
{
    private readonly IReadOnlyList<string> _prefissi;

    /// <summary>⚠️ Costruttore <b>unico</b>, e apposta: due costruttori qui dentro renderebbero il tipo
    /// ambiguo per il contenitore DI, che sceglie quello con più parametri risolvibili. Chi ha i prefissi già
    /// in mano (i test) usa <see cref="ForPrefixes"/>.</summary>
    public ShapeFallbackScope(IOptions<DivisionOptions>? division = null)
        : this((IReadOnlyList<string>?)division?.Value.IcaoPrefixes) { }

    private ShapeFallbackScope(IReadOnlyList<string>? divisionPrefixes) =>
        _prefissi = divisionPrefixes is { Count: > 0 } p ? p : new[] { "LI" };

    /// <summary>Perimetro costruito su prefissi dati: per i test e per i giri che non passano dal DI.</summary>
    public static ShapeFallbackScope ForPrefixes(params string[] prefixes) => new(prefixes);

    /// <summary>
    /// Vero se il callsign (<c>LIRR_NE_CTR</c>) o l'ICAO (<c>LIBA</c>) è della divisione. Riusa la stessa
    /// domanda della gerarchia (<see cref="HierarchyRules.IsForeignCode"/>): «estero» ha una definizione sola.
    /// </summary>
    public bool IsDomestic(string? callsignOrIcao) =>
        !string.IsNullOrWhiteSpace(callsignOrIcao) && !HierarchyRules.IsForeignCode(callsignOrIcao!, _prefissi);
}

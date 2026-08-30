using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// La forma di un settore come la vede <b>chiunque</b>: i pezzi da disegnare, con le loro quote, e la fonte
/// che li ha dati.
///
/// <para><paramref name="UncoveredKeys"/> = agganci che nel caricamento in vigore non esistono più. ⚠️ Non
/// sono un errore da nascondere: il settore torna alla forma di IVAO e la pagina deve poter dire quale
/// aggancio è rimasto senza volume.</para>
/// </summary>
public sealed record SectorShape(
    string Callsign, ShapeSource Source, IReadOnlyList<ShapePart> Parts, IReadOnlyList<string> UncoveredKeys)
{
    /// <summary>Vero se non c'è niente da disegnare: sotto questo, chi chiede non mostra nulla.</summary>
    public bool IsEmpty => Parts.Count == 0;

    /// <summary>La forma viene dall'AIP, cioè da una scelta umana: la pagina lo dice, e la stampa cita la fonte.</summary>
    public bool FromAip => Source == ShapeSource.Aip;
}

/// <summary>
/// <b>La porta unica per la forma di un settore</b> — anello <b>e</b> quote, sempre della stessa fonte.
///
/// <para>Prima di questa porta la forma si leggeva in sei posti diversi, e l'aggancio agli spazi aerei
/// dell'AIP era onorato in <b>due</b>: un avvicinamento agganciato <i>disegnava</i> il confine dell'AIP nel
/// documento e <i>rivendicava</i> il traffico dentro il monoblocco di IVAO. Due verità sullo stesso oggetto,
/// e quella sbagliata non dava nessun errore: dava numeri. Carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>.</para>
///
/// <para>La precedenza, che vive <b>qui e in nessun altro posto</b>:</para>
/// <list type="number">
///   <item><b>L'aggancio all'AIP</b>, risolto sul caricamento in vigore: N pezzi, ognuno con la sua banda.</item>
///   <item><b>I pezzi in archivio</b> (<c>SectorShapeParts</c>) della fonte del settore.</item>
///   <item><b>Le colonne del catalogo</b>: un anello e le due quote di IVAO — con il <b>gate AIRAC</b>, che
///     durante il congelamento di una release dà la geometria in vigore a quel ciclo.</item>
/// </list>
///
/// <para>⚠️ <b>L'assenza non cancella mai.</b> Un aggancio che non si risolve, o una fonte muta, fanno
/// scendere al gradino sotto — non a «nessuna forma». È la lezione del 26 agosto 2026, quando un <c>[]</c> di
/// sorgente azzerò 83 poligoni su 83.</para>
/// </summary>
public interface ISectorShapeResolver
{
    /// <summary>
    /// Le forme dei callsign chiesti. Chiave = callsign maiuscolo; un callsign senza nessuna forma —
    /// nemmeno di IVAO — <b>non compare</b>: sono i settori che un'area non ce l'hanno (DEL e GND).
    /// </summary>
    Task<IReadOnlyDictionary<string, SectorShape>> ResolveAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default);
}

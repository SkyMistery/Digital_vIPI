using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>
/// Le aree regolamentate viste come una MAPPA sola, nella stessa forma dell'AoR (<see cref="AccAorView"/>).
///
/// <para><b>Perché.</b> Fino al 27 agosto 2026 la sezione era un elenco di dettagli collassabili, ognuno con
/// la <b>propria</b> mappina: su LIBB ne uscivano 77 e su LIRR 105 — è il caso che `vipi-aor.js` cita nel
/// commento sull'accensione a scaglioni, cioè centinaia di tessere chieste per disegnare cento volte lo
/// stesso pezzo di Mediterraneo. Una mappa sola con le chip fa lo stesso lavoro con un centesimo dei
/// tasselli, e per giunta permette di CONFRONTARE due aree, che con le mappine era impossibile.</para>
///
/// <para><b>Perché la forma è quella dell'AoR e non una nuova.</b> Il 2D (Leaflet, chip, tutti/nessuno), il
/// 3D (prismi estrusi per banda FL) e il commutatore fra i due esistono già e sono guidati dal DOM:
/// riusarli vuol dire zero motore nuovo lato mappa. Il prezzo è una traduzione di nomi, ed è questa classe:
/// un'area diventa un «settore» il cui <c>Callsign</c> è l'<b>id IVAO</b> (chiave tecnica, quella che finisce
/// in <c>data-sec</c>) e il cui <c>Name</c> è il nome leggibile.</para>
///
/// <para>⚠️ <b>L'id, non il nome, fa da chiave.</b> I nomi delle aree contengono spazi, punti e trattini
/// («LI R300A Amendola bis») e il JS li userebbe dentro un selettore <c>[data-sec="…"]</c>. L'id IVAO è un
/// numero: non ha niente da rompere e non collide.</para>
///
/// PURA/deterministica, nessun I/O.
/// </summary>
public static class RegulatedAreasMap
{
    /// <summary>
    /// Vista mappa delle aree, nell'ordine in cui arrivano. Le aree <b>senza shape</b> restano nell'elenco
    /// (con zero poligoni) e non spariscono: la loro chip continua ad accendere e spegnere la descrizione,
    /// che è l'unica cosa che di loro si può mostrare.
    /// </summary>
    public static AccAorView Build(IReadOnlyList<AccSpecialAreaView> areas)
    {
        if (areas.Count == 0) return AccAorView.Empty;

        var sectors = new List<AccSectorAor>(areas.Count);
        foreach (var a in areas)
        {
            var (bottom, top) = AorFlBand.Normalize(a.MinimumAlt, a.MaximumAlt);
            sectors.Add(new AccSectorAor(
                Callsign: a.IvaoId,
                Name: a.Name,
                Color: SpecialAreaColorScheme.For(a.Type),
                Polygons: a.Shape is null ? Array.Empty<AppAorPolygon>() : new[] { a.Shape },
                LowerFl: bottom,
                UpperFl: top,
                Label: ChipLabel(a)));
        }

        // Il posto delle chip-configurazione lo prendono i preset per TIPO: stesso contratto («accendi
        // esattamente questo insieme»), altra semantica. Vanno QUI dentro e non lasciati al componente,
        // perché la fila di tasti la disegna AccAor leggendo Configs: costruirli e non metterceli è
        // esattamente il modo in cui, alla prima prova dal vivo, la fila non compariva affatto.
        return new AccAorView(sectors, Presets(areas));
    }

    /// <summary>
    /// Testo della chip. Il nome intero non ci sta — su LIRR le chip sono 105 e i nomi arrivano a
    /// «LI R301B SMarco in Lamis Bis» — e il prefisso <c>LI </c> è su quasi tutte, quindi non distingue
    /// niente: si toglie. Il tipo non si ripete nella chip perché lo dice già il colore.
    /// </summary>
    public static string ChipLabel(AccSpecialAreaView a)
    {
        var n = (a.Name ?? "").Trim();
        return n.StartsWith("LI ", StringComparison.OrdinalIgnoreCase) && n.Length > 3 ? n[3..].Trim() : n;
    }

    /// <summary>
    /// I preset per tipo: una voce per ogni tipo presente, con gli id delle aree di quel tipo. Riusano il
    /// contratto delle chip-configurazione dell'AoR («accendi esattamente questo insieme»), che è esattamente
    /// la semantica giusta per «mostrami solo le zone R».
    /// </summary>
    public static IReadOnlyList<AccConfigSelection> Presets(IReadOnlyList<AccSpecialAreaView> areas)
    {
        if (areas.Count == 0) return Array.Empty<AccConfigSelection>();

        var result = new List<AccConfigSelection>();
        foreach (var tipo in SpecialAreaColorScheme.OrderTypes(areas.Select(a => a.Type)))
        {
            var ids = areas
                .Where(a => string.Equals((a.Type ?? "").Trim(), tipo, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.IvaoId)
                .ToList();
            if (ids.Count > 0) result.Add(new AccConfigSelection(tipo, tipo, ids));
        }
        return result;
    }
}

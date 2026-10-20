using System.Globalization;
using Aip;
using Aip.Aurora;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Un'area dell'AIP espanso con un tratto di confine o di costa, nella sezione «Confini» del Confronto (riordino R9, D20):
/// <paramref name="Chiave"/> è quella dell'area (<c>LIMM FIR MILANO</c>); <paramref name="Tracciabile"/> se tutti i suoi
/// tratti si trovano sul tracciato di <c>itgeo.geo</c>; <paramref name="Correzione"/> la correzione che ce li ha già
/// portati, se c'è.
/// </summary>
public sealed record ConfineDellAip(EsitoDelConfronto Esito, string Chiave, AirspaceType Tipo, bool Tracciabile, IReadOnlyList<string> Dettagli, string? Correzione);

/// <summary>
/// Dal sector all'AIP espanso (riordino R9, D20): i tratti di confine e di costa che l'AIP nomina senza disegnarli
/// («Italian northern geographical border», «line at 500 m from coast») presi dal tracciato di <c>GEO/itgeo.geo</c> —
/// l'unica eccezione alla regola dei dati solo da fonte primaria (committente, 19 ottobre: «usiamo itgeo.geo per i confini,
/// un'unica eccezione lì»).
/// <para>Il gesto scrive una <b>correzione</b> dell'area, come «Correggi» (D22: l'AIP espanso si cambia solo così): nel
/// <c>before</c> l'area del PDF con la sua frase, nell'<c>after</c> la stessa area coi tratti di confine fatti di rette sui
/// punti del sector. 🟡 Proposta dell'agente: rette e non un tratto di confine coi punti, perché il modello dell'AIP (comune,
/// multi-Stato) non cambia; la frase resta nel <c>before</c> e nella fonte della correzione.</para>
/// </summary>
public static class ConfiniNellAip
{
    /// <summary>La fonte delle correzioni di questo gesto: le riconosce come «già portate».</summary>
    public const string Fonte = "GEO/itgeo.geo del sector (eccezione del committente, 19 ottobre 2026)";

    /// <summary>
    /// Le righe della sezione: le aree con un tratto di confine o di costa (da portare: <see cref="EsitoDelConfronto.SoloNelSector"/>,
    /// il sector ha il tracciato e l'AIP no) e quelle a cui è già stato portato da una correzione in piedi
    /// (<see cref="EsitoDelConfronto.Uguale"/>). <paramref name="tracciatore"/> null: il sector non ha <c>itgeo.geo</c>.
    /// </summary>
    public static IReadOnlyList<ConfineDellAip> Righe(AipDocument modello, AipCorrectionFile? correzioni, BorderTracer? tracciatore)
    {
        ArgumentNullException.ThrowIfNull(modello);
        var righe = new List<ConfineDellAip>();
        foreach (var area in modello.Airspaces)
        {
            if (area.Geometry is RingGeometry anello && anello.Segments.Any(s => s is BorderSegment))
            {
                var dettagli = new List<string>();
                bool tracciabile = tracciatore is not null;
                for (int i = 0; i < anello.Segments.Count; i++)
                {
                    if (anello.Segments[i] is not BorderSegment b)
                        continue;
                    var a = anello.Segments[(i + 1) % anello.Segments.Count].From;
                    var punti = tracciatore is null ? null : Punti(tracciatore, b, a);
                    tracciabile &= punti is not null;
                    var retta = Sphere.DistanceNm(b.From, a);
                    dettagli.Add(punti is null
                        ? $"«{b.Text}»: {(tracciatore is null ? "il sector non ha itgeo.geo" : "non si trova sul tracciato del sector")} (retta di {Nm(retta)} NM)"
                        : $"«{b.Text}»: {punti.Count} {(punti.Count == 1 ? "punto" : "punti")} dal sector, {Nm(Lunghezza(b.From, punti, a))} NM (retta di {Nm(retta)} NM)");
                }
                righe.Add(new ConfineDellAip(EsitoDelConfronto.SoloNelSector, area.Key, area.Type, tracciabile, dettagli, null));
            }
            else if (correzioni is not null && CorrezioniDellAip.Attiva(correzioni, area.Key) is { Source: Fonte } fatta)
            {
                righe.Add(new ConfineDellAip(EsitoDelConfronto.Uguale, area.Key, area.Type, true,
                    [$"portato dal sector con la correzione {fatta.Id} (ritirandola torna la frase dell'AIP)"], fatta.Id));
            }
        }
        return [.. righe.OrderBy(r => r.Esito == EsitoDelConfronto.Uguale ? 1 : 0).ThenBy(r => r.Chiave, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// L'area coi tratti di confine e di costa fatti di rette sui punti del sector: il tratto parte dal suo capo e passa per i
    /// punti del tracciato; il capo dopo è l'inizio del tratto che segue, come prima. Null, col perché, se un tratto non si
    /// trova o l'area non ne ha.
    /// </summary>
    public static (Airspace? Area, string? Perche) Tracciata(Airspace area, BorderTracer tracciatore)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(tracciatore);
        if (area.Geometry is not RingGeometry anello || !anello.Segments.Any(s => s is BorderSegment))
            return (null, $"{area.Key}: non ha tratti di confine o di costa.");
        var tratti = new List<Segment>();
        for (int i = 0; i < anello.Segments.Count; i++)
        {
            if (anello.Segments[i] is not BorderSegment b)
            {
                tratti.Add(anello.Segments[i]);
                continue;
            }
            var a = anello.Segments[(i + 1) % anello.Segments.Count].From;
            if (Punti(tracciatore, b, a) is not { } punti)
                return (null, $"{area.Key}: il tratto «{b.Text}» non si trova sul tracciato del sector.");
            tratti.Add(new LineSegment(b.From));
            tratti.AddRange(punti.Select(p => new LineSegment(p)));
        }
        return (area with { Geometry = new RingGeometry(tratti) }, null);
    }

    /// <summary>Il perché della correzione: le frasi dell'AIP che il sector disegna.</summary>
    public static string Perche(Airspace area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var frasi = area.Geometry is RingGeometry anello ? anello.Segments.OfType<BorderSegment>().Select(b => $"«{b.Text}»").Distinct() : [];
        return $"L'AIP nomina {string.Join(", ", frasi)} senza disegnarlo: il tracciato viene dal sector.";
    }

    /// <summary>Quanto un punto del tracciato può stare da un capo del tratto per essere lui (NM): il vertice su cui il capo cade.</summary>
    private const double SulCapo = 0.05;

    /// <summary>
    /// I punti del sector fra i capi del tratto, senza i capi: la strada sulla rete parte e arriva su un vertice, che può
    /// essere il capo stesso (allora sarebbe un tratto lungo zero).
    /// </summary>
    private static IReadOnlyList<Position>? Punti(BorderTracer tracciatore, BorderSegment tratto, Position a) =>
        tracciatore(tratto, tratto.From, a) is { } punti
            ? [.. punti.Where(p => Sphere.DistanceNm(p, tratto.From) > SulCapo && Sphere.DistanceNm(p, a) > SulCapo)]
            : null;

    private static double Lunghezza(Position da, IReadOnlyList<Position> punti, Position a)
    {
        double nm = 0;
        var prima = da;
        foreach (var p in punti.Append(a))
        {
            nm += Sphere.DistanceNm(prima, p);
            prima = p;
        }
        return nm;
    }

    private static string Nm(double valore) => valore.ToString("0", CultureInfo.InvariantCulture);
}

using Vipi.Application.Aor;
using Vipi.Application.Content;

namespace Vipi.Application.Airspace;

/// <summary>La forma di un settore presa dall'AIP: i poligoni da disegnare e la banda per il 3D.</summary>
public sealed record AirspaceAorShape(IReadOnlyList<AppAorPolygon> Polygons, int? LowerFl, int? UpperFl);

/// <summary>
/// Da aggancio a forma disegnabile. PURA, e sta in un posto solo perché le viste AoR che la usano sono
/// <b>due</b> — quella dell'APP e quella dell'ACC — e due copie diverebbero due comportamenti diversi il
/// giorno che una delle due cambia.
///
/// <para>⚠️ <b>I poligoni sono una LISTA, ed è tutto il punto.</b> La colonna della shape di un settore ne
/// tiene <b>uno</b>: è il motivo per cui l'aggancio non ci si scrive dentro (carta §6-bis). Amendola sono
/// due zone e Catania sette, e un CTR di sette zone ridotto alla prima sarebbe un confine sbagliato
/// disegnato senza un errore da nessuna parte.</para>
///
/// <para>⚠️ <b>La banda del 3D viene dai volumi, non dai limiti del settore.</b> Se un avvicinamento è stato
/// agganciato al suo CTR è perché quel CTR <i>è</i> il suo spazio: base e tetto giusti sono i suoi. Si prende
/// l'inviluppo — la base più bassa e il tetto più alto — perché il 3D estrude un settore per volta, e con
/// zone a quote diverse l'inviluppo è l'unica risposta che non nasconde niente.</para>
/// </summary>
public static class AirspaceAor
{
    /// <summary>
    /// La forma agganciata, o <c>null</c> se non c'è niente da sostituire — nessun aggancio, tutti scoperti,
    /// o nessun poligono che si disegni. <b>Null vuol dire «lascia il settore com'era»</b>: un aggancio che
    /// non si risolve non deve cancellare l'area che il settore già mostrava.
    /// </summary>
    public static AirspaceAorShape? Shape(SectorAirspaceBindingRow? binding)
    {
        if (binding is null || binding.Volumes.Count == 0) return null;

        var poligoni = new List<AppAorPolygon>(binding.Volumes.Count);
        foreach (var v in binding.Volumes)
            if (AorPolygonProjector.Project(v.PolygonJson) is { } p)
                poligoni.Add(p);

        if (poligoni.Count == 0) return null;

        // Inviluppo delle quote. ⚠️ `AorFlBand.Normalize` legge un valore sopra 660 come PIEDI e lo divide
        // per cento: i nostri piedi (2500, o 10500 per un FL105) ci passano già giusti.
        var basi = binding.Volumes.Select(v => v.BaseFeet).Where(f => f is not null).Select(f => f!.Value).ToList();
        var tetti = binding.Volumes.Select(v => v.TopFeet).ToList();

        // Un tetto illimitato (null) vince su tutti: se anche un solo volume non ha soffitto, non ce l'ha
        // nemmeno l'inviluppo.
        int? tetto = tetti.Any(t => t is null) ? null : tetti.Max();
        int? baseQ = basi.Count > 0 ? basi.Min() : null;

        var (bottom, top) = AorFlBand.Normalize(baseQ, tetto);
        return new AirspaceAorShape(poligoni, bottom, top);
    }
}

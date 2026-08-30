using Vipi.Domain;
using Vipi.Application.Aor;
using Vipi.Application.Content;

namespace Vipi.Application.Airspace;

/// <summary>
/// Gli spazi aerei visti come <b>una mappa sola</b>, nella forma dell'AoR (<see cref="AccAorView"/>).
/// Gemella di <see cref="RegulatedAreasMap"/>, e per la stessa ragione: il 2D con le chip, il 3D e il
/// commutatore fra i due <b>esistono già</b> e sono guidati dal DOM. Riusarli vuol dire nessun motore nuovo
/// lato mappa; il prezzo è una traduzione di nomi, ed è questa classe.
///
/// <para>⚠️ <b>La chiave tecnica è l'id, non il nome.</b> I nomi dei volumi contengono spazi, barre e
/// apostrofi (<c>CTA MILANO Z36 VALLE D'AVETO</c>), e il JS li usa dentro un selettore
/// <c>[data-sec="…"]</c>. L'id è un numero: non ha niente da rompere. È la stessa lezione delle aree
/// regolamentate.</para>
///
/// <para>⚠️ <b>I settori nostri e i volumi dell'AIP stanno nella STESSA mappa</b>, ed è tutto il punto della
/// pagina: metterli in due mappe affiancate vorrebbe dire chiedere a chi guarda di confrontare a memoria
/// due immagini con due scale diverse. Il prefisso della chiave (<c>s:</c> e <c>v:</c>) tiene separate le
/// due famiglie di identificatori, che altrimenti potrebbero collidere.</para>
///
/// PURA/deterministica, nessun I/O.
/// </summary>
public static class AirspaceMap
{
    /// <summary>Prefisso della chiave tecnica di un settore nostro.</summary>
    public const string ChiaveSettore = "s:";

    /// <summary>Prefisso della chiave tecnica di un volume dell'AIP.</summary>
    public const string ChiaveVolume = "v:";

    /// <summary>
    /// La mappa: prima i settori nostri, poi i volumi dell'AIP, e sotto un <b>preset per famiglia</b> più uno
    /// per i settori — che riusano il contratto delle chip-configurazione («accendi esattamente questo
    /// insieme»), cioè esattamente la semantica di «mostrami solo i CTR».
    /// </summary>
    public static AccAorView Build(
        IReadOnlyList<SectorShapeRow> settori, IReadOnlyList<AirspaceVolumeRow> volumi)
    {
        var anelli = new List<AccSectorAor>();

        foreach (var s in settori)
        {
            // ⚠️ La geometria IN VIGORE, non l'ultima arrivata: è quel che i documenti pubblicano oggi, e
            // una pagina pubblica che mostrasse la prossima direbbe una cosa che non è ancora vera.
            var poly = AorPolygonProjector.Project(s.Shape.InForce ?? s.Shape.Current);
            if (poly is null) continue;

            anelli.Add(new AccSectorAor(
                Callsign: ChiaveSettore + s.Callsign,
                Name: s.Callsign,
                Color: AorColorScheme.Resolve(s.Callsign, null),
                Polygons: new[] { poly },
                Label: s.Callsign));
        }

        foreach (var v in volumi)
        {
            var poly = AorPolygonProjector.Project(v.PolygonJson);
            if (poly is null) continue;

            var (bottom, top) = AorFlBand.Normalize(v.BaseFeet, v.TopFeet);
            anelli.Add(new AccSectorAor(
                Callsign: ChiaveVolume + v.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name: $"{v.Name} · {v.BandLabel}",
                Color: AirspaceColorScheme.For(v.Family),
                Polygons: new[] { poly },
                LowerFl: bottom,
                UpperFl: top,
                Label: v.Name));
        }

        return anelli.Count == 0 ? AccAorView.Empty : new AccAorView(anelli, Presets(settori, volumi));
    }

    /// <summary>Un preset per famiglia presente, più uno per i settori nostri se ce ne sono.</summary>
    public static IReadOnlyList<AccConfigSelection> Presets(
        IReadOnlyList<SectorShapeRow> settori, IReadOnlyList<AirspaceVolumeRow> volumi)
    {
        var presets = new List<AccConfigSelection>();

        if (settori.Count > 0)
            presets.Add(new AccConfigSelection("settori", "settori",
                settori.Select(s => ChiaveSettore + s.Callsign).ToList()));

        foreach (var gruppo in volumi.GroupBy(v => v.Family).OrderBy(g => (int)g.Key))
            presets.Add(new AccConfigSelection(
                gruppo.Key.ToString().ToLowerInvariant(),
                gruppo.Key.ToString().ToUpperInvariant(),
                gruppo.Select(v => ChiaveVolume + v.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList()));

        return presets;
    }
}

/// <summary>
/// Un colore per famiglia di spazio aereo. Gemello di <c>SpecialAreaColorScheme</c>.
///
/// <para>Le tinte seguono la convenzione delle carte: il <b>controllato</b> in blu (più scuro sotto, più
/// chiaro in quota), la <b>zona di traffico</b> in verde, la <b>FIR</b> in grigio perché è la cornice e non
/// un'area operativa, la <b>TMZ</b> in viola. ⚠️ Sono tinte <b>nostre</b>, non colori ufficiali: per le
/// classi di spazio aereo un colore ufficiale non esiste, ed è lo stesso motivo per cui le TSA/TRA non ne
/// hanno uno.</para>
/// </summary>
public static class AirspaceColorScheme
{
    /// <summary>Il colore della famiglia; le famiglie non utilizzabili tornano il grigio delle altre aree.</summary>
    public static string For(AirspaceFamily family) => family switch
    {
        AirspaceFamily.Ctr => "#1d4ed8",
        AirspaceFamily.Cta => "#3b82f6",
        AirspaceFamily.Tma => "#0891b2",
        AirspaceFamily.Atz => "#059669",
        AirspaceFamily.Fir => "#64748b",
        AirspaceFamily.Tmz => "#7c3aed",
        _ => "#94a3b8",
    };
}

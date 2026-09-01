namespace Vipi.Application.Abstractions;

/// <summary>
/// Una posizione ATC come la dichiara il sectorfile (<c>OTHER/itfreq.frq</c>): callsign e frequenza.
/// <para>⚠️ La terza colonna del file è una <b>lista di visibilità con esclusioni</b> (<c>-LIMM_WN4_CTR</c>)
/// e non entra qui: dice che cosa Aurora disegna a chi apre quella posizione, non un fatto aeronautico.</para>
/// </summary>
/// <param name="Frequency">MHz come scritta nel file (<c>135.455</c>); null se il campo non è una frequenza.</param>
public sealed record SectorfilePosition(string Callsign, string? Frequency);

/// <summary>
/// Un aeroporto come lo dichiara il sectorfile (<c>OTHER/itap.ap</c>).
/// <para>⚠️ Il file elenca anche voci che aeroporti non sono (<c>LIZZ … AIR DEFENCE</c>) e 44 scali che vIPI
/// non documenta: chi confronta deve saperlo, il parser non filtra nulla.</para>
/// </summary>
/// <param name="TransitionAltitudeFt">TA in piedi; <b>0 nel file = non dichiarata</b>, e qui diventa null.</param>
public sealed record SectorfileAirport(
    string Icao, int? ElevationFt, int? TransitionAltitudeFt, double? Lat, double? Lon, string? Name);

/// <summary>
/// Una <b>estremità</b> di pista come la dichiara il sectorfile (<c>OTHER/itrw.rw</c>): ogni riga del file ne
/// descrive due (le due soglie della stessa pista), e qui diventano due righe.
///
/// <para>⚠️ <b>Il file non contiene le lunghezze di pista.</b> I campi 4 e 5 sono le <b>elevazioni delle due
/// soglie</b>, non le lunghezze — la descrizione in <c>STATO_SECTORFILE_ITALIANO.md</c> §5 dice «lunghezze»
/// ed è imprecisa. Misurato il 1 settembre 2026 sui dati veri.</para>
///
/// <para>⚠️ Il <b>QFU c'è ma non si usa</b>, e non è una dimenticanza: misurato, confrontarlo con
/// <c>AirportRunway.Bearing</c> produce 115 divergenze a 1° e zero a 5°, cioè solo rumore. Vedi
/// <c>docs/design/piano-coerenza-sectorfile.md</c> §3/C.</para>
/// </summary>
/// <param name="Ident">Designatore <b>normalizzato</b>: senza zero iniziale (<c>09</c> → <c>9</c>), perché il
/// sectorfile lo scrive e IVAO no.</param>
public sealed record SectorfileRunwayEnd(
    string Icao, string Ident, int? ThresholdElevationFt, double? ThresholdLat, double? ThresholdLon);

/// <summary>
/// Quel che il sectorfile afferma sulle cose che <b>anche vIPI</b> tiene: posizioni, aeroporti, piste. È la
/// materia prima del confronto di coerenza, e non viene importata da nessuna parte — la sorgente
/// autoritativa di questi dati resta l'API IVAO (ADR-0006).
/// </summary>
public sealed record SectorfileFacts(
    IReadOnlyList<SectorfilePosition> Positions,
    IReadOnlyList<SectorfileAirport> Airports,
    IReadOnlyList<SectorfileRunwayEnd> RunwayEnds)
{
    public static readonly SectorfileFacts Empty =
        new(Array.Empty<SectorfilePosition>(), Array.Empty<SectorfileAirport>(), Array.Empty<SectorfileRunwayEnd>());
}

/// <summary>
/// Porta neutra: i tre file del sectorfile che descrivono posizioni, aeroporti e piste.
/// <para>null = la sorgente non è configurata o non ha risposto. ⚠️ <b>Non</b> è <see cref="SectorfileFacts.Empty"/>:
/// «non lo so» e «il sectorfile non ha niente» portano a due conclusioni opposte, e un confronto contro il
/// vuoto aprirebbe un rilievo su <i>ogni</i> riga che abbiamo.</para>
/// </summary>
public interface ISectorfileFactsSource
{
    Task<SectorfileFacts?> GetFactsAsync(CancellationToken ct = default);
}

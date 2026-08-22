using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Cosa passare a <c>IAirportRepository.MergeFromSourceAsync</c> secondo la policy di import: la TA
/// dall'anagrafica e le piste dal dettaglio, ma <b>solo</b> per le categorie che la policy dichiara di
/// sorgente. Per le altre si passa «nessun cambio» (TA <c>null</c>, lista piste vuota), che è come il merge
/// legge l'assenza di dati.
///
/// <para><b>Perché un punto solo.</b> Lo stesso merge lo chiamano due percorsi: il reimport dell'editor
/// aeroporto (<see cref="AirportEditingService.ReimportFromSourceAsync"/>) e la generazione del documento
/// (<see cref="StructureEditingService"/>, «Genera documenti» e il massivo di <c>/services/vsop/admin/airports</c>).
/// Fino al 22 agosto 2026 il primo leggeva la policy e il secondo no: con «Piste» o «Transition Altitude»
/// escluse in Sorgenti, generare il documento sovrascriveva la TA scritta a mano, riportava lunghezza e
/// bearing della sorgente sulle piste e faceva rientrare le piste che l'utente aveva tolto (il merge
/// aggiunge quelle che non trova). Un gate per categoria, non uno per chiamante.</para>
/// </summary>
internal static class SourceMergeInputs
{
    /// <summary>Legge dalla sorgente le sole categorie importate. La TA è best-effort: se l'anagrafica non
    /// risponde resta <c>null</c> (= invariata), perché non avere la TA non deve impedire il resto.</summary>
    public static async Task<(int? Ta, List<(string Ident, int? LengthM, int? Bearing)> Runways)> ReadAsync(
        ImportPolicySnapshot policy, string icao,
        IAirportDirectory directory, IAirportDetailProvider details, CancellationToken ct)
    {
        var runways = policy.Runways
            ? (await details.GetRunwaysAsync(icao, ct)).Select(r => (r.Ident, r.LengthM, r.Bearing)).ToList()
            : new List<(string, int?, int?)>();

        int? ta = null;
        if (policy.TransitionAltitude)
            try
            {
                ta = (await directory.GetAirportsAsync(ct))
                    .FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase))?.TransitionAltitude;
            }
            catch { /* anagrafica non disponibile: TA resta invariata, la sezione si completa a mano */ }

        return (ta, runways);
    }
}

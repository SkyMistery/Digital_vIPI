using System.Linq.Expressions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Regola unica per capire QUALE documento descrive un aeroporto.
/// <para>
/// Un settore <see cref="SectorKind.Airport"/> porta l'ICAO anche quando è un APP NON REMOTIZZATO
/// (Type=App, ApproachKind=Standalone), che però ha un documento tutto suo (doc 08e) con editor e
/// viewer dedicati. Cercare il documento d'aeroporto con il solo «Kind=Airport && AirportIcao=X»
/// pesca quindi anche l'APP: dove i due documenti coesistono (es. LIBA_APP e LIBA_TWR) vince quello
/// che il database restituisce per primo, e l'aeroporto si ritrova a mostrare/pubblicare l'APP.
/// </para>
/// <para>Chi risolve ICAO → documento d'aeroporto DEVE passare da qui.</para>
/// </summary>
internal static class SectorDocumentRules
{
    /// <summary>Settore che può identificare il documento di AEROPORTO: aeroportuale, ma non un APP standalone.</summary>
    internal static readonly Expression<Func<Sector, bool>> IsAirportDocSector =
        s => s.Kind == SectorKind.Airport
             && !(s.Type == SectorType.App && s.ApproachKind == ApproachKind.Standalone);

    /// <summary>Filtra i settori che identificano documenti d'aeroporto (esclude gli APP standalone).</summary>
    internal static IQueryable<Sector> AirportDocSectors(this IQueryable<Sector> sectors) =>
        sectors.Where(IsAirportDocSector);

    /// <summary>Versione in memoria della stessa regola (per i descrittori che lavorano su un Document già caricato).</summary>
    internal static bool IsAirportDocSectorOf(Sector s) =>
        s.Kind == SectorKind.Airport
        && !(s.Type == SectorType.App && s.ApproachKind == ApproachKind.Standalone);
}

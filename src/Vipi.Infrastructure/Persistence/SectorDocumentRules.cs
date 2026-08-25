using System.Linq.Expressions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Come si trovava il documento di un aeroporto <b>prima del 25 agosto 2026</b>: passando dai suoi settori.
///
/// <para>⚠️ <b>Non è più la strada.</b> Il documento d'aeroporto è legato all'AEROPORTO
/// (<c>Airport.DocumentId</c>), perché descrive uno scalo e non un suo settore. Chi risolve ICAO → documento
/// deve chiederlo lì. Questa regola sopravvive per un solo lavoro: il <b>ponte</b> che porta i documenti già
/// scritti sul legame nuovo (<c>EfDocumentMaintenance.LinkAirportDocumentsAsync</c>), che per forza deve
/// leggere dove il dato viveva prima.</para>
///
/// <para>Il perché del filtro, che vale ancora dentro il ponte: un settore <see cref="SectorKind.Airport"/>
/// porta l'ICAO anche quando è un APP NON REMOTIZZATO (Type=App, ApproachKind=Standalone), che però ha un
/// documento tutto suo (doc 08e). Cercare con il solo «Kind=Airport &amp;&amp; AirportIcao=X» pesca quindi anche
/// l'APP: dove i due coesistono (LIBA_APP e LIBA_TWR) vince quello che il database restituisce per primo, e
/// l'aeroporto si ritrova a mostrare l'APP.</para>
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
}

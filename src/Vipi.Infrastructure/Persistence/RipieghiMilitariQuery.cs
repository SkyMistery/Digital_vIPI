using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Da dove si leggono gli APP militari e i loro MIL_CTR fratelli (<see cref="RipiegoMilitare"/>). Una query sola per
/// i tre posti che leggono i ripieghi — la topologia, la Diagnostica, la Struttura — perché tre letture diverse
/// dello stesso fatto sono tre risposte che iniziano a divergere.
///
/// <para>⚠️ Si legge l'albero <b>proiettato</b> (<c>Sectors</c>, solo attivi), lo stesso su cui cammina la catena: un
/// fratello trovato su un albero diverso da quello della ricaduta sarebbe il fratello di un altro.</para>
/// </summary>
internal static class RipieghiMilitariQuery
{
    public static async Task<IReadOnlyDictionary<string, string>> FratelliAsync(VipiDbContext db, CancellationToken ct)
    {
        var settori = await db.Sectors.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.Callsign,
                s.Type,
                Padre = s.ParentSector != null ? s.ParentSector.Callsign : null,
                Categoria = s.Airport != null ? (AirportCategory?)s.Airport.Category : null,
            })
            .ToListAsync(ct);

        // D1: l'APP (o la sua coda DEP, che nella proiezione è un App) di uno scalo «Solo militare».
        var appMilitari = settori
            .Where(s => s.Type == SectorType.App && s.Categoria == AirportCategory.MilitaryOnly)
            .Select(s => (s.Callsign, s.Padre));

        return RipiegoMilitare.Fratelli(appMilitari, settori.Select(s => (s.Callsign, s.Padre)));
    }
}

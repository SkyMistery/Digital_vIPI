using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF + sorgente: il nome di uno scalo, prima dai nostri, poi da IVAO (carta vSOP militari §12f).
///
/// <para>⚠️ La chiamata alla sorgente è <b>best-effort e silenziosa</b>: credenziali assenti, rete giù o
/// codice sconosciuto danno tutti «non ho il nome», che è quel che il chiamante deve sapere. Far fallire
/// l'aggiunta di una riga perché IVAO non risponde vorrebbe dire che una tabella di documento si compila solo
/// quando la sorgente è in piedi.</para>
/// </summary>
public sealed class EfAirportNameLookup : IAirportNameLookup
{
    private readonly VipiDbContext _db;
    private readonly IAirportDirectory? _sorgente;

    public EfAirportNameLookup(VipiDbContext db, IAirportDirectory? sorgente = null)
    {
        _db = db;
        _sorgente = sorgente;
    }

    public async Task<IReadOnlyDictionary<string, string>> NamesAsync(
        IReadOnlyList<string> icaos, CancellationToken ct = default)
    {
        var codici = icaos.Select(i => (i ?? "").Trim().ToUpperInvariant())
            .Where(i => i.Length > 0).Distinct().ToList();
        if (codici.Count == 0) return new Dictionary<string, string>();

        return await _db.Airports.AsNoTracking()
            .Where(a => codici.Contains(a.Icao))
            .ToDictionaryAsync(a => a.Icao, a => a.Name, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AirportName>> ListAsync(CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking().OrderBy(a => a.Icao)
            .Select(a => new AirportName(a.Icao, a.Name, true)).ToListAsync(ct).ConfigureAwait(false);

    public async Task<AirportName?> FindAsync(string icao, CancellationToken ct = default)
    {
        var codice = (icao ?? "").Trim().ToUpperInvariant();
        if (codice.Length == 0) return null;

        var nostro = await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == codice).Select(a => a.Name).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (nostro is not null) return new AirportName(codice, nostro, InArchivio: true);

        if (_sorgente is null) return null;
        try
        {
            var dalla = await _sorgente.GetByIcaoAsync(codice, ct).ConfigureAwait(false);
            return dalla is null ? null : new AirportName(codice, dalla.Name, InArchivio: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidOperationException) { return null; }   // credenziali sorgente assenti: non è un errore qui
        catch (HttpRequestException) { return null; }        // sorgente irraggiungibile: idem
    }
}

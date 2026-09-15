using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Weather;

/// <summary>
/// <see cref="IStazioniMeteo"/> letto dall'anagrafica: UNA query per tutti gli scali che hanno un riferimento,
/// tenuta in memoria per <see cref="Durata"/>.
///
/// <para>⚠️ Scope PROPRIO a ogni rilettura, e non il <c>DbContext</c> di chi chiede: il provider meteo lo chiamano
/// pagine e isole dentro il loro caricamento, e un contesto condiviso col circuito è la corsa già pagata più volte
/// ([[il-tornello-e-un-altro-dbcontext]]).</para>
///
/// <para>⚠️ La scadenza c'è anche se chi scrive chiama <see cref="Invalida"/>: in produzione possono girare più
/// processi, e l'invalidazione arriva solo a quello che ha scritto. Dopo due minuti gli altri si allineano.</para>
/// </summary>
public sealed class StazioniMeteoDaAnagrafica : IStazioniMeteo
{
    /// <summary>Quanto vale la copia in memoria.</summary>
    public static readonly TimeSpan Durata = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly SemaphoreSlim _rilettura = new(1, 1);
    private volatile Copia? _copia;

    private sealed record Copia(IReadOnlyDictionary<string, string> Mappa, DateTimeOffset Scade);

    public StazioniMeteoDaAnagrafica(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<string?> RiferimentoDiAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao)) return null;
        var mappa = await MappaAsync(ct).ConfigureAwait(false);
        return mappa.TryGetValue(icao.Trim().ToUpperInvariant(), out var rif) ? rif : null;
    }

    public void Invalida() => _copia = null;

    private async Task<IReadOnlyDictionary<string, string>> MappaAsync(CancellationToken ct)
    {
        if (_copia is { } c && DateTimeOffset.UtcNow < c.Scade) return c.Mappa;

        await _rilettura.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_copia is { } c2 && DateTimeOffset.UtcNow < c2.Scade) return c2.Mappa;
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
                var righe = await db.Airports.AsNoTracking()
                    .Where(a => a.MetarStationIcao != null && a.MetarStationIcao != "")
                    .Select(a => new { a.Icao, a.MetarStationIcao })
                    .ToListAsync(ct).ConfigureAwait(false);
                var mappa = righe.ToDictionary(r => r.Icao.ToUpperInvariant(), r => r.MetarStationIcao!.ToUpperInvariant(),
                    StringComparer.OrdinalIgnoreCase);
                _copia = new Copia(mappa, DateTimeOffset.UtcNow + Durata);
                return mappa;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Il meteo non deve cadere perché l'anagrafica non risponde: si resta sull'ultima copia, o sul
                // proprio ICAO. Si riprova fra poco, non a ogni richiesta.
                var ripiego = _copia?.Mappa ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _copia = new Copia(ripiego, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20));
                return ripiego;
            }
        }
        finally
        {
            _rilettura.Release();
        }
    }
}

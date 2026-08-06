using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Hosting;

/// <summary>
/// Copia della topologia globale tenuta da parte per qualche secondo. Singleton.
///
/// <para><b>Perché.</b> <c>BuildGlobalAsync</c> rilegge tutti i settori attivi a ogni chiamata. Va bene per
/// una pagina che un umano apre; non va bene per l'endpoint del bridge Aurora, che è anonimo e interrogato in
/// polling da ogni tool desktop aperto. Su <c>atc.it.ivao.aero</c> il database è condiviso con il sito che ci
/// ospita: è il costo che si nota per primo, e lo pagherebbe qualcun altro.</para>
///
/// <para><b>Perché una TTL corta e non un invalidamento esplicito.</b> La gerarchia cambia per azione di un
/// admin in <c>/vsop/admin/sectorstructure</c> o per un import: eventi rari e non urgenti al secondo. Trenta
/// secondi di ritardo su un cambio di gerarchia non cambiano nulla per chi sta controllando; un canale di
/// invalidamento in più, invece, sarebbe una cosa da tenere allineata per sempre.</para>
///
/// <para>⚠️ Vale <b>solo</b> per il bridge: gli altri consumatori di <see cref="ITopologyProvider"/> — AoR,
/// coordinamenti, vista live — continuano a leggere il dato fresco. La cache si applica costruendo il
/// servizio del bridge con <see cref="CachedGlobalTopologyProvider"/>, non registrandola per tutti.</para>
/// </summary>
public sealed class GlobalTopologyCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Topology? _valore;
    private DateTime _scadenzaUtc;

    /// <summary>Quante volte si è servita la copia in cache invece di ricostruirla (diagnostica e test).</summary>
    public int Riusi { get; private set; }

    public async Task<Topology> GetAsync(Func<CancellationToken, Task<Topology>> costruisci, TimeSpan ttl, CancellationToken ct)
    {
        if (ttl <= TimeSpan.Zero) return await costruisci(ct);

        if (_valore is { } fresco && DateTime.UtcNow < _scadenzaUtc)
        {
            Riusi++;
            return fresco;
        }

        await _gate.WaitAsync(ct);
        try
        {
            // Ricontrollo dentro il lock: fra il primo controllo e qui, un'altra richiesta può aver già
            // ricostruito. Senza, N richieste concorrenti su cache scaduta farebbero N letture del database —
            // cioè proprio il caso che questa classe esiste per evitare.
            if (_valore is { } appena && DateTime.UtcNow < _scadenzaUtc)
            {
                Riusi++;
                return appena;
            }

            var costruito = await costruisci(ct);
            _valore = costruito;
            _scadenzaUtc = DateTime.UtcNow.Add(ttl);
            return costruito;
        }
        finally { _gate.Release(); }
    }
}

/// <summary>
/// <see cref="ITopologyProvider"/> che serve la topologia globale dalla <see cref="GlobalTopologyCache"/> e
/// delega tutto il resto al provider vero. Si usa solo nella costruzione del servizio del bridge Aurora.
/// </summary>
public sealed class CachedGlobalTopologyProvider : ITopologyProvider
{
    private readonly ITopologyProvider _interno;
    private readonly GlobalTopologyCache _cache;
    private readonly TimeSpan _ttl;

    public CachedGlobalTopologyProvider(ITopologyProvider interno, GlobalTopologyCache cache, TimeSpan ttl)
    {
        _interno = interno;
        _cache = cache;
        _ttl = ttl;
    }

    public Task<Topology> BuildGlobalAsync(CancellationToken ct = default)
        => _cache.GetAsync(_interno.BuildGlobalAsync, _ttl, ct);

    /// <summary>Non passa dalla cache: è la topologia di una singola ACC, e chi la chiede è la UI.</summary>
    public Task<Topology?> BuildByAccCodeAsync(string accCode, CancellationToken ct = default)
        => _interno.BuildByAccCodeAsync(accCode, ct);
}

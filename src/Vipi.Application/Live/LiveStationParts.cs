using Vipi.Application.Aor;
using Vipi.Application.Content;

namespace Vipi.Application.Live;

/// <summary>
/// I pezzi che ogni descrittore compone allo stesso modo: frequenze derivate, trasferimenti del mittente
/// effettivo, AoR, catena di copertura. Stanno qui una volta sola — i descrittori decidono solo COSA
/// passare (quali membri, quale documento), non COME si calcola.
/// </summary>
public sealed class LiveStationParts
{
    private readonly IAccDerivationService _acc;
    private readonly ITransferService _transfers;
    private readonly IAorService _aor;

    public LiveStationParts(IAccDerivationService acc, ITransferService transfers, IAorService aor)
    {
        _acc = acc;
        _transfers = transfers;
        _aor = aor;
    }

    /// <summary>
    /// Frequenze derivate per un insieme di membri, con un blocco SINTETICO passato alla derivazione normale.
    /// Un membro d'aeroporto (torre, ground, APP) espande l'intero catalogo del suo aeroporto — vedi
    /// <c>EfAccDerivationRepository.DeriveFrequenciesForMembersAsync</c>: vale per ogni tipo, non solo per gli APP.
    /// </summary>
    public Task<IReadOnlyList<AppFreqRow>> FrequenciesAsync(
        string accCode, IReadOnlyList<string> members, string? root = null, CancellationToken ct = default)
    {
        if (members.Count == 0) return Task.FromResult((IReadOnlyList<AppFreqRow>)Array.Empty<AppFreqRow>());
        var block = new AccBlock
        {
            Key = "live:synthetic",
            Kind = AccBlockKind.AppGroup,
            MemberCallsigns = members.ToList(),
        };
        return _acc.DeriveFrequenciesAsync(accCode, block, root, ct);
    }

    /// <summary>Tutti i CTR dell'ACC (blocco Aerovia a membri vuoti = pool implicito).</summary>
    public Task<IReadOnlyList<AppFreqRow>> AreaFrequenciesAsync(
        string accCode, string? root = null, CancellationToken ct = default) =>
        _acc.DeriveFrequenciesAsync(accCode, new AccBlock { Key = "live:area", Kind = AccBlockKind.Aerovia }, root, ct);

    /// <summary>
    /// «I miei trasferimenti, più quelli dei miei figli chiusi». È il mittente EFFETTIVO dopo la risalita della
    /// gerarchia (<see cref="ResolvedTransferFlow.ResolvedOwnerCallsign"/>), non il dominio topologico: un figlio
    /// online si tiene i propri.
    ///
    /// Si risolve con l'insieme online PIÙ la postazione guardata. Senza, consultare una posizione offline
    /// (o guardare la propria prima di collegarsi) farebbe risalire i suoi flussi a un antenato e la pagina
    /// risulterebbe vuota proprio quando serve.
    /// </summary>
    public async Task<IReadOnlyList<ResolvedTransferFlow>> TransfersAsync(
        string accCode, string callsign, IReadOnlySet<string> online, CancellationToken ct = default)
    {
        var asIfOnline = new HashSet<string>(online, StringComparer.OrdinalIgnoreCase) { callsign };
        var all = await _transfers.ResolveForAccAsync(accCode, asIfOnline, ct);
        return all.Where(r => string.Equals(r.ResolvedOwnerCallsign, callsign, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>AoR della postazione: chi copro io, chi è gestito da un subordinato online.</summary>
    public AorResult Aor(Topology topology, string callsign, IReadOnlySet<string> online) =>
        _aor.Resolve(topology, callsign, online);

    /// <summary>
    /// Catena di copertura verso l'alto: gli antenati della postazione, dal padre alla radice. Per un ground o
    /// un delivery — che di trasferimenti propri non ne hanno — è l'informazione principale: a chi passi salendo.
    /// </summary>
    public static IReadOnlyList<string> CoverageChain(Topology topology, string callsign)
    {
        var chain = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { callsign };
        var cur = callsign;
        while (topology.Parent.TryGetValue(cur, out var parent) && seen.Add(parent))
        {
            chain.Add(parent);
            cur = parent;
        }
        return chain;
    }
}

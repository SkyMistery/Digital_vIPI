using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// I pezzi che ogni descrittore compone allo stesso modo: frequenze derivate, trasferimenti del mittente
/// effettivo, AoR, catena di copertura. Stanno qui una volta sola — i descrittori decidono solo COSA
/// passare (quali membri, quale documento), non COME si calcola.
/// </summary>
public sealed class LiveStationParts
{
    private readonly IAccDerivationService _acc;
    private readonly IAgreementService _transfers;
    private readonly IAorService _aor;
    private readonly IDocumentAdminService _docs;

    public LiveStationParts(IAccDerivationService acc, IAgreementService transfers, IAorService aor,
        IDocumentAdminService docs)
    {
        _acc = acc;
        _transfers = transfers;
        _aor = aor;
        _docs = docs;
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
        string accCode, string callsign, IReadOnlySet<string> online,
        CancellationToken ct = default)
    {
        var asIfOnline = new HashSet<string>(online, StringComparer.OrdinalIgnoreCase) { callsign };
        var all = await _transfers.ResolveForAccAsync(accCode, asIfOnline, ct);

        return all
            .Where(r => string.Equals(r.ResolvedOwnerCallsign, callsign, StringComparison.OrdinalIgnoreCase))
            .Select(r => new ResolvedTransferFlow
            {
                Flow = r.Flow,
                ResolvedOwnerCallsign = r.ResolvedOwnerCallsign,
                OwnerOnline = r.OwnerOnline,
                Points = r.Points.Where(p => IsRealHandoff(p, callsign)).ToList(),
            })
            .Where(r => r.Points.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Un punto è un handoff solo se a raccoglierlo è <b>qualcun altro</b>. Se il ricevente scritto è chiuso e la
    /// sua catena di ripiego porta a chi sta guardando, quel cielo lo sta già tenendo lui: non c'è niente da
    /// passare, e il punto sparirebbe dicendo «passa a te stesso» — un'istruzione che non significa nulla e che
    /// sporca l'elenco proprio dove servono i trasferimenti veri.
    ///
    /// <para>🔴 Fino al 4 ottobre 2026 la domanda era «il ricevente è un mio <b>discendente</b> chiuso?», cioè si
    /// guardavano i soli padri. Sbagliava in due versi: WS5 che tiene ES5 per una <b>riga di ripiego</b> (ES5 non è
    /// suo discendente) si vedeva passare il traffico a sé stesso; e un punto verso una torre chiusa spariva anche
    /// quando a raccoglierlo era il suo avvicinamento aperto, che sta in mezzo. Ora decide il ricevente
    /// <b>risolto</b>, che viene dalla stessa catena dei trasferimenti.</para>
    ///
    /// Verso chiunque altro la risalita è informazione utile (chi prende il traffico adesso, fino a UNICOM) e il
    /// punto resta.
    /// </summary>
    private static bool IsRealHandoff(ResolvedTransferPoint point, string chiGuarda) =>
        !(point.IsOnline && string.Equals(point.ResolvedHandler, chiGuarda, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Chip «vista rapida aeroporto»: gli aeroporti PUBBLICATI appesi a un settore del dominio della postazione.
    /// «Pubblicato» è la vIPI civile <b>o</b> il vSOP militare (<see cref="Awos.AwosGate.Edizione"/>): fino al 30
    /// settembre 2026 si guardava la sola civile, e i campi militari senza vIPI non comparivano in nessun modo.
    /// Vale per ogni tipo che copre più di uno scalo — un'area, ma anche un avvicinamento (LIBD_CS0_APP tiene
    /// LIBD e LIBR). In coda i «delegati»: una posizione del loro ICAO è online, quindi li controlla qualcun altro.
    /// </summary>
    public async Task<IReadOnlyList<LiveAirportChip>> AirportChipsAsync(LiveStationContext ctx, CancellationToken ct = default)
    {
        var docs = await _docs.ListAsync(ct);

        var domain = ctx.Topology.DomainOf(ctx.Callsign);
        var sopra = CoverageChain(ctx.Topology, ctx.Callsign);

        return ctx.Structure.Airports
            .Where(a => a.IsPublic && Awos.AwosGate.Edizione(docs, a.Icao) is not null)
            .Where(a => a.ParentCallsign is { } pc && domain.Contains(pc))
            .Select(a =>
            {
                var chi = Presidency(ctx, a.Icao, a.ParentCallsign);
                return new LiveAirportChip(a.Icao, Delegato(chi, ctx.Callsign, sopra), chi);
            })
            .OrderByDescending(c => !c.Delegated)
            .ThenBy(c => c.Icao, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lo scalo è «delegato» se lo presiede <b>un altro</b>: una sua posizione online che non è chi guarda e non sta
    /// sopra di lui.
    ///
    /// <para>🔴 Fino al 30 settembre 2026 bastava una posizione dello scalo online (<c>Local.Count &gt; 0</c>). Ma un
    /// avvicinamento può essere lui stesso una posizione dello scalo — <c>LIMF_WW0_APP</c> ha <c>AirportIcao = LIMF</c>
    /// — e allora chi guardava da lì vedeva LIMF «delegato» a sé stesso, senza nessuno sotto (committente). Una
    /// posizione SOPRA chi guarda, poi, non gli toglie lo scalo: è lui che copre per lei.</para>
    /// </summary>
    internal static bool Delegato(AirportPresidency chi, string chiGuarda, IReadOnlyList<string> sopra) =>
        chi.Local.Any(s => !string.Equals(s.Callsign, chiGuarda, StringComparison.OrdinalIgnoreCase)
                           && !sopra.Contains(s.Callsign, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Chi presiede l'aeroporto adesso: posizioni sue online (dal gate in su) più chi copre il resto risalendo.
    ///
    /// <para>Sostituisce il vecchio «delegato = c'è un callsign online che comincia con l'ICAO», che aveva due
    /// difetti: non diceva <b>chi</b> chiamare, e contava anche l'<b>ATIS</b> — che è una frequenza, non una
    /// postazione che controlla. Qui si parte dalle posizioni note dell'aeroporto, quindi l'ATIS non entra
    /// perché non è un settore.</para>
    /// </summary>
    private static AirportPresidency Presidency(LiveStationContext ctx, string icao, string? airportParent)
    {
        var posizioni = ctx.Structure.Sectors
            .Where(s => s.IsActive && string.Equals(s.AirportIcao, icao, StringComparison.OrdinalIgnoreCase))
            .Select(s => (s.Callsign, s.Type))
            .ToList();

        var antenati = AirportPresidencyResolver.Ancestors(airportParent, ctx.Topology.Parent);
        return AirportPresidencyResolver.Resolve(posizioni, antenati, ctx.Online);
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

using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un settore che nel banco di prova si può aprire o chiudere.</summary>
/// <param name="Area">Vero per un settore d'area (CTR), falso per un avvicinamento.</param>
public sealed record BenchSector(string Callsign, string Name, bool Area);

/// <summary>Una configurazione già scritta in una vIPI dell'ACC: uno scenario pronto.</summary>
/// <param name="Block">Il blocco della vIPI che la porta (aerovia, o il titolo di un gruppo APP).</param>
public sealed record BenchPreset(string Name, string Block, IReadOnlyList<string> Open);

/// <summary>Quel che il banco di un ACC mette sul tavolo: i settori da aprire e gli scenari già scritti.</summary>
public sealed record BenchScope(string AccCode, IReadOnlyList<BenchSector> Sectors, IReadOnlyList<BenchPreset> Presets);

/// <summary>Un settore (o una sua fascia) tenuto da qualcuno.</summary>
/// <param name="Band">La fascia, scritta, quando il settore è diviso fra più mani; null se è tenuto per intero.</param>
public sealed record BenchItem(string Sector, string? Band);

/// <summary>Chi tiene che cosa in uno scenario.</summary>
/// <param name="Holder">Chi tiene; <c>null</c> = nessuno della catena è aperto.</param>
/// <param name="Outside">Chi tiene non è fra i settori dello scenario: è un ente di un altro centro, o una posizione
/// d'aeroporto — che il banco considera aperti.</param>
public sealed record BenchGroup(string? Holder, bool Outside, IReadOnlyList<BenchItem> Items);

/// <summary>L'esito di uno scenario: chi tiene cosa, e i trasferimenti dell'ACC risolti con quegli aperti.</summary>
public sealed record BenchOutcome(IReadOnlyList<BenchGroup> Coverage, IReadOnlyList<ResolvedTransferFlow> Transfers);

/// <summary>
/// Il <b>banco di prova</b> della struttura: «con questi aperti, chi tiene cosa — e i trasferimenti a chi vanno».
///
/// <para><b>Perché esiste.</b> Da quando la risposta dipende da chi è in frequenza, una struttura non si può più
/// <i>guardare</i>: si può solo aspettare di vederla sbagliare, a traffico vero. Il committente lo ha chiesto il
/// 4 ottobre 2026 dopo aver provato a far uscire giuste le due configurazioni di Milano spostando un padre: «anche
/// per questo servirebbe un meccanismo di test, per provarle subito». Carta
/// <c>docs/feature/2026-10-04-copertura-unica.md</c>.</para>
///
/// <para>⚠️ <b>Non ha un motore suo.</b> Chi tiene cosa lo dice <see cref="FallbackChain.Holders"/>, i trasferimenti
/// li risolve <see cref="IAgreementService.ResolveForAccAsync"/> — le stesse due porte della mappa AoR, della
/// tabella delle configurazioni e della vista live. Un banco che calcolasse per conto suo proverebbe un'altra cosa.</para>
/// </summary>
public interface ICoverageBenchService
{
    /// <summary>I settori dell'ACC e le configurazioni già scritte nella sua vIPI pubblicata. Null se l'ACC non c'è.</summary>
    Task<BenchScope?> ScopeAsync(string accCode, CancellationToken ct = default);

    /// <summary>
    /// Lo scenario: dei settori dell'ACC sono aperti solo quelli in <paramref name="open"/>.
    /// <para>⚠️ Tutto ciò che <b>non</b> è un settore dello scenario — i centri confinanti, le torri — si considera
    /// <b>aperto</b>: la domanda è «come si divide il mio cielo», e con i vicini chiusi ogni trasferimento verso
    /// fuori finirebbe su UNICOM, coprendo le sole differenze che si vogliono vedere.</para>
    /// </summary>
    Task<BenchOutcome> SimulateAsync(string accCode, IReadOnlyCollection<string> open, CancellationToken ct = default);
}

/// <summary>Il cuore puro del banco: nessun I/O.</summary>
public static class CoverageBench
{
    /// <summary>
    /// L'insieme «online» di uno scenario: gli aperti scelti, più tutto ciò che sta fuori dallo scenario.
    /// </summary>
    public static IReadOnlySet<string> OnlineDi(
        IEnumerable<string> tuttiISettori, IEnumerable<string> delloScenario, IEnumerable<string> aperti)
    {
        var dentro = new HashSet<string>(delloScenario, StringComparer.OrdinalIgnoreCase);
        var online = new HashSet<string>(tuttiISettori.Where(s => !dentro.Contains(s)), StringComparer.OrdinalIgnoreCase);
        foreach (var a in aperti)
            if (dentro.Contains(a)) online.Add(a);
        return online;
    }

    /// <summary>
    /// Chi tiene ogni settore <b>chiuso</b> dello scenario, raggruppato per chi tiene: prima gli aperti nell'ordine
    /// in cui sono stati scelti (anche quelli che non assorbono niente — è un'informazione), poi gli enti fuori
    /// dallo scenario, in fondo «nessuno».
    /// </summary>
    public static IReadOnlyList<BenchGroup> Copertura(
        Topology topology, IReadOnlyList<string> delloScenario, IReadOnlyList<string> aperti)
    {
        var oic = StringComparer.OrdinalIgnoreCase;
        var dentro = new HashSet<string>(delloScenario, oic);
        var scelti = aperti.Where(dentro.Contains).Distinct(oic).ToList();
        var online = OnlineDi(topology.Sectors, delloScenario, scelti);

        var perChi = new Dictionary<string, List<BenchItem>>(oic);
        var nessuno = new List<BenchItem>();
        foreach (var s in delloScenario.OrderBy(x => x, oic))
        {
            if (online.Contains(s)) continue;   // aperto: tiene sé stesso, non c'è niente da dire

            var banda = topology.Bands.TryGetValue(s, out var b) ? b : (null, null);
            var fasce = FallbackChain.Holders(s, banda.BaseFeet, banda.TopFeet, topology.Fallbacks, topology.ParentOf,
                online.Contains);
            foreach (var f in fasce)
            {
                var voce = new BenchItem(s, fasce.Count == 1 ? null : ConfigTableProjector.Fascia(f.BaseFeet, f.TopFeet));
                if (f.Holder is not { Length: > 0 } chi) { nessuno.Add(voce); continue; }
                if (!perChi.TryGetValue(chi, out var lista)) perChi[chi] = lista = new List<BenchItem>();
                lista.Add(voce);
            }
        }

        var gruppi = new List<BenchGroup>();
        foreach (var a in scelti)
            gruppi.Add(new BenchGroup(a, Outside: false,
                perChi.TryGetValue(a, out var l) ? l : (IReadOnlyList<BenchItem>)Array.Empty<BenchItem>()));
        foreach (var (chi, lista) in perChi.Where(kv => !dentro.Contains(kv.Key)).OrderBy(kv => kv.Key, oic))
            gruppi.Add(new BenchGroup(chi, Outside: true, lista));
        if (nessuno.Count > 0) gruppi.Add(new BenchGroup(null, Outside: false, nessuno));
        return gruppi;
    }
}

/// <inheritdoc cref="ICoverageBenchService"/>
public sealed class CoverageBenchService : ICoverageBenchService
{
    private readonly IAccDerivationRepository _repo;
    private readonly ITopologyProvider _topology;
    private readonly IAccDocumentService _documenti;
    private readonly IAgreementService _accordi;
    private readonly IEditAuthorizationService _authz;

    public CoverageBenchService(IAccDerivationRepository repo, ITopologyProvider topology,
        IAccDocumentService documenti, IAgreementService accordi, IEditAuthorizationService authz)
    {
        _repo = repo;
        _topology = topology;
        _documenti = documenti;
        _accordi = accordi;
        _authz = authz;
    }

    public async Task<BenchScope?> ScopeAsync(string accCode, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var settori = await SettoriAsync(accCode, ct);
        if (settori.Count == 0) return null;

        // ⚠️ Le configurazioni si leggono dalla vIPI PUBBLICATA, e non dalla bozza: la porta della bozza
        // (`LoadForEditAsync`) garantisce il documento, cioè può scrivere — e un banco di prova non scrive. Chi sta
        // ancora scrivendo una configurazione la prova nell'editor della vIPI, dove la tabella si deriva dal vivo.
        var presets = new List<BenchPreset>();
        if (await _documenti.LoadForViewAsync(accCode, ct) is { } vipi)
            foreach (var blocco in vipi.Blocks)
                foreach (var cfg in blocco.Block.Configurations)
                    if (cfg.Open.Count > 0)
                        presets.Add(new BenchPreset(cfg.Name, blocco.Block.Title, cfg.OpenCallsigns.ToList()));

        return new BenchScope(accCode.Trim().ToUpperInvariant(), settori, presets);
    }

    public async Task<BenchOutcome> SimulateAsync(string accCode, IReadOnlyCollection<string> open,
        CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var scenario = (await SettoriAsync(accCode, ct)).Select(s => s.Callsign).ToList();
        var topo = await _topology.BuildGlobalAsync(ct);
        var aperti = open.ToList();

        var copertura = CoverageBench.Copertura(topo, scenario, aperti);
        var online = CoverageBench.OnlineDi(topo.Sectors, scenario, aperti);
        return new BenchOutcome(copertura, await _accordi.ResolveForAccAsync(accCode, online, ct));
    }

    /// <summary>I settori d'area e gli avvicinamenti dell'ACC, senza doppioni: è l'insieme che si può aprire.</summary>
    private async Task<IReadOnlyList<BenchSector>> SettoriAsync(string accCode, CancellationToken ct)
    {
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settori = new List<BenchSector>();
        foreach (var s in await _repo.ListCtrSectorsAsync(accCode, ct))
            if (visti.Add(s.Callsign)) settori.Add(new BenchSector(s.Callsign, s.Name, Area: true));
        foreach (var s in await _repo.ListAppSectorsAsync(accCode, ct))
            if (visti.Add(s.Callsign)) settori.Add(new BenchSector(s.Callsign, s.Name, Area: false));
        return settori;
    }
}

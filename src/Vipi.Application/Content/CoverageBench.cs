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

/// <summary>Una configurazione possibile dichiarata in Struttura: uno scenario pronto <b>per il suo gruppo</b>.</summary>
public sealed record BenchPreset(string Name, IReadOnlyList<string> Open);

/// <summary>
/// Un gruppo di settori con le sue configurazioni possibili: i settori d'area dell'ACC, oppure un ente con le sue
/// posizioni.
/// <para>⚠️ Una configurazione parla dei <b>soli settori del suo gruppo</b>: sceglierla non apre e non chiude
/// nient'altro (<see cref="CoverageBench.ConPreset"/>). Fino al 12 ottobre 2026 i tasti erano un elenco solo, e
/// «Conf 2» dell'area chiudeva tutti gli avvicinamenti — uno scenario che non esiste.</para>
/// </summary>
/// <param name="Name">Il codice dell'ACC per i settori d'area, o il nome dell'ente.</param>
/// <param name="Sectors">I settori del gruppo che il banco sa aprire e chiudere.</param>
public sealed record BenchPresetGroup(string Name, IReadOnlyList<string> Sectors, IReadOnlyList<BenchPreset> Presets);

/// <summary>Quel che il banco di un ACC mette sul tavolo: i settori da aprire e gli scenari già scritti, gruppo per gruppo.</summary>
public sealed record BenchScope(string AccCode, IReadOnlyList<BenchSector> Sectors, IReadOnlyList<BenchPresetGroup> Groups);

/// <summary>Che cosa succede a una clausola in uno scenario.</summary>
public enum BenchTransferState
{
    /// <summary>Cede chi è scritto, riceve chi è scritto.</summary>
    AsWritten,

    /// <summary>Uno dei due capi è chiuso e al suo posto c'è un altro: il trasferimento avviene, fra altre mani.</summary>
    PickedUp,

    /// <summary>Chi cede e chi riceve sono la stessa postazione: quel cielo lo tiene già lei, non c'è niente da passare.
    /// È lo stesso caso che la vista live toglie dall'elenco (<c>LiveStationParts.IsRealHandoff</c>).</summary>
    ToItself,

    /// <summary>Nessuno cede (il cielo del cedente non lo tiene nessuno) o nessuno riceve (UNICOM).</summary>
    Uncovered,
}

/// <summary>
/// Una riga della tabella dei trasferimenti del banco: <b>una clausola</b>, con chi la cede e chi la riceve davvero.
/// </summary>
/// <param name="Airports">Gli scali della sezione; vuoto per i sorvoli.</param>
/// <param name="Sender">Chi cede davvero; <c>null</c> = nessuno.</param>
/// <param name="Points">I punti della clausola che hanno questo esito, nell'ordine in cui sono scritti.</param>
/// <param name="Receiver">Chi riceve davvero; <c>null</c> = nessuno (UNICOM).</param>
/// <param name="ByCoverage">A decidere il ricevente è stato un rinvio «copertura del punto».</param>
public sealed record BenchTransferRow(
    TransferFlowKind Kind, IReadOnlyList<string> Airports, string WrittenSender, string? Sender,
    IReadOnlyList<string> Points, string Level, string? Condition, string WrittenReceiver, string? Receiver,
    bool ByCoverage, BenchTransferState State)
{
    public bool SenderDiffers => !string.Equals(Sender, WrittenSender, StringComparison.OrdinalIgnoreCase);
    public bool ReceiverDiffers => !string.Equals(Receiver, WrittenReceiver, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Un settore (o una sua fascia) tenuto da qualcuno.</summary>
/// <param name="Band">La fascia, scritta, quando il settore è diviso fra più mani; null se è tenuto per intero.</param>
public sealed record BenchItem(string Sector, string? Band);

/// <summary>Chi tiene che cosa in uno scenario.</summary>
/// <param name="Holder">Chi tiene; <c>null</c> = nessuno della catena è aperto.</param>
/// <param name="Outside">Chi tiene non è fra i settori dello scenario: è un ente di un altro centro, o una posizione
/// d'aeroporto — che il banco considera aperti.</param>
public sealed record BenchGroup(string? Holder, bool Outside, IReadOnlyList<BenchItem> Items);

/// <summary>L'esito di uno scenario: chi tiene cosa, e i trasferimenti dell'ACC risolti con quegli aperti.</summary>
/// <param name="NonPreviste">I gruppi i cui aperti non sono una delle configurazioni possibili dichiarate. Il banco
/// lo <b>dice</b> e risponde lo stesso: serve anche a vedere che cosa succede quando qualcuno lo fa davvero.</param>
public sealed record BenchOutcome(
    IReadOnlyList<BenchGroup> Coverage, IReadOnlyList<ResolvedTransferFlow> Transfers,
    IReadOnlyList<GruppoFuoriElenco> NonPreviste);

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
    /// <summary>I settori dell'ACC e le configurazioni possibili dei suoi gruppi. Null se l'ACC non c'è.</summary>
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

    /// <summary>
    /// Gli aperti dopo aver scelto una configurazione di un gruppo: cambiano i <b>soli settori di quel gruppo</b>,
    /// gli altri restano come sono.
    /// <para>È quel che fa di uno scenario una <b>combinazione</b> — una configurazione dell'area, una per ogni
    /// ente — invece che un tasto solo: con i 12 avvicinamenti di Milano chiusi, 51 degli 80 punti che «cambiavano
    /// mano» erano WS2 ed ES2 che passavano il traffico a sé stessi.</para>
    /// </summary>
    public static IReadOnlyList<string> ConPreset(
        IEnumerable<string> aperti, IEnumerable<string> delGruppo, IEnumerable<string> delPreset)
    {
        var oic = StringComparer.OrdinalIgnoreCase;
        var gruppo = new HashSet<string>(delGruppo, oic);
        var esito = aperti.Where(a => !gruppo.Contains(a)).Distinct(oic).ToList();
        // ⚠️ Un nome che non è più del gruppo (un elenco rimasto indietro rispetto alla struttura) non si apre:
        // aprirlo da qui vorrebbe dire toccare un settore di cui questa configurazione non può parlare.
        foreach (var p in delPreset)
            if (gruppo.Contains(p) && !esito.Contains(p, oic)) esito.Add(p);
        return esito;
    }

    /// <summary>
    /// I flussi risolti, <b>una riga per clausola</b>: sette STAR scritte nella stessa clausola sono una riga, e lo
    /// stesso vale per gli scali di una sezione che ne nomina più d'uno. La clausola si divide solo quando i suoi
    /// punti hanno esiti diversi (due cedenti, o un rinvio che risponde punto per punto).
    ///
    /// <para><b>Perché non per punto.</b> Il banco mostrava un punto per riga e, di suo, i soli punti che
    /// cambiavano mano: una clausola che funziona come è scritta non compariva, e chi cercava «il trasferimento
    /// WS2 → Genova» non lo trovava (committente, 12 ottobre 2026). Ora compaiono tutte, con l'esito.</para>
    /// </summary>
    public static IReadOnlyList<BenchTransferRow> Trasferimenti(IReadOnlyList<ResolvedTransferFlow> flussi)
    {
        var oic = StringComparer.OrdinalIgnoreCase;
        var righe = new List<(BenchTransferRow Riga, List<string> Scali, List<string> Punti)>();
        var perChiave = new Dictionary<(int Clausola, TransferFlowKind Kind, string Scritto, string Cede, string A,
            string Riceve, string Livello, bool Rinvio), int>();
        var senzaClausola = 0;

        foreach (var f in flussi)
        {
            var cede = f.OwnerOnline ? f.ResolvedOwnerCallsign : null;
            foreach (var p in f.Points)
            {
                var scritto = p.Point.NextSectorCallsign ?? "";
                var riceve = p.IsOnline ? p.ResolvedHandler : null;
                var rinvio = p.Coverage is { Outcome: CoverageFallbackOutcome.Resolved } c
                             && oic.Equals(c.TargetCallsign, p.ResolvedHandler);
                var livello = p.Point.LevelText ?? "";

                // Un punto senza clausola (righe di prima degli accordi) non si riunisce con niente.
                var chiave = (p.Point.ClauseId ?? --senzaClausola, f.Flow.Kind, f.Flow.OwningSectorCallsign.ToUpperInvariant(),
                    (cede ?? "").ToUpperInvariant(), scritto.ToUpperInvariant(), (riceve ?? "").ToUpperInvariant(),
                    livello, rinvio);
                if (!perChiave.TryGetValue(chiave, out var i))
                {
                    perChiave[chiave] = i = righe.Count;
                    var scali = new List<string>();
                    var punti = new List<string>();
                    righe.Add((new BenchTransferRow(f.Flow.Kind, scali, f.Flow.OwningSectorCallsign, cede, punti, livello,
                        string.IsNullOrWhiteSpace(p.Point.ConditionDisplay) ? null : p.Point.ConditionDisplay,
                        scritto, riceve, rinvio, StatoDi(f.Flow.OwningSectorCallsign, cede, scritto, riceve)), scali, punti));
                }

                var (_, scaliDellaRiga, puntiDellaRiga) = righe[i];
                if (f.Flow.AirportIcao is { Length: > 0 } icao && !scaliDellaRiga.Contains(icao, oic)) scaliDellaRiga.Add(icao);
                if (!puntiDellaRiga.Contains(p.Point.Cop, oic)) puntiDellaRiga.Add(p.Point.Cop);
            }
        }

        return righe.Select(r => r.Riga).ToList();
    }

    /// <summary>
    /// L'esito di una clausola. ⚠️ L'ordine dei controlli conta: «nessuno» viene prima di tutto, e «a sé stesso»
    /// prima di «come scritto» — che non può più essere, se i due capi sono la stessa postazione.
    /// </summary>
    private static BenchTransferState StatoDi(string cedenteScritto, string? cede, string riceventeScritto, string? riceve)
    {
        var oic = StringComparer.OrdinalIgnoreCase;
        if (cede is null || riceve is null) return BenchTransferState.Uncovered;
        if (oic.Equals(cede, riceve)) return BenchTransferState.ToItself;
        return oic.Equals(cede, cedenteScritto) && oic.Equals(riceve, riceventeScritto)
            ? BenchTransferState.AsWritten
            : BenchTransferState.PickedUp;
    }
}

/// <inheritdoc cref="ICoverageBenchService"/>
public sealed class CoverageBenchService : ICoverageBenchService
{
    private readonly IAccDerivationRepository _repo;
    private readonly ITopologyProvider _topology;
    private readonly ISectorConfigurationService _configurazioni;
    private readonly IAgreementService _accordi;
    private readonly IEditAuthorizationService _authz;

    public CoverageBenchService(IAccDerivationRepository repo, ITopologyProvider topology,
        ISectorConfigurationService configurazioni, IAgreementService accordi, IEditAuthorizationService authz)
    {
        _repo = repo;
        _topology = topology;
        _configurazioni = configurazioni;
        _accordi = accordi;
        _authz = authz;
    }

    public async Task<BenchScope?> ScopeAsync(string accCode, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var settori = await SettoriAsync(accCode, ct);
        if (settori.Count == 0) return null;

        // Gli scenari pronti sono le configurazioni possibili dichiarate in Struttura, gruppo per gruppo. Fino
        // all'8 ottobre 2026 venivano dalla vIPI pubblicata, che ne era l'unica copia (carta
        // 2026-10-08-configurazioni-possibili): ora il documento le legge dallo stesso posto.
        // ⚠️ Restano divise PER GRUPPO: una configurazione dell'area non dice niente degli avvicinamenti, e
        // viceversa. Un gruppo senza configurazioni non esce: i suoi settori si aprono e si chiudono a mano.
        var delBanco = new HashSet<string>(settori.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        var gruppi = new List<BenchPresetGroup>();
        foreach (var gruppo in await _configurazioni.GruppiAsync(accCode, ct))
        {
            var presets = gruppo.Configurazioni.Where(cfg => cfg.Open.Count > 0)
                .Select(cfg => new BenchPreset(cfg.Name, cfg.OpenCallsigns.ToList())).ToList();
            if (presets.Count == 0) continue;
            // Un ente può avere per posizione una torre, che il banco non apre e non chiude: resta fuori.
            gruppi.Add(new BenchPresetGroup(gruppo.Nome,
                gruppo.Settori.Select(s => s.Callsign).Where(delBanco.Contains).ToList(), presets));
        }

        return new BenchScope(accCode.Trim().ToUpperInvariant(), settori, gruppi);
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
        // ⚠️ «Non prevista» si chiede dei soli aperti SCELTI, non di `online`: quello contiene anche tutto ciò
        // che sta fuori dallo scenario (gli altri centri, aperti per costruzione), e ogni loro gruppo con un
        // elenco risulterebbe fuori elenco.
        var scelti = aperti.Where(a => scenario.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
        return new BenchOutcome(copertura, await _accordi.ResolveForAccAsync(accCode, online, ct),
            topo.Configurazioni.NonPreviste(scelti));
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

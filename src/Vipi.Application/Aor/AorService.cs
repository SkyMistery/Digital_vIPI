using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Aor;

/// <summary>Una fascia del cielo di un settore e chi la tiene, nella risoluzione AoR.</summary>
/// <param name="BaseFeet">Piede in piedi (incluso). Null = quello del settore.</param>
/// <param name="TopFeet">Tetto in piedi (escluso). Null = quello del settore.</param>
/// <param name="Owner">Il callsign che la tiene.</param>
public readonly record struct AorHolding(int? BaseFeet, int? TopFeet, string Owner);

/// <summary>Risultato della risoluzione AoR: ownership e stato di ogni settore del dominio di P.</summary>
public sealed class AorResult
{
    /// <summary>
    /// sectorKey → callsign che lo possiede, data la configurazione online.
    ///
    /// <para>⚠️ È la risposta <b>a una voce sola</b>, per chi una divisione non la sa disegnare. Quando il
    /// settore è diviso per quota fra più mani (<see cref="Holdings"/> con più di una fascia) qui c'è P se P ne
    /// tiene almeno una — è cielo di cui risponde — altrimenti chi tiene la fascia più bassa.</para>
    /// </summary>
    public required IReadOnlyDictionary<string, string> Ownership { get; init; }

    /// <summary>sectorKey → stato (Covered = lo copro io P, Online = lo gestisce un subordinato online).</summary>
    public required IReadOnlyDictionary<string, SectorState> State { get; init; }

    /// <summary>
    /// sectorKey → chi lo tiene, <b>fascia per fascia</b>, dal basso. Quasi sempre una fascia sola; più d'una
    /// quando una riga di ripiego vale solo per una parte della banda del settore.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AorHolding>> Holdings { get; init; }
        = new Dictionary<string, IReadOnlyList<AorHolding>>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Risolve l'ownership e lo stato dei settori (la parte più critica del sistema). ADR-0001 D5.
/// Puro: nessun I/O, deterministico, cacheable. Sorgente dei test = SPEC_Logica_AoR §5 (S1–S10).
/// </summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public interface IAorService
{
    /// <summary>Calcola ownership e stato dei settori nel dominio di <paramref name="p"/> dato l'insieme online.</summary>
    AorResult Resolve(Topology topology, string p, IReadOnlySet<string> online);
}

/// <inheritdoc cref="IAorService"/>
public sealed class AorService : IAorService
{
    public AorResult Resolve(Topology topology, string p, IReadOnlySet<string> online)
    {
        var domain = topology.DomainOf(p);

        // Settore == posizione: i settori del dominio sono i settori stessi di Dom(P).
        var sectors = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);

        // 1. Ogni settore possiede sé stesso; chi non è online cede il suo cielo lungo la CATENA DI RIPIEGO — le
        //    righe dichiarate che valgono a quella quota, poi il padre — e non più lungo i soli padri: è la
        //    stessa strada che fanno i trasferimenti (FallbackChain), così la mappa, la tabella delle
        //    configurazioni e la vista live non possono dire tre cose diverse. P raccoglie quando la catena
        //    arriva a lui, e quando non raccoglie nessuno (top-down completo).
        //    ⚠️ Fino al 6 ottobre 2026 qui in mezzo si applicavano le «regole di unificazione»: un secondo modo di
        //    dire chi tiene chi, senza editor e senza una riga in archivio. Tolte: lo dice la catena.
        bool Tiene(string c) => c.Equals(p, StringComparison.OrdinalIgnoreCase) || online.Contains(c);

        var ownership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var holdings = new Dictionary<string, IReadOnlyList<AorHolding>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sectors)
        {
            var banda = topology.Bands.TryGetValue(s, out var b) ? b : (null, null);
            var fasce = Unisci(FallbackChain
                .Holders(s, banda.BaseFeet, banda.TopFeet, topology.Fallbacks, topology.ParentOf, Tiene)
                .Select(h => new AorHolding(h.BaseFeet, h.TopFeet, h.Holder ?? p)));

            if (fasce.Count == 0) fasce = new[] { new AorHolding(banda.BaseFeet, banda.TopFeet, p) };

            holdings[s] = fasce;
            ownership[s] = fasce.Any(f => f.Owner.Equals(p, StringComparison.OrdinalIgnoreCase)) ? p : fasce[0].Owner;
        }

        // 2. Stato: Online se gestito da un subordinato online diverso da P, altrimenti Covered.
        var state = new Dictionary<string, SectorState>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sectors)
        {
            var owner = ownership[s];
            state[s] = online.Contains(owner) && !owner.Equals(p, StringComparison.OrdinalIgnoreCase)
                ? SectorState.Online
                : SectorState.Covered;
        }

        return new AorResult { Ownership = ownership, State = state, Holdings = holdings };
    }

    /// <summary>Due fasce contigue nella stessa mano sono una fascia: «nessuno» è diventato P dopo la catena.</summary>
    private static IReadOnlyList<AorHolding> Unisci(IEnumerable<AorHolding> fasce)
    {
        var esito = new List<AorHolding>();
        foreach (var f in fasce)
        {
            if (esito.Count > 0 && esito[^1].Owner.Equals(f.Owner, StringComparison.OrdinalIgnoreCase))
                esito[^1] = esito[^1] with { TopFeet = f.TopFeet };
            else
                esito.Add(f);
        }
        return esito;
    }
}

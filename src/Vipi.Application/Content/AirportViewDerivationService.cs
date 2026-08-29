using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Risolve le sezioni derivate della vIPI d'aeroporto per la VISTA (doc 10 §3d, esteso dalla carta 2026-08-26):
/// se <paramref name="useFrozen"/> e c'è una release effettiva con la sezione congelata, legge l'output frozen
/// per chiave di sezione (chiave di release = ICAO); altrimenti deriva live dal profilo strutturato.
/// <para>
/// La cattura salva SOLO le sezioni Frozen → per una sezione Live il reader ritorna <c>null</c> e si ricade su
/// live (nessun controllo di <see cref="RenderMode"/> qui). ⚠️ È anche ciò che rende morbido il passaggio: gli
/// aeroporti con una release già effettiva non hanno un payload congelato per le chiavi nuove, quindi
/// continuano a leggersi live finché non si ripubblica.
/// </para>
/// <para>
/// Il meteo non passa di qui: è l'unica sezione <see cref="SectionCatalog.IsAlwaysLive"/>, e la pagina lo chiede
/// al provider meteo. Un METAR dentro uno snapshot di release sarebbe meteo scaduto spacciato per attuale.
/// </para>
/// </summary>
public interface IAirportViewDerivationService
{
    /// <summary>Tutte le sezioni derivate dell'aeroporto in un colpo solo.</summary>
    /// <param name="edizione">
    /// ⚠️ <b>Da quale RELEASE leggere il congelato</b>, non da quali tabelle derivare: le tabelle sono le
    /// stesse — meteo, piste, quote e frequenze di uno scalo sono quelle, qualunque documento le mostri — ma
    /// gli <b>snapshot</b> sono due, perché due sono i documenti che parlano di quel campo e ognuno si
    /// pubblica per conto suo (carta vSOP militari §4: cicli AIRAC indipendenti).
    /// <para>
    /// ⚠️ Passare <see cref="ReleaseTargetType.Airport"/> dalla pagina militare non è «un default innocuo»:
    /// sui campi MISTI (Pisa) il vSOP militare mostrerebbe la fotografia della release CIVILE, timbrata al
    /// ciclo civile, e ripubblicare il militare non la cambierebbe; sui campi SOLO militari (Rivolto,
    /// Aviano, Ghedi, Decimomannu) non esiste release civile, si ricadrebbe sempre live, e il congelamento
    /// sarebbe un no-op invisibile.
    /// </para>
    /// <para>
    /// ⚠️ Per questo il parametro è <b>obbligatorio e senza default</b>: un default sarebbe «civile», cioè
    /// la risposta giusta per il chiamante che c'era e sbagliata IN SILENZIO per quello nuovo — la forma
    /// esatta del difetto che si sta correggendo.
    /// </para>
    /// </param>
    Task<AirportDerived> ResolveForViewAsync(string icao, bool useFrozen, ReleaseTargetType edizione,
        CancellationToken ct = default);

    /// <summary>Le sole SID. Resta a parte perché la pagina le ri-filtra per pista scelta dal lettore.</summary>
    Task<AirportSidView> ResolveSidsForViewAsync(string icao, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportViewDerivationService"/>
public sealed class AirportViewDerivationService : IAirportViewDerivationService
{
    private readonly IAirportProfileReader _repo;
    private readonly IAirportSectorService _sectors;
    private readonly IAirportSidDerivationService _sids;
    private readonly IFrozenSectionReader _frozen;

    public AirportViewDerivationService(IAirportProfileReader repo, IAirportSectorService sectors,
        IAirportSidDerivationService sids, IFrozenSectionReader frozen)
    {
        _repo = repo;
        _sectors = sectors;
        _sids = sids;
        _frozen = frozen;
    }

    public async Task<AirportDerived> ResolveForViewAsync(string icao, bool useFrozen, ReleaseTargetType edizione,
        CancellationToken ct = default)
    {
        icao = Norm(icao);

        // Lo snapshot una volta sola (doc 14 §3c): erano cinque letture dello stesso payload, contando le SID.
        // ⚠️ La chiave di release è l'ICAO per tutte e due le edizioni; a distinguerle è il TIPO — vedi il
        // commento sul parametro nell'interfaccia.
        var frozen = useFrozen ? await _frozen.LoadAsync(edizione, icao, ct) : FrozenSections.Empty;

        var rules = frozen.Get<AirportRulesView>("runwayrules");
        var transition = frozen.Get<AirportTransitionView>("transition");
        var freqs = frozen.Get<AirportFreqView>("frequencies");
        var runways = frozen.Get<AirportRunwaysView>("runways");

        // Il profilo si carica una volta sola, e solo se serve davvero: con tutte e quattro le sezioni congelate
        // la pagina pubblica non tocca le tabelle dell'aeroporto.
        AirportData? data = null;
        if (rules is null || transition is null || runways is null || freqs is null)
            data = await _repo.LoadAsync(icao, ct);

        rules ??= AirportSectionProjection.Rules(data);
        transition ??= AirportSectionProjection.Transition(data);
        runways ??= AirportSectionProjection.Runways(data);
        freqs ??= AirportSectionProjection.Frequencies(
            await _sectors.ListByAirportAsync(icao, ct), data?.Links);

        // Le SID dallo STESSO lotto: chiamare qui il metodo pubblico rileggerebbe lo snapshot una sesta volta.
        return new AirportDerived(rules, transition, freqs, runways,
            frozen.Get<AirportSidView>("sids") ?? await _sids.DeriveAsync(icao, ct));
    }

    public async Task<AirportSidView> ResolveSidsForViewAsync(string icao, bool useFrozen, CancellationToken ct = default)
    {
        icao = Norm(icao);
        var frozen = useFrozen ? await _frozen.LoadAsync(ReleaseTargetType.Airport, icao, ct) : FrozenSections.Empty;
        return frozen.Get<AirportSidView>("sids") ?? await _sids.DeriveAsync(icao, ct);
    }

    private static string Norm(string? icao) => (icao ?? "").Trim().ToUpperInvariant();
}

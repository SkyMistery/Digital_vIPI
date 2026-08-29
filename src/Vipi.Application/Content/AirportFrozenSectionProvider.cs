using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen delle sezioni derivate della vIPI d'aeroporto (doc 10 §3e, esteso dalla carta 2026-08-26).
/// Chiave di release = ICAO.
/// <para>
/// Fino a quella carta l'unica sezione congelabile era <c>sids</c>, perché tutto il resto del documento era
/// <b>già cotto</b> nei blocchi. Ora le sezioni fisse sono ancore senza corpo e si derivano a view-time: se non
/// si congelassero qui, pubblicare non fisserebbe più niente e la pagina pubblica cambierebbe da sola a ogni
/// modifica del profilo.
/// </para>
/// <para>
/// ⚠️ Il meteo non si cattura mai (<see cref="SectionCatalog.IsAlwaysLive"/>): un METAR dentro uno snapshot di
/// release sarebbe meteo scaduto spacciato per attuale. La sezione nasce Live e l'editor non le offre il toggle,
/// ma la guardia sta anche qui — chi arrivasse a metterla Frozen a mano non congelerebbe comunque il tempo.
/// </para>
/// </summary>
public sealed class AirportFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IAirportProfileReader _repo;
    private readonly IAirportSectorService _sectors;
    private readonly IAirportSidDerivationService _sids;

    /// <param name="type">
    /// La famiglia che questo provider serve. ⚠️ <b>Sono DUE</b>: la vIPI civile d'aeroporto e il vSOP
    /// <b>militare</b> dello stesso scalo, che deriva le stesse tre tabelle (<c>frequencies</c>,
    /// <c>runways</c>, <c>transition</c>) perché parlano dello stesso campo. Il <i>motore</i> è uno solo —
    /// riscriverlo sarebbe due proiezioni che col tempo divergono — ma le <b>catture</b> devono restare due,
    /// perché due sono le release: un vSOP militare si pubblica con un suo progressivo e un suo ciclo AIRAC,
    /// e deve fotografare le tabelle in quel momento, non nel momento in cui è stata pubblicata la civile.
    /// <para>
    /// ⚠️ Non è la stessa cosa di «riusare la chiave» (carta vSOP militari §2): lì si condivide il renderer,
    /// qui si terrebbe lo <i>snapshot</i>. Chi registra questo provider una volta sola lascia l'edizione
    /// militare senza cattura, e il registry risponde <c>Empty</c> in silenzio.
    /// </para>
    /// </param>
    public AirportFrozenSectionProvider(IAirportProfileReader repo, IAirportSectorService sectors,
        IAirportSidDerivationService sids, ReleaseTargetType type = ReleaseTargetType.Airport)
    {
        _repo = repo;
        _sectors = sectors;
        _sids = sids;
        Type = type;
    }

    public ReleaseTargetType Type { get; }

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        var sezioni = FrozenSectionScan.FrozenDerived(doc)
            .Where(s => !SectionCatalog.IsAlwaysLive(s.SectionKey))
            .ToList();
        if (sezioni.Count == 0) return result;

        // Il profilo una volta sola: le quattro sezioni di tabella escono tutte da qui.
        var chiavi = sezioni.Select(s => s.SectionKey.ToLowerInvariant()).ToHashSet();
        var data = chiavi.Overlaps(new[] { "runwayrules", "transition", "runways", "frequencies" })
            ? await _repo.LoadAsync(key, ct)
            : null;

        foreach (var s in sezioni)
        {
            object? vm = s.SectionKey.ToLowerInvariant() switch
            {
                "runwayrules" => AirportSectionProjection.Rules(data),
                "transition" => AirportSectionProjection.Transition(data),
                "runways" => AirportSectionProjection.Runways(data),
                "frequencies" => AirportSectionProjection.Frequencies(await _sectors.ListByAirportAsync(key, ct), data?.Links),
                "sids" => await _sids.DeriveAsync(key, ct),
                _ => null,
            };
            if (vm is not null) result[s.Id] = JsonSerializer.Serialize(vm);
        }
        return result;
    }
}

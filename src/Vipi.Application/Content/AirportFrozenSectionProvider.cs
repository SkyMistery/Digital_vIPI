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

    /// <summary>L'anagrafica delle radioassistenze: serve solo all'edizione militare, ed è l'unica sezione
    /// congelata i cui valori stanno FUORI dal documento e fuori dal profilo dell'aeroporto.</summary>
    private readonly INavaidCatalog? _navaids;

    /// <summary>L'archivio degli scali, per i nomi degli aeroporti alternati. Militare come sopra.</summary>
    private readonly IAirportNameLookup? _aeroporti;

    /// <summary>
    /// Il ciclo AIRAC per cui si sta congelando, che <c>ReleaseService</c> apre attorno alla cattura.
    /// <para>
    /// ⚠️ Serve alle SID e per la stessa ragione delle shape: una SID importata compare solo dal ciclo
    /// <b>successivo</b> al prelievo, quindi «quali SID ci sono» è una domanda che ha risposte diverse a
    /// cicli diversi. Congelando al ciclo di OGGI una release programmata al 2608 ci si scriveva dentro la
    /// tabella di adesso — e la release usciva con meno SID di quante ne avrà.
    /// </para>
    /// <para>Nullo fuori dal congelamento: lì si guarda al ciclo corrente, che è il comportamento di sempre.</para>
    /// </summary>
    private readonly ShapeReleaseContext? _cicloDiRilascio;

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
        IAirportSidDerivationService sids, ReleaseTargetType type = ReleaseTargetType.Airport,
        INavaidCatalog? navaids = null, IAirportNameLookup? aeroporti = null,
        ShapeReleaseContext? cicloDiRilascio = null)
    {
        _repo = repo;
        _sectors = sectors;
        _sids = sids;
        _navaids = navaids;
        _aeroporti = aeroporti;
        _cicloDiRilascio = cicloDiRilascio;
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
                // ⚠️ Al ciclo della RELEASE, non a quello di oggi (vedi _cicloDiRilascio).
                "sids" => await _sids.DeriveAsync(key, _cicloDiRilascio?.Cycle, ct),
                // ⚠️ L'unica sezione congelata i cui valori NON stanno nel documento né nel profilo dello
                // scalo: il documento dice quali radioassistenze cita, l'anagrafica dice quanto valgono. Senza
                // questa cattura una frequenza corretta oggi cambierebbe da sola un SOP pubblicato al ciclo
                // scorso — che è esattamente ciò che pubblicare deve impedire.
                "navaids" => _navaids is null
                    ? null
                    : await _navaids.GetManyAsync(MilNavaidsPayload.Leggi(SectionPayload.Read(s.Blocks)), ct),
                "diversion" => _navaids is null
                    ? null
                    : await MilDiversionResolver.ResolveAsync(
                        MilDiversionPayload.Leggi(SectionPayload.Read(s.Blocks)), _navaids, _aeroporti, ct),
                _ => null,
            };
            if (vm is not null) result[s.Id] = JsonSerializer.Serialize(vm);
        }
        return result;
    }
}

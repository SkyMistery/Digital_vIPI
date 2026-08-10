using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// Come si rende una postazione nella vista live. NON è <see cref="SectorType"/>: raggruppa i tipi che si
/// mostrano allo stesso modo (una torre e un ground vedono lo stesso aeroporto, con corredo diverso).
/// </summary>
public enum LiveStationType { Area, Approach, Tower, Ground }

/// <summary>Ingredienti che il registry passa al descrittore: tutto già risolto, nessun I/O per il descrittore
/// che non gli serva davvero.</summary>
public sealed record LiveStationContext(
    string Callsign,
    SectorRow Sector,
    AccInfo Acc,
    StructureData Structure,
    Topology Topology,
    IReadOnlySet<string> Online);

/// <summary>
/// Chip «vista rapida aeroporto» dei tipi d'area: ICAO, se è controllato da qualcun altro online, e
/// <b>chi</b> lo presiede adesso. Il «chi» non è decorazione: senza, la pagina dice che l'aeroporto è
/// coperto ma non da chi, che è l'unica informazione per cui la si guarda.
/// </summary>
public sealed record LiveAirportChip(string Icao, bool Delegated, AirportPresidency Presidency);

/// <summary>Gruppo-APP del documento reso come sezione a sé (coperto o delegato = collasso morbido).</summary>
public sealed record LiveGroup(AccBlock Block, IReadOnlyList<AppFreqRow> Freqs, bool Delegated);

/// <summary>
/// Riferimento al documento esteso della postazione. Porta gli ingredienti, non l'URL: la rotta la compone la UI
/// col registry <c>IDocKindRoutes</c> (doc 09 §3b), che è dove vive la conoscenza delle rotte.
/// </summary>
public sealed record LiveDocRef(ManagedDocKind Kind, string AccCode, string? Scope);

/// <summary>
/// Modello uniforme reso dalla pagina live, qualunque sia il tipo di ente. È il punto dell'unificazione:
/// la pagina non sa più se sta rendendo un CTR, un APP o una torre.
/// </summary>
public sealed record LiveView
{
    public required string Callsign { get; init; }
    public required string Title { get; init; }
    public required string AccCode { get; init; }
    public required LiveStationType Type { get; init; }

    /// <summary>Aeroporto della postazione (torri, ground, APP d'aeroporto): pannello fisso, non chip.</summary>
    public string? AirportIcao { get; init; }

    /// <summary>Aeroporti raggiungibili dai tipi d'area: chip di vista rapida.</summary>
    public IReadOnlyList<LiveAirportChip> AirportChips { get; init; } = Array.Empty<LiveAirportChip>();

    public IReadOnlyList<AppFreqRow> Frequencies { get; init; } = Array.Empty<AppFreqRow>();
    public IReadOnlyList<LiveGroup> Groups { get; init; } = Array.Empty<LiveGroup>();
    public IReadOnlyList<ResolvedTransferFlow> Transfers { get; init; } = Array.Empty<ResolvedTransferFlow>();
    public AorResult? Aor { get; init; }

    /// <summary>Catena di copertura verso l'alto (chi ti assorbe se chiudi / a chi passi salendo). Per i tipi
    /// con pochi trasferimenti propri (ground, delivery) è l'informazione principale della pagina.</summary>
    public IReadOnlyList<string> CoverageChain { get; init; } = Array.Empty<string>();

    /// <summary>Radice dell'albero ACC di riferimento: serve ai componenti che rendono le sezioni del
    /// documento (chiave di render dell'AoR). Null per i tipi che non hanno un albero documentale.</summary>
    public string? TreeRoot { get; init; }

    public LiveDocRef? ExtendedDoc { get; init; }

    /// <summary>Il documento di riferimento non è pubblicato: la vista resta valida (deriva dai cataloghi),
    /// ma va detto. Vedi memoria live-view-design.</summary>
    public bool NoDocument { get; init; }
}

/// <summary>Esito della composizione: la postazione può non esistere in nessuna struttura.</summary>
public sealed record LiveViewResult(LiveView? View, string RequestedCallsign)
{
    public bool Found => View is not null;
    public static LiveViewResult NotFound(string callsign) => new(null, callsign);
}

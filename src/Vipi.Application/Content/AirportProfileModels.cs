using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli del profilo strutturato dell'aeroporto (sorgente da cui si rigenera il documento).
// Gli stessi record fungono da input per il salvataggio: Id == 0 = riga nuova.

/// <summary>Riga QNH→Transition Level. Range numerico (estremi null = aperto).</summary>
public sealed record TlRow(int Id, int? QnhFrom, int? QnhTo, string Level);

/// <summary>Estremità di pista: Ident/Length/Bearing da IVAO, il resto editoriale.</summary>
public sealed record RunwayRow(int Id, string Ident, int? LengthM, int? Bearing,
    string? ToraM, string? LdaM, string? AppProcedures, string? Patterns, string? Circling);

/// <summary>Regola di scelta pista: piste DEP/ARR preferenziali + soglie (coda/traverso/superficie) + filtro temporale opzionale.</summary>
public sealed record RunwayRuleRow(int Id, string DepRunways, string ArrRunways, string? Name,
    int MaxTailwindKt, int? MaxCrosswindKt, RunwaySurface Surface, string? Note,
    int? TimeFromLocalMin = null, int? TimeToLocalMin = null, int? DaysOfWeekMask = null, DateParity DateParity = DateParity.Any,
    int? DateFromMonthDay = null, int? DateToMonthDay = null);

/// <summary>Riga SID.</summary>
public sealed record SidRow(int Id, string? Runway, string Fix, string Name, string? Transition,
    string? InitialClimb, string? Type, string? Cat, string? Wtc, string? Condition);

/// <summary>Frequenza linkata (riferimento vivo): valore risolto da Sector.DefaultFrequency al momento del load/rebuild.</summary>
public sealed record FrequencyLinkRow(int Id, int SourceSectorId, string Label, string Callsign, string FrequencyMhz);

/// <summary>Sezione editoriale libera (titolo + corpo): colonna destra del documento / sotto le SID su schermi stretti.</summary>
public sealed record ExtraSectionRow(int Id, string Title, string? Body);

/// <summary>Settore selezionabile dal picker (qualunque settore con frequenza nel DB).</summary>
public sealed record LinkableFrequencyRow(int SectorId, string? Icao, string Callsign, string FrequencyMhz);

/// <summary>Profilo completo dell'aeroporto per editor e viewer.</summary>
public sealed class AirportProfileData
{
    public required int AirportId { get; init; }
    public required string Icao { get; init; }
    public required string Name { get; init; }
    public required string AccCode { get; init; }
    public int? TransitionAltitudeFt { get; init; }
    public required IReadOnlyList<TlRow> TransitionLevels { get; init; }
    public required IReadOnlyList<RunwayRow> Runways { get; init; }
    public required IReadOnlyList<RunwayRuleRow> Rules { get; init; }
    public required IReadOnlyList<SidRow> Sids { get; init; }
    public required IReadOnlyList<FrequencyLinkRow> Links { get; init; }
    public required IReadOnlyList<ExtraSectionRow> ExtraSections { get; init; }
}

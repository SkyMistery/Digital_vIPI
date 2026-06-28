using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli del profilo strutturato dell'aeroporto (sorgente da cui si rigenera il documento).
// Gli stessi record fungono da input per il salvataggio: Id == 0 = riga nuova.

/// <summary>Riga QNH→Transition Level. Range numerico (estremi null = aperto).</summary>
public sealed record TlRow(int Id, int? QnhFrom, int? QnhTo, string Level);

/// <summary>Estremità di pista: Ident/Length/Bearing da IVAO, il resto editoriale.</summary>
public sealed record RunwayRow(int Id, string Ident, int? LengthM, int? Bearing,
    string? ToraM, string? LdaM, string? AppProcedures, string? Patterns, string? Circling);

/// <summary>Regola di scelta pista: condizione (vento + pioggia/neve) → piste DEP/ARR.</summary>
public sealed record RunwayRuleRow(int Id, int? WindDirFrom, int? WindDirTo, int? WindSpeedMin, int? WindSpeedMax,
    bool? Rain, bool? Snow, string DepRunways, string ArrRunways, string? Note,
    int? TimeFromUtcMin = null, int? TimeToUtcMin = null, int? DaysOfWeekMask = null, DateParity DateParity = DateParity.Any);

/// <summary>Riga SID.</summary>
public sealed record SidRow(int Id, string? Runway, string Fix, string Name, string? Transition,
    string? InitialClimb, string? Type, string? Cat, string? Wtc, string? Condition);

/// <summary>Frequenza propria dell'aeroporto (da un settore DEL/GND/TWR/APP). Sola lettura nell'editor.</summary>
public sealed record OwnFrequencyRow(SectorType Type, string Name, string Callsign, string FrequencyMhz, bool IsPrimary);

/// <summary>Frequenza linkata (riferimento vivo): valore risolto dalla Frequency sorgente al momento del load/rebuild.</summary>
public sealed record FrequencyLinkRow(int Id, int SourceFrequencyId, string Label, string Callsign, string FrequencyMhz);

/// <summary>Frequenza selezionabile dal picker (qualunque Frequency nel DB).</summary>
public sealed record LinkableFrequencyRow(int FrequencyId, string? Icao, string SectorCallsign,
    string Label, string Callsign, string FrequencyMhz);

/// <summary>Profilo completo dell'aeroporto per editor e viewer.</summary>
public sealed class AirportProfileData
{
    public required int AirportId { get; init; }
    public required string Icao { get; init; }
    public required string Name { get; init; }
    public required string AccCode { get; init; }
    public int? TransitionAltitudeFt { get; init; }
    public string? AtisFrequency { get; init; }
    public required IReadOnlyList<TlRow> TransitionLevels { get; init; }
    public required IReadOnlyList<RunwayRow> Runways { get; init; }
    public required IReadOnlyList<RunwayRuleRow> Rules { get; init; }
    public required IReadOnlyList<SidRow> Sids { get; init; }
    public required IReadOnlyList<OwnFrequencyRow> OwnFrequencies { get; init; }
    public required IReadOnlyList<FrequencyLinkRow> Links { get; init; }
}

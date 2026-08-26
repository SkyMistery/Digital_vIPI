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

/// <summary>Riga SID. I campi da <paramref name="IsImported"/> in poi riguardano l'import da sectorfile.</summary>
public sealed record SidRow(int Id, string? Runway, string Fix, string Name, string? Transition,
    string? InitialClimb, string? Type, string? Cat, string? Wtc, string? Condition,
    bool IsImported = false, int? Priority = null, string? StableKey = null,
    string? SourceAiracCycle = null, bool ForcePublished = false, bool NeedsFixReview = false,
    bool InitialClimbByApp = false)
{
    /// <summary>La riga è pubblica al ciclo AIRAC indicato? Manuali sempre; importate solo se forzate o dal ciclo successivo al prelievo.</summary>
    public bool IsPublicAt(string currentCycle, Vipi.Domain.Services.IAiracService airac)
    {
        if (!IsImported || ForcePublished) return true;
        if (string.IsNullOrWhiteSpace(SourceAiracCycle)) return true;   // sicurezza: senza ciclo sorgente non nascondere
        try { return airac.EffectiveUtcForCycle(currentCycle) > airac.EffectiveUtcForCycle(SourceAiracCycle); }
        catch (ArgumentException) { return true; }
    }
}

/// <summary>Riga SID importata dal sectorfile (input del merge). Priority/ForcePublished sono riapplicati dal repo per StableKey.</summary>
public sealed record ImportedSid(string? Runway, string Fix, string Name, string? Transition,
    string? Type, string StableKey, bool NeedsFixReview);

/// <summary>Frequenza linkata (riferimento vivo): valore risolto da Sector.DefaultFrequency al momento del load/rebuild.</summary>
public sealed record FrequencyLinkRow(int Id, int SourceSectorId, string Label, string Callsign, string FrequencyMhz);

/// <summary>Settore selezionabile dal picker (qualunque settore con frequenza nel DB).</summary>
public sealed record LinkableFrequencyRow(int SectorId, string? Icao, string Callsign, string FrequencyMhz, string? AtcCallsign = null);

/// <summary>Profilo completo dell'aeroporto per editor e viewer.</summary>
public sealed class AirportData
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
}

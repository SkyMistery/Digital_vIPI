using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli del profilo strutturato dell'aeroporto (sorgente da cui si rigenera il documento).
// Gli stessi record fungono da input per il salvataggio: Id == 0 = riga nuova.

/// <summary>Riga QNH→Transition Level. Range numerico (estremi null = aperto).</summary>
public sealed record TlRow(int Id, int? QnhFrom, int? QnhTo, string Level);

/// <summary>Estremità di pista: Ident/Length/Bearing e le coordinate della SOGLIA da IVAO, il resto editoriale.</summary>
/// <param name="ThresholdLat">
/// Coordinate della soglia in gradi decimali, e la sua elevazione: le manda IVAO con le piste.
/// <para>⚠️ Sono <b>in coda e con un default</b> perché nessun editor le scrive — chi costruisce una riga a
/// mano non deve doversene ricordare. E chi salva non deve <b>poterle perdere</b>: la conservazione vera sta
/// in <c>EfAirportRepository.SaveRunwaysAsync</c>, che le riporta per ident qualunque cosa arrivi.</para>
/// </param>
public sealed record RunwayRow(int Id, string Ident, int? LengthM, int? Bearing,
    string? ToraM, string? LdaM, string? AppProcedures, string? Patterns, string? Circling,
    double? ThresholdLat = null, double? ThresholdLon = null, int? ThresholdElevationFt = null);

/// <summary>
/// Che cosa ha fatto il merge da IVAO alle piste di UN aeroporto.
/// </summary>
/// <param name="OrphansWithData">
/// Le piste che l'archivio ha e la sorgente non nomina più, e che portano lavoro editoriale (TORA, LDA,
/// procedure, circuiti, circling).
/// <para>⚠️ Il merge <b>non le tocca</b>. Una pista scritta a mano è lavoro di una persona, e l'assenza
/// dalla sorgente è un'informazione, non un permesso di cancellare: le orfane VUOTE se ne vanno da sole,
/// queste restano e si nominano, perché le tolga chi sa dove spostare i dati. È la stessa regola per cui gli
/// upsert «puliti» delle aree regolamentate azzerarono 83 poligoni.</para>
/// </param>
public sealed record RunwayMergeOutcome(
    int Added, int Updated, int RemovedEmpty, IReadOnlyList<string> OrphansWithData)
{
    /// <summary>Nessun cambio: la sorgente non ha mandato piste (esclusa dalla policy, o non ha risposto).</summary>
    public static RunwayMergeOutcome None { get; } = new(0, 0, 0, Array.Empty<string>());
}

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
    /// <summary>
    /// La riga è pubblica al ciclo AIRAC indicato? Manuali sempre; importate se forzate o se il ciclo
    /// indicato ha <b>raggiunto</b> quello da cui la riga è in vigore.
    ///
    /// <para>⚠️ <b><c>SourceAiracCycle</c> è «il ciclo DAL QUALE la riga vale», e il confronto è
    /// <c>&gt;=</c></b> — dal 2 settembre 2026, carta §AW2. Prima era «il ciclo in cui l'abbiamo
    /// prelevata» con un <c>&gt;</c>, cioè un buffer di un ciclo <b>indovinato</b>: la sorgente il proprio
    /// ciclo lo <b>dichiara</b> (<c>CHANGELOG/&lt;ciclo&gt;.txt</c> nel sectorfile Aurora) e nessuno glielo
    /// chiedeva. Dove non lo dichiara il buffer resta, ma lo aggiunge <see cref="SidStampCycle"/> scrivendo
    /// direttamente il ciclo d'entrata: qui non c'è più niente da indovinare.</para>
    ///
    /// <para>⚠️ Senza ciclo scritto <b>non si nasconde</b>: una riga senza timbro è un dato che non sappiamo
    /// collocare, e nessuna SID è peggio di una SID in anticipo.</para>
    /// </summary>
    public bool IsPublicAt(string currentCycle, Vipi.Domain.Services.IAiracService airac)
    {
        if (!IsImported || ForcePublished) return true;
        if (string.IsNullOrWhiteSpace(SourceAiracCycle)) return true;   // sicurezza: senza ciclo sorgente non nascondere
        try { return airac.EffectiveUtcForCycle(currentCycle) >= airac.EffectiveUtcForCycle(SourceAiracCycle); }
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

/// <summary>
/// L'anagrafica militare di uno scalo più i due legami documentali, letti INSIEME: è tutto ciò che serve a
/// decidere quale edizione si può creare (carta vSOP militari §5-bis).
///
/// <para>⚠️ Le due domande si fanno in una lettura sola perché le loro risposte si <b>condizionano</b>: un
/// campo solo militare non ha una vIPI civile da creare, e un campo misto non ha un vSOP militare finché la
/// civile non c'è. Chiederle separatamente vorrebbe dire poterle vedere in due istanti diversi, e decidere
/// su una coppia che non è mai esistita.</para>
/// </summary>
/// <param name="HasMilitaryPresence">Dalla sorgente: c'è una base sul campo. ⚠️ Vero anche su Linate,
/// Pisa, Ciampino: non vuol dire «aeroporto militare».</param>
/// <param name="IsMilitaryOnly">Scelta di un amministratore: nessun traffico civile.</param>
/// <param name="DocumentId">La vIPI CIVILE, se esiste (anche solo in bozza).</param>
/// <param name="MilDocumentId">Il vSOP MILITARE, se esiste (anche solo in bozza).</param>
public sealed record AirportMilitaryState(
    bool HasMilitaryPresence, bool IsMilitaryOnly, int? DocumentId, int? MilDocumentId);

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

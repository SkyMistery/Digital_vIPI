namespace Vipi.Domain.Entities;

/// <summary>
/// Release AIRAC di un documento: snapshot delle SOLE scelte editoriali (<see cref="PayloadJson"/>) pubblicato con
/// un ciclo AIRAC di rilascio. Modello unico per tutti i tipi (vLOA/vIPI ACC/APP/Aeroporto): lo stato live resta la
/// bozza sempre aperta, la release ne congela una fotografia. Il pubblico vede la release con
/// <see cref="ReleaseEffectiveUtc"/> &lt;= adesso più recente; le future sono schedulate. I dati derivati
/// (poligoni/frequenze/gerarchia/trasferimenti) NON sono nello snapshot: si renderizzano sempre coi cataloghi correnti.
/// </summary>
public class DocRelease
{
    public int Id { get; set; }
    public ReleaseTargetType TargetType { get; set; }

    /// <summary>Chiave del bersaglio: vLOA = documentId; AccVipi = "{accCode}|{rootCallsign}"; App = callsign settore; Airport = ICAO.</summary>
    public string TargetKey { get; set; } = default!;

    public int VersionNumber { get; set; }                 // progressivo per (TargetType, TargetKey)
    public string ReleaseAiracCycle { get; set; } = default!;   // "YYNN"
    public DateTime ReleaseEffectiveUtc { get; set; }      // chiave di selezione ordinabile (data efficace del ciclo)
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Scheduled;

    /// <summary>Snapshot serializzato delle scelte editoriali (shape dipende dal <see cref="TargetType"/>).</summary>
    public string PayloadJson { get; set; } = default!;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? Note { get; set; }
}

namespace Vipi.Application.Content;

// Modelli della vIPI ACC: documento a blocchi (Aerovia/CTR + gruppi APP). Riusa i record editoriali/derivati
// dell'APP (AppSeparationRow, AppCustomSection, AppFreqOrderOverride, AppFreqRow, AppCoordination, AppAorPolygon).

/// <summary>Tipo di blocco della vIPI ACC.</summary>
public enum AccBlockKind { Aerovia, AppGroup }

/// <summary>Un settore APERTO in una configurazione: callsign + Center Point/Range (input manuale staff).</summary>
public sealed class AccConfigOpen
{
    public string Callsign { get; set; } = "";
    public string? CenterPoint { get; set; }
    public string? Range { get; set; }
}

/// <summary>Configurazione operativa = insieme di settori APERTI; il sistema deriva l'accorpamento (chi copre chi).</summary>
public sealed class AccConfiguration
{
    public string Key { get; set; } = "";                          // stabile, es. "cfg:{guid}"
    public string Name { get; set; } = "";                         // es. "2 settori (NE/SW)"
    public List<AccConfigOpen> Open { get; set; } = new();         // settori aperti + CP/Range per settore

    public IEnumerable<string> OpenCallsigns => Open.Select(o => o.Callsign);
}

/// <summary>Area regolamentata (editoriale strutturato): nome + dettaglio markdown. Es. «R-64 · CUNEO».</summary>
public sealed class AccRegulatedArea
{
    public string Name { get; set; } = "";
    public string? Detail { get; set; }
}

/// <summary>
/// Un blocco della vIPI ACC: Aerovia (settori CTR, pool implicito) o gruppo-APP (settori APP scelti).
/// Contiene lo stato editoriale (sezioni/override) + le configurazioni che guidano l'AoR.
/// </summary>
public sealed class AccBlock
{
    public string Key { get; set; } = "";                          // "aerovia" | "grp:{guid}"
    public AccBlockKind Kind { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Settori membri (callsign). Aerovia: vuoto = tutti i CTR dell'ACC. Gruppo-APP: gli APP scelti.</summary>
    public List<string> MemberCallsigns { get; set; } = new();

    public List<string> SectionOrder { get; set; } = new();
    public List<string> HiddenSections { get; set; } = new();
    public List<AppCustomSection> CustomSections { get; set; } = new();
    public List<AccConfiguration> Configurations { get; set; } = new();

    // editoriale
    public List<AppSeparationRow> Separations { get; set; } = new();
    public string? VfrJson { get; set; }
    public List<AccRegulatedArea> RegulatedAreas { get; set; } = new();          // legacy (deprecato): sostituito da Regulated
    public RegulatedSelection Regulated { get; set; } = new();                    // #8: aree speciali (proprio ACC auto/manuale + extra altri-ACC)

    // frequenze
    public List<AppFreqOrderOverride> FreqOrder { get; set; } = new();
    public List<string> FreqLinkCallsigns { get; set; } = new();   // link extra per callsign (riferimento vivo)
}

/// <summary>Dati completi della vIPI ACC per editor e viewer: identità + blocchi.</summary>
public sealed class AccVipiData
{
    public required string AccCode { get; init; }
    public required string AccName { get; init; }
    public required List<AccBlock> Blocks { get; init; }
}

/// <summary>Settore selezionabile (per picker membri gruppo / settori aperti config): callsign + nome.</summary>
public sealed record AccSectorPick(string Callsign, string Name);

/// <summary>Radice di un albero di settori CTR dell'ACC (una vIPI per albero). Callsign + nome del CTR radice.</summary>
public sealed record AccTreeRoot(string Callsign, string Name);

/// <summary>Area speciale selezionabile (picker editor). <see cref="CenterId"/> = ACC di appartenenza (per il picker
/// cross-ACC delle aree extra).</summary>
public sealed record SpecialAreaPick(string IvaoId, string Name, string? Type, int? MinimumAlt, int? MaximumAlt, string CenterId);

/// <summary>Selezione delle aree regolamentate di un blocco vIPI ACC. Le aree del <b>proprio</b> ACC sono in
/// <see cref="OwnAuto"/> (tutte, dinamiche: seguono gli import) finché lo staff non passa a manuale scegliendo un
/// sottoinsieme in <see cref="OwnIds"/>. <see cref="ExtraIds"/> sono aree di <b>altri</b> ACC aggiunte a mano
/// (indipendenti dal modo auto/manuale delle proprie). Il modo auto vale solo per il blocco Aerovia.</summary>
public sealed class RegulatedSelection
{
    public bool OwnAuto { get; set; } = true;
    public List<string> OwnIds { get; set; } = new();
    public List<string> ExtraIds { get; set; } = new();
}

/// <summary>Area speciale grezza dal DB (per la proiezione nel viewer). Shape = JSON grezzo.</summary>
public sealed record SpecialAreaDetail(
    string IvaoId, string Name, string? Type, string? Description, string? ActivationDetails,
    int? MinimumAlt, int? MaximumAlt, string? RegionMapPolygon);

/// <summary>Area speciale attaccata a una vIPI, risolta per il viewer: metadati + shape proiettata (null = assente).</summary>
public sealed record AccSpecialAreaView(
    string IvaoId, string Name, string? Type, string? Description, string? ActivationDetails,
    int? MinimumAlt, int? MaximumAlt, AppAorPolygon? Shape);

/// <summary>Flussi verso un aeroporto (foglia dell'albero): arrivi + partenze separati.</summary>
public sealed record AccAirportFlows(string AirportLabel, IReadOnlyList<AppCoordRow> Arrivals, IReadOnlyList<AppCoordRow> Departures);

/// <summary>Flussi senza aeroporto (sorvoli/VFR/altro) sotto un ACC, raggruppati per etichetta di tipo.</summary>
public sealed record AccExtraFlows(string KindLabel, IReadOnlyList<AppCoordRow> Rows);

/// <summary>Aeroporti di un ACC (secondo livello): gli aeroporti a cui il settore trasferisce (arrivi/partenze)
/// + i flussi senza aeroporto (<see cref="Extras"/>: sorvoli/VFR/altro).</summary>
public sealed record AccAccAirports(string AccLabel, IReadOnlyList<AccAirportFlows> Airports, IReadOnlyList<AccExtraFlows> Extras);

/// <summary>Coordinamenti di un settore del blocco (primo livello, es. «NE»): ACC → Aeroporto → Arrivi/Partenze.</summary>
public sealed record AccSectorApps(string SectorLabel, IReadOnlyList<AccAccAirports> Accs);

/// <summary>Coordinamenti derivati di un blocco: un unico albero Settore → ACC → Aeroporto → Arrivi/Partenze.</summary>
public sealed class AccCoordination
{
    public required IReadOnlyList<AccSectorApps> Sectors { get; init; }

    public static AccCoordination Empty { get; } = new() { Sectors = Array.Empty<AccSectorApps>() };
}

/// <summary>AoR di una configurazione: nome config + i poligoni (unione) dei suoi settori aperti.</summary>
public sealed record AccConfigAor(string ConfigKey, string ConfigName, IReadOnlyList<AppAorPolygon> Polygons);

/// <summary>AoR di un singolo settore del blocco: callsign + nome + colore + poligoni. Anello toggleabile in mappa.</summary>
public sealed record AccSectorAor(string Callsign, string Name, string Color, IReadOnlyList<AppAorPolygon> Polygons);

/// <summary>Selezione di configurazione per la mappa: quali settori accendere.</summary>
public sealed record AccConfigSelection(string Key, string Name, IReadOnlyList<string> OpenCallsigns);

/// <summary>Riga tabella accorpamento derivata: settore unificato (aperto) + settori assorbiti + CP/Range manuali.
/// Settore unificato e assorbiti sono espressi come <b>callsign</b> (es. <c>LIRR_NE_CTR</c>), non come nome.</summary>
public sealed record AccConfigTableRow(
    string UnifiedCallsign, IReadOnlyList<string> Absorbed, string? CenterPoint, string? Range);

/// <summary>Tabella accorpamento di una configurazione (derivata via AorService).</summary>
public sealed record AccConfigTableView(string ConfigKey, string ConfigName, IReadOnlyList<AccConfigTableRow> Rows);

/// <summary>Vista AoR del blocco: settori (anelli toggleabili) + configurazioni selezionabili. Una sola mappa.</summary>
public sealed record AccAorView(IReadOnlyList<AccSectorAor> Sectors, IReadOnlyList<AccConfigSelection> Configs)
{
    public static AccAorView Empty { get; } = new(Array.Empty<AccSectorAor>(), Array.Empty<AccConfigSelection>());
}

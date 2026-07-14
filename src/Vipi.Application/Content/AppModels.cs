using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli del profilo dell'APP non remotizzato (standalone). Parti editoriali (salvate) + derivate (live).
// Mirror, in chiave APP, di AirportModels.

/// <summary>Riga della sezione Separazioni (editoriale): colonne fisse verticale + laterale (es. «1000 ft» / «3 NM»);
/// dalla 2ª riga in poi un free-text <see cref="Applicability"/> specifica quando si applica (la 1ª è la predefinita).</summary>
public sealed record AppSeparationRow(string Vertical, string Lateral, string? Applicability = null);

/// <summary>Override d'ordine di una singola frequenza, per callsign (FreqOrderJson).</summary>
public sealed record AppFreqOrderOverride(string Callsign, int Order);

/// <summary>
/// Riga della sezione Frequenze (DERIVATA): dal sottoalbero (settori + ATIS dal catalogo) o da un link extra.
/// <see cref="IsPrimary"/> = frequenza principale dell'APP (★). <see cref="IsLink"/> = frequenza linkata.
/// </summary>
public sealed record AppFreqRow(
    int? SourceSectorId, string Name, string Callsign, string FrequencyMhz,
    string Position, bool IsPrimary, bool IsLink);

/// <summary>Riga di un gruppo di coordinamento (DERIVATA dai trasferimenti del settore APP).
/// I membri init opzionali portano il contesto per comporre la frase di coordinamento (una per riga CoP).</summary>
public sealed record AppCoordRow(string Cop, string Level, string Next, TransferFlowKind Kind)
{
    /// <summary>Callsign del settore che cede il traffico (mittente).</summary>
    public string? OwnerCallsign { get; init; }
    /// <summary>ICAO dell'aeroporto destinazione del flusso (null = nessuna frase).</summary>
    public string? AirportIcao { get; init; }
    /// <summary>Vincolo di livello: guida lo stato «in discesa/stabile/in salita».</summary>
    public LevelConstraint? Constraint { get; init; }
    /// <summary>Frase di coordinamento già composta (null/vuota = non mostrata).</summary>
    public string? Sentence { get; init; }
}

/// <summary>Gruppo di coordinamenti: la chiave è un callsign ente (ACC/torre) o un'etichetta di tipo (sorvoli).</summary>
public sealed record AppCoordGroup(string TargetCallsign, IReadOnlyList<AppCoordRow> Rows);

/// <summary>Coordinamenti derivati di un APP: verso gli ACC (partenze+arrivi), verso le torri (solo arrivi),
/// e i flussi senza aeroporto (<see cref="Overflights"/>: sorvoli/VFR/altro, per etichetta di tipo).</summary>
public sealed class AppCoordination
{
    public required IReadOnlyList<AppCoordGroup> TowardAcc { get; init; }
    public required IReadOnlyList<AppCoordGroup> TowardTowers { get; init; }
    public IReadOnlyList<AppCoordGroup> Overflights { get; init; } = Array.Empty<AppCoordGroup>();

    public static AppCoordination Empty { get; } =
        new() { TowardAcc = Array.Empty<AppCoordGroup>(), TowardTowers = Array.Empty<AppCoordGroup>() };
}

/// <summary>Poligono AoR: punti reali [lat,lon] (per overlay su mappa) + path SVG di fallback + bounding box/centro. null = nessuna shape.</summary>
public sealed record AppAorPolygon(
    string ViewBox, string Path, IReadOnlyList<double[]> Points,
    double MinLat, double MinLon, double MaxLat, double MaxLon, double CenterLat, double CenterLon);

/// <summary>Riga della tabella VFR (trasferimento VFR APP↔torre): situazione → procedura.</summary>
public sealed record AppVfrRow(string Situation, string Procedure);

/// <summary>Contenuto editoriale della sezione VFR: prosa introduttiva (markdown) + tabella opzionale.</summary>
public sealed record AppVfrContent(string? Intro, IReadOnlyList<AppVfrRow> Rows)
{
    public static AppVfrContent Empty { get; } = new(null, Array.Empty<AppVfrRow>());
}

/// <summary>Tipo di blocco di una sezione custom.</summary>
public enum AppCustomBlockType { Prose, Table }

/// <summary>Blocco di una sezione custom: prosa (markdown in <see cref="Text"/>) o tabella (<see cref="Columns"/>+<see cref="Rows"/>).</summary>
public sealed record AppCustomBlock(
    AppCustomBlockType Type, string? Text,
    IReadOnlyList<string>? Columns, IReadOnlyList<IReadOnlyList<string>>? Rows);

/// <summary>Sezione custom (libera): chiave stabile + titolo + blocchi prosa/tabella. Riordinata insieme alle fisse.</summary>
public sealed record AppCustomSection(string Key, string Title, IReadOnlyList<AppCustomBlock> Blocks);


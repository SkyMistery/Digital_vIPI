namespace Vipi.Domain.Services;

/// <summary>
/// Una riga di catalogo come serve all'albero di copertura, e nient'altro.
/// </summary>
/// <param name="Callsign">Chiave naturale della riga (<c>ComposePosition</c>).</param>
/// <param name="ParentCallsign">Padre <b>scritto</b>. Null = da derivare (posizione d'aeroporto) o radice.</param>
/// <param name="AirportIcao">Scalo di appartenenza; <c>null</c> per un settore d'area, che scaletta non ha.</param>
/// <param name="Type">Tipo di posizione, per il gradino della scaletta.</param>
/// <param name="IsHidden">Nascosta dall'admin: non entra nel gruppo da cui la scaletta sceglie.</param>
public readonly record struct HierarchyCatalogRow(
    string Callsign, string? ParentCallsign, string? AirportIcao, SectorType Type, bool IsHidden);

/// <summary>
/// L'albero di copertura <b>EFFETTIVO</b>: per ogni nodo interno, il padre scritto se c'è, altrimenti quello
/// derivato dalla scaletta d'aeroporto (<see cref="AirportPositionLadder"/>).
///
/// <para><b>Perché esiste.</b> Questo è l'albero che leggono davvero la proiezione dei settori, la ricaduta
/// dei trasferimenti e la pagina Struttura — mentre la guardia anti-ciclo, fino al 31 agosto 2026, guardava
/// quello dei soli padri <b>scritti</b>. Due alberi diversi, e il difetto stava nella differenza: in
/// produzione <c>LIMF_WW0_APP</c> era nipote di sé stesso e nessun controllo poteva vederlo. Un solo posto
/// che sappia costruirlo è ciò che impedisce alla differenza di riformarsi.</para>
///
/// <para>Puro e deterministico, nessun I/O. Confronti sui callsign sempre senza distinzione di maiuscole.</para>
/// </summary>
public static class EffectiveHierarchy
{
    /// <summary>
    /// callsign → padre effettivo (<c>null</c> = radice, o nessun padre derivabile).
    /// </summary>
    /// <param name="rows">Tutti i nodi interni: settori d'area (<c>AirportIcao</c> null) e posizioni d'aeroporto.</param>
    /// <param name="airportParentByIcao">ICAO → padre dello <b>scalo</b>, l'uscita in fondo alla scaletta.</param>
    public static Dictionary<string, string?> ParentMap(
        IReadOnlyList<HierarchyCatalogRow> rows,
        IReadOnlyDictionary<string, string?> airportParentByIcao)
    {
        // Le posizioni visibili di ogni scalo: è il gruppo fra cui la scaletta sceglie. Una nascosta non è un
        // padre possibile, quindi non deve nemmeno partecipare alla scelta.
        var scalette = rows
            .Where(r => r.AirportIcao is not null && !r.IsHidden)
            .GroupBy(r => r.AirportIcao!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LadderPosition>)g
                    .Select(r => new LadderPosition(r.Callsign, r.Type, r.ParentCallsign)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var mappa = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (r.ParentCallsign is not null || r.AirportIcao is null)
            {
                mappa[r.Callsign] = r.ParentCallsign;
                continue;
            }

            mappa[r.Callsign] = AirportPositionLadder.ParentOf(
                new LadderPosition(r.Callsign, r.Type, null),
                scalette.GetValueOrDefault(r.AirportIcao) ?? Array.Empty<LadderPosition>(),
                airportParentByIcao.GetValueOrDefault(r.AirportIcao),
                r.AirportIcao);
        }
        return mappa;
    }

    /// <summary>Suffisso di catalogo → tipo, per il solo calcolo della scaletta.</summary>
    public static SectorType TypeOfPosition(string? position) => (position?.Trim().ToUpperInvariant()) switch
    {
        "DEL" => SectorType.Del,
        "GND" => SectorType.Gnd,
        "TWR" => SectorType.Twr,
        _ => SectorType.App,
    };

    /// <summary>L'ATIS non è una posizione di controllo: non è un nodo dell'albero, né un padre possibile.</summary>
    public static bool IsAtis(string? position) =>
        string.Equals(position?.Trim(), "ATIS", StringComparison.OrdinalIgnoreCase);
}

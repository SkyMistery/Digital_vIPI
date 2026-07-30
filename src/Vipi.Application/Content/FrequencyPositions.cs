using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Vocabolario condiviso delle posizioni-frequenza: ordine di presentazione, nome leggibile e mappatura dal tipo
/// di settore. Gemello di <see cref="FrequencyOrdering"/> (che applica gli override per-callsign), qui c'è la
/// semantica di base della posizione.
/// <para>
/// Era triplicato nei repository di derivazione (ACC, APP, aeroporto) e <b>aveva già divergiato</b>: la copia
/// dell'aeroporto usava <c>position ?? "—"</c> invece di <c>IsNullOrWhiteSpace(position) ? "—" : position</c>,
/// quindi una posizione fatta di soli spazi rendeva una cella bianca nel documento aeroporto e un trattino in
/// quelli ACC/APP. Vince la variante difensiva: nessuna cella vuota.
/// </para>
/// </summary>
public static class FrequencyPositions
{
    /// <summary>
    /// Ordine di presentazione canonico. Le posizioni non elencate finiscono in coda (99).
    /// Include <c>CTR</c>: le copie di APP e aeroporto lo omettevano, facendo cadere le righe CTR nel gruppo
    /// delle posizioni ignote invece che in fondo alle note (differenza osservabile solo se in quegli elenchi
    /// convivono un CTR e una posizione ignota).
    /// </summary>
    private static readonly string[] Order = { "ATIS", "DEL", "GND", "TWR", "APP", "DEP", "CTR" };

    /// <summary>Indice d'ordine della posizione; 99 se non riconosciuta.</summary>
    public static int OrderOf(string? position)
    {
        var i = Array.IndexOf(Order, Normalize(position));
        return i < 0 ? 99 : i;
    }

    /// <summary>Nome leggibile della posizione. Posizione assente, vuota o di soli spazi → em-dash.</summary>
    public static string NameOf(string? position) => Normalize(position) switch
    {
        "ATIS" => "ATIS",
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        _ => string.IsNullOrWhiteSpace(position) ? "—" : position!,
    };

    /// <summary>Sigla di posizione dal tipo di settore (le due varianti di torre collassano su TWR).</summary>
    public static string FromSectorType(SectorType t) => t switch
    {
        SectorType.Del => "DEL",
        SectorType.Gnd => "GND",
        SectorType.Twr or SectorType.ITwr => "TWR",
        SectorType.App => "APP",
        SectorType.Ctr => "CTR",
        _ => t.ToString().ToUpperInvariant(),
    };

    private static string Normalize(string? position) => (position ?? "").Trim().ToUpperInvariant();
}

namespace Vipi.Sectorfile.Models;

/// <summary>Un filtro degli slot di uno stand: il genere (<c>t</c>, <c>c</c>, <c>d</c>, <c>w</c>) e il suo valore.</summary>
public sealed record FiltroDelloSlot(char Genere, string Valore)
{
    public override string ToString() => $"{Genere}_{Valore}";
}

/// <summary>
/// Gli slot di uno stand (6° campo di <c>[GATES]</c>, manuale IVAO «Slots for Gates»; lotto «Subito» slice 12b, R2):
/// filtri separati da spazi. <c>t_A320</c> tipo di aereo, <c>c_OAL</c> prefisso del nominativo, <c>d_LIRF</c> scalo di
/// partenza, <c>w_</c> cargo ammessi (un volo è cargo se le RMK dicono <c>CARGO</c>; senza <c>w_</c> i cargo non
/// entrano). Fra generi diversi vale E, dentro lo stesso genere vale O.
/// </summary>
public static class SlotDelloStand
{
    /// <summary>I tipi dello stand (5° campo), nell'ordine del manuale.</summary>
    public const string Tipi = "LMHSG";

    /// <summary>I generi dei filtri, col loro significato.</summary>
    public static IReadOnlyDictionary<char, string> Generi { get; } = new Dictionary<char, string>
    {
        ['t'] = "tipo di aereo",
        ['c'] = "prefisso del nominativo",
        ['d'] = "scalo di partenza",
        ['w'] = "cargo ammessi",
    };

    /// <summary>I filtri di uno slot, nell'ordine in cui sono scritti; quelli che non si leggono non ci sono.</summary>
    public static IReadOnlyList<FiltroDelloSlot> Leggi(string? slot)
        => [.. Pezzi(slot).Where(p => Sbagliato(p) is null).Select(p => new FiltroDelloSlot(char.ToLowerInvariant(p[0]), p[2..]))];

    /// <summary>Cosa non va in uno slot, filtro per filtro; vuoto se è scritto bene (o non c'è).</summary>
    public static IReadOnlyList<string> Problemi(string? slot)
        => [.. Pezzi(slot).Select(Sbagliato).Where(p => p is not null).Select(p => p!)];

    /// <summary>
    /// Lo slot come si scrive: i generi nell'ordine <c>w_ t_ c_ d_</c> non si impongono (l'ordine è dell'AOD), ma i
    /// valori vanno in maiuscolo e i doppioni cadono. Null se non resta niente.
    /// </summary>
    public static string? Normale(string? slot)
    {
        var visti = new HashSet<string>(StringComparer.Ordinal);
        var filtri = Pezzi(slot).Select(p => p.Length >= 2 && p[1] == '_' ? char.ToLowerInvariant(p[0]) + "_" + p[2..].ToUpperInvariant() : p)
            .Where(visti.Add).ToList();
        return filtri.Count == 0 ? null : string.Join(' ', filtri);
    }

    private static IEnumerable<string> Pezzi(string? slot)
        => (slot ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Sbagliato(string pezzo)
    {
        if (pezzo.Length < 2 || pezzo[1] != '_' || !Generi.ContainsKey(char.ToLowerInvariant(pezzo[0])))
            return $"«{pezzo}» non è un filtro: si scrive t_A320 (tipo), c_OAL (nominativo), d_LIRF (partenza) o w_ (cargo)";
        char genere = char.ToLowerInvariant(pezzo[0]);
        string valore = pezzo[2..];
        if (!valore.All(char.IsAsciiLetterOrDigit))
            return $"«{pezzo}»: dopo {genere}_ solo lettere e cifre";
        return genere switch
        {
            'w' when valore.Length > 0 => $"«{pezzo}»: w_ si scrive da solo",
            't' or 'c' when valore.Length == 0 => $"«{pezzo}»: manca il valore dopo {genere}_",
            'd' when valore.Length != 4 => $"«{pezzo}»: dopo d_ va l'ICAO dello scalo di partenza, 4 lettere",
            _ => null,
        };
    }
}

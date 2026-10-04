using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Tipo e slot proposti per uno stand dai suoi metadati, col perché da dire all'AOD.</summary>
/// <param name="Tipo">Il tipo per Aurora (L, M, H, S, G), o null se i metadati non lo dicono.</param>
/// <param name="Slot">Gli slot da scrivere (quelli che lo stand ha già restano), o null se non c'è niente da aggiungere.</param>
public sealed record TipoESlotProposti(string? Tipo, string? Slot, string Perche);

/// <summary>
/// Tipo e slot di uno stand (lotto «Subito» slice 12b, «file per file» R2, R2b): i valori che la scheda accetta, e la
/// proposta dai metadati — dal codice ICAO e dall'uso il tipo, dall'uso «cargo» il filtro <c>w_</c>, dalle compagnie i
/// filtri <c>c_</c>.
/// </summary>
/// <remarks>
/// 🔴 La corrispondenza codice → tipo è una <b>proposta dell'agente</b> (4 ottobre 2026), da confermare col committente:
/// A e B → L, C → M, D ed E → H, F → S; l'uso «ga» (aviazione generale) → G qualunque sia il codice. I filtri <c>t_</c>
/// (tipi di aereo per codice) NON si propongono: servirebbe una tabella dei tipi per codice presa da una fonte primaria.
/// </remarks>
public static class PropostaDelloStand
{
    /// <summary>Gli usi di uno stand (chiave <c>use</c>, anche più d'uno con la virgola), col significato.</summary>
    public static IReadOnlyDictionary<string, string> Usi { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["schengen"] = "Schengen",
        ["nonschengen"] = "extra-Schengen",
        ["cargo"] = "cargo",
        ["ga"] = "aviazione generale",
        ["mil"] = "militare",
        ["heli"] = "elicotteri",
    };

    /// <summary>
    /// Il valore da scrivere in tipo (<c>Type</c>) o slot (<c>Slot</c>) di uno stand, o false col perché. Vuoto = null
    /// (il campo non si scrive).
    /// </summary>
    public static bool Normalizza(string campo, string? valore, out string? scritto, out string? perche)
    {
        perche = null;
        scritto = string.IsNullOrWhiteSpace(valore) ? null : valore.Trim();
        if (scritto is null)
            return true;

        if (campo == nameof(Stand.Type))
        {
            scritto = scritto.ToUpperInvariant();
            if (scritto.Length == 1 && SlotDelloStand.Tipi.Contains(scritto[0], StringComparison.Ordinal))
                return true;
            perche = $"«{valore!.Trim()}» non è un tipo di stand: L, M, H, S o G.";
            return false;
        }

        if (SlotDelloStand.Problemi(scritto) is { Count: > 0 } problemi)
        {
            perche = string.Join("; ", problemi) + ".";
            return false;
        }

        scritto = SlotDelloStand.Normale(scritto);
        return true;
    }

    /// <summary>
    /// La proposta per uno stand dai suoi metadati (<paramref name="chiavi"/>: come sono scritti nel tag), o null se non
    /// dicono niente o se lo stand è già così.
    /// </summary>
    public static TipoESlotProposti? Di(Stand stand, IReadOnlyDictionary<string, string>? chiavi)
    {
        ArgumentNullException.ThrowIfNull(stand);
        if (chiavi is null || chiavi.Count == 0)
            return null;

        string? Valore(string chiave) => chiavi.TryGetValue(chiave, out string? scritto) ? Metadati.Testo(scritto).Trim() : null;
        var usi = Elenco(Valore("use")).Select(u => u.ToLowerInvariant()).ToList();
        var perche = new List<string>();

        string? tipo = null;
        if (usi.Contains("ga"))
        {
            tipo = "G";
            perche.Add("uso aviazione generale → G");
        }
        else if (Valore("code")?.ToUpperInvariant() is { Length: 1 } codice)
        {
            tipo = codice[0] switch { 'A' or 'B' => "L", 'C' => "M", 'D' or 'E' => "H", 'F' => "S", _ => null };
            if (tipo is not null)
                perche.Add($"codice {codice} → {tipo}");
        }

        var filtri = new List<string>();
        if (usi.Contains("cargo"))
        {
            filtri.Add("w_");
            perche.Add("uso cargo → w_");
        }

        var compagnie = Elenco(Valore("airlines")).Select(c => c.ToUpperInvariant()).Where(c => c.All(char.IsAsciiLetterOrDigit)).ToList();
        if (compagnie.Count > 0)
        {
            filtri.AddRange(compagnie.Select(c => "c_" + c));
            perche.Add($"compagnie → {string.Join(' ', compagnie.Select(c => "c_" + c))}");
        }

        // Gli slot che lo stand ha già restano, e i proposti si aggiungono in fondo.
        string? slot = filtri.Count == 0 ? null : SlotDelloStand.Normale(string.Join(' ', [stand.Slot ?? string.Empty, .. filtri]));
        bool tipoNuovo = tipo is not null && !string.Equals(tipo, stand.Type?.Trim(), StringComparison.OrdinalIgnoreCase);
        bool slotNuovo = slot is not null && !string.Equals(slot, SlotDelloStand.Normale(stand.Slot), StringComparison.Ordinal);
        return tipoNuovo || slotNuovo
            ? new TipoESlotProposti(tipoNuovo ? tipo : null, slotNuovo ? slot : null, string.Join(" · ", perche))
            : null;
    }

    private static IEnumerable<string> Elenco(string? valore)
        => (valore ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Trim('"'));
}

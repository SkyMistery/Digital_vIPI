using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// Un tratto di un'aerovia, dal punto numero <paramref name="Ordinale"/> al successivo, col verso e le quote che porta
/// il tag <c>//@@</c> del punto che lo apre (null: non scritto).
/// </summary>
public sealed record TrattoDiAerovia(int Ordinale, string Da, string A, string? Verso, string? Inferiore, string? Superiore)
{
    public bool ConDati => Verso is not null || Inferiore is not null || Superiore is not null;
}

/// <summary>
/// I tratti delle aerovie (lotto «Subito» slice 14c, «file per file» B2): ogni tratto ha il suo verso e la sua quota
/// minima e massima, come nel PDF dell'AIP (ENR 3). Stanno nel tag <c>//@@"PUNTO" dir=… lower=… upper=…</c> sul punto
/// che apre il tratto (§M regola 4 e catalogo): Aurora lo legge come un commento.
/// </summary>
/// <remarks>
/// Il verso è rispetto all'ordine dei punti nel file: <c>both</c> nei due versi, <c>fwd</c> solo da questo punto al
/// successivo, <c>back</c> solo al contrario. L'ultimo punto di un pezzo non apre un tratto; un'interruzione
/// (<c>T;BREAK</c>) e le righe delle etichette non ne hanno.
/// </remarks>
public static class TrattiDelleAerovie
{
    public const string Verso = "dir";
    public const string Inferiore = "lower";
    public const string Superiore = "upper";

    /// <summary>I versi, come li scrive §M, con la freccia e le parole della scheda.</summary>
    public static IReadOnlyList<(string Valore, string Segno, string Significato)> Versi { get; } =
    [
        ("both", "↔", "nei due versi"),
        ("fwd", "→", "solo verso il punto dopo"),
        ("back", "←", "solo dal punto dopo a questo"),
    ];

    /// <summary>I tratti del record, o vuoto se non è un pezzo di aerovia con almeno due punti.</summary>
    public static IReadOnlyList<TrattoDiAerovia> Di(IFileConRecord file, int record)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (record < 0 || record >= file.RecordDelModello.Count || !EUnPezzo(file.RecordDelModello[record]))
            return [];
        return Di(file.PuntiConTag(record));
    }

    /// <summary>I tratti da una fila di punti coi loro tag.</summary>
    public static IReadOnlyList<TrattoDiAerovia> Di(IReadOnlyList<PuntoConTag> punti)
    {
        ArgumentNullException.ThrowIfNull(punti);
        var tratti = new List<TrattoDiAerovia>(Math.Max(0, punti.Count - 1));
        for (int i = 0; i + 1 < punti.Count; i++)
        {
            string? Valore(string chiave) => punti[i].Chiavi.TryGetValue(chiave, out string? scritto) && Metadati.Testo(scritto).Trim() is { Length: > 0 } testo ? testo : null;
            tratti.Add(new TrattoDiAerovia(punti[i].Ordinale, punti[i].Punto, punti[i + 1].Punto, Valore(Verso), Valore(Inferiore), Valore(Superiore)));
        }

        return tratti;
    }

    /// <summary>Vero per un pezzo di tracciato (righe <c>T;</c> con almeno due punti), non per un'interruzione o un'etichetta.</summary>
    public static bool EUnPezzo(object record)
        => record is Airway { FixLabels.Count: >= 2 } aerovia && !string.Equals(aerovia.Name.Trim(), "BREAK", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// I tratti che hanno un dato, uno per riga, per il passaggio del mouse sulla mappa
    /// (<c>ELKAP → BIBEK: nei due versi, FL95 – FL195</c>). Null se nessuno ne ha.
    /// </summary>
    public static string? Suggerimento(IReadOnlyList<PuntoConTag> punti)
    {
        var righe = Di(punti).Where(t => t.ConDati).Select(Detto).ToList();
        return righe.Count > 0 ? string.Join("\n", righe) : null;
    }

    private static string Detto(TrattoDiAerovia tratto)
    {
        string?[] pezzi =
        [
            tratto.Verso switch
            {
                null => null,
                "both" => "nei due versi",
                "fwd" => $"solo da {tratto.Da} a {tratto.A}",
                "back" => $"solo da {tratto.A} a {tratto.Da}",
                var altro => altro,
            },
            tratto.Inferiore is null && tratto.Superiore is null ? null : $"{tratto.Inferiore ?? "?"} – {tratto.Superiore ?? "?"}",
        ];
        return $"{tratto.Da} → {tratto.A}: {string.Join(", ", pezzi.Where(p => p is not null))}";
    }
}

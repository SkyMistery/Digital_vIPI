using System.Globalization;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Una posizione ATC da scegliere per il fuoco su un settore: il codice e, se c'è nei <c>.frq</c>, la frequenza.</summary>
public sealed record SettoreDellaVista(string Codice, string? Frequenza);

/// <summary>
/// La vista per settore (carta «Lavorare su uno scalo o su un settore», R7, D2): un settore è una <b>posizione</b> ATC
/// (<c>LIRR_NC_CTR</c>), e la riguardano i settori dei <c>.tfl</c> che la accendono, i bordi dei <c>.hartcc</c>,
/// <c>.lartcc</c> e <c>.artcc</c> col suo nome (<c>RR NC</c> o <c>LIRR NC</c>), i file che portano il suo nome
/// (<c>lirr_nc_ctr.tfl</c>) e le sue righe dei <c>.frq</c>. Come la vista per scalo non sposta niente: ogni voce porta al
/// suo record o al suo file.
/// <para>Il bordo si riconosce dal nome del gruppo: lo scalo dell'ACC per intero o senza le prime due lettere, e la
/// parte di mezzo del codice (<c>LIRR_NC_CTR</c> → <c>LIRR NC</c>, <c>RR NC</c>). Un bordo che porta il nome di un altro
/// ACC (<c>LIRR ES0</c> per <c>LICA_ES0_APP</c>) non si prende: dal solo nome non si sa.</para>
/// </summary>
public static class VistaPerSettore
{
    // Le estensioni dei file dei settori: i soli che, col nome della posizione, sono «file del settore».
    private static readonly string[] EstensioniDeiSettori = [".tfl", ".hartcc", ".lartcc", ".artcc"];

    /// <summary>
    /// Le posizioni con qualcosa da disegnare — un settore di un <c>.tfl</c> che le accende, un bordo o un file col loro
    /// nome —, una volta ciascuna, per codice. Quelle dei <c>.frq</c> senza niente sulla mappa non ci sono: il fuoco su di
    /// loro mostrerebbe una mappa vuota.
    /// </summary>
    public static IReadOnlyList<SettoreDellaVista> Settori(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);

        var frequenze = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, posizione) in VistaPerScalo.Record<AtcPosition>(sessione))
            frequenze.TryAdd(posizione.Code.Trim(), posizione.FrequencyMhz.ToString("0.000", CultureInfo.InvariantCulture));

        var accese = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, settore) in VistaPerScalo.Record<TflSector>(sessione))
            accese.UnionWith(settore.Posizioni());

        var bordi = VistaPerScalo.Record<StaticBoundaryGroup>(sessione).Select(r => Normale(r.Record.Name)).ToHashSet(StringComparer.Ordinal);
        var nomiDeiFile = sessione.File.Values.Where(f => EDiSettori(f.Relativo))
            .Select(f => Path.GetFileNameWithoutExtension(f.Relativo)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. frequenze.Keys.Concat(accese)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => accese.Contains(c) || nomiDeiFile.Contains(c) || NomiDelBordo(c).Any(bordi.Contains))
            .Select(c => c.ToUpperInvariant())
            .Order(StringComparer.Ordinal)
            .Select(c => new SettoreDellaVista(c, frequenze.GetValueOrDefault(c)))];
    }

    /// <summary>Le sezioni di una posizione, nell'ordine in cui si leggono; quelle vuote non ci sono.</summary>
    public static IReadOnlyList<SezioneDelloScalo> Di(SessioneAperta sessione, string codice)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentException.ThrowIfNullOrWhiteSpace(codice);
        string posizione = codice.Trim().ToUpperInvariant();
        var nomi = NomiDelBordo(posizione).ToHashSet(StringComparer.Ordinal);

        var colSuoNome = sessione.File.Values
            .Where(f => EDiSettori(f.Relativo) && string.Equals(Path.GetFileNameWithoutExtension(f.Relativo), posizione, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Relativo, StringComparer.Ordinal)
            .Select(f => new VoceDelloScalo(VistaPerScalo.NomeDelFile(f.Relativo), f.Relativo, null,
                f.Record == 1 ? "1 record" : $"{f.Record.ToString("N0", CultureInfo.InvariantCulture)} record"))
            .ToList();

        SezioneDelloScalo[] sezioni =
        [
            new("settori", "Settori", [.. VistaPerScalo.Record<TflSector>(sessione)
                .Where(r => r.Record.Posizioni().Contains(posizione, StringComparer.OrdinalIgnoreCase))
                .Select(r => new VoceDelloScalo(r.Record.SectorCode.Trim(), r.File, r.Indice,
                    $"{VistaPerScalo.NomeDelFile(r.File)} · {r.Record.Vertices.Count} vertici"))]),
            new("bordi", "Bordi", [.. VistaPerScalo.Record<StaticBoundaryGroup>(sessione)
                .Where(r => nomi.Contains(Normale(r.Record.Name)))
                .Select(r => new VoceDelloScalo(r.Record.Name.Trim(), r.File, r.Indice, VistaPerScalo.NomeDelFile(r.File)))]),
            new("file", "File del settore", colSuoNome),
            new("posizioni", "Posizioni", [.. VistaPerScalo.Record<AtcPosition>(sessione)
                .Where(r => string.Equals(r.Record.Code.Trim(), posizione, StringComparison.OrdinalIgnoreCase))
                .Select(r => new VoceDelloScalo(r.Record.Code.Trim(), r.File, r.Indice,
                    r.Record.FrequencyMhz.ToString("0.000", CultureInfo.InvariantCulture) + " · " + VistaPerScalo.NomeDelFile(r.File)))]),
        ];
        return [.. sezioni.Where(s => s.Voci.Count > 0)];
    }

    /// <summary>
    /// I nomi che può avere il bordo di una posizione, normalizzati: <c>LIRR_NC_CTR</c> → <c>LIRR NC</c> e <c>RR NC</c>.
    /// Una posizione senza parte di mezzo (<c>LIRF_APP</c>, <c>LIRR_FSS</c>) non ne ha.
    /// </summary>
    public static IEnumerable<string> NomiDelBordo(string codice)
    {
        ArgumentNullException.ThrowIfNull(codice);
        string[] parti = codice.Trim().ToUpperInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parti.Length < 3)
            yield break;
        string mezzo = string.Join(' ', parti[1..^1]);
        yield return $"{parti[0]} {mezzo}";
        if (parti[0].Length > 2)
            yield return $"{parti[0][2..]} {mezzo}";
    }

    private static string Normale(string nome)
        => string.Join(' ', nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    private static bool EDiSettori(string relativo)
        => EstensioniDeiSettori.Any(e => relativo.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}

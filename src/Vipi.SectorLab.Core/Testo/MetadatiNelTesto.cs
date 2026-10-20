using Vipi.Sectorfile.IO;

namespace Vipi.SectorLab.Core.Testo;

/// <summary>
/// I metadati <c>//@</c> nel testo della pagina Testo (D13: «cancellare a mano un record che ha metadati chiede prima»).
/// Un record si riconosce dal NOME, come lo aggancia la dichiarazione (<see cref="Metadati"/>): <c>//@"BC404" note=…</c>,
/// poi facoltativi <c>//@START</c> … <c>//@END "BC404"</c>. Se nel testo scritto quel nome non è più un campo di nessuna
/// riga di dati, il record è stato cancellato e i suoi tag resterebbero senza di lui.
/// </summary>
public static class MetadatiNelTesto
{
    /// <summary>
    /// Il nome dichiarato da una riga <c>//@</c>, o null se la riga non è una dichiarazione (<c>//@START</c>, <c>//@END</c>,
    /// <c>//@@</c> di un punto, <c>//@source=…</c> del file). Fra virgolette; nella forma vecchia, fino alla prima parola con
    /// <c>=</c>.
    /// </summary>
    public static string? NomeDichiarato(string riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        var t = riga.TrimStart();
        if (!t.StartsWith("//@", StringComparison.Ordinal) || t.StartsWith("//@@", StringComparison.Ordinal))
            return null;
        var resto = t[3..];
        if (resto.StartsWith("START", StringComparison.Ordinal) || resto.StartsWith("END", StringComparison.Ordinal))
            return null;
        if (resto.StartsWith('"'))
            return resto.IndexOf('"', 1) is var chiude and > 1 ? resto[1..chiude] : null;
        var parole = resto.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeWhile(p => !p.Contains('=', StringComparison.Ordinal)).ToList();
        return parole.Count > 0 ? string.Join(' ', parole) : null;
    }

    /// <summary>
    /// I record coi metadati che <paramref name="dopo"/> non ha più: dichiarati in <paramref name="prima"/>, col nome che
    /// in <paramref name="prima"/> era un campo di una riga di dati e in <paramref name="dopo"/> non lo è di nessuna.
    /// </summary>
    public static IReadOnlyList<string> Tolti(IReadOnlyList<string> prima, IReadOnlyList<string> dopo)
    {
        ArgumentNullException.ThrowIfNull(prima);
        ArgumentNullException.ThrowIfNull(dopo);
        var nomiPrima = NomiNeiDati(prima);
        var nomiDopo = NomiNeiDati(dopo);
        return [.. prima.Select(NomeDichiarato).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => nomiPrima.Contains(n) && !nomiDopo.Contains(n))];
    }

    /// <summary>
    /// <paramref name="righe"/> senza i metadati dei record <paramref name="nomi"/>: la dichiarazione, e il suo
    /// <c>//@START</c> (se viene subito dopo) col suo <c>//@END "nome"</c>.
    /// </summary>
    public static IReadOnlyList<string> SenzaIMetadatiDi(IReadOnlyList<string> righe, IReadOnlyCollection<string> nomi)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(nomi);
        var via = new HashSet<int>();
        for (int i = 0; i < righe.Count; i++)
        {
            if (NomeDichiarato(righe[i]) is not { } nome || !nomi.Contains(nome, StringComparer.OrdinalIgnoreCase))
                continue;
            via.Add(i);
            int j = i + 1;
            while (j < righe.Count && righe[j].Trim().Length == 0)
                j++;
            if (j >= righe.Count || !righe[j].TrimStart().StartsWith("//@START", StringComparison.Ordinal))
                continue;
            via.Add(j);
            for (int k = j + 1; k < righe.Count; k++)
            {
                var r = righe[k].TrimStart();
                if (r.StartsWith("//@END", StringComparison.Ordinal))
                {
                    via.Add(k);
                    break;
                }
                if (NomeDichiarato(righe[k]) is not null)
                    break;   // un'altra dichiarazione: il blocco non aveva la sua fine
            }
        }
        return [.. righe.Where((_, i) => !via.Contains(i))];
    }

    // I campi delle righe di dati (non commenti), senza spazi in testa e in coda.
    private static HashSet<string> NomiNeiDati(IReadOnlyList<string> righe) =>
        righe.Where(r => !r.TrimStart().StartsWith("//", StringComparison.Ordinal))
             .SelectMany(r => r.Split(';'))
             .Select(c => c.Trim())
             .Where(c => c.Length > 0)
             .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

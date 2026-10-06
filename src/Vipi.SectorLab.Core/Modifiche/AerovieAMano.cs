using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Le righe da cambiare per un gesto su un'aerovia, o il perché non si può.</summary>
public sealed record GestoSullAerovia(IReadOnlyDictionary<int, IReadOnlyList<string>>? Sostituzioni, string? Perche)
{
    public static GestoSullAerovia No(string perche) => new(null, perche);
}

/// <summary>
/// Aggiungere e togliere un'aerovia a mano (lotto «Subito» slice 14e-14f, «file per file» B14, B15, B5).
/// </summary>
/// <remarks>
/// <para><b>Aggiungere</b>: un nome e la sequenza dei punti. Il Lab scrive il blocco dell'aerovia —
/// <c>//@"NOME" locked=si</c> (a mano: l'import dai PDF non la tocca), un tag per tratto col verso di base «nei due
/// versi» (<c>//@@"PUNTO" dir=both</c>) e le righe <c>T;</c> — nella parte dei tracciati, in ordine di nome. Le quote
/// non si inventano: restano da scrivere nella scheda dei tratti, e finché mancano c'è l'avviso. Le etichette le
/// calcola <see cref="EtichetteDelleAerovie"/>, subito dopo.</para>
/// <para><b>Togliere</b>: via tutti i suoi pezzi, i <c>BREAK</c> fra loro, il suo blocco e i suoi tag; un'etichetta
/// condivisa perde solo il suo nome, una solo sua sparisce. I commenti scritti a mano restano. Anche un'aerovia
/// <c>locked</c> si toglie: quel segno ferma l'import, non l'AOD (committente, B5).</para>
/// </remarks>
public static class AerovieAMano
{
    /// <summary>Perché quel nome non va per un'aerovia nuova, o null.</summary>
    public static string? PercheNonVa(string? nome, AerovieLette lette)
    {
        ArgumentNullException.ThrowIfNull(lette);
        string n = (nome ?? string.Empty).Trim();
        if (n.Length == 0)
            return "Serve il nome dell'aerovia.";
        if (n.Any(c => char.IsWhiteSpace(c) || c is ';' or '"' or '/'))
            return "Il nome di un'aerovia non ha spazi, punti e virgola, virgolette o barre.";
        if (n.Contains('-', StringComparison.Ordinal))
            return "Il trattino unisce i nomi di due aerovie nelle etichette condivise (L53-P873): un nome non può averlo.";
        if (string.Equals(n, RigheDelleAerovie.Interruzione, StringComparison.OrdinalIgnoreCase))
            return "BREAK è il nome che spezza un'aerovia: non può chiamarsi così.";
        if (lette.Nomi.Contains(n, StringComparer.OrdinalIgnoreCase))
            return $"{n} c'è già in questo file.";
        return null;
    }

    /// <summary>I punti scritti dall'AOD: separati da spazi, virgole o a capo.</summary>
    public static IReadOnlyList<string> Punti(string? testo)
        => (testo ?? string.Empty).Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToUpperInvariant()).ToList();

    /// <summary>Il blocco di un'aerovia nuova, nella parte dei tracciati, in ordine di nome.</summary>
    public static GestoSullAerovia Nuova(IReadOnlyList<string> righe, string? nome, IReadOnlyList<string> punti, Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(punti);
        ArgumentNullException.ThrowIfNull(punto);
        var lette = RigheDelleAerovie.Leggi(righe, punto);
        if (PercheNonVa(nome, lette) is { } perche)
            return GestoSullAerovia.No(perche);
        string n = nome!.Trim();
        if (punti.Count < 2)
            return GestoSullAerovia.No("Un'aerovia ha almeno due punti.");
        if (punti.FirstOrDefault(p => punto(p) is null) is { } ignoto)
            return GestoSullAerovia.No($"«{ignoto}» non è un fix, un VOR o un NDB del sector: i punti di un'aerovia si scrivono per nome.");
        if (Enumerable.Range(0, punti.Count - 1).FirstOrDefault(i => punti[i] == punti[i + 1], -1) is var doppio and >= 0)
            return GestoSullAerovia.No($"«{punti[doppio]}» è scritto due volte di seguito.");

        var blocco = new List<string> { $"//@\"{n}\" locked=si", "//@START" };
        for (int i = 0; i < punti.Count; i++)
        {
            // Il tag sta sul punto che APRE il tratto: l'ultimo non ne apre.
            if (i < punti.Count - 1)
                blocco.Add($"//@@\"{punti[i]}\" dir=both");
            blocco.Add($"T;{n};{punti[i]};{punti[i]};");
        }

        blocco.Add($"//@END \"{n}\"");

        // Dopo l'ultima riga dell'aerovia col nome più grande fra quelli che non la superano; se nessuna, prima della
        // prima; se il file non ha tracciati, in testa.
        var perNome = lette.Pezzi.Select(p => (p.Aerovia, Ultima: p.Punti[^1].Riga, Prima: p.Punti[0].Riga)).ToList();
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        if (perNome.Count == 0)
        {
            if (righe.Count == 0)
                return GestoSullAerovia.No("Il file è vuoto: la prima aerovia si scrive a mano, con le sue intestazioni.");
            sostituzioni[1] = [.. blocco, righe[0]];
            return new(sostituzioni, null);
        }

        var nonOltre = perNome.Where(p => string.CompareOrdinal(p.Aerovia, n) <= 0).ToList();
        if (nonOltre.Count == 0)
        {
            int prima = PrimaRigaDelBlocco(righe, perNome.Min(p => p.Prima));
            sostituzioni[prima] = [.. blocco, righe[prima - 1]];
            return new(sostituzioni, null);
        }

        string massimo = nonOltre.Select(p => p.Aerovia).Max(StringComparer.Ordinal)!;
        int dopo = nonOltre.Where(p => p.Aerovia == massimo).Max(p => p.Ultima);
        // Se l'aerovia prima è un blocco, si va oltre il suo //@END.
        while (dopo < righe.Count && righe[dopo].TrimStart().StartsWith("//@END", StringComparison.Ordinal))
            dopo++;
        sostituzioni[dopo] = [righe[dopo - 1], .. blocco];
        return new(sostituzioni, null);
    }

    /// <summary>Toglie un'aerovia: i suoi tracciati, i suoi tag, il suo nome dalle etichette.</summary>
    public static GestoSullAerovia Togli(IReadOnlyList<string> righe, string? nome, Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(punto);
        string n = (nome ?? string.Empty).Trim();
        var lette = RigheDelleAerovie.Leggi(righe, punto);
        if (!lette.Nomi.Contains(n, StringComparer.Ordinal) && !lette.Etichette.Any(e => e.Nomi.Contains(n, StringComparer.Ordinal)))
            return GestoSullAerovia.No($"{n} non c'è in questo file.");

        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        var sue = lette.Pezzi.Where(p => p.Aerovia == n).SelectMany(p => p.Punti).Select(p => p.Riga).ToHashSet();
        foreach (int riga in sue)
        {
            sostituzioni[riga] = [];
            // I tag dei suoi punti, subito sopra.
            for (int sopra = riga - 1; sopra >= 1 && Metadati.EUnTagDiPunto(righe[sopra - 1].TrimStart()); sopra--)
                sostituzioni[sopra] = [];
        }

        // Un BREAK fra due suoi pezzi, o in testa o in coda a un suo pezzo, è suo.
        foreach (int riga in lette.Interruzioni)
        {
            int prima = DatoVicino(righe, riga, -1), dopo = DatoVicino(righe, riga, +1);
            if (sue.Contains(prima) || (prima == 0 && sue.Contains(dopo)))
                sostituzioni[riga] = [];
        }

        // Il suo blocco: la dichiarazione, //@START e //@END.
        for (int i = 0; i < righe.Count; i++)
        {
            string riga = righe[i].TrimStart();
            if (riga.StartsWith($"//@\"{n}\"", StringComparison.Ordinal))
            {
                sostituzioni[i + 1] = [];
                if (i + 1 < righe.Count && righe[i + 1].Trim() == "//@START")
                    sostituzioni[i + 2] = [];
            }
            else if (riga.StartsWith("//@END", StringComparison.Ordinal) && riga["//@END".Length..].Trim().Trim('"') == n)
            {
                sostituzioni[i + 1] = [];
            }
        }

        foreach (var etichetta in lette.Etichette.Where(e => e.Nomi.Contains(n, StringComparer.Ordinal)))
        {
            var altri = etichetta.Nomi.Where(x => x != n).ToList();
            if (altri.Count == 0)
            {
                sostituzioni[etichetta.Riga] = [];
            }
            else
            {
                string[] campi = righe[etichetta.Riga - 1].Split(';');
                campi[1] = string.Join('-', altri);
                sostituzioni[etichetta.Riga] = [string.Join(';', campi)];
            }
        }

        return new(sostituzioni, null);
    }

    // La riga di dati (non vuota, non commento) più vicina in quel verso, o 0.
    private static int DatoVicino(IReadOnlyList<string> righe, int riga, int verso)
    {
        for (int i = riga + verso; i >= 1 && i <= righe.Count; i += verso)
        {
            string t = righe[i - 1].Trim();
            if (Metadati.EUnTag(t))
                continue;
            return t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) ? 0 : i;
        }

        return 0;
    }

    // Se sopra la prima riga di dati ci sono la dichiarazione del blocco, //@START e i tag del punto, si parte da lì.
    private static int PrimaRigaDelBlocco(IReadOnlyList<string> righe, int riga)
    {
        while (riga > 1 && Metadati.EUnTag(righe[riga - 2].TrimStart()))
            riga--;
        return riga;
    }
}

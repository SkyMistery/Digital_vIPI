namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Un file dell'albero, come lo vede chi sfoglia.</summary>
/// <param name="Relativo">Il percorso relativo alla radice del clone: è la chiave della sessione.</param>
/// <param name="Nome">Solo il nome, per l'elenco.</param>
/// <param name="Record">Quanti record ha letto il motore; zero per un file che non interpreta.</param>
/// <param name="Interpretato">Falso per i file tenuti come testo (<c>.txt</c>, <c>.cpr</c>, <c>.clr</c>…).</param>
public sealed record FileDaSfogliare(string Relativo, string Nome, int Record, bool Interpretato);

/// <summary>Una cartella con dentro le sue e i suoi file, in ordine di nome.</summary>
public sealed record CartellaDaSfogliare(
    string Nome,
    string Percorso,
    IReadOnlyList<CartellaDaSfogliare> Cartelle,
    IReadOnlyList<FileDaSfogliare> File)
{
    public int RecordTotali => File.Sum(f => f.Record) + Cartelle.Sum(c => c.RecordTotali);

    public int FileTotali => File.Count + Cartelle.Sum(c => c.FileTotali);
}

/// <summary>
/// L'albero delle cartelle da sfogliare (carta F3 §2.2 passo 2, slice 5): si costruisce dai file che la sessione ha
/// già aperto, non rileggendo il disco — così quel che si sfoglia è esattamente quel che si ha in mano, e un file
/// comparso dopo l'apertura non appare finché non si riapre (che è la stessa regola del salvataggio, §2.4).
/// </summary>
public static class AlberoDaSfogliare
{
    /// <summary>
    /// L'albero sotto <c>SectorFiles/Include/IT</c>. Gli <c>.isc</c>, <c>update.ini</c> e <c>changelog.md</c> stanno
    /// fuori da lì: si ritrovano in <see cref="FuoriDaiDati"/>.
    /// </summary>
    public static CartellaDaSfogliare Di(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        string radice = sessione.Cartella.Relativo(sessione.Cartella.CartellaIt);
        return Costruisci(radice, sessione.File.Values.Where(f => Sotto(f.Relativo, radice)));
    }

    /// <summary>I file aperti che stanno fuori dalla cartella dei dati: gli <c>.isc</c> e i due file della radice.</summary>
    public static IReadOnlyList<FileDaSfogliare> FuoriDaiDati(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        string radice = sessione.Cartella.Relativo(sessione.Cartella.CartellaIt);
        return [.. sessione.File.Values
            .Where(f => !Sotto(f.Relativo, radice))
            .Select(Voce)
            .OrderBy(f => f.Relativo, StringComparer.OrdinalIgnoreCase)];
    }

    private static bool Sotto(string relativo, string radice)
        => relativo.StartsWith(radice + "/", StringComparison.OrdinalIgnoreCase);

    private static FileDaSfogliare Voce(FileAperto file)
        => new(file.Relativo, file.Relativo[(file.Relativo.LastIndexOf('/') + 1)..], file.Record, file is IFileConRecord);

    private static CartellaDaSfogliare Costruisci(string percorso, IEnumerable<FileAperto> file)
    {
        var quiDentro = new List<FileDaSfogliare>();
        var sotto = new Dictionary<string, List<FileAperto>>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in file)
        {
            string resto = f.Relativo[(percorso.Length + 1)..];
            int barra = resto.IndexOf('/');
            if (barra < 0)
            {
                quiDentro.Add(Voce(f));
                continue;
            }

            string figlia = resto[..barra];
            if (!sotto.TryGetValue(figlia, out var lista))
                sotto[figlia] = lista = [];
            lista.Add(f);
        }

        var cartelle = sotto
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(c => Costruisci($"{percorso}/{c.Key}", c.Value))
            .ToList();

        return new CartellaDaSfogliare(
            percorso[(percorso.LastIndexOf('/') + 1)..],
            percorso,
            cartelle,
            [.. quiDentro.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase)]);
    }
}

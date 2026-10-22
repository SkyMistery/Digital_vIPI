using System.Globalization;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// I cicli dell'AIP che il Lab ha, rispetto al giorno (carta «L'AIP intero», passo 8). <see cref="DiOggi"/> è il ciclo in
/// vigore oggi dal calendario AIRAC; <see cref="InVigore"/> è lui se il Lab ce l'ha; <see cref="Successivo"/> il primo dei
/// cicli che entrano dopo (già usciti: l'emendamento li crea prima della data), <see cref="Prossimi"/> quelli dopo ancora;
/// <see cref="Superati"/> quelli che non sono più in vigore.
/// </summary>
public sealed record StatoDeiCicli(string DiOggi, string? InVigore, string? Successivo, IReadOnlyList<string> Prossimi, IReadOnlyList<string> Superati)
{
    /// <summary>
    /// Il ciclo su cui il Lab lavora: quello in vigore; se il Lab non ce l'ha, il successivo (oggi, 22 ottobre 2026, è in vigore
    /// il 2610 e i PDF scaricati sono del 2611); se non c'è nemmeno quello, l'ultimo superato.
    /// </summary>
    public string? DaAprire => InVigore ?? Successivo ?? Prossimi.FirstOrDefault() ?? Superati.LastOrDefault();

    /// <summary>I superati che si possono togliere: solo se il Lab ha il ciclo in vigore (decisione del 21 ottobre, §2).</summary>
    public IReadOnlyList<string> DaTogliere => InVigore is null ? [] : Superati;

    /// <summary>
    /// Che cos'è il ciclo per oggi, in parole: «in vigore dal 29/10/2026», «non ancora in vigore: dal 26/11/2026», «non più
    /// in vigore dal 26/11/2026».
    /// </summary>
    public string Dice(string ciclo)
    {
        var dal = AipEspanso.CicloInVigore(ciclo);
        if (ciclo == DiOggi) return $"in vigore dal {Data(dal)}";
        if (dal <= AipEspanso.CicloInVigore(DiOggi)) return $"non più in vigore dal {Data(AipEspanso.CicloInVigore(Dopo(ciclo)))}";
        return $"non ancora in vigore: dal {Data(dal)}";
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Il ciclo dopo: il primo il cui giorno d'entrata viene 28 giorni dopo.</summary>
    internal static string Dopo(string ciclo) => CicloAirac.Di(AipEspanso.CicloInVigore(ciclo).AddDays(28)).Nome;
}

/// <summary>I cicli dell'AIP nel tempo: quale si apre, quale si toglie (carta «L'AIP intero», passo 8).</summary>
public static class CicliDellAip
{
    /// <summary>Lo stato dei cicli <paramref name="presenti"/> (i nomi, <c>2611</c>) il giorno <paramref name="oggi"/>.</summary>
    public static StatoDeiCicli Di(IEnumerable<string> presenti, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(presenti);
        var diOggi = CicloAirac.Di(oggi).Nome;
        var cicli = presenti.Where(CicloAirac.Valido).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var futuri = cicli.Where(c => AipEspanso.CicloInVigore(c) > oggi).ToList();
        return new StatoDeiCicli(
            diOggi,
            cicli.Contains(diOggi) ? diOggi : null,
            futuri.FirstOrDefault(c => c == StatoDeiCicli.Dopo(diOggi)),
            [.. futuri.Where(c => c != StatoDeiCicli.Dopo(diOggi))],
            [.. cicli.Where(c => AipEspanso.CicloInVigore(c) <= oggi && c != diOggi)]);
    }

    /// <summary>
    /// Toglie un ciclo superato: la sua cartella dei PDF (<c>AIP 2611</c>, i PDF si riscaricano) e, se l'AIP espanso non è un
    /// repo, la sua cartella del modello. Nel repo dell'AIP espanso il ciclo resta: sta sul suo ramo, con la sua storia.
    /// Torna null se è andata, o il perché; non tocca niente se il ciclo non si può togliere.
    /// </summary>
    public static string? Togli(string cartellaDeiPdf, string? cartellaDellAipEspanso, string ciclo, DateOnly oggi)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaDeiPdf);
        ArgumentException.ThrowIfNullOrEmpty(ciclo);
        var stato = Di(Presenti(cartellaDeiPdf, cartellaDellAipEspanso), oggi);
        if (!stato.Superati.Contains(ciclo))
            return $"Il {ciclo} non è un ciclo superato: si tolgono solo quelli che non sono più in vigore.";
        if (stato.InVigore is null)
            return $"Il Lab non ha ancora il ciclo in vigore ({stato.DiOggi}): il {ciclo} resta finché non c'è.";
        var pdf = EmendamentoDellAip.CartellaDelCiclo(cartellaDeiPdf, ciclo);
        if (Directory.Exists(pdf))
            Directory.Delete(pdf, recursive: true);
        if (cartellaDellAipEspanso is not null && !RepoDellAip.EUnRepo(cartellaDellAipEspanso))
        {
            var modello = Path.Combine(cartellaDellAipEspanso, ciclo);
            if (Directory.Exists(modello))
                Directory.Delete(modello, recursive: true);
        }
        return null;
    }

    /// <summary>I cicli che il Lab ha: le cartelle dei PDF (<c>AIP 2611</c>) e i modelli dell'AIP espanso (<c>2611\li.json</c>).</summary>
    public static IReadOnlyList<string> Presenti(string? cartellaDeiPdf, string? cartellaDellAipEspanso)
    {
        var cicli = new List<string>();
        if (cartellaDeiPdf is not null)
            cicli.AddRange(EmendamentoDellAip.CicliNellaCartella(cartellaDeiPdf));
        cicli.AddRange(ConIlModello(cartellaDellAipEspanso));
        return [.. cicli.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>I cicli che hanno il modello nell'AIP espanso.</summary>
    public static IReadOnlyList<string> ConIlModello(string? cartellaDellAipEspanso) =>
        cartellaDellAipEspanso is not null && Directory.Exists(cartellaDellAipEspanso)
            ? [.. Directory.EnumerateDirectories(cartellaDellAipEspanso).Select(Path.GetFileName).OfType<string>()
                .Where(c => CicloAirac.Valido(c) && File.Exists(AipEspanso.File(cartellaDellAipEspanso, c))).Order(StringComparer.Ordinal)]
            : [];
}

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

/// <summary>
/// Un ciclo nella pagina «Lettura e consegna»: che cos'è oggi, i suoi PDF (<c>null</c>: la cartella non c'è), quando è
/// stato scritto il modello (<c>null</c>: non c'è), la data più recente delle pagine lette, se si può togliere, se è quello
/// su cui il Lab lavora.
/// </summary>
public sealed record RigaDelCiclo(string Ciclo, string Dice, int? Pdf, DateTime? Scritto, DateOnly? UltimaData, int Pagine, bool DaTogliere, bool DaAprire);

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

    /// <summary>
    /// I PDF dell'AIP messi nella cartella dei PDF fuori dalle cartelle dei cicli (pagina «Lettura e consegna», 22 ottobre:
    /// il committente ci aveva copiato <c>ENR</c>, <c>AD 2</c>, <c>AD 3</c> del 2611, e il Lab li aveva letti come 2610).
    /// </summary>
    public static IReadOnlyList<string> PdfSciolti(string? cartellaDeiPdf)
    {
        if (cartellaDeiPdf is null || !Directory.Exists(cartellaDeiPdf))
            return [];
        var cicli = EmendamentoDellAip.CicliNellaCartella(cartellaDeiPdf)
            .Select(c => EmendamentoDellAip.CartellaDelCiclo(cartellaDeiPdf, c) + Path.DirectorySeparatorChar).ToList();
        return [.. PdfDellAip.Elenco(cartellaDeiPdf).Where(f => !cicli.Any(c => f.StartsWith(c, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>
    /// Mette i PDF sciolti nella cartella del loro ciclo (<c>AIP 2611</c>), con le loro sottocartelle; le cartelle rimaste
    /// vuote se ne vanno. Torna null se è andata, o il perché; con la cartella del ciclo che c'è già non sposta niente.
    /// </summary>
    public static string? MettiNelCiclo(string cartellaDeiPdf, string ciclo, IReadOnlyList<string> sciolti)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaDeiPdf);
        ArgumentNullException.ThrowIfNull(sciolti);
        var dove = EmendamentoDellAip.CartellaDelCiclo(cartellaDeiPdf, ciclo);
        if (Directory.Exists(dove))
            return $"C'è già la cartella «AIP {ciclo}»: questi PDF mettili lì dentro tu, o togli quella cartella.";
        var radice = Path.GetFullPath(cartellaDeiPdf);
        var cartelle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in sciolti)
        {
            var relativo = Path.GetRelativePath(radice, Path.GetFullPath(file));
            var nuovo = Path.Combine(dove, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(nuovo)!);
            File.Move(file, nuovo);
            for (var c = Path.GetDirectoryName(Path.GetFullPath(file)); c is not null && c.Length > radice.Length; c = Path.GetDirectoryName(c))
                cartelle.Add(c);
        }
        // Le più profonde prima; una cartella con dentro altro (un file dell'AOD) resta.
        foreach (var c in cartelle.OrderByDescending(c => c.Length))
            if (Directory.Exists(c) && !Directory.EnumerateFileSystemEntries(c).Any())
                Directory.Delete(c);
        return null;
    }

    /// <summary>
    /// Una riga per ciclo che il Lab ha (pagina «Lettura e consegna»): quanti PDF, se c'è il modello e quando è stato
    /// scritto, la data più recente delle sue pagine.
    /// </summary>
    public static IReadOnlyList<RigaDelCiclo> Righe(string? cartellaDeiPdf, string? cartellaDellAipEspanso, DateOnly oggi)
    {
        var stato = Di(Presenti(cartellaDeiPdf, cartellaDellAipEspanso), oggi);
        return [.. new[] { stato.InVigore, stato.Successivo }.OfType<string>().Concat(stato.Prossimi).Concat(stato.Superati)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(ciclo =>
            {
                var pdf = cartellaDeiPdf is null ? null : EmendamentoDellAip.CartellaDelCiclo(cartellaDeiPdf, ciclo);
                var modello = cartellaDellAipEspanso is null ? null : AipEspanso.File(cartellaDellAipEspanso, ciclo);
                var indice = cartellaDellAipEspanso is null ? null : IndiceDellePagine.File(cartellaDellAipEspanso, ciclo);
                DateOnly? ultima = null;
                var pagine = 0;
                if (indice is not null && File.Exists(indice))
                {
                    try
                    {
                        var lette = IndiceDellePagine.Leggi(File.ReadAllText(indice));
                        ultima = lette.Max(p => p.Effective);
                        pagine = lette.Count;
                    }
                    catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
                    {
                        // Un indice illeggibile: la riga lo dice senza data, la lettura lo riscrive.
                    }
                }
                return new RigaDelCiclo(ciclo, stato.Dice(ciclo),
                    pdf is not null && Directory.Exists(pdf) ? PdfDellAip.Elenco(pdf).Count : null,
                    modello is not null && File.Exists(modello) ? File.GetLastWriteTime(modello) : null,
                    ultima, pagine, stato.DaTogliere.Contains(ciclo), ciclo == stato.DaAprire);
            })];
    }

    /// <summary>I cicli che hanno il modello nell'AIP espanso.</summary>
    public static IReadOnlyList<string> ConIlModello(string? cartellaDellAipEspanso) =>
        cartellaDellAipEspanso is not null && Directory.Exists(cartellaDellAipEspanso)
            ? [.. Directory.EnumerateDirectories(cartellaDellAipEspanso).Select(Path.GetFileName).OfType<string>()
                .Where(c => CicloAirac.Valido(c) && File.Exists(AipEspanso.File(cartellaDellAipEspanso, c))).Order(StringComparer.Ordinal)]
            : [];
}

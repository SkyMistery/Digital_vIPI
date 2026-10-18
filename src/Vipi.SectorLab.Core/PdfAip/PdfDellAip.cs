using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Com'è andato «Prendi i PDF da Download»: quelli spostati (nuovi o al posto di uno con lo stesso nome).</summary>
public sealed record PresaDaDownload(IReadOnlyList<string> Nuovi, IReadOnlyList<string> Sostituiti)
{
    public int Quanti => Nuovi.Count + Sostituiti.Count;
}

/// <summary>
/// La cartella dei PDF dell'AIP (carta F6 §3, «I PDF: chi li mette nella cartella»): l'AOD ce li mette a mano, o il Lab
/// li <b>sposta</b> dalla cartella Download di Windows su un tasto. Solo i PDF dell'AIP, riconosciuti dal nome che ENAV
/// dà ai file (<c>LI-ENR 5.1.2.pdf</c>, <c>LI-AD 2 LIRF 1.pdf</c>, <c>LI-Cover Page.pdf</c>). Il Lab non scarica niente
/// dal sito di ENAV.
/// <para>I PDF del ciclo prima non si archiviano (committente, 18 settembre): uno con lo stesso nome prende il posto del
/// vecchio.</para>
/// </summary>
public static partial class PdfDellAip
{
    [GeneratedRegex(@"^LI-(?:ENR|AD|GEN|Cover)\b.*\.pdf$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxNome();

    /// <summary>Vero se il nome è quello di un PDF dell'AIP italiano.</summary>
    public static bool EDellAip(string nomeDelFile) => RxNome().IsMatch(Path.GetFileName(nomeDelFile));

    /// <summary>La cartella Download dell'utente di Windows (<c>%USERPROFILE%\Downloads</c>).</summary>
    public static string Download() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>I PDF dell'AIP nella cartella e nelle sue sottocartelle (l'AOD può tenerli divisi per sezione), in ordine di nome.</summary>
    public static IReadOnlyList<string> Elenco(string cartella) =>
        Directory.Exists(cartella)
            ? Directory.EnumerateFiles(cartella, "*.pdf", SearchOption.AllDirectories)
                .Where(EDellAip)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

    /// <summary>
    /// Sposta i PDF dell'AIP da <paramref name="download"/> (solo il primo livello: Download non si esplora) in
    /// <paramref name="cartella"/>. Un file con lo stesso nome già nella cartella, anche in una sottocartella, viene
    /// sostituito <b>lì dov'è</b>: l'ordine che l'AOD si è dato resta.
    /// </summary>
    public static PresaDaDownload PrendiDaDownload(string download, string cartella)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartella);
        Directory.CreateDirectory(cartella);
        var esistenti = Directory.EnumerateFiles(cartella, "*.pdf", SearchOption.AllDirectories)
            .GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var nuovi = new List<string>();
        var sostituiti = new List<string>();
        if (!Directory.Exists(download))
            return new PresaDaDownload(nuovi, sostituiti);

        foreach (var file in Directory.EnumerateFiles(download, "*.pdf", SearchOption.TopDirectoryOnly).Where(EDellAip).Order(StringComparer.OrdinalIgnoreCase).ToList())
        {
            var nome = Path.GetFileName(file);
            if (esistenti.TryGetValue(nome, out var dove))
            {
                File.Move(file, dove, overwrite: true);
                sostituiti.Add(nome);
            }
            else
            {
                File.Move(file, Path.Combine(cartella, nome));
                nuovi.Add(nome);
            }
        }
        return new PresaDaDownload(nuovi, sostituiti);
    }
}

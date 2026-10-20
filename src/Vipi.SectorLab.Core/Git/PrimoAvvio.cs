using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// Il primo avvio di un AOD (20 ottobre, chiesto dal committente: «tutto in un posto solo»): una cartella di lavoro, e
/// dentro il sector, l'AIP espanso e la cartella dei PDF dell'AIP. Il Lab prende i due repo dai link che l'AOD incolla
/// e crea la cartella dei PDF, che non sta in nessun repo (peso, copyright ENAV).
/// <para>Si può rifare: un repo già preso in quella cartella si salta, così un passo fallito (la rete, le credenziali)
/// si riprova senza ricominciare da capo.</para>
/// </summary>
public static class PrimoAvvio
{
    public const string CartellaDelSector = "sector";
    public const string CartellaDellAipEspanso = "aip-espanso";
    public const string CartellaDeiPdf = "pdf-aip";

    /// <summary>Le tre cartelle dentro quella di lavoro.</summary>
    public sealed record Cartelle(string Sector, string AipEspanso, string Pdf);

    /// <summary>Le tre cartelle dentro <paramref name="lavoro"/>, che deve essere scritta per intero.</summary>
    public static Cartelle CartelleDi(string lavoro)
    {
        string radice = Path.GetFullPath(lavoro.Trim());
        return new Cartelle(
            Path.Combine(radice, CartellaDelSector),
            Path.Combine(radice, CartellaDellAipEspanso),
            Path.Combine(radice, CartellaDeiPdf));
    }

    /// <summary>
    /// I sector dell'ambiente di lavoro (committente, 20 ottobre: «più è isolato l'ambiente di lavoro meno casino
    /// facciamo»): le sottocartelle che sono un sector preso, <see cref="CartellaDelSector"/> per primo, poi le altre
    /// in ordine (il ciclo dopo, una prova).
    /// </summary>
    public static IReadOnlyList<string> SectorDellAmbiente(string? lavoro)
    {
        if (string.IsNullOrWhiteSpace(lavoro) || !Directory.Exists(lavoro))
            return [];
        return [.. Directory.EnumerateDirectories(lavoro)
            .Where(c => !string.Equals(Path.GetFileName(c), CartellaDellAipEspanso, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(Path.GetFileName(c), CartellaDeiPdf, StringComparison.OrdinalIgnoreCase))
            .Where(SectorGiaPreso)
            .OrderBy(c => string.Equals(Path.GetFileName(c), CartellaDelSector, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(c => Path.GetFileName(c), StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Vero se <paramref name="cartella"/> sta dentro l'ambiente di lavoro (o è una sua sottocartella).</summary>
    public static bool NellAmbiente(string lavoro, string cartella)
    {
        string madre = Path.GetFullPath(lavoro.Trim()).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        string figlia = Path.GetFullPath(cartella.Trim()).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return figlia.StartsWith(madre, StringComparison.OrdinalIgnoreCase) && figlia.Length > madre.Length;
    }

    /// <summary>
    /// Perché non si può prendere un altro sector con quel nome nell'ambiente, o null. Il nome è una cartella sola
    /// (lettere, cifre, <c>-</c>, <c>_</c>, <c>.</c>), non quella dell'AIP espanso o dei PDF, e non già occupata.
    /// </summary>
    public static string? PercheNonVaUnAltroSector(string lavoro, string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Dai un nome al sector (per esempio «ciclo-2611» o «prova»): diventa la sua cartella nell'ambiente.";
        string pulito = nome.Trim();
        if (pulito is "." or ".." || !pulito.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            return "Il nome è una cartella sola: lettere, cifre, «-», «_» e «.».";
        if (string.Equals(pulito, CartellaDellAipEspanso, StringComparison.OrdinalIgnoreCase)
            || string.Equals(pulito, CartellaDeiPdf, StringComparison.OrdinalIgnoreCase))
            return $"«{pulito}» è la cartella dell'AIP espanso o dei PDF: scegli un altro nome.";
        string dove = Path.Combine(lavoro, pulito);
        if (NonVuota(dove))
            return SectorGiaPreso(dove)
                ? $"Il sector «{pulito}» c'è già nell'ambiente: aprilo dall'elenco."
                : $"{dove} c'è già e non è un sector: scegli un altro nome.";
        return null;
    }

    /// <summary>Vero se nella cartella c'è già il sector preso (un clone con <c>SectorFiles</c>).</summary>
    public static bool SectorGiaPreso(string cartella) =>
        RepoDellAip.EUnRepo(cartella) && Directory.Exists(Path.Combine(cartella, PresaDelSector.CartellaDaPrendere));

    /// <summary>Perché non si può preparare il primo avvio lì con quei link, o null.</summary>
    public static string? PercheNonVa(string? lavoro, string? indirizzoDelSector, string? indirizzoDellAip)
    {
        if (string.IsNullOrWhiteSpace(lavoro))
            return "Scegli la cartella di lavoro: dentro il Lab mette il sector, l'AIP espanso e i PDF.";
        if (!Path.IsPathFullyQualified(lavoro.Trim()))
            return "La cartella di lavoro va scritta per intero (D:\\IVAO).";
        if (File.Exists(lavoro.Trim()))
            return "Lì c'è un file, non una cartella.";
        if (IndirizzoDelRepo.PercheNonVa(indirizzoDelSector) is { } sector)
            return "Sector: " + sector;
        if (IndirizzoDelRepo.PercheNonVa(indirizzoDellAip) is { } aip)
            return "AIP espanso: " + aip;

        var cartelle = CartelleDi(lavoro);
        if (!SectorGiaPreso(cartelle.Sector) && NonVuota(cartelle.Sector))
            return $"{cartelle.Sector} c'è già e non è il sector preso dal repo: scegli un'altra cartella di lavoro.";
        if (!RepoDellAip.EUnRepo(cartelle.AipEspanso) && NonVuota(cartelle.AipEspanso))
            return $"{cartelle.AipEspanso} c'è già e non è l'AIP espanso preso dal repo: scegli un'altra cartella di lavoro.";
        if (File.Exists(cartelle.Pdf))
            return $"{cartelle.Pdf} è un file: scegli un'altra cartella di lavoro.";
        return null;
    }

    private static bool NonVuota(string cartella) =>
        File.Exists(cartella) || (Directory.Exists(cartella) && Directory.EnumerateFileSystemEntries(cartella).Any());

    /// <summary>
    /// Prepara tutto: il sector, l'AIP espanso, la cartella dei PDF. <paramref name="passo"/> dice a che punto è
    /// (per la schermata). Torna le cartelle se è andata, o il perché.
    /// </summary>
    public static async Task<(Cartelle? Cartelle, string? Perche)> PreparaAsync(
        Git git, string lavoro, string indirizzoDelSector, string indirizzoDellAip, Action<string>? passo = null,
        CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (PercheNonVa(lavoro, indirizzoDelSector, indirizzoDellAip) is { } perche)
            return (null, perche);
        var cartelle = CartelleDi(lavoro);

        if (!SectorGiaPreso(cartelle.Sector))
        {
            passo?.Invoke("Prendo il sector dal repo…");
            if (await PresaDelSector.PrendiAsync(git, indirizzoDelSector, cartelle.Sector, annulla).ConfigureAwait(false) is { } noSector)
                return (null, "Sector: " + noSector);
        }

        if (!RepoDellAip.EUnRepo(cartelle.AipEspanso))
        {
            passo?.Invoke("Prendo l'AIP espanso dal repo…");
            if (await RepoDellAip.PrendiAsync(git, indirizzoDellAip, cartelle.AipEspanso, annulla).ConfigureAwait(false) is { } noAip)
                return (null, "AIP espanso: " + noAip);
        }

        passo?.Invoke("Creo la cartella dei PDF…");
        Directory.CreateDirectory(cartelle.Pdf);
        return (cartelle, null);
    }
}

namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// «Prendi il sector» (carta F4, slice 2): la copia parziale del repo per un AOD che non ce l'ha. Si scarica solo
/// <c>SectorFiles</c> e i contenuti di oggi (<c>--filter=blob:none --sparse</c>): sul repo vero 3 secondi e 3,7 MB
/// invece dei 412 MB della cronologia intera, che porta <c>Aurora.exe</c> e le DLL (misura del 17 ottobre, §1).
/// </summary>
public static class PresaDelSector
{
    /// <summary>
    /// Il repo da cui si prende, finché l'AOD non ne sceglie un altro nelle Impostazioni: il **fork di prova**. 🔴 Carta
    /// F4: il Lab lavora sul fork finché il committente non dice di passare a <c>ivao-italy/it-aurora-sector</c>.
    /// </summary>
    public const string IndirizzoDiBase = "https://github.com/SkyMistery/it-aurora-sector-test.git";

    /// <summary>La sola cartella che si scarica: il resto del repo (Aurora, DLL, suoni) non serve al Lab.</summary>
    public const string CartellaDaPrendere = "SectorFiles";

    /// <summary>Perché non si può prendere il sector in quella cartella da quell'indirizzo, o null.</summary>
    public static string? PercheNonVa(string? indirizzo, string? cartella)
    {
        if (string.IsNullOrWhiteSpace(indirizzo))
            return "Manca l'indirizzo del repo del sector.";
        if (string.IsNullOrWhiteSpace(cartella))
            return "Manca la cartella dove metterlo.";
        if (!Path.IsPathFullyQualified(cartella.Trim()))
            return "La cartella va scritta per intero (D:\\…\\it-aurora-sector).";
        string pulita = cartella.Trim();
        if (File.Exists(pulita))
            return "Lì c'è un file, non una cartella.";
        if (Directory.Exists(pulita) && Directory.EnumerateFileSystemEntries(pulita).Any())
            return "La cartella non è vuota: il sector va in una cartella nuova (o vuota), per non mescolarlo con altro.";
        return null;
    }

    /// <summary>
    /// Prende il sector: clona in parte, poi scarica <c>SectorFiles</c>. Torna null se è andata, o il perché. Se la
    /// clonazione va a metà la cartella resta com'è: il Lab non cancella niente.
    /// </summary>
    public static async Task<string?> PrendiAsync(Git git, string indirizzo, string cartella, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (PercheNonVa(indirizzo, cartella) is { } perche)
            return perche;

        string dove = Path.GetFullPath(cartella.Trim());
        string sopra = Path.GetDirectoryName(dove) ?? dove;
        Directory.CreateDirectory(sopra);

        var clone = await git.EseguiAsync(sopra, ["clone", "--quiet", "--filter=blob:none", "--sparse", indirizzo.Trim(), dove], annulla).ConfigureAwait(false);
        if (!clone.Riuscito)
            return clone.Codice == -1 ? clone.Errore : $"Il repo non si è lasciato prendere: {clone.Errore}";

        var parte = await git.EseguiAsync(dove, ["sparse-checkout", "add", CartellaDaPrendere], annulla).ConfigureAwait(false);
        if (!parte.Riuscito)
            return $"Preso il repo, ma non la cartella {CartellaDaPrendere}: {parte.Errore}";
        if (!Directory.Exists(Path.Combine(dove, CartellaDaPrendere)))
            return $"Il repo non ha la cartella {CartellaDaPrendere}: è davvero quello del sector?";
        return null;
    }
}

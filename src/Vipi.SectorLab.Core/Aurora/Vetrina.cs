using System.Globalization;
using System.Text.Json;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>Che cosa c'è in vetrina: da quale sector, quale ramo e commit, quando ce l'ha messo il Lab.</summary>
public sealed record InVetrina(string Sorgente, string? Ramo, string? Commit, DateTimeOffset Quando);

/// <summary>Com'è andato «Porta in Aurora»: i file copiati, quelli tolti, quelli rimasti uguali.</summary>
public sealed record EsitoDellaVetrina(int Copiati, int Tolti, int Uguali);

/// <summary>
/// La vetrina (carta F5, slice 1; D1): una cartella fissa a cui l'AOD punta una volta il «secondo sector» di Aurora, e
/// in cui il Lab mette, con «Porta in Aurora», la <c>SectorFiles</c> del sector aperto — come specchio: i file uguali
/// restano, i cambiati si copiano, quelli che il sector non ha più si tolgono. Così si lavora su più sector (il ciclo
/// dopo, una prova) e si sceglie nel Lab quale vedere, senza toccare le impostazioni di Aurora.
/// <para>🔴 La vetrina è del Lab: la riconosce dal file segno (<see cref="Segno"/>). Il Lab non specchia mai in una
/// cartella che non ha il segno e non è vuota, in un clone git, né nella <c>SectorFiles</c> pubblicata di Aurora.</para>
/// </summary>
public static class Vetrina
{
    /// <summary>Il file segno della vetrina, nella sua cartella: dice anche che cosa c'è dentro.</summary>
    public const string Segno = ".vetrina-del-lab";

    /// <summary>La cartella che Aurora legge dentro la vetrina: quella da mettere nel suo «secondo sector».</summary>
    public static string SectorFilesDi(string vetrina) => Path.Combine(vetrina, "SectorFiles");

    /// <summary>
    /// Perché quella cartella non può fare da vetrina, o null. <paramref name="pubblicata"/>: la <c>SectorFiles</c> che
    /// Aurora usa per il sector pubblicato (<c>%APPDATA%\IVAO\AuroraWindows\SectorFiles</c>), che non si tocca.
    /// </summary>
    public static string? PercheNonVa(string? vetrina, string? pubblicata)
    {
        if (string.IsNullOrWhiteSpace(vetrina))
            return "Manca la cartella della vetrina: scegline una nelle Impostazioni.";
        if (!Path.IsPathFullyQualified(vetrina.Trim()))
            return "La vetrina va scritta per intero (D:\\…\\Aurora-vetrina).";
        string piena = Path.GetFullPath(vetrina.Trim());
        if (pubblicata is not null && (Dentro(piena, Path.GetFullPath(pubblicata)) || Dentro(Path.GetFullPath(pubblicata), piena)))
            return "Quella è la cartella del sector pubblicato di Aurora: la vetrina è un'altra.";
        for (var cartella = new DirectoryInfo(piena); cartella is not null; cartella = cartella.Parent)
        {
            if (Directory.Exists(Path.Combine(cartella.FullName, ".git")) || File.Exists(Path.Combine(cartella.FullName, ".git")))
                return "Quella cartella sta in un clone git: la vetrina è una cartella a parte, che il Lab può riempire e svuotare.";
        }

        if (Directory.Exists(piena) && !File.Exists(Path.Combine(piena, Segno)) && Directory.EnumerateFileSystemEntries(piena).Any())
            return "Quella cartella non è vuota e non è una vetrina del Lab: il Lab non ci specchia dentro, per non cancellare niente.";
        return null;
    }

    /// <summary>Che cosa c'è in vetrina, dal file segno; null se la vetrina è vuota o non è del Lab.</summary>
    public static InVetrina? Contenuto(string vetrina)
    {
        string segno = Path.Combine(vetrina, Segno);
        try
        {
            return File.Exists(segno) && new FileInfo(segno).Length > 0 ? JsonSerializer.Deserialize<InVetrina>(File.ReadAllText(segno)) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// «Porta in Aurora»: la <c>SectorFiles</c> di <paramref name="sector"/> (la radice del sector aperto) in vetrina, come
    /// specchio. Torna l'esito, o il perché in <paramref name="perche"/>.
    /// </summary>
    public static EsitoDellaVetrina? Porta(string sector, string vetrina, string? pubblicata, string? ramo, string? commit, DateTimeOffset quando, out string? perche)
    {
        ArgumentException.ThrowIfNullOrEmpty(sector);
        perche = PercheNonVa(vetrina, pubblicata);
        if (perche is not null)
            return null;
        string da = Path.Combine(sector, "SectorFiles");
        if (!Directory.Exists(da))
        {
            perche = $"Nel sector aperto non c'è SectorFiles ({da}).";
            return null;
        }

        string piena = Path.GetFullPath(vetrina.Trim());
        string a = SectorFilesDi(piena);
        Directory.CreateDirectory(a);
        // Il segno prima di tutto: una vetrina riempita a metà resta riconoscibile, e la prossima volta si finisce.
        File.WriteAllText(Path.Combine(piena, Segno), "");

        int copiati = 0, uguali = 0, tolti = 0;
        var presenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(da, "*", SearchOption.AllDirectories))
        {
            string relativo = Path.GetRelativePath(da, file);
            presenti.Add(relativo);
            string dove = Path.Combine(a, relativo);
            if (File.Exists(dove) && Uguali(file, dove))
            {
                uguali++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dove)!);
            File.Copy(file, dove, overwrite: true);
            copiati++;
        }

        foreach (string file in Directory.EnumerateFiles(a, "*", SearchOption.AllDirectories).ToList())
        {
            if (presenti.Contains(Path.GetRelativePath(a, file)))
                continue;
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
            tolti++;
        }

        foreach (string cartella in Directory.EnumerateDirectories(a, "*", SearchOption.AllDirectories).OrderByDescending(c => c.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(cartella).Any())
                Directory.Delete(cartella);
        }

        File.WriteAllText(Path.Combine(piena, Segno), JsonSerializer.Serialize(new InVetrina(Path.GetFullPath(sector), ramo, commit, quando)));
        return new EsitoDellaVetrina(copiati, tolti, uguali);
    }

    /// <summary>
    /// Il «secondo sector» di Aurora: la sola riga <c>SecondSector=</c> della sezione <c>[SECTOR]</c> del suo
    /// <c>Settings\profile.ini</c>. 🔴 Lo stesso file tiene l'accesso IVAO del controllore: non si legge altro, non si
    /// scrive, non si registra. Null se il file o la riga non ci sono.
    /// </summary>
    public static string? SecondoSector(string profileIni)
    {
        try
        {
            if (!File.Exists(profileIni))
                return null;
            bool inSector = false;
            foreach (string riga in File.ReadLines(profileIni))
            {
                string pulita = riga.Trim();
                if (pulita.StartsWith('['))
                {
                    inSector = pulita.Equals("[SECTOR]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inSector && pulita.StartsWith("SecondSector=", StringComparison.OrdinalIgnoreCase))
                    return pulita["SecondSector=".Length..].Trim() is { Length: > 0 } valore ? valore : null;
            }

            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Vero se il «secondo sector» di Aurora è la <c>SectorFiles</c> della vetrina.</summary>
    public static bool AuroraGuarda(string? secondoSector, string vetrina)
        => secondoSector is not null
           && string.Equals(Path.GetFullPath(secondoSector).TrimEnd('\\', '/'), Path.GetFullPath(SectorFilesDi(vetrina)).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Le impostazioni di Aurora (<c>%APPDATA%\IVAO\AuroraWindows\Settings\profile.ini</c>) e il sector pubblicato.</summary>
    public static (string ProfileIni, string Pubblicata) DiAurora()
    {
        string aurora = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IVAO", "AuroraWindows");
        return (Path.Combine(aurora, "Settings", "profile.ini"), Path.Combine(aurora, "SectorFiles"));
    }

    private static bool Dentro(string figlia, string madre)
        => (figlia.TrimEnd('\\', '/') + Path.DirectorySeparatorChar).StartsWith(madre.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool Uguali(string a, string b)
    {
        var fa = new FileInfo(a);
        var fb = new FileInfo(b);
        if (fa.Length != fb.Length)
            return false;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    /// <summary>L'ora di «Porta in Aurora» come la dice la barra (ora locale, minuti).</summary>
    public static string Ora(DateTimeOffset quando) => quando.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
}

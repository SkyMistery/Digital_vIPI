using System.Security.Cryptography;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// L'impronta di un file (SHA-256 dei byte) com'era all'apertura. Il salvataggio la riconfronta col disco: se è
/// cambiata (un <c>git pull</c>, un altro editor, un collega sulla stessa cartella) non si scrive (carta F3 §2.4).
/// </summary>
public readonly record struct Impronta(string Esadecimale)
{
    public static Impronta Di(ReadOnlySpan<byte> byteDelFile) => new(Convert.ToHexString(SHA256.HashData(byteDelFile)));

    /// <summary>L'impronta del file com'è ADESSO sul disco; nulla se il file non c'è più.</summary>
    public static Impronta? DelFile(string percorso)
    {
        try
        {
            return Di(File.ReadAllBytes(percorso));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public override string ToString() => Esadecimale;
}

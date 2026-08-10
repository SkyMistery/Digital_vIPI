using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;

namespace Vipi.Host;

/// <summary>
/// Cache-busting per gli asset statici: sostituisce <c>@Assets[...]</c>, che è .NET 9+ e su net8 non esiste
/// (ADR-0007 §D4-ter).
///
/// <para><b>L'impronta è del contenuto del singolo file</b>, non della build. Si legge il file dal provider
/// di <c>UseStaticFiles</c> — lo stesso che poi lo serve, quindi non c'è modo che le due cose divergano — se
/// ne prende lo SHA-256 e i primi 8 caratteri finiscono nell'URL. Conseguenza pratica: dopo un deploy il
/// browser riscarica **solo i file davvero cambiati**, e per gli altri l'URL resta identico, quindi la copia
/// in cache resta valida.</para>
///
/// <para><b>Perché non basta un'impronta sola per tutti.</b> La prima versione usava il MVID dell'assembly
/// della RCL, che cambia a ogni compilazione: bastava ricompilare per invalidare CSS, JS e mappe insieme,
/// anche se identici byte per byte. Era il compromesso dichiarato nel passaggio a net8 (voce C4 dei lavori
/// aperti); questo lo toglie senza aspettare EF Core 10.</para>
///
/// <para><b>Il ripiego conta quanto il caso buono.</b> Se il file non si trova — percorso sbagliato, provider
/// non inizializzato, asset servito da altrove — si torna al MVID invece di lasciare l'URL nudo: un URL senza
/// versione è il guasto vero, perché lascia in cache un CSS vecchio dopo un aggiornamento. Sbagliare
/// invalidando troppo è innocuo; sbagliare invalidando troppo poco no.</para>
///
/// <para>Le impronte si calcolano una volta sola per percorso e restano in memoria: i file statici non
/// cambiano sotto un processo in esecuzione.</para>
/// </summary>
public static class AssetVersion
{
    private static readonly ConcurrentDictionary<string, string> Impronte = new(StringComparer.Ordinal);
    private static IFileProvider? _file;

    /// <summary>
    /// Da chiamare all'avvio con lo stesso provider che serve i file statici
    /// (<c>app.Environment.WebRootFileProvider</c>). Senza, tutto funziona lo stesso ma si ricade sul MVID.
    /// </summary>
    public static void Initialize(IFileProvider provider)
    {
        _file = provider;
        Impronte.Clear();
    }

    /// <summary>Suffisso di ripiego, già completo di <c>?</c>: il MVID dell'assembly della RCL.</summary>
    public static string Query { get; } = CalcolaMvid();

    /// <summary>URL di un asset con la sua impronta. Sostituisce <c>@Assets["percorso"]</c>.</summary>
    public static string Url(string path) => path + Impronte.GetOrAdd(path, Impronta);

    private static string Impronta(string path)
    {
        try
        {
            // Il percorso negli attributi href/src è relativo alla radice web: al provider serve con lo slash.
            var info = _file?.GetFileInfo("/" + path.TrimStart('/'));
            if (info is null || !info.Exists) return Query;

            using var stream = info.CreateReadStream();
            var hash = SHA256.HashData(LeggiTutto(stream));
            return "?v=" + Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }
        catch
        {
            return Query;
        }
    }

    /// <summary>Il provider può restituire uno stream non seekable: <c>SHA256.HashData</c> vuole i byte.</summary>
    private static byte[] LeggiTutto(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string CalcolaMvid()
    {
        try
        {
            var mvid = Vipi.Hosting.VipiModuleExtensions.UiAssembly.ManifestModule.ModuleVersionId;
            return "?v=" + mvid.ToString("N")[..8];
        }
        catch
        {
            return "";
        }
    }
}

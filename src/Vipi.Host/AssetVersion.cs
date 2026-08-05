using System.Reflection;

namespace Vipi.Host;

/// <summary>
/// Sostituto del cache-busting che su .NET 9+ dava <c>@Assets[...]</c>, non disponibile su net8.
///
/// <para><b>Cosa si è perso.</b> <c>MapStaticAssets</c> calcola a build-time l'impronta del <i>contenuto</i>
/// di ogni file e la mette nell'URL, quindi dopo un deploy il browser riscarica solo i file davvero
/// cambiati e può tenere gli altri come <c>immutable</c>. Qui l'impronta è una sola per tutti: il MVID
/// dell'assembly della RCL, che cambia a ogni build. Effetto pratico: dopo ogni deploy il browser
/// riscarica <b>tutti</b> gli asset, anche quelli identici a prima.</para>
///
/// <para>È il compromesso accettato per stare su net8 (ADR-0007 §D4-ter). Costa qualche centinaio di
/// kilobyte a deploy per utente, su un sito con pochi deploy e pochi utenti; in cambio il problema che
/// risolve — un CSS vecchio in cache dopo un aggiornamento — resta risolto, ed è quello che conta.
/// Sbagliare in questa direzione è innocuo, sbagliare nell'altra no.</para>
///
/// <para>Il MVID è preferibile alla versione dell'assembly perché cambia a <b>ogni</b> compilazione, anche
/// quando il numero di versione resta fermo — che è il caso normale qui, visto che non lo incrementiamo.</para>
/// </summary>
public static class AssetVersion
{
    /// <summary>
    /// Suffisso da appendere agli URL degli asset, già completo di <c>?</c>. Vuoto non capita mai, ma se
    /// il MVID non fosse leggibile si degrada a nessun suffisso invece di rompere la pagina.
    /// </summary>
    public static string Query { get; } = Calcola();

    private static string Calcola()
    {
        try
        {
            // L'assembly della RCL: è da lì che viene la quasi totalità di CSS e JS (_content/Vipi.Ui/...).
            var mvid = Vipi.Hosting.VipiModuleExtensions.UiAssembly.ManifestModule.ModuleVersionId;
            return "?v=" + mvid.ToString("N")[..8];
        }
        catch
        {
            return "";
        }
    }

    /// <summary>URL di un asset con il suffisso di versione. Sostituisce <c>@Assets["percorso"]</c>.</summary>
    public static string Url(string path) => path + Query;
}

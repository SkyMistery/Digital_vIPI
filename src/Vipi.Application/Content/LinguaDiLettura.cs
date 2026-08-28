namespace Vipi.Application.Content;

/// <summary>
/// Quali lingue esistono, e come si chiede all'indirizzo di cambiarle (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §7).
///
/// <para>
/// ⚠️ <b>Un posto solo.</b> Prima del 28 agosto 2026 l'elenco delle lingue stava dentro
/// <c>VipiModuleExtensions</c> (privato) e le chiavi della stringa di query dentro
/// <c>CultureCookieMiddleware</c> (private anch'esse): due conoscenze chiuse in due file che il <b>selettore
/// di lingua</b> non poteva vedere, perché la UI non dipende dall'hosting. Riscriverle nella barra avrebbe
/// creato una terza copia — e un giorno una delle tre avrebbe detto una cosa diversa dalle altre, in
/// silenzio: un tasto che offre una lingua che il server non serve non dà errore, ricarica la stessa pagina.
/// </para>
///
/// <para>
/// ⚠️ <b>La lingua vale per l'INTERFACCIA e per il DOCUMENTO insieme</b>, ed è una decisione, non una
/// semplificazione: un documento inglese dentro un'interfaccia italiana è una schermata mezza tradotta.
/// Il controllo è uno; chi lo tocca sposta tutta la pagina. Vedi <see cref="ReadingLanguageContext"/>, che
/// è il lato di chi COMPONE la prosa generata.
/// </para>
/// </summary>
public static class LinguaDiLettura
{
    /// <summary>
    /// Le lingue servite, la <b>prima è la predefinita</b>. L'ordine è quello in cui si mostrano nel
    /// selettore.
    /// </summary>
    public static readonly string[] Supportate = { "it", "en" };

    /// <summary>La lingua predefinita del sito: quella in cui si scrive quando nessuno chiede altro.</summary>
    public static string Predefinita => Supportate[0];

    /// <summary>
    /// Le chiavi che <c>QueryStringRequestCultureProvider</c> legge, coi suoi nomi di default. Chi ne mette
    /// una nell'indirizzo sta chiedendo la lingua <b>esplicitamente</b>, ed è l'unico caso in cui la scelta
    /// si ricorda nel cookie.
    /// </summary>
    public static readonly string[] ChiaviQuery = { "culture", "ui-culture" };

    /// <summary>La chiave da <b>scrivere</b> per chiedere una lingua: una sola, la prima.</summary>
    public static string ChiaveQuery => ChiaviQuery[0];

    /// <summary>Vero se questa è una lingua che il sito serve davvero.</summary>
    public static bool Supportata(string? lingua) =>
        lingua is not null &&
        Supportate.Contains(lingua, StringComparer.OrdinalIgnoreCase);
}

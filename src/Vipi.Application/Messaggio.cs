using System.Globalization;

namespace Vipi.Application;

/// <summary>
/// Un messaggio che va detto a una persona, nella lingua in cui sta leggendo.
///
/// <para>
/// Serve ai testi che nascono <b>nell'applicazione</b> e finiscono sotto gli occhi di chi modifica: gli
/// errori di validazione e i motivi per cui un'eliminazione è bloccata. Non possono passare dalle risorse —
/// quelle vivono in <c>Vipi.Ui</c>, e l'applicazione non dipende dalla UI — quindi si portano dietro
/// <b>tutte e due le lingue</b>, e qui si sceglie. È lo stesso schema del template dei coordinamenti e del
/// catalogo di ricerca della Guida (<c>docs/design/regole-lingua.md</c> R6-R7).
/// </para>
///
/// <para>
/// ⚠️ <b>La lingua si legge dalla cultura ambientale</b>, non la si fa passare di firma in firma. La ragione
/// è la stessa di <see cref="Content.ReadingLanguageContext"/>: fra chi conosce la lingua di lettura (la
/// richiesta) e chi compone il messaggio (un servizio in fondo a una catena di chiamate) ci sono cinque o
/// sei firme che dovrebbero portarsi dietro un parametro che riguarda uno solo dei loro chiamanti. E un
/// messaggio d'errore non è mai parte di uno snapshot congelato — quello è l'unico caso in cui la cultura
/// ambientale non basterebbe.
/// </para>
///
/// <para>
/// ⚠️ <b>L'inglese si scrive a mano</b>, come tutte le stringhe dell'applicazione: al motore automatico va
/// solo la prosa dei documenti. Un messaggio d'errore lo si scrive una volta e resta lì per anni.
/// </para>
/// </summary>
public static class Messaggio
{
    /// <summary>Il testo nella lingua di chi legge: <paramref name="en"/> in inglese, altrimenti <paramref name="it"/>.</summary>
    public static string Lingua(string it, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? en
            : it;
}

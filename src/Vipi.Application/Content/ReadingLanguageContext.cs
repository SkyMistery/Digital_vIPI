using System.Globalization;

namespace Vipi.Application.Content;

/// <summary>
/// «In che lingua va composta la <b>prosa generata</b>»: le frasi di coordinamento e tutto ciò che non è
/// testo editoriale ma codice che scrive (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>La prosa generata non passa dal traduttore automatico, e non deve.</b> Quelle frasi le scrive il
/// nostro codice: mandarle a un motore vorrebbe dire pagare per tradurre una cosa di cui possediamo già
/// entrambe le versioni, e accettarne la fraseologia invece della nostra. Si sceglie il <b>template</b>
/// giusto, non si traduce l'uscita.
/// </para>
///
/// <para>
/// <b>Perché un oggetto e non un parametro.</b> È la stessa ragione di <see cref="ShapeReleaseContext"/>,
/// e volutamente lo stesso pattern: fra chi conosce la lingua di lettura (la pagina) e chi compone la frase
/// (i servizi di derivazione) ci sono i provider di cattura e i servizi di vista — sei firme che
/// dovrebbero portarsi dietro un parametro che riguarda uno solo dei loro chiamanti.
/// </para>
///
/// <para>
/// ⚠️ È <b>scoped</b> come il DbContext: vale per una richiesta sola, non è uno stato globale.
/// </para>
/// </summary>
public sealed class ReadingLanguageContext
{
    private string? _forzata;

    /// <summary>
    /// La lingua in cui comporre. Fuori da una cattura è quella dell'<b>interfaccia</b>, che in Blazor
    /// Server il circuito imposta per sé.
    /// <para>⚠️ Si legge la cultura ambientale invece di farsela passare, e la ragione è che la prosa
    /// generata deve seguire <b>la stessa</b> chip della barra che decide tutto il resto della pagina: due
    /// sorgenti di verità sulla lingua darebbero una schermata mezza tradotta, che è esattamente ciò che
    /// questa funzione esiste per evitare.</para>
    /// </summary>
    public string Corrente =>
        _forzata ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

    /// <summary>
    /// Impone una lingua per la durata del blocco: serve al <b>congelamento</b>, dove non c'è nessun
    /// lettore e la lingua la decide chi pubblica, non chi guarda.
    /// </summary>
    public IDisposable Rendering(string lingua)
    {
        _forzata = lingua.ToLowerInvariant();
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ReadingLanguageContext _ctx;
        public Scope(ReadingLanguageContext ctx) => _ctx = ctx;
        public void Dispose() => _ctx._forzata = null;
    }
}

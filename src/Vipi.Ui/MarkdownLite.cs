using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Renderer Markdown minimale del contenuto editoriale: <b>grassetto</b>, <i>corsivo</i>, <u>sottolineato</u>,
/// elenchi puntati e numerati, a capo. HTML-encoded per sicurezza.
///
/// <para>
/// ⚠️ <b>Di link ne conosce UNO SOLO</b>: <c>[testo](allegato:slug)</c>. Non è una mancanza da colmare — è
/// il perimetro. Questo renderer <b>encoda e poi sostituisce con delle regex</b>, quindi aprirlo a
/// <c>[testo](url)</c> qualunque significherebbe far entrare nel contenuto editoriale un indirizzo
/// arbitrario, <c>javascript:</c> compreso, dentro un <c>href</c> che costruiamo noi. Uno schema solo,
/// riconosciuto per prefisso, e l'indirizzo lo compone <see cref="AttachmentRules"/> a partire dallo slug:
/// quel che sta nel testo è un <b>nome</b>, mai un indirizzo.
/// </para>
///
/// <para>
/// ⚠️ <b>Il testo si legge RIGA PER RIGA</b>, e questo è il cuore del formato, non un dettaglio di
/// implementazione. Prima era una catena di <c>Replace</c> sul testo intero, e da lì venivano tre difetti
/// che non si vedevano finché il contenuto restava di una frase sola:
/// <list type="bullet">
///   <item>i fine riga di Windows: <c>"\r\n\r\n"</c> non contiene <c>"\n\n"</c>, quindi due capoversi
///     scritti su Windows uscivano attaccati, con un <c>\r</c> orfano dentro;</item>
///   <item>un marcatore spaiato (<c>*</c>, <c>**</c>) si mangiava tutto fino al successivo, <b>a capi
///     compresi</b>: il corsivo poteva attraversare mezzo documento;</item>
///   <item>gli elenchi non esistevano — e <c>* voce</c> a inizio riga sarebbe finito nel corsivo, perché
///     l'inline girava prima di sapere che quella riga era una voce.</item>
/// </list>
/// La classificazione della riga viene <b>prima</b> dell'inline apposta: è l'ordine che rende gli elenchi
/// possibili senza litigare col corsivo.
/// </para>
///
/// <para>
/// ⚠️ <b>Niente elenchi annidati.</b> Una voce rientrata è una voce come le altre. È il perimetro, non una
/// dimenticanza: annidare vuol dire portarsi dentro l'ambiguità dei livelli a spazi, e nei documenti
/// operativi un elenco a due livelli non è mai servito.
/// </para>
/// </summary>
public static class MarkdownLite
{
    /// <summary>
    /// Il link inline a un allegato: <c>[LoA Marseille](allegato:loa-lirr-lfmm)</c>.
    ///
    /// <para>Lo slug è vincolato alla sua forma — minuscole, cifre, trattini singoli — e non a «qualunque
    /// cosa dopo i due punti»: senza, <c>allegato:../../qualcosa</c> passerebbe per uno slug e finirebbe
    /// dentro l'indirizzo che componiamo.</para>
    ///
    /// <para>Il testo del link è <b>già encodato</b> quando questa regex gira: <c>[&lt;script&gt;](…)</c> è
    /// diventato testo prima, e resta testo dentro l'ancora.</para>
    /// </summary>
    private static readonly Regex LinkAllegato = new(
        @"\[([^\]\r\n]+)\]\(allegato:([a-z0-9]+(?:-[a-z0-9]+)*)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Grassetto = new(
        @"\*\*(.+?)\*\*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Il sottolineato è <c>__testo__</c>. Nel Markdown di scuola <c>__</c> è un secondo modo di scrivere il
    /// grassetto — qui il grassetto è <c>**</c> e basta, quindi <c>__</c> era libero ed è il marcatore più
    /// riconoscibile che restava.
    /// <para>⚠️ I bordi vogliono <b>non-spazio</b> attaccato ai marcatori: senza, <c>a __ b __ c</c>
    /// diventerebbe un sottolineato, e soprattutto un identificatore con due trattini bassi in mezzo
    /// (<c>QNH__LIMC</c>) si trasformerebbe in markup. Contati sul corpus reale: zero corpi contengono
    /// <c>__</c> oggi, quindi la sintassi non ruba niente a nessuno.</para>
    /// </summary>
    private static readonly Regex Sottolineato = new(
        @"(?<!_)__(?=\S)(.+?)(?<=\S)__(?!_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Corsivo = new(
        @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Voce di elenco puntato. <c>•</c> c'è perché gli elenchi <b>già scritti a mano</b> nei vSOP usano
    /// quello (visto sul primo SOP vero il 28 agosto 2026): riconoscerlo li trasforma in elenchi veri senza
    /// che nessuno debba riscriverli.
    /// <para>⚠️ Lo <b>spazio dopo il marcatore è obbligatorio</b>, ed è ciò che tiene separati l'elenco e il
    /// corsivo: <c>* voce</c> è una voce, <c>*corsivo*</c> no.</para>
    /// </summary>
    private static readonly Regex VocePuntata = new(
        @"^[ \t]*[-*+•][ \t]+(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Voce di elenco numerato: <c>1. </c> oppure <c>1) </c>.</summary>
    private static readonly Regex VoceNumerata = new(
        @"^[ \t]*(\d{1,3})[.)][ \t]+(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return new MarkupString(string.Empty);

        // Fine riga a uno stile solo PRIMA di qualunque taglio: è la stessa forma canonica che si dà al
        // testo prima di tradurlo (TranslationText.Normalize), e per la stessa ragione — «una riga» non
        // deve voler dire due cose diverse a seconda di chi ha battuto il testo.
        var righe = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var sb = new StringBuilder();
        var i = 0;
        while (i < righe.Length)
        {
            if (righe[i].Trim().Length == 0) { i++; continue; }   // riga vuota: separa e sparisce

            if (VocePuntata.Match(righe[i]) is { Success: true })
                i = Elenco(sb, righe, i, ordinato: false);
            else if (VoceNumerata.Match(righe[i]) is { Success: true })
                i = Elenco(sb, righe, i, ordinato: true);
            else
                i = Paragrafo(sb, righe, i);
        }

        return new MarkupString(sb.ToString());
    }

    /// <summary>
    /// Un capoverso: le righe fino alla prima vuota o alla prima voce di elenco. Dentro, gli a capo restano
    /// a capo (<c>&lt;br&gt;</c>) — è così che il documento letto somiglia al testo scritto nell'editor.
    /// </summary>
    private static int Paragrafo(StringBuilder sb, string[] righe, int i)
    {
        sb.Append("<p>");
        var prima = true;
        for (; i < righe.Length; i++)
        {
            if (righe[i].Trim().Length == 0) break;
            if (VocePuntata.IsMatch(righe[i]) || VoceNumerata.IsMatch(righe[i])) break;
            if (!prima) sb.Append("<br>");
            sb.Append(Inline(righe[i]));
            prima = false;
        }
        sb.Append("</p>");
        return i;
    }

    /// <summary>
    /// Un elenco: le voci contigue dello <b>stesso tipo</b>. Un puntato che continua in numerato chiude e ne
    /// apre un altro — sono due elenchi, e fonderli mentirebbe sulla struttura.
    /// <para>Il numero d'inizio si conserva (<c>&lt;ol start="3"&gt;</c>): chi scrive «3.» sta continuando un
    /// elenco interrotto da una tabella o da un'immagine, e ricominciare da 1 gli cambierebbe il documento.</para>
    /// </summary>
    private static int Elenco(StringBuilder sb, string[] righe, int i, bool ordinato)
    {
        var voci = new List<string>();
        var inizio = 1;

        for (; i < righe.Length; i++)
        {
            if (ordinato)
            {
                var m = VoceNumerata.Match(righe[i]);
                if (!m.Success) break;
                if (voci.Count == 0 && int.TryParse(m.Groups[1].Value, out var n)) inizio = n;
                voci.Add(m.Groups[2].Value);
            }
            else
            {
                var m = VocePuntata.Match(righe[i]);
                if (!m.Success) break;
                voci.Add(m.Groups[1].Value);
            }
        }

        // `md-list` sta sull'elemento e non su un antenato: il renderer serve anche dove NON c'è `.prose` —
        // il corpo di un callout e la didascalia di una figura non ce l'hanno. Una classe propria è l'unico
        // aggancio che vale in tutti e tre i posti insieme.
        if (ordinato) sb.Append(inizio == 1 ? "<ol class=\"md-list\">" : $"<ol class=\"md-list\" start=\"{inizio}\">");
        else sb.Append("<ul class=\"md-list\">");

        foreach (var v in voci) sb.Append("<li>").Append(Inline(v)).Append("</li>");
        sb.Append(ordinato ? "</ol>" : "</ul>");
        return i;
    }

    /// <summary>
    /// Il markup dentro UNA riga. Encoda per primo — tutto quel che viene dopo costruisce tag sopra un testo
    /// che è già innocuo — e il link per ultimo, così il suo testo può portare grassetto e corsivo.
    /// <para>⚠️ Lavorando su una riga sola, un marcatore spaiato si mangia al massimo la sua riga. Prima
    /// arrivava fino al successivo, ovunque fosse.</para>
    /// </summary>
    private static string Inline(string riga)
    {
        var html = WebUtility.HtmlEncode(riga);
        html = Grassetto.Replace(html, "<strong>$1</strong>");
        html = Sottolineato.Replace(html, "<u>$1</u>");
        html = Corsivo.Replace(html, "<em>$1</em>");
        return LinkAllegato.Replace(html, m =>
            $"<a href=\"{AttachmentRules.UrlDi(m.Groups[2].Value)}\" target=\"_blank\" rel=\"noopener\">{m.Groups[1].Value}</a>");
    }
}

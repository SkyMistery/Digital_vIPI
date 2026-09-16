using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Renderer Markdown minimale del contenuto editoriale: <b>grassetto</b>, <i>corsivo</i>, <u>sottolineato</u>,
/// elenchi puntati e numerati annidati fino a cinque livelli, a capo. HTML-encoded per sicurezza.
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
/// ⚠️ <b>Il livello si scrive coi TRATTINI, non con gli spazi</b> (16 settembre 2026, richiesta del
/// committente): <c>- voce</c>, <c>-- voce</c>, <c>--- voce</c> per i puntati; <c>1) voce</c>,
/// <c>-1) voce</c>, <c>--1) voce</c> per i numerati. Fino al 2026-09-16 gli elenchi non si annidavano affatto,
/// e la ragione scritta qui era proprio l'ambiguità dei livelli a spazi. I trattini la tolgono: si VEDONO,
/// sopravvivono a un <c>Trim</c> e a un copia-incolla, e non dipendono da quanti spazi vale un TAB. Una voce
/// rientrata a spazi resta perciò una voce del suo livello di trattini — gli spazi in testa non contano.
/// </para>
/// <para>
/// Il TIPO si decide per voce, quindi i livelli si mescolano: un <c>-1)</c> sotto un <c>- voce</c> è un
/// numerato dentro un puntato, e viceversa. Il simbolo lo sceglie il foglio di stile da livello e tipo
/// (<c>md-l1</c>…<c>md-l5</c>), non il testo.
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

    /// <summary>Quanti livelli di elenco esistono: il numero sta in <see cref="VoceDiElenco"/>.</summary>
    public const int LivelliMassimi = VoceDiElenco.LivelliMassimi;

    // ⚠️ La sintassi delle voci NON sta qui: sta in `VoceDiElenco`, che legge anche il protettore della
    // traduzione per togliere i marcatori prima di spedire una riga al motore. Due copie della stessa regola
    // divergerebbero in silenzio — il motore riceverebbe marcatori che il renderer riconosce.
    private static bool ProvaVoce(string riga, out VoceDiElenco voce) => VoceDiElenco.Prova(riga, out voce);

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

            i = ProvaVoce(righe[i], out _) ? Elenco(sb, righe, i) : Paragrafo(sb, righe, i);
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
            if (ProvaVoce(righe[i], out _)) break;
            if (!prima) sb.Append("<br>");
            sb.Append(Inline(righe[i]));
            prima = false;
        }
        sb.Append("</p>");
        return i;
    }

    /// <summary>
    /// Un elenco, con i suoi annidati: le voci contigue, di qualunque livello e tipo, fino alla prima riga che
    /// non è una voce.
    ///
    /// <para>Una pila di elenchi aperti, uno per livello. Ogni voce di livello L chiude quel che sta sotto L,
    /// e poi:</para>
    /// <list type="bullet">
    ///   <item>allo <b>stesso livello e tipo</b> è la voce successiva;</item>
    ///   <item>allo stesso livello ma di <b>tipo diverso</b> chiude quell'elenco e ne apre un altro — un puntato
    ///     che continua in numerato sono due elenchi, e fonderli mentirebbe sulla struttura;</item>
    ///   <item>un livello <b>sotto</b> apre un elenco nuovo DENTRO la voce aperta (<c>&lt;ul&gt;</c> dentro
    ///     <c>&lt;li&gt;</c>: è l'annidamento che un lettore di schermo sa annunciare).</item>
    /// </list>
    /// <para>⚠️ Un salto di livello (dal primo al terzo) si aggancia al livello subito sotto quello aperto: un
    /// elenco di terzo livello senza un secondo che lo contenga non ha un posto dove stare, e inventarsi una
    /// voce vuota metterebbe nel documento un pallino che nessuno ha scritto.</para>
    /// <para>Il numero d'inizio si conserva (<c>&lt;ol start="3"&gt;</c>): chi scrive «3.» sta continuando un
    /// elenco interrotto da una tabella o da un'immagine, e ricominciare da 1 gli cambierebbe il documento.</para>
    /// </summary>
    private static int Elenco(StringBuilder sb, string[] righe, int i)
    {
        // Il tipo di ogni elenco aperto; il livello è la sua posizione nella pila (fondo = livello 1).
        var aperti = new Stack<bool>();

        for (; i < righe.Length; i++)
        {
            if (!ProvaVoce(righe[i], out var v)) break;
            var livello = Math.Min(v.Livello, aperti.Count + 1);

            while (aperti.Count > livello) Chiudi(sb, aperti);

            if (aperti.Count == livello)
            {
                if (aperti.Peek() == v.Ordinata) sb.Append("</li>");
                else { Chiudi(sb, aperti); Apri(sb, aperti, v); }
            }
            else Apri(sb, aperti, v);

            sb.Append("<li>").Append(Inline(v.Testo));
        }

        while (aperti.Count > 0) Chiudi(sb, aperti);
        return i;
    }

    // `md-list` sta sull'elemento e non su un antenato: il renderer serve anche dove NON c'è `.prose` — il
    // corpo di un callout e la didascalia di una figura non ce l'hanno. Una classe propria è l'unico aggancio
    // che vale in tutti e tre i posti insieme. `md-lN` porta il LIVELLO, da cui il foglio sceglie il simbolo:
    // contarlo in CSS dagli antenati sbaglierebbe appena un puntato sta dentro un numerato.
    private static void Apri(StringBuilder sb, Stack<bool> aperti, VoceDiElenco v)
    {
        aperti.Push(v.Ordinata);
        var cls = $"md-list md-l{aperti.Count}";
        if (!v.Ordinata) sb.Append("<ul class=\"").Append(cls).Append("\">");
        else if (v.Numero == 1) sb.Append("<ol class=\"").Append(cls).Append("\">");
        else sb.Append("<ol class=\"").Append(cls).Append("\" start=\"").Append(v.Numero).Append("\">");
    }

    private static void Chiudi(StringBuilder sb, Stack<bool> aperti) =>
        sb.Append("</li>").Append(aperti.Pop() ? "</ol>" : "</ul>");

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

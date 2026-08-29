using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Renderer Markdown minimale per i contenuti demo (F2): grassetto, corsivo, a capo.
/// In una fase successiva si potrà passare a Markdig (già usato dal sito host). HTML-encoded per sicurezza.
///
/// <para>
/// ⚠️ <b>Di link ne conosce UNO SOLO</b>: <c>[testo](allegato:slug)</c>. Non è una mancanza da colmare — è
/// il perimetro. Questo renderer <b>encoda e poi sostituisce con delle regex</b>, quindi aprirlo a
/// <c>[testo](url)</c> qualunque significherebbe far entrare nel contenuto editoriale un indirizzo
/// arbitrario, <c>javascript:</c> compreso, dentro un <c>href</c> che costruiamo noi. Uno schema solo,
/// riconosciuto per prefisso, e l'indirizzo lo compone <see cref="AttachmentRules"/> a partire dallo slug:
/// quel che sta nel testo è un <b>nome</b>, mai un indirizzo.
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

    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return new MarkupString(string.Empty);

        var html = WebUtility.HtmlEncode(markdown);
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<em>$1</em>");

        // Dopo grassetto e corsivo, così il testo di un link può portarli; prima degli a capo, perché
        // un'ancora non deve spezzarsi su una riga. L'indirizzo lo componiamo noi dallo slug: nel testo
        // c'è un nome, non un URL.
        html = LinkAllegato.Replace(html, m =>
            $"<a href=\"{AttachmentRules.UrlDi(m.Groups[2].Value)}\" target=\"_blank\" rel=\"noopener\">{m.Groups[1].Value}</a>");

        html = html.Replace("\n\n", "</p><p>").Replace("\n", "<br>");
        return new MarkupString($"<p>{html}</p>");
    }
}

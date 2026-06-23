using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Vipi.Ui;

/// <summary>
/// Renderer Markdown minimale per i contenuti demo (F2): grassetto, corsivo, a capo.
/// In una fase successiva si potrà passare a Markdig (già usato dal sito host). HTML-encoded per sicurezza.
/// </summary>
public static class MarkdownLite
{
    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return new MarkupString(string.Empty);

        var html = WebUtility.HtmlEncode(markdown);
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<em>$1</em>");
        html = html.Replace("\n\n", "</p><p>").Replace("\n", "<br>");
        return new MarkupString($"<p>{html}</p>");
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Application.Import;

/// <summary>
/// Legge una tabella HTML incollata.
///
/// <para>
/// ⚠️ <b>E' la porta a fedelta' piu' alta, e vale la pena spiegarlo.</b> Quando si copia da Excel, da Word o
/// da una pagina web, la clipboard contiene <i>anche</i> un <c>text/html</c> con la tabella vera. Li' le
/// celle <b>sono</b> celle: niente separatore da indovinare, niente cella multi-parola ambigua, niente riga
/// che si spezza dove non deve. Ogni altra porta e' un'euristica; questa no.
/// </para>
/// <para>
/// ⚠️ <b>Il <c>colspan</c> si espande, il <c>rowspan</c> no.</b> Una cella su due colonne diventa la cella
/// piu' una vuota, perche' altrimenti la riga sarebbe piu' corta e in una tabella le celle successive
/// scalerebbero a sinistra — il dato sembrerebbe sbagliato invece che unito. Il <c>rowspan</c> invece
/// vorrebbe ricordare le righe precedenti: si legge come cella vuota, e chi rilegge l'anteprima la riempie.
/// </para>
/// <para>
/// ⚠️ Non e' un parser HTML e non deve diventarlo: legge <b>la prima tabella</b> di un frammento incollato.
/// Un documento intero con tre tabelle da' la prima, ed e' il comportamento che si spiega in una riga.
/// </para>
/// </summary>
public static class TabellaHtml
{
    private static readonly Regex Tabella =
        new("<table[^>]*>(.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex Riga =
        new("<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex Cella =
        new("<t(?:d|h)([^>]*)>(.*?)</t(?:d|h)>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex Colspan =
        new("colspan\\s*=\\s*[\"']?(\\d+)", RegexOptions.IgnoreCase);

    private static readonly Regex Interruzione =
        new("<br[^>]*>|</p>|</div>", RegexOptions.IgnoreCase);

    private static readonly Regex Marcatore = new("<[^>]+>", RegexOptions.Singleline);

    private static readonly Regex Entita = new("&(#x[0-9a-fA-F]+|#\\d+|[a-zA-Z]+);");

    /// <summary>La prima tabella del frammento, o <see cref="Griglia.Vuota"/> se non ce n'e' nessuna.</summary>
    public static Griglia Leggi(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Griglia.Vuota;

        var tab = Tabella.Match(html!);
        if (!tab.Success) return Griglia.Vuota;

        var righe = new List<IReadOnlyList<string>>();
        foreach (Match r in Riga.Matches(tab.Groups[1].Value))
        {
            var celle = new List<string>();
            foreach (Match c in Cella.Matches(r.Groups[1].Value))
            {
                celle.Add(Testo(c.Groups[2].Value));
                var span = Colspan.Match(c.Groups[1].Value);
                if (span.Success && int.TryParse(span.Groups[1].Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var n) && n > 1)
                    for (var k = 1; k < Math.Min(n, 64); k++) celle.Add("");
            }
            if (celle.Count > 0) righe.Add(celle);
        }
        return righe.Count == 0 ? Griglia.Vuota : new Griglia(righe, FormaGriglia.Html);
    }

    /// <summary>Il contenuto di una cella: interruzioni a spazio, marcatori via, entita' sciolte.</summary>
    private static string Testo(string html)
    {
        var t = Interruzione.Replace(html, " ");
        t = Marcatore.Replace(t, "");
        t = SciogliEntita(t);
        return TestoTabellare.NormalizzaSegni(t);
    }

    /// <summary>Un punto di codice che si puo' davvero convertire: i surrogati non sono caratteri, e
    /// darli a <c>ConvertFromUtf32</c> alza un'eccezione invece di rendere un'entita' scritta male.</summary>
    private static bool Codice(int x) => x > 0 && x <= 0x10FFFF && (x < 0xD800 || x > 0xDFFF);

    private static string SciogliEntita(string t) => Entita.Replace(t, m =>
    {
        var nome = m.Groups[1].Value;
        if (nome.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(nome.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var x) && Codice(x)
                ? char.ConvertFromUtf32(x)
                : m.Value;
        if (nome.StartsWith("#", StringComparison.Ordinal))
            return int.TryParse(nome.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture,
                out var d) && Codice(d)
                ? char.ConvertFromUtf32(d)
                : m.Value;

        return nome.ToLowerInvariant() switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "nbsp" => " ",
            "ndash" => "-",
            "mdash" => "-",
            "deg" => "\u00B0",
            _ => m.Value,
        };
    });
}

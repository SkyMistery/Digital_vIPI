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
/// ⚠️ <b>Le celle unite si espandono tutte e due</b>, in orizzontale e in verticale. Una cella su due colonne
/// diventa la cella piu' una vuota, e una cella su due righe lascia una vuota nella riga sotto: altrimenti la
/// riga sarebbe piu' corta e in una tabella le celle successive scalerebbero a sinistra — il valore della
/// colonna <i>n</i> finirebbe nel campo della colonna <i>n-1</i>, e l'anteprima non mostrerebbe un buco ma una
/// tabella plausibile e sbagliata. La vuota si legge come vuota, e chi rilegge l'anteprima la riempie.
/// </para>
/// <para>
/// ⚠️ Fino al 7 settembre 2026 questo paragrafo prometteva il <c>rowspan</c> e il codice non lo faceva:
/// nessun ramo inseriva la cella ereditata, e le righe corte restavano corte (<c>Griglia.Colonne</c> e' il
/// <i>massimo</i> delle lunghezze, non pareggia niente). Le tabelle aeronautiche uniscono in verticale di
/// continuo — e le rende cosi' anche questo sito (<c>CoordTable</c>, <c>TableBlock</c>,
/// <c>AppFrequencies</c>): bastava copiare una pagina della vIPI e reincollarla qui (revisione del
/// 6 settembre 2026, R-020).
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

    private static readonly Regex Rowspan =
        new("rowspan\\s*=\\s*[\"']?(\\d+)", RegexOptions.IgnoreCase);

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

        // Quel che le righe PRECEDENTI si portano dietro: colonna → quante righe restano da coprire. È tutto
        // il conto del `rowspan`, ed è lo stesso del `colspan` su un asse diverso.
        var eredita = new Dictionary<int, int>();

        foreach (Match r in Riga.Matches(tab.Groups[1].Value))
        {
            var celle = new List<string>();
            var colonna = 0;

            // Le colonne già occupate da una cella unita in verticale si riempiono di vuoto PRIMA di piazzare
            // la prossima cella scritta: altrimenti quella prende il posto di chi la copre, e da lì in giù
            // tutta la riga scala a sinistra.
            void Ereditate()
            {
                while (eredita.TryGetValue(colonna, out var restano) && restano > 0)
                {
                    celle.Add("");
                    eredita[colonna] = restano - 1;
                    colonna++;
                }
            }

            foreach (Match c in Cella.Matches(r.Groups[1].Value))
            {
                Ereditate();

                var testo = Testo(c.Groups[2].Value);
                var larghe = Quante(Colspan, c.Groups[1].Value);
                var alte = Quante(Rowspan, c.Groups[1].Value);

                for (var k = 0; k < larghe; k++)
                {
                    celle.Add(k == 0 ? testo : "");
                    if (alte > 1) eredita[colonna] = alte - 1;
                    colonna++;
                }
            }

            // Le eredità in coda: una cella unita che sta all'ULTIMA colonna non ha nessuna cella scritta
            // dopo di sé a farle da innesco.
            Ereditate();

            if (celle.Count > 0) righe.Add(celle);
        }
        return righe.Count == 0 ? Griglia.Vuota : new Griglia(righe, FormaGriglia.Html);
    }

    /// <summary>
    /// Quante celle vale uno span: 1 se l'attributo non c'è o non è un numero.
    /// <para>⚠️ Col tetto a 64 che c'era già per il <c>colspan</c>: un <c>rowspan="100000"</c> incollato —
    /// per errore o apposta — non deve diventare centomila righe di vuoto.</para>
    /// </summary>
    private static int Quante(Regex quale, string attributi)
    {
        var m = quale.Match(attributi);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None,
                   CultureInfo.InvariantCulture, out var n) && n > 1
            ? Math.Min(n, 64)
            : 1;
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

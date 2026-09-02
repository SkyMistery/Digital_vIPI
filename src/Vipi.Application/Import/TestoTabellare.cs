using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vipi.Application.Import;

/// <summary>
/// Le pulizie che vengono <b>prima</b> di spezzare un testo in colonne, e i tagli suggeriti per il modo a
/// larghezza fissa.
///
/// <para>
/// ⚠️ Le pulizie non sono tutte uguali e non si applicano allo stesso momento. Lo <b>spazio unificatore</b>
/// (U+00A0) e i fine-riga si normalizzano <i>sempre</i>: sono differenze che nessuno vede e che
/// spezzerebbero un confronto per nessun motivo. I <b>trattini lunghi</b> e gli <b>spazi ripetuti</b> invece
/// si normalizzano <i>solo dove si spezza per spazi</i> — dentro una cella di un CSV sono contenuto scritto
/// da qualcuno, e riscriverlo sarebbe cambiare il documento mentre lo si importa.
/// </para>
/// <para>
/// ⚠️ I caratteri speciali si scrivono con la loro <b>sequenza di escape</b> e mai in chiaro: un carattere
/// invisibile finito in un file sorgente non si vede rileggendo il diff, e in questo progetto uno di essi ha
/// già fatto cadere l'host dei test invece di far fallire un test.
/// </para>
/// </summary>
public static class TestoTabellare
{
    /// <summary>
    /// Fine-riga a <c>\n</c>, spazi unificatori a spazio normale, e via il segno d'ordine dei byte in testa.
    /// Si applica sempre.
    ///
    /// <para>⚠️ Il segno d'ordine dei byte e' <b>invisibile</b> e apre ogni CSV scritto per Excel — compreso
    /// il nostro. Lasciato dentro diventa parte della PRIMA cella: l'intestazione «Nome» si chiama
    /// «(segno)Nome», non combacia con nessun nome di colonna, e l'unica cosa che si vede e' una mappatura
    /// che «non funziona» su un file che sembra giusto. Trovato chiudendo il giro esporta-reimporta.</para>
    /// </summary>
    public static string Normalizza(string? testo) =>
        string.IsNullOrEmpty(testo)
            ? ""
            : testo!.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n')
                .Replace('\u00A0', ' ')    // spazio unificatore: esce da Word e dal web
                .Replace('\u202F', ' ')    // unificatore stretto
                .Replace('\u2009', ' ');   // spazio sottile

    /// <summary>
    /// I trattini lunghi diventano il trattino normale e gli spazi ripetuti diventano uno.
    /// <para>⚠️ Serve dove si spezza per spazi: nelle cinque righe d'esempio degli «Aeroporti alternati»
    /// convivono il trattino lungo (quattro righe) e quello normale (una), e una riga ha un doppio spazio.
    /// Chi legge non vede la differenza; un'espressione regolare sì.</para>
    /// </summary>
    public static string NormalizzaSegni(string? testo)
    {
        if (string.IsNullOrEmpty(testo)) return "";
        var sb = new StringBuilder(testo!.Length);
        var spazioPrima = false;
        foreach (var c in testo)
        {
            var ch = c switch
            {
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2015' or '\u2212' => '-',
                '\u00A0' or '\u202F' or '\u2009' or '\t' => ' ',
                _ => c,
            };
            if (ch == ' ')
            {
                if (spazioPrima) continue;
                spazioPrima = true;
            }
            else spazioPrima = false;
            sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Il <b>primo numero</b> scritto nel testo, con la virgola letta come punto e l'unita' ignorata:
    /// <c>72.2</c>, <c>72,2</c>, <c>72.2NM</c> e <c>308 gradi</c> danno tutti il loro numero.
    ///
    /// <para>⚠️ La virgola vale come il punto perche' chi scrive in italiano digita «72,2»: con la sola
    /// lettura invariante quel valore diventa <c>null</c> in silenzio, e la cella si svuota da sola dopo
    /// essere stata compilata.</para>
    /// <para>⚠️ L'unita' si ignora invece di far fallire la lettura: <c>72.2NM</c> e' esattamente quel che si
    /// incolla da un PDF, e rifiutarlo vorrebbe dire far ripulire a mano la colonna che l'import esiste per
    /// non far ridigitare.</para>
    /// </summary>
    public static decimal? Numero(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;

        var cifre = new StringBuilder();
        var visto = false;
        foreach (var c in testo!)
        {
            if (c >= '0' && c <= '9') { cifre.Append(c); visto = true; continue; }
            if ((c == '.' || c == ',') && visto && cifre.ToString().IndexOf('.') < 0) { cifre.Append('.'); continue; }
            if (c == '-' && cifre.Length == 0) { cifre.Append('-'); continue; }
            if (visto) break;   // finito il numero: quel che segue e' l'unita'
        }

        var t = cifre.ToString().TrimEnd('.');
        return t.Length > 0 && t != "-"
               && decimal.TryParse(t, System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    /// <summary>Le righe non vuote del testo, già normalizzate.</summary>
    public static IReadOnlyList<string> Righe(string? testo) =>
        Normalizza(testo).Split('\n').Where(r => r.Trim().Length > 0).ToList();

    /// <summary>
    /// I tagli di colonna <b>suggeriti</b> per il modo a larghezza fissa: le posizioni dove <b>tutte</b> le
    /// righe hanno uno spazio (o sono già finite) e subito dopo almeno una riga riprende a scrivere.
    ///
    /// <para>⚠️ È un suggerimento, non una lettura: i tagli si mettono e si tolgono cliccando il righello.
    /// Una tabella copiata da un PDF con una colonna piena su tutte le righe non ha nessun taglio da
    /// suggerire, e va bene così — è il motivo per cui il modo esiste con il righello invece che da solo.</para>
    /// </summary>
    public static IReadOnlyList<int> TagliSuggeriti(string? testo)
    {
        var righe = Righe(testo);
        if (righe.Count == 0) return Array.Empty<int>();

        var larghezza = righe.Max(r => r.Length);
        var tagli = new List<int>();
        var vuotaPrima = true;   // la colonna 0 non è un taglio
        for (var x = 0; x < larghezza; x++)
        {
            var vuota = righe.All(r => x >= r.Length || r[x] == ' ');
            if (vuotaPrima && !vuota && x > 0) tagli.Add(x);
            vuotaPrima = vuota;
        }
        return tagli;
    }
}

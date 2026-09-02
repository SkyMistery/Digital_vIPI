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
    /// <summary>Fine-riga a <c>\n</c> e spazi unificatori a spazio normale. Si applica sempre.</summary>
    public static string Normalizza(string? testo) =>
        string.IsNullOrEmpty(testo)
            ? ""
            : testo!.Replace("\r\n", "\n").Replace('\r', '\n')
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

    /// <summary>Le righe non vuote del testo, già normalizzate.</summary>
    public static IReadOnlyList<string> Righe(string? testo) =>
        Normalizza(testo).Split('\n').Where(r => r.Trim().Length > 0).ToList();

    /// <summary>
    /// I tagli di colonna <b>suggeriti</b> per il modo a larghezza fissa: le posizioni dove <b>tutte</b> le
    /// righe hanno uno spazio (o sono già finite) e subito dopo almeno una riga riprende a scrivere.
    ///
    /// <para>⚠️ È un suggerimento, non una lettura: i tagli l'utente li trascina. Una tabella copiata da un
    /// PDF con una colonna piena su tutte le righe non ha nessun taglio da suggerire, e va bene così — è il
    /// motivo per cui il modo esiste con le maniglie invece che da solo.</para>
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

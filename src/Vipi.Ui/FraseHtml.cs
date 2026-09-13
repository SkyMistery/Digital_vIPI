using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;

namespace Vipi.Ui;

/// <summary>
/// Una frase dei <c>.resx</c> che porta markup (<c>&lt;b&gt;</c>, <c>&lt;code&gt;</c>, un collegamento) resa come
/// HTML, con gli <b>argomenti encodati</b>.
///
/// <para>
/// ⚠️ <b>Il markup lo scrive chi traduce, i valori no.</b> Il cast a MarkupString di <c>string.Format(frase, x)</c> tratta
/// da HTML anche <c>x</c>, e <c>x</c> arriva dalla query, da un titolo scritto da un Editor, da un codice
/// importato da IVAO. Il 13 settembre 2026 (T-001) un <c>?app=&lt;script …&gt;</c> su una pagina pubblica in
/// SSR usciva crudo nell'HTML; sulla vista live (T-003) il titolo di un gruppo APP. Qui ogni argomento passa
/// da <see cref="WebUtility.HtmlEncode(string)"/>, che copre anche le virgolette: un valore messo dentro un
/// <c>href="{1}"</c> non esce dall'attributo.
/// </para>
///
/// <para>
/// ⚠️ Un valore che finisce <b>dentro un indirizzo</b> (una query) va prima escapato come dato d'indirizzo
/// da chi chiama (<see cref="Uri.EscapeDataString(string)"/>): l'HTML-encoding impedisce di uscire
/// dall'attributo, non di cambiare il significato dell'indirizzo. Lo schema resta sempre nella frase o nel
/// prefisso fisso: mai un argomento che <i>è</i> l'indirizzo intero preso da fuori.
/// </para>
///
/// <para>Una guardia di test (<c>FraseHtmlTests</c>) rifiuta ogni cast a MarkupString di un <c>string.Format</c> nel markup.</para>
/// </summary>
public static class FraseHtml
{
    public static MarkupString Format(string frase, params object?[] argomenti)
    {
        var encodati = new object[argomenti.Length];
        for (var i = 0; i < argomenti.Length; i++)
            encodati[i] = WebUtility.HtmlEncode(Convert.ToString(argomenti[i], CultureInfo.CurrentCulture) ?? "");
        return new MarkupString(string.Format(CultureInfo.CurrentCulture, frase, encodati));
    }
}

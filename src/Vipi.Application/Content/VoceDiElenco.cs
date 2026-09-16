using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>
/// Una riga di testo editoriale che è una <b>voce di elenco</b>: di che livello, di che tipo, con che testo.
///
/// <para>
/// ⚠️ <b>È la porta sola della sintassi degli elenchi</b> (16 settembre 2026). La leggono in due: il renderer
/// (<c>MarkdownLite</c>, in <c>Vipi.Ui</c>) per disegnare l'elenco, e il protettore della traduzione
/// (<see cref="Translation.TextProtector"/>) per togliere il marcatore prima di spedire la riga al motore. Se
/// le due regole vivessero in due file, il giorno che una delle due imparasse un marcatore nuovo il motore
/// comincerebbe a ricevere — e a «tradurre» — marcatori che il renderer riconosce, o viceversa.
/// </para>
///
/// <para>
/// La sintassi: il livello si scrive coi <b>trattini</b>, non con gli spazi.
/// <list type="bullet">
///   <item>puntato — <c>- voce</c>, <c>-- voce</c>, … fino a <see cref="LivelliMassimi"/> trattini; a primo
///     livello valgono anche <c>* voce</c>, <c>+ voce</c> e <c>• voce</c> (quest'ultimo perché gli elenchi
///     già scritti a mano nei vSOP usano quello);</item>
///   <item>numerato — <c>1) voce</c> o <c>1. voce</c>, con davanti un trattino per ogni livello oltre il
///     primo: <c>-1) voce</c> è il secondo.</item>
/// </list>
/// ⚠️ Lo <b>spazio dopo il marcatore è obbligatorio</b>. È ciò che tiene fuori dagli elenchi il corsivo
/// (<c>*corsivo*</c>), una riga di soli trattini, e «-5 gradi».
/// </para>
/// </summary>
/// <param name="Livello">Da 1 a <see cref="LivelliMassimi"/>: quel che sta più in fondo resta all'ultimo.</param>
/// <param name="Ordinata">Vero per i numerati.</param>
/// <param name="Numero">Il numero scritto (1 per i puntati). Conta solo sulla prima voce di un elenco.</param>
/// <param name="Marcatore">Il prefisso della riga così com'è scritto, spazi compresi: <c>"-- "</c>,
/// <c>"  -1) "</c>. Riga = <c>Marcatore + Testo</c>, sempre.</param>
/// <param name="Testo">Quel che viene dopo il marcatore.</param>
public readonly record struct VoceDiElenco(int Livello, bool Ordinata, int Numero, string Marcatore, string Testo)
{
    /// <summary>Quanti livelli di elenco esistono.</summary>
    public const int LivelliMassimi = 5;

    // ⚠️ Il numerato si prova PRIMA dei trattini. `-1) voce` comincia con un trattino ma non ha lo spazio
    // subito dopo, quindi la regola dei trattini da sola non lo prenderebbe comunque: l'ordine lo rende
    // leggibile invece di lasciarlo dedurre dalla regex.
    private static readonly Regex Numerata = new(
        @"^([ \t]*(-*)(\d{1,3})[.)][ \t]+)(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Trattini = new(
        @"^([ \t]*(-+)[ \t]+)(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Simbolo = new(
        @"^([ \t]*[*+•][ \t]+)(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Se <paramref name="riga"/> è una voce di elenco, quale. Una riga sola: niente a capo dentro.</summary>
    public static bool Prova(string? riga, out VoceDiElenco voce)
    {
        voce = default;
        if (string.IsNullOrEmpty(riga)) return false;

        if (Numerata.Match(riga) is { Success: true } n)
        {
            voce = new VoceDiElenco(Limita(n.Groups[2].Length + 1), true,
                                    int.TryParse(n.Groups[3].Value, out var k) ? k : 1,
                                    n.Groups[1].Value, n.Groups[4].Value);
            return true;
        }
        if (Trattini.Match(riga) is { Success: true } t)
        {
            voce = new VoceDiElenco(Limita(t.Groups[2].Length), false, 1, t.Groups[1].Value, t.Groups[3].Value);
            return true;
        }
        if (Simbolo.Match(riga) is { Success: true } s)
        {
            voce = new VoceDiElenco(1, false, 1, s.Groups[1].Value, s.Groups[2].Value);
            return true;
        }
        return false;
    }

    private static int Limita(int n) => Math.Clamp(n, 1, LivelliMassimi);
}

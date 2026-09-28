namespace Vipi.Host;

/// <summary>
/// Un valore che arriva da fuori — un percorso, un Referer, l'errore dichiarato dal portale IVAO — messo in <b>una
/// riga</b> di un registro di <c>diagnostica/</c>: senza a capo, senza caratteri di controllo, e con un tetto.
///
/// <para>🔴 <b>Perché esiste</b> (revisione totale 3, U-023, U-120, U-127). I registri si leggono a occhio e con gli
/// strumenti (<c>tools/errori-per-era.py</c>, la memoria delle firme di <see cref="RegistroAvvisi"/>), e tutti e due
/// riconoscono una voce dalla riga che comincia con un timbro. Un percorso con <c>%0A</c> — ASP.NET Core lo
/// decodifica — andava a capo e scriveva a colonna 0 una riga scelta da un anonimo: una voce finta, oppure una firma
/// «già scritta oggi» che zittiva un avviso vero fino a mezzanotte. E un percorso da 8 kB, dieci volte per voce,
/// faceva ruotare il file in poche voci.</para>
///
/// <para>La regola era già scritta una volta, in <see cref="RegistroRichieste.Riga"/>: qui sta in un posto solo.</para>
/// </summary>
internal static class TestoDiRegistro
{
    /// <summary>
    /// <paramref name="valore"/> in una riga sola: ogni carattere di controllo (a capo, tab, …) e i separatori di riga
    /// Unicode diventano uno spazio, e oltre <paramref name="max"/> caratteri si tronca con «…». Null resta null.
    /// </summary>
    public static string? Riga(string? valore, int max)
    {
        if (valore is null) return null;
        var t = valore.Length > max ? valore[..max] : valore;
        var pulito = string.Create(t.Length, t, static (dest, src) =>
        {
            for (var i = 0; i < src.Length; i++)
                dest[i] = char.IsControl(src[i]) || src[i] is '\u2028' or '\u2029' ? ' ' : src[i];
        });
        return valore.Length > max ? pulito + "…" : pulito;
    }

    /// <summary>
    /// Un testo su più righe (un messaggio, uno stack) dentro una voce: <b>ogni</b> riga rientra di due spazi. Così a
    /// colonna 0 stanno solo le righe che scriviamo noi — trattini, timbri, <c>ANCORA</c> — e un messaggio che porta un
    /// a capo seguito da un timbro non si legge come una voce.
    /// </summary>
    public static string Rientro(string testo) =>
        "  " + testo.ReplaceLineEndings("\n").Replace("\u2028", " ").Replace("\u2029", " ")
                    .Replace("\n", Environment.NewLine + "  ");
}

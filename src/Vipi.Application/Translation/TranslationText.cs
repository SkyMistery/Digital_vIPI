using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Application.Translation;

/// <summary>
/// Normalizzazione e impronta del testo sorgente: il cuore puro della memoria di traduzione (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §1-2).
///
/// <para>
/// ⚠️ <b>Questa classe decide quanto morde il dedup</b>, che è metà del valore della funzione. Due testi che
/// dicono la stessa cosa e differiscono solo per uno spazio, un a-capo Windows o un apostrofo tipografico
/// devono avere <b>lo stesso hash</b>, o si pagano due traduzioni per la stessa frase e la correzione fatta
/// sull'una non si vede sull'altra.
/// </para>
///
/// <para>
/// ⚠️ <b>Si normalizza per l'hash E per l'invio.</b> Se si mandasse al motore il testo grezzo mentre l'hash
/// è del normalizzato, due grafie diverse condividerebbero la chiave ma <c>SourceText</c> sarebbe quella
/// arrivata per prima: la memoria mostrerebbe a chi corregge un testo diverso da quello tradotto. Il
/// normalizzato è la sola forma che gira.
/// </para>
///
/// <para>
/// Cosa <b>non</b> si normalizza, e non è una dimenticanza: le <b>maiuscole</b> (in aviazione distinguono un
/// identificatore da una parola) e la <b>punteggiatura</b> diversa dalle virgolette (cambia la frase).
/// </para>
/// </summary>
public static partial class TranslationText
{
    // Caratteri che sono GRAFIA e non contenuto. Scritti come CODICE NUMERICO e non come carattere: a
    // schermo NO-BREAK SPACE e spazio normale sono identici, e quattro righe di sostituzione tutte uguali
    // non le potrebbe rileggere nessuno.
    private const char SpazioUnificatore = (char)0x00A0;   // NO-BREAK SPACE, lo mettono i programmi di scrittura
    private const char SpazioStrettoUnif = (char)0x202F;   // NARROW NO-BREAK SPACE
    private const char SpazioDiCifra = (char)0x2007;       // FIGURE SPACE
    private const char SpazioSottile = (char)0x2009;       // THIN SPACE
    private const char ApiceAperto = (char)0x2018;         // apice singolo aperto
    private const char ApiceChiuso = (char)0x2019;         // apice singolo chiuso: l'apostrofo automatico
    private const char ApiceModificatore = (char)0x02BC;   // MODIFIER LETTER APOSTROPHE
    private const char VirgolettaAperta = (char)0x201C;    // virgoletta doppia aperta
    private const char VirgolettaChiusa = (char)0x201D;    // virgoletta doppia chiusa
    private const char VirgolettaBassa = (char)0x201E;     // virgoletta doppia bassa

    /// <summary>Spazi orizzontali consecutivi (spazio, tabulazione) dentro una riga.</summary>
    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpaziOrizzontali();

    /// <summary>Tre o più a-capo di fila: un paragrafo vuoto in più non cambia il significato.</summary>
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex AcapoRidondanti();

    /// <summary>Almeno una lettera — di qualunque alfabeto. Senza, non c'è niente da tradurre.</summary>
    [GeneratedRegex(@"\p{L}")]
    private static partial Regex QualcheLettera();

    /// <summary>Il carattere ricondotto alla forma ASCII, se è di quelli che sono solo grafia.</summary>
    private static char Canonico(char c) => c switch
    {
        SpazioUnificatore or SpazioStrettoUnif or SpazioDiCifra or SpazioSottile => ' ',
        ApiceAperto or ApiceChiuso or ApiceModificatore => '\'',
        VirgolettaAperta or VirgolettaChiusa or VirgolettaBassa => '"',
        _ => c,
    };

    /// <summary>
    /// Forma canonica di un testo sorgente. Idempotente: <c>Normalize(Normalize(x)) == Normalize(x)</c>, ed
    /// è una proprietà su cui c'è un test — se cadesse, lo stesso testo salvato due volte cambierebbe hash.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        // 1. Unicode in forma composta: "è" scritta come e+accento e come è sono la STESSA frase, e senza
        //    questo passo hanno due hash. Prima di tutto il resto, perché cambia i codepoint.
        var s = raw.Normalize(NormalizationForm.FormC);

        // 2. Fine riga a uno stile solo, e grafia ricondotta all'ASCII, in una passata sola. Un testo
        //    scritto su Windows e lo stesso incollato da altrove non devono divergere.
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\r')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n') continue;  // \r\n: l'a-capo lo mette il \n dopo
                sb.Append('\n');
                continue;
            }
            sb.Append(Canonico(c));
        }

        // 3. Spazi orizzontali collassati e code di riga tagliate, riga per riga.
        var righe = sb.ToString().Split('\n');
        for (var i = 0; i < righe.Length; i++)
            righe[i] = SpaziOrizzontali().Replace(righe[i], " ").TrimEnd();

        // 4. Paragrafi vuoti in eccesso, e bordi.
        return AcapoRidondanti().Replace(string.Join("\n", righe), "\n\n").Trim();
    }

    /// <summary>
    /// Impronta del testo, <b>normalizzato o no</b>: normalizza sempre prima, così nessun chiamante può
    /// sbagliare l'ordine e produrre una chiave che nessun altro ritroverà.
    /// </summary>
    public static string Hash(string? raw)
    {
        var norm = Normalize(raw);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm))).ToLowerInvariant();
    }

    /// <summary>
    /// Vero se in questo testo c'è qualcosa da tradurre: almeno una lettera. <c>"126.850"</c>, <c>"—"</c>,
    /// <c>"1 / 2"</c> non ne hanno.
    /// <para>⚠️ È un filtro <b>grossolano</b> e sta qui solo per non spedire l'ovvio: <c>"16R"</c> HA una
    /// lettera e passa questo cancello. A riconoscere gli identificatori pensa il protettore (§3a), che è
    /// un'altra cosa e arriva dopo.</para>
    /// </summary>
    public static bool HasSomethingToTranslate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && QualcheLettera().IsMatch(raw);

    /// <summary>
    /// Ripara il <b>grassetto</b> di una traduzione: se i marcatori <c>**</c> non sono più tanti quanti
    /// nell'originale, li toglie tutti.
    ///
    /// <para>
    /// ⚠️ <b>Visto a schermo il 28 agosto 2026</b>, sul primo SOP vero: «• A <c>**</c>nord<c>**</c> del campo»
    /// è tornato «• To the north<c>**</c> of the field», con un marcatore orfano <b>stampato nella pagina</b>.
    /// I marcatori NON si proteggono — provato, e il motore infila le parole dentro i tag — quindi il motore
    /// li sposta e ogni tanto ne perde uno.
    /// </para>
    /// <para>
    /// Fra un grassetto perso e due asterischi a schermo si sceglie il grassetto perso: il testo resta
    /// giusto, e quello che si nota è solo che una parola non è in neretto.
    /// </para>
    /// </summary>
    public static string RiparaGrassetto(string sorgente, string tradotto)
    {
        var attesi = Marcatori(sorgente);
        // Dispari = sicuramente rotto. Diverso dall'originale ma pari = il motore ha spostato un grassetto
        // su un'altra parola: sgradevole, non sbagliato, e si tiene.
        if (attesi == Marcatori(tradotto) || Marcatori(tradotto) % 2 == 0) return tradotto;
        return tradotto.Replace("**", "");
    }

    private static int Marcatori(string? t)
    {
        if (string.IsNullOrEmpty(t)) return 0;
        var n = 0;
        for (var i = 0; i + 1 < t.Length; i++)
            if (t[i] == '*' && t[i + 1] == '*') { n++; i++; }
        return n;
    }
}

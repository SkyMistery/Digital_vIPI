using System.Text;
using System.Text.RegularExpressions;

namespace Vipi.Application.Translation;

/// <summary>
/// Che cosa esce da un testo protetto, e come si rimette a posto dopo la traduzione.
/// </summary>
/// <param name="Text">Il testo con i segnaposto al posto di ciò che non si traduce. È <b>questo</b> che va
/// al motore, mai l'originale.</param>
/// <param name="Tokens">Ciò che è stato tolto, nell'ordine dei segnaposto.</param>
/// <param name="Safe">Falso se dopo la protezione resta qualcosa che <b>somiglia a un dato personale</b>.
/// Un segmento non sicuro <b>non si spedisce</b>: si marca «da tradurre a mano».</param>
public sealed record ProtectedText(string Text, IReadOnlyList<string> Tokens, bool Safe);

/// <summary>
/// Toglie dal testo ciò che non va tradotto — e, prima ancora, ciò che non deve <b>uscire di qui</b>
/// (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §3).
///
/// <para>
/// ⚠️ <b>I segnaposto sono vuoti, e non è un dettaglio.</b> DeepL saprebbe rispettare il contenuto di un tag
/// marcato «non tradurre», ma quel contenuto <b>viaggerebbe lo stesso</b>. Per un callsign non importa; per
/// un VID sì. Un segnaposto autochiudente — <c>&lt;x id="0"/&gt;</c> — è l'unica forma in cui si può
/// affermare che il dato non ha lasciato il processo, invece di sperarlo.
/// </para>
///
/// <para>
/// ⚠️ <b>Proteggere troppo costa poco, proteggere troppo poco costa un dato.</b> Dove la regola è incerta si
/// sbaglia verso la protezione: un identificatore protetto per errore torna indietro identico, e al massimo
/// la frase intorno si traduce un filo peggio.
/// </para>
///
/// <para>
/// <b>Le tre difese della carta §3b, nell'ordine.</b> (1) Strutturale: nomi e VID vanno <i>derivati</i>, non
/// digitati — il timbro di <c>ValidityStamp</c> lo è già. (2) Protettore: questa classe, con il roster dello
/// staff passato dal chiamante. (3) Fail closed: <see cref="ProtectedText.Safe"/>.
/// </para>
/// </summary>
public sealed partial class TextProtector
{
    /// <summary>I nomi del roster staff, dal più lungo al più corto: «Mario Rossi» prima di «Mario», o del
    /// nome intero resterebbe in chiaro il cognome.</summary>
    private readonly IReadOnlyList<string> _nomi;

    /// <param name="nomiDaNonInviare">I nomi delle persone note (<c>StaffMember.DisplayName</c>). ⚠️ Vanno
    /// passati: questa classe non sa da sola chi esiste, e il roster è l'unico posto dove un nome è un
    /// <b>dato</b> invece che una stringa qualunque.</param>
    public TextProtector(IEnumerable<string>? nomiDaNonInviare = null) =>
        _nomi = (nomiDaNonInviare ?? Enumerable.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3)
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .ToList();

    // ---- Le regole, in ordine di applicazione --------------------------------------------------------

    /// <summary>Un VID annunciato come tale: «VID 123456», «vid: 1234567».</summary>
    [GeneratedRegex(@"\bVID\s*:?\s*\d{4,8}\b", RegexOptions.IgnoreCase)]
    private static partial Regex VidAnnunciato();

    /// <summary>Una sequenza di 6-8 cifre isolata: la forma di un VID IVAO anche senza etichetta.
    /// ⚠️ Volutamente larga. Se prende un numero che VID non era, si perde un numero nella traduzione — se
    /// non lo prendesse, uscirebbe un identificativo di persona.</summary>
    [GeneratedRegex(@"(?<![\d.,])\d{6,8}(?![\d.,])")]
    private static partial Regex ForseUnVid();

    /// <summary>Callsign ATC: <c>LIRF_TWR</c>, <c>LIPP_MIL_CTR</c>, <c>LIRR_NE1_CTR</c>.</summary>
    [GeneratedRegex(@"\b[A-Z]{2,4}(?:_[A-Z0-9]{1,4})+\b")]
    private static partial Regex Callsign();

    /// <summary>Frequenza radio: <c>126.850</c>, <c>118.1</c>.</summary>
    [GeneratedRegex(@"\b1[0-3]\d\.\d{1,3}\b")]
    private static partial Regex Frequenza();

    /// <summary>Livello di volo: <c>FL120</c>, <c>FL 75</c>.</summary>
    [GeneratedRegex(@"\bFL\s?\d{2,3}\b")]
    private static partial Regex LivelloDiVolo();

    /// <summary>Pista: <c>RWY 16R</c>, <c>16L</c>, <c>04</c> solo se preceduta da RWY.</summary>
    [GeneratedRegex(@"\bRWY\s?[0-3]\d[LRC]?\b|\b[0-3]\d[LRC]\b")]
    private static partial Regex Pista();

    /// <summary>Canale TACAN: <c>CH 37X</c>, <c>CH111X</c>.</summary>
    [GeneratedRegex(@"\bCH\s?\d{1,3}[XY]\b")]
    private static partial Regex CanaleTacan();

    /// <summary>Transponder annunciato: <c>SQUAWK 7000</c>.</summary>
    [GeneratedRegex(@"\bSQUAWK\s?[0-7]{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Squawk();

    /// <summary>
    /// Sigla tutta maiuscola: ICAO (<c>LIRF</c>), punto (<c>BEKIV</c>, <c>QUIESA</c>), o punto alfanumerico
    /// (<c>RPN1</c>, <c>RPW1</c>).
    /// <para>⚠️ Quattro lettere <b>o più</b>, non «quattro o cinque»: i nomi dei punti veri non stanno nella
    /// forma ICAO a cinque lettere. Nei SOP misurati ci sono <c>QUIESA</c> (sei) accanto a <c>RPN1</c>
    /// (tre più una cifra), e una regola tarata sui cinque li perde entrambi.</para>
    /// <para>⚠️ Si applica <b>solo</b> se il testo intorno ha delle minuscole. In una cella tutta maiuscola —
    /// «REVIEW CYCLE» — ogni parola somiglierebbe a un identificatore, e non si tradurrebbe più niente.</para>
    /// </summary>
    [GeneratedRegex(@"\b[A-Z]{2,}\d{1,2}\b|\b[A-Z]{4,}\b")]
    private static partial Regex SiglaMaiuscola();

    /// <summary>I marcatori di grassetto e corsivo. Protetti perché il motore può spostarli o mangiarli, e
    /// un asterisco spaiato rompe la resa del blocco.</summary>
    [GeneratedRegex(@"\*{1,2}")]
    private static partial Regex MarcatoriMarkdown();

    /// <summary>Il segnaposto, come si scrive e come si rilegge.</summary>
    [GeneratedRegex(@"<x id=""(\d+)""\s*/>")]
    private static partial Regex Segnaposto();

    /// <summary>Vero se il testo ha delle lettere minuscole: allora è prosa, e le sigle maiuscole spiccano.</summary>
    private static bool HaMinuscole(string s) => s.Any(char.IsLower);

    // ---- Protezione ----------------------------------------------------------------------------------

    /// <summary>
    /// Sostituisce con segnaposto vuoti tutto ciò che non si traduce e tutto ciò che non deve uscire.
    /// L'ordine conta: prima i dati personali, poi gli identificatori dal più specifico al più generico —
    /// altrimenti una regola larga mangia un pezzo di ciò che una stretta avrebbe riconosciuto per intero.
    /// </summary>
    public ProtectedText Protect(string? testo)
    {
        var s = TranslationText.Normalize(testo);
        var tokens = new List<string>();

        // ⚠️ La domanda «questo testo è prosa?» si fa ORA, sull'originale. I segnaposto contengono minuscole
        // (`<x id="0"/>`), quindi chiederlo dopo direbbe «prosa» anche di una cella tutta maiuscola in cui
        // sia stata protetta una frequenza — e da lì in poi ogni parola di quella cella verrebbe scambiata
        // per una sigla e non si tradurrebbe più niente.
        var eProsa = HaMinuscole(s);

        // 1. DATI PERSONALI, per primi e sempre: se una regola successiva ne spezzasse uno, quello che
        //    resta uscirebbe in chiaro.
        foreach (var nome in _nomi)
            s = SostituisciLetterale(s, nome, tokens);
        s = Sostituisci(s, VidAnnunciato(), tokens);
        s = Sostituisci(s, ForseUnVid(), tokens);

        // 2. IDENTIFICATORI, dal più specifico al più generico.
        s = Sostituisci(s, Callsign(), tokens);
        s = Sostituisci(s, CanaleTacan(), tokens);
        s = Sostituisci(s, Squawk(), tokens);
        s = Sostituisci(s, Frequenza(), tokens);
        s = Sostituisci(s, LivelloDiVolo(), tokens);
        s = Sostituisci(s, Pista(), tokens);
        if (eProsa) s = Sostituisci(s, SiglaMaiuscola(), tokens);

        // 3. Marcatori di formattazione.
        s = Sostituisci(s, MarcatoriMarkdown(), tokens);

        return new ProtectedText(s, tokens, Safe: !RestaQualcosaDiPersonale(s));
    }

    /// <summary>
    /// Il cancello <b>fail closed</b> (§3b): dopo la protezione non deve restare niente che somigli a un
    /// dato personale. Se resta, il segmento <b>non si manda</b> e si marca «da tradurre a mano».
    /// <para>⚠️ Rifiutare è sicuro; ripulire in silenzio no — cambierebbe il testo sotto i piedi di chi
    /// scrive, e la traduzione tornerebbe disallineata dal sorgente di cui porta l'impronta.</para>
    /// </summary>
    public bool RestaQualcosaDiPersonale(string? testoProtetto)
    {
        if (string.IsNullOrEmpty(testoProtetto)) return false;
        if (VidAnnunciato().IsMatch(testoProtetto) || ForseUnVid().IsMatch(testoProtetto)) return true;
        // A parola intera, come la protezione: altrimenti «crossing» in una vLOA di confine renderebbe
        // «non sicuro» — quindi non traducibile — ogni blocco che parla di attraversamenti.
        return _nomi.Any(n => TrovaParolaIntera(testoProtetto, n));
    }

    // ---- Ripristino ----------------------------------------------------------------------------------

    /// <summary>
    /// Rimette al loro posto i pezzi protetti dentro il testo tornato dal motore.
    /// <para>⚠️ Torna <c>false</c> se un segnaposto non è tornato indietro, o se ne è tornato uno che non
    /// esisteva: vuol dire che il motore l'ha mangiato o inventato, e la traduzione ha <b>perso un
    /// identificatore</b>. Una frase a cui manca il callsign è peggio della frase non tradotta, e va
    /// buttata invece che salvata.</para>
    /// </summary>
    public static bool TryRestore(string? tradotto, IReadOnlyList<string> tokens, out string risultato)
    {
        risultato = tradotto ?? "";
        if (tokens.Count == 0) return true;

        var visti = new bool[tokens.Count];
        var ok = true;
        risultato = Segnaposto().Replace(risultato, m =>
        {
            if (!int.TryParse(m.Groups[1].Value, out var i) || i < 0 || i >= tokens.Count)
            {
                ok = false;             // un segnaposto che non abbiamo mai messo
                return m.Value;
            }
            visti[i] = true;
            return tokens[i];
        });

        return ok && visti.All(v => v);
    }

    // ---- Meccanica -----------------------------------------------------------------------------------

    private static string Sostituisci(string s, Regex regola, List<string> tokens) =>
        regola.Replace(s, m => Deposita(m.Value, tokens));

    /// <summary>
    /// Un nome non è un'espressione regolare: si cerca come testo, senza distinguere maiuscole, ma
    /// <b>a parola intera</b>.
    /// <para>
    /// ⚠️ La parola intera non è pignoleria: senza, un cognome comune trasforma in segnaposto i pezzi delle
    /// parole che lo contengono. Trovato dal test sul corpus reale, dove «Rossi» sta dentro
    /// «c<b>rossi</b>ng», che nelle vLOA compare in ogni frase di confine — il documento pubblicato sarebbe
    /// uscito a brandelli, e non per un dato personale ma per una collisione di lettere.
    /// </para>
    /// </summary>
    private static string SostituisciLetterale(string s, string ago, List<string> tokens)
    {
        var sb = new StringBuilder();
        var da = 0;
        while (true)
        {
            var i = s.IndexOf(ago, da, StringComparison.OrdinalIgnoreCase);
            if (i < 0) break;
            if (ParolaIntera(s, i, ago.Length))
            {
                sb.Append(s, da, i - da).Append(Deposita(s.Substring(i, ago.Length), tokens));
                da = i + ago.Length;
            }
            else
            {
                // Falso positivo: si prosegue di un carattere, o «Rossi» dentro «crossing» bloccherebbe la
                // ricerca di un «Rossi» vero più avanti nella stessa frase.
                sb.Append(s, da, i + 1 - da);
                da = i + 1;
            }
        }
        return sb.Append(s, da, s.Length - da).ToString();
    }

    /// <summary>Vero se <paramref name="ago"/> compare in <paramref name="s"/> come parola intera.</summary>
    private static bool TrovaParolaIntera(string s, string ago)
    {
        for (var da = 0; da <= s.Length - ago.Length;)
        {
            var i = s.IndexOf(ago, da, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            if (ParolaIntera(s, i, ago.Length)) return true;
            da = i + 1;
        }
        return false;
    }

    /// <summary>Vero se l'occorrenza non è incastonata dentro una parola più lunga.</summary>
    private static bool ParolaIntera(string s, int inizio, int lunghezza)
    {
        var prima = inizio == 0 || !char.IsLetterOrDigit(s[inizio - 1]);
        var fine = inizio + lunghezza;
        var dopo = fine >= s.Length || !char.IsLetterOrDigit(s[fine]);
        return prima && dopo;
    }

    private static string Deposita(string valore, List<string> tokens)
    {
        tokens.Add(valore);
        return $"<x id=\"{tokens.Count - 1}\"/>";
    }
}

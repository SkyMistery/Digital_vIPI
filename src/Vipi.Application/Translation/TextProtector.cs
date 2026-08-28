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
/// ⚠️ <b>I segnaposto hanno DUE forme, e la differenza e' misurata.</b> Un identificatore pubblico viaggia
/// dentro il tag — <c>&lt;x id="0"&gt;LIRF_TWR&lt;/x&gt;</c> — perche' al motore serve l'ancora per capire
/// la frase. Un dato personale no: li' il tag resta <b>vuoto</b>, ed e' l'unica forma in cui si puo'
/// affermare che il dato non ha lasciato il processo, invece di sperarlo.
/// </para>
///
/// <para>
/// <b>Il prezzo del tag vuoto, contro il servizio vero</b> (Azure Translator, 27 agosto 2026): «Contatta X
/// sulla Y e riporta sottovento» col segnaposto vuoto torna <i>«Contact X <b>on and</b> Y bring it back
/// downwind»</i> — senza ancora, il motore perde l'ordine delle parole. Col valore dentro il tag torna
/// <i>«Contact LIRF_TWR <b>on</b> 118.1 <b>and</b> bring it back downwind»</i>, cioe' la stessa qualita' del
/// testo non protetto. Per i dati personali quel prezzo si paga volentieri: sono pochi segmenti, e sono
/// proprio quelli che vogliono comunque una persona.
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

    /// <summary>
    /// Il segnaposto, come si scrive e come si rilegge.
    ///
    /// <para>
    /// ⚠️ <b>In lettura si accettano tre forme.</b> Noi scriviamo <c>&lt;x id="0"&gt;VALORE&lt;/x&gt;</c> per
    /// gli identificatori e <c>&lt;x id="0"/&gt;</c> per i dati personali, ma un motore che tratta il testo
    /// come marcatura puo' restituire l'una nell'altra — o chiudere il tag vuoto per esteso. Se la lettura
    /// pretendesse la forma esatta, ogni segmento con un callsign risulterebbe «segnaposto mangiato» e
    /// finirebbe fra gli scartati: la traduzione non funzionerebbe mai, e il rapporto darebbe la colpa al
    /// motore.
    /// </para>
    /// <para>
    /// ⚠️ Il valore che torna dentro il tag <b>si ignora</b>: vale sempre quello che avevamo messo da parte.
    /// Un motore che «migliorasse» un callsign non deve poterlo scrivere nel documento.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"<x id=""(\d+)""\s*/>|<x id=""(\d+)""[^>]*>(.*?)</x\s*>", RegexOptions.Singleline)]
    private static partial Regex Segnaposto();

    /// <summary>L'indice del segnaposto, da qualunque delle forme accettate.</summary>
    private static string IndiceDi(Match m) =>
        m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;

    /// <summary>Il contenuto del segnaposto tornato indietro; null per la forma autochiudente.</summary>
    private static string? ContenutoDi(Match m) => m.Groups[3].Success ? m.Groups[3].Value : null;

    /// <summary>Vero se il testo ha delle lettere minuscole: allora è prosa, e le sigle maiuscole spiccano.</summary>
    private static bool HaMinuscole(string s) => s.Any(char.IsLower);

    /// <summary>
    /// Vero se il segmento è <b>una parola sola</b> tutta maiuscola: <c>MARTE</c>, <c>CHI</c>, <c>NAXAV</c>,
    /// <c>PONY</c>, <c>NIL</c>. Una cella così è un <b>identificatore</b>, non una frase.
    ///
    /// <para>
    /// ⚠️ <b>Misurato il 28 agosto 2026 sul primo SOP vero</b>, e questo è il caso peggiore visto finora: la
    /// cella <c>MARTE</c> è tornata <i>MARS</i> e la cella <c>CHI</c> è tornata <i>WHO</i>. Sono nomi di punti
    /// significativi in un piano di volo: un pilota che pianifica <i>WHO</i> non trova niente. Non è una
    /// traduzione brutta, è un <b>dato falso</b>.
    /// </para>
    /// <para>
    /// ⚠️ Perché la regola sulle sigle maiuscole non bastava: si applica solo dove c'è della prosa attorno
    /// (<c>eProsa</c>), e in una cella che è <i>solo</i> «MARTE» di minuscole non ce n'è. La condizione giusta
    /// non è «ci sono minuscole» ma «è una parola sola»: «REVIEW CYCLE», che di parole ne ha due, resta
    /// traducibile.
    /// </para>
    /// <para>
    /// ⚠️ Il prezzo: una cella che fosse una parola sola scritta in maiuscolo <i>e</i> da tradurre — «NOTE»
    /// come intestazione — resta in italiano. È il prezzo giusto: una parola non tradotta si vede, un nome
    /// di punto cambiato no.
    /// </para>
    /// </summary>
    private static bool UnaParolaSolaMaiuscola(string s)
    {
        var t = s.Trim();
        if (t.Length < 2 || t.Any(char.IsWhiteSpace)) return false;
        // Almeno una lettera, e nessuna minuscola: «MARTE», «RPN1», «H24», ma non «06» né «---».
        return t.Any(char.IsLetter) && !t.Any(char.IsLower);
    }

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

        // 0. UNA PAROLA SOLA, TUTTA MAIUSCOLA: è un identificatore, e si mette da parte per intero prima di
        //    ogni altra regola. Vedi il commento di `UnaParolaSolaMaiuscola`: qui si sono persi «MARTE» e
        //    «CHI», che sono punti di un piano di volo.
        if (UnaParolaSolaMaiuscola(s))
            // Safe: in una parola sola tutta maiuscola non c'è niente di personale — e comunque il testo
            // protetto resta VUOTO, quindi il segmento non parte affatto.
            return new ProtectedText(Deposita(s, tokens, Riservatezza.Intraducibile), tokens, Safe: true);

        // 1. DATI PERSONALI, per primi e sempre: se una regola successiva ne spezzasse uno, quello che
        //    resta uscirebbe in chiaro.
        foreach (var nome in _nomi)
            s = SostituisciLetterale(s, nome, tokens, Riservatezza.Personale);
        s = Sostituisci(s, VidAnnunciato(), tokens, Riservatezza.Personale);
        s = Sostituisci(s, ForseUnVid(), tokens, Riservatezza.Personale);

        // 2. IDENTIFICATORI, dal più specifico al più generico.
        s = Sostituisci(s, Callsign(), tokens);
        s = Sostituisci(s, CanaleTacan(), tokens);
        s = Sostituisci(s, Squawk(), tokens);
        s = Sostituisci(s, Frequenza(), tokens);
        s = Sostituisci(s, LivelloDiVolo(), tokens);
        s = Sostituisci(s, Pista(), tokens);
        if (eProsa) s = Sostituisci(s, SiglaMaiuscola(), tokens);

        // 3. I marcatori di grassetto NON si proteggono, e la ragione e' misurata (Azure, 27 agosto 2026).
        //    Proteggerli spezza la frase in tre e il motore SPOSTA LE PAROLE DENTRO I TAG:
        //      IN  «is initiated <x id="0">**</x>not later than 5 minutes<x id="1">**</x> before…»
        //      OUT «viene <x id="0">avviato **</x>non oltre 5 <x id="1">minuti**</x> prima…»
        //    e il ripristino, che sostituisce il tag col gettone, CANCELLA «avviato» e «minuti». Lasciati
        //    stare, la stessa frase esce intera e con gli asterischi al loro posto: per il motore in
        //    modalita' marcatura un asterisco e' testo, non struttura, e non ha motivo di spostarlo.

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
            if (!int.TryParse(IndiceDi(m), out var i) || i < 0 || i >= tokens.Count)
            {
                ok = false;             // un segnaposto che non abbiamo mai messo
                return m.Value;
            }

            // ⚠️ IL MOTORE PUO' TOCCARE IL CONTENUTO DEL TAG, e le tre cose che puo' fare vogliono tre
            // risposte diverse. Misurato su Azure col corpus vero (27 agosto 2026), 12 casi su 30:
            //
            //   1. lo lascia com'e'            -> a posto
            //   2. ci INFILA DENTRO altro      -> «<x>con LGGG</x>», «<x>**LGGG</x>»: la preposizione o
            //      l'asterisco appartengono alla traduzione, e il nostro valore e' ancora li'. Si tiene
            //      quello che e' tornato: buttarlo perderebbe una parola giusta.
            //   3. lo CAMBIA o lo perde        -> «messo RWY 07, tornato RWY 25» (Azure ha invertito
            //      "RWY 07/25" in "RWY 25/07"), oppure «messo LYBA, tornato /». Qui non si ripara niente:
            //      la frase e' compromessa e si BUTTA. Accettare quel caso avrebbe scritto una PISTA
            //      SBAGLIATA in un documento operativo, ed e' il motivo per cui questo controllo esiste.
            var contenuto = ContenutoDi(m);
            visti[i] = true;

            var atteso = tokens[i];

            // Niente contenuto da confrontare: e' la forma autochiudente, oppure il tag e' tornato vuoto.
            // ⚠️ Il vuoto si ACCETTA in entrambi i casi, e per due ragioni diverse: per un dato personale
            // il tag e' PARTITO vuoto (e' tutto il punto), e per un identificatore un tag svuotato dal
            // motore si richiude rimettendoci dentro il valore nostro, che e' la cosa giusta. Trattarlo
            // come «valore perso» avrebbe buttato via ogni segmento con un VID o un nome dentro.
            if (contenuto is null || contenuto.Trim().Length == 0) return atteso;
            if (contenuto.Trim() == atteso.Trim()) return atteso;          // caso 1
            if (contenuto.Contains(atteso, StringComparison.Ordinal)) return contenuto;   // caso 2

            ok = false;                                                    // caso 3
            return atteso;
        });

        return ok && visti.All(v => v);
    }

    // ---- Meccanica -----------------------------------------------------------------------------------

    /// <summary>
    /// Applica una regola <b>solo fuori dai segnaposto gia' piazzati</b>.
    ///
    /// <para>
    /// ⚠️ <b>Perche' non basta un Replace sull'intera stringa.</b> Da quando gli identificatori viaggiano
    /// DENTRO il tag, il loro valore e' testo visibile alle regole che vengono dopo: su
    /// «Imposta SQUAWK 7000 subito» la regola dello squawk produce
    /// <c>&lt;x id="0"&gt;SQUAWK 7000&lt;/x&gt;</c>, e subito dopo la regola delle sigle maiuscole vedrebbe
    /// «SQUAWK» li' dentro e lo avvolgerebbe in un secondo tag — <b>annidato dentro il primo</b>. Il testo
    /// che parte sarebbe marcatura rotta, e al ritorno il ripristino non ritroverebbe piu' i pezzi.
    /// Trovato da un test, non a runtime.
    /// </para>
    /// </summary>
    private static string Sostituisci(string s, Regex regola, List<string> tokens,
                                      Riservatezza riservatezza = Riservatezza.Pubblico)
    {
        var sb = new StringBuilder(s.Length);
        var da = 0;

        foreach (Match segnaposto in Segnaposto().Matches(s))
        {
            var prima = s.Substring(da, segnaposto.Index - da);
            sb.Append(regola.Replace(prima, m => Deposita(m.Value, tokens, riservatezza)));
            sb.Append(segnaposto.Value);          // intoccabile: e' gia' protetto
            da = segnaposto.Index + segnaposto.Length;
        }

        var resto = s.Substring(da);
        return sb.Append(regola.Replace(resto, m => Deposita(m.Value, tokens, riservatezza))).ToString();
    }

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
    private static string SostituisciLetterale(string s, string ago, List<string> tokens, Riservatezza riservatezza)
    {
        var sb = new StringBuilder();
        var da = 0;
        while (true)
        {
            var i = s.IndexOf(ago, da, StringComparison.OrdinalIgnoreCase);
            if (i < 0) break;
            if (ParolaIntera(s, i, ago.Length))
            {
                sb.Append(s, da, i - da).Append(Deposita(s.Substring(i, ago.Length), tokens, riservatezza));
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

    /// <summary>
    /// Se il valore protetto puo' VIAGGIARE (identificatore pubblico) o no (dato personale).
    /// </summary>
    private enum Riservatezza
    {
        /// <summary>Callsign, ICAO, frequenza, livello, pista: pubblici. Vanno dentro il tag.</summary>
        Pubblico,

        /// <summary>VID, nomi di persona: il tag resta VUOTO e il valore non lascia il processo.</summary>
        Personale,

        /// <summary>
        /// Pubblico ma <b>intraducibile</b>: il segmento è tutto qui dentro, quindi il tag resta vuoto e non
        /// c'è più niente da spedire. ⚠️ Col valore dentro il tag il segmento partirebbe lo stesso, il motore
        /// lo cambierebbe («MARTE» → «MARS»), il ripristino lo scarterebbe — e questo a <b>ogni giro, per
        /// sempre</b>, con un contatore «scartati» che sale e non vuol dire niente.
        /// </summary>
        Intraducibile,
    }

    /// <summary>
    /// Mette da parte un valore e restituisce il suo segnaposto.
    ///
    /// <para>
    /// ⚠️ <b>Le due forme non sono un vezzo: sono state MISURATE contro il servizio vero</b> (27 agosto 2026,
    /// Azure Translator). Con il segnaposto vuoto, «Contatta X sulla Y e riporta sottovento» torna
    /// <i>«Contact X <b>on and</b> Y bring it back downwind»</i>: senza l'ancora, il motore perde l'ordine
    /// delle parole. Col valore dentro il tag torna <i>«Contact LIRF_TWR <b>on</b> 118.1 <b>and</b> bring it
    /// back downwind»</i> — la stessa qualita' del testo non protetto.
    /// </para>
    /// <para>
    /// Quindi: <b>gli identificatori viaggiano</b> (sono pubblici, e servono al motore per capire la frase);
    /// <b>i dati personali no</b>, e li' si paga la qualita' — ma sono pochi segmenti, e sono proprio quelli
    /// che vogliono comunque una persona.
    /// </para>
    /// </summary>
    /// <summary>
    /// Vero se del testo protetto non resta che <b>segnaposto</b>: non c'è più niente da tradurre, e
    /// spedirlo sarebbe pagare per farsi restituire ciò che abbiamo già.
    ///
    /// <para>
    /// ⚠️ Non è un'ottimizzazione: senza questo controllo una cella come «MARTE» parte, il motore la
    /// «traduce», il ripristino la scarta perché il contenuto è cambiato — e succede a <b>ogni giro, per
    /// sempre</b>, con un contatore «scartati» che sale e non vuol dire niente.
    /// </para>
    /// </summary>
    public static bool SoloSegnaposti(string? protetto)
    {
        if (string.IsNullOrWhiteSpace(protetto)) return true;
        var resto = Segnaposto().Replace(protetto, "");
        return !TranslationText.HasSomethingToTranslate(resto);
    }

    private static string Deposita(string valore, List<string> tokens, Riservatezza riservatezza)
    {
        tokens.Add(valore);
        var i = tokens.Count - 1;

        // Vuoto quando il valore non deve uscire, e anche quando romperebbe la marcatura: le nostre regole
        // sugli identificatori non producono mai parentesi angolari o e-commerciali, ma se un giorno lo
        // facessero, meglio una frase tradotta peggio che una richiesta rifiutata dal motore.
        if (riservatezza is Riservatezza.Personale or Riservatezza.Intraducibile
            || valore.IndexOfAny(new[] { '&', '<', '>' }) >= 0)
            return $"<x id=\"{i}\"/>";

        return $"<x id=\"{i}\">{valore}</x>";
    }
}

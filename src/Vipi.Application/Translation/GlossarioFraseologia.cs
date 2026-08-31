using Vipi.Application.Abstractions;

namespace Vipi.Application.Translation;

/// <summary>Una voce del glossario: come si dice in italiano, e come si dice — sempre — in inglese.</summary>
/// <param name="Sorgente">La frase nella lingua del documento. ⚠️ Si cerca <b>a parola intera</b> e senza
/// distinguere le maiuscole, come i nomi del roster nel protettore.</param>
/// <param name="Resa">Quello che va scritto al suo posto. Va nel documento <b>verbatim</b>: il motore non
/// lo tocca, e nessuno lo declina.</param>
public sealed record VoceGlossario(string Sorgente, string Resa);

/// <summary>Perché una voce non si può accettare. Il testo per chi legge lo mette la pagina, non questo strato.</summary>
public enum GlossarioRifiuto
{
    /// <summary>Uno dei due lati è vuoto.</summary>
    Vuoto,

    /// <summary>Meno di quattro caratteri: sotto quella soglia la parola intera non basta più a difendere.</summary>
    TroppoCorto,

    /// <summary>
    /// Più lungo di quanto la colonna tenga.
    /// <para>⚠️ Il cancello sta <b>qui</b> e non solo nel database: un rifiuto del database arriva come
    /// eccezione durante il salvataggio, cioè come pagina d'errore a chi stava scrivendo una voce, e senza
    /// dirgli quale dei due campi era troppo lungo.</para>
    /// <para>Ma il numero non è arbitrario: una voce di glossario è una <b>formula</b>, e una formula più
    /// lunga di così è una frase — e le frasi intere le tratta già la memoria di traduzione.</para>
    /// </summary>
    TroppoLungo,

    /// <summary>
    /// Nella sorgente c'è qualcosa che il protettore tratterebbe da <b>identificatore</b> — un callsign, una
    /// frequenza, una pista. ⚠️ Il glossario passa <b>prima</b> di quelle regole, quindi se lo tenesse
    /// dentro se lo inghiottirebbe: l'identificatore finirebbe nel testo fisso della resa, uguale per ogni
    /// documento in cui la frase compare. Una frequenza sbagliata scritta in cento carte, e nessun errore.
    /// </summary>
    ContieneIdentificatore,

    /// <summary>Ci sono <c>&lt;</c>, <c>&gt;</c> o <c>&amp;</c>: romperebbero la marcatura dei segnaposto.</summary>
    ContieneMarcatura,

    /// <summary>Quella sorgente c'è già. Due rese per la stessa frase le sceglierebbe il caso.</summary>
    Duplicato,
}

/// <summary>
/// Il <b>glossario di fraseologia</b>: le frasi che si dicono in un modo solo, e che nessun motore deve
/// scegliere per noi (<c>lavori-aperti §Q3</c>, carta <c>2026-08-27-documenti-bilingue.md</c> §5).
///
/// <para>
/// ⚠️ <b>Perché non bastava <see cref="TitoliUfficiali"/>.</b> Quella lista si semina nella memoria di
/// traduzione, e la memoria è indicizzata per <b>segmento intero</b>: funziona su un titolo o su una cella
/// di tabella, che <i>sono</i> un segmento. Su una frase intera non funziona — la frase è diversa in ogni
/// documento, e la sua impronta pure. Restano fuori proprio i casi che la carta chiama il rischio numero
/// uno: la fraseologia <b>dentro</b> le frasi.
/// </para>
///
/// <para>
/// <b>Come funziona, in una riga.</b> Prima di spedire, il protettore mette la frase italiana in un
/// segnaposto <c>&lt;g&gt;</c> e tiene da parte la resa inglese; al ritorno rimette la <b>nostra</b> resa,
/// qualunque cosa il motore abbia fatto lì dentro. È lo stesso meccanismo dei callsign, con una differenza
/// sola e voluta: per un callsign si rimette ciò che era partito, per una voce di glossario si rimette
/// un'<b>altra</b> cosa.
/// </para>
///
/// <para>
/// ⚠️ <b>La resa entra verbatim, e questo detta che cosa può stare qui dentro.</b> Non c'è declinazione, non
/// c'è concordanza, non c'è contesto: «report downwind» è quella stringa lì, in ogni frase in cui la voce
/// scatta. Vanno bene le formule che <i>sono</i> fisse — la fraseologia standard lo è per definizione — e va
/// male una parola comune che cambia forma. È il motivo per cui questa lista la cura un controllore e non
/// chi scrive il codice: sapere quali formule sono fisse è il suo mestiere, non il nostro.
/// </para>
/// </summary>
public sealed class GlossarioFraseologia
{
    /// <summary>Nessuna voce: il protettore si comporta esattamente come prima che il glossario esistesse.</summary>
    public static readonly GlossarioFraseologia Vuoto = new(Array.Empty<VoceGlossario>());

    /// <summary>Sotto questa lunghezza non si accetta una voce. Vedi <see cref="GlossarioRifiuto.TroppoCorto"/>.</summary>
    public const int LunghezzaMinima = 4;

    /// <summary>⚠️ Sono le larghezze delle colonne di <c>GlossaryTerm</c>: se cambiano là, cambiano qui, o il
    /// cancello lascia passare quello che il database poi rifiuta.</summary>
    public const int LunghezzaMassimaSorgente = 200;

    /// <inheritdoc cref="LunghezzaMassimaSorgente"/>
    public const int LunghezzaMassimaResa = 400;

    private readonly IReadOnlyList<VoceGlossario> _voci;

    /// <param name="voci">Le voci di <b>una sola direzione</b> (it→en oppure en→it). Chi costruisce ha già
    /// filtrato per coppia di lingue: questa classe non sa che lingue siano, e non le serve saperlo.</param>
    public GlossarioFraseologia(IEnumerable<VoceGlossario> voci) =>
        // ⚠️ Dalla più lunga alla più corta, come il roster dei nomi nel protettore e per la stessa ragione:
        // se «riporta» scattasse prima di «riporta sottovento», della formula intera resterebbe in italiano
        // la metà — e quella metà non la vedrebbe più nessuno, perché la prima ha già consumato il testo.
        _voci = voci
            .Where(v => !string.IsNullOrWhiteSpace(v.Sorgente) && !string.IsNullOrWhiteSpace(v.Resa))
            .Select(v => new VoceGlossario(v.Sorgente.Trim(), v.Resa.Trim()))
            .DistinctBy(v => v.Sorgente, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => v.Sorgente.Length)
            .ToList();

    /// <summary>Le voci, già ordinate come vanno applicate.</summary>
    public IReadOnlyList<VoceGlossario> Voci => _voci;

    public int Count => _voci.Count;

    /// <summary>
    /// Perché questa voce non si può accettare, o <c>null</c> se va bene. La chiamano la pagina di cura
    /// <b>e</b> il seme, così una voce cablata nel codice non può entrare per una porta che a una voce
    /// scritta a mano sarebbe chiusa.
    /// </summary>
    /// <param name="sorgenti">Le sorgenti già presenti, per il controllo di duplicato. Può essere null.</param>
    public static GlossarioRifiuto? PerchéNonVa(
        string? sorgente, string? resa, IEnumerable<string>? sorgenti = null)
    {
        if (string.IsNullOrWhiteSpace(sorgente) || string.IsNullOrWhiteSpace(resa))
            return GlossarioRifiuto.Vuoto;

        var s = sorgente.Trim();
        var r = resa.Trim();

        if (s.Length < LunghezzaMinima || r.Length < LunghezzaMinima)
            return GlossarioRifiuto.TroppoCorto;

        if (s.Length > LunghezzaMassimaSorgente || r.Length > LunghezzaMassimaResa)
            return GlossarioRifiuto.TroppoLungo;

        if (s.IndexOfAny(MarcaturaVietata) >= 0 || r.IndexOfAny(MarcaturaVietata) >= 0)
            return GlossarioRifiuto.ContieneMarcatura;

        if (TextProtector.ContieneIdentificatori(s))
            return GlossarioRifiuto.ContieneIdentificatore;

        if (sorgenti is not null && sorgenti.Any(x => string.Equals(x?.Trim(), s, StringComparison.OrdinalIgnoreCase)))
            return GlossarioRifiuto.Duplicato;

        return null;
    }

    /// <summary>
    /// Vero se la sorgente è una <b>parola sola</b>. Non è un rifiuto — «riattacca» è fraseologia buona — ma
    /// la pagina lo dice a chi la sta scrivendo: una parola sola scatta ovunque, anche dove voleva dire
    /// un'altra cosa, e chi la mette deve averci pensato.
    /// </summary>
    public static bool ParolaSola(string? sorgente) =>
        !string.IsNullOrWhiteSpace(sorgente) && !sorgente.Trim().Any(char.IsWhiteSpace);

    private static readonly char[] MarcaturaVietata = { '<', '>', '&' };

    /// <summary>
    /// Le voci con cui il glossario nasce, per la coppia <b>it→en</b>. Si seminano una volta e da lì in poi
    /// vivono nella tabella, dove un controllore le corregge senza toccare il codice.
    ///
    /// <para>
    /// ⚠️ <b>I primi tre gruppi sono difetti VISTI, non ipotesi</b>, e sono la ragione per cui questa classe
    /// esiste. Le altre sono fraseologia standard, messa qui perché la lista non nasca vuota — ma con una
    /// differenza che va detta: dei primi tre sappiamo che cosa faceva la macchina, delle altre no. Chi
    /// cura il glossario è libero di toglierle.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<VoceGlossario> Semi = new VoceGlossario[]
    {
        // ---- MISURATE contro il servizio vero. Ognuna ha una resa sbagliata alle spalle. ----

        // Azure, 27 agosto 2026 (carta §5): «Contatta LIRF_TWR sulla 118.1 e riporta sottovento» tornava
        // «…and BRING IT BACK DOWNWIND». Grammatica giusta, fraseologia inesistente. È l'esempio con cui la
        // carta dimostra che il glossario non è un accessorio della funzione: è la funzione.
        new("riporta sottovento", "report downwind"),

        // LIPI Rivolto, 28 agosto 2026 (lavori-aperti §R2): «il campo» tornava «the CAMP» — un accampamento.
        // Le tre forme perché la preposizione articolata fa parte della frase: cercando solo «il campo»,
        // «a nord del campo» resterebbe scoperto, ed è la forma che nei SOP compare di più.
        new("il campo", "the airfield"),
        new("del campo", "of the airfield"),
        new("sul campo", "on the airfield"),

        // LIPI Rivolto, 28 agosto 2026 (§R2): «le posizioni di armamento e disarmo» tornava «the COCKING and
        // disarming positions». ⚠️ Il titolo di sezione «Armamento/disarmo» era già coperto da
        // TitoliUfficiali — ed è esattamente la dimostrazione del limite di quella lista: lo stesso concetto,
        // dentro una frase invece che come titolo, non era coperto da niente.
        new("armamento e disarmo", "arming and dearming"),

        // ---- FRASEOLOGIA STANDARD. Non misurate: nessuna resa sbagliata alle spalle, solo la certezza che
        //      la forma inglese è UNA. Stanno qui perché la lista non nasca vuota. ----

        new("riporta in finale", "report final"),
        new("riporta in base", "report base"),
        new("riporta il campo in vista", "report field in sight"),
        new("riporta la pista in vista", "report runway in sight"),
        new("riporta stabilizzato", "report established"),
        new("mantieni la posizione", "hold position"),
        new("attendi a punto attesa", "hold short"),
        new("allinea e attendi", "line up and wait"),
        new("autorizzato al decollo", "cleared for take-off"),
        new("autorizzato all'atterraggio", "cleared to land"),
        new("continua l'avvicinamento", "continue approach"),
        new("avvicinamento interrotto", "missed approach"),
        new("libera la pista", "vacate the runway"),
        new("messa in moto approvata", "start-up approved"),
        new("traffico in vista", "traffic in sight"),
        new("circuito di traffico", "traffic circuit"),
        new("punto attesa", "holding point"),
    };

    /// <summary>Il glossario di una direzione, letto dal deposito. Una lettura sola per giro.</summary>
    public static async Task<GlossarioFraseologia> CaricaAsync(
        IGlossaryStore deposito, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var voci = await deposito.ListAsync(sourceLang, targetLang, cerca: null, ct).ConfigureAwait(false);
        return voci.Count == 0
            ? Vuoto
            : new GlossarioFraseologia(voci.Select(v => new VoceGlossario(v.SourceText, v.TargetText)));
    }

    /// <summary>
    /// Mette i <see cref="Semi"/> nel deposito, <b>solo se in quella direzione non c'è ancora niente</b>.
    ///
    /// <para>
    /// ⚠️ <b>La condizione è «vuoto», non «questa voce manca», e la differenza è tutta qui.</b> Con la
    /// seconda, una voce che il curatore ha <i>tolto</i> — perché in italiano non si dice così, perché su
    /// quel campo si dice altro — tornerebbe al riavvio successivo, per sempre, senza che nessuno capisca da
    /// dove. Questa lista è il <b>contenuto iniziale</b> di una cosa che appartiene a una persona, non una
    /// regola che il codice fa rispettare: dal primo momento in cui quella persona la tocca, il codice non ci
    /// scrive più.
    /// </para>
    /// <para>
    /// ⚠️ Le voci passano dallo <b>stesso cancello</b> della pagina di cura (<see cref="PerchéNonVa"/>): una
    /// voce cablata qui dentro non può entrare da una porta che a una scritta a mano sarebbe chiusa. Se un
    /// giorno un seme non passasse, non entra <b>lui</b> e gli altri sì — e un test lo dice prima che accada.
    /// </para>
    /// </summary>
    /// <returns>Quante voci ha scritto. Zero è il caso normale dal secondo avvio in poi.</returns>
    public static async Task<int> SeminaAsync(
        IGlossaryStore deposito, string sourceLang = "it", string targetLang = "en",
        CancellationToken ct = default)
    {
        if (await deposito.ContaAsync(sourceLang, targetLang, ct).ConfigureAwait(false) > 0) return 0;

        var scritte = 0;
        var messe = new List<string>(Semi.Count);
        foreach (var voce in Semi)
        {
            ct.ThrowIfCancellationRequested();
            if (PerchéNonVa(voce.Sorgente, voce.Resa, messe) is not null) continue;

            // userId null = nessuna persona: è il contenuto di partenza, non la scelta di qualcuno. La
            // pagina lo mostra così, ed è giusto che si distingua da una voce che un controllore ha voluto.
            await deposito.UpsertAsync(sourceLang, targetLang, voce.Sorgente, voce.Resa, null, ct)
                .ConfigureAwait(false);
            messe.Add(voce.Sorgente);
            scritte++;
        }

        return scritte;
    }
}

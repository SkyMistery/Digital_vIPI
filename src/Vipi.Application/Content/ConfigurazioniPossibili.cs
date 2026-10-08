using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// L'elenco delle configurazioni di <b>un</b> gruppo di settori, come sta scritto in Struttura.
/// </summary>
/// <param name="Genere">Di che cosa è fatto il gruppo: i settori d'area di un ACC, o le posizioni di un ente.</param>
/// <param name="Codice">Il codice dell'ACC o dell'ente.</param>
/// <param name="Configurazioni">Le configurazioni, nell'ordine in cui sono scritte.</param>
/// <param name="Completo">Vero se l'elenco è dichiarato completo: solo allora <b>vincola</b>
/// (<see cref="ConfigurazioniPossibili"/>). Spento, è un elenco di esempi: serve al documento e al banco.</param>
public sealed record ElencoDiConfigurazioni(
    ConfigurationGroupKind Genere, string Codice, IReadOnlyList<AccConfiguration> Configurazioni,
    bool Completo = false);

/// <summary>Che cosa un elenco dice di un settore che nomina — ricavato, mai scritto.</summary>
/// <param name="SempreCon">Gli altri settori presenti in <b>tutte</b> le configurazioni che lo contengono.</param>
/// <param name="MaiCon">I settori nominati dall'elenco che non compaiono mai insieme a lui.</param>
/// <param name="DaSolo">Vero se esiste la configurazione fatta di lui e basta.</param>
public sealed record ConseguenzaDiApertura(
    string Settore, IReadOnlyList<string> SempreCon, IReadOnlyList<string> MaiCon, bool DaSolo);

/// <summary>Un gruppo i cui aperti non sono nessuna delle configurazioni scritte.</summary>
/// <param name="Aperti">I settori <b>nominati dall'elenco</b> che risultano aperti.</param>
public sealed record GruppoFuoriElenco(ElencoDiConfigurazioni Elenco, IReadOnlyList<string> Aperti);

/// <summary>
/// Le <b>configurazioni possibili</b> dei gruppi di settori: quali insiemi di aperti esistono.
///
/// <para><b>Perché esiste.</b> La gerarchia dice chi raccoglie chi, non chi può stare aperto: a Milano
/// <c>ES5</c> apre solo con <c>WS5</c> (che non è suo padre), <c>LIMF_WW0</c> esclude <c>LIMF_WN0</c> (che è suo
/// figlio), e un APP sta aperto col suo CTR chiuso. Il committente, l'8 ottobre 2026: «la gerarchia non ci
/// aiuta». Carta <c>docs/feature/2026-10-08-configurazioni-possibili.md</c>.</para>
///
/// <para>La semantica sono quattro regole, e stanno tutte qui:</para>
/// <list type="number">
/// <item>vincola solo un elenco dichiarato <b>completo</b> (<see cref="ElencoDiConfigurazioni.Completo"/>): gli
/// altri sono esempi, e qui dentro non entrano — a Roma <c>LIRR_EW_CTR</c> sta da solo il 42% del tempo, e
/// nessuna delle tre configurazioni scritte nel suo documento lo dice;</item>
/// <item>un elenco parla dei <b>soli settori che nomina</b> — <c>LIMM_MIL_CTR</c>, che nessuna configurazione
/// di Milano nomina, resta libero e non «mai aperto»;</item>
/// <item>fra i nominati, un insieme di aperti è previsto se è una delle configurazioni scritte, <b>oppure se è
/// vuoto</b>: «tutti chiusi» non si dichiara;</item>
/// <item>un gruppo senza elenco non ha vincoli.</item>
/// </list>
///
/// <para>⚠️ <b>Vale per gli scenari che il sistema si inventa</b> (la sonda della Diagnostica, la scala di
/// risalita, il banco di prova), <b>mai</b> per chi è online davvero: se <c>ES2</c> è in frequenza senza
/// <c>WS2</c> il traffico va a <c>ES2</c>, e questa classe serve al più a dirlo.</para>
///
/// <para>Puro e immutabile, nessun I/O.</para>
/// </summary>
public sealed class ConfigurazioniPossibili
{
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <summary>Nessun elenco: nessun vincolo, com'era prima che la tabella esistesse.</summary>
    public static readonly ConfigurazioniPossibili Nessuna = new(Array.Empty<ElencoDiConfigurazioni>());

    private readonly List<(ElencoDiConfigurazioni Elenco, List<HashSet<string>> Insiemi, HashSet<string> Nominati)> _gruppi = new();

    public ConfigurazioniPossibili(IEnumerable<ElencoDiConfigurazioni> elenchi)
    {
        foreach (var e in elenchi)
        {
            if (!e.Completo) continue;          // un elenco di esempi non vincola niente
            var insiemi = InsiemiDi(e.Configurazioni);
            if (insiemi.Count == 0) continue;   // un elenco senza nemmeno un settore non dice niente
            _gruppi.Add((e, insiemi, new HashSet<string>(insiemi.SelectMany(i => i), OIC)));
        }
    }

    /// <summary>Gli elenchi che vincolano: completi, e con almeno un settore.</summary>
    public IReadOnlyList<ElencoDiConfigurazioni> Elenchi => _gruppi.Select(g => g.Elenco).ToList();

    /// <summary>Vero se nessun gruppo ha un elenco completo: tutto si comporta come prima.</summary>
    public bool Vuote => _gruppi.Count == 0;

    /// <summary>
    /// Chiusi questi, chi <b>non può</b> restare aperto: i settori nominati per cui ogni configurazione che li
    /// contiene contiene anche un chiuso. I chiusi stessi non tornano.
    ///
    /// <para>È chiuso per transitività senza bisogno di rigirare: se <c>X</c> cade perché ogni sua
    /// configurazione tocca un chiuso, le configurazioni di chi sta «sempre con <c>X</c>» sono fra quelle, e
    /// cadono per la stessa ragione. Chiuso <c>WS2</c> cadono <c>ES2</c>, <c>WS5</c> ed <c>ES5</c>.</para>
    /// </summary>
    public IReadOnlySet<string> ChiusiCon(IReadOnlyCollection<string> chiusi)
    {
        var caduti = new HashSet<string>(OIC);
        if (_gruppi.Count == 0 || chiusi.Count == 0) return caduti;

        var k = chiusi as HashSet<string> is { } gia && Equals(gia.Comparer, OIC) ? gia : new HashSet<string>(chiusi, OIC);
        foreach (var (_, insiemi, nominati) in _gruppi)
        {
            if (!nominati.Overlaps(k)) continue;   // nessun chiuso è di questo gruppo: non cambia niente
            foreach (var x in nominati)
                if (!k.Contains(x) && !insiemi.Any(i => i.Contains(x) && !i.Overlaps(k)))
                    caduti.Add(x);
        }
        return caduti;
    }

    /// <summary>
    /// I gruppi i cui aperti non sono una configurazione scritta. Vuoto = lo scenario è previsto.
    /// </summary>
    public IReadOnlyList<GruppoFuoriElenco> NonPreviste(IReadOnlyCollection<string> aperti)
    {
        var fuori = new List<GruppoFuoriElenco>();
        if (_gruppi.Count == 0) return fuori;

        var a = new HashSet<string>(aperti, OIC);
        foreach (var (elenco, insiemi, nominati) in _gruppi)
        {
            var suoi = nominati.Where(a.Contains).OrderBy(x => x, OIC).ToList();
            if (suoi.Count == 0) continue;                          // tutti chiusi: sempre previsto
            if (insiemi.Any(i => i.SetEquals(suoi))) continue;
            fuori.Add(new GruppoFuoriElenco(elenco, suoi));
        }
        return fuori;
    }

    /// <summary>I settori che un elenco nomina, nell'ordine in cui compaiono la prima volta.</summary>
    public static IReadOnlyList<string> Nominati(IEnumerable<AccConfiguration> configurazioni)
    {
        var visti = new HashSet<string>(OIC);
        var ordine = new List<string>();
        foreach (var c in configurazioni)
            foreach (var cs in c.OpenCallsigns)
                if (!string.IsNullOrWhiteSpace(cs) && visti.Add(cs.Trim())) ordine.Add(cs.Trim());
        return ordine;
    }

    /// <summary>
    /// Che cosa l'elenco dice di ogni settore che nomina. È quello che la Struttura mostra sotto l'elenco: chi
    /// legge «WN0: sempre con LIMJ_WS0» e sa che non è vero, sa anche quale configurazione manca.
    /// </summary>
    public static IReadOnlyList<ConseguenzaDiApertura> Conseguenze(IReadOnlyList<AccConfiguration> configurazioni)
    {
        var insiemi = InsiemiDi(configurazioni);
        var nominati = Nominati(configurazioni);
        var esito = new List<ConseguenzaDiApertura>();
        foreach (var x in nominati)
        {
            var suoi = insiemi.Where(i => i.Contains(x)).ToList();
            var sempre = nominati.Where(y => !OIC.Equals(x, y) && suoi.All(i => i.Contains(y))).ToList();
            var mai = nominati.Where(y => !OIC.Equals(x, y) && !suoi.Any(i => i.Contains(y))).ToList();
            esito.Add(new ConseguenzaDiApertura(x, sempre, mai, suoi.Any(i => i.Count == 1)));
        }
        return esito;
    }

    /// <summary>
    /// Le configurazioni come insiemi, senza le vuote e senza i doppioni: una riga appena aggiunta nell'editor
    /// e mai riempita non è «tutti chiusi», è una riga a metà.
    /// </summary>
    private static List<HashSet<string>> InsiemiDi(IEnumerable<AccConfiguration> configurazioni)
    {
        var insiemi = new List<HashSet<string>>();
        foreach (var c in configurazioni)
        {
            var i = new HashSet<string>(
                c.OpenCallsigns.Where(cs => !string.IsNullOrWhiteSpace(cs)).Select(cs => cs.Trim()), OIC);
            if (i.Count > 0 && !insiemi.Any(altro => altro.SetEquals(i))) insiemi.Add(i);
        }
        return insiemi;
    }
}

/// <summary>
/// Un gruppo di settori come lo mostra la Struttura: di che cosa è fatto, quali settori si possono aprire, e
/// l'elenco scritto.
/// </summary>
/// <param name="Nome">Come lo chiama chi legge: «Settori d'area» non si scrive qui (lo sa la pagina); per un
/// ente è il suo nome.</param>
/// <param name="Settori">I settori fra cui scegliere: i CTR ordinari dell'ACC, o le posizioni dell'ente più gli
/// avvicinamenti che stanno sotto di loro.</param>
/// <param name="Completo">Vero se l'elenco è dichiarato completo, cioè se vincola.</param>
public sealed record GruppoDiSettori(
    ConfigurationGroupKind Genere, string Codice, string Nome,
    IReadOnlyList<AccSectorPick> Settori, IReadOnlyList<AccConfiguration> Configurazioni, bool Completo = false);

/// <summary>
/// Le configurazioni possibili <b>dichiarate</b>: leggerle e riscriverle, gruppo per gruppo.
///
/// <para>È la sola porta di scrittura. Il documento le <b>legge</b> (<see cref="ListAsync"/>) e non le scrive
/// più: fino all'8 ottobre 2026 erano il <c>BodyJson</c> della sua sezione <c>configurations</c>. Carta
/// <c>docs/feature/2026-10-08-configurazioni-possibili.md</c>.</para>
/// </summary>
public interface ISectorConfigurationService
{
    /// <summary>I gruppi di un ACC: i suoi settori d'area per primi, poi i suoi enti per nome.</summary>
    Task<IReadOnlyList<GruppoDiSettori>> GruppiAsync(string accCode, CancellationToken ct = default);

    /// <summary>L'elenco di un gruppo. Vuoto se non ne ha uno.</summary>
    Task<IReadOnlyList<AccConfiguration>> ListAsync(
        ConfigurationGroupKind genere, string codice, CancellationToken ct = default);

    /// <summary>
    /// Sostituisce <b>tutto</b> l'elenco di un gruppo (lista vuota = nessun vincolo). Struttura: vuole il ruolo
    /// e il lock della struttura. Un settore che non è del gruppo è un errore, e si dice quale.
    /// </summary>
    /// <param name="completo">Vero se l'elenco è completo e quindi <b>vincola</b>. Un elenco senza nemmeno un
    /// settore aperto non può esserlo: si scrive spento.</param>
    Task ReplaceAsync(ConfigurationGroupKind genere, string codice, IReadOnlyList<AccConfiguration> configurazioni,
        bool completo, CancellationToken ct = default);

    /// <summary>Tutti gli elenchi, pronti per chi deve chiedere «chi non può restare aperto».</summary>
    Task<ConfigurazioniPossibili> TutteAsync(CancellationToken ct = default);
}

/// <summary>La forma scritta di un elenco: la stessa che aveva nel documento.</summary>
public static class ConfigurazioniJson
{
    /// <summary>Vuoto o malformato = nessuna configurazione: un elenco illeggibile non deve fermare una pagina.</summary>
    public static List<AccConfiguration> Leggi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AccConfiguration>();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<AccConfiguration>>(json) ?? new List<AccConfiguration>(); }
        catch (System.Text.Json.JsonException) { return new List<AccConfiguration>(); }
    }

    /// <summary>Lista vuota = stringa vuota: la riga resta, senza un «[]» da interpretare.</summary>
    public static string Scrivi(IReadOnlyCollection<AccConfiguration>? configurazioni) =>
        configurazioni is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(configurazioni) : "";
}

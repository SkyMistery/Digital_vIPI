using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La configurazione della traduzione dei documenti, presa dai file veri.
///
/// <para><b>Perché esiste.</b> Fino al 28 agosto 2026 la sezione <c>Translation</c> non compariva in
/// nessun <c>appsettings</c>: il cablaggio era completo — motori, catena, memoria, giro dei quindici
/// minuti — e <c>Enabled</c> è falso per default, quindi la funzione era <b>spenta senza che niente lo
/// dicesse</b>. Ora la sezione c'è, ed è fatta di undici chiavi scritte a mano.</para>
///
/// <para>⚠️ <b>Una chiave scritta male non dà nessun errore.</b> Il legame della configurazione è per nome:
/// <c>MaxCaratteriTotale</c> al posto di <c>MaxCaratteriTotali</c> non fallisce, non avvisa e non compare
/// da nessuna parte — si lega a niente, e la proprietà resta al suo default. Il tetto di spesa diventa
/// zero, cioè «nessun tetto», che è il contrario di quello che c'era scritto nel file. È l'unico modo in
/// cui questi file possono mentire, ed è per questo che il test non li LEGGE: li LEGA, e poi guarda che
/// cosa è arrivato dall'altra parte.</para>
///
/// <para>⚠️ Il difetto gemello — la riga giusta nel file sbagliato — lo copre
/// <see cref="LivelliDiLogTests"/>, con lo stesso metodo: si chiede al sistema, non al JSON.</para>
/// </summary>
public sealed class ConfigurazioneTraduzioneTests
{
    /// <summary>Il pacchetto che viaggia: porta la FORMA della sezione, non le chiavi.</summary>
    private static TranslationOptions DalPacchetto()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var opzioni = new TranslationOptions();
        config.GetSection(TranslationOptions.SectionName).Bind(opzioni);
        return opzioni;
    }

    /// <summary>
    /// Ogni chiave del file arriva davvero a una proprietà. Il legame è per nome e in silenzio: una chiave
    /// che non corrisponde a niente viene semplicemente ignorata, e il valore che si legge nel file non è
    /// quello che l'applicazione usa.
    /// </summary>
    [Fact]
    public void Ogni_chiave_scritta_nel_file_si_lega_a_una_proprieta()
    {
        var sezione = SezioneDelPacchetto();
        var ignorate = new List<string>();
        Confronta(sezione, typeof(TranslationOptions), TranslationOptions.SectionName, ignorate);

        Assert.True(ignorate.Count == 0,
            "Chiavi scritte in appsettings.json che non corrispondono a nessuna proprietà: il legame le " +
            "ignora in silenzio e la proprietà resta al suo default, quindi il file dice una cosa e " +
            "l'applicazione ne fa un'altra.\n  " + string.Join("\n  ", ignorate));
    }

    /// <summary>
    /// I valori che contano, letti dall'altra parte del legame. Non è una copia del file: è la prova che i
    /// numeri scritti là arrivano qui.
    /// </summary>
    [Fact]
    public void I_valori_del_pacchetto_arrivano_dove_devono()
    {
        var o = DalPacchetto();

        // ⚠️ SPENTA nel pacchetto, ed è voluto: si accende in appsettings.Production.json. Un ambiente di
        // sviluppo che traduce spende franchigia vera per prove che nessuno leggerà.
        Assert.False(o.Enabled);

        // ⚠️ NESSUNA CHIAVE nei file versionati: user-secrets in sviluppo, cartella «segreti» in
        // produzione. Le righe vuote nel file servono a dire quale chiave si aspetta, non a portarne una.
        Assert.True(string.IsNullOrWhiteSpace(o.Azure.ApiKey), "C'è una chiave Azure in appsettings.json.");
        Assert.True(string.IsNullOrWhiteSpace(o.DeepL.ApiKey), "C'è una chiave DeepL in appsettings.json.");

        // Il tetto di DeepL è la ragione per cui questa sezione ha dei numeri: la sua franchigia è UNA
        // TANTUM e non si rinnova, quindi un tetto cumulativo protegge una riserva. Quello di Azure resta
        // a zero perché la sua si rinnova ogni mese, e un tetto cumulativo lo fermerebbe per sempre al
        // primo mese pieno.
        Assert.True(o.DeepL.MaxCaratteriTotali > 0,
            "Il tetto cumulativo di DeepL è sparito: la franchigia una tantum resta senza difesa.");
        Assert.Equal(0, o.Azure.MaxCaratteriTotali);

        // «EN» secco è deprecato come bersaglio, e l'inglese aeronautico è quello britannico.
        Assert.Equal("EN-GB", o.DeepL.EnglishVariant);
    }

    /// <summary>
    /// ⚠️ Un nome che non corrisponde a nessun motore viene <b>scartato in silenzio</b> da
    /// <c>TranslationFillUseCase</c>, che filtra l'ordine su quelli registrati. Con l'unico nome buono
    /// scritto male, la catena resta vuota e non traduce niente — senza un errore da nessuna parte.
    /// </summary>
    [Fact]
    public void Lordine_nomina_solo_motori_che_esistono()
    {
        var noti = new[] { "azure", "deepl" };
        var o = DalPacchetto();

        Assert.NotEmpty(o.Order);
        var sconosciuti = o.Order.Where(n => !noti.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();

        Assert.True(sconosciuti.Count == 0,
            $"Translation:Order nomina motori che non esistono ({string.Join(", ", sconosciuti)}): la catena " +
            $"li scarta senza dirlo. I nomi sono: {string.Join(", ", noti)}.");
    }

    /// <summary>
    /// Le lingue bersaglio devono essere quelle che il sito serve davvero, o si tradurrebbe verso una
    /// lingua che nessuno può chiedere: caratteri spesi per righe di memoria che nessuna pagina legge.
    /// </summary>
    [Fact]
    public void Le_lingue_bersaglio_sono_quelle_servite()
    {
        var o = DalPacchetto();

        Assert.NotEmpty(o.Targets);
        var estranee = o.Targets.Where(t => !LinguaDiLettura.Supportata(t)).ToList();

        Assert.True(estranee.Count == 0,
            $"Translation:Targets contiene lingue che il sito non serve ({string.Join(", ", estranee)}): " +
            "si spenderebbero caratteri per traduzioni che nessuna pagina può mostrare. Le lingue servite " +
            $"sono in LinguaDiLettura.Supportate: {string.Join(", ", LinguaDiLettura.Supportate)}.");
    }

    /// <summary>
    /// L'interruttore in produzione. ⚠️ È l'unica riga che separa «bilingue» da «sembra bilingue»: senza,
    /// il selettore di lingua c'è lo stesso e le etichette cambiano lo stesso, ma la prosa dei documenti
    /// resta nella lingua in cui è stata scritta — e non lo dice nessun errore, nessun log e nessuna
    /// pagina. Vedi deploy/atc-ivao/LEGGIMI-TRADUZIONE.md.
    /// </summary>
    [Fact]
    public void In_produzione_la_traduzione_e_accesa()
    {
        var percorso = Path.Combine(RadiceDelRepo(), "deploy", "atc-ivao", "appsettings.Production.json");
        Assert.True(File.Exists(percorso), "File di configurazione di produzione non trovato: " + percorso);

        var config = new ConfigurationBuilder().AddJsonFile(percorso, optional: false).Build();

        Assert.True(config.GetValue<bool>($"{TranslationOptions.SectionName}:Enabled"),
            "Translation:Enabled non è true in deploy/atc-ivao/appsettings.Production.json: il sito di " +
            "produzione mostrerebbe i documenti nella lingua in cui sono stati scritti, senza nessun " +
            "segnale. È l'errore che questa funzione fa in silenzio.");
    }

    // ---- attrezzi -------------------------------------------------------------------------------------

    /// <summary>La sezione <c>Translation</c> del pacchetto, come albero JSON grezzo.</summary>
    private static JsonElement SezioneDelPacchetto()
    {
        var percorso = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        Assert.True(File.Exists(percorso), "appsettings.json non copiato accanto ai test: " + percorso);

        using var doc = JsonDocument.Parse(File.ReadAllText(percorso));
        Assert.True(doc.RootElement.TryGetProperty(TranslationOptions.SectionName, out var sezione),
            "appsettings.json non ha la sezione «Translation»: la forma della configurazione torna a non " +
            "essere scritta da nessuna parte, che è il difetto da cui nasce questo file.");

        return sezione.Clone();
    }

    /// <summary>
    /// Cammina l'albero JSON accanto al tipo che dovrebbe riceverlo e raccoglie le chiavi che non trovano
    /// una proprietà. Si scende solo negli oggetti: gli array (<c>Targets</c>, <c>Order</c>) si legano per
    /// posizione e non hanno nomi da sbagliare.
    /// </summary>
    private static void Confronta(JsonElement nodo, Type tipo, string percorso, List<string> ignorate)
    {
        foreach (var proprietaJson in nodo.EnumerateObject())
        {
            // Le righe «//Chiave» sono i commenti di questi file: non sono configurazione.
            if (proprietaJson.Name.StartsWith("//", StringComparison.Ordinal)) continue;

            var proprieta = tipo.GetProperty(proprietaJson.Name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);

            if (proprieta is null)
            {
                ignorate.Add($"{percorso}:{proprietaJson.Name}  (nessuna proprietà su {tipo.Name})");
                continue;
            }

            if (proprietaJson.Value.ValueKind == JsonValueKind.Object)
                Confronta(proprietaJson.Value, proprieta.PropertyType, $"{percorso}:{proprietaJson.Name}", ignorate);
        }
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}

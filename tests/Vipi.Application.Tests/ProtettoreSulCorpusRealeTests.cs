using System.Text.Json;
using System.Text.RegularExpressions;
using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Il cancello sui dati personali provato su <b>tutto il corpus editoriale reale</b>, non su esempi scelti
/// (carta <c>2026-08-27-documenti-bilingue.md</c> §3b, ultimo capoverso: «prova, non promessa»).
///
/// <para>
/// La fixture è l'estrazione del <c>vipi.db</c> del 27 agosto 2026: <b>499 campi</b> — corpi di blocco,
/// JSON di blocco, titoli di sezione e di documento — per 23.344 caratteri. È lo stesso schema con cui
/// <c>real-flows.tsv</c> porta i flussi veri dentro la suite.
/// </para>
///
/// <para>
/// ⚠️ <b>Che cosa questo test prova, e che cosa no — detto prima che qualcuno ci conti sopra.</b> Misurato
/// mentre lo scrivevo: nel corpus di oggi <b>non c'è un solo VID</b> e <b>non c'è un solo nome dello
/// staff</b>. Quindi la prima parte è una <b>rete di regressione</b>, non una dimostrazione: dice che oggi
/// non esce niente, e si accorgerà del giorno in cui qualcuno scriverà un firmatario a mano dentro un
/// blocco di «Validità e revisione» — che è il buco vero individuato dalla carta. A dimostrare che il
/// cancello <b>si chiude davvero</b> serve la seconda parte, che i dati personali dentro il corpus reale ce
/// li mette apposta.
/// </para>
/// </summary>
public class ProtettoreSulCorpusRealeTests
{
    private sealed record Campo(string Kind, string Text);

    /// <summary>Una sequenza da 6 a 8 cifre: la forma di un VID IVAO.</summary>
    private static readonly Regex SequenzaDaVid = new(@"(?<![\d.,])\d{6,8}(?![\d.,])", RegexOptions.Compiled);

    /// <summary>Il nome avvelenato come parola intera: «crossing» non e' un cognome.</summary>
    private static readonly Regex NomeIntero = new(@"(Mario|Rossi)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Nomi finti col taglio di quelli veri: il roster non si committa.</summary>
    private static readonly string[] RosterFinto = { "Mario Rossi", "Giulia Bianchi", "Anna Verdi" };

    private static IReadOnlyList<Campo> Corpus() =>
        JsonSerializer.Deserialize<List<Campo>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "testi-editoriali-reali.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    /// <summary>I segmenti che il traduttore vedrebbe davvero: quelli che il segmentatore estrae.</summary>
    private static IEnumerable<string> SegmentiDi(Campo c) =>
        c.Kind == "json" ? TextSegmenter.SplitJson(c.Text) : TextSegmenter.SplitProse(c.Text);

    [Fact]
    public void La_fixture_e_quella_misurata()
    {
        // Se il corpus cambia, questo test lo dice invece di lasciare che gli altri cambino significato.
        var corpus = Corpus();
        Assert.Equal(499, corpus.Count);
        Assert.Equal(23344, corpus.Sum(c => c.Text.Length));
    }

    [Fact]
    public void Nessun_segmento_del_corpus_reale_porta_fuori_un_dato_personale()
    {
        var protettore = new TextProtector(RosterFinto);
        var corpus = Corpus();
        var spediti = 0;

        foreach (var campo in corpus)
            foreach (var segmento in SegmentiDi(campo))
            {
                if (!TranslationText.HasSomethingToTranslate(segmento)) continue;
                var protetto = protettore.Protect(segmento);
                Assert.True(protetto.Safe, $"segmento non sicuro: {segmento}");
                Assert.DoesNotMatch(SequenzaDaVid, protetto.Text);
                spediti++;
            }

        // Il numero è parte dell'asserzione: se domani il segmentatore ne estraesse la metà, il test sopra
        // resterebbe verde per il motivo sbagliato — «non esce niente» è vero anche quando non esce nulla.
        Assert.True(spediti > 200, $"troppo pochi segmenti spediti: {spediti}");
    }

    [Fact]
    public void Lo_sha_di_un_immagine_non_arriva_nemmeno_al_protettore()
    {
        // ⚠️ Il caso misurato: in tutto il corpus l'UNICA sequenza da 6-8 cifre è lo sha256 dentro il JSON
        // di un blocco immagine. Non esce perché `mediaId` non è nell'elenco di ciò che si traduce — cioè
        // per costruzione, non per fortuna. Questo test è lì perché resti così.
        var conSha = Corpus().Where(c => c.Kind == "json" && c.Text.Contains("mediaId", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(conSha);
        foreach (var campo in conSha)
            Assert.All(TextSegmenter.SplitJson(campo.Text),
                       s => Assert.DoesNotContain("mediaId", s, StringComparison.Ordinal));
    }

    [Fact]
    public void Un_VID_scritto_a_mano_dentro_un_blocco_vero_non_esce()
    {
        // La dimostrazione che al corpus di oggi manca: i dati personali ce li mettiamo noi, dentro i testi
        // veri, nella forma in cui arriverebbero davvero — il firmatario di una vLOA.
        var protettore = new TextProtector(RosterFinto);
        var basi = Corpus().Where(c => c.Kind == "prose").Take(20).Select(c => c.Text).ToList();
        Assert.NotEmpty(basi);

        foreach (var basePros in basi)
            foreach (var veleno in new[] { " Firmato da Mario Rossi.", " VID 123456.", " Riferimento 7654321." })
            {
                var protetto = protettore.Protect(basePros + veleno);
                Assert.True(protetto.Safe);
                // A PAROLA INTERA, e il perche' e' il difetto che questo file ha scoperto: nei testi veri
                // «Rossi» compare dentro «crossing» -- «traffic crossing the common boundary» -- e una
                // asserzione per sottostringa direbbe «dato personale in uscita» su una frase pulita.
                Assert.DoesNotMatch(NomeIntero, protetto.Text);
                Assert.DoesNotMatch(SequenzaDaVid, protetto.Text);
            }
    }

    [Fact]
    public void Il_giro_completo_sul_corpus_reale_non_perde_niente()
    {
        // Con un motore che restituisce il testo immutato, protezione e ripristino devono ridare il
        // normalizzato. Se qui si perdesse un pezzo, si perderebbe anche in produzione — e in produzione
        // sarebbe un callsign sparito da una frase che continua a sembrare giusta.
        var protettore = new TextProtector(RosterFinto);
        foreach (var campo in Corpus())
            foreach (var segmento in SegmentiDi(campo))
            {
                var protetto = protettore.Protect(segmento);
                Assert.True(TextProtector.TryRestore(protetto.Text, protetto.Tokens, out var tornato),
                            $"ripristino fallito: {segmento}");
                Assert.Equal(TranslationText.Normalize(segmento), tornato);
            }
    }
}

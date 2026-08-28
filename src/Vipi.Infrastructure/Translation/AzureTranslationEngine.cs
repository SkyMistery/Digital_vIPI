using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;

namespace Vipi.Infrastructure.Translation;

/// <summary>
/// Il motore Azure AI Translator (Text Translation v3.0) — il <b>primario</b> dal 27 agosto 2026.
///
/// <para>
/// ⚠️ <b>Il testo arriva già protetto</b>: i segnaposto ci sono già e i dati personali non ci sono più.
/// Questa classe non vede mai l'originale.
/// </para>
///
/// <para>
/// ⚠️ <b>Due trappole di Azure, e nessuna delle due si diagnostica da sola.</b>
/// </para>
/// <list type="number">
///   <item>
///   <b>L'intestazione della regione.</b> Su una risorsa regionale o multi-servizio, senza
///   <c>Ocp-Apim-Subscription-Region</c> Azure risponde <b>401</b>. Che somiglia a una chiave sbagliata, e
///   manda a rigenerare una chiave che andava benissimo.
///   </item>
///   <item>
///   <b>Il 403 vuol dire due cose.</b> Chiave rifiutata <i>e</i> quota gratuita esaurita rispondono
///   entrambe 403, e le due azioni sono opposte: chiamare una persona, oppure lasciare che la catena passi
///   all'altro motore. Le distingue solo il <b>codice nel corpo</b> (<c>403000</c> = non autorizzato,
///   <c>403001</c> = quota finita), quindi il corpo si legge anche quando lo stato basterebbe a dire «no».
///   </item>
/// </list>
///
/// <para>
/// Per i segnaposto si usa <c>textType=html</c>: è il modo in cui Azure lascia stare la marcatura.
/// ⚠️ In quella modalità Azure può <b>normalizzare</b> <c>&lt;x id="0"/&gt;</c> in
/// <c>&lt;x id="0"&gt;&lt;/x&gt;</c> — per questo il ripristino accetta entrambe le forme.
/// </para>
///
/// <para>
/// <b>E il glossario di fraseologia non chiede niente a questa classe.</b> I suoi segnaposto
/// (<c>&lt;g id="0" translate="no"&gt;…&lt;/g&gt;</c>) portano già l'attributo che in modalità marcatura
/// Azure onora da sé: il contenuto non si traduce, quindi non si paga e non lo si può rovinare. È la
/// funzione nativa del motore, usata da dove va usata — nel testo — invece che da un ramo di codice che
/// questo motore avrebbe e l'altro no.
/// </para>
/// </summary>
public sealed class AzureTranslationEngine : ITranslationEngine
{
    public const string HttpClientName = "azure-translator";

    /// <summary>Codice che Azure mette nel corpo quando la franchigia è finita: non è un problema di chiave.</summary>
    private const int CodiceQuotaFinita = 403001;

    /// <summary>
    /// ⚠️ Azure vuole il campo <c>Text</c> con la <b>maiuscola</b>, e le estensioni JSON di HttpClient
    /// serializzano di default in camelCase (l'impostazione «web»): senza queste opzioni sul filo finirebbe
    /// <c>text</c>. Non e' detto che Azure protesti — potrebbe semplicemente non vedere il testo.
    /// </summary>
    private static readonly JsonSerializerOptions ComeLoVuoleAzure = new() { PropertyNamingPolicy = null };

    private readonly IHttpClientFactory _factory;
    private readonly TranslationOptions _opt;

    public AzureTranslationEngine(IHttpClientFactory factory, IOptions<TranslationOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    public string Name => "azure";

    public bool IsConfigured => _opt.Enabled && !string.IsNullOrWhiteSpace(_opt.Azure.ApiKey);

    public async Task<TranslationBatch> TranslateAsync(
        IReadOnlyList<string> testi, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (!IsConfigured) return TranslationBatch.Ko(TranslationOutcome.NotConfigured, engine: Name);
        if (testi.Count == 0) return TranslationBatch.Ok(Array.Empty<string>(), Name);

        var risultato = new List<string>(testi.Count);
        var perChiamata = Math.Max(1, _opt.Azure.MaxTextsPerCall);

        for (var i = 0; i < testi.Count; i += perChiamata)
        {
            var lotto = testi.Skip(i).Take(perChiamata).ToList();
            var esito = await UnLottoAsync(lotto, sourceLang, targetLang, ct).ConfigureAwait(false);
            if (esito.Outcome != TranslationOutcome.Ok) return esito;
            risultato.AddRange(esito.Texts!);
        }

        return TranslationBatch.Ok(risultato, Name);
    }

    private async Task<TranslationBatch> UnLottoAsync(
        IReadOnlyList<string> lotto, string sourceLang, string targetLang, CancellationToken ct)
    {
        var da = CodiceLingua(sourceLang);
        var a = CodiceLingua(targetLang);
        var url = $"/translate?api-version=3.0&from={da}&to={a}&textType=html";

        HttpResponseMessage risposta;
        string corpoGrezzo;
        try
        {
            var http = _factory.CreateClient(HttpClientName);
            http.BaseAddress ??= new Uri(_opt.Azure.BaseUrl);

            using var richiesta = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(lotto.Select(t => new { Text = t }).ToArray(), options: ComeLoVuoleAzure),
            };
            richiesta.Headers.Add("Ocp-Apim-Subscription-Key", _opt.Azure.ApiKey);
            // ⚠️ Senza questa, una risorsa regionale risponde 401 — vedi l'avviso sulla classe.
            if (!string.IsNullOrWhiteSpace(_opt.Azure.Region))
                richiesta.Headers.Add("Ocp-Apim-Subscription-Region", _opt.Azure.Region);

            risposta = await http.SendAsync(richiesta, ct).ConfigureAwait(false);
            corpoGrezzo = await risposta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return TranslationBatch.Ko(TranslationOutcome.TemporaryFailure, e.GetType().Name, Name);
        }

        if (!risposta.IsSuccessStatusCode)
            return TranslationBatch.Ko(EsitoDi(risposta.StatusCode, corpoGrezzo),
                                       $"HTTP {(int)risposta.StatusCode}", Name);

        List<string>? tradotti;
        try
        {
            tradotti = LeggiTraduzioni(corpoGrezzo);
        }
        catch (JsonException)
        {
            return TranslationBatch.Ko(TranslationOutcome.PermanentFailure, "risposta illeggibile", Name);
        }

        if (tradotti is null || tradotti.Count != lotto.Count)
            // Stesso contratto di DeepL, e stessa ragione: chi chiama riaccoppia per POSIZIONE, e un
            // conteggio diverso accoppierebbe la traduzione di una frase con l'impronta di un'altra.
            return TranslationBatch.Ko(TranslationOutcome.PermanentFailure,
                $"attesi {lotto.Count} testi, arrivati {tradotti?.Count ?? 0}", Name);

        return TranslationBatch.Ok(tradotti, Name);
    }

    /// <summary>
    /// La risposta è un array, uno per testo chiesto: <c>[{"translations":[{"text":"…","to":"en"}]}]</c>.
    /// Si prende la prima traduzione di ciascuno — ne abbiamo chiesta una lingua sola.
    /// </summary>
    private static List<string>? LeggiTraduzioni(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        var fuori = new List<string>();
        foreach (var voce in doc.RootElement.EnumerateArray())
        {
            if (!voce.TryGetProperty("translations", out var tr) || tr.ValueKind != JsonValueKind.Array) return null;
            var prima = tr.EnumerateArray().FirstOrDefault();
            if (prima.ValueKind != JsonValueKind.Object || !prima.TryGetProperty("text", out var testo)) return null;
            fuori.Add(testo.GetString() ?? "");
        }
        return fuori;
    }

    /// <summary>
    /// Che cosa fare, dato lo stato <b>e il corpo</b>.
    /// <para>⚠️ Il corpo serve solo per il 403, ma serve davvero: chiave rifiutata e quota finita hanno lo
    /// stesso stato e azioni opposte.</para>
    /// </summary>
    private static TranslationOutcome EsitoDi(HttpStatusCode stato, string corpo) => stato switch
    {
        HttpStatusCode.Unauthorized => TranslationOutcome.AuthFailed,
        HttpStatusCode.Forbidden => QuotaFinita(corpo) ? TranslationOutcome.QuotaExceeded : TranslationOutcome.AuthFailed,
        HttpStatusCode.TooManyRequests => TranslationOutcome.TemporaryFailure,
        >= HttpStatusCode.InternalServerError => TranslationOutcome.TemporaryFailure,
        _ => TranslationOutcome.PermanentFailure,
    };

    private static bool QuotaFinita(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            return doc.RootElement.TryGetProperty("error", out var err)
                   && err.TryGetProperty("code", out var codice)
                   && codice.TryGetInt32(out var n)
                   && n == CodiceQuotaFinita;
        }
        catch (JsonException)
        {
            // Corpo illeggibile su un 403: si sceglie l'ipotesi che NON consuma l'altro motore. Se fosse
            // davvero quota, il giro dopo lo dirà con un corpo leggibile.
            return false;
        }
    }

    /// <summary>Azure vuole i codici brevi: <c>it</c>, <c>en</c>. Niente varianti come su DeepL.</summary>
    private static string CodiceLingua(string lang) =>
        lang.Equals("it", StringComparison.OrdinalIgnoreCase) ? "it" : "en";
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;

namespace Vipi.Infrastructure.Translation;

/// <summary>
/// Il motore DeepL (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>Il testo arriva già protetto</b>: i segnaposto <c>&lt;x id="0"/&gt;</c> ci sono già e i dati
/// personali non ci sono più. Questa classe non guarda mai l'originale — non ce l'ha.
/// </para>
///
/// <para>
/// ⚠️ <b>La trappola dell'XML.</b> Si chiede a DeepL <c>tag_handling=xml</c>, che è l'unico modo perché
/// rispetti i segnaposto. Ma da quel momento il testo <b>deve essere XML valido</b>, e la prosa vera non lo
/// è: basta una <c>&amp;</c> in «Roma &amp; Milano» o un <c>&lt;</c> in «traffico &lt; FL100» e la richiesta
/// torna con un errore che non parla di caratteri. Quindi si <b>protegge il segnaposto, si scappa il
/// resto, e si rimette il segnaposto</b> — in quest'ordine, o si scapperebbero anche le parentesi angolari
/// dei segnaposto, che è esattamente ciò che li rende invisibili al motore.
/// </para>
/// </summary>
public sealed partial class DeepLTranslationEngine : ITranslationEngine
{
    public const string HttpClientName = "deepl";

    /// <summary>Le chiavi del piano gratuito finiscono così, e vogliono un altro server.</summary>
    private const string SuffissoChiaveGratuita = ":fx";

    private const string BaseGratuita = "https://api-free.deepl.com";
    private const string BasePagata = "https://api.deepl.com";

    private readonly IHttpClientFactory _factory;
    private readonly TranslationOptions _opt;

    public DeepLTranslationEngine(IHttpClientFactory factory, IOptions<TranslationOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    public string Name => "deepl";

    public bool IsConfigured => _opt.Enabled && !string.IsNullOrWhiteSpace(_opt.DeepL.ApiKey);

    /// <summary>Il segnaposto del protettore, che qui va tenuto fuori dalla fuga XML.</summary>
    [GeneratedRegex(@"<x id=""\d+""\s*/>")]
    private static partial Regex Segnaposto();

    public async Task<TranslationBatch> TranslateAsync(
        IReadOnlyList<string> testi, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (!IsConfigured) return TranslationBatch.Ko(TranslationOutcome.NotConfigured, engine: Name);
        if (testi.Count == 0) return TranslationBatch.Ok(Array.Empty<string>(), Name);

        var risultato = new List<string>(testi.Count);

        // A lotti: DeepL ne accetta 50 per chiamata, e chi ci chiama non deve saperlo.
        for (var i = 0; i < testi.Count; i += Math.Max(1, _opt.DeepL.MaxTextsPerCall))
        {
            var lotto = testi.Skip(i).Take(Math.Max(1, _opt.DeepL.MaxTextsPerCall)).ToList();
            var esito = await UnLottoAsync(lotto, sourceLang, targetLang, ct).ConfigureAwait(false);
            if (esito.Outcome != TranslationOutcome.Ok) return esito;   // un lotto rotto ferma tutto
            risultato.AddRange(esito.Texts!);
        }

        return TranslationBatch.Ok(risultato, Name);
    }

    private async Task<TranslationBatch> UnLottoAsync(
        IReadOnlyList<string> lotto, string sourceLang, string targetLang, CancellationToken ct)
    {
        var richiesta = new RichiestaDeepL
        {
            text = lotto.Select(ScappaTenendoISegnaposto).ToArray(),
            source_lang = CodiceSorgente(sourceLang),
            target_lang = CodiceBersaglio(targetLang),
            tag_handling = "xml",
            ignore_tags = new[] { "x" },
            glossary_id = string.IsNullOrWhiteSpace(_opt.DeepL.GlossaryId) ? null : _opt.DeepL.GlossaryId,
        };

        HttpResponseMessage risposta;
        try
        {
            var http = _factory.CreateClient(HttpClientName);
            http.BaseAddress ??= new Uri(BaseDedotta());
            http.DefaultRequestHeaders.Remove("Authorization");
            http.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {_opt.DeepL.ApiKey}");
            risposta = await http.PostAsJsonAsync("/v2/translate", richiesta, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // La rete non c'è o ha impiegato troppo: si riprova al giro dopo, non è colpa del testo.
            return TranslationBatch.Ko(TranslationOutcome.TemporaryFailure, e.GetType().Name, Name);
        }

        var esito = EsitoDi(risposta.StatusCode);
        if (esito != TranslationOutcome.Ok)
            return TranslationBatch.Ko(esito, $"HTTP {(int)risposta.StatusCode}", Name);

        RispostaDeepL? corpo;
        try
        {
            corpo = await risposta.Content.ReadFromJsonAsync<RispostaDeepL>(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or HttpRequestException)
        {
            return TranslationBatch.Ko(TranslationOutcome.PermanentFailure, "risposta illeggibile", Name);
        }

        var tradotti = corpo?.translations;
        if (tradotti is null || tradotti.Count != lotto.Count)
            // ⚠️ Il contratto è «uno per ingresso, nello stesso ordine»: chi ci chiama riaccoppia per
            // POSIZIONE. Un conteggio diverso non si aggiusta a naso — accoppierebbe la traduzione di una
            // frase con l'impronta di un'altra, e la memoria resterebbe sbagliata per sempre.
            return TranslationBatch.Ko(TranslationOutcome.PermanentFailure,
                $"attesi {lotto.Count} testi, arrivati {tradotti?.Count ?? 0}", Name);

        return TranslationBatch.Ok(tradotti.Select(t => Rientra(t.text ?? "")).ToList(), Name);
    }

    /// <summary>
    /// I codici di stato che contano, e le tre azioni che ne discendono: chiamare una persona, aspettare il
    /// periodo nuovo, riprovare fra poco.
    /// </summary>
    private static TranslationOutcome EsitoDi(HttpStatusCode stato) => stato switch
    {
        HttpStatusCode.OK => TranslationOutcome.Ok,
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => TranslationOutcome.AuthFailed,
        (HttpStatusCode)456 => TranslationOutcome.QuotaExceeded,   // il codice che DeepL usa per la quota
        HttpStatusCode.TooManyRequests => TranslationOutcome.TemporaryFailure,
        >= HttpStatusCode.InternalServerError => TranslationOutcome.TemporaryFailure,
        _ => TranslationOutcome.PermanentFailure,
    };

    /// <summary>
    /// Il server giusto per questa chiave. ⚠️ Puntare all'altro risponde 403, che somiglia a una chiave
    /// scaduta e manda a cercare il guasto dalla parte opposta.
    /// </summary>
    private string BaseDedotta()
    {
        if (!string.IsNullOrWhiteSpace(_opt.DeepL.BaseUrl)) return _opt.DeepL.BaseUrl;
        return _opt.DeepL.ApiKey!.TrimEnd().EndsWith(SuffissoChiaveGratuita, StringComparison.OrdinalIgnoreCase)
            ? BaseGratuita
            : BasePagata;
    }

    private static string CodiceSorgente(string lang) => lang.ToUpperInvariant() switch
    {
        "IT" => "IT",
        _ => "EN",
    };

    private string CodiceBersaglio(string lang) => lang.ToUpperInvariant() switch
    {
        "IT" => "IT",
        // ⚠️ «EN» secco è deprecato come bersaglio, e l'inglese aeronautico è quello britannico.
        _ => _opt.DeepL.EnglishVariant,
    };

    /// <summary>
    /// Rende il testo XML valido <b>senza toccare i segnaposto</b>. L'ordine è l'unica cosa che conta: si
    /// mettono da parte i segnaposto, si scappa tutto il resto, si rimettono. Al contrario si scapperebbero
    /// le parentesi angolari dei segnaposto, e il motore non li vedrebbe più come tag.
    /// </summary>
    public static string ScappaTenendoISegnaposto(string testo)
    {
        // Una passata sola sui pezzi FRA un segnaposto e l'altro. Niente sentinelle: una sentinella va
        // scelta fra caratteri che il testo non puo' contenere, e un testo scritto da una persona puo'
        // contenere qualunque cosa -- caratteri di controllo compresi, se e' stato incollato da un PDF.
        var sb = new StringBuilder(testo.Length + 16);
        var da = 0;
        foreach (Match m in Segnaposto().Matches(testo))
        {
            sb.Append(Scappa(testo.Substring(da, m.Index - da)));
            sb.Append(m.Value);            // il segnaposto passa intatto: e' cio' che lo rende un tag
            da = m.Index + m.Length;
        }
        return sb.Append(Scappa(testo.Substring(da))).ToString();
    }

    private static string Scappa(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Disfa la fuga XML sul testo tornato. <c>&amp;amp;</c> per ultimo, o si disferebbe due volte.</summary>
    public static string Rientra(string testo) =>
        testo.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

    // ---- Forma del filo -------------------------------------------------------------------------------

    private sealed class RichiestaDeepL
    {
        public string[] text { get; set; } = Array.Empty<string>();
        public string? source_lang { get; set; }
        public string? target_lang { get; set; }
        public string? tag_handling { get; set; }
        public string[]? ignore_tags { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? glossary_id { get; set; }
    }

    private sealed class RispostaDeepL
    {
        public List<TestoTradotto>? translations { get; set; }
    }

    private sealed class TestoTradotto
    {
        public string? text { get; set; }
        public string? detected_source_language { get; set; }
    }
}

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <see cref="GitHubSidSourceRelease"/>: il ciclo che la sorgente dichiara, il ripiego sulla data di
/// cambiamento, e il fatto che <b>non solleva mai</b>. Carta 2026-09-02 §AW2.
///
/// <para>I corpi di risposta sono ritagliati da quelli veri, letti il 2 settembre 2026: l'elenco del
/// <c>CHANGELOG</c> conteneva <c>2608.txt</c> come nome più alto — e <b>non</b> un <c>2609.txt</c> — e la API
/// dei commit dava <c>2026-09-01T12:54:57Z</c> per la cartella dei file di settore.</para>
/// </summary>
public class GitHubSidSourceReleaseTests
{
    private const string ChangelogUrl = "https://api.test/contents/CHANGELOG";
    private const string CommitsUrl = "https://api.test/commits";

    /// <summary>Nomi presi dalla cartella vera, disordinati apposta e con la revisione intermedia <c>2304_1</c>.</summary>
    private const string ElencoChangelog = """
        [{"name":"2306.txt","type":"file"},
         {"name":"2304_1.txt","type":"file"},
         {"name":"2608.txt","type":"file"},
         {"name":"2401.txt","type":"file"},
         {"name":"2607.txt","type":"file"},
         {"name":"LEGGIMI.md","type":"file"}]
        """;

    private const string UnCommit = """
        [{"sha":"69d50e","commit":{"author":{"date":"2026-08-30T09:00:00Z"},
                                   "committer":{"date":"2026-09-01T12:54:57Z"}}}]
        """;

    private sealed class Handler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, (HttpStatusCode Code, string Body)> _risposte;
        public List<string> Chiamate { get; } = new();

        public Handler(IReadOnlyDictionary<string, (HttpStatusCode, string)> risposte) => _risposte = risposte;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            lock (Chiamate) Chiamate.Add(url);
            return Task.FromResult(_risposte.TryGetValue(url, out var r)
                ? new HttpResponseMessage(r.Code) { Content = new StringContent(r.Body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static GitHubSidSourceRelease Costruisci(Handler h, SectorfileCache? cache = null) =>
        new(new HttpClient(h, disposeHandler: false),
            Options.Create(new SectorfileOptions { SidChangelogUrl = ChangelogUrl, SidCommitsUrl = CommitsUrl }),
            cache ?? new SectorfileCache(),
            new AiracService(),
            NullLogger<GitHubSidSourceRelease>.Instance);

    private static Handler Con(params (string Url, HttpStatusCode Code, string Body)[] r) =>
        new(r.ToDictionary(x => x.Url, x => (x.Code, x.Body)));

    /// <summary>Fra i nomi vince il ciclo più avanti — e il confronto NON è alfabetico, vedi sotto.</summary>
    [Fact]
    public async Task Legge_il_ciclo_dichiarato_piu_recente()
    {
        var esito = await Costruisci(Con((ChangelogUrl, HttpStatusCode.OK, ElencoChangelog))).ReadAsync();

        Assert.Equal("2608", esito.DeclaredCycle);
        Assert.Null(esito.LastChangedUtc);   // il ripiego non si chiede nemmeno
    }

    /// <summary>
    /// ⚠️ «2701» viene dopo «2613», ma non in ordine alfabetico: il più alto si sceglie sulla <b>data
    /// efficace</b> del ciclo che il nome dichiara. È lo stesso inciampo già pagato in <c>ShapeAiracGate</c>.
    /// </summary>
    [Fact]
    public async Task Il_confronto_fra_cicli_e_sulle_date_non_sulle_stringhe()
    {
        var elenco = """[{"name":"2613.txt"},{"name":"2701.txt"}]""";
        var esito = await Costruisci(Con((ChangelogUrl, HttpStatusCode.OK, elenco))).ReadAsync();

        Assert.Equal("2701", esito.DeclaredCycle);
    }

    /// <summary>Senza changelog leggibile si scende alla data dell'ultimo commit, che è il ripiego dichiarato.</summary>
    [Fact]
    public async Task Senza_changelog_ripiega_sulla_data_di_cambiamento()
    {
        var esito = await Costruisci(Con(
            (ChangelogUrl, HttpStatusCode.NotFound, ""),
            (CommitsUrl, HttpStatusCode.OK, UnCommit))).ReadAsync();

        Assert.Null(esito.DeclaredCycle);
        Assert.Equal(new DateTime(2026, 9, 1, 12, 54, 57, DateTimeKind.Utc), esito.LastChangedUtc);
    }

    /// <summary>
    /// ⚠️ Si legge <c>committer</c> e non <c>author</c>: sul corpo vero le due date differiscono, e quella
    /// che conta è quando il file è arrivato nel repo che scarichiamo.
    /// </summary>
    [Fact]
    public async Task Della_data_conta_il_committer_non_lautore()
    {
        var esito = await Costruisci(Con(
            (ChangelogUrl, HttpStatusCode.NotFound, ""),
            (CommitsUrl, HttpStatusCode.OK, UnCommit))).ReadAsync();

        Assert.NotEqual(new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc), esito.LastChangedUtc);
    }

    /// <summary>
    /// ⚠️ <b>La radice può essere un oggetto</b>: è la forma degli errori di GitHub, e <c>EnumerateArray</c>
    /// su un oggetto solleva <c>InvalidOperationException</c> — non <c>JsonException</c>, quindi non basta
    /// catturare quella. Inciampo già pagato altrove in questo progetto.
    /// </summary>
    [Fact]
    public async Task Un_errore_di_quota_non_solleva_e_torna_muta()
    {
        var quota = """{"message":"API rate limit exceeded","documentation_url":"https://docs.github.com"}""";
        var esito = await Costruisci(Con(
            (ChangelogUrl, HttpStatusCode.Forbidden, quota),
            (CommitsUrl, HttpStatusCode.Forbidden, quota))).ReadAsync();

        Assert.Null(esito.DeclaredCycle);
        Assert.Null(esito.LastChangedUtc);
    }

    /// <summary>Un 200 con dentro l'oggetto d'errore: stessa strada, nessuna eccezione.</summary>
    [Fact]
    public async Task Una_radice_che_non_e_un_array_non_solleva()
    {
        var esito = await Costruisci(Con(
            (ChangelogUrl, HttpStatusCode.OK, """{"message":"nope"}"""),
            (CommitsUrl, HttpStatusCode.OK, """{"message":"nope"}"""))).ReadAsync();

        Assert.Equal(Vipi.Application.Abstractions.SidSourceRelease.Muta, esito);
    }

    /// <summary>
    /// Il giro d'import chiama <c>ImportAsync</c> una volta per aeroporto — decine — e questa risposta non
    /// dipende dall'ICAO: senza cache sarebbe una chiamata per scalo, cioè la quota anonima esaurita a metà
    /// giro. ⚠️ Vale <b>anche</b> per il «non lo so»: un 403 richiesto trentanove volte dà trentanove 403.
    /// </summary>
    [Fact]
    public async Task Si_chiede_una_volta_sola_per_giro()
    {
        var h = Con((ChangelogUrl, HttpStatusCode.Forbidden, """{"message":"quota"}"""),
                    (CommitsUrl, HttpStatusCode.Forbidden, """{"message":"quota"}"""));
        var cache = new SectorfileCache();
        var sut = Costruisci(h, cache);

        for (var i = 0; i < 5; i++) await sut.ReadAsync();

        Assert.Equal(2, h.Chiamate.Count);   // changelog + ripiego, una volta sola

        cache.Invalidate();                  // il giro dopo riparte dai file
        await sut.ReadAsync();
        Assert.Equal(4, h.Chiamate.Count);
    }
}

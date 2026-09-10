using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Auth;
using Vipi.Host;
using Xunit;
using Vipi.Domain;

namespace Vipi.E2E.Tests;

/// <summary>
/// Quando una pagina muore, l'utente deve capire che cosa fare e noi dobbiamo poter capire perché.
///
/// <para>Fino al 24 agosto 2026 non succedeva né l'una né l'altra cosa: usciva la <c>Error.razor</c> del
/// modello di progetto — inglese, senza marchio, con tre paragrafi su come si accende la modalità di
/// sviluppo — e dell'eccezione non restava traccia leggibile, perché su <c>atc.it.ivao.aero</c> i log del
/// processo non li legge nessuno (niente shell, solo FTP).</para>
///
/// <para>⚠️ L'ambiente è <b>Staging</b> e non Development di proposito: <c>UseExceptionHandler</c> è
/// montato solo fuori da Development, quindi in Development questa strada non esiste proprio.</para>
/// </summary>
public sealed class PaginaErroreTests
{
    [Fact]
    public async Task Una_pagina_che_muore_da_la_pagina_nostra_e_lascia_una_riga()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        Assert.NotNull(registro);
        if (File.Exists(registro)) File.Delete(registro);

        using var fabbrica = new FabbricaRotta();
        // ⚠️ La lingua si CHIEDE, non si eredita dalla macchina: da quando la pagina la segue, un test
        // che asserisce l'italiano senza dirlo passa in Italia e cade su una macchina inglese.
        var richiesta = new HttpRequestMessage(HttpMethod.Get, "/services");
        richiesta.Headers.AcceptLanguage.ParseAdd("it");
        var res = await fabbrica.CreateClient().SendAsync(richiesta);
        var html = await res.Content.ReadAsStringAsync();

        // 1. Lo stato resta 500: le sonde devono continuare a vedere un guasto come tale.
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);

        // 2. La pagina è la nostra, e parla a chi la legge.
        Assert.Contains("Questa pagina non si è aperta", html);
        Assert.Contains("/services/vsop", html);

        // 3. Dell'eccezione in pagina non entra niente: né tipo né messaggio.
        Assert.DoesNotContain("InvalidOperationException", html);
        Assert.DoesNotContain(Rotto.Messaggio, html);

        // 4. Il codice mostrato è quello scritto nel registro: è il filo fra la fotografia e lo stack trace.
        var codice = Codice(html);
        var righe = await File.ReadAllTextAsync(registro!);
        Assert.Contains(codice, righe);
        Assert.Contains(Rotto.Messaggio, righe);
        Assert.Contains("GET /services", righe);
    }

    /// <summary>
    /// La pagina d'errore segue la lingua di chi legge — e questo test prova <b>più</b> di quanto sembri.
    ///
    /// <para>⚠️ <b>La parte non ovvia è che la cultura sia già risolta quando la pagina si compone.</b>
    /// <c>UseExceptionHandler("/Error")</c> non scrive una risposta al volo: <b>ri-esegue</b> la pipeline su
    /// <c>/Error</c>, e in quel secondo giro <c>UseRequestLocalization</c> passa prima dell'endpoint. Se
    /// invece la pagina si fosse composta dove l'eccezione è stata <i>catturata</i>, la cultura non sarebbe
    /// stata quella della richiesta: le modifiche a <c>CurrentUICulture</c> scendono lungo la catena di
    /// chiamate, non risalgono. Da una prova a tavolino questo non si vede; da qui sì.</para>
    ///
    /// <para>⚠️ Nel giro di ri-esecuzione la stringa di query originale <b>non c'è più</b> — l'indirizzo è
    /// <c>/Error</c> — quindi un <c>?culture=</c> non arriverebbe. Restano il <b>cookie</b> e
    /// <c>Accept-Language</c>, ed è per questo che la lingua si ricorda nel cookie
    /// (<c>CultureCookieMiddleware</c>).</para>
    /// </summary>
    [Theory]
    [InlineData("en", "en", "This page did not open", "Questa pagina")]
    [InlineData("it", "it", "Questa pagina non si è aperta", "This page did not open")]
    // Una lingua che non serviamo ricade sull'italiano — e `lang` deve dirlo, o a un lettore di schermo la
    // pagina dichiara una lingua che non è quella che ha dentro.
    [InlineData("de", "it", "Questa pagina non si è aperta", "This page did not open")]
    public async Task La_pagina_d_errore_segue_la_lingua_di_chi_legge(
        string chiesta, string attesa, string ciDeveEssere, string nonCiDeveEssere)
    {
        using var fabbrica = new FabbricaRotta();
        var richiesta = new HttpRequestMessage(HttpMethod.Get, "/services");
        richiesta.Headers.AcceptLanguage.ParseAdd(chiesta);

        var res = await fabbrica.CreateClient().SendAsync(richiesta);
        var html = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Contains($"<html lang=\"{attesa}\">", html);
        Assert.Contains(ciDeveEssere, html);
        Assert.DoesNotContain(nonCiDeveEssere, html);
    }

    /// <summary>La stringa di query non deve finire nel registro: su /signin-oidc è una credenziale.</summary>
    [Fact]
    public async Task La_stringa_di_query_non_finisce_nel_registro()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        if (File.Exists(registro)) File.Delete(registro);

        using var fabbrica = new FabbricaRotta();
        await fabbrica.CreateClient().GetAsync("/services?code=SEGRETISSIMO");

        var righe = await File.ReadAllTextAsync(registro!);
        Assert.Contains("GET /services", righe);
        Assert.DoesNotContain("SEGRETISSIMO", righe);
    }

    /// <summary>
    /// 🔴 <b>La pagina c'era, la riga no.</b> Il 10 settembre 2026 un socio ha visto la pagina d'errore
    /// dopo il login e nel registro non c'era niente per quell'ora. Le due spiegazioni — «la riga non si è
    /// scritta» e «non c'era nessuna eccezione da scrivere» — portano a indagini opposte, e dalla pagina non
    /// si distinguono: è la stessa. <c>/Error</c> è un endpoint come un altro, e ci si arriva anche a piedi.
    /// </summary>
    [Fact]
    public async Task La_pagina_raggiunta_SENZA_eccezione_lo_dice_nel_registro()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        Assert.NotNull(registro);
        if (File.Exists(registro)) File.Delete(registro);

        using var fabbrica = new FabbricaRotta();
        // ⚠️ La query c'è apposta: nel Referer di una navigazione interna ci finisce, e se si veniva dal
        // callback quella query è il `code` OAuth.
        var richiesta = new HttpRequestMessage(HttpMethod.Get, "/Error");
        richiesta.Headers.Referrer = new Uri("https://atc.it.ivao.aero/signin-oidc?code=SEGRETISSIMO&state=xyz");
        var res = await fabbrica.CreateClient().SendAsync(richiesta);

        Assert.Equal(HttpStatusCode.OK, await Task.FromResult(res.StatusCode));
        var righe = await File.ReadAllTextAsync(registro!);

        Assert.Contains("SENZA eccezione", righe);
        Assert.Contains("/signin-oidc", righe);
        Assert.DoesNotContain("SEGRETISSIMO", righe);
        Assert.DoesNotContain("xyz", righe);
        // Una riga, non uno stack: non è un guasto, è un fatto che serve a leggere gli altri.
        // \u000A e non Environment.NewLine: il file nasce da un raw string literal e cresce per
        // append, quindi dentro ci convivono le due fini-riga. Splittare su quella sbagliata dà UN pezzo
        // solo — cioè tutto il file — e l'asserzione guarda l'intestazione credendo di guardare la nota.
        var nota = righe.Split('\u000A', StringSplitOptions.RemoveEmptyEntries)
                        .First(r => r.Contains("SENZA eccezione")).TrimEnd('\u000D');
        Assert.StartsWith("NOTA", nota);
    }

    /// <summary>
    /// 🔴 <b>39 voci su 71.</b> Nel file sceso dal server il 7 settembre 2026 più di metà del registro era
    /// <c>ObjectDisposedException</c> sul <c>VipiDbContext</c> di circuiti chiusi — il prezzo normale di chi
    /// chiude la scheda mentre un caricamento è in volo. Non sono guasti, ma sono lunghe uno stack l'una, e
    /// i due difetti VERI di quella finestra ci sono finiti in mezzo. Un registro fatto per metà di rumore è
    /// un registro che si smette di leggere — e che ruota via a 512 kB portandosi la storia che serviva.
    ///
    /// <para>Quindi: la PRIMA di ogni famiglia intera, con il suo stack; dalla seconda in poi una riga.</para>
    /// </summary>
    [Fact]
    public async Task Il_rumore_di_chi_se_ne_va_vale_una_riga_dalla_seconda_volta()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        Assert.NotNull(registro);
        if (File.Exists(registro)) File.Delete(registro);

        var smaltito = Cattura(new ObjectDisposedException("VipiDbContext"));

        DiagnosticaErrori.Registra("uno", "GET", "/services/vsop/lipz", null, smaltito);
        DiagnosticaErrori.Registra("due", "GET", "/services/vsop/lipz", null, smaltito);

        var righe = await File.ReadAllTextAsync(registro!);

        // La prima volta si scrive intera: uno stack di esempio serve a capire da dove nasce la famiglia.
        Assert.Contains("ObjectDisposedException", righe);
        Assert.Contains("Cattura", righe);

        // La seconda vale una riga sola, e la riga porta comunque tipo, punto nostro e percorso.
        var note = righe.Split(Environment.NewLine).Where(r => r.StartsWith("NOTA ", StringComparison.Ordinal)).ToList();
        var nota = Assert.Single(note);
        Assert.Contains("ObjectDisposedException", nota);
        Assert.Contains("Vipi.E2E.Tests", nota);
        Assert.Contains("/services/vsop/lipz", nota);
    }

    /// <summary>
    /// 🔴 <b>E non si demoliscono tutte le <c>ObjectDisposedException</c>.</b> Il 4 settembre 2026 il difetto
    /// era proprio una di quelle — su un <c>SemaphoreSlim</c> smaltito, non su un <c>DbContext</c> — e
    /// abbatteva il circuito. Una regola scritta sul TIPO l'avrebbe nascosta; questa è scritta sull'OGGETTO.
    /// </summary>
    [Fact]
    public async Task Un_semaforo_smaltito_resta_un_guasto_intero()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile);
        if (File.Exists(registro)) File.Delete(registro);

        var semaforo = Cattura(new ObjectDisposedException("SemaphoreSlim"));
        DiagnosticaErrori.Registra("uno", "GET", "/services/vsop/lipz", null, semaforo);
        DiagnosticaErrori.Registra("due", "GET", "/services/vsop/lipz", null, semaforo);

        var righe = await File.ReadAllTextAsync(registro!);
        // ⚠️ Si guardano le RIGHE, non il testo: «NOTA» compare anche nell'intestazione del file, che
        // quella distinzione la spiega — e cercarla nel testo intero farebbe passare il test per il
        // motivo sbagliato.
        Assert.DoesNotContain(righe.Split(Environment.NewLine), r => r.StartsWith("NOTA ", StringComparison.Ordinal));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(righe, "SemaphoreSlim").Count);
    }

    /// <summary>Un'eccezione con uno stack VERO: la firma di una nota è il tipo più il primo fotogramma
    /// nostro, e senza stack non ci sarebbe niente da distinguere.</summary>
    private static Exception Cattura(Exception da)
    {
        try { throw da; }
        catch (Exception ex) { return ex; }
    }

    /// <summary>Il codice mostrato in pagina, dal riquadro in fondo.</summary>
    private static string Codice(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, @"<span class=""m"">([^<]+)</span>");
        Assert.True(m.Success, "la pagina d'errore non mostra nessun codice: senza, una segnalazione non è " +
                               "rintracciabile e il registro serve a metà.\n" + html[..Math.Min(800, html.Length)]);
        return m.Groups[1].Value;
    }

    /// <summary>
    /// L'identità non si risolve: è un guasto DENTRO il layout condiviso — e non è guardato di proposito,
    /// perché senza sapere chi sei la pagina non può nemmeno decidere che cosa mostrarti. È il caso che una
    /// pagina d'errore fatta di componenti non sopravviverebbe: passando dallo stesso layout, lancerebbe una
    /// seconda volta e l'utente resterebbe davanti a una risposta vuota.
    ///
    /// <para>⚠️ Prima qui c'era un catalogo ACC illeggibile. Non serve più: da oggi quel caso <b>degrada</b>
    /// invece di morire (<c>BarraNonAffondaLaPaginaTests</c>), e un test che pretende un 500 da una strada
    /// che ora regge proverebbe il contrario di quello che vogliamo.</para>
    /// </summary>
    private sealed class Rotto : IEditAuthorizationService
    {
        internal const string Messaggio = "autorizzazione non risolvibile (simulato)";
        private static Exception Giu() => new InvalidOperationException(Messaggio);

        // È ciò che il layout chiede per decidere che cosa mostrare, e non è guardato di proposito.
        public bool IsAdmin => throw Giu();
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;

        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() => throw Giu();
    }

    private sealed class FabbricaRotta : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-errore-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<IEditAuthorizationService>();
                s.AddScoped<IEditAuthorizationService, Rotto>();
            });
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }
}
